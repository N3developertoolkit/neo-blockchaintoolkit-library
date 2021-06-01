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

namespace Neo.BlockchainToolkit.Models
{
    public class DebugInfo
    {
        public UInt160 ScriptHash { get; set; } = UInt160.Zero;
        public IReadOnlyList<string> Documents { get; set; } = ImmutableList<string>.Empty;
        public IReadOnlyList<Method> Methods { get; set; } = ImmutableList<Method>.Empty;
        public IReadOnlyList<Event> Events { get; set; } = ImmutableList<Event>.Empty;
        public IReadOnlyList<SlotVariable> StaticVariables { get; set; }
            = ImmutableList<SlotVariable>.Empty;

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

            return new NotFound();
        }

        internal static async Task<DebugInfo> LoadAsync(System.IO.Stream stream, IReadOnlyDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
        {
            var documentResolver = new DocumentResolver(sourceFileMap, fileSystem);
            using var streamReader = new System.IO.StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return Load(root, documentResolver.ResolveDocument);
        }

        public readonly struct SlotVariable
        {
            public readonly string Name;
            public readonly string Type;
            public readonly int SlotIndex;

            public SlotVariable(string name, string type, int slotIndex)
            {
                Name = name;
                Type = type;
                SlotIndex = slotIndex;
            }
        }

        public class Method
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public (int Start, int End) Range { get; set; }
            public string ReturnType { get; set; } = string.Empty;
            public IReadOnlyList<SlotVariable> Parameters { get; set; } = ImmutableList<SlotVariable>.Empty;
            public IReadOnlyList<SlotVariable> Variables { get; set; } = ImmutableList<SlotVariable>.Empty;
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; } = ImmutableList<SequencePoint>.Empty;
        }

        public class Event
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<SlotVariable> Parameters { get; set; } = ImmutableList<SlotVariable>.Empty;
        }

        public class SequencePoint
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }
        }

        internal class DocumentResolver
        {
            readonly Dictionary<string, string> folderMap;
            readonly IFileSystem fileSystem;

            public DocumentResolver(IReadOnlyDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
            {
                folderMap = sourceFileMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.fileSystem = fileSystem;
            }

            public string ResolveDocument(JToken token)
            {
                var _document = token.Value<string>() ?? "";
                if (fileSystem.File.Exists(_document))
                {
                    return _document;
                }

                foreach (var kvp in folderMap)
                {
                    if (_document.StartsWith(kvp.Key))
                    {
                        var mapDocument = fileSystem.Path.Join(kvp.Value, _document.Substring(kvp.Key.Length));
                        if (fileSystem.File.Exists(mapDocument))
                        {
                            return mapDocument;
                        }
                    }
                }

                var cwd = fileSystem.Directory.GetCurrentDirectory();
                var cwdDocument = fileSystem.Path.Join(cwd, fileSystem.Path.GetFileName(_document));
                if (fileSystem.File.Exists(cwdDocument))
                {
                    var directoryName = fileSystem.Path.GetDirectoryName(_document);
                    if (directoryName != null)
                    {
                        folderMap.Add(directoryName, cwd);
                    }

                    return cwdDocument;
                }

                var folderName = fileSystem.Path.GetFileName(cwd);
                var folderIndex = _document.IndexOf(folderName);
                if (folderIndex >= 0)
                {
                    var relPath = _document.Substring(folderIndex + folderName.Length);
                    var newPath = fileSystem.Path.GetFullPath(fileSystem.Path.Join(cwd, relPath));

                    if (fileSystem.File.Exists(newPath))
                    {
                        return newPath;
                    }
                }

                return _document;
            }
        }

        static Regex spRegex = new Regex(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");

        internal static DebugInfo Load(JObject json, Func<JToken, string> funcResolveDocument)
        {
            var hash = json.TryGetValue("hash", out var hashToken)
                ? UInt160.TryParse(hashToken.ToObject<string>(), out var _hash)
                    ? _hash
                    : throw new FormatException("Invalid hash value")
                : throw new FormatException("Missing hash value");

            var documents = EnumToken(json, "documents").Select(funcResolveDocument).ToImmutableList();
            var events = EnumToken(json, "events").Select(ParseEvent).ToImmutableList();
            var methods = EnumToken(json, "methods").Select(ParseMethod).ToImmutableList();
            var staticVars = LoadSlotVariables(json, "static-variables").ToImmutableList();

            return new DebugInfo
            {
                ScriptHash = hash,
                Documents = documents,
                Methods = methods,
                Events = events,
                StaticVariables = staticVars,
            };

            static IEnumerable<SlotVariable> LoadSlotVariables(JToken token, string propertyName)
            {
                var vars = EnumToken(token, propertyName).Select(ParseType).ToList();
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

            static DebugInfo.Event ParseEvent(JToken token)
            {
                var id = token.Value<string>("id") ?? throw new FormatException("Invalid event id");
                var nameValue = token.Value<string>("name") ?? throw new FormatException("Invalid event name");
                var (ns, name) = TrySplitComma(nameValue, out var _name)
                    ? _name
                    : throw new FormatException("Invalid name string \"{value}\"");
                var @params = LoadSlotVariables(token, "params").ToImmutableList();

                return new DebugInfo.Event()
                {
                    Id = id,
                    Name = name,
                    Namespace = ns,
                    Parameters = @params,
                };
            }

            static DebugInfo.SequencePoint ParseSequencePoint(JToken token)
            {
                var value = token.Value<string>() ?? throw new FormatException("invalid Sequence Point token");
                var matches = spRegex.Match(value);
                if (matches.Groups.Count != 7) throw new FormatException($"Invalid Sequence Point \"{value}\"");

                return new DebugInfo.SequencePoint
                {
                    Address = ParseGroup(1),
                    Document = ParseGroup(2),
                    Start = (ParseGroup(3), ParseGroup(4)),
                    End = (ParseGroup(5), ParseGroup(6)),
                };

                int ParseGroup(int i)
                {
                    return int.TryParse(matches.Groups[i].Value, out var value)
                        ? value
                        : throw new FormatException($"Invalid Sequence Point \"{value}\"");
                }
            }

            static DebugInfo.Method ParseMethod(JToken token)
            {
                var id = token.Value<string>("id") ?? throw new FormatException("Invalid method id");
                var @return = token.Value<string>("return") ?? "Void";
                var @params = LoadSlotVariables(token, "params").ToImmutableList();
                var variables = LoadSlotVariables(token, "variables").ToImmutableList();
                var sequencePoints = EnumToken(token, "sequence-points").Select(ParseSequencePoint).ToImmutableList();

                var nameValue = token.Value<string>("name") ?? throw new FormatException("Invalid method name");
                (string ns, string name) nameTuple = TrySplitComma(nameValue, out var _name)
                    ? _name
                    : throw new FormatException($"Invalid name string \"{nameValue}\"");

                var rangeValue = token.Value<string>("range") ?? throw new FormatException("Invalid method range");
                var rangeSplitValues = rangeValue.Split('-');
                if (rangeSplitValues.Length != 2) throw new FormatException($"Invalid range string \"{rangeValue}\"");
                var rangeStart = int.TryParse(rangeSplitValues[0], out var _start)
                    ? _start
                    : throw new FormatException($"Invalid range string \"{rangeValue}\"");
                var rangeEnd = int.TryParse(rangeSplitValues[1], out var _end)
                    ? _end
                    : throw new FormatException($"Invalid range string \"{rangeValue}\"");

                return new DebugInfo.Method()
                {
                    Id = id,
                    Name = nameTuple.name,
                    Namespace = nameTuple.ns,
                    Range = (rangeStart, rangeEnd),
                    Parameters = @params,
                    ReturnType = @return,
                    Variables = variables.ToImmutableList(),
                    SequencePoints = sequencePoints.ToImmutableList(),
                };
            }

            static IEnumerable<JToken> EnumToken(JToken token, string propertyName)
            {
                return token[propertyName] ?? Enumerable.Empty<JToken>();
            }

            static bool TrySplitComma(string value, out (string, string) splitValues)
            {
                var values = value.Split(',');
                if (values.Length == 2)
                {
                    splitValues = (values[0], values[1]);
                    return true;
                }

                splitValues = default;
                return false;
            }
        }
    }
}
