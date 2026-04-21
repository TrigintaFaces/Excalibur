// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Analyzes dispatch invocations and warns when the message type argument doesn't implement
/// any of the expected dispatch interfaces (IDispatchAction, IDispatchEvent, IDispatchMessage).
/// </summary>
/// <remarks>
/// <para>
/// Reports DISP006 when a type argument to IDispatcher.DispatchAsync&lt;T&gt; doesn't implement
/// a dispatch marker interface. This catches misuse at compile time rather than at runtime.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MessageTypeInterfaceAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(DiagnosticDescriptors.MessageTypeMissingInterface);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
	}

	private static void AnalyzeInvocation(OperationAnalysisContext context)
	{
		var invocation = (IInvocationOperation)context.Operation;
		var method = invocation.TargetMethod;

		// Only check DispatchAsync methods on IDispatcher
		if (method.Name != "DispatchAsync" || !method.IsGenericMethod)
		{
			return;
		}

		var containingType = method.ContainingType;
		if (containingType?.Name != "IDispatcher" &&
			!ImplementsIDispatcher(containingType, context.Compilation))
		{
			return;
		}

		// The first type argument is the message type
		if (method.TypeArguments.Length == 0)
		{
			return;
		}

		var messageType = method.TypeArguments[0];

		// Skip type parameters (open generics)
		if (messageType.TypeKind == TypeKind.TypeParameter)
		{
			return;
		}

		if (!ImplementsDispatchInterface(messageType, context.Compilation))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticDescriptors.MessageTypeMissingInterface,
					invocation.Syntax.GetLocation(),
					messageType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}

	private static bool ImplementsIDispatcher(INamedTypeSymbol? type, Compilation compilation)
	{
		if (type == null)
		{
			return false;
		}

		var dispatcherInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IDispatcher");
		if (dispatcherInterface == null)
		{
			return false;
		}

		return type.AllInterfaces.Any(i =>
			SymbolEqualityComparer.Default.Equals(i, dispatcherInterface));
	}

	private static bool ImplementsDispatchInterface(ITypeSymbol messageType, Compilation compilation)
	{
		var dispatchInterfaces = new[]
		{
			"Excalibur.Dispatch.Abstractions.IDispatchMessage",
			"Excalibur.Dispatch.Abstractions.IDispatchAction`1",
			"Excalibur.Dispatch.Abstractions.IDispatchEvent",
			"Excalibur.Dispatch.Abstractions.IDomainEvent",
			"Excalibur.Dispatch.Abstractions.IIntegrationEvent",
		};

		foreach (var interfaceName in dispatchInterfaces)
		{
			var interfaceSymbol = compilation.GetTypeByMetadataName(interfaceName);
			if (interfaceSymbol == null)
			{
				continue;
			}

			// Direct implementation check
			if (SymbolEqualityComparer.Default.Equals(messageType, interfaceSymbol))
			{
				return true;
			}

			foreach (var iface in messageType.AllInterfaces)
			{
				if (SymbolEqualityComparer.Default.Equals(iface, interfaceSymbol) ||
					(iface.IsGenericType && SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, interfaceSymbol)))
				{
					return true;
				}
			}
		}

		return false;
	}
}
