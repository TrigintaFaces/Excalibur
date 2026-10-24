// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
