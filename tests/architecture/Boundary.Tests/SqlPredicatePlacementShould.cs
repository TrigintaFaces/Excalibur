// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class SqlPredicatePlacementShould
{
    [Theory]
    [InlineData("", false)]
    [InlineData("// {fragment}", false)]
    [InlineData("var text = \"{fragment}\";", false)]
    [InlineData("var sql = $\"SELECT * FROM t {fragment}\";", true)]
    [InlineData("var sql = \"SELECT * FROM t\" + fragment;", true)]
    [InlineData("var sql = \"SELECT * FROM t\"; sql += fragment;", true)]
    [InlineData("Use(fragment);", true)]
    [InlineData("var sql = string.Concat(\"SELECT * FROM t\", fragment);", true)]
    [InlineData("var sql = string.Join(\" \", new[] { \"SELECT * FROM t\", fragment });", true)]
    [InlineData("var unused = new[] { fragment };", false)]
    public void RequireAResolvedPlacementReference(string use, bool expected)
    {
        var facts = Inspect("public void Test(bool scoped) { var fragment = scoped ? \" AND TenantId = @TenantId\" : string.Empty; " + use + "\n } private void Use(string value) {}");
        facts.ShouldHaveSingleItem().Placed.ShouldBe(expected);
    }

    [Fact]
    public void RejectSameNamedLocalInAnotherMethod()
    {
        var facts = Inspect("""
            public void First() { var fragment = " WHERE TenantId = @id"; }
            public string Second() { var fragment = "other"; return $"{fragment}"; }
            """);
        facts.ShouldHaveSingleItem().Placed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("var sql = $\"SELECT * FROM t {fragment}\";", true)]
    public void RetainConcatenatedFragmentDeclarations(string use, bool expected) => Inspect(
        "public void Test(string suffix) { var fragment = \" AND TenantId = @id\" + suffix; " + use + " }")
        .ShouldHaveSingleItem().Placed.ShouldBe(expected);

    [Fact]
    public void IgnoreFullCommandWithTrailingPredicate() => Inspect("""
        public void Test() { var sql = "SELECT * FROM t" + " WHERE TenantId = @id"; }
        """).ShouldBeEmpty();

    [Fact]
    public void JudgeEachFragmentSeparately()
    {
        var facts = Inspect("""
            public string Test() { var first = " WHERE TenantId = @id"; var second = ", TenantId"; return $"SELECT * {first}"; }
            """);
        facts.Length.ShouldBe(2);
        facts.Single(fact => fact.Name == "first").Placed.ShouldBeTrue();
        facts.Single(fact => fact.Name == "second").Placed.ShouldBeFalse();
    }

    [Fact]
    public void RecognizeQualifiedFieldReferences() => Inspect("""
        private string fragment = " WHERE TenantId = @id";
        public string Test() => $"SELECT * FROM t {this.fragment}";
        """).ShouldHaveSingleItem().Placed.ShouldBeTrue();

    [Fact]
    public void IgnoreAssembledCommandsAndConsumedSql() => Inspect("""
        public void Test() {
            var sql = $"SELECT * FROM t WHERE TenantId = @id";
            var result = Query(" WHERE TenantId = @id");
        }
        private string Query(string sql) => sql;
        """).ShouldBeEmpty();

    [Fact]
    public void IgnoreCommentDeclarations() => Inspect("// var fragment = \" WHERE TenantId = @id\";\n").ShouldBeEmpty();

    [Fact]
    public void RecognizeVerbatimStringFragments() => Inspect("""
        public void Test() { string fragment = @" WHERE TenantId = @id"; }
        """).ShouldHaveSingleItem().Placed.ShouldBeFalse();

    private static SqlPredicateFact[] Inspect(string members)
    {
        var compilation = CSharpCompilation.Create("Fixture", [CSharpSyntaxTree.ParseText("public class Fixture { " + members + " }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return SqlPredicatePlacement.Find(compilation).ToArray();
    }
}
