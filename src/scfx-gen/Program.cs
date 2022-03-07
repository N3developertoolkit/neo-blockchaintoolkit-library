
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Neo.BlockchainToolkit.Models;
using CliWrap;
using CliWrap.Buffered;
using SemVersion;

EnableAnsiEscapeSequences();

var result = await Cli.Wrap("dotnet")
    .WithArguments("nuget locals global-packages --list")
    .ExecuteBufferedAsync();

var globalPackages = result.ExitCode == 0 && result.StandardOutput.StartsWith("global-packages:")
    ? result.StandardOutput.Substring(16).Trim()
    : throw new Exception($"dotnet nuget locals global-packages --list failed");

result = await Cli.Wrap("dotnet")
    .WithArguments("--list-sdks")
    .ExecuteBufferedAsync();

var sdkPath = result.StandardOutput.Split(Environment.NewLine)
    .Select(l => Regex.Match(l, @"(.*)\ \[(.*)\]"))
    .Where(m => m.Success)
    .Select(m => (
        version: SemanticVersion.Parse(m.Groups[1].Value),
        path: (string?)m.Groups[2].Value))
    .OrderByDescending(t => t.version)
    .Select(t => t.path)
    .FirstOrDefault();

var dotnetPath = Path.GetDirectoryName(sdkPath);
if (string.IsNullOrEmpty(dotnetPath)) throw new Exception("dotnet --list-sdks failed");

var packagePath = Path.Combine(globalPackages, "neo.smartcontract.framework");
if (!Directory.Exists(packagePath)) throw new Exception("SmartContract Framework not in nuget global packages repo");

var latestVersion = Directory.EnumerateDirectories(packagePath)
    .Select(p => SemVersion.SemanticVersion.Parse(Path.GetFileName(p)))
    .Where(v => string.IsNullOrEmpty(v.Prerelease))
    .OrderByDescending(v => v)
    .FirstOrDefault();

if (latestVersion is null) throw new Exception("valid version of SCFX not found");

var path = Path.Combine(Path.Combine(packagePath, $"{latestVersion}"), "src");
if (!Directory.Exists(path)) throw new Exception($"SmartContract Framework source not found {path}");

var enumOptions = new EnumerationOptions() { RecurseSubdirectories = true };
var trees = Directory.EnumerateFiles(path, "*.cs", enumOptions)
    .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));
var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

var netCoreAppRefPath = Path.Combine(dotnetPath, "packs", "Microsoft.NETCore.App.Ref");
var sdkVersion = Directory.EnumerateDirectories(netCoreAppRefPath)
    .Select(p => SemVersion.SemanticVersion.Parse(Path.GetFileName(p)))
    .Where(v => string.IsNullOrEmpty(v.Prerelease))
    .Where(v => v.Major == 6)
    .OrderByDescending(v => v)
    .FirstOrDefault();

if (sdkVersion is null) throw new Exception("Could not locate 6.x reference assemblies");

var referencesPath = Path.Combine(netCoreAppRefPath, $"{sdkVersion}", "ref", "net6.0");
var references = Directory
    .EnumerateFiles(referencesPath, "*.dll")
    .Select(@ref => MetadataReference.CreateFromFile(@ref));
var compilation = CSharpCompilation.Create(null, trees, references, compileOptions);

var diags = compilation.GetDiagnostics();
if (diags.Any(d => d.Severity == DiagnosticSeverity.Error))
{
    foreach (var d in diags)
    {
        var colorCode = d.Severity switch
        {
            DiagnosticSeverity.Error => 31,
            DiagnosticSeverity.Warning => 33,
            DiagnosticSeverity.Info => 37,
            DiagnosticSeverity.Hidden => 36,
            _ => throw new Exception(),
        };
        Console.WriteLine($"\x1b[{colorCode}m{d}\x1b[0m");
    }
    return;
}

var resolver = new ContractTypeVisitor(compilation);

var types = compilation.SyntaxTrees
    .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
    .Select(typeDef => compilation.GetSemanticModel(typeDef.SyntaxTree).GetDeclaredSymbol(typeDef))
    .OfType<INamedTypeSymbol>()
    .OrderBy(s => s.Name);

