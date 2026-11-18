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
    internal static (Diagnostic[] Selected, Diagnostic[] Context) Analyze(DocumentationBlock block, IEnumerable<DocumentationBlock> context, IEnumerable<MetadataReference> references, Project? generatorContext = null)
    {
        var selected = Parse(block, (CSharpParseOptions?)generatorContext?.ParseOptions);
        var trees = ContextTrees(block, context, selected);
        trees.AddRange(DocumentationConsumerContext.GlobalUsings(generatorContext));
        var options = (CSharpCompilationOptions?)generatorContext?.CompilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var kind = selected.GetRoot().ChildNodes().Any(node => node is GlobalStatementSyntax)
            ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary;
        if (kind == OutputKind.DynamicallyLinkedLibrary && options.OutputKind != kind)
        {
            options = options.WithMainTypeName(null);
        }

        var compilation = CSharpCompilation.Create("DocumentationConsumer", trees, references, options.WithOutputKind(kind));
        var evaluated = GeneratorValidation.UpdateCompilation(
            generatorContext, compilation, (CSharpParseOptions)selected.Options, out var generatorDiagnostics);
        var diagnostics = evaluated.GetDiagnostics().Concat(generatorDiagnostics).Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ToArray();
        return (diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == selected).ToArray(),
            diagnostics.Where(diagnostic => diagnostic.Location.SourceTree != selected).ToArray());
    }

    private static List<SyntaxTree> ContextTrees(DocumentationBlock block, IEnumerable<DocumentationBlock> context, SyntaxTree selected)
    {
        var trees = new List<SyntaxTree> { selected };
        var names = selected.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>().Select(node => node.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        var partials = PartialIdentities(selected.GetRoot());
        var candidates = context.Where(item => !ReferenceEquals(item, block) && !string.Equals(item.Tier, "ignore", StringComparison.Ordinal) &&
            string.Equals(item.File, block.File, StringComparison.Ordinal)).Select(item => Parse(item, (CSharpParseOptions)selected.Options)).ToList();
        bool added;
        do
        {
            added = false;
            foreach (var candidate in candidates.ToArray())
            {
                var unit = (CompilationUnitSyntax)candidate.GetRoot();
                if (!PartialIdentities(unit).Overlaps(partials) && !unit.DescendantNodes().Any(node => (node is BaseTypeDeclarationSyntax type && names.Contains(type.Identifier.ValueText)) || (node is DelegateDeclarationSyntax declaration && names.Contains(declaration.Identifier.ValueText))))
                {
                    continue;
                }

                // Remove executable top-level examples. Preserve declarations in their original
                // namespace/import scopes; never hoist another fence's imports into this one.
                var declarations = unit.WithMembers(SyntaxFactory.List(unit.Members.Where(member => member is not GlobalStatementSyntax)));
                trees.Add(CSharpSyntaxTree.Create(declarations, (CSharpParseOptions)candidate.Options, candidate.FilePath));
                names.UnionWith(declarations.DescendantNodes().OfType<SimpleNameSyntax>().Select(node => node.Identifier.ValueText));
                partials.UnionWith(PartialIdentities(declarations));
                candidates.Remove(candidate);
                added = true;
            }
        }
        while (added);

        return trees;
    }

    private static HashSet<string> PartialIdentities(SyntaxNode root) => root.DescendantNodes().OfType<TypeDeclarationSyntax>()
        .Where(type => type.Modifiers.Any(SyntaxKind.PartialKeyword))
        .Select(PartialIdentity)
        .ToHashSet(StringComparer.Ordinal);

    private static string PartialIdentity(TypeDeclarationSyntax type)
    {
        var namespaces = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
            .Select(scope => string.Concat(scope.Name.DescendantTokens().Select(token => token.ValueText)));
        var owners = type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Reverse()
            .Select(owner => owner.Identifier.ValueText + "`" + (owner.TypeParameterList?.Parameters.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.Join('.', namespaces) + "/" + string.Join('/', owners);
    }

    private static SyntaxTree Parse(DocumentationBlock block, CSharpParseOptions? options = null) => CSharpSyntaxTree.ParseText(
        block.Code, options ?? new CSharpParseOptions(LanguageVersion.Latest),
        block.File + ":" + block.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
