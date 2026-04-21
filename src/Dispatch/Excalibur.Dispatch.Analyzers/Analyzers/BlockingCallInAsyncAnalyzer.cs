// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Analyzes async methods for synchronous blocking calls (<c>.Result</c>, <c>.Wait()</c>,
/// <c>.GetAwaiter().GetResult()</c>) and reports DISP106. These can cause thread pool
/// starvation in message handlers and middleware.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingCallInAsyncAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BlockingMemberNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Result",
        "Wait",
        "GetResult");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(AnalyzerDiagnosticDescriptors.BlockingCallInAsyncMethod);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var memberName = memberAccess.Name.Identifier.ValueText;

        if (!BlockingMemberNames.Contains(memberName))
        {
            return;
        }

        // Only flag inside async methods
        if (context.ContainingSymbol is not IMethodSymbol containingMethod || !containingMethod.IsAsync)
        {
            return;
        }

        // Only check Excalibur/Dispatch namespaces
        var namespaceName = containingMethod.ContainingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!namespaceName.StartsWith("Excalibur", StringComparison.Ordinal) &&
            !namespaceName.StartsWith("Dispatch", StringComparison.Ordinal))
        {
            return;
        }

        // Check if accessing Task.Result, Task.Wait(), or GetAwaiter().GetResult()
        var expressionType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
        var typeName = expressionType.Type?.ToDisplayString() ?? string.Empty;

        var isTaskBlocking = memberName == "Result" && typeName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal);
        var isWaitBlocking = memberName == "Wait" && typeName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal);
        var isGetResultBlocking = memberName == "GetResult" && typeName.IndexOf("TaskAwaiter", StringComparison.Ordinal) >= 0;

        if (isTaskBlocking || isWaitBlocking || isGetResultBlocking)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    AnalyzerDiagnosticDescriptors.BlockingCallInAsyncMethod,
                    memberAccess.Name.GetLocation(),
                    memberName));
        }
    }
}
