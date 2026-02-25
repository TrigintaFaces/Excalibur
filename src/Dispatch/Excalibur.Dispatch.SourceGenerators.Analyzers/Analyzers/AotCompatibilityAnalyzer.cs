// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Analyzes code for AOT compatibility issues with reflection usage.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer reports DISP003 (Warning) when reflection-based methods are used
/// without proper AOT annotations like [RequiresDynamicCode] or [DynamicallyAccessedMembers].
/// </para>
/// <para>
/// Detected patterns:
/// <list type="bullet">
/// <item>Type.GetType(string)</item>
/// <item>Type.MakeGenericType(params Type[])</item>
/// <item>MethodInfo.MakeGenericMethod(params Type[])</item>
/// <item>Activator.CreateInstance(Type)</item>
/// </list>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AotCompatibilityAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Methods that require AOT annotations when used.
	/// </summary>
	private static readonly ImmutableHashSet<string> ReflectionMethods = ImmutableHashSet.Create(
		"System.Type.GetType",
		"System.Type.MakeGenericType",
		"System.Reflection.MethodInfo.MakeGenericMethod",
		"System.Activator.CreateInstance",
		"System.Reflection.Assembly.GetType");

	/// <summary>
	/// Gets the supported diagnostics for this analyzer.
	/// </summary>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DiagnosticDescriptors.ReflectionWithoutAotAnnotation);

	/// <summary>
	/// Initializes the analyzer context.
	/// </summary>
	/// <param name="context">The analysis context.</param>
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
	}

	private static void AnalyzeInvocation(OperationAnalysisContext context)
	{
		var invocation = (IInvocationOperation)context.Operation;
		var targetMethod = invocation.TargetMethod;

		// Build fully qualified method name
		var containingType = targetMethod.ContainingType?.ToDisplayString();
		if (containingType == null)
		{
			return;
		}

		var fullMethodName = $"{containingType}.{targetMethod.Name}";

		// Check if this is a reflection method that needs AOT annotation
		if (!ReflectionMethods.Contains(fullMethodName))
		{
			return;
		}

		// Get the containing method/property
		var containingSymbol = GetContainingMethodOrProperty(context.ContainingSymbol);
		if (containingSymbol == null)
		{
			return;
		}

		// Check if the containing member has AOT annotations
		if (HasAotAnnotation(containingSymbol))
		{
			return;
		}

		// Report diagnostic
		var diagnostic = Diagnostic.Create(
			DiagnosticDescriptors.ReflectionWithoutAotAnnotation,
			invocation.Syntax.GetLocation(),
			containingSymbol.Name,
			targetMethod.Name);

		context.ReportDiagnostic(diagnostic);
	}

	private static ISymbol? GetContainingMethodOrProperty(ISymbol? symbol)
	{
		while (symbol != null)
		{
			if (symbol is IMethodSymbol or IPropertySymbol)
			{
				return symbol;
			}

			symbol = symbol.ContainingSymbol;
		}

		return null;
	}

	private static bool HasAotAnnotation(ISymbol symbol)
	{
		// Check for [RequiresDynamicCode], [RequiresUnreferencedCode], [DynamicallyAccessedMembers]
		foreach (var attribute in symbol.GetAttributes())
		{
			var attributeName = attribute.AttributeClass?.Name;
			if (attributeName is "RequiresDynamicCodeAttribute" or
				"RequiresUnreferencedCodeAttribute" or
				"DynamicallyAccessedMembersAttribute" or
				"UnconditionalSuppressMessageAttribute")
			{
				return true;
			}
		}

		// Also check containing type for class-level annotations
		if (symbol.ContainingType != null)
		{
			foreach (var attribute in symbol.ContainingType.GetAttributes())
			{
				var attributeName = attribute.AttributeClass?.Name;
				if (attributeName is "RequiresDynamicCodeAttribute" or
					"RequiresUnreferencedCodeAttribute")
				{
					return true;
				}
			}
		}

		return false;
	}
}
