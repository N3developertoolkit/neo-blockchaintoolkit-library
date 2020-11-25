
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit
{
    public class ContractParameterParser
    {
        public delegate bool TryGetAccount(string value, [MaybeNullWhen(false)] out UInt160 account);

        private readonly IFileSystem fileSystem;
        private readonly TryGetAccount? tryGetAccount;

        public ContractParameterParser()
            : this(new FileSystem())
        {
        }

        public ContractParameterParser(TryGetAccount tryGetAccount)
            : this(new FileSystem(), tryGetAccount)
        {
        }

        public ContractParameterParser(IFileSystem fileSystem, TryGetAccount? tryGetAccount = null)
        {
            this.fileSystem = fileSystem;
            this.tryGetAccount = tryGetAccount;
        }

        public async Task<Script> LoadInvocationScriptAsync(string path)
        {
            var invokeFile = fileSystem.Path.GetFullPath(path);
            if (!fileSystem.File.Exists(invokeFile)) throw new ArgumentException($"{path} doens't exist", nameof(path));

            var basePath = fileSystem.Path.GetDirectoryName(invokeFile);

            using var streamReader = fileSystem.File.OpenText(invokeFile);
            using var jsonReader = new JsonTextReader(streamReader);
            var document = await JContainer.LoadAsync(jsonReader).ConfigureAwait(false);
            return LoadInvocationScript(document, basePath);
        }

        private Script LoadInvocationScript(JToken document, string basePath)
        {
            var scriptBuilder = new ScriptBuilder();
            switch (document.Type)
            {
                case JTokenType.Object:
                    EmitAppCall((JObject)document, basePath);
                    break;
                case JTokenType.Array:
                    {
                        foreach (var item in document)
                        {
                            EmitAppCall((JObject)item, basePath);
                        }
                    }
                    break;
                default:
                    throw new FormatException("invalid invocation file");
            }
            return scriptBuilder.ToArray();

            void EmitAppCall(JObject json, string basePath)
            {
                var contract = json.Value<string>("contract");
                contract = contract.Length > 0 && contract[0] == '#'
                    ? contract[1..] : contract;

                var scriptHash = TryLoadScriptHash(contract, basePath, out var value)
                    ? value
                    : UInt160.TryParse(contract, out var uint160)
                        ? uint160
                        : throw new FormatException($"Invalid contract value \"{json.Value<string>("contract")}\"");

                var operation = json.Value<string>("operation");
                var args = json.TryGetValue("args", out var jsonArgs)
                    ? ParseParameters(jsonArgs, basePath).ToArray()
                    : Array.Empty<ContractParameter>();

                // the following code is lifted + modified from Neo.VM.Helper.EmitAppCall
                // EmitPush(ContractParameter) doesn't correctly handle Any/null instances
                // TODO: replace with scriptBuilder.EmitAppCall(scriptHash, operation, args);
                //       once https://github.com/neo-project/neo/issues/2104 is fixed

                for (int i = args.Length - 1; i >= 0; i--)
                {
                    if (args[i].Type == ContractParameterType.Any && args[i].Value == null)
                    {
                        scriptBuilder.Emit(OpCode.PUSHNULL);
                    }
                    else
                    {
                        scriptBuilder.EmitPush(args[i]);
                    }
                }
                scriptBuilder.EmitPush(args.Length);
                scriptBuilder.Emit(OpCode.PACK);
                scriptBuilder.EmitPush(operation);
                scriptBuilder.EmitPush(scriptHash);
                scriptBuilder.EmitSysCall(ApplicationEngine.System_Contract_Call);
            }
        }

        public IEnumerable<ContractParameter> ParseParameters(JToken json, string basePath = "")
            => json.Type switch
            {
                JTokenType.Array => json.Select(e => ParseParameter(e, basePath)),
                _ => new[] { ParseParameter(json, basePath) }
            };

        public ContractParameter ParseParameter(JToken? json, string basePath = "")
        {
            if (json == null)
            {
                return new ContractParameter() { Type = ContractParameterType.Any };
            }

            return json.Type switch
            {
                JTokenType.Null => new ContractParameter() { Type = ContractParameterType.Any },
                JTokenType.Boolean => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = json.Value<bool>(),
                },
                JTokenType.Integer => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(json.Value<long>())
                },
                JTokenType.Array => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = ((JArray)json)
                        .Select(e => ParseParameter(e, basePath))
                        .ToList()
                },
                JTokenType.String => ParseStringParameter(json.Value<string>(), basePath),
                JTokenType.Object => ParseObjectParameter((JObject)json, basePath),
                _ => throw new ArgumentException($"Invalid JTokenType {json.Type}", nameof(json))
            };
        }

        internal ContractParameter ParseStringParameter(string value, string basePath)
        {
            if (value.Length >= 1 && value[0] == '@')
            {
                var substring = value[1..];

                if (tryGetAccount != null && tryGetAccount(substring, out var account))
                {
                    return new ContractParameter(ContractParameterType.Hash160) { Value = account };
                }

                if (TryParseAddress(substring, out var address))
                {
                    return new ContractParameter(ContractParameterType.Hash160) { Value = address };
                }
            }

            if (value[0] == '#')
            {
                var substring = value[1..];

                if (UInt160.TryParse(substring, out var uint160))
                {
                    return new ContractParameter(ContractParameterType.Hash160) { Value = uint160 };
                }

                if (UInt256.TryParse(substring, out var uint256))
                {
                    return new ContractParameter(ContractParameterType.Hash256) { Value = uint256 };
                }

                if (TryLoadScriptHash(substring, basePath, out var scriptHash))
                {
                    return new ContractParameter(ContractParameterType.Hash160) { Value = scriptHash };
                }
            }

            if (TryParseHexString(value, out var byteArray))
            {
                return new ContractParameter(ContractParameterType.ByteArray) { Value = byteArray };
            }

            return new ContractParameter(ContractParameterType.String) { Value = value };

            static bool TryParseAddress(string address, out UInt160 scriptHash)
            {
                try
                {
                    // NOTE: Neo.Wallets.Helper.ToScriptHash uses up ProtocolSettings.Default.AddressVersion
                    scriptHash = address.ToScriptHash();
                    return true;
                }
                catch (FormatException)
                {
                    scriptHash = default!;
                    return false;
                }
            }

            static bool TryParseHexString(string hexString, out byte[] array)
            {
                try
                {
                    if (hexString.Length > 2 && hexString.StartsWith("0x"))
                    {
                        array = hexString[2..].HexToBytes();
                        return true;
                    }
                }
                catch (FormatException)
                {
                }

                array = default!;
                return false;
            }
        }

        public bool TryLoadScriptHash(string text, [MaybeNullWhen(false)] out UInt160 value)
        {
            return TryLoadScriptHash(text, string.Empty, out value);
        }

        public bool TryLoadScriptHash(string text, string basePath, [MaybeNullWhen(false)] out UInt160 value)
        {
            if (text.EndsWith(".nef"))
            {
                if (fileSystem.Path.IsPathFullyQualified(text) || fileSystem.Path.IsPathFullyQualified(basePath))
                {
                    var resolvedPath = fileSystem.Path.IsPathFullyQualified(text)
                        ? text 
                        : fileSystem.Path.GetFullPath(text, basePath);
                    if (fileSystem.File.Exists(resolvedPath))
                    {
                        using var stream = fileSystem.File.OpenRead(resolvedPath);
                        using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                        value = reader.ReadSerializable<NefFile>().ScriptHash;
                        return true;
                    }
                }
            }

            var nativeContract = NativeContract.Contracts.SingleOrDefault(c => c.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (nativeContract != null)
            {
                value = nativeContract.Hash;
                return true;
            }

            value = null!;
            return false;
        }

        internal ContractParameter ParseObjectParameter(JObject json, string basePath)
        {
            var type = Enum.Parse<ContractParameterType>(json.Value<string>("type"));
            var valueProp = json["value"] ?? throw new JsonException();

            object value = type switch
            {
                ContractParameterType.Signature => valueProp.Value<string>().HexToBytes(),
                ContractParameterType.ByteArray => valueProp.Value<string>().HexToBytes(),
                ContractParameterType.Boolean => valueProp.Value<bool>(),
                ContractParameterType.Integer => BigInteger.Parse(valueProp.Value<string>()),
                ContractParameterType.Hash160 => UInt160.Parse(valueProp.Value<string>()),
                ContractParameterType.Hash256 => UInt256.Parse(valueProp.Value<string>()),
                ContractParameterType.PublicKey => ECPoint.Parse(valueProp.Value<string>(), ECCurve.Secp256r1),
                ContractParameterType.String => valueProp.Value<string>() ?? throw new JsonException(),
                ContractParameterType.Array => valueProp
                    .Select(e => ParseParameter(e, basePath))
                    .ToList(),
                ContractParameterType.Map => valueProp.Select(ParseMapElement).ToList(),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            return new ContractParameter() { Type = type, Value = value };

            KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json)
                => KeyValuePair.Create(
                    ParseParameter(json["key"] ?? throw new JsonException(), basePath),
                    ParseParameter(json["value"] ?? throw new JsonException(), basePath));
        }
    }
}
