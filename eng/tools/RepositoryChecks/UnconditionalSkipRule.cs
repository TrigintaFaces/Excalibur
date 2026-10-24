// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Finds a resolved xUnit skip at callable entry, never a guarded block's first statement.</summary>
internal static class UnconditionalSkipRule
{
    /// <summary>Returns one location for each unconditional entry skip.</summary>
    internal static IEnumerable<int> Find(Compilation compilation, SyntaxTree tree)
    {
        var assert = compilation.GetTypeByMetadataName("Xunit.Assert");
        if (assert?.ContainingAssembly.Identity.Name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) != true)
        {
            yield break;
        }

        var model = compilation.GetSemanticModel(tree);
        foreach (var declaration in tree.GetRoot().DescendantNodes())
        {
            var expression = declaration switch
            {
                BaseMethodDeclarationSyntax method => method.ExpressionBody?.Expression ?? Entry(method.Body),
                LocalFunctionStatementSyntax local => local.ExpressionBody?.Expression ?? Entry(local.Body),
                LambdaExpressionSyntax lambda => lambda.ExpressionBody ?? Entry(lambda.Block),
                AnonymousMethodExpressionSyntax anonymous => Entry(anonymous.Block),
                AccessorDeclarationSyntax accessor => accessor.ExpressionBody?.Expression ?? Entry(accessor.Body),
                _ => null,
            };
            if (expression is not null && model.GetOperation(expression) is IInvocationOperation call &&
string.Equals(call.TargetMethod.Name, "Skip", StringComparison.Ordinal) && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, assert))
            {
                yield return expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            }
        }
    }

    private static ExpressionSyntax? Entry(BlockSyntax? body) => (body?.Statements.FirstOrDefault() as ExpressionStatementSyntax)?.Expression;
}
