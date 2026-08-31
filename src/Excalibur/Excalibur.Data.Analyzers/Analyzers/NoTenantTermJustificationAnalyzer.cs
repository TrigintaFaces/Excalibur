// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Data.Analyzers;

/// <summary>
/// EXDATA001 — a type declaring that it carries no tenant term must state why.
/// </summary>
/// <remarks>
/// <para>
/// The predicate is entirely positive: the attribute is present, its justification argument is a compile-time
/// constant, and that constant is null, empty, or whitespace. There is no inference and no dataflow, so there
/// is no shape on which the rule can be wrong.
/// </para>
/// <para>
/// This rule exists because moving the declaration from a factory method to an attribute would otherwise
/// silently drop a guarantee. The factory called <c>ThrowIfNullOrWhiteSpace</c> on its reason; an attribute
/// constructor runs only if someone reflects over it, which nothing does, so the check had to move from run
/// time to compile time or cease to exist.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoTenantTermJustificationAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The metadata name of the attribute this rule inspects.</summary>
	internal const string AttributeMetadataName = "Excalibur.Data.NoTenantTermAttribute";

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DataDiagnosticDescriptors.NoTenantTermJustificationIsEmpty);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationStart =>
		{
			var attributeType = compilationStart.Compilation.GetTypeByMetadataName(AttributeMetadataName);
			if (attributeType is null)
			{
				// The attribute is not referenced by this compilation, so no declaration can exist.
				return;
			}

			compilationStart.RegisterSymbolAction(
				symbolContext => AnalyzeType(symbolContext, attributeType),
				SymbolKind.NamedType);
		});
	}

	private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol attributeType)
	{
		var type = (INamedTypeSymbol)context.Symbol;

		foreach (var attribute in type.GetAttributes())
		{
			if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
			{
				continue;
			}

			// (confinement, justification) — the justification is the second positional argument. A shorter
			// argument list is a compiler error against this attribute's only constructor, and reporting a
			// second diagnostic on top of it would only add noise.
			if (attribute.ConstructorArguments.Length < 2)
			{
				continue;
			}

			var justification = attribute.ConstructorArguments[1];
			if (justification.Kind == TypedConstantKind.Error)
			{
				// The argument did not resolve to a constant; the compiler has already said so.
				continue;
			}

			if (justification.Value is string text && !string.IsNullOrWhiteSpace(text))
			{
				continue;
			}

			var confinement = DescribeConfinement(attribute);
			var location = attribute.ApplicationSyntaxReference is { } reference
				? Location.Create(reference.SyntaxTree, reference.Span)
				: type.Locations.FirstOrDefault() ?? Location.None;

			context.ReportDiagnostic(
				Diagnostic.Create(
					DataDiagnosticDescriptors.NoTenantTermJustificationIsEmpty,
					location,
					type.Name,
					confinement));
		}
	}

	/// <summary>
	/// Renders the declared confinement kind for the diagnostic message, so the author is asked about the
	/// argument they actually chose rather than about tenancy in general.
	/// </summary>
	private static string DescribeConfinement(AttributeData attribute)
	{
		var confinement = attribute.ConstructorArguments[0];

		if (confinement.Kind == TypedConstantKind.Enum && confinement.Type is INamedTypeSymbol enumType)
		{
			foreach (var member in enumType.GetMembers())
			{
				if (member is IFieldSymbol { HasConstantValue: true } field
					&& Equals(field.ConstantValue, confinement.Value))
				{
					return $"{enumType.Name}.{field.Name}";
				}
			}
		}

		return "the declared confinement argument";
	}
}
