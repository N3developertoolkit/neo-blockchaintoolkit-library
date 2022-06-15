
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Neo.BlockchainToolkit
{
    public static partial class ContractInvocationParser
    {
        static Lazy<IFileSystem> defaultFileSystem = new Lazy<IFileSystem>(() => new FileSystem());
        public static readonly IArgBinders NullBinders = new NullArgBinders();

        public static IArgBinders GetExpressArgBinders(ExpressChain chain, IEnumerable<(UInt160 hash, ContractManifest manifest)> contracts, IFileSystem? fileSystem = null)
        {
            return new ExpressArgBinders(chain, contracts, fileSystem);
        }

        public static async Task<IReadOnlyList<ContractInvocation>> LoadAsync(string path, IFileSystem? fileSystem = null)
        {
            fileSystem ??= defaultFileSystem.Value;

            var invokeFile = fileSystem.Path.GetFullPath(path);
            if (!fileSystem.File.Exists(invokeFile)) throw new ArgumentException($"{path} doesn't exist", nameof(path));

            using var stream = fileSystem.File.Open(invokeFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            return ParseInvocations(doc.RootElement).ToArray();
        }

        public static async Task<Script> LoadScriptAsync(string path, IArgBinders binders)
        {
            var invocations = await LoadAsync(path, binders.FileSystem);

            List<Diagnostic> diagnostics = new();
            var bound = Bind(invocations, binders, diagnostics);
            if (diagnostics.Any(d => d.Severity == Diagnostic.SeverityLevel.Error))
            {
                if (diagnostics.Count == 1)
                {
                    throw new DiagnosticException(diagnostics[0]);
                }
                else
                {
                    throw new AggregateException(diagnostics.Select(d => new DiagnosticException(d)));
                }
            }

            using ScriptBuilder builder = new();
            builder.EmitInvocations(bound);
            return builder.ToArray();
        }

        public static IReadOnlyList<ContractInvocation> ParseInvocations(in JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                var count = json.GetArrayLength();
                List<ContractInvocation> invocations = new(count);
                for (int i = 0; i < count; i++)
                {
                    invocations.Add(ParseInvocation(json[i]));
                }
                return invocations;
            }
            else
            {
                return new[] { ParseInvocation(json) };
            }
        }

        public static ContractInvocation ParseInvocation(in JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid invocation JSON");

            var contract = json.GetProperty("contract").GetString();
            if (string.IsNullOrEmpty(contract)) throw new JsonException("missing invocation contract property");
            var operation = json.GetProperty("operation").GetString();
            if (string.IsNullOrEmpty(operation)) throw new JsonException("missing invocation operation property");

            // default to CallFlags.All if not specified in JSON
            var callFlags = json.TryGetProperty("callFlags", out var jsonCallFlags)
                ? Enum.Parse<CallFlags>(jsonCallFlags.GetRawText())
                : CallFlags.All;

            IReadOnlyList<ContractArg> args = json.TryGetProperty("args", out var jsonArgs)
                ? ParseArgs(jsonArgs).ToList() : Array.Empty<ContractArg>();

            return new ContractInvocation(contract, operation, callFlags, args);
        }

        public static IReadOnlyList<ContractArg> ParseArgs(in JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                var count = json.GetArrayLength();
                List<ContractArg> args = new(count);
                for (int i = 0; i < count; i++)
                {
                    args.Add(ParseArg(json[i]));
                }
                return args;
            }
            else
            {
                return new[] { ParseArg(json) };
            }
        }

        internal static ContractArg ParseArg(in JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.Null => NullContractArg.Null,
                JsonValueKind.True => new PrimitiveContractArg(true),
                JsonValueKind.False => new PrimitiveContractArg(false),
                JsonValueKind.Number => new PrimitiveContractArg(BigInteger.Parse(json.GetRawText())),
                JsonValueKind.String => new PrimitiveContractArg(json.GetRawText()),
                JsonValueKind.Array => new ArrayContractArg(json.EnumerateArray().Select(item => ParseArg(item)).ToArray()),
                JsonValueKind.Object => ParseObject(json),
                _ => throw new NotSupportedException($"Invalid JsonValueKind {json.ValueKind}")
            };
        }

        internal static ContractArg ParseObject(in JsonElement json)
        {
            if (!json.TryGetProperty("value", out var value))
            {
                return NullContractArg.Null; 
            }

            var type = Enum.Parse<ContractParameterType>(json.GetProperty("type").GetRawText());
            return type switch
            {
                ContractParameterType.Any => NullContractArg.Null,
                ContractParameterType.Signature or
                ContractParameterType.ByteArray => new PrimitiveContractArg(value.GetBytesFromBase64()),
                ContractParameterType.Boolean => new PrimitiveContractArg(value.GetBoolean()),
                ContractParameterType.Integer => new PrimitiveContractArg(BigInteger.Parse(value.GetRawText())),
                ContractParameterType.Hash160 => new PrimitiveContractArg(UInt160.Parse(value.GetRawText())),
                ContractParameterType.Hash256 => new PrimitiveContractArg(UInt256.Parse(value.GetRawText())),
                ContractParameterType.PublicKey => new PrimitiveContractArg(ECPoint.Parse(value.GetRawText(), ECCurve.Secp256r1)),
                ContractParameterType.String => new PrimitiveContractArg(value.GetRawText()),
                ContractParameterType.Array => new ArrayContractArg(value.EnumerateArray().Select(v => ParseArg(v)).ToList()),
                ContractParameterType.Map => new MapContractArg(value.EnumerateArray().Select(v => ParseMapElement(v)).ToList()),
                _ => throw new NotSupportedException($"invalid ContractParameterType {type}"),
            };

            static KeyValuePair<PrimitiveContractArg, ContractArg> ParseMapElement(in JsonElement json)
            {
                var key = ParseArg(json.GetProperty("key"));
                return key is PrimitiveContractArg primitiveKey
                    ? KeyValuePair.Create(primitiveKey, ParseArg(json.GetProperty("value")))
                    : throw new Exception("Map key must be a primitive type");
            }
        }

        public static bool Validate(IEnumerable<ContractInvocation> invocations, ICollection<Diagnostic>? diagnostics)
        {
            bool valid = true;
            foreach (var invocation in invocations)
            {
                valid &= Validate(invocation, diagnostics);
            }
            return valid;
        }

        public static bool Validate(ContractInvocation invocation, ICollection<Diagnostic>? diagnostics)
        {
            bool valid = true;
            if (invocation.Contract.TryPickT1(out var name, out _))
            {
                diagnostics?.Add(Diagnostic.Error($"Unbound contract {name}"));
                valid = false;
            }
            if (string.IsNullOrEmpty(invocation.Operation))
            {
                diagnostics?.Add(Diagnostic.Error("Invalid operation"));
                valid = false;
            }
            for (int i = 0; i < invocation.Args.Count; i++)
            {
                valid &= Validate(invocation.Args[i], diagnostics);
            }
            return valid;
        }

        static bool Validate(ContractArg arg, ICollection<Diagnostic>? diagnostics)
        {
            bool valid = true;
            if (arg is PrimitiveContractArg primitive)
            {
                if (primitive.Value.TryPickT3(out var @string, out _))
                {
                    diagnostics?.Add(Diagnostic.Error($"Unbound string argument '{@string}"));
                    return false;
                }
            }
            else if (arg is ArrayContractArg array)
            {
                for (int i = 0; i < array.Values.Count; i++)
                {
                    valid &= Validate(array.Values[i], diagnostics);
                }
            }
            else if (arg is MapContractArg map)
            {
                for (int i = 0; i < map.Values.Count; i++)
                {
                    valid &= Validate(map.Values[i].Key, diagnostics);
                    valid &= Validate(map.Values[i].Value, diagnostics);
                }
            }
            return valid;
        }

        public static IReadOnlyList<ContractInvocation> Bind(
            IReadOnlyCollection<ContractInvocation> invocations,
            IArgBinders binders,
            ICollection<Diagnostic> diagnostics)
        {
            List<ContractInvocation> updatedInvocations = new(invocations.Count);
            foreach (var invocation in invocations)
            {
                updatedInvocations.Add(Bind(invocation, binders, diagnostics));
            }
            return updatedInvocations;

        }

        public static ContractInvocation Bind(
            in ContractInvocation invocation,
            IArgBinders binders,
            ICollection<Diagnostic> diagnostics)
        {
            if (invocation.Contract.TryPickT1(out var contractName, out var contractHash))
            {
                if (!TryGetContract(contractName, binders, out contractHash))
                {
                    diagnostics.Add(Diagnostic.Error($"Failed to bind contract {contractName}"));
                }
            }

            var args = invocation.Args.Update(a => Bind(a, binders, diagnostics));
            return invocation with { Contract = contractHash, Args = args };
        }

        public static ContractArg Bind(
            ContractArg arg,
            IArgBinders binders,
            ICollection<Diagnostic> diagnostics)
        {
            if (arg is PrimitiveContractArg primitive
                && primitive.Value.TryPickT3(out var @string, out _))
            {
                return Bind(@string, binders, diagnostics);
            }
            else if (arg is ArrayContractArg array)
            {
                var values = array.Values.Update(a => Bind(a, binders, diagnostics));
                return object.ReferenceEquals(values, array.Values)
                    ? array
                    : new ArrayContractArg(values);
            }
            else if (arg is MapContractArg map)
            {
                if (Validate(map, null)) return map;

                List<KeyValuePair<PrimitiveContractArg, ContractArg>> values = new(map.Values.Count);
                for (int i = 0; i < map.Values.Count; i++)
                {
                    var key = Bind(map.Values[i].Key, binders, diagnostics);
                    if (key is not PrimitiveContractArg)
                    {
                        diagnostics.Add(Diagnostic.Error("Map keys must be primitive types"));
                    }
                    var value = Bind(map.Values[i].Value, binders, diagnostics);
                    var kvp = KeyValuePair.Create(
                        key as PrimitiveContractArg ?? map.Values[i].Key,
                        value);
                    values.Add(kvp);
                }
                return new MapContractArg(values);
            }
            else
            {
                return arg;
            }
        }

        public static ContractArg Bind(
            string arg,
            IArgBinders binders,
            ICollection<Diagnostic> diagnostics)
        {
            if (arg.Length == 0) return new PrimitiveContractArg(default(ReadOnlyMemory<byte>));

            if (arg[0] == '#')
            {
                var hashString = arg.Substring(1);

                if (UInt256.TryParse(hashString, out var uint256))
                {
                    return new PrimitiveContractArg(uint256);
                }
                if (TryGetContract(hashString, binders, out var uint160))
                {
                    return new PrimitiveContractArg(uint160);
                }
                diagnostics?.Add(Diagnostic.Warning($"Failed to bind {arg} as hash string"));
            }

            if (arg[0] == '@')
            {
                var addressString = arg.Substring(1);

                if (binders.TryGetAccount is not null
                    && binders.TryGetAccount(addressString, out var account))
                {
                    return new PrimitiveContractArg(account);
                }

                try
                {
                    var scriptHash = Neo.Wallets.Helper.ToScriptHash(addressString, binders.AddressVersion);
                    return new PrimitiveContractArg(scriptHash);
                }
                catch (FormatException) { }

                diagnostics?.Add(Diagnostic.Warning($"Failed to bind {arg} as address string"));
            }

            const string FILE_URI = "file://";
            if (arg.StartsWith(FILE_URI))
            {
                var fileSystem = binders.FileSystem;
                var path = fileSystem.NormalizePath(arg.Substring(FILE_URI.Length));
                path = !fileSystem.Path.IsPathFullyQualified(path)
                    ? fileSystem.Path.GetFullPath(path, fileSystem.Directory.GetCurrentDirectory())
                    : path;

                if (!fileSystem.File.Exists(path))
                {
                    diagnostics?.Add(Diagnostic.Error($"{path} path not found"));
                }
                else
                {
                    try
                    {
                        var bytes = fileSystem.File.ReadAllBytes(path);
                        return new PrimitiveContractArg(bytes);
                    }
                    catch (Exception ex)
                    {
                        diagnostics?.Add(Diagnostic.Error(ex.Message));
                    }
                }
            }

            if (arg.StartsWith("0x"))
            {
                try
                {
                    var bytes = Convert.FromHexString(arg.AsSpan(2));
                    return new PrimitiveContractArg(bytes);
                }
                catch (FormatException) { }

                diagnostics?.Add(Diagnostic.Warning($"Failed to bind {arg} as hex string"));
            }

            // TODO: Should we support base64 encoding here? If so, should there be a prefix?
            //       "data:application/octet-stream;base64," would be appropriate prefix,
            //       but man that is LONG

            return new PrimitiveContractArg(arg);
        }


        static bool TryGetContract(string contract, IArgBinders binders, out UInt160 hash)
        {
            if (UInt160.TryParse(contract, out hash))
            {
                return true;
            }

            if (binders.TryGetContract is not null
                && binders.TryGetContract(contract, out hash))
            {
                return true;
            }

            foreach (var nativeContract in NativeContract.Contracts)
            {
                if (string.Equals(contract, nativeContract.Name, StringComparison.OrdinalIgnoreCase))
                {
                    hash = nativeContract.Hash;
                    return true;
                }
            }

            return false;
        }
    }
}
