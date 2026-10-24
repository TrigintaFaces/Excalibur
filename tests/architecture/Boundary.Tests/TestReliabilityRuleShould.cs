// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class TestReliabilityRuleShould
{
    [Theory]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(5)); _ = task.WaitAsync(TimeSpan.FromSeconds(30));", 1)]
    [InlineData("var wait = TimeSpan.FromSeconds(30); using var c = new CTS(TimeSpan.FromSeconds(5)); _ = task.WaitAsync(wait);", 1)]
    [InlineData("var wait = TimeSpan.FromSeconds(2); wait = TimeSpan.FromSeconds(30); using var c = new CTS(TimeSpan.FromSeconds(5)); _ = task.WaitAsync(wait);", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(50)); _ = task.WaitAsync(TimeSpan.FromSeconds(30));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(5)); // deadline-ok: deliberately testing cancellation\n _ = task.WaitAsync(TimeSpan.FromSeconds(30));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(5)); Action other = () => { _ = task.WaitAsync(TimeSpan.FromSeconds(30)); };", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(5)); var unrelated = TimeSpan.FromSeconds(30); _ = task.WaitAsync(TimeSpan.FromSeconds(2));", 0)]
    public void PairDeadlinesWithinTheirCallable(string body, int expected)
    {
        var compilation = Compile("Fixture", "public void Run(Task task) { " + body + " }");
        NestedDeadlineRule.Find(compilation, compilation.SyntaxTrees.Single(), "tests/Fixture.cs").Count().ShouldBe(expected);
    }

    [Fact]
    public void PreserveTimeoutSubjectInsideLambda()
    {
        var compilation = Compile("Fixture", """
            public void RespectTimeout(Task task) {
                Action action = () => { using var c = new CTS(TimeSpan.FromSeconds(5)); _ = task.WaitAsync(TimeSpan.FromSeconds(30)); };
            }
            """);
        NestedDeadlineRule.Find(compilation, compilation.SyntaxTrees.Single(), "tests/Fixture.cs").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("public void Run() { Xunit.Assert.Skip(\"no\"); }", 1)]
    [InlineData("public Fixture() { Xunit.Assert.Skip(\"no\"); }", 1)]
    [InlineData("public void Run() { void Local() { Xunit.Assert.Skip(\"no\"); } }", 1)]
    [InlineData("public void Run() { Action action = () => { Xunit.Assert.Skip(\"no\"); }; }", 1)]
    [InlineData("public void Run(bool enabled) { if (enabled) { Xunit.Assert.Skip(\"no\"); } }", 0)]
    [InlineData("public void Run() => Xunit.Assert.Skip(\"no\");", 1)]
    [InlineData("public int Value { get { Xunit.Assert.Skip(\"no\"); return 0; } }", 1)]
    [InlineData("public void Run() { var text = \"Assert.Skip(no)\"; }", 0)]
    public void RecognizeOnlyActualUnconditionalSkipEntries(string members, int expected)
    {
        var compilation = Compile("xunit.fixture", members, "namespace Xunit { public static class Assert { public static void Skip(string reason) {} } }");
        UnconditionalSkipRule.Find(compilation, compilation.SyntaxTrees.Single()).Count().ShouldBe(expected);
    }

    [Fact]
    public void RejectApplicationAssertImpostor()
    {
        var compilation = Compile("Application", "public void Run() { Xunit.Assert.Skip(\"no\"); }", "namespace Xunit { public static class Assert { public static void Skip(string reason) {} } }");
        UnconditionalSkipRule.Find(compilation, compilation.SyntaxTrees.Single()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("postgres:16-alpine", "concrete")]
    [InlineData("localhost:5000/postgres:16", "concrete")]
    [InlineData("localhost:5000/postgres", "floating")]
    [InlineData("server:2022-latest", "floating")]
    [InlineData("server:", "floating")]
    [InlineData("server@sha256:abc", "digest")]
    [InlineData(null, "unresolved")]
    public void ClassifyImageReferences(string? value, string expected) => ContainerImageRule.Classify(value).ShouldBe(expected);

    [Theory]
    [InlineData("Testcontainers", 1)]
    [InlineData("Application", 0)]
    public void BindImageConstantsOnlyOnActualContainerApi(string assembly, int expected)
    {
        var compilation = Compile(assembly, """
            public Fixture WithImage(string image) => this;
            public void Run() { const string image = "postgres:16"; this.WithImage(image); }
            """);
        var facts = ContainerImageRule.Find(compilation, compilation.SyntaxTrees.Single(), "tests/Fixture.cs").ToArray();
        facts.Length.ShouldBe(expected);
        if (expected > 0)
        {
            facts[0].Classification.ShouldBe("concrete");
        }
    }

    private static CSharpCompilation Compile(string assembly, string members, string extra = "")
    {
        var source = "using System; using System.Threading.Tasks; using CTS = System.Threading.CancellationTokenSource; public class Fixture { " + members + " } " + extra;
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }
}
