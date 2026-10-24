// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class GeneratedInputRuleShould
{
    [Theory]
    [InlineData("[Xunit.Fact] public void Law() { /* new Random(seed) */ }", false)]
    [InlineData("[Xunit.Fact] public void Law() { var seed = 42; }", false)]
    [InlineData("[Xunit.Fact] public void Law() { var random = new Random(42); }", true)]
    [InlineData("[Xunit.Fact] public void Law() { Generate(); } private void Generate() { var random = new Random(42); }", true)]
    [InlineData("[Xunit.Fact] public void Law() {} private void Uncalled() { var random = new Random(42); }", false)]
    [InlineData("[Xunit.Fact] public void Law() { Action uncalled = () => { var random = new Random(42); }; }", false)]
    [InlineData("[Xunit.Fact] public void Law() { void Uncalled() { var random = new Random(42); } }", false)]
    [InlineData("[Xunit.Fact] public void Law() { Generate(); void Generate() { var random = new Random(42); } }", true)]
    [InlineData("[Xunit.Fact, Xunit.MemberData(nameof(Data))] public void Law() {} public static object Data() => new Random(42);", true)]
    public void RequireReachableGeneratorEvidence(string members, bool expected) => Inspect("public class Fixture { " + members + " }").Any().ShouldBe(expected);

    [Fact]
    public void ExcludeUndiscoverableAbstractSuite() => Inspect("""
        public abstract class Fixture { [Xunit.Fact] public void Law() { var random = new Random(42); } }
        """).ShouldBeEmpty();

    [Fact]
    public void IncludeInheritedTestsOnConcreteSuite() => Inspect("""
        public abstract class Base { [Xunit.Fact] public void Law() { var random = new Random(42); } }
        public class Fixture : Base { }
        """).ShouldHaveSingleItem();

    [Theory]
    [InlineData("FsCheck.Xunit", true)]
    [InlineData("Application", false)]
    public void RecognizeTrustedRunnerGeneratedInputs(string assembly, bool expected) => Inspect("""
        public class Fixture { [FsCheck.Xunit.Property] public bool Law(int value) => value == value; }
        namespace FsCheck.Xunit { public class PropertyAttribute : Attribute { } }
        """, assembly).Any().ShouldBe(expected);

    private static string[] Inspect(string source, string assembly = "Fixture")
    {
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText("""
            using System;
            namespace Xunit {
                public class FactAttribute : Attribute { }
                public class MemberDataAttribute : Attribute { public MemberDataAttribute(string member) { } }
            }
            """ + source, path: "tests/property/Fixture.cs")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return GeneratedInputRule.Find(compilation).ToArray();
    }
}
