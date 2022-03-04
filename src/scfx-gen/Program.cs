﻿
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

EnableAnsiEscapeSequences();

// TODO: use "dotnet nuget locals global-packages --list"
//       returns: global-packages: C:\Users\harry\.nuget\packages\
const string globalPackages = @"C:\Users\harry\.nuget\packages\";

var packagePath = Path.Combine(globalPackages, "neo.smartcontract.framework");
if (!Directory.Exists(packagePath)) throw new Exception("SmartContract Framework not in nuget global packages repo");

// TODO: find latest version or use version specified in CLI argument
//       and use SemanticVersion type

var version = "3.1.0";
var path = Path.Combine(Path.Combine(packagePath, version), "src");
if (!Directory.Exists(path)) throw new Exception($"SmartContract Framework source not found {path}");

var enumOptions = new EnumerationOptions() { RecurseSubdirectories = true };
var trees = Directory.EnumerateFiles(path, "*.cs", enumOptions)
    .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f));
var compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

// TODO: determine targeting pack folder @ runtime

// Details: https://docs.microsoft.com/en-us/dotnet/core/distribution-packaging
// to locate the targeting pack folder
// * run 'dotnet --list-sdks' to get a list of installed SDKs + folder
//   Output should be something like '6.0.102 [C:\Program Files\dotnet\sdk]'.
// * 'packs' folder should be sibling to sdk folder from --list-sdks command
// * 'packs/Microsoft.NETCore.App.Ref/<latest 6.0.x version>/ref/net6.0 

// it's probably not important, but I could probably write  an MSBuild project
// that echoed $(NetCoreRoot) or $(NetCoreTargetingPackRoot) to the console.
// I wonder if you can launch msbuild as a command and pass build steps in on 
// the command line. That would make it very easy to get $(NetCoreTargetingPackRoot)
// But in the short term, I can simply start with the hardcoded path.

const string PACK_FOLDER = @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.2\ref\net6.0";
var references = Directory
    .EnumerateFiles(PACK_FOLDER, "*.dll")
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
    .Select(typeDef => compilation.GetSemanticModel(typeDef.SyntaxTree).GetDeclaredSymbol(typeDef));

var attrib = compilation.FindType("System.Attribute");
var contractAttrib = compilation.FindType("Neo.SmartContract.Framework.Attributes.ContractAttribute");

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

namespace Neo.BlockchainToolkit.Models;

public static class NeoCoreTypes
{");

bool first = true;
foreach (var type in types)
{
    if (type is null) continue;

    Func<ISymbol?, ISymbol?, bool> compare = SymbolEqualityComparer.Default.Equals;
    if (compare(type.BaseType, attrib)) continue;
    if (type.TypeKind == TypeKind.Interface) continue;
    if (resolver.NeoPrimitives.Contains(type)) continue;
    if (type.AllInterfaces.Any(i => compare(i, type))) continue;
    if (type.IsStatic) continue;
    if (type.GetAttributes().Any(a => compare(a.AttributeClass, contractAttrib))) continue;
    if (type.IsGenericType) continue;

    var fields = type.GetMembers()
        .OfType<IFieldSymbol>()
        .Where(f => !f.HasConstantValue && !f.IsStatic);
    if (!fields.Any()) continue;

    var privateName = $"_{char.ToLowerInvariant(type.Name[0])}{type.Name.Substring(1)}";

    if (!first) Console.WriteLine(); else first = false; 
    Console.WriteLine($"    public static StructContractType {type.Name} => {privateName}.Value;");
    Console.WriteLine($"    static readonly Lazy<StructContractType> {privateName} = new(() => ");
    Console.WriteLine($"        new StructContractType(");
    Console.WriteLine($"            \"{type.Name}\",");
    Console.WriteLine($"            new (string, ContractType)[] {{");
    foreach (var field in fields)
    {
        var fieldType = resolver.Resolve(field.Type);
        Console.WriteLine($"                (\"{field.Name}\", {fieldType.AsSource()}),");

    }
    Console.WriteLine($"            }}));");
}
Console.WriteLine("}");

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
