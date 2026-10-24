// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>Enforces the production namespace budget using the C# parser.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class NamespaceDepthShould(ITestOutputHelper output)
{
    internal sealed record NamespaceUse(string Name, int Depth, int Line);

    internal static bool IsProhibited(NamespaceUse use) => use.Depth >= 6;

    internal static IReadOnlyList<NamespaceUse> Analyze(string source)
    {
        var uses = new HashSet<NamespaceUse>();
        foreach (var tree in CSharpSourceInventory.ParseConfigurations(source))
        {
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                var parts = declaration.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                    .SelectMany(parent => parent.Name.DescendantTokens())
                    .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
                    .Select(token => token.ValueText).ToArray();
                uses.Add(new NamespaceUse(string.Join(".", parts), parts.Length,
                    declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
            }
        }

        return uses.OrderBy(use => use.Line).ThenBy(use => use.Name, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void KeepProductionNamespacesWithinTheDepthBudget()
    {
        var repository = TestHelpers.GetRepositoryRoot();
        var files = CSharpSourceInventory.ReadProductionSources();
        var all = files.SelectMany(file => Analyze(file.Text)
            .Select(use => (Path: Path.GetRelativePath(repository, file.Path), Use: use))).ToArray();
        all.ShouldNotBeEmpty("an empty namespace inventory is not a successful validation");
        var justified = all.Where(item => item.Use.Depth == 5).ToArray();
        output.WriteLine($"Examined {files.Count} source files and {all.Length} namespace declarations across conditional configurations.");
        output.WriteLine($"Preferred: at most four segments; {justified.Length} declarations at depth five require architectural justification.");
        foreach (var item in justified)
        {
            output.WriteLine($"Depth five: {item.Path}:{item.Use.Line} {item.Use.Name}");
        }

        all.Where(item => IsProhibited(item.Use)).Select(item => $"{item.Path}:{item.Use.Line}: {item.Use.Name} (depth {item.Use.Depth})")
            .ShouldBeEmpty("production namespaces with six or more segments are prohibited");
    }

    [Theory]
    [InlineData("namespace A.B.C.D;", 4)]
    [InlineData("namespace A.B.C.D.E;", 5)]
    [InlineData("namespace A.B.C.D.E.F;", 6)]
    [InlineData("namespace A.B.C { namespace D.E.F { } }", 6)]
    [InlineData("  namespace A.\n B.C.\n D.E.F { }", 6)]
    [InlineData("namespace @A.@B.@C.@D.@E.@F;", 6)]
    [InlineData("namespace A /* .Ignored. */ . B.C.D.E.F;", 6)]
    [InlineData("// namespace A.B.C.D.E.F;\nnamespace A;", 1)]
    [InlineData("class C { string Text = \"\"\"\nnamespace A.B.C.D.E.F;\n\"\"\"; }", 0)]
    [InlineData("#if FEATURE\nnamespace A.B.C.D.E.F;\n#else\nnamespace A;\n#endif", 6)]
    [InlineData("#if ONE\n#if TWO\nnamespace A.B.C.D.E.F;\n#endif\n#endif", 6)]
    [InlineData("#if ONE\nnamespace A;\n#elif TWO\nnamespace A.B.C.D.E.F;\n#endif", 6)]
    [InlineData("#if FEATURE\nnamespace A.B.C {\n#endif\nnamespace D.E.F {}\n#if FEATURE\n}\n#endif", 6)]
    [InlineData("#define FEATURE\n#if FEATURE\nnamespace A;\n#else\nnamespace A.B.C.D.E.F;\n#endif", 1)]
    [InlineData("#undef FEATURE\n#if FEATURE\nnamespace A;\n#else\nnamespace A.B.C.D.E.F;\n#endif", 6)]
    public void CountActualNamespaceSegmentsInEveryConditionalConfiguration(string source, int maximumDepth)
    {
        var uses = Analyze(source);
        uses.Select(use => use.Depth).DefaultIfEmpty().Max().ShouldBe(maximumDepth);
        uses.Any(IsProhibited).ShouldBe(maximumDepth >= 6);
    }

    [Fact]
    public void RefuseMalformedSource() => Should.Throw<InvalidOperationException>(() => Analyze("namespace A..B;"));

    [Fact]
    public void RefuseAnUnboundedConditionalPopulation()
    {
        var source = "#if " + string.Join(" || ", Enumerable.Range(0, 11).Select(index => $"FEATURE{index}")) + "\nnamespace A;\n#endif";
        Should.Throw<InvalidOperationException>(() => Analyze(source));
    }
}
