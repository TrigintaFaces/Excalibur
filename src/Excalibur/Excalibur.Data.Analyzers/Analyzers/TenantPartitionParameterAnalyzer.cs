// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Data.Analyzers;

/// <summary>
/// EXDATA002 — a relational data request must not accept a tenant partition and then discard it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The predicate, stated exactly.</strong> The rule reports when all of the following hold:
/// </para>
/// <list type="number">
/// <item>the declaring type derives, transitively, from <c>Excalibur.Data.DataRequestBase&lt;,&gt;</c>;</item>
/// <item>a constructor of that type declares a parameter whose type is <c>Excalibur.Dispatch.TenantScope</c>
/// or <c>Excalibur.Dispatch.KeyedTenantPartition</c>;</item>
/// <item>that parameter is referenced <em>nowhere</em> in the constructor — not in its initializer, not in
/// its body, not in a lambda inside its body.</item>
/// </list>
/// <para>
/// <strong>What it deliberately does not fire on, and why each one matters.</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <em>A request addressed by a unique key.</em> It takes an identifier, not a partition, so it is not a
/// subject of the rule and stays silent with no annotation. That is the load-bearing case: a statement
/// already addressed by a primary key must not acquire a tenant term, so an analyzer that asked it for one
/// would be teaching the defect it exists to prevent.
/// </description>
/// </item>
/// <item>
/// <description>
/// <em>A deliberately estate-wide request.</em> It takes no tenant at all, so it is likewise not a subject
/// and needs no opt-out.
/// </description>
/// </item>
/// <item>
/// <description>
/// <em>Any use whatsoever of the parameter.</em> Converting it, storing it in a field, logging it, passing
/// it to a base constructor or a helper — all silence the rule. The rule does not attempt to prove the value
/// reaches the outgoing parameters. Such a proof would have to give up on indirection, and a proof that gives
/// up is indistinguishable from a defect; under a build that promotes warnings to errors, guessing wrong
/// fails a consumer's compilation.
/// </description>
/// </item>
/// <item>
/// <description>
/// <em>An <c>ITenantContext</c> parameter.</em> Excluded on purpose. A context can legitimately be present
/// for an authorization decision without being a filter term, and including it would reintroduce pressure
/// toward adding tenant predicates to statements that must not have them.
/// </description>
/// </item>
/// <item>
/// <description>
/// <em>A partial type with a primary constructor.</em> The parameter is in scope across every part, and
/// reading only one part could report a use that exists elsewhere. The rule declines to judge instead.
/// </description>
/// </item>
/// </list>
/// <para>
/// The remedy offered is "bind it, or remove the parameter" — never "add a tenant predicate". A request that
/// carries no tenant term takes no tenant, so removing the parameter is a correct and often the correct fix.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantPartitionParameterAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The open generic base every relational data request derives from.</summary>
	internal const string DataRequestBaseMetadataName = "Excalibur.Data.DataRequestBase`2";

	/// <summary>The partition-valued types whose only purpose is to name a tenant.</summary>
	internal static readonly ImmutableArray<string> TenantPartitionMetadataNames = ImmutableArray.Create(
		"Excalibur.Dispatch.TenantScope",
		"Excalibur.Dispatch.KeyedTenantPartition");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DataDiagnosticDescriptors.TenantPartitionParameterIsDiscarded);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationStart =>
		{
			var requestBase = compilationStart.Compilation.GetTypeByMetadataName(DataRequestBaseMetadataName);
			if (requestBase is null)
			{
				// This compilation does not reference the relational request surface, so it has no subjects.
				return;
			}

			var partitionTypes = TenantPartitionMetadataNames
				.Select(compilationStart.Compilation.GetTypeByMetadataName)
				.Where(static type => type is not null)
				.ToImmutableArray();

			if (partitionTypes.IsEmpty)
			{
				return;
			}

			compilationStart.RegisterSyntaxNodeAction(
				nodeContext => AnalyzeConstructor(nodeContext, requestBase, partitionTypes),
				SyntaxKind.ConstructorDeclaration);

			compilationStart.RegisterSyntaxNodeAction(
				nodeContext => AnalyzePrimaryConstructor(nodeContext, requestBase, partitionTypes),
				SyntaxKind.ClassDeclaration,
				SyntaxKind.RecordDeclaration);
		});
	}

	/// <summary>Handles the ordinary case: an explicitly declared constructor.</summary>
	private static void AnalyzeConstructor(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol requestBase,
		ImmutableArray<INamedTypeSymbol?> partitionTypes)
	{
		var declaration = (ConstructorDeclarationSyntax)context.Node;

		// A constructor with neither a body nor an expression body (extern, or a partial definition) has no
		// code in which a use could appear, so nothing can be concluded from the absence of one.
		if (declaration.Body is null && declaration.ExpressionBody is null)
		{
			return;
		}

		if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
			is not IMethodSymbol constructor)
		{
			return;
		}

		if (!DerivesFromRequestBase(constructor.ContainingType, requestBase))
		{
			return;
		}

		ReportDiscardedParameters(
			context,
			constructor.ContainingType,
			constructor.Parameters,
			partitionTypes,
			searchRoots: new SyntaxNode[] { declaration });
	}

	/// <summary>
	/// Handles a primary constructor, whose parameters are in scope across the whole type declaration rather
	/// than inside a constructor body.
	/// </summary>
	private static void AnalyzePrimaryConstructor(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol requestBase,
		ImmutableArray<INamedTypeSymbol?> partitionTypes)
	{
		var declaration = (TypeDeclarationSyntax)context.Node;
		if (declaration.ParameterList is null)
		{
			return;
		}

		if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
			is not INamedTypeSymbol type)
		{
			return;
		}

		// A partial type's primary-constructor parameters are in scope in every part. Reading one part would
		// let a use in another part read as an absence, so decline rather than guess.
		if (type.DeclaringSyntaxReferences.Length != 1)
		{
			return;
		}

		if (!DerivesFromRequestBase(type, requestBase))
		{
			return;
		}

		// The primary constructor is the one whose declaring syntax IS the type declaration. Compared by tree
		// and span rather than by node identity, which Roslyn does not promise across GetSyntax calls.
		var primaryConstructor = type.InstanceConstructors.FirstOrDefault(
			ctor => ctor.DeclaringSyntaxReferences.Any(
				reference => reference.SyntaxTree == declaration.SyntaxTree
					&& reference.Span == declaration.Span));

		if (primaryConstructor is null)
		{
			return;
		}

		ReportDiscardedParameters(
			context,
			type,
			primaryConstructor.Parameters,
			partitionTypes,
			searchRoots: new SyntaxNode[] { declaration });
	}

	private static void ReportDiscardedParameters(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol declaringType,
		ImmutableArray<IParameterSymbol> parameters,
		ImmutableArray<INamedTypeSymbol?> partitionTypes,
		IReadOnlyList<SyntaxNode> searchRoots)
	{
		foreach (var parameter in parameters)
		{
			if (!IsTenantPartition(parameter.Type, partitionTypes))
			{
				continue;
			}

			if (IsReferenced(context, parameter, searchRoots))
			{
				continue;
			}

			var location = parameter.Locations.FirstOrDefault();
			if (location is null || !location.IsInSource)
			{
				continue;
			}

			context.ReportDiagnostic(
				Diagnostic.Create(
					DataDiagnosticDescriptors.TenantPartitionParameterIsDiscarded,
					location,
					declaringType.Name,
					parameter.Name));
		}
	}

	private static bool IsTenantPartition(ITypeSymbol type, ImmutableArray<INamedTypeSymbol?> partitionTypes)
	{
		foreach (var candidate in partitionTypes)
		{
			if (candidate is not null && SymbolEqualityComparer.Default.Equals(type, candidate))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Reports whether the parameter is bound to any identifier in the searched syntax.
	/// </summary>
	/// <remarks>
	/// The name match is only a cheap filter; the decision is made by resolving the identifier's symbol and
	/// comparing it to the parameter, so an unrelated local or member that happens to share the name neither
	/// silences the rule nor triggers it.
	/// </remarks>
	private static bool IsReferenced(
		SyntaxNodeAnalysisContext context,
		IParameterSymbol parameter,
		IReadOnlyList<SyntaxNode> searchRoots)
	{
		foreach (var root in searchRoots)
		{
			foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (!string.Equals(identifier.Identifier.ValueText, parameter.Name, System.StringComparison.Ordinal))
				{
					continue;
				}

				var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
				if (SymbolEqualityComparer.Default.Equals(symbol, parameter))
				{
					return true;
				}

				// An identifier that carries the parameter's name but did not resolve is ambiguous. Treat it
				// as a use: silence costs a missed defect, a false report costs a broken build.
				if (symbol is null)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool DerivesFromRequestBase(INamedTypeSymbol? type, INamedTypeSymbol requestBase)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, requestBase))
			{
				return true;
			}
		}

		return false;
	}
}
