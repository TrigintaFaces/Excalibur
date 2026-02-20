// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Analyzes handler classes for discoverability by source generators.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer reports DISP001 (Warning) and DISP002 (Info) diagnostics for handlers
/// that may not be properly discovered by the Dispatch source generators.
/// </para>
/// <para>
/// DISP001: Handler Not Discoverable - when a handler implements IDispatchHandler but
/// doesn't have [AutoRegister] attribute and may not be in a scanned assembly.
/// </para>
/// <para>
/// DISP002: Missing AutoRegister Attribute - informational suggestion to add the attribute.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerDiscoverabilityAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Gets the supported diagnostics for this analyzer.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(
			DiagnosticDescriptors.HandlerNotDiscoverable,
			DiagnosticDescriptors.MissingAutoRegisterAttribute);

	/// <summary>
	/// Initializes the analyzer context.
	/// </summary>
	/// <param name="context">The analysis context.</param>
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		var namedType = (INamedTypeSymbol)context.Symbol;

		// Only analyze classes that are non-abstract and public/internal
		if (namedType.TypeKind != TypeKind.Class ||
			namedType.IsAbstract ||
			namedType.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
		{
			return;
		}

		// Check if it implements any Dispatch handler interface
		var handlerInterface = GetImplementedHandlerInterface(namedType, context.Compilation);
		if (handlerInterface == null)
		{
			return;
		}

		// Check for [AutoRegister] attribute
		var hasAutoRegisterAttribute = HasAutoRegisterAttribute(namedType);

		if (!hasAutoRegisterAttribute)
		{
			// Report DISP001: Handler Not Discoverable (Warning)
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.HandlerNotDiscoverable,
				namedType.Locations[0],
				namedType.Name,
				handlerInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

			context.ReportDiagnostic(diagnostic);

			// Report DISP002: Missing AutoRegister Attribute (Info)
			var infoDiagnostic = Diagnostic.Create(
				DiagnosticDescriptors.MissingAutoRegisterAttribute,
				namedType.Locations[0],
				namedType.Name);

			context.ReportDiagnostic(infoDiagnostic);
		}
	}

	private static INamedTypeSymbol? GetImplementedHandlerInterface(
		INamedTypeSymbol namedType,
		Compilation compilation)
	{
		// Look for IDispatchHandler<T> interface
		var dispatchHandlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IDispatchHandler`1")
			?? compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.Delivery.IDispatchHandler`1");

		foreach (var iface in namedType.AllInterfaces)
		{
			if (iface.IsGenericType &&
				SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, dispatchHandlerInterface))
			{
				return iface;
			}
		}

		// Also check for IActionHandler<T> and IStreamingDocumentHandler<T,U>
		var actionHandlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IActionHandler`1");
		if (actionHandlerInterface != null)
		{
			foreach (var iface in namedType.AllInterfaces)
			{
				if (iface.IsGenericType &&
					SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, actionHandlerInterface))
				{
					return iface;
				}
			}
		}

		var streamingHandlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IStreamingDocumentHandler`2");
		if (streamingHandlerInterface != null)
		{
			foreach (var iface in namedType.AllInterfaces)
			{
				if (iface.IsGenericType &&
					SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, streamingHandlerInterface))
				{
					return iface;
				}
			}
		}

		return null;
	}

	private static bool HasAutoRegisterAttribute(INamedTypeSymbol namedType)
	{
		foreach (var attribute in namedType.GetAttributes())
		{
			if (attribute.AttributeClass?.Name is "AutoRegisterAttribute" or "AutoRegister")
			{
				return true;
			}
		}

		return false;
	}
}
