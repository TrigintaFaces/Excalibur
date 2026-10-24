// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Binds documentation fragments as consumer code against real assembly references.</summary>
internal static class DocumentationApiRule
{
    /// <summary>Preserves all diagnostics; callers distinguish selected failures from contextual evidence.</summary>
    internal static (Diagnostic[] Selected, Diagnostic[] Context) Analyze(DocumentationBlock block, IEnumerable<DocumentationBlock> context, IEnumerable<MetadataReference> references)
    {
        var selected = Parse(block);
        var trees = ContextTrees(block, context, selected);
        var compilation = CSharpCompilation.Create("DocumentationConsumer", trees, references,
            new CSharpCompilationOptions(selected.GetRoot().ChildNodes().Any(node => node is GlobalStatementSyntax)
                ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ToArray();
        return (diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == selected).ToArray(),
            diagnostics.Where(diagnostic => diagnostic.Location.SourceTree != selected).ToArray());
    }

    private static List<SyntaxTree> ContextTrees(DocumentationBlock block, IEnumerable<DocumentationBlock> context, SyntaxTree selected)
    {
        var trees = new List<SyntaxTree> { selected };
        var names = selected.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>().Select(node => node.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        var candidates = context.Where(item => !ReferenceEquals(item, block) && !string.Equals(item.Tier, "ignore", StringComparison.Ordinal) &&
            string.Equals(item.File, block.File, StringComparison.Ordinal)).Select(Parse).ToList();
        bool added;
        do
        {
            added = false;
            foreach (var candidate in candidates.ToArray())
            {
                var unit = (CompilationUnitSyntax)candidate.GetRoot();
                if (!unit.DescendantNodes().Any(node => (node is BaseTypeDeclarationSyntax type && names.Contains(type.Identifier.ValueText)) || (node is DelegateDeclarationSyntax declaration && names.Contains(declaration.Identifier.ValueText))))
                {
                    continue;
                }

                // Remove executable top-level examples. Preserve declarations in their original
                // namespace/import scopes; never hoist another fence's imports into this one.
                var declarations = unit.WithMembers(SyntaxFactory.List(unit.Members.Where(member => member is not GlobalStatementSyntax)));
                trees.Add(CSharpSyntaxTree.Create(declarations, (CSharpParseOptions)candidate.Options, candidate.FilePath));
                names.UnionWith(declarations.DescendantNodes().OfType<SimpleNameSyntax>().Select(node => node.Identifier.ValueText));
                candidates.Remove(candidate);
                added = true;
            }
        }
        while (added);

        return trees;
    }

    private static SyntaxTree Parse(DocumentationBlock block) => CSharpSyntaxTree.ParseText(
        block.Code, new CSharpParseOptions(LanguageVersion.Latest),
        block.File + ":" + block.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
