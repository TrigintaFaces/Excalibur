// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class DocumentationReferencesShould
{
    [Fact]
    public void RefuseAnEmptyConsumerPopulation() =>
        Should.Throw<InvalidOperationException>(() => DocumentationReferences.Create([]));

    [Theory]
    [InlineData("relative.dll")]
    [InlineData("")]
    public void RefuseUnresolvedReferencePaths(string path) =>
        Should.Throw<InvalidOperationException>(() => DocumentationReferences.Create([path]));

    [Fact]
    public void KeepUnresolvedPrivateImplementationApisUnavailable()
    {
        var references = DocumentationReferences.Create([typeof(object).Assembly.Location]);
        var allowed = new DocumentationBlock("consumer.md", 1, "compile", "public class Example { public object Value = new object(); }");
        var denied = allowed with { Code = "public class Example { public Boundary.Tests.DocumentationReferencesShould Value; }" };
        DocumentationApiRule.Analyze(allowed, [allowed], references).Selected
            .ShouldNotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        DocumentationApiRule.Analyze(denied, [denied], references).Selected
            .ShouldContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("docs-site/docs/deployment/aws-lambda.md", "samples/05-serverless/AwsLambda/AwsLambda.csproj")]
    [InlineData("docs-site/docs/deployment/azure-functions.md", "samples/05-serverless/AzureFunctions/AzureFunctions.csproj")]
    [InlineData("docs-site/docs/deployment/google-cloud-functions.md", "samples/05-serverless/GoogleCloudFunctions/GoogleCloudFunctions.csproj")]
    [InlineData("docs-site/docs/advanced/native-aot.md", "samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj")]
    [InlineData("docs-site/docs/outbox/index.md", null)]
    [InlineData("docs-site/docs/deployment/unlisted-host.md", null)]
    public void KeepOptionalProfilesExplicitAndPageScoped(string page, string? expected) =>
        DocumentationCensus.ProfileProject(page).ShouldBe(expected);

    [Theory]
    [InlineData("samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj", true)]
    [InlineData("samples/05-serverless/AwsLambda/AwsLambda.csproj", false)]
    [InlineData("samples/05-serverless/AzureFunctions/AzureFunctions.csproj", false)]
    [InlineData("samples/05-serverless/GoogleCloudFunctions/GoogleCloudFunctions.csproj", false)]
    [InlineData("unlisted.csproj", false)]
    [InlineData(null, false)]
    public void KeepNativeGeneratorContextSeparateFromHostRuntimeGraphs(string? project, bool expected) =>
        DocumentationReferences.IsGeneratorOnlyProfile(project).ShouldBe(expected);

    [Fact]
    public async Task UseSdkProjectNameRatherThanDifferentAssemblyNameForGlobalImports()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
            "AwsLambda", "AwsLambdaSample", LanguageNames.CSharp,
            filePath: Path.Combine(Path.GetTempPath(), "AwsLambda.csproj")));
        project = project.AddDocument("AwsLambda.GlobalUsings.g.cs", "global using System;",
            filePath: Path.Combine(Path.GetTempPath(), "AwsLambda.GlobalUsings.g.cs")).Project;
        project = project.AddDocument("Business.cs", "public class PrivateSampleBusiness {} ").Project;
        foreach (var document in project.Documents)
        {
            (await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        }

        var globals = DocumentationConsumerContext.GlobalUsings(project);
        globals.Length.ShouldBe(1);
        globals[0].GetText().ToString().ShouldBe("global using System;");
        var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var source = new DocumentationBlock("consumer.md", 1, "compile", "public class Example { public PrivateSampleBusiness Value = null!; }");
        DocumentationApiRule.Analyze(source, [source], [reference], project).Selected
            .ShouldContain(diagnostic => diagnostic.Id == "CS0246");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefuseConflictingAssemblyBindings(bool differentVersion)
    {
        var directory = Path.Combine(Path.GetTempPath(), "documentation-bindings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = EmitReference(directory, "first.dll", "1.0.0.0", 1);
            var second = EmitReference(directory, "second.dll", differentVersion ? "2.0.0.0" : "1.0.0.0", 2);
            Should.Throw<InvalidOperationException>(() => DocumentationReferences.Merge([first], [second]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void KeepCanonicalBindingForIdenticalAssemblyBytes()
    {
        var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var merged = DocumentationReferences.Merge([reference], [reference]);
        merged.Length.ShouldBe(1);
        merged[0].ShouldBeSameAs(reference);
    }

    private static MetadataReference EmitReference(string directory, string file, string version, int value)
    {
        var source = "[assembly: System.Reflection.AssemblyVersion(\"" + version + "\")] public class Marker { public const int Value = " + value + "; }";
        var compilation = CSharpCompilation.Create("OptionalProfile", [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = Path.Combine(directory, file);
        var emitted = compilation.Emit(path);
        emitted.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitted.Diagnostics));
        return MetadataReference.CreateFromFile(path);
    }
}
