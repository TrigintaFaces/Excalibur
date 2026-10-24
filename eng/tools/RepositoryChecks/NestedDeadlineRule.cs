// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Finds a cancellation deadline shorter than a resolved wait in the same callable body.</summary>
internal static class NestedDeadlineRule
{
    private static readonly string[] Subjects = ["Cancel", "Timeout", "Expire", "Deadline", "Elapsed", "Abort"];
    private static readonly HashSet<string> WaitMethods = new(["AwaitSignalAsync", "WaitUntilAsync", "WhenAny", "WaitAsync"], StringComparer.Ordinal);

    /// <summary>Preserves explicit justifications and the existing deadline-subject exemption.</summary>
    internal static IEnumerable<string> Find(Compilation compilation, SyntaxTree tree, string path)
    {
        var core = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
        var cancellation = core.GetTypeByMetadataName("System.Threading.CancellationTokenSource");
        var model = compilation.GetSemanticModel(tree);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (model.GetOperation(node) is not IObjectCreationOperation creation ||
                !SymbolEqualityComparer.Default.Equals(creation.Type, cancellation) || TestTimingRule.Justified(node, "deadline-ok:"))
            {
                continue;
            }

            var deadline = creation.Arguments.Select(argument => TestTimingRule.Milliseconds(argument.Value, core)).FirstOrDefault(value => value >= 0);
            var scope = Callable(node);
            var namedScope = node.Ancestors().FirstOrDefault(parent => parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
            var method = namedScope switch
            {
                BaseMethodDeclarationSyntax declaration => model.GetDeclaredSymbol(declaration),
                LocalFunctionStatementSyntax local => model.GetDeclaredSymbol(local),
                _ => null,
            };
            if (deadline is null || scope is null || Subjects.Any(subject => method?.Name.Contains(subject, StringComparison.OrdinalIgnoreCase) == true))
            {
                continue;
            }

            var waits = scope.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call => Callable(call) == scope)
                .Select(call => model.GetOperation(call)).OfType<IInvocationOperation>().Where(IsWait);
            var longest = waits.SelectMany(wait => wait.Arguments.SelectMany(argument => Durations(argument.Value, core, model, new HashSet<ISymbol>(SymbolEqualityComparer.Default)))).DefaultIfEmpty(0).Max();
            if (longest > deadline)
            {
                yield return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {deadline}ms cancellation precedes {longest}ms wait");
            }
        }
    }

    private static SyntaxNode? Callable(SyntaxNode node) => node.Ancestors().FirstOrDefault(parent =>
        parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

    private static bool IsWait(IInvocationOperation operation) => WaitMethods.Contains(operation.TargetMethod.Name);

    private static IEnumerable<double> Durations(IOperation operation, IAssemblySymbol core, SemanticModel model, HashSet<ISymbol> visited)
    {
        if (operation is ILocalReferenceOperation local && visited.Add(local.Local))
        {
            foreach (var declaration in local.Local.DeclaringSyntaxReferences)
            {
                if (declaration.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initializer } && model.GetOperation(initializer) is { } initial)
                {
                    foreach (var value in Durations(initial, core, model, visited))
                    {
                        yield return value;
                    }
                }
            }

            // Retain every possible assigned duration rather than trusting an obsolete initializer.
            var scope = Callable(operation.Syntax);
            foreach (var assignment in scope?.DescendantNodes().OfType<AssignmentExpressionSyntax>() ?? [])
            {
                if (Callable(assignment) == scope && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, local.Local) &&
                    model.GetOperation(assignment.Right) is { } assigned)
                {
                    foreach (var value in Durations(assigned, core, model, visited))
                    {
                        yield return value;
                    }
                }
            }
        }

        if (SymbolEqualityComparer.Default.Equals(operation.Type, core.GetTypeByMetadataName("System.TimeSpan")) &&
            TestTimingRule.Milliseconds(operation, core) is { } duration)
        {
            yield return duration;
            yield break;
        }

        if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
        {
            yield break;
        }

        foreach (var child in operation.ChildOperations)
        {
            foreach (var value in Durations(child, core, model, visited))
            {
                yield return value;
            }
        }
    }
}
