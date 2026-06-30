// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Migration.Analyzers;

/// <summary>
/// Flags a <c>using MediatR;</c> directive as swappable to the Excalibur.Dispatch compat namespace
/// (EXMIG0003), enabling the companion using-swap code-fix.
/// </summary>
/// <remarks>
/// Implements FR-12 / AC-15 of EPIC w2zq7d. Only the exact top-level <c>MediatR</c> namespace import is
/// flagged (not aliased or <c>using static</c> forms), so the swap is unambiguous and idempotent on
/// partially-migrated files (EC-7).
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MediatRUsingDirectiveAnalyzer : DiagnosticAnalyzer
{
	private const string MediatRNamespace = "MediatR";

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(MigrationDiagnosticDescriptors.MediatRUsingDirectiveSwappable);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
	}

	private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
	{
		var usingDirective = (UsingDirectiveSyntax)context.Node;

		// Only a plain namespace import: not 'using static', not an alias.
		if (usingDirective.StaticKeyword != default ||
			usingDirective.Alias is not null ||
			usingDirective.Name is not IdentifierNameSyntax { Identifier.ValueText: MediatRNamespace })
		{
			return;
		}

		var diagnostic = Diagnostic.Create(
			MigrationDiagnosticDescriptors.MediatRUsingDirectiveSwappable,
			usingDirective.GetLocation(),
			MediatRNamespace);

		context.ReportDiagnostic(diagnostic);
	}
}
