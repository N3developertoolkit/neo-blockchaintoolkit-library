using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;

using NotFound = OneOf.Types.NotFound;
using ContractParameterType = Neo.SmartContract.ContractParameterType;

namespace Neo.BlockchainToolkit.Models
{
    using ParsedType = OneOf<ContractType, DebugInfo.VoidReturn, NotFound>;
    using StructMap = IReadOnlyDictionary<string, StructContractType>;
    using ParseTypeFunc = Func<string, OneOf<ContractType, DebugInfo.VoidReturn, NotFound>>;

    public record DebugInfo(
        uint Version,
        uint Checksum,
        UInt160 ScriptHash,
        IReadOnlyList<string> Documents,
        IReadOnlyList<DebugInfo.Method> Methods,
        IReadOnlyList<DebugInfo.Event> Events,
        IReadOnlyList<DebugInfo.SlotVariable> StaticVariables,
        IReadOnlyList<StructContractType> Structs,
        IReadOnlyList<StorageGroupDef> StorageGroups)
    {
        public record Event(
            string Id,
            string Namespace,
            string Name,
            IReadOnlyList<SlotVariable> Parameters);

        public record Method(
            string Id,
            string Namespace,
            string Name,
            (int Start, int End) Range,
            string ReturnType,
            IReadOnlyList<SlotVariable> Parameters,
            IReadOnlyList<SlotVariable> Variables,
            IReadOnlyList<SequencePoint> SequencePoints);

        public record SequencePoint(
            int Address,
            int Document,
            (int Line, int Column) Start,
            (int Line, int Column) End);

        public record SlotVariable(
            string Name,
            string Type,
            int Index);

        public struct VoidReturn { }

        internal readonly record struct UnboundVariable(
            string Name,
            string Type,
            int? Index);

        internal readonly record struct UnboundStruct(
            string Name, 
            IReadOnlyList<(string, string)> Fields);

        const string NEF_DBG_NFO_EXTENSION = ".nefdbgnfo";
        const string DEBUG_JSON_EXTENSION = ".debug.json";
        static readonly Regex spRegex = new(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");

        public static DebugInfo Parse(JObject json)
        {
            var version = json.TryGetValue("version", out var versionToken)
                ? versionToken.Value<uint>()
                : 1;

            if (version != 1 && version != 2) throw new JsonException($"Invalid version {version}");

            var checksum = version == 1
                ? 0
                : json.TryGetValue("checksum", out var checksumToken)
                    ? checksumToken.Value<uint>()
                    : throw new JsonException("Missing checksum value");

            var hash = json.TryGetValue("hash", out var hashToken)
                ? UInt160.Parse(hashToken.Value<string>() ?? "")
                : throw new JsonException("Missing hash value");

            var structMap = BindStructs(SelectList(version == 1 ? null : json["structs"], ParseStruct));

            ParseTypeFunc parseType = version == 1
                ? TryParseContractParameterType
                : typeStr => typeStr is null || typeStr.Equals("#Void")
                    ? default(VoidReturn)
                    : ContractType.TryParse(typeStr, structMap, out var type)
                        ? type
                        : default(NotFound);

            var documents = SelectList(json["documents"], token => token.Value<string>() ?? "");
            var events = SelectList(json["events"], ParseEvent);
            var methods = SelectList(json["methods"], ParseMethod);
            var staticVars = ParseSlotVariables(json["static-variables"]).ToArray();
            var structs = structMap.Values.ToArray();
            var storages = SelectList(version == 1 ? null : json["storages"], s => ParseStorage(s, structMap));

            return new DebugInfo(version, checksum, hash, documents, methods, events, staticVars, structs, storages);
        }

        static IReadOnlyList<T> SelectList<T>(JToken? token, Func<JToken, T> func) 
            => token?.Select(func).ToArray() ?? Array.Empty<T>();

        static IEnumerable<SlotVariable> ParseSlotVariables(JToken? token)
        {
            if (token is null) return Enumerable.Empty<SlotVariable>();
            var vars = token.Select(ParseType).ToList();

            // Work around https://github.com/neo-project/neo-devpack-dotnet/issues/637
            // if a variable list has any slot indexes, but the first variable is "this,Any" remove it
            if (vars.Any(t => t.slotIndex.HasValue)
                && vars.Count > 0
                && vars[0].name == "this"
                && vars[0].type == "Any"
                && !vars[0].slotIndex.HasValue)
            {
                vars.RemoveAt(0);
            }
            // end https://github.com/neo-project/neo-devpack-dotnet/issues/637 workaround

            if (vars.Any(t => t.slotIndex.HasValue) && !vars.All(t => t.slotIndex.HasValue))
            {
                throw new FormatException("cannot mix and match optional slot index information");
            }

            return vars.Select((v, i) => new SlotVariable(v.name, v.type, v.slotIndex!.HasValue ? v.slotIndex.Value : i));

            static (string name, string type, int? slotIndex) ParseType(JToken token)
            {
                var value = token.Value<string>() ?? throw new FormatException("invalid type token");
                var values = value.Split(',');
                if (values.Length == 2)
                {
                    return (values[0], values[1], null);
                }
                if (values.Length == 3
                    && int.TryParse(values[2], out var slotIndex)
                    && slotIndex >= 0)
                {

                    return (values[0], values[1], slotIndex);
                }

                throw new FormatException($"invalid type string \"{value}\"");
            }
        }

        static Event ParseEvent(JToken token)
        {
            var id = token.Value<string>("id") ?? throw new FormatException("Invalid event id");
            var (@namespace, name) = ParseName(token["name"]);
            var @params = ParseSlotVariables(token["params"]).ToImmutableList();

            return new Event(id, name, @namespace, @params);
        }

        static SequencePoint ParseSequencePoint(JToken token)
        {
            var value = token.Value<string>() ?? throw new FormatException("invalid Sequence Point token");
            var match = spRegex.Match(value);
            if (match.Groups.Count != 7) throw new FormatException($"Invalid Sequence Point \"{value}\"");

            var address = int.Parse(match.Groups[1].Value);
            var document = int.Parse(match.Groups[2].Value);
            var start = (int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
            var end = (int.Parse(match.Groups[5].Value), int.Parse(match.Groups[6].Value));

            return new SequencePoint(address, document, start, end);
        }

        static Method ParseMethod(JToken token)
        {
            var id = token.Value<string>("id") ?? throw new FormatException("Invalid method id");
            var (@namespace, name) = ParseName(token["name"]);
            var @return = token.Value<string>("return") ?? "Void";
            var @params = ParseSlotVariables(token["params"]).ToArray();
            var variables = ParseSlotVariables(token["variables"]).ToArray();
            var sequencePoints = token["sequence-points"]?.Select(ParseSequencePoint).ToArray()
                ?? Array.Empty<SequencePoint>();
            var range = ParseRange(token["range"]);

            return new Method(id, name, @namespace, range, @return, @params, variables, sequencePoints);
        }

        static UnboundStruct ParseStruct(JToken token)
        {
            var (@namespace, name) = ParseName(token["name"]);
             var nsqName = $"{@namespace}.{name}";
            if (!StructContractType.IsValidName(nsqName)) throw new JsonException($"Invalid struct name \"{nsqName}\"");
            var fields = token["fields"]?.Select(f => ParseName(f.Value<string>())).ToArray() ?? Array.Empty<(string, string)>();
            return new UnboundStruct(nsqName, fields);
        }

        static StructMap BindStructs(IReadOnlyList<UnboundStruct>? unboundStructs)
        {
            unboundStructs ??= Array.Empty<UnboundStruct>();   

            // loop thru the unbound structs, attempting to bind any unbound fields, until all structs are bound
            var structMap = new Dictionary<string, StructContractType>();
            while (structMap.Count < unboundStructs.Count)
            {
                var boundCount = structMap.Count;
                foreach (var @struct in unboundStructs)
                {
                    if (structMap.ContainsKey(@struct.Name)) continue;

                    List<(string name, ContractType type)> fields = new(@struct.Fields.Count);
                    for (int i = 0; i < @struct.Fields.Count; i++)
                    {
                        var (name, typeString) = @struct.Fields[i];
                        if (ContractType.TryParse(typeString, structMap, out var type))
                        {
                            fields.Add((name, type));
                        }
                        else 
                        { 
                            break;
                        }
                    }

                    if (fields.Count == @struct.Fields.Count)
                    {
                        structMap.Add(@struct.Name, new StructContractType(@struct.Name, fields));
                    }
                } 

                // if no progress was made in a given loop, binding is stuck so error out
                if (boundCount >= structMap.Count)
                {
                    var unboundStructNames = unboundStructs
                        .Where(s => !structMap.ContainsKey(s.Name))
                        .Select(s => s.Name);
                    throw new Exception($"Failed to bind {string.Join(",", unboundStructNames)} structs");
                }
            }
            return structMap;
        }

        static StorageGroupDef ParseStorage(JToken token, StructMap structMap)
        {
            var (@namespace, name) = ParseName(token["name"]);
            if (string.IsNullOrEmpty(name)) throw new JsonException("invalid storage name");
            var type = ContractType.TryParse(token.Value<string>("type") ?? "", structMap, out var _type)
                ? _type
                : ContractType.Unspecified;
            var prefix = Convert.FromHexString(token.Value<string>("prefix") ?? "");
            var segments = token["segments"]?.Select(j =>
            {
                var (name, primitive) = ParseName(j.Value<string>());
                var type = ContractType.TryParsePrimitive(primitive, out var _type)
                    ? _type : throw new JsonException($"Invalid storage segment type {primitive}");
                return new KeySegment(name, type);
            }) ?? Array.Empty<KeySegment>();

            return new StorageGroupDef(name, prefix.AsMemory(), segments.ToArray(), type);
        }

        static (string, string) ParseName(JToken? token)
        {
            var name = token?.Value<string>() ?? throw new FormatException("Missing name");
            var values = name.Split(',') ?? throw new FormatException($"Invalid name '{name}'");
            return values.Length == 2
                ? (values[0], values[1])
                : throw new FormatException($"Invalid name '{name}'");
        }

        static (int, int) ParseRange(JToken? token)
        {
            var range = token?.Value<string>() ?? throw new FormatException("Missing range");
            var values = range.Split('-') ?? throw new FormatException($"Invalid range '{range}'");
            return values.Length == 2
                ? (int.Parse(values[0]), int.Parse(values[1]))
                : throw new FormatException($"Invalid range '{range}'");
        }

        internal static ParsedType TryParseContractParameterType(string type)
           => Enum.TryParse<ContractParameterType>(type, out var paramType)
               ? paramType switch
               {
                   ContractParameterType.Any => ContractType.Unspecified,
                   ContractParameterType.Array => new ArrayContractType(ContractType.Unspecified),
                   ContractParameterType.Boolean => PrimitiveContractType.Boolean,
                   ContractParameterType.ByteArray => PrimitiveContractType.ByteArray,
                   ContractParameterType.Hash160 => PrimitiveContractType.Hash160,
                   ContractParameterType.Hash256 => PrimitiveContractType.Hash256,
                   ContractParameterType.Integer => PrimitiveContractType.Integer,
                   ContractParameterType.InteropInterface => InteropContractType.Unknown,
                   ContractParameterType.Map => new MapContractType(PrimitiveType.ByteArray, ContractType.Unspecified),
                   ContractParameterType.PublicKey => PrimitiveContractType.PublicKey,
                   ContractParameterType.Signature => PrimitiveContractType.Signature,
                   ContractParameterType.String => PrimitiveContractType.String,
                   ContractParameterType.Void => default(VoidReturn),
                   _ => default(NotFound),
               }
               : default(NotFound);

        [Obsolete($"use {nameof(LoadContractDebugInfoAsync)} instead")]
        public static Task<OneOf<DebugInfo, NotFound>> LoadAsync(string nefFileName, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
            => LoadContractDebugInfoAsync(nefFileName, sourceFileMap, fileSystem);

        public static async Task<OneOf<DebugInfo, NotFound>> LoadContractDebugInfoAsync(string nefFileName, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();

            DebugInfo debugInfo;
            var compressedFileName = fileSystem.Path.ChangeExtension(nefFileName, NEF_DBG_NFO_EXTENSION);
            var uncompressedFileName = fileSystem.Path.ChangeExtension(nefFileName, DEBUG_JSON_EXTENSION);
            if (fileSystem.File.Exists(compressedFileName))
            {
                using var stream = fileSystem.File.OpenRead(compressedFileName);
                debugInfo = await LoadCompressedAsync(stream).ConfigureAwait(false);
            }
            else if (fileSystem.File.Exists(uncompressedFileName))
            {
                using var stream = fileSystem.File.OpenRead(uncompressedFileName);
                debugInfo = await LoadAsync(stream).ConfigureAwait(false);
            }
            else
            {
                return default(NotFound);
            }

            var resolvedDocuments = ResolveDocuments(debugInfo, sourceFileMap, fileSystem).ToArray();
            return debugInfo with { Documents = resolvedDocuments };
        }

        public static async Task<DebugInfo> LoadAsync(string fileName, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();

            var extension = fileSystem.Path.GetExtension(fileName);
            if (extension == NEF_DBG_NFO_EXTENSION)
            {
                using var stream = fileSystem.File.OpenRead(fileName);
                return await LoadCompressedAsync(stream).ConfigureAwait(false);
            }
            else if (extension == DEBUG_JSON_EXTENSION)
            {
                using var stream = fileSystem.File.OpenRead(fileName);
                return await LoadAsync(stream).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"Invalid Debug Info extension {extension}", nameof(fileName));
            }
        }

        internal static async Task<DebugInfo> LoadCompressedAsync(System.IO.Stream stream)
        {
            using var archive = new ZipArchive(stream);
            using var entryStream = archive.Entries[0].Open();
            return await LoadAsync(entryStream).ConfigureAwait(false);
        }

        internal static async Task<DebugInfo> LoadAsync(System.IO.Stream stream)
        {
            using var streamReader = new System.IO.StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return Parse(root);
        }

        public static IEnumerable<string> ResolveDocuments(DebugInfo debugInfo, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            var sourceMap = sourceFileMap?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new();
            return debugInfo.Documents.Select(doc => ResolveDocument(doc, sourceMap, fileSystem));
        }

        internal static string ResolveDocument(string document, IDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
        {
            if (fileSystem.File.Exists(document)) return document;

            foreach (var (key, value) in sourceFileMap)
            {
                if (document.StartsWith(key))
                {
                    var remainder = FileNameUtilities.TrimStartDirectorySeparators(document[key.Length..]);
                    var mappedDoc = fileSystem.NormalizePath(fileSystem.Path.Join(value, remainder));
                    if (fileSystem.File.Exists(mappedDoc))
                    {
                        return mappedDoc;
                    }
                }
            }

            var cwd = fileSystem.Directory.GetCurrentDirectory();
            var cwdDocument = fileSystem.Path.Join(cwd, FileNameUtilities.GetFileName(document));
            if (fileSystem.File.Exists(cwdDocument))
            {
                var directoryName = FileNameUtilities.GetDirectoryName(document);
                if (directoryName != null)
                {
                    sourceFileMap.Add(directoryName, cwd);
                }

                return cwdDocument;
            }

            var folderName = FileNameUtilities.GetFileName(cwd);
            var folderIndex = document.IndexOf(folderName);
            if (folderIndex >= 0)
            {
                var relPath = document[(folderIndex + folderName.Length)..];
                var newPath = fileSystem.Path.GetFullPath(fileSystem.Path.Join(cwd, relPath));

                if (fileSystem.File.Exists(newPath))
                {
                    return newPath;
                }
            }

            return document;
        }
    }
}
