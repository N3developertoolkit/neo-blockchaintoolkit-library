
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Neo.BlockchainToolkit.Models;
using CliWrap;
using CliWrap.Buffered;
using SemVersion;

// This code is used to generate NativeStructs.cs in the bctklib project. 
// Generated code is written to the console. 
// This command will generate a new NativeStructs file:
//     dotnet run > ..\bctklib\models\NativeStructs.cs

EnableAnsiEscapeSequences();

// Locate reference assemblies

var listSdkResult = await Cli.Wrap("dotnet")
    .WithArguments("--list-sdks")
    .ExecuteBufferedAsync();

var sdkPath = listSdkResult.StandardOutput.Split(Environment.NewLine)
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

var netCoreAppRefPath = Path.Combine(dotnetPath, "packs", "Microsoft.NETCore.App.Ref");
var sdkVersion = Directory.EnumerateDirectories(netCoreAppRefPath)
    .Select(p => SemVersion.SemanticVersion.Parse(Path.GetFileName(p)))
    .Where(v => string.IsNullOrEmpty(v.Prerelease))
    .Where(v => v.Major == 6)
    .OrderByDescending(v => v)
    .FirstOrDefault();

if (sdkVersion is null) throw new Exception("Could not locate 6.x reference assemblies");

var referencesPath = Path.Combine(netCoreAppRefPath, $"{sdkVersion}", "ref", "net6.0");
var refAssemblyFiles = Directory.EnumerateFiles(referencesPath, "*.dll");

// Locate SmartContract Framework sources

var listGlobalPackagesResult = await Cli.Wrap("dotnet")
    .WithArguments("nuget locals global-packages --list")
    .ExecuteBufferedAsync();

const string GLOBAL_PACKAGES = "global-packages:";
var globalPackages = listGlobalPackagesResult.ExitCode == 0 && listGlobalPackagesResult.StandardOutput.StartsWith(GLOBAL_PACKAGES)
    ? listGlobalPackagesResult.StandardOutput.Substring(GLOBAL_PACKAGES.Length).Trim()
    : throw new Exception($"dotnet nuget locals global-packages --list failed");

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
var scfxSourceFiles = Directory.EnumerateFiles(path, "*.cs", enumOptions);

// Generate compilation

var refAssemblies = refAssemblyFiles
    .Select(@ref => MetadataReference.CreateFromFile(@ref));
var scfxSources = scfxSourceFiles
    .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)); 

var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
var compilation = CSharpCompilation.Create(null, scfxSources, refAssemblies, options);

// If there are errors, write diagnostics and exit 

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

// Note: SCFX has not been updated yet to include Address type
//       In the mean time, modify the following UInt160 fields
//       to have Address type instead.

var hash160AsAddress = new List<(string type, string field)>()
{
    ("Nep11TokenState", "Owner"),
    ("Block", "NextConsensus"),
    ("Transaction", "Sender"),
};

// Note: Nep11TokenState and StorageMap are C# helper types that aren't
//       really core types. Not sure they should be included in NeoCoreTypes.

//       StorageMap has two fields, an interop StorageContext + the prefix
//       byte array. Advanced debug view of StorageMap doesn't seem very 
//       important.

//       Nep11TokenState is obviously of limited general use and it's
//       intended to be derived from rather than used directly. Note however
//       that Nep11TokenState subclasses will render Owner field as UInt160
//       instead of Address until Address type gets added to SCFX

// Configure variables needed during code generation

var resolver = new ContractTypeVisitor(compilation);
var generatedTypes = new List<(string fnsqName, string lazyName)>();
var attrib = compilation.FindType("System.Attribute");
var contractAttrib = compilation.FindType("Neo.SmartContract.Framework.Attributes.ContractAttribute");
var types = compilation.SyntaxTrees
    .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
    .Select(typeDef => compilation.GetSemanticModel(typeDef.SyntaxTree).GetDeclaredSymbol(typeDef))
    .OfType<INamedTypeSymbol>()
    .OrderBy(s => s.Name);

// Generate Code Header

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

// generate StructContractType for each SCFX type

foreach (var type in types)
{
    // skip types that don't get code generated

    if (SymbolEquals(type.BaseType, attrib)) continue;
    if (type.TypeKind == TypeKind.Interface) continue;
    if (resolver.NeoPrimitives.Contains(type)) continue;
    if (type.AllInterfaces.Any(i => SymbolEquals(i, type))) continue;
    if (type.IsStatic) continue;
    if (type.GetAttributes().Any(a => SymbolEquals(a.AttributeClass, contractAttrib))) continue;
    if (type.IsGenericType) continue;

    var fields = type.GetAllFields()
        .Where(f => !f.HasConstantValue && !f.IsStatic);
    if (!fields.Any()) continue;

    // create namespace qualified + lazy variable type names

    var nsqName = $"#Neo.{type.Name}";
    var lazyName = $"{char.ToLowerInvariant(type.Name[0])}{type.Name.Substring(1)}";
    if (lazyName == type.Name) lazyName = "_" + lazyName;

    // generate code for private Lazy<StructContractType> field
    // and public StructContractType property

    if (generatedTypes.Count > 0) Console.WriteLine();
    Console.WriteLine($"    public static StructContractType {type.Name} => {lazyName}.Value;");
    Console.WriteLine($"    static readonly Lazy<StructContractType> {lazyName} = new(() => ");
    Console.WriteLine($"        new StructContractType(");
    Console.WriteLine($"            \"{nsqName}\",");
    Console.WriteLine($"            new (string, ContractType)[] {{");
    foreach (var field in fields)
    {
        var fieldType = resolver.Resolve(field.Type);

        // Change Hash160 primitive types to Address
        // for type/field pairs in hash160AsAddress

        if (fieldType is PrimitiveContractType primitive
            && primitive.Type == PrimitiveType.Hash160
            && hash160AsAddress.Any(t => t.type == type.Name && t.field == field.Name))
        {
            fieldType = PrimitiveContractType.Address;
        }

        Console.WriteLine($"                (\"{field.Name}\", {fieldType.AsSource()}),");
    }
    Console.WriteLine($"            }}));");

    // save namespace qualified and lazy variable type names for 
    // creating coreTypes dictionary used by TryGetType

    generatedTypes.Add((nsqName, lazyName));
}

// write out coreTypes lazy dictionary

Console.WriteLine(@"
    static readonly Lazy<IReadOnlyDictionary<string, Lazy<StructContractType>>> coreTypes 
        = new Lazy<IReadOnlyDictionary<string, Lazy<StructContractType>>>(() => new Dictionary<string, Lazy<StructContractType>>()
        {");

foreach (var (fnsq, lazy) in generatedTypes)
{
    Console.WriteLine($"            {{ \"{fnsq}\", {lazy} }},");
}

// write out public TryGetType method

Console.WriteLine(@"        });

    public static bool TryGetType(string name, [MaybeNullWhen(false)] out StructContractType type)
    {
        if (coreTypes.Value.TryGetValue(name, out var lazy))
        {
            type = lazy.Value;
            return true;
        }

        type = default;
        return false;
    }
}");

static bool SymbolEquals(ISymbol? a, ISymbol? b) 
    => SymbolEqualityComparer.Default.Equals(a, b);

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
