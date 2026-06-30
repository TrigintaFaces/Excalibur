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
/// Code-fix for EXMIG0001: rewrites a MediatR <c>services.AddMediatR(...)</c> registration call to the
/// Excalibur.Dispatch compat entry point <c>services.AddMediatRCompat(...)</c>, preserving all
/// arguments (including the assembly-scan configuration lambda).
/// </summary>
/// <remarks>
/// Implements FR-11 / AC-9 of EPIC w2zq7d. The rewrite is a pure method-name substitution on the
/// invoked simple name, so the call's arguments — including
/// <c>cfg =&gt; cfg.RegisterServicesFromAssembly(asm)</c> — are carried over verbatim.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddMediatRRegistrationCodeFixProvider))]
[Shared]
public sealed class AddMediatRRegistrationCodeFixProvider : CodeFixProvider
{
	private const string CompatMethodName = "AddMediatRCompat";
	private const string Title = "Migrate to AddMediatRCompat (Excalibur.Dispatch)";

	/// <inheritdoc />
	public override ImmutableArray<string> FixableDiagnosticIds { get; } =
		ImmutableArray.Create(MigrationDiagnosticIds.MediatRRegistrationPortable);

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
		var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

		// The diagnostic anchors on the 'AddMediatR' simple name; resolve it robustly.
		var nameSyntax = node as SimpleNameSyntax ?? node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().FirstOrDefault();
		if (nameSyntax is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				title: Title,
				createChangedDocument: ct => ReplaceMethodNameAsync(context.Document, root, nameSyntax, ct),
				equivalenceKey: nameof(AddMediatRRegistrationCodeFixProvider)),
			diagnostic);
	}

	private static Task<Document> ReplaceMethodNameAsync(
		Document document,
		SyntaxNode root,
		SimpleNameSyntax nameSyntax,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		SimpleNameSyntax replacement = nameSyntax switch
		{
			GenericNameSyntax generic => generic.WithIdentifier(SyntaxFactory.Identifier(CompatMethodName)),
			_ => SyntaxFactory.IdentifierName(CompatMethodName),
		};

		replacement = replacement.WithTriviaFrom(nameSyntax);
		var newRoot = root.ReplaceNode(nameSyntax, replacement);
		return Task.FromResult(document.WithSyntaxRoot(newRoot));
	}
}
