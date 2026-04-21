// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Analyzes handler classes and suggests sealing them for performance.
/// </summary>
/// <remarks>
/// <para>
/// Reports DISP005 when a concrete (non-abstract) handler class is not sealed.
/// Sealed classes enable JIT devirtualization and prevent accidental inheritance.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerSealedAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DiagnosticDescriptors.HandlerShouldBeSealed);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		var namedType = (INamedTypeSymbol)context.Symbol;

		// Only analyze concrete, non-sealed classes
		if (namedType.TypeKind != TypeKind.Class ||
			namedType.IsAbstract ||
			namedType.IsSealed ||
			namedType.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
		{
			return;
		}

		// Check if it implements any Dispatch handler interface
		if (!ImplementsHandlerInterface(namedType, context.Compilation))
		{
			return;
		}

		// Report DISP005: Handler should be sealed
		context.ReportDiagnostic(
			Diagnostic.Create(
				DiagnosticDescriptors.HandlerShouldBeSealed,
				namedType.Locations[0],
				namedType.Name));
	}

	private static bool ImplementsHandlerInterface(INamedTypeSymbol namedType, Compilation compilation)
	{
		var handlerInterfaceNames = new[]
		{
			"Excalibur.Dispatch.Abstractions.Delivery.IActionHandler`1",
			"Excalibur.Dispatch.Abstractions.Delivery.IActionHandler`2",
			"Excalibur.Dispatch.Abstractions.Delivery.IEventHandler`1",
			"Excalibur.Dispatch.Abstractions.Delivery.IDocumentHandler`1",
			"Excalibur.Dispatch.Abstractions.IDispatchHandler`1",
		};

		foreach (var interfaceName in handlerInterfaceNames)
		{
			var interfaceSymbol = compilation.GetTypeByMetadataName(interfaceName);
			if (interfaceSymbol == null)
			{
				continue;
			}

			foreach (var iface in namedType.AllInterfaces)
			{
				if (iface.IsGenericType &&
					SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, interfaceSymbol))
				{
					return true;
				}
			}
		}

		return false;
	}
}
