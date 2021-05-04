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

            static async Task<DebugInfo> LoadAsync(System.IO.Stream stream, IReadOnlyDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
            {
                var documentResolver = new DocumentResolver(sourceFileMap, fileSystem);
                using var streamReader = new System.IO.StreamReader(stream);
                using var jsonReader = new JsonTextReader(streamReader);
                var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
                return Parse(root, documentResolver);
            }
        }

        public class Method
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public (int Start, int End) Range { get; set; }
            public string ReturnType { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; }
                = ImmutableList<(string, string)>.Empty;
            public IReadOnlyList<(string Name, string Type)> Variables { get; set; }
                = ImmutableList<(string, string)>.Empty;
            public IReadOnlyList<SequencePoint> SequencePoints { get; set; }
                = ImmutableList<SequencePoint>.Empty;
        }

        public class Event
        {
            public string Id { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public IReadOnlyList<(string Name, string Type)> Parameters { get; set; }
                = ImmutableList<(string, string)>.Empty;
        }

        public class SequencePoint
        {
            public int Address { get; set; }
            public int Document { get; set; }
            public (int line, int column) Start { get; set; }
            public (int line, int column) End { get; set; }
        }

        class DocumentResolver
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
                    return _document ;
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

        private static DebugInfo Parse(JObject json, DocumentResolver documentResolver)
        {
            var hash = Neo.UInt160.Parse(json.Value<string>("hash"));
            var documents = (json["documents"] ?? Enumerable.Empty<JToken>())
                .Select(documentResolver.ResolveDocument).ToImmutableList();
            var events = (json["events"] ?? Enumerable.Empty<JToken>()).Select(ParseEvent).ToImmutableList();
            var spRegex = new Regex(@"^(\d+)\[(\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");
            var methods = (json["methods"] ?? Enumerable.Empty<JToken>()).Select(t => ParseMethod(t, spRegex)).ToImmutableList();

            return new DebugInfo
            {
                ScriptHash = hash,
                Documents = documents,
                Methods = methods,
                Events = events,
            };

            static DebugInfo.Event ParseEvent(JToken token)
            {
                var (ns, name) = SplitComma(token.Value<string>("name") ?? "");
                var @params = (token["params"] ?? Enumerable.Empty<JToken>())
                    .Select(t => SplitComma(t.Value<string>() ?? ""));
                return new DebugInfo.Event()
                {
                    Id = token.Value<string>("id") ?? "",
                    Name = name,
                    Namespace = ns,
                    Parameters = @params.ToList()
                };
            }

            static DebugInfo.SequencePoint ParseSequencePoint(string value, Regex spRegex)
            {
                var matches = spRegex.Match(value);
                if (matches.Groups.Count != 7) throw new ArgumentException(nameof(value));

                int ParseGroup(int i)
                {
                    return int.Parse(matches.Groups[i].Value);
                }

                return new DebugInfo.SequencePoint
                {
                    Address = ParseGroup(1),
                    Document = ParseGroup(2),
                    Start = (ParseGroup(3), ParseGroup(4)),
                    End = (ParseGroup(5), ParseGroup(6)),
                };
            }

            static DebugInfo.Method ParseMethod(JToken token, Regex spRegex)
            {
                var (ns, name) = SplitComma(token.Value<string>("name") ?? "");
                var @params = (token["params"] ?? Enumerable.Empty<JToken>()).Select(t => SplitComma(t.Value<string>() ?? ""));
                var variables = (token["variables"] ?? Enumerable.Empty<JToken>()).Select(t => SplitComma(t.Value<string>() ?? ""));
                var sequencePoints = (token["sequence-points"] ?? Enumerable.Empty<JToken>())
                    .Select(t => ParseSequencePoint(t.Value<string>() ?? "", spRegex))
                    .OrderBy(sp => sp.Address);
                var range = (token.Value<string>("range") ?? "").Split('-');
                if (range.Length != 2) throw new JsonException("invalid method range property");

                return new DebugInfo.Method()
                {
                    Id = token.Value<string>("id") ?? "",
                    Name = name,
                    Namespace = ns,
                    Range = (int.Parse(range[0]), int.Parse(range[1])),
                    Parameters = @params.ToImmutableList(),
                    ReturnType = token.Value<string>("return") ?? "",
                    Variables = variables.ToImmutableList(),
                    SequencePoints = sequencePoints.ToImmutableList(),
                };
            }

            static (string, string) SplitComma(string value)
            {
                var values = value.Split(',');
                if (values.Length != 2) throw new ArgumentException(nameof(value));
                return (values[0], values[1]);
            }
        }
    }
}
