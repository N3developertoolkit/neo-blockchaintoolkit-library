// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC;

using StructMap = System.Collections.Generic.IReadOnlyDictionary<string, Neo.BlockchainToolkit.Models.StructContractType>;

var uri = new Uri("http://seed1.neo.org:10332");
using var rpcClient = new RpcClient(uri);
var nativeContracts = await rpcClient.GetNativeContractsAsync();

using var stream = GetResourceStream("NativeContracts.json");
using var jsonDoc = await JsonDocument.ParseAsync(stream);

var interopTypes = jsonDoc.RootElement.GetProperty("interoperable-types");
var nativeContractStorages = jsonDoc.RootElement.GetProperty("native-contract-storage");

var unboundStructs = interopTypes.EnumerateArray()
    .Select(ParseInteroperableType);
var structs = DebugInfo.BindStructs(unboundStructs);

var storages = nativeContractStorages.EnumerateArray()
    .Select(j => ParseNativeContractStorage(j, structs))
    .ToList();


;


static (string name, IReadOnlyList<(string name, string type)> fields) ParseInteroperableType(JsonElement json)
{
    var name = json.GetProperty("name").GetString() ?? throw new Exception();
    List<(string, string)> fields = new();

    foreach (var field in json.GetProperty("fields").EnumerateArray())
    {
        var fieldName = field.GetProperty("name").GetString() ?? throw new Exception();
        var fieldType = field.GetProperty("type").GetString() ?? throw new Exception();
        fields.Add((fieldName, fieldType));
    }

    return (name, fields);
}

static (UInt160 hash, IReadOnlyList<StorageGroupDef> storages) ParseNativeContractStorage(JsonElement json, StructMap structMap)
{
    var hash = UInt160.Parse(json.GetProperty("hash").GetString() ?? "");
    var storageGroups = json.GetProperty("storages").EnumerateArray()
        .Select(j => ParseStorageGroup(j, structMap))
        .ToList();
    return (hash, storageGroups);
}

static StorageGroupDef ParseStorageGroup(JsonElement json, StructMap structMap)
{
    var name = json.GetProperty("name").GetString() ?? throw new Exception();
    var type = ContractType.Parse(json.GetProperty("type").GetString() ?? throw new Exception(), structMap);
    var prefix = Convert.FromHexString(json.GetProperty("prefix").GetString() ?? throw new Exception());
    var jsonSegments = json.TryGetProperty("segments", out var _segments)
        ? _segments.EnumerateArray() : Enumerable.Empty<JsonElement>();

    var segments = jsonSegments.Select(j => {
        var seg = j.GetString() ?? throw new Exception("null segment string");
        var arr = seg.Split(',');
        if (arr.Length != 2) throw new Exception($"Invalid segment string {seg}");

        var name = arr[0];
        var type = arr[1];

        if (ContractType.TryParsePrimitive(type, out var parsedType))
        {
            return new KeySegment(name, parsedType);
        }

        if (type == "#BigEndianU32")
        {
            return new KeySegment(name, default(BigEndianUInt32));
        }

        if (type == "#BigEndianU64")
        {
            return new KeySegment(name, default(BigEndianUInt64));
        }

        throw new NotSupportedException($"cannot parse ContractType {arr[1]}");
    });

    return new StorageGroupDef(name, prefix.AsMemory(), segments.ToArray(), type);
}

static Stream GetResourceStream(string name)
{
    var assembly = typeof(Program).Assembly;
    var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new FileNotFoundException();
    return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException();
}
