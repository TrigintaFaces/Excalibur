// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RepositoryAnalyzers;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CosmosFixturePolicyShould
{
    private const string Contracts = """
        namespace Testcontainers.CosmosDb {
            public class CosmosDbBuilder { public CosmosDbContainer Build() => new(); }
            public class CosmosDbContainer {
                public string GetConnectionString() => "";
                public object HttpClient => new();
            }
        }
        namespace Microsoft.Azure.Cosmos {
            public class CosmosClient { public CosmosClient(string connectionString = "") {} }
        }
        namespace Microsoft.Azure.Cosmos.Fluent {
            public class CosmosClientBuilder {
                public CosmosClientBuilder(string connectionString) {}
                public CosmosClientBuilder WithHttpClientFactory(System.Func<object> factory) => this;
                public Microsoft.Azure.Cosmos.CosmosClient Build() => new();
            }
        }
        """;
    private const string GoodClient = "new CosmosClientBuilder(_container.GetConnectionString()).WithHttpClientFactory(() => _container.HttpClient).Build()";

    private static CosmosFixtureResult Inspect(string body, string extra = "", string[]? symbols = null) =>
        CosmosFixtureRule.Analyze(Compile(body, extra, symbols));

    private static CSharpCompilation Compile(string body, string extra = "", string[]? symbols = null, string builder = "CosmosDbBuilder")
    {
        var source = """
            using Testcontainers.CosmosDb;
            using Microsoft.Azure.Cosmos.Fluent;
            using Alias = Testcontainers.CosmosDb.CosmosDbBuilder;
            class Fixture {
                private readonly CosmosDbContainer _container = new CosmosDbBuilder().Build();
                public void Initialize(CosmosDbContainer other, Fixture otherFixture) {
            """ + body + "}}" + extra;
        source = source.Replace("new CosmosDbBuilder()", "new " + builder + "()", StringComparison.Ordinal);
        var options = new CSharpParseOptions(preprocessorSymbols: symbols ?? []);
        var compilation = CSharpCompilation.Create("Fixtures", [CSharpSyntaxTree.ParseText(source, options), CSharpSyntaxTree.ParseText(Contracts, options)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }

    [Fact]
    public void AcceptTheRepositoryFluentPattern()
    {
        var result = Inspect("var client = " + GoodClient + ";");
        result.Fixtures.ShouldBe(1);
        result.Clients.ShouldBe(1);
        result.Violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("new CosmosClientBuilder(_container.GetConnectionString()).Build()")]
    [InlineData("new CosmosClientBuilder(other.GetConnectionString()).WithHttpClientFactory(() => _container.HttpClient).Build()")]
    [InlineData("new CosmosClientBuilder(_container.GetConnectionString()).WithHttpClientFactory(() => other.HttpClient).Build()")]
    [InlineData("new CosmosClientBuilder(\"AccountEndpoint=\" + \"http://wrong\").WithHttpClientFactory(() => _container.HttpClient).Build()")]
    [InlineData("new Microsoft.Azure.Cosmos.CosmosClient(_container.GetConnectionString())")]
    public void RejectAnIncorrectClientEvenWhenCorrectLookingCallsAlsoExist(string badClient)
    {
        var result = Inspect("var unused = _container.GetConnectionString(); var good = " + GoodClient + "; var bad = " + badClient + ";");
        result.Clients.ShouldBe(2);
        result.Violations.ShouldHaveSingleItem();
    }

    [Fact]
    public void RejectAShadowedContainerSymbol() =>
        Inspect("var _container = other; var client = " + GoodClient + ";").Violations.ShouldHaveSingleItem();

    [Fact]
    public void RejectTheSameFieldOnAnotherFixtureInstance() =>
        Inspect("var client = new CosmosClientBuilder(_container.GetConnectionString()).WithHttpClientFactory(() => otherFixture._container.HttpClient).Build();")
            .Violations.ShouldHaveSingleItem();

    [Fact]
    public void RejectNestedReturnsAsFactoryEvidence() =>
        Inspect("var client = new CosmosClientBuilder(_container.GetConnectionString()).WithHttpClientFactory(() => { object Nested() => _container.HttpClient; throw new System.InvalidOperationException(); }).Build();")
            .Violations.ShouldHaveSingleItem();

    [Fact]
    public void IgnoreUnrelatedNestedTypeConfiguration() =>
        Inspect("", "class Other { void Configure(Testcontainers.CosmosDb.CosmosDbContainer _container) { var client = " + GoodClient + "; }}")
            .Violations.ShouldHaveSingleItem();

    [Fact]
    public void IgnoreCommentsAndStringsAsConfiguration() =>
        Inspect("// " + GoodClient + "\n var text = \"WithHttpClientFactory\";").Violations.ShouldHaveSingleItem();

    [Fact]
    public void KeepConditionalConfigurationsSeparate()
    {
        var body = "\n#if HEALTHY\nvar client = " + GoodClient + ";\n#else\nvar client = new CosmosClientBuilder(_container.GetConnectionString()).Build();\n#endif\n";
        Inspect(body, symbols: ["HEALTHY"]).Violations.ShouldBeEmpty();
        Inspect(body).Violations.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("Alias")]
    [InlineData("global::Testcontainers.CosmosDb.CosmosDbBuilder")]
    public void ResolveConstructorAliasesAndQualifiedNames(string builder)
    {
        var result = CosmosFixtureRule.Analyze(Compile("var client = " + GoodClient + ";", builder: builder));
        result.Fixtures.ShouldBe(1);
        result.Violations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportRealCompilerAnalyzerDiagnostics(bool invalid)
    {
        var body = invalid ? "var client = new CosmosClientBuilder(_container.GetConnectionString()).Build();" : "var client = " + GoodClient + ";";
        var diagnostics = await Compile(body).WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CosmosFixtureAnalyzer()),
                new AnalyzerOptions([], new RepositoryPolicyOptions("RepositoryCosmosFixturePolicy")))
            .GetAnalyzerDiagnosticsAsync();
        diagnostics.Length.ShouldBe(invalid ? 1 : 0);
        if (invalid)
        {
            diagnostics[0].Id.ShouldBe("REPO001");
            diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
            diagnostics[0].Location.IsInSource.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task WireTheAnalyzerIntoEveryIntegrationProjectAndRetainANonemptyFixturePopulation()
    {
        var root = TestHelpers.GetRepositoryRoot();
        var integration = Path.Combine(root, "tests", "integration");
        var projects = Directory.GetFiles(integration, "*.csproj", SearchOption.AllDirectories);
        projects.ShouldNotBeEmpty();
        foreach (var project in projects)
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var argument in new[] { "msbuild", project, "-nologo", "-p:Configuration=" + configuration, "-getProperty:RunAnalyzers,RunAnalyzersDuringBuild,RepositoryCosmosFixturePolicy", "-getItem:ProjectReference" })
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start().ShouldBeTrue();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            process.ExitCode.ShouldBe(0, await stderr);
            using var evaluated = JsonDocument.Parse(await stdout);
            evaluated.RootElement.GetProperty("Properties").GetProperty("RepositoryCosmosFixturePolicy").GetString().ShouldBe("true");
            foreach (var property in new[] { "RunAnalyzers", "RunAnalyzersDuringBuild" })
            {
                string.Equals(evaluated.RootElement.GetProperty("Properties").GetProperty(property).GetString(), "false", StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse(project + " " + configuration + " " + property);
            }

            var analyzerProject = Path.GetFullPath(Path.Combine(root, "eng", "tools", "RepositoryAnalyzers", "RepositoryAnalyzers.csproj"));
            evaluated.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray().Count(reference =>
                string.Equals(Path.GetFullPath(reference.GetProperty("FullPath").GetString()!), analyzerProject, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                string.Equals(reference.GetProperty("OutputItemType").GetString(), "Analyzer", StringComparison.Ordinal) &&
                string.Equals(reference.GetProperty("ReferenceOutputAssembly").GetString(), "false", StringComparison.OrdinalIgnoreCase)).ShouldBe(1, project);
        }

        var population = CSharpSourceInventory.ReadSources(Directory.EnumerateFiles(integration, "*.cs", SearchOption.AllDirectories)
            .Where(CSharpSourceInventory.IsProductionSource));
        var constructors = population.SelectMany(source => CSharpSourceInventory.ParseConfigurations(source.Text))
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            .Count(creation => creation.Type.DescendantTokens().Any(token => token.ValueText == "CosmosDbBuilder"));
        constructors.ShouldBeGreaterThan(0, "The fixture policy must observe actual emulator construction sites.");
    }
}
