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
 
    public partial class DebugInfo
    {
        public readonly record struct Event(string Namespace, string Name, IReadOnlyList<SlotVariable> Parameters);
        public readonly record struct Method
        {
            public string Namespace { get; init; }
            public string Name { get; init; }
            public (int Start, int End) Range { get; init; }
            public OneOf<ContractType, VoidReturn> ReturnType { get; init; }
            public IReadOnlyList<SlotVariable> Parameters { get; init; }
            public IReadOnlyList<SlotVariable> Variables { get; init; }
            public IReadOnlyList<SequencePoint> SequencePoints { get; init; }
        }
        public readonly record struct SequencePoint(int Address, int Document, (int line, int column) Start, (int line, int column) End);
        public readonly record struct SlotVariable(string Name, ContractType Type, int Index);
        internal readonly record struct UnboundVariable(string Name, string Type, int? Index);
        public struct VoidReturn { }

        public uint Version { get; init; }
        public uint CheckSum { get; init; }
        public UInt160 ScriptHash { get; init; } = UInt160.Zero;
        public IReadOnlyList<string> Documents { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Method> Methods { get; set; } = Array.Empty<Method>();
        public IReadOnlyList<Event> Events { get; init; } = Array.Empty<Event>();
        public IReadOnlyList<SlotVariable> StaticVariables { get; init; }
            = Array.Empty<SlotVariable>();
        public IReadOnlyList<StructContractType> Structs { get; init; }
            = Array.Empty<StructContractType>();
        public IReadOnlyList<StorageGroupDef> StorageGroups { get; init; }
            = Array.Empty<StorageGroupDef>();

        public static async Task<OneOf<DebugInfo, NotFound>> LoadAsync(string nefFileName, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            sourceFileMap ??= ImmutableDictionary<string, string>.Empty;

            var debugJsonFileName = fileSystem.Path.ChangeExtension(nefFileName, ".nefdbgnfo");
            if (fileSystem.File.Exists(debugJsonFileName))
            {
                using var archiveStream = fileSystem.File.OpenRead(debugJsonFileName);
                using var archive = new ZipArchive(archiveStream);
                using var debugJsonStream = archive.Entries[0].Open();
                return await LoadAsync(debugJsonStream, sourceFileMap, fileSystem).ConfigureAwait(false);
            }

            debugJsonFileName = fileSystem.Path.ChangeExtension(nefFileName, ".debug.json");
            if (fileSystem.File.Exists(debugJsonFileName))
            {
                using var debugJsonStream = fileSystem.File.OpenRead(debugJsonFileName);
                return await LoadAsync(debugJsonStream, sourceFileMap, fileSystem).ConfigureAwait(false);
            }

            return default(NotFound);
        }

        internal static async Task<DebugInfo> LoadAsync(System.IO.Stream stream, IReadOnlyDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
        {
            var documentResolver = new DocumentResolver(sourceFileMap, fileSystem);
            using var streamReader = new System.IO.StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return Load(root, documentResolver.ResolveDocument);
        }

        internal static DebugInfo Load(JObject json, Func<JToken, string> resolveDocument)
        {
            var version = json.TryGetValue("version", out var versionToken)
                ? versionToken.Value<uint>()
                : 1;

            if (version != 1 && version != 2) throw new JsonException($"Invalid version {version}");

            var hash = json.TryGetValue("hash", out var hashToken)
                ? UInt160.Parse(hashToken.Value<string>() ?? "")
                : throw new JsonException("Missing hash value");

            var checksum = version == 1 
                ? 0
                : json.TryGetValue("checksum", out var checksumToken)
                    ? checksumToken.Value<uint>()
                    : throw new JsonException("Missing checksum value");
            var structMap = version == 1
                ? ImmutableDictionary<string, StructContractType>.Empty
                : ParseStructs(json["structs"]);

            Func<string, ParsedType> parseType = version == 1
                ? TryParseContractParameterType
                : typeStr => typeStr is null || typeStr.Equals("#Void")
                    ? default(VoidReturn)
                    : ContractType.TryParse(typeStr, structMap, out var type)
                        ? type
                        : default(NotFound);

            var events = json["events"].Ensure().Select(t => ParseEvent(t, parseType));
            var methods = json["methods"].Ensure().Select(t => ParseMethod(t, parseType));
            var staticVars = ParseVariables(json["static-variables"], parseType);
            var documents = json["documents"].Ensure().Select(resolveDocument);
            var storages = version == 1
                ? Enumerable.Empty<StorageGroupDef>()
                : json["storages"].Ensure().Select(s => ParseStorage(s, structMap));

            return new DebugInfo
            {
                Version = version,
                CheckSum = checksum,
                ScriptHash = hash,
                Documents = documents.ToArray(),
                Methods = methods.ToArray(),
                Events = events.ToArray(),
                StaticVariables = staticVars.ToArray(),
                Structs = structMap.Values.ToArray(),
                StorageGroups = storages.ToArray(),
            };
        }

        internal static Func<DebugInfo.UnboundVariable, int, DebugInfo.SlotVariable> GetBindVariableFunc(ParseTypeFunc parseType)
            => (value, i) => {
                var type = parseType(value.Type).Match<ContractType>(
                    ct => ct,
                    _ => throw new NotSupportedException("Void type not supported for variables"),
                    _ => ContractType.Unspecified);
                var index = value.Index.HasValue ? value.Index.Value : i;
                return new SlotVariable(value.Name, type, index);
            };

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

        internal static IEnumerable<SlotVariable> ParseVariables(JToken? token, ParseTypeFunc parseType) 
            => token.Ensure().Select(ParseUnboundVariable)
                .Validate(ValidateVariables)
                .Select(GetBindVariableFunc(parseType));

        internal static UnboundVariable ParseUnboundVariable(JToken token)
        {
            var value = token.Value<string>() ?? "";
            var values = value.Split(',');
            if (values.Length < 2 || values.Length > 3) throw new JsonException($"invalid type string \"{value}\"");
            int? index = values.Length == 3 ? int.Parse(values[2]) : null;
            return new UnboundVariable(values[0], values[1], index);
        }

        internal static void ValidateVariables(IReadOnlyList<UnboundVariable> variables)
        {
            if (variables.Any(t => t.Index.HasValue) 
                && !variables.All(t => t.Index.HasValue))
            {
                throw new NotSupportedException("cannot mix and match optional slot index information");
            }
        }

        internal static DebugInfo.Event ParseEvent(JToken token, ParseTypeFunc parseType)
        {
            var (ns, name) = SplitCommaPair(token.Value<string>("name"));
            var @params = ParseVariables(token["params"], parseType);

            return new DebugInfo.Event(ns, name, @params.ToArray());
        }

        static Regex spRegex = new Regex(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");
        internal static DebugInfo.SequencePoint ParseSequencePoint(JToken token)
        {
            var value = token.Value<string>() ?? throw new JsonException("invalid Sequence Point token");
            var matches = spRegex.Match(value);
            if (matches.Groups.Count != 7) throw new JsonException($"Invalid Sequence Point \"{value}\"");

            var address = ParseGroup(1);
            var document = ParseGroup(2);
            var startLine = ParseGroup(3);
            var startCol = ParseGroup(4);
            var endLine = ParseGroup(5);
            var endCol = ParseGroup(6);

            return new DebugInfo.SequencePoint(address, document, (startLine, startCol), (endLine, endCol));

            int ParseGroup(int i) => int.Parse(matches.Groups[i].Value);
        }

        internal static DebugInfo.Method ParseMethod(JToken token, ParseTypeFunc parseType)
        {
            var (ns, name) = SplitCommaPair(token.Value<string>("name"));
            var @return = parseType(token.Value<string>("return"))
                .TryPickT2(out _, out var _return)
                    ? ContractType.Unspecified
                    : _return;

            var unboundParams = RemoveInvalidThisParam(token["params"]
                .Ensure()
                .Select(ParseUnboundVariable));
            
            var @params = unboundParams
                .Validate(ValidateVariables)
                .Select(GetBindVariableFunc(parseType));

            var variables = ParseVariables(token["variables"], parseType);
            var sequencePoints = token["sequence-points"].Ensure().Select(ParseSequencePoint);

            var rangeValue = token.Value<string>("range") ?? "";
            var rangeValues = rangeValue.Split('-');
            if (rangeValues.Length != 2) throw new JsonException($"Invalid range string \"{rangeValue}\"");
            var rangeStart = int.Parse(rangeValues[0]);
            var rangeEnd = int.Parse(rangeValues[1]);

            return new DebugInfo.Method()
            {
                Name = name,
                Namespace = ns,
                Range = (rangeStart, rangeEnd),
                Parameters = @params.ToArray(),
                ReturnType = @return,
                Variables = variables.ToArray(),
                SequencePoints = sequencePoints.ToArray(),
            };

            static IEnumerable<UnboundVariable> RemoveInvalidThisParam(IEnumerable<UnboundVariable> @params)
            {
                // Work around https://github.com/neo-project/neo-devpack-dotnet/issues/637
                var first = @params.FirstOrDefault();
                return first.Name == "this" 
                        && first.Type == "Any" 
                        && !first.Index.HasValue
                        && @params.Skip(1).Any(p => p.Index.HasValue)
                    ? @params.Skip(1)
                    : @params;
            }
        }

        internal static StorageGroupDef ParseStorage(JToken storageToken, StructMap structMap)
        {
            var (ns, name) = SplitCommaPair(storageToken.Value<string>("name"));
            if (string.IsNullOrEmpty(name)) throw new JsonException("invalid storage name");
            var type = ContractType.TryParse(storageToken.Value<string>("type") ?? "", structMap, out var _type)
                ? _type : ContractType.Unspecified;
            var prefix = Convert.FromHexString(storageToken.Value<string>("prefix") ?? "");

            var segments = storageToken["segments"].Ensure().Select(j =>
                {
                    var (name, primitive) = SplitCommaPair(j.Value<string>());
                    var type = ContractType.TryParsePrimitive(primitive, out var _type)
                        ? _type : throw new JsonException($"Invalid storage segment type {primitive}");
                    return new KeySegment(name, type);
                });

            return new StorageGroupDef(name, prefix.AsMemory(), segments.ToArray(), type);
        }

        internal static StructMap ParseStructs(JToken? structsToken) 
            => structsToken is null
                ? ImmutableDictionary<string, StructContractType>.Empty
                : BindStructs(ParseUnboundStructs(structsToken));

        internal static IEnumerable<(string name, IReadOnlyList<(string, string)> fields)> ParseUnboundStructs(JToken structs)
            => structs.Select(s =>
                {
                    var (ns, name) = SplitCommaPair(s.Value<string>("name"));
                    var nsqName = $"{ns}.{name}";
                    if (!StructContractType.IsValidName(nsqName)) throw new JsonException($"Invalid struct name \"{nsqName}\"");
                    var fields = s["fields"].Ensure().Select(f => SplitCommaPair(f.Value<string>())).ToArray();
                    return (nsqName, (IReadOnlyList<(string, string)>)fields);
                });

        internal static StructMap BindStructs(IEnumerable<(string name, IReadOnlyList<(string name, string type)> fields)> unboundStructs)
        {
            // cache unboundStructs if not already a read only list under the hood
            unboundStructs = unboundStructs as IReadOnlyList<(string, IReadOnlyList<(string, string)>)> ?? unboundStructs.ToArray();

            // loop thru the unbound structs, attempting to bind any unbound fields, until all structs are bound
            var boundStructMap = new Dictionary<string, StructContractType>();
            var unboundCount = unboundStructs.Count();
            while (boundStructMap.Count < unboundCount)
            {
                var boundCount = boundStructMap.Count;
                foreach (var @struct in unboundStructs)
                {
                    // if struct is already bound, skip it
                    if (boundStructMap.ContainsKey(@struct.name)) continue;

                    List<(string name, ContractType type)> fields = new(@struct.fields.Count);
                    foreach (var (fieldName, fieldType) in @struct.fields)
                    {
                        if (ContractType.TryParse(fieldType, boundStructMap, out var contractType))
                        {
                            fields.Add((fieldName, contractType));
                        }
                        else
                        {
                            break;
                        }
                    }

                    // if all the fields are bound, bind the struct def
                    if (fields.Count == @struct.fields.Count)
                    {
                        boundStructMap.Add(@struct.name, new StructContractType(@struct.name, fields));
                    }
                }

                // if no progress was made in a given loop, binding is stuck so error out
                if (boundCount >= boundStructMap.Count)
                {
                    var unboundStructNames = unboundStructs
                        .Where(s => !boundStructMap.ContainsKey(s.name))
                        .Select(s => s.name);
                    throw new Exception($"Failed to bind {string.Join(",", unboundStructNames)} structs");
                }
            }

            return boundStructMap;
        }

        static (string, string) SplitCommaPair(string? value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            var values = value.Split(',');
            if (values.Length == 2)
            {
                return (values[0], values[1]);
            }

            throw new ArgumentException(nameof(value));
        }
    }
}
