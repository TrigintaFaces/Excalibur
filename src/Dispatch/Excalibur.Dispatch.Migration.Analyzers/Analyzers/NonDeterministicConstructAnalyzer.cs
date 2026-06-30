// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.Migration.Analyzers;

/// <summary>
/// Flags MediatR constructs that fall outside the Excalibur.Dispatch compat surface and therefore have
/// no deterministic mechanical rewrite, emitting an informational diagnostic (EXMIG0002) describing the
/// required manual migration step rather than silently skipping it.
/// </summary>
/// <remarks>
/// <para>
/// The MediatR compatibility shim deliberately does not
/// reproduce MediatR's pre/post processors, exception handlers/actions, or stream pipeline behaviors;
/// these surface here so the consumer knows a manual step remains (no silent gap).
/// </para>
/// <para>
/// Detection is syntax-based on the base-type simple name so it fires whether or not the MediatR
/// assembly is still referenced — the realistic state of code mid-migration.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonDeterministicConstructAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// MediatR construct interface simple names that are intentionally NOT shimmed and have
	/// no deterministic mechanical rewrite.
	/// </summary>
	private static readonly ImmutableHashSet<string> NonPortableConstructNames = ImmutableHashSet.Create(
		"IRequestPreProcessor",
		"IRequestPostProcessor",
		"IRequestExceptionHandler",
		"IRequestExceptionAction",
		"IStreamPipelineBehavior");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(MigrationDiagnosticDescriptors.NonDeterministicConstruct);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(
			AnalyzeTypeDeclaration,
			SyntaxKind.ClassDeclaration,
			SyntaxKind.StructDeclaration,
			SyntaxKind.RecordDeclaration,
			SyntaxKind.RecordStructDeclaration);
	}

	private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
	{
		var typeDeclaration = (TypeDeclarationSyntax)context.Node;

		if (typeDeclaration.BaseList is null)
		{
			return;
		}

		foreach (var baseType in typeDeclaration.BaseList.Types)
		{
			if (!TryGetConstructName(baseType.Type, out var constructName) ||
				!NonPortableConstructNames.Contains(constructName))
			{
				continue;
			}

			var diagnostic = Diagnostic.Create(
				MigrationDiagnosticDescriptors.NonDeterministicConstruct,
				baseType.GetLocation(),
				typeDeclaration.Identifier.ValueText,
				constructName);

			context.ReportDiagnostic(diagnostic);
		}
	}

	/// <summary>
	/// Resolves the simple name of a base-type reference, unwrapping generic and qualified forms
	/// (<c>MediatR.IRequestPreProcessor&lt;T&gt;</c> → <c>IRequestPreProcessor</c>).
	/// </summary>
	private static bool TryGetConstructName(TypeSyntax type, out string name)
	{
		switch (type)
		{
			case GenericNameSyntax generic:
				name = generic.Identifier.ValueText;
				return true;
			case QualifiedNameSyntax qualified:
				return TryGetConstructName(qualified.Right, out name);
			case AliasQualifiedNameSyntax aliasQualified:
				return TryGetConstructName(aliasQualified.Name, out name);
			case SimpleNameSyntax simple:
				name = simple.Identifier.ValueText;
				return true;
			default:
				name = string.Empty;
				return false;
		}
	}
}
