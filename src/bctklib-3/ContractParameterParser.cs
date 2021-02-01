
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
        public delegate bool TryGetUInt160(string value, [MaybeNullWhen(false)] out UInt160 account);

        private readonly TryGetUInt160? tryGetAccount;
        private readonly TryGetUInt160? tryGetContract;

        public ContractParameterParser(TryGetUInt160? tryGetAccount = null, TryGetUInt160? tryGetContract = null)
        {
            this.tryGetAccount = tryGetAccount;
            this.tryGetContract = tryGetContract;
        }

        public async Task<Script> LoadInvocationScriptAsync(string path, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            var invokeFile = fileSystem.Path.GetFullPath(path);
            if (!fileSystem.File.Exists(invokeFile)) throw new ArgumentException($"{path} doens't exist", nameof(path));

            using var streamReader = fileSystem.File.OpenText(invokeFile);
            using var jsonReader = new JsonTextReader(streamReader);
            var document = await JContainer.LoadAsync(jsonReader).ConfigureAwait(false);
            return LoadInvocationScript(document);
        }

        private Script LoadInvocationScript(JToken document)
        {
            var scriptBuilder = new ScriptBuilder();
            switch (document.Type)
            {
                case JTokenType.Object:
                    EmitAppCall((JObject)document);
                    break;
                case JTokenType.Array:
                    {
                        foreach (var item in document)
                        {
                            EmitAppCall((JObject)item);
                        }
                    }
                    break;
                default:
                    throw new FormatException("invalid invocation file");
            }
            return scriptBuilder.ToArray();

            void EmitAppCall(JObject json)
            {
                var contract = json.Value<string>("contract");
                contract = contract.Length > 0 && contract[0] == '#'
                    ? contract[1..] : contract;

                var scriptHash = TryLoadScriptHash(contract, out var value)
                    ? value
                    : UInt160.TryParse(contract, out var uint160)
                        ? uint160
                        : throw new FormatException($"Invalid contract value \"{json.Value<string>("contract")}\"");

                var operation = json.Value<string>("operation");
                var args = json.TryGetValue("args", out var jsonArgs)
                    ? ParseParameters(jsonArgs).ToArray()
                    : Array.Empty<ContractParameter>();

                scriptBuilder.EmitDynamicCall(scriptHash, operation, args);
            }
        }

        public IEnumerable<ContractParameter> ParseParameters(JToken json)
            => json.Type switch
            {
                JTokenType.Array => json.Select(e => ParseParameter(e)),
                _ => new[] { ParseParameter(json) }
            };

        public ContractParameter ParseParameter(JToken? json)
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
                        .Select(e => ParseParameter(e))
                        .ToList()
                },
                JTokenType.String => ParseStringParameter(json.Value<string>()),
                JTokenType.Object => ParseObjectParameter((JObject)json),
                _ => throw new ArgumentException($"Invalid JTokenType {json.Type}", nameof(json))
            };
        }

        internal ContractParameter ParseStringParameter(string value)
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

                if (TryLoadScriptHash(substring, out var scriptHash))
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
            if (tryGetContract != null && tryGetContract(text, out var scriptHash))
            {
                value = scriptHash;
                return true;
            }

            var nativeContract = NativeContract.Contracts.SingleOrDefault(c => string.Equals(text, c.Name));
            if (nativeContract != null)
            {
                value = nativeContract.Hash;
                return true;
            }

            nativeContract = NativeContract.Contracts.SingleOrDefault(c => string.Equals(text, c.Name, StringComparison.OrdinalIgnoreCase));
            if (nativeContract != null)
            {
                value = nativeContract.Hash;
                return true;
            }

            value = null!;
            return false;
        }

        internal ContractParameter ParseObjectParameter(JObject json)
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
                    .Select(e => ParseParameter(e))
                    .ToList(),
                ContractParameterType.Map => valueProp.Select(ParseMapElement).ToList(),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            return new ContractParameter() { Type = type, Value = value };

            KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json)
                => KeyValuePair.Create(
                    ParseParameter(json["key"] ?? throw new JsonException()),
                    ParseParameter(json["value"] ?? throw new JsonException()));
        }
    }
}
