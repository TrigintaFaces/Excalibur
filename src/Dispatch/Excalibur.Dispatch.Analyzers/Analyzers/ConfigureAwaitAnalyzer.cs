// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes await expressions in library code and reports DISP105 when
/// <c>ConfigureAwait(false)</c> is missing. Framework/library code must not
/// capture the synchronization context.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigureAwaitAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.MissingConfigureAwait);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAwaitExpression, SyntaxKind.AwaitExpression);
    }

    private static void AnalyzeAwaitExpression(SyntaxNodeAnalysisContext context)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;

        // Only check Excalibur/Dispatch namespaces (library code)
        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var namespaceName = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!namespaceName.StartsWith("Excalibur", StringComparison.Ordinal) &&
            !namespaceName.StartsWith("Dispatch", StringComparison.Ordinal))
        {
            return;
        }

        // Check if the awaited expression already has .ConfigureAwait(...)
        if (awaitExpression.Expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "ConfigureAwait")
        {
            return;
        }

        // Check the return type — only flag Task/Task<T>/ValueTask/ValueTask<T>
        var typeInfo = context.SemanticModel.GetTypeInfo(awaitExpression.Expression, context.CancellationToken);
        var awaitedTypeName = typeInfo.Type?.ToDisplayString() ?? string.Empty;
        if (!awaitedTypeName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) &&
            !awaitedTypeName.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                AnalyzerDiagnosticDescriptors.MissingConfigureAwait,
                awaitExpression.GetLocation()));
    }
}
