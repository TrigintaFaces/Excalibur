// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class ConformanceForkSemanticsShould
{
    private static readonly MetadataReference CoreLibrary = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    [Fact]
    public void RefuseInactiveInheritanceEvenInAnOtherwiseCompiledFile()
    {
        const string source = "class Active {}\n#if DEBUG\nabstract class PrivateConformance {} class Suite : PrivateConformance {}\n#endif";
        var releaseCoverage = new HashSet<string> { SourceOwnership.SyntaxIdentity(CSharpSyntaxTree.ParseText(source)) };
        SourceOwnership.HasUncoveredSyntax(source, releaseCoverage).ShouldBeTrue();
        var allCoverage = CSharpConfigurations.Parse(source).Select(SourceOwnership.SyntaxIdentity).ToHashSet();
        SourceOwnership.HasUncoveredSyntax(source, allCoverage).ShouldBeFalse();
    }

    [Theory]
    [InlineData("class Suite :\n#if DEBUG\n PrivateConformance\n#else\n PublishedConformance\n#endif\n {}")]
    [InlineData("#if DEBUG\nusing Kit = PrivateConformance;\n#else\nusing Kit = PublishedConformance;\n#endif\nclass Suite : Kit {}")]
    [InlineData("#if DEBUG\nglobal using Kit = PrivateConformance;\n#else\nglobal using Kit = PublishedConformance;\n#endif")]
    public void RefuseUncompiledBaseExpressionsAndCrossFileAliases(string source)
    {
        var covered = new HashSet<string> { SourceOwnership.SyntaxIdentity(CSharpSyntaxTree.ParseText(source)) };
        SourceOwnership.HasUncoveredSyntax(source, covered).ShouldBeTrue();
    }

    [Fact]
    public void RefuseConditionalCoverageBorrowedFromAnotherOwningProject()
    {
        const string source = "#if DEBUG\nglobal using Kit = PrivateConformance;\n#else\nglobal using Kit = PublishedConformance;\n#endif";
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, source);
            var release = Compile("").AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, path: path));
            var evidence = new ConformanceEvidence();
            Should.Throw<InvalidOperationException>(() => evidence.Add(release, null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PermitRetiringTheLastPrivateContract() => ConformancePolicy.Evaluate([], ["# no remaining debt"]).Passed.ShouldBeTrue();

    [Fact]
    public void RejectANewContractWithAnAlreadyBaselinedBareName()
    {
        var verdict = ConformancePolicy.Evaluate([new("Contract", "OtherAssembly:Private.Contract", "Tests:Suite")], ["Assembly:Private.Contract"]);
        verdict.Passed.ShouldBeFalse();
        verdict.NewContracts.ShouldContain("OtherAssembly:Private.Contract");
        verdict.StaleExceptions.ShouldContain("Assembly:Private.Contract");
    }

    [Fact]
    public void AcceptOnlyTheExactMeasuredContractIdentity() =>
        ConformancePolicy.Evaluate([new("Contract", "Assembly:Private.Contract", "Tests:Suite")], ["Assembly:Private.Contract"]).Passed.ShouldBeTrue();

    [Theory]
    [InlineData("Contract")]
    [InlineData("Assembly:Private.Contract\nAssembly:Private.Contract")]
    public void RefuseAmbiguousExceptionLedgers(string entries) =>
        Should.Throw<InvalidOperationException>(() => ConformancePolicy.Evaluate([], entries.Split('\n')));

    [Theory]
    [InlineData("class Leaf {}", false)]
    [InlineData("class Leaf { string Text = \"class Suite : HiddenConformance {}\"; }", false)]
    [InlineData("class Suite : HiddenConformance {}", true)]
    [InlineData("record Suite : HiddenConformance;", true)]
    [InlineData("#if HIDDEN\nclass Suite : HiddenConformance {}\n#endif", true)]
    public void RefuseUncompiledInheritanceEvenInInactiveBranches(string source, bool expected) =>
        SourceOwnership.HasInheritance(source).ShouldBe(expected);

    [Theory]
    [InlineData("tests", 1)]
    [InlineData("src", 0)]
    [InlineData("tests-other", 0)]
    public void ScopePrivateContractsToTheTestSourceTree(string directory, int expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "conformance-scope");
        var tree = CSharpSyntaxTree.ParseText("abstract class PrivateConformance {} class Suite : PrivateConformance {}",
            path: Path.Combine(root, directory, "Fixture.cs"));
        var compilation = Compile("").AddSyntaxTrees(tree);
        ConformanceForkRule.Analyze(compilation, testRoot: Path.Combine(root, "tests")).Count.ShouldBe(expected);
    }

    [Fact]
    public void RecognizePublishedKitsAfterDependencyRetargeting()
    {
        var originalDependency = Compile("[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")] namespace Dependency; public class Value {}")
            .WithAssemblyName("Dependency");
        var replacementDependency = Compile("[assembly: System.Reflection.AssemblyVersion(\"2.0.0.0\")] namespace Dependency; public class Value {}")
            .WithAssemblyName("Dependency");
        var published = Compile("namespace Published; public abstract class StoreConformanceTestKit { public Dependency.Value GetValue() => new(); }", originalDependency.ToMetadataReference())
            .WithAssemblyName(ConformanceForkRule.PublishedAssembly);
        var reference = published.ToMetadataReference();
        var consumer = Compile("class Suite : Published.StoreConformanceTestKit {}", reference, replacementDependency.ToMetadataReference());
        consumer.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var visible = (IAssemblySymbol)consumer.GetAssemblyOrModuleSymbol(reference)!;
        SymbolEqualityComparer.Default.Equals(published.Assembly, visible).ShouldBeFalse("The control must actually retarget the source assembly.");
        var resolved = PublishedContracts.ResolveReference(consumer, published.Assembly);
        SymbolEqualityComparer.Default.Equals(resolved, visible).ShouldBeTrue();
        var evidence = new ConformanceEvidence();
        evidence.Add(consumer, resolved).ShouldBe(0);
        evidence.PublishedDerivations.ShouldBe(1);
        evidence.PublishedKits.ShouldContain("Published.StoreConformanceTestKit");
    }

    [Fact]
    public void AnchorPublishedExemptionsToSourceReferencesRatherThanAssemblyNames()
    {
        var published = Compile("namespace Published; public abstract class StoreConformanceTestKit {}")
            .WithAssemblyName(ConformanceForkRule.PublishedAssembly);
        var consumer = Compile("class Suite : Published.StoreConformanceTestKit {}", published.ToMetadataReference());
        var resolved = PublishedContracts.ResolveReference(consumer, published.Assembly);
        resolved.ShouldNotBeNull();
        ConformanceForkRule.Analyze(consumer, resolved).ShouldBeEmpty();

        var impostor = Compile("namespace Published; public abstract class StoreConformanceTestKit {}")
            .WithAssemblyName(ConformanceForkRule.PublishedAssembly);
        PublishedContracts.ResolveReference(consumer, impostor.Assembly).ShouldBeNull();
    }

    private static CSharpCompilation Compile(string source, params MetadataReference[] references) =>
        CSharpCompilation.Create("Repository.Tests", [CSharpSyntaxTree.ParseText(source)],
            new[] { CoreLibrary }.Concat(references), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static MetadataReference PublishedKit(string assemblyName = ConformanceForkRule.PublishedAssembly)
    {
        var compilation = Compile("namespace Published; public abstract class StoreConformanceTestKit {}").WithAssemblyName(assemblyName);
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Theory]
    [InlineData("using Kit = Published.StoreConformanceTestKit; class Suite : Kit {}")]
    [InlineData("class Suite : global::Published.StoreConformanceTestKit {}")]
    [InlineData("class Outer { public class Suite : Published.StoreConformanceTestKit {} }")]
    public void AcceptPublishedAssemblyIdentityAcrossCSharpSpellings(string source)
    {
        var kit = PublishedKit();
        var compilation = Compile(source, kit);
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        ConformanceForkRule.Analyze(compilation, (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(kit)!).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("abstract class StoreConformanceTestKit {} class Suite : StoreConformanceTestKit {}", 1)]
    [InlineData("abstract class StoreConformanceTestKit {} abstract class Middle : StoreConformanceTestKit {} class Suite : Middle {}", 1)]
    [InlineData("abstract class StoreConformanceTestKit<T> {} class Suite : StoreConformanceTestKit<int> {}", 1)]
    [InlineData("class Outer { private abstract class StoreConformanceTestKit {} private class Suite : StoreConformanceTestKit {} }", 1)]
    [InlineData("abstract class StoreConformanceTestKit {}", 0)]
    [InlineData("class StoreConformanceTestKit {} class Suite : StoreConformanceTestKit {}", 0)]
    [InlineData("class Suite { string Text = \"class Fork : StoreConformanceTestKit {}\"; }", 0)]
    public void InspectResolvedInheritanceRatherThanDeclarationText(string source, int expected)
    {
        var compilation = Compile(source, PublishedKit());
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        ConformanceForkRule.Analyze(compilation).Count.ShouldBe(expected);
    }

    [Fact]
    public void RejectAPrivateShadowEvenWhenItsBareNameMatchesThePublishedKit()
    {
        var compilation = Compile("namespace Private { abstract class StoreConformanceTestKit {} class Suite : StoreConformanceTestKit {} }", PublishedKit());
        var fork = ConformanceForkRule.Analyze(compilation).ShouldHaveSingleItem();
        fork.BaseIdentity.ShouldBe("Repository.Tests:Private.StoreConformanceTestKit");
        fork.SuiteIdentity.ShouldBe("Repository.Tests:Private.Suite");
    }

    [Fact]
    public void DistinguishTheDefiningAssemblyFromTheNamespace()
    {
        var compilation = Compile("class Suite : Published.StoreConformanceTestKit {}", PublishedKit("Private.Test.Helpers"));
        ConformanceForkRule.Analyze(compilation).ShouldHaveSingleItem().BaseIdentity
            .ShouldBe("Private.Test.Helpers:Published.StoreConformanceTestKit");
    }

    [Fact]
    public void RefuseUnresolvedBaseSymbols() =>
        Should.Throw<InvalidOperationException>(() => ConformanceForkRule.Analyze(Compile("class Suite : MissingConformanceKit {}")));

    [Fact]
    public async Task RefuseBrokenReferencedMethodBodyEvenWhenRootCompilationIsClean()
    {
        using var workspace = new AdhocWorkspace();
        var helperId = ProjectId.CreateNewId();
        var rootId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(helperId, "Helper", "Helper", LanguageNames.CSharp)
            .WithProjectCompilationOptions(helperId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(helperId, CoreLibrary)
            .AddDocument(DocumentId.CreateNewId(helperId), "Helper.cs", "public abstract class PrivateConformanceKit { public int Value() => MissingValue; }")
            .AddProject(rootId, "Root", "Root", LanguageNames.CSharp)
            .WithProjectCompilationOptions(rootId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(rootId, CoreLibrary)
            .AddProjectReference(rootId, new ProjectReference(helperId))
            .AddDocument(DocumentId.CreateNewId(rootId), "Suite.cs", "public class Suite : PrivateConformanceKit {}");
        var root = solution.GetProject(rootId)!;
        var compilation = await root.GetCompilationAsync();
        compilation!.GetDiagnostics().ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => CompilationValidation.ValidateProjectClosureAsync(root, CancellationToken.None));
    }

    [Fact]
    public void RefuseToTrustAnAssemblyNameWithoutAnAnchoredPublishedSymbol()
    {
        var compilation = Compile("class Suite : Published.StoreConformanceTestKit {}", PublishedKit());
        ConformanceForkRule.Analyze(compilation).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseGeneratorFailureEvenWhenOrdinarySourceCompiles(bool missing)
    {
        using var workspace = new AdhocWorkspace();
        var id = ProjectId.CreateNewId();
        AnalyzerReference reference = missing ? new UnresolvedAnalyzerReference("missing-generator.dll") : new ThrowingGeneratorReference();
        var solution = workspace.CurrentSolution.AddProject(id, "Root", "Root", LanguageNames.CSharp)
            .WithProjectCompilationOptions(id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(id, CoreLibrary)
            .AddAnalyzerReference(id, reference)
            .AddDocument(DocumentId.CreateNewId(id), "Root.cs", "public class Root {}");
        var project = solution.GetProject(id)!;
        var compilation = await project.GetCompilationAsync();
        compilation!.GetDiagnostics().ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => GeneratorValidation.ValidateAsync(project, compilation, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptPublishedSymbolsAcrossActualSourceProjectReferences()
    {
        using var workspace = new AdhocWorkspace();
        var publishedId = ProjectId.CreateNewId();
        var rootId = ProjectId.CreateNewId();
        var repository = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "semantic-contract-root"));
        var publishedPath = Path.Combine(repository, "src", "Excalibur", "Excalibur.Testing.Conformance", "Excalibur.Testing.Conformance.csproj");
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(publishedId, VersionStamp.Create(), "Published", ConformanceForkRule.PublishedAssembly, LanguageNames.CSharp, filePath: publishedPath))
            .WithProjectCompilationOptions(publishedId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(publishedId, CoreLibrary)
            .AddDocument(DocumentId.CreateNewId(publishedId), "Kit.cs", "public abstract class StoreConformanceKit {}")
            .AddProject(rootId, "Root", "Root", LanguageNames.CSharp)
            .WithProjectCompilationOptions(rootId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(rootId, CoreLibrary)
            .AddProjectReference(rootId, new ProjectReference(publishedId))
            .AddDocument(DocumentId.CreateNewId(rootId), "Suite.cs", "public class Suite : StoreConformanceKit {}");
        var root = solution.GetProject(rootId)!;
        var compilation = await root.GetCompilationAsync();
        var published = await PublishedContracts.FindAsync(root, repository, CancellationToken.None);
        published.ShouldNotBeNull();
        ConformanceForkRule.Analyze(compilation!, published).ShouldBeEmpty();
        ConformanceForkRule.Analyze(compilation!).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RefuseAFileReferenceWhoseLoadFailureWasAlreadyCached()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var workspace = new AdhocWorkspace();
            var reference = new AnalyzerFileReference(path, new TestAssemblyLoader());
            var failures = 0;
            reference.AnalyzerLoadFailed += (_, _) => failures++;
            reference.GetGenerators(LanguageNames.CSharp).ShouldBeEmpty();
            failures.ShouldBeGreaterThan(0);
            var originalFailures = failures;
            reference.GetGenerators(LanguageNames.CSharp).ShouldBeEmpty();
            failures.ShouldBe(originalFailures);
            var project = GeneratorProject(workspace, reference);
            var compilation = await project.GetCompilationAsync();
            compilation!.GetDiagnostics().ShouldBeEmpty();
            await Should.ThrowAsync<InvalidOperationException>(() => GeneratorValidation.ValidateAsync(project, compilation, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RequireVerifiedGeneratorOutputToMatchTheAuditedCompilation(int changeOutput)
    {
        using var workspace = new AdhocWorkspace();
        var reference = new TextGeneratorReference();
        var project = GeneratorProject(workspace, reference);
        var compilation = await project.GetCompilationAsync();
        compilation!.GetDiagnostics().ShouldBeEmpty();
        if (changeOutput != 0)
        {
            reference.Source = "public class ChangedConformanceSuite {}";
            reference.Enabled = changeOutput != 2;
            await Should.ThrowAsync<InvalidOperationException>(() => GeneratorValidation.ValidateAsync(project, compilation, CancellationToken.None));
        }
        else
        {
            await GeneratorValidation.ValidateAsync(project, compilation, CancellationToken.None);
        }
    }

    private static Project GeneratorProject(AdhocWorkspace workspace, AnalyzerReference reference)
    {
        var id = ProjectId.CreateNewId();
        return workspace.CurrentSolution.AddProject(id, "Root", "Root", LanguageNames.CSharp)
            .WithProjectCompilationOptions(id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(id, CoreLibrary)
            .AddAnalyzerReference(id, reference)
            .AddDocument(DocumentId.CreateNewId(id), "Root.cs", "public class Root {}")
            .GetProject(id)!;
    }

    private sealed class TestAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath) { }
        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }

    private sealed class TextGeneratorReference : AnalyzerReference
    {
        public string Source { get; set; } = "public class GeneratedSuite {}";
        public bool Enabled { get; set; } = true;
        public override string FullPath => "text-generator";
        public override object Id => this;
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];
        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) => Enabled ? [new TextGenerator(Source).AsSourceGenerator()] : [];
    }

    private sealed class TextGenerator(string source) : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterPostInitializationOutput(output => output.AddSource("Generated.cs", source));
    }

    private sealed class ThrowingGeneratorReference : AnalyzerReference
    {
        public override string FullPath => "test-generator";
        public override object Id => this;
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];
        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) => [new ThrowingGenerator().AsSourceGenerator()];
    }

    private sealed class ThrowingGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterPostInitializationOutput(_ => throw new InvalidOperationException("Deliberate generator failure."));
    }
}
