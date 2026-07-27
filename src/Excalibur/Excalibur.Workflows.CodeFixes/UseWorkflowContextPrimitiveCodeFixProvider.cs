// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Excalibur.Workflows.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Excalibur.Workflows.CodeFixes;

/// <summary>
/// Code-fix for <see cref="WorkflowDiagnosticIds.NonDeterministicApiInWorkflow"/> (EXWF001): rewrites a
/// non-deterministic API read (for example <c>DateTime.UtcNow</c> or <c>Guid.NewGuid()</c>) inside a
/// workflow body to the deterministic <c>IWorkflowContext</c> primitive
/// (<c>await ctx.UtcNowAsync(cancellationToken)</c> / <c>await ctx.NewGuidAsync(cancellationToken)</c>).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseWorkflowContextPrimitiveCodeFixProvider))]
[Shared]
public sealed class UseWorkflowContextPrimitiveCodeFixProvider : CodeFixProvider
{
    private const string WorkflowContextTypeName = "IWorkflowContext";
    private const string CancellationTokenTypeName = "CancellationToken";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(WorkflowDiagnosticIds.ReplacementPropertyKey, out var method)
            || string.IsNullOrEmpty(method))
        {
            return;
        }

        // The flagged node is the member access; a Guid.NewGuid() call wraps it in an invocation which is the
        // node we must replace whole.
        var memberAccess = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
        if (memberAccess is null)
        {
            return;
        }

        ExpressionSyntax target = memberAccess.Parent is InvocationExpressionSyntax invocation
            ? invocation
            : memberAccess;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use ctx.{method}(...)",
                createChangedDocument: ct => ReplaceAsync(context.Document, root, target, method!, ct),
                equivalenceKey: nameof(UseWorkflowContextPrimitiveCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        SyntaxNode root,
        ExpressionSyntax target,
        string method,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        var contextName = FindParameterName(model, target, WorkflowContextTypeName) ?? "context";
        var tokenName = FindParameterName(model, target, CancellationTokenTypeName) ?? "cancellationToken";

        // await {ctx}.{method}({ct})
        var replacement = AwaitExpression(
                InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(contextName),
                            IdentifierName(method)))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName(tokenName))))))
            .WithTriviaFrom(target);

        var newRoot = root.ReplaceNode(target, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    // Finds the name of the nearest enclosing parameter whose type simple-name matches typeSimpleName
    // (IWorkflowContext / CancellationToken), scanning enclosing lambdas and the containing method.
    private static string? FindParameterName(SemanticModel? model, SyntaxNode node, string typeSimpleName)
    {
        foreach (var ancestor in node.Ancestors())
        {
            var parameters = ancestor switch
            {
                ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
                BaseMethodDeclarationSyntax methodDecl => methodDecl.ParameterList.Parameters,
                _ => default,
            };

            if (parameters.Count == 0)
            {
                continue;
            }

            foreach (var parameter in parameters)
            {
                if (parameter.Type is null)
                {
                    continue;
                }

                var typeName = model?.GetTypeInfo(parameter.Type).Type?.Name ?? SimpleName(parameter.Type);
                if (string.Equals(typeName, typeSimpleName, System.StringComparison.Ordinal))
                {
                    return parameter.Identifier.ValueText;
                }
            }
        }

        return null;
    }

    private static string SimpleName(TypeSyntax type)
    {
        var text = type.ToString();
        var lastDot = text.LastIndexOf('.');
        return lastDot >= 0 ? text.Substring(lastDot + 1) : text;
    }
}
