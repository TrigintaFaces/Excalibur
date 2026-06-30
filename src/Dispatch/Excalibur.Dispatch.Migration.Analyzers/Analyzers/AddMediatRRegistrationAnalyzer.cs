// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Migration.Analyzers;

/// <summary>
/// Flags MediatR DI registration calls (e.g. <c>services.AddMediatR(...)</c>) as mechanically
/// portable to the Excalibur.Dispatch compat registration entry point (EXMIG0001).
/// </summary>
/// <remarks>
/// <para>
/// Implements FR-10 / AC-9 of EPIC w2zq7d (migration tooling). Detection is syntax-based on the
/// invoked method's simple name (<c>AddMediatR</c>) so the diagnostic fires whether or not the
/// MediatR assembly is still referenced — the realistic state of code mid-migration off the
/// now-commercial package.
/// </para>
/// <para>
/// The companion code-fix (bead <c>wfh6e3</c>, in <c>Excalibur.Dispatch.Migration.CodeFixes</c>)
/// rewrites the flagged call to <c>AddMediatRCompat(...)</c>, preserving assembly-scan arguments.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AddMediatRRegistrationAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The MediatR registration extension method name this analyzer flags.
	/// </summary>
	private const string AddMediatRMethodName = "AddMediatR";

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(MigrationDiagnosticDescriptors.MediatRRegistrationPortable);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
	}

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (!TryGetInvokedSimpleName(invocation.Expression, out var nameSyntax) ||
			nameSyntax.Identifier.ValueText != AddMediatRMethodName)
		{
			return;
		}

		// Report at the method-name identifier so the squiggle/code-fix anchors on 'AddMediatR'.
		var diagnostic = Diagnostic.Create(
			MigrationDiagnosticDescriptors.MediatRRegistrationPortable,
			nameSyntax.GetLocation(),
			AddMediatRMethodName);

		context.ReportDiagnostic(diagnostic);
	}

	/// <summary>
	/// Extracts the invoked method's simple-name syntax for both <c>receiver.AddMediatR(...)</c>
	/// (member access) and bare <c>AddMediatR(...)</c> (via <c>using static</c>) call shapes,
	/// including generic invocations (<c>AddMediatR&lt;T&gt;(...)</c>).
	/// </summary>
	private static bool TryGetInvokedSimpleName(ExpressionSyntax expression, out SimpleNameSyntax nameSyntax)
	{
		switch (expression)
		{
			case MemberAccessExpressionSyntax memberAccess:
				nameSyntax = memberAccess.Name;
				return true;
			case SimpleNameSyntax simpleName:
				nameSyntax = simpleName;
				return true;
			case MemberBindingExpressionSyntax memberBinding:
				nameSyntax = memberBinding.Name;
				return true;
			default:
				nameSyntax = null!;
				return false;
		}
	}
}
