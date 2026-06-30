// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Excalibur.Dispatch.Migration.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Excalibur.Dispatch.Migration.CodeFixes;

/// <summary>
/// Code-fix for EXMIG0004: renames a handler's <c>HandleAsync</c> method to <c>Handle</c> to match the
/// Excalibur.Dispatch compat handler shape.
/// </summary>
/// <remarks>
/// Implements FR-13 / AC-16 of EPIC w2zq7d. Only the deterministic method-name delta
/// (<c>HandleAsync</c> → <c>Handle</c>) is auto-fixed; other signature deltas are surfaced by the
/// diagnostic message for manual migration (no silent skip).
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HandlerSignatureCodeFixProvider))]
[Shared]
public sealed class HandlerSignatureCodeFixProvider : CodeFixProvider
{
	private const string ExpectedHandlerMethodName = "Handle";
	private const string Title = "Rename handler method to 'Handle'";

	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		ImmutableArray.Create(MigrationDiagnosticIds.HandlerSignatureDelta);

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
		var method = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
		if (method is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: Title,
				createChangedDocument: ct => RenameMethodAsync(context.Document, root, method, ct),
				equivalenceKey: nameof(HandlerSignatureCodeFixProvider)),
			diagnostic);
	}

	private static Task<Document> RenameMethodAsync(
		Document document,
		SyntaxNode root,
		MethodDeclarationSyntax method,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var renamed = method.WithIdentifier(
			SyntaxFactory.Identifier(ExpectedHandlerMethodName).WithTriviaFrom(method.Identifier));
		var newRoot = root.ReplaceNode(method, renamed);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
