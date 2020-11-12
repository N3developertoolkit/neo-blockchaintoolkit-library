
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;

namespace Neo.BlockchainToolkit
{
    public class ContractParameterParser
    {
        public delegate bool TryGetAccount(string value, [MaybeNullWhen(false)] out UInt160 account);

        private readonly IFileSystem fileSystem;
        private readonly TryGetAccount tryGetAccount;

        public ContractParameterParser(IFileSystem fileSystem, TryGetAccount? tryGetAccount = null)
        {
            this.fileSystem = fileSystem;
            this.tryGetAccount = tryGetAccount ?? DefaultTryGetAccount;
        }

        private static bool DefaultTryGetAccount(string _, [MaybeNullWhen(false)] out UInt160 account)
        {
            account = null!;
            return false;
        }

        public Script LoadInvocationScript(string path)
        {
            var invokeFile = fileSystem.Path.GetFullPath(path);
            if (!fileSystem.File.Exists(invokeFile)) throw new ArgumentException($"{path} doens't exist", nameof(path));

            var basePath = fileSystem.Path.GetDirectoryName(invokeFile);

            using var stream = fileSystem.File.OpenRead(invokeFile);
            using var document = JsonDocument.Parse(stream);
            return LoadInvocationScript(document, basePath);
        }

        private Script LoadInvocationScript(JsonDocument document, string basePath)
        {
            var scriptBuilder = new ScriptBuilder();
            switch (document.RootElement.ValueKind)
            {
                case JsonValueKind.Object:
                    EmitAppCall(document.RootElement, basePath);
                    break;
                case JsonValueKind.Array:
                    {
                        foreach (var arrayElement in document.RootElement.EnumerateArray())
                        {
                            EmitAppCall(arrayElement, basePath);
                        }
                    }
                    break;
                default:
                    throw new FormatException("invalid invocation file");
            }
            return scriptBuilder.ToArray();

            void EmitAppCall(JsonElement json, string basePath)
            {
                var contract = json.GetProperty("contract").GetString() ?? string.Empty;
                contract = contract.Length > 0 && contract[0] == '#'
                    ? contract[1..] : contract;

                var scriptHash = TryLoadScriptHash(contract, basePath, out var value)
                    ? value
                    : UInt160.TryParse(contract, out var uint160)
                        ? uint160
                        : throw new FormatException($"Invalid contract value \"{json.GetProperty("contract").GetString()}\"");

                var operation = json.GetProperty("operation").GetString() ?? throw new FormatException("Missing operation property");
                var args = ParseParameters(json.GetProperty("args"), basePath).ToArray();
                scriptBuilder.EmitAppCall(scriptHash, operation, args);
            }
        }

        public IEnumerable<ContractParameter> ParseParameters(JsonElement json, string basePath = "")
            => json.ValueKind switch
            {
                JsonValueKind.Array => json.EnumerateArray()
                    .Select(e => ParseParameter(e, basePath)),
                JsonValueKind.Object => Enumerable.Repeat(ParseParameter(json, basePath), 1),
                _ => throw new ArgumentException($"Invalid JsonValueKind {json.ValueKind}", nameof(json)),
            };

        public ContractParameter ParseParameter(JsonElement json, string basePath = "")
            => json.ValueKind switch
            {
                JsonValueKind.True => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = true,
                },
                JsonValueKind.False => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = false,
                },
                JsonValueKind.Number => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(json.GetInt64())
                },
                JsonValueKind.Array => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = json.EnumerateArray()
                        .Select(e => ParseParameter(e, basePath))
                        .ToList()
                },
                JsonValueKind.String => ParseStringParameter(json.GetString() ?? throw new JsonException(), basePath),
                JsonValueKind.Object => ParseObjectParameter(json, basePath),
                _ => throw new ArgumentException($"Invalid JsonValueKind {json.ValueKind}", nameof(json))
            };

        internal ContractParameter ParseStringParameter(string value, string basePath)
        {
            if (value.Length >= 1 && value[0] == '@')
            {
                var substring = value[1..];

                if (tryGetAccount(substring, out var account))
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
                    // NOTE, Neo.Wallets.Helper.ToScriptHash uses up ProtocolSettings.Default.AddressVersion
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

        internal bool TryLoadScriptHash(string text, string basePath, [MaybeNullWhen(false)] out UInt160 value)
        {
            if (text.EndsWith(".nef"))
            {
                var resolvedPath = ResolvePath(text);
                if (fileSystem.File.Exists(resolvedPath))
                {
                    using var stream = fileSystem.File.OpenRead(resolvedPath);
                    using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                    value = reader.ReadSerializable<NefFile>().ScriptHash;
                    return true;
                }
            }

            value = null!;
            return false;

            string ResolvePath(string path)
            {
                if (fileSystem.Path.IsPathFullyQualified(path))
                {
                    return path;
                }

                return fileSystem.Path.GetFullPath(path, basePath);
            }
        }

        internal ContractParameter ParseObjectParameter(JsonElement json, string basePath)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"Invalid JsonValueKind {json.ValueKind}", nameof(json));
            }

            var type = Enum.Parse<ContractParameterType>(json.GetProperty("type").GetString() ?? throw new JsonException());
            var valueProp = json.GetProperty("value");

            object value = type switch
            {
                ContractParameterType.Signature => valueProp.GetString().HexToBytes(),
                ContractParameterType.ByteArray => valueProp.GetString().HexToBytes(),
                ContractParameterType.Boolean => valueProp.GetBoolean(),
                ContractParameterType.Integer => BigInteger.Parse(valueProp.GetString()),
                ContractParameterType.Hash160 => UInt160.Parse(valueProp.GetString()),
                ContractParameterType.Hash256 => UInt256.Parse(valueProp.GetString()),
                ContractParameterType.PublicKey => ECPoint.Parse(valueProp.GetString(), ECCurve.Secp256r1),
                ContractParameterType.String => valueProp.GetString() ?? throw new JsonException(),
                ContractParameterType.Array => valueProp.EnumerateArray()
                    .Select(e => ParseParameter(e, basePath))
                    .ToList(),
                ContractParameterType.Map => valueProp.EnumerateArray().Select(ParseMapElement).ToList(),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            return new ContractParameter() { Type = type, Value = value };

            KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JsonElement json)
                => KeyValuePair.Create(
                    ParseParameter(json.GetProperty("key"), basePath),
                    ParseParameter(json.GetProperty("value"), basePath));
        }
    }
}
