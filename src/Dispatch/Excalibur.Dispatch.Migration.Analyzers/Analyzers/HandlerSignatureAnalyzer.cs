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
/// Flags a handler whose handler-method name differs from the Excalibur.Dispatch compat shape's
/// <c>Handle</c> (EXMIG0004) — the common <c>HandleAsync</c> delta is deterministically code-fixable.
/// </summary>
/// <remarks>
/// <para>
/// Implements FR-13 / AC-16 of EPIC w2zq7d. The compat handler contract is
/// <c>Task&lt;TResponse&gt; Handle(TRequest request, CancellationToken cancellationToken)</c>. A handler
/// implementing a compat handler interface but declaring <c>HandleAsync</c> has a deterministic rename
/// fix; the diagnostic message names the expected method so non-fixable deltas surface the manual step
/// (no silent skip).
/// </para>
/// <para>
/// Detection is syntax-based on the implemented interface's simple name so it fires whether or not the
/// MediatR/compat assembly is referenced mid-migration.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerSignatureAnalyzer : DiagnosticAnalyzer
{
	private const string ExpectedHandlerMethodName = "Handle";
	private const string LegacyHandlerMethodName = "HandleAsync";

	/// <summary>
	/// Compat/MediatR handler interface simple names whose contract method is <c>Handle</c>.
	/// </summary>
	private static readonly ImmutableHashSet<string> HandlerInterfaceNames = ImmutableHashSet.Create(
		"IRequestHandler",
		"INotificationHandler",
		"IStreamRequestHandler");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(MigrationDiagnosticDescriptors.HandlerSignatureDelta);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(
			AnalyzeTypeDeclaration,
			SyntaxKind.ClassDeclaration,
			SyntaxKind.RecordDeclaration);
	}

	private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
	{
		var typeDeclaration = (TypeDeclarationSyntax)context.Node;

		if (typeDeclaration.BaseList is null || !ImplementsHandlerInterface(typeDeclaration))
		{
			return;
		}

		foreach (var member in typeDeclaration.Members)
		{
			if (member is MethodDeclarationSyntax { Identifier.ValueText: LegacyHandlerMethodName } method)
			{
				var diagnostic = Diagnostic.Create(
					MigrationDiagnosticDescriptors.HandlerSignatureDelta,
					method.Identifier.GetLocation(),
					typeDeclaration.Identifier.ValueText,
					LegacyHandlerMethodName,
					ExpectedHandlerMethodName);

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	private static bool ImplementsHandlerInterface(TypeDeclarationSyntax typeDeclaration)
	{
		foreach (var baseType in typeDeclaration.BaseList!.Types)
		{
			if (TryGetSimpleName(baseType.Type, out var name) && HandlerInterfaceNames.Contains(name))
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryGetSimpleName(TypeSyntax type, out string name)
	{
		switch (type)
		{
			case GenericNameSyntax generic:
				name = generic.Identifier.ValueText;
				return true;
			case QualifiedNameSyntax qualified:
				return TryGetSimpleName(qualified.Right, out name);
			case AliasQualifiedNameSyntax aliasQualified:
				return TryGetSimpleName(aliasQualified.Name, out name);
			case SimpleNameSyntax simple:
				name = simple.Identifier.ValueText;
				return true;
			default:
				name = string.Empty;
				return false;
		}
	}
}
