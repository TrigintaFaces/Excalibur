// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class DocumentationApiRuleShould
{
    [Theory]
    [InlineData("using Excalibur; var o = new Options { Count = 1 };", true)]
    [InlineData("using Excalibur; var o = new Options { Missing = 1 };", false)]
    [InlineData("using O = Excalibur.Options; var o = new O { Count = 1 };", true)]
    [InlineData("using Excalibur; var o = new Options { Hidden = 1 };", false)]
    [InlineData("using Excalibur; var o = new Options<int>();", false)]
    [InlineData("using Excalibur; var o = new UnknownExample();", false)]
    [InlineData("using Excalibur; new External.Services().AddDispatch();", true)]
    [InlineData("using Excalibur; new External.Services().MissingDispatch();", false)]
    public void BindActualConsumerSurface(string source, bool valid)
    {
        var block = new DocumentationBlock("docs/test.md", 4, "resolve", source);
        var result = DocumentationApiRule.Analyze(block, [block], References());
        result.Selected.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("resolve", true)]
    [InlineData("ignore", false)]
    public void UseDeclaredContextWithoutBorrowingIgnoredStubs(string tier, bool valid)
    {
        var block = new DocumentationBlock("docs/test.md", 4, "resolve", "using Example; var command = new Command();");
        var context = new DocumentationBlock("docs/test.md", 1, tier, "namespace Example { public class Command {} }");
        var result = DocumentationApiRule.Analyze(block, [context, block], References());
        result.Selected.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBe(!valid);
    }

    [Fact]
    public void PreserveNamespaceQualifiedDuplicateNames()
    {
        var block = new DocumentationBlock("docs/test.md", 4, "resolve", "var o = new Excalibur.Options { Count = 1 };");
        var context = new DocumentationBlock("docs/test.md", 1, "resolve", "namespace Example { public class Options { public int Missing; } }");
        DocumentationApiRule.Analyze(block, [context, block], References()).Selected.ShouldBeEmpty();
    }

    [Fact]
    public void DoNotHoistImportsFromContext()
    {
        var block = new DocumentationBlock("docs/test.md", 4, "resolve", "var o = new Options(); var c = new Command();");
        var context = new DocumentationBlock("docs/test.md", 1, "resolve", "using Excalibur; public class Command {}");
        DocumentationApiRule.Analyze(block, [context, block], References()).Selected.ShouldContain(diagnostic => diagnostic.Id == "CS0246");
    }

    [Theory]
    [InlineData("var c = new Command<int>();", "public class Command<T> {}")]
    [InlineData("Callback c = () => {}; c();", "public delegate void Callback();")]
    public void ResolveGenericAndDelegateContext(string code, string declaration)
    {
        var block = new DocumentationBlock("docs/test.md", 4, "compile", code);
        var context = new DocumentationBlock("docs/test.md", 1, "resolve", declaration);
        var result = DocumentationApiRule.Analyze(block, [context, block], References());
        result.Selected.Concat(result.Context).ShouldBeEmpty();
    }

    [Fact]
    public void ExposeBrokenContextRatherThanCertifyingRunnableCode()
    {
        var block = new DocumentationBlock("docs/test.md", 4, "compile", "new Command().Run();");
        var context = new DocumentationBlock("docs/test.md", 1, "resolve", "public class Command { public void Run() { Missing.Call(); } }");
        var result = DocumentationApiRule.Analyze(block, [context, block], References());
        result.Context.ShouldContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ExposeConflictingContextDeclarations()
    {
        var block = new DocumentationBlock("docs/test.md", 4, "resolve", "new Command();");
        var first = new DocumentationBlock("docs/test.md", 1, "resolve", "public class Command {}");
        var second = new DocumentationBlock("docs/test.md", 2, "resolve", "public class Command {}");
        var result = DocumentationApiRule.Analyze(block, [first, second, block], References());
        result.Selected.Concat(result.Context).ShouldContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("broken-member", false)]
    [InlineData("unknown-application", false)]
    [InlineData("context-conflict", false)]
    public void BindRealGeneratedJsonMetadataWithoutMaskingInvalidCode(string variant, bool valid)
    {
        const string source = """
            using System.Text.Json;
            using System.Text.Json.Serialization;
            public sealed class Payload { public int Value { get; set; } }
            [JsonSerializable(typeof(Payload))]
            public partial class PayloadContext : JsonSerializerContext { }
            public static class Example {
                public static string Render() => JsonSerializer.Serialize(new Payload { Value = 7 }, PayloadContext.Default.Payload);
            }
            """;
        var names = new[] { "System.Private.CoreLib.dll", "System.Runtime.dll", "System.Text.Json.dll", "System.Collections.dll", "System.Memory.dll", "System.Text.Encodings.Web.dll", "System.Runtime.InteropServices.dll" };
        var references = names.Select(SelectedRuntimeReference).ToArray();
        var generatorPath = typeof(DocumentationApiRuleShould).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DocumentationJsonGeneratorPath").Value;
        generatorPath.ShouldNotBeNullOrWhiteSpace();
        File.Exists(generatorPath).ShouldBeTrue();
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("DocumentationGeneratorControl", LanguageNames.CSharp)
            .WithMetadataReferences(references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp14))
            .WithAnalyzerReferences([new AnalyzerFileReference(generatorPath!, new JsonGeneratorLoader())]);
        var generators = GeneratorValidation.Discover(project);
        generators.ShouldNotBeEmpty();
        var direct = CSharpCompilation.Create("DirectJsonControl", [CSharpSyntaxTree.ParseText(source, (CSharpParseOptions)project.ParseOptions!)], references,
            (CSharpCompilationOptions)project.CompilationOptions!);
        CSharpGeneratorDriver.Create(generators, parseOptions: (CSharpParseOptions)project.ParseOptions!)
            .RunGeneratorsAndUpdateCompilation(direct, out var generated, out var generatorDiagnostics);
        generatorDiagnostics.ShouldBeEmpty();
        generated.GetDiagnostics().Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBeEmpty();
        generated.SyntaxTrees.Count().ShouldBeGreaterThan(1);

        var code = variant switch
        {
            "broken-member" => source.Replace("PayloadContext.Default.Payload", "PayloadContext.Default.Missing", StringComparison.Ordinal),
            "unknown-application" => source.Replace("typeof(Payload)", "typeof(UnresolvedApplication)", StringComparison.Ordinal),
            _ => source,
        };
        var block = new DocumentationBlock("docs/generated.md", 10, "resolve", code);
        var context = new DocumentationBlock("docs/generated.md", 1, "resolve", "public sealed class Payload { }");
        var result = DocumentationApiRule.Analyze(block, variant == "context-conflict" ? [context, block] : [block], references, project);
        result.Selected.Concat(result.Context).Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBe(!valid);
    }

    [Fact]
    public void ClearExecutableEntryPointOnlyWhenAdaptingToLibrary()
    {
        var references = new[] { SelectedRuntimeReference("System.Private.CoreLib.dll") };
        var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, mainTypeName: "Entry", nullableContextOptions: NullableContextOptions.Enable);
        const string source = "public static class Entry { public static void Main() { } }";
        var direct = CSharpCompilation.Create("DirectExecutableControl", [CSharpSyntaxTree.ParseText(source)], references, options);
        direct.GetDiagnostics().Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBeEmpty();
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("ExecutableProfile", LanguageNames.CSharp).WithCompilationOptions(options);
        var block = new DocumentationBlock("docs/executable-profile.md", 1, "compile", source);
        var result = DocumentationApiRule.Analyze(block, [block], references, project);
        result.Selected.Concat(result.Context).ShouldBeEmpty();
        var retained = (CSharpCompilationOptions)project.CompilationOptions!;
        retained.MainTypeName.ShouldBe("Entry");
        retained.OutputKind.ShouldBe(OutputKind.ConsoleApplication);
        retained.NullableContextOptions.ShouldBe(NullableContextOptions.Enable);
        options.OutputKind.ShouldBe(OutputKind.ConsoleApplication);
        options.MainTypeName.ShouldBe("Entry");
        options.NullableContextOptions.ShouldBe(NullableContextOptions.Enable);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("broken-member", false)]
    [InlineData("unknown-application", false)]
    public async Task BindActualSdkGlobalUsingsAndNullableContext(string variant, bool valid)
    {
        var path = typeof(DocumentationApiRuleShould).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DocumentationGlobalUsingsPath").Value;
        path.ShouldNotBeNullOrWhiteSpace();
        File.Exists(path).ShouldBeTrue();
        var names = new[] { "System.Private.CoreLib.dll", "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll", "System.Net.Http.dll", "System.Threading.dll", "System.Threading.Tasks.dll", "System.Xml.XDocument.dll", "System.Xml.ReaderWriter.dll", "System.Xml.Linq.dll", "System.Text.Json.dll" };
        var references = names.Select(SelectedRuntimeReference).Cast<MetadataReference>()
            .Concat([MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location), MetadataReference.CreateFromFile(typeof(Shouldly.ShouldAssertException).Assembly.Location)]).ToArray();
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Boundary.Tests", LanguageNames.CSharp)
            .WithMetadataReferences(references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp14));
        var document = project.AddDocument(Path.GetFileName(path!), await File.ReadAllTextAsync(path!), filePath: path);
        var globals = await document.GetSyntaxTreeAsync();
        globals.ShouldNotBeNull();
        const string source = "public static class Example { public static Task<List<string?>> Run(CancellationToken token) => Task.FromResult(new List<string?>()); }";
        var direct = CSharpCompilation.Create("DirectSdkProfileControl",
            [globals!, CSharpSyntaxTree.ParseText(source, (CSharpParseOptions)document.Project.ParseOptions!)], references,
            (CSharpCompilationOptions)document.Project.CompilationOptions!);
        direct.GetDiagnostics().Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBeEmpty();
        var code = variant switch
        {
            "broken-member" => source.Replace("Task.FromResult", "Task.Missing", StringComparison.Ordinal),
            "unknown-application" => source.Replace("List<string?>", "List<MissingApplication>", StringComparison.Ordinal),
            _ => source,
        };
        var block = new DocumentationBlock("docs/profile.md", 1, "compile", code);
        var result = DocumentationApiRule.Analyze(block, [block], references, document.Project);
        result.Selected.Concat(result.Context).Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBe(!valid);
    }

    [Theory]
    [InlineData("Example", "Value", true)]
    [InlineData("Example", "Missing", false)]
    [InlineData("Other.Example", "Value", false)]
    [InlineData("Example<T>", "Value", false)]
    [InlineData("equivalent-namespace", "Value", true)]
    public void ResolveOnlyMatchingPartialDeclarationContext(string declaration, string member, bool valid)
    {
        var block = new DocumentationBlock("docs/partial.md", 10, "compile", (declaration == "equivalent-namespace" ? "namespace A.B { public partial class Example { public int Run() => " + member + "; } }" : "public partial class Example { public int Run() => " + member + "; }"));
        var code = declaration == "equivalent-namespace"
            ? "namespace A { namespace B { public partial class Example : Base {} public class Base { public int Value => 7; } } }"
            : declaration == "Other.Example"
            ? "namespace Other { public partial class Example : Base {} public class Base { public int Value => 7; } }"
            : "public partial class " + declaration + " : Base {} public class Base { public int Value => 7; }";
        var context = new DocumentationBlock("docs/partial.md", 1, "resolve", code);
        var result = DocumentationApiRule.Analyze(block, [context, block], References());
        result.Selected.Concat(result.Context).Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ShouldBe(!valid);
    }

    private static PortableExecutableReference SelectedRuntimeReference(string fileName)
    {
        var requested = Path.GetFileNameWithoutExtension(fileName);
        // Use the runtime's selected binding, not a basename guess among TPA locations.
        var assembly = Assembly.Load(new AssemblyName(requested));
        var identity = assembly.GetName();
        identity.Name.ShouldBe(requested);
        assembly.IsDynamic.ShouldBeFalse();
        File.Exists(assembly.Location).ShouldBeTrue();
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        context.ShouldNotBeNull();
        var bindings = context.Assemblies
            .Where(candidate => string.Equals(candidate.GetName().Name, requested, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        bindings.Length.ShouldBe(1);
        bindings[0].ShouldBeSameAs(assembly);
        TestContext.Current.TestOutputHelper?.WriteLine($"REFERENCE: {identity.FullName}; {assembly.Location}; context={context.Name}");
        return MetadataReference.CreateFromFile(assembly.Location);
    }

    private sealed class JsonGeneratorLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath) { }

        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }

    private static MetadataReference[] References()
    {
        var core = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var framework = CSharpCompilation.Create("Framework", [CSharpSyntaxTree.ParseText("""
            namespace External { public class Services {} }
            namespace Excalibur {
                public class Options { public int Count { get; set; } internal int Hidden { get; set; } }
                public static class Registration { public static void AddDispatch(this External.Services services) {} }
            }
            """)], [core], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        framework.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return [core, framework.ToMetadataReference()];
    }
}
