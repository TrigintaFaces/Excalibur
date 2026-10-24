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
    internal static IEnumerable<SyntaxTree> Parse(string source)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse);
        var initial = CSharpSyntaxTree.ParseText(source, options).GetRoot();
        var symbols = initial.DescendantTrivia(descendIntoTrivia: true)
            .Select(trivia => trivia.GetStructure()).OfType<ConditionalDirectiveTriviaSyntax>()
            .SelectMany(directive => directive.Condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(identifier => identifier.Identifier.ValueText).Distinct(StringComparer.Ordinal).ToArray();

        // Never silently truncate conditional coverage. The bound protects CI from exponential work.
        if (symbols.Length > 10)
        {
            throw new InvalidOperationException("C# policy validation cannot exhaustively evaluate more than ten conditional symbols in one file.");
        }

        for (var assignment = 0; assignment < (1 << symbols.Length); assignment++)
        {
            var enabled = symbols.Where((_, index) => (assignment & (1 << index)) != 0);
            var tree = CSharpSyntaxTree.ParseText(source, options.WithPreprocessorSymbols(enabled));

            // An intentional #error excludes a target configuration but does not make its syntax
            // unreadable. Actual parser errors must refuse, never shrink the namespace population.
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && !string.Equals(d.Id, "CS1029", StringComparison.Ordinal)).ToArray();
            if (errors.Length != 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
            }

            yield return tree;
        }
    }
}
