// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
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
/// Code-fix for EXMIG0003: swaps a <c>using MediatR;</c> directive to
/// <c>using Excalibur.Dispatch.Compat.MediatR;</c>.
/// </summary>
/// <remarks>
/// Implements FR-12 / AC-15 of EPIC w2zq7d. The fix is idempotent and avoids duplicate/orphaned usings
/// (EC-7 / EC-8): if the compat namespace is already imported in the same container, the redundant
/// <c>using MediatR;</c> is removed rather than producing a duplicate import.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MediatRUsingDirectiveCodeFixProvider))]
[Shared]
public sealed class MediatRUsingDirectiveCodeFixProvider : CodeFixProvider
{
	private const string CompatNamespace = "Excalibur.Dispatch.Compat.MediatR";
	private const string Title = "Swap to 'using Excalibur.Dispatch.Compat.MediatR;'";

	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		ImmutableArray.Create(MigrationDiagnosticIds.MediatRUsingDirectiveSwappable);

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
		var usingDirective = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) as UsingDirectiveSyntax
			?? root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<UsingDirectiveSyntax>();
		if (usingDirective is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: Title,
				createChangedDocument: ct => SwapUsingAsync(context.Document, root, usingDirective, ct),
				equivalenceKey: nameof(MediatRUsingDirectiveCodeFixProvider)),
			diagnostic);
	}

	private static Task<Document> SwapUsingAsync(
		Document document,
		SyntaxNode root,
		UsingDirectiveSyntax usingDirective,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Determine the sibling using directives in the same container to enforce idempotency.
		var siblings = usingDirective.Parent switch
		{
			CompilationUnitSyntax cu => cu.Usings,
			BaseNamespaceDeclarationSyntax ns => ns.Usings,
			_ => default,
		};

		var compatAlreadyImported = siblings.Any(u =>
			u != usingDirective &&
			u.StaticKeyword == default &&
			u.Alias is null &&
			u.Name?.ToString() == CompatNamespace);

		SyntaxNode newRoot;
		if (compatAlreadyImported)
		{
			// Avoid a duplicate import: drop the now-redundant 'using MediatR;' (EC-8).
			newRoot = root.RemoveNode(usingDirective, SyntaxRemoveOptions.KeepNoTrivia)!;
		}
		else
		{
			var swapped = usingDirective
				.WithName(SyntaxFactory.ParseName(CompatNamespace))
				.WithTriviaFrom(usingDirective);
			newRoot = root.ReplaceNode(usingDirective, swapped);
		}

		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
