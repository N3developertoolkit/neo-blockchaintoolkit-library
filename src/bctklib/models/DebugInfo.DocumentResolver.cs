using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public partial class DebugInfo
    {
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
                        var remainder = FileNameUtilities.TrimStartDirectorySeparators(_document.Substring(kvp.Key.Length));
                        var mapDocument = fileSystem.NormalizePath(fileSystem.Path.Join(kvp.Value, remainder));
                        if (fileSystem.File.Exists(mapDocument))
                        {
                            return mapDocument;
                        }
                    }
                }

                var cwd = fileSystem.Directory.GetCurrentDirectory();
                var cwdDocument = fileSystem.Path.Join(cwd, FileNameUtilities.GetFileName(_document));
                if (fileSystem.File.Exists(cwdDocument))
                {
                    var directoryName = FileNameUtilities.GetDirectoryName(_document);
                    if (directoryName != null)
                    {
                        folderMap.Add(directoryName, cwd);
                    }

                    return cwdDocument;
                }

                var folderName = FileNameUtilities.GetFileName(cwd);
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
    }
}
