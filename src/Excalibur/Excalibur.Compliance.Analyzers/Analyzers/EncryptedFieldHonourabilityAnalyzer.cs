// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Compliance.Analyzers;

/// <summary>
/// Reports <c>[EncryptedField]</c> and <c>[Sensitive]</c> annotations the framework cannot honour.
/// </summary>
/// <remarks>
/// <para>
/// The criteria are the ones <c>EncryptedFieldBinding.Inspect</c> applies at run time, checked in the same
/// order: a <c>[PersonalData]</c> property with an explicit purpose, then an unsupported type, then a
/// missing accessor. Only the first applicable problem is reported for a property, as the runtime does.
/// </para>
/// <para>
/// One rule has no runtime counterpart. The runtime inspects public instance properties only, so an
/// annotation on a static or non-public property is never seen -- no exception, no log line. That is the
/// silent case, and it is reported here because nothing else ever will.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EncryptedFieldHonourabilityAnalyzer : DiagnosticAnalyzer
{
	internal const string EncryptedFieldMetadataName = "Excalibur.Compliance.EncryptedFieldAttribute";
	internal const string SensitiveMetadataName = "Excalibur.Compliance.SensitiveAttribute";
	internal const string PersonalDataMetadataName = "Excalibur.Compliance.PersonalDataAttribute";

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(
			ComplianceDiagnosticDescriptors.UnsupportedPropertyType,
			ComplianceDiagnosticDescriptors.MissingAccessor,
			ComplianceDiagnosticDescriptors.NotInspected,
			ComplianceDiagnosticDescriptors.ErasableAndSurvivingPurpose);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationStart =>
		{
			var compilation = compilationStart.Compilation;
			var encryptedField = compilation.GetTypeByMetadataName(EncryptedFieldMetadataName);
			var sensitive = compilation.GetTypeByMetadataName(SensitiveMetadataName);
			if (encryptedField is null && sensitive is null)
			{
				// Neither attribute is referenced, so no annotation can exist in this compilation.
				return;
			}

			var symbols = new KnownSymbols(
				encryptedField,
				sensitive,
				compilation.GetTypeByMetadataName(PersonalDataMetadataName));

			compilationStart.RegisterSymbolAction(
				symbolContext => AnalyzeProperty(symbolContext, symbols),
				SymbolKind.Property);
		});
	}

	private static void AnalyzeProperty(SymbolAnalysisContext context, KnownSymbols symbols)
	{
		var property = (IPropertySymbol)context.Symbol;

		var encryptedField = Find(property, symbols.EncryptedField);
		var sensitive = Find(property, symbols.Sensitive);
		if (encryptedField is null && sensitive is null)
		{
			return;
		}

		// The marker names the attribute the author actually wrote, so the diagnostic points at the line
		// they need to change.
		var marker = encryptedField is not null ? "[EncryptedField]" : "[Sensitive]";
		var location = LocationOf(encryptedField ?? sensitive!, property);
		var name = property.ContainingType.Name + "." + property.Name;

		if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
		{
			var why = property.IsStatic ? "static" : "not public";
			context.ReportDiagnostic(Diagnostic.Create(
				ComplianceDiagnosticDescriptors.NotInspected, location, marker, name, why));
			return;
		}

		if (encryptedField is not null
			&& symbols.PersonalData is not null
			&& Find(property, symbols.PersonalData) is not null
			&& PurposeOf(encryptedField) is { } purpose
			&& !string.IsNullOrWhiteSpace(purpose))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				ComplianceDiagnosticDescriptors.ErasableAndSurvivingPurpose, location, name, purpose));
			return;
		}

		if (!IsEncryptable(property.Type))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				ComplianceDiagnosticDescriptors.UnsupportedPropertyType,
				location,
				marker,
				name,
				property.Type.ToDisplayString()));
			return;
		}

		// An init accessor is a setter to reflection, which is what writes the ciphertext back.
		if (property.GetMethod is null || property.SetMethod is null)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				ComplianceDiagnosticDescriptors.MissingAccessor, location, marker, name));
		}
	}

	private static bool IsEncryptable(ITypeSymbol type) =>
		type.SpecialType == SpecialType.System_String
		|| (type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte });

	private static AttributeData? Find(IPropertySymbol property, INamedTypeSymbol? attributeType) =>
		attributeType is null
			? null
			: property.GetAttributes().FirstOrDefault(a =>
				SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));

	private static string? PurposeOf(AttributeData attribute)
	{
		foreach (var argument in attribute.NamedArguments)
		{
			if (argument.Key == "Purpose" && argument.Value.Value is string purpose)
			{
				return purpose;
			}
		}

		return null;
	}

	private static Location LocationOf(AttributeData attribute, IPropertySymbol property) =>
		attribute.ApplicationSyntaxReference is { } reference
			? Location.Create(reference.SyntaxTree, reference.Span)
			: property.Locations.FirstOrDefault() ?? Location.None;

	private sealed class KnownSymbols(
		INamedTypeSymbol? encryptedField,
		INamedTypeSymbol? sensitive,
		INamedTypeSymbol? personalData)
	{
		public INamedTypeSymbol? EncryptedField { get; } = encryptedField;

		public INamedTypeSymbol? Sensitive { get; } = sensitive;

		public INamedTypeSymbol? PersonalData { get; } = personalData;
	}
}
