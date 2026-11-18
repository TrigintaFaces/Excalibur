// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepositoryChecks;

/// <summary>Parses every conditional configuration without silently truncating source coverage.</summary>
internal static class CSharpConfigurations
{
    /// <summary>Enumerates syntax trees for every conditional-symbol assignment.</summary>
    /// <exception cref="InvalidOperationException">The source has parsing errors or too many conditional symbols.</exception>
    internal static IEnumerable<SyntaxTree> Parse(string source) =>
        ParseSources(new Dictionary<string, string>(StringComparer.Ordinal) { ["source.cs"] = source })
            .Select(trees => trees[0]);

    /// <summary>Parses supplied snapshots under the same conditional assignment, retaining file identity.</summary>
    /// <exception cref="InvalidOperationException">The snapshots have parsing errors or too many conditional symbols.</exception>
    internal static IEnumerable<SyntaxTree[]> ParseSources(IReadOnlyDictionary<string, string> sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse);
        var ordered = sources.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        var symbols = ordered.SelectMany(entry => CSharpSyntaxTree.ParseText(entry.Value, options)
                .GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).OfType<ConditionalDirectiveTriviaSyntax>()
                .SelectMany(directive => directive.Condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                .Select(identifier => identifier.Identifier.ValueText))
            .Distinct(StringComparer.Ordinal).ToArray();

        // Bound the union; independent per-file assignments would combine impossible configurations.
        if (symbols.Length > 10)
        {
            throw new InvalidOperationException("C# policy validation cannot exhaustively evaluate more than ten conditional symbols in supplied source snapshots.");
        }

        for (var assignment = 0; assignment < (1 << symbols.Length); assignment++)
        {
            var enabled = symbols.Where((_, index) => (assignment & (1 << index)) != 0).ToArray();
            var trees = ordered.Select(entry => CSharpSyntaxTree.ParseText(
                entry.Value,
                options.WithPreprocessorSymbols(enabled),
                path: entry.Key)).ToArray();
            foreach (var tree in trees)
            {
                // Preserve the existing intentional-#error and parser-error policy.
                var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && !string.Equals(d.Id, "CS1029", StringComparison.Ordinal)).ToArray();
                if (errors.Length != 0)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
                }
            }

            yield return trees;
        }
    }
}