var attrib = compilation.FindType("System.Attribute");
var contractAttrib = compilation.FindType("Neo.SmartContract.Framework.Attributes.ContractAttribute");

var hash160AsAddress = new List<(string type, string field)>()
{
    ("Nep11TokenState", "Owner"),
    ("Block", "NextConsensus"),
    ("Transaction", "Sender"),
};

var skipTypes = new List<string>()
{
    "Nep11TokenState", "StorageMap"
};

// Note: Nep11TokenState and StorageMap are C# helper types that aren't
//       really core types. Not sure they should be included in NeoCoreTypes.
//       StorageMap has two fields, an interop StorageContext + the prefix
//       byte array. Advanced debug view of StorageMap doesn't seem very 
//       important. Nep11TokenState is obviously of limited general use and
//       it's intentded to be derived rather than used directly. On the 
//       other hand, not sure how to ensure Nep11TokenState.Owner renders
//       as account rather than UInt160 in a derived type w/o modifying
//       the C# source. 


Console.WriteLine(@"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the scfx-gen tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.Models;

public static partial class NativeStructs
{");

bool first = true;
List<(string fnsqName, string lazyName)> generatedTypes = new();

foreach (var type in types)
{
    Func<ISymbol?, ISymbol?, bool> compare = SymbolEqualityComparer.Default.Equals;
    if (compare(type.BaseType, attrib)) continue;
    if (type.TypeKind == TypeKind.Interface) continue;
    if (resolver.NeoPrimitives.Contains(type)) continue;
    if (type.AllInterfaces.Any(i => compare(i, type))) continue;
    if (type.IsStatic) continue;
    if (type.GetAttributes().Any(a => compare(a.AttributeClass, contractAttrib))) continue;
    if (type.IsGenericType) continue;
    if (skipTypes.Contains(type.Name)) continue;

    var fields = type.GetAllFields()
        .Where(f => !f.HasConstantValue && !f.IsStatic);
    if (!fields.Any()) continue;

    var fnsqName = $"Neo#{type.Name}";
    var lazyName = $"_{char.ToLowerInvariant(type.Name[0])}{type.Name.Substring(1)}";
    generatedTypes.Add((fnsqName, lazyName));

    if (!first) Console.WriteLine(); else first = false;
    Console.WriteLine($"    public static StructContractType {type.Name} => {lazyName}.Value;");
    Console.WriteLine($"    static readonly Lazy<StructContractType> {lazyName} = new(() => ");
    Console.WriteLine($"        new StructContractType(");
    Console.WriteLine($"            \"{fnsqName}\",");
    Console.WriteLine($"            new (string, ContractType)[] {{");
    foreach (var field in fields)
    {
        var fieldType = resolver.Resolve(field.Type);

        if (fieldType is PrimitiveContractType primitive
            && primitive.Type == PrimitiveType.Hash160
            && hash160AsAddress.Any(t => t.type == type.Name && t.field == field.Name))
        {
            fieldType = PrimitiveContractType.Address;
        }

        Console.WriteLine($"                (\"{field.Name}\", {fieldType.AsSource()}),");

    }
    Console.WriteLine($"            }}));");
}

Console.WriteLine(@"
    static IReadOnlyDictionary<string, Lazy<StructContractType>> coreTypes 
        = new Dictionary<string, Lazy<StructContractType>>()
        {");


foreach (var (fnsq, lazy) in generatedTypes)
{
    Console.WriteLine($"            {{ \"{fnsq}\", {lazy} }},");
}

Console.WriteLine(@"        };

    public static bool TryGetType(string name, [MaybeNullWhen(false)] out StructContractType type)
    {
        if (coreTypes.TryGetValue(name, out var lazy))
        {
            type = lazy.Value;
            return true;
        }

        type = default;
        return false;
    }
}");

static void EnableAnsiEscapeSequences()
{
    const int STD_OUTPUT_HANDLE = -11;
    var stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);

    if (GetConsoleMode(stdOutHandle, out uint outMode))
    {
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        outMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
        SetConsoleMode(stdOutHandle, outMode);
    }
}

[DllImport("kernel32.dll")]
static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

[DllImport("kernel32.dll")]
static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetStdHandle(int nStdHandle);
