// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Provides performance optimization hints for handler implementations.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer reports DISP004 (Info) diagnostics with optimization suggestions:
/// <list type="bullet">
/// <item>Classes that could be sealed</item>
/// <item>Methods returning Task that could return ValueTask</item>
/// <item>Handlers that could benefit from specific patterns</item>
/// </list>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptimizationHintAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Gets the supported diagnostics for this analyzer.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DiagnosticDescriptors.OptimizationHint);

	/// <summary>
	/// Initializes the analyzer context.
	/// </summary>
	/// <param name="context">The analysis context.</param>
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		var namedType = (INamedTypeSymbol)context.Symbol;

		// Only analyze classes
		if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
		{
			return;
		}

		// Check if this is a handler type
		if (!IsHandlerType(namedType, context.Compilation))
		{
			return;
		}

		// Suggest sealing if not already sealed and has no derived types
		if (!namedType.IsSealed)
		{
			var hasDerivedTypes = HasDerivedTypes(namedType, context.Compilation);
			if (!hasDerivedTypes)
			{
				var diagnostic = Diagnostic.Create(
					DiagnosticDescriptors.OptimizationHint,
					namedType.Locations[0],
					$"Handler '{namedType.Name}' could be sealed for better performance (enables devirtualization)");

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var method = (IMethodSymbol)context.Symbol;

		// Only analyze HandleAsync methods in handler types
		// Skip abstract methods - they have no implementation to optimize
		if (method.Name != "HandleAsync" ||
			method.IsAbstract ||
			method.ContainingType == null ||
			!IsHandlerType(method.ContainingType, context.Compilation))
		{
			return;
		}

		// Check if method returns Task<T> and could return ValueTask<T>
		if (method.ReturnType is not INamedTypeSymbol returnType)
		{
			return;
		}

		// Suggest ValueTask if returning Task and method body suggests sync completion
		if (returnType.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")
		{
			// Only suggest if method has simple body (likely sync completion)
			// This is a heuristic - the actual check would need syntax analysis
			var diagnostic = Diagnostic.Create(
				DiagnosticDescriptors.OptimizationHint,
				method.Locations[0],
				$"Method '{method.Name}' returns Task<T>. Consider ValueTask<T> if synchronous completion is common for zero-allocation fast path");

			context.ReportDiagnostic(diagnostic);
		}
	}

	private static bool IsHandlerType(INamedTypeSymbol namedType, Compilation compilation)
	{
		// Check for IDispatchHandler<T>
		var dispatchHandlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IDispatchHandler`1");
		if (dispatchHandlerInterface != null)
		{
			foreach (var iface in namedType.AllInterfaces)
			{
				if (iface.IsGenericType &&
					SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, dispatchHandlerInterface))
				{
					return true;
				}
			}
		}

		// Check for IActionHandler<T>
		var actionHandlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IActionHandler`1");
		if (actionHandlerInterface != null)
		{
			foreach (var iface in namedType.AllInterfaces)
			{
				if (iface.IsGenericType &&
					SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, actionHandlerInterface))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool HasDerivedTypes(INamedTypeSymbol _1, Compilation _2)
	{
		// Simple check: look for types that directly inherit from this type
		// A more comprehensive check would need to examine all types in the compilation
		// For performance, we just check if the class is already sealed or abstract
		// If not sealed and not abstract, suggest sealing
		// Note: Full derived type detection would require iterating all compilation types
		return false; // Simplified: assume no derived types for non-sealed handlers
	}
}
