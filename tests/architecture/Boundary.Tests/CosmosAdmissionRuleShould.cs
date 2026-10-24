// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CosmosAdmissionRuleShould
{
    [Theory]
    [InlineData("public class Tests : Xunit.IClassFixture<Fixture> { [Xunit.Fact] public void Runs() {} }", "Tests")]
    [InlineData("namespace Odd { public class Renamed { public Renamed(Fixture fixture) {} [Xunit.Fact] public void Runs() {} } }", "Odd.Renamed")]
    [InlineData("public abstract class Base : Xunit.IClassFixture<Fixture> { [Xunit.Fact] public void Runs() {} } public class Derived : Base {}", "Derived")]
    [InlineData("[Xunit.CollectionDefinition(\"Group\")] public class Group : Xunit.IClassFixture<Fixture> {} [Xunit.Collection(\"Group\")] public class Tests { [Xunit.Fact] public void Runs() {} }", "Tests")]
    public void DiscoverByResolvedDependenciesInsteadOfNames(string tests, string expected)
    {
        var result = CosmosAdmissionRule.Analyze([Compile(tests)]);
        result.Fixtures.ShouldBe(1);
        result.Tests.ShouldContain(expected);
    }

    [Fact]
    public void IgnoreCommentsAndUnrelatedSameNamedFixtures()
    {
        var result = CosmosAdmissionRule.Analyze([Compile("""
            namespace Unrelated { public class Fixture {} public class Tests : Xunit.IClassFixture<Fixture> {
                // This mentions Testcontainers.CosmosDb.CosmosDbBuilder but constructs nothing.
                [Xunit.Fact] public void Runs() {}
            } }
            """)]);
        result.Fixtures.ShouldBe(1);
        result.Tests.ShouldBeEmpty();
    }

    [Fact]
    public void PreserveQualifiedClassNames()
    {
        var result = CosmosAdmissionRule.Analyze([Compile("""
            namespace First { public class Tests : Xunit.IClassFixture<global::Fixture> { [Xunit.Fact] public void Runs() {} } }
            namespace Second { public class Tests { [Xunit.Fact] public void Runs() {} } }
            """)]);
        result.Tests.ShouldBe(["First.Tests"]);
    }

    [Fact]
    public void ScopeCollectionNamesToTheirTestAssembly()
    {
        var first = Compile("""
            [Xunit.CollectionDefinition("Database")] public class Group : Xunit.IClassFixture<Fixture> {}
            [Xunit.Collection("Database")] public class UsesCosmos { [Xunit.Fact] public void Runs() {} }
            """, "First");
        var second = Compile("""
            [Xunit.CollectionDefinition("Database")] public class Group {}
            [Xunit.Collection("Database")] public class Unrelated { [Xunit.Fact] public void Runs() {} }
            """, "Second");
        CosmosAdmissionRule.Analyze([first, second]).Tests.ShouldBe(["UsesCosmos"]);
    }

    [Fact]
    public void ExcludeHiddenBaseTestMethods()
    {
        CosmosAdmissionRule.Analyze([Compile("""
            public abstract class Base : Xunit.IClassFixture<Fixture> { [Xunit.Fact] public void Runs() {} }
            public class Derived : Base { public new void Runs() {} }
            """)]).Tests.ShouldBeEmpty();
    }

    [Fact]
    public void RejectSourceContainerImpostors()
    {
        CosmosAdmissionRule.Analyze([Compile("""
            namespace Testcontainers.CosmosDb { public class CosmosDbBuilder {} }
            public class Tests : Xunit.IClassFixture<Fixture> { [Xunit.Fact] public void Runs() {} }
            """)]).Fixtures.ShouldBe(0);
    }

    private static CSharpCompilation Compile(string tests, string assemblyName = "FixtureAssembly")
    {
        var core = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var container = CSharpCompilation.Create("Testcontainers.CosmosDb", [CSharpSyntaxTree.ParseText(
            "namespace Testcontainers.CosmosDb { public class CosmosDbBuilder {} }")], [core], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText("""
            namespace Xunit {
                public class FactAttribute : System.Attribute {}
                public class CollectionAttribute : System.Attribute { public CollectionAttribute(string name) {} }
                public class CollectionDefinitionAttribute : System.Attribute { public CollectionDefinitionAttribute(string name) {} }
                public interface IClassFixture<T> {}
            }
            public class Fixture { public object Create() => new Testcontainers.CosmosDb.CosmosDbBuilder(); }
            """ + tests)], [core, container.ToMetadataReference()], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }
}
