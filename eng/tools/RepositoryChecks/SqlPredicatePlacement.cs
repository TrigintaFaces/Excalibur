// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Detects declared SQL fragments with no resolved interpolation, concatenation, or argument use.</summary>
/// <remarks>This is placement evidence; tenant predicate behavior is verified by the request conformance tests.</remarks>
internal static partial class SqlPredicatePlacement
{
    [GeneratedRegex(@"^\s*(?:(?:AND|OR|WHERE)\s+[A-Za-z_][A-Za-z0-9_.]*\s*=\s*[@:]|,\s*[A-Za-z_][A-Za-z0-9_]*\s*$)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Fragment { get; }

    /// <summary>Returns every classified fragment, retaining the measured population as evidence.</summary>
    internal static IEnumerable<SqlPredicateFact> Find(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var variable in tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Initializer is null || !IsFragment(model.GetOperation(variable.Initializer.Value)) || model.GetDeclaredSymbol(variable) is not { } symbol)
                {
                    continue;
                }

                var scopes = symbol is IFieldSymbol field ? field.ContainingType.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()) :
                    symbol.ContainingSymbol.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax());
                var placed = scopes.Any(scope => HasPlacement(compilation, scope, symbol));
                yield return new SqlPredicateFact(tree.FilePath, variable.Identifier.ValueText, variable.GetLocation().GetLineSpan().StartLinePosition.Line + 1, placed);
            }
        }
    }

    private static bool IsFragment(IOperation? operation) => operation switch
    {
        ILiteralOperation literal when literal.ConstantValue is { HasValue: true, Value: string value } => Fragment.IsMatch(value),
        IConditionalOperation conditional => IsFragment(conditional.WhenTrue) || IsFragment(conditional.WhenFalse),
        IConversionOperation conversion => IsFragment(conversion.Operand),
        IParenthesizedOperation parenthesized => IsFragment(parenthesized.Operand),
        ICoalesceOperation coalesce => IsFragment(coalesce.Value) || IsFragment(coalesce.WhenNull),
        IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } binary => IsFragment(binary.LeftOperand),
        _ => false,
    };

    private static bool HasPlacement(Compilation compilation, SyntaxNode scope, ISymbol symbol)
    {
        var model = ConformanceArmWiring.ModelFor(compilation, scope.SyntaxTree);
        foreach (var expression in scope.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var reference = model.GetOperation(expression);
            var referencedSymbol = reference switch
            {
                ILocalReferenceOperation local => (ISymbol)local.Local,
                IFieldReferenceOperation field => field.Field,
                _ => null,
            };
            if (!SymbolEqualityComparer.Default.Equals(referencedSymbol, symbol))
            {
                continue;
            }

            for (var operation = reference?.Parent; operation is not null; operation = operation.Parent)
            {
                if (operation is IInterpolationOperation or IArgumentOperation or
                    IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } or
                    ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String })
                {
                    return true;
                }

                if (operation is not IConversionOperation and not IParenthesizedOperation and not IArrayInitializerOperation and not IArrayCreationOperation)
                {
                    break;
                }
            }
        }

        return false;
    }
}
