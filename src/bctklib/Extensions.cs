
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Neo.BlockchainToolkit
{
    public static class Extensions
    {
        public static ExpressChain LoadChain(this IFileSystem fileSystem, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(path);
            using var streamReader = new System.IO.StreamReader(stream);
            using var reader = new JsonTextReader(streamReader);
            return serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {path}");
        }

        public static void SaveChain(this IFileSystem fileSystem, ExpressChain chain, string path)
        {
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var streamWriter = new System.IO.StreamWriter(stream);
            using var writer = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented };
            serializer.Serialize(writer, chain);
        }

        public static ExpressChain FindChain(this IFileSystem fileSystem, string fileName = Constants.DEFAULT_EXPRESS_FILENAME, string? searchFolder = null)
        {
            if (fileSystem.TryFindChain(out var chain, fileName, searchFolder)) return chain;
            throw new Exception($"{fileName} Neo-Express file not found");
        }

        public static bool TryFindChain(this IFileSystem fileSystem, [MaybeNullWhen(false)] out ExpressChain chain, string fileName = Constants.DEFAULT_EXPRESS_FILENAME, string? searchFolder = null)
        {
            searchFolder ??= fileSystem.Directory.GetCurrentDirectory();
            while (searchFolder != null)
            {
                var filePath = fileSystem.Path.Combine(searchFolder, fileName);
                if (fileSystem.File.Exists(filePath))
                {
                    chain = fileSystem.LoadChain(filePath);
                    return true;
                }

                searchFolder = fileSystem.Path.GetDirectoryName(searchFolder);
            }

            chain = null;
            return false;
        }

        internal static bool TryFind<T>(this IEnumerable<T> @this, Func<T, bool> func, [MaybeNullWhen(false)] out T result)
        {
            foreach (var item in @this)
            {
                if (func(item))
                {
                    result = item;
                    return true;
                }
            }

            result = default;
            return false;
        }

        internal static IReadOnlyList<T> Update<T>(this IReadOnlyList<T> @this, Func<T, T> update) where T : class
            => Update(@this, update, ReferenceEquals);

        internal static IReadOnlyList<T> Update<T>(this IReadOnlyList<T> @this, Func<T, T> update, Func<T, T, bool> equals)
        {
            // Lazily create updatedItems list when we first encounter an updated item
            List<T>? updatedList = null;
            for (int i = 0; i < @this.Count; i++)
            {
                // Potentially update the item
                var updatedItem = update(@this[i]);

                // if we haven't already got an updatedItems list
                // check to see if the object returned from update
                // is different from the one we passed in 
                if (updatedList is null && !equals(updatedItem, @this[i]))
                {
                    // if the updated item is different, this is the
                    // first modified item in the list. 
                    // Create the updatedItems list and add all the
                    // previously processed but unmodified items 

                    updatedList = new List<T>(@this.Count);
                    for (int j = 0; j < i; j++)
                    {
                        updatedList.Add(@this[j]);
                    }
                }

                // if the updated items list exists, add the updatedItem to it
                // (modified or not) 
                if (updatedList is not null)
                {
                    updatedList.Add(updatedItem);
                }
            }

            // updateItems will be null if there were no modifications
            return updatedList ?? @this;
        }


        internal static string NormalizePath(this IFileSystem fileSystem, string path)
        {
            if (fileSystem.Path.DirectorySeparatorChar == '\\')
            {
                return fileSystem.Path.GetFullPath(path);
            }
            else
            {
                return path.Replace('\\', '/');
            }
        }

        public static string GetInstructionAddressPadding(this Script script)
        {
            var digitCount = EnumerateInstructions(script).Last().address switch
            {
                var x when x < 10 => 1,
                var x when x < 100 => 2,
                var x when x < 1000 => 3,
                var x when x < 10000 => 4,
                var x when x <= ushort.MaxValue => 5,
                _ => throw new Exception($"Max script length is {ushort.MaxValue} bytes"),
            };
            return new string('0', digitCount);
        }

        public static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(this Script script)
        {
            var address = 0;
            var opcode = OpCode.PUSH0;
            while (address < script.Length)
            {
                var instruction = script.GetInstruction(address);
                opcode = instruction.OpCode;
                yield return (address, instruction);
                address += instruction.Size;
            }

            if (opcode != OpCode.RET)
            {
                yield return (address, Instruction.RET);
            }
        }

        public static bool IsBranchInstruction(this Instruction instruction)
            => instruction.OpCode >= OpCode.JMPIF
                && instruction.OpCode <= OpCode.JMPLE_L;

        public static string GetOperandString(this Instruction instruction)
        {
            return string.Create<ReadOnlyMemory<byte>>(instruction.Operand.Length * 3 - 1,
                instruction.Operand, (span, memory) =>
                {
                    var first = memory.Span[0];
                    span[0] = GetHexValue(first / 16);
                    span[1] = GetHexValue(first % 16);

                    var index = 1;
                    for (var i = 2; i < span.Length; i += 3)
                    {
                        var b = memory.Span[index++];
                        span[i] = '-';
                        span[i + 1] = GetHexValue(b / 16);
                        span[i + 2] = GetHexValue(b % 16);
                    }
                });

            static char GetHexValue(int i) => (i < 10) ? (char)(i + '0') : (char)(i - 10 + 'A');
        }

        static readonly Lazy<IReadOnlyDictionary<uint, string>> sysCallNames = new Lazy<IReadOnlyDictionary<uint, string>>(
            () => ApplicationEngine.Services.ToImmutableDictionary(kvp => kvp.Value.Hash, kvp => kvp.Value.Name));

        public static string GetComment(this Instruction instruction, int ip, MethodToken[]? tokens = null)
        {
            tokens ??= Array.Empty<MethodToken>();

            switch (instruction.OpCode)
            {
                case OpCode.PUSHINT8:
                case OpCode.PUSHINT16:
                case OpCode.PUSHINT32:
                case OpCode.PUSHINT64:
                case OpCode.PUSHINT128:
                case OpCode.PUSHINT256:
                    return $"{new BigInteger(instruction.Operand.Span)}";
                case OpCode.PUSHM1:
                    return $"{(int)instruction.OpCode - (int)OpCode.PUSH0}";
                case OpCode.PUSHDATA1:
                case OpCode.PUSHDATA2:
                case OpCode.PUSHDATA4:
                    {
                        var text = System.Text.Encoding.UTF8.GetString(instruction.Operand.Span)
                            .Replace("\r", "\"\\r\"").Replace("\n", "\"\\n\"");
                        if (instruction.Operand.Length == 20)
                        {
                            return $"as script hash: {new UInt160(instruction.Operand.Span)}, as text: \"{text}\"";
                        }
                        return $"as text: \"{text}\"";
                    }
                case OpCode.SYSCALL:
                    return sysCallNames.Value.TryGetValue(instruction.TokenU32, out var name)
                        ? $"{name} SysCall"
                        : $"Unknown SysCall {instruction.TokenU32}";
                case OpCode.CALLT:
                    {
                        int index = instruction.TokenU16;
                        if (index >= tokens.Length)
                            return $"Unknown token {instruction.TokenU16}";
                        var token = tokens[index];
                        var contract = NativeContract.Contracts.SingleOrDefault(c => c.Hash == token.Hash);
                        var tokenName = contract is null ? $"{token.Hash}" : contract.Name;
                        return $"{tokenName}.{token.Method} token call";
                    }
                case OpCode.INITSLOT:
                    return $"{instruction.TokenU8} local variables, {instruction.TokenU8_1} arguments";
                case OpCode.JMP_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPLE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPNE_L:
                case OpCode.CALL_L:
                    return OffsetComment(instruction.TokenI32);
                case OpCode.JMP:
                case OpCode.JMPEQ:
                case OpCode.JMPGE:
                case OpCode.JMPGT:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPLE:
                case OpCode.JMPLT:
                case OpCode.JMPNE:
                case OpCode.CALL:
                    return OffsetComment(instruction.TokenI8);
                default:
                    return string.Empty;
            }

            string OffsetComment(int offset) => $"pos: {ip + offset}, offset: {offset}";
        }

        // replicated logic from Blockchain.OnInitialized + Blockchain.Persist
        public static void EnsureLedgerInitialized(this IStore store, ProtocolSettings settings)
        {
            using var snapshot = new SnapshotCache(store.GetSnapshot());
            if (LedgerInitialized(snapshot)) return;

            var block = NeoSystem.CreateGenesisBlock(settings);
            if (block.Transactions.Length != 0) throw new Exception("Unexpected Transactions in genesis block");

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativeOnPersist operation failed", engine.FaultException);
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativePostPersist operation failed", engine.FaultException);
            }

            snapshot.Commit();

            // replicated logic from LedgerContract.Initialized
            static bool LedgerInitialized(DataCache snapshot)
            {
                const byte Prefix_Block = 5;
                var key = new KeyBuilder(NativeContract.Ledger.Id, Prefix_Block).ToArray();
                return snapshot.Find(key).Any();
            }
        }
    }
}
