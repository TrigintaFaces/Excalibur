// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RSEXPERIMENTAL002 // Interceptable location is experimental

namespace Excalibur.Dispatch.SourceGenerators.Pipeline;

/// <summary>
/// Generates call-site forwarding methods for supported dispatcher invocations.
/// </summary>
/// <remarks>Forwarding preserves the selected overload, returned task, exceptions and cancellation.
/// Middleware remains owned and executed by the configured dispatcher.</remarks>
[Generator]
public sealed class StaticPipelineGenerator : IIncrementalGenerator
{
	private const string DispatchAsyncMethodName = "DispatchAsync";
	private const string DispatchMessageInterfaceName = "IDispatchMessage";
	private const string DispatchCommandInterfaceName = "IDispatchCommand";
	private const string DispatchQueryInterfaceName = "IDispatchQuery";
	private const string DomainEventInterfaceName = "IDomainEvent";
	private const string IntegrationEventInterfaceName = "IIntegrationEvent";

	/// <summary>
	/// Initializes the static pipeline generator with the given context.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all DispatchAsync invocations where message type is statically known
		var callSites = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsDispatchAsyncCandidate(node),
				transform: static (context, _) => GetPipelineChainInfo(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!);

		// Generate static pipelines for deterministic message types
		context.RegisterSourceOutput(callSites.Collect(), GenerateStaticPipelines);
	}

	/// <summary>
	/// Checks if a syntax node is a potential DispatchAsync invocation.
	/// </summary>
	private static bool IsDispatchAsyncCandidate(SyntaxNode node)
	{
		if (node is not InvocationExpressionSyntax invocation)
		{
			return false;
		}

		// Check for member access (e.g., dispatcher.DispatchAsync)
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return memberAccess.Name.Identifier.Text == DispatchAsyncMethodName;
		}

		// Check for identifier (e.g., DispatchAsync with using static)
		if (invocation.Expression is IdentifierNameSyntax identifier)
		{
			return identifier.Identifier.Text == DispatchAsyncMethodName;
		}

		return false;
	}

	/// <summary>
	/// Extracts pipeline chain information from a DispatchAsync call site.
	/// </summary>
	private static PipelineChainInfo? GetPipelineChainInfo(GeneratorSyntaxContext context)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;
		var semanticModel = context.SemanticModel;

		// Get the containing namespace to skip Excalibur framework internals
		// This avoids interceptor conflicts with DispatchInterceptorGenerator
		var containingClass = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
		if (containingClass != null)
		{
			var containingTypeSymbol = semanticModel.GetDeclaredSymbol(containingClass);
			var containingNamespace = containingTypeSymbol?.ContainingNamespace?.ToDisplayString() ?? string.Empty;

			// Skip call sites within Excalibur.Dispatch.* namespaces to avoid conflicts
			// with DispatchInterceptorGenerator
			if (containingNamespace.StartsWith("Excalibur.Dispatch.", StringComparison.Ordinal) ||
				containingNamespace == "Excalibur.Dispatch")
			{
				return null;
			}
		}

		// Get the method symbol
		var symbolInfo = semanticModel.GetSymbolInfo(invocation);
		if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
		{
			return null;
		}

		// Verify this is on a type that implements IDispatcher
		var containingType = methodSymbol.ContainingType;
		if (containingType == null)
		{
			return null;
		}

		var isDispatcher = SymbolEqualityComparer.Default.Equals(containingType,
			semanticModel.Compilation.GetTypeByMetadataName("Excalibur.Dispatch.IDispatcher"))
			&& methodSymbol.Parameters.Length == 3;

		if (!isDispatcher)
		{
			return null;
		}

		// Get the message type from the generic type argument
		if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length == 0)
		{
			return null;
		}

		if (methodSymbol.TypeArguments[0] is not INamedTypeSymbol messageType)
		{
			return null;
		}

		// Forwarders live in a separate generated file, outside the caller's lexical scope.
		if (!CanNameType(messageType, semanticModel.Compilation)
			|| !CanNameType(methodSymbol.ReturnType, semanticModel.Compilation))
		{
			return null;
		}

		// Skip interfaces (dynamic dispatch)
		if (messageType.TypeKind == TypeKind.Interface)
		{
			return null;
		}

		// Verify it implements IDispatchMessage
		var implementsMessage = messageType.AllInterfaces.Any(i =>
			i.Name == DispatchMessageInterfaceName);

		if (!implementsMessage)
		{
			return null;
		}

		// Check for determinism (simplified check - full version would query PipelineMetadata)
		var (isDeterministic, nonDeterministicReason) = CheckDeterminism(messageType);

		// Get interceptable location
		var interceptableLocation = semanticModel.GetInterceptableLocation(invocation, cancellationToken: default);
		if (interceptableLocation == null)
		{
			return null;
		}

		// Get file location for unique ID
		var location = invocation.GetLocation();
		var lineSpan = location.GetLineSpan();

		// Determine message kind and result type
		var messageKind = DetermineMessageKind(messageType);
		var (hasResult, resultType, resultTypeFullName) = DetermineResultType(methodSymbol);

		return new PipelineChainInfo
		{
			MessageType = messageType,
			MessageTypeFullName = messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			MessageTypeName = messageType.Name,
			MessageKind = messageKind,
			IsDeterministic = isDeterministic,
			NonDeterministicReason = nonDeterministicReason,
			HasResult = hasResult,
			ResultType = resultType,
			ResultTypeFullName = resultTypeFullName,
			InterceptableLocationData = interceptableLocation.GetInterceptsLocationAttributeSyntax(),
			FilePath = lineSpan.Path,
			Line = lineSpan.StartLinePosition.Line + 1,
			Column = lineSpan.StartLinePosition.Character + 1
		};
	}

	private static bool CanNameType(ITypeSymbol type, Compilation compilation)
	{
		if (type is IArrayTypeSymbol array)
		{
			return CanNameType(array.ElementType, compilation);
		}
		if (type is not INamedTypeSymbol named || named.IsAnonymousType || named.IsUnboundGenericType
			|| named.IsFileLocal || named.TypeKind == TypeKind.Error
			|| !compilation.IsSymbolAccessibleWithin(named, compilation.Assembly))
		{
			return false;
		}
		return (named.ContainingType is null || CanNameType(named.ContainingType, compilation))
			&& named.TypeArguments.All(argument => CanNameType(argument, compilation));
	}

	/// <summary>
	/// Checks if a message type has a deterministic pipeline.
	/// </summary>
	private static (bool IsDeterministic, string? Reason) CheckDeterminism(INamedTypeSymbol messageType)
	{
		// Check for attributes that indicate non-deterministic pipelines
		var attributes = messageType.GetAttributes();

		// Pipeline profile attributes
		if (attributes.Any(a => a.AttributeClass?.Name is "PipelineProfileAttribute" or "UsePipelineProfileAttribute"))
		{
			// Dynamic profile selection
			var profileAttr = attributes.FirstOrDefault(a =>
				a.AttributeClass?.Name is "PipelineProfileAttribute" or "UsePipelineProfileAttribute");
			if (profileAttr?.ConstructorArguments.Length == 0 ||
				profileAttr?.ConstructorArguments[0].Value == null)
			{
				return (false, "Dynamic pipeline profile selection");
			}
		}

		// Tenant-specific attributes
		if (attributes.Any(a => a.AttributeClass?.Name is "TenantSpecificAttribute" or "PerTenantAttribute" or "MultiTenantAttribute"))
		{
			return (false, "Tenant-specific pipeline routing");
		}

		// Conditional middleware attributes
		if (attributes.Any(a => a.AttributeClass?.Name is "ConditionalMiddlewareAttribute" or "FeatureFlagMiddlewareAttribute"))
		{
			return (false, "Conditional middleware via attribute");
		}

		return (true, null);
	}

	/// <summary>
	/// Determines the message kind from implemented interfaces.
	/// </summary>
	private static string DetermineMessageKind(INamedTypeSymbol messageType)
	{
		var interfaces = messageType.AllInterfaces;

		if (interfaces.Any(i => i.Name == DispatchCommandInterfaceName))
		{
			return "Command";
		}

		if (interfaces.Any(i => i.Name == DispatchQueryInterfaceName ||
								i.Name.StartsWith("IDispatchAction", StringComparison.Ordinal)))
		{
			return "Query";
		}

		if (interfaces.Any(i => i.Name == DomainEventInterfaceName))
		{
			return "DomainEvent";
		}

		if (interfaces.Any(i => i.Name == IntegrationEventInterfaceName))
		{
			return "IntegrationEvent";
		}

		return "Message";
	}

	/// <summary>
	/// Determines if the message returns a result and gets the result type.
	/// </summary>
	private static (bool HasResult, ITypeSymbol? ResultType, string? ResultTypeFullName) DetermineResultType(
		IMethodSymbol methodSymbol)
	{
		if (methodSymbol.ReturnType is INamedTypeSymbol { TypeArguments.Length: 1 } task
			&& task.TypeArguments[0] is INamedTypeSymbol { TypeArguments.Length: 1 } result)
		{
			var resultType = result.TypeArguments[0];
			return (true, resultType, resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
		}
		return (false, null, null);
	}

	/// <summary>
	/// Generates static pipeline interceptors for all discovered call sites.
	/// </summary>
	private static void GenerateStaticPipelines(
		SourceProductionContext context,
		ImmutableArray<PipelineChainInfo> callSites)
	{
		if (callSites.IsDefaultOrEmpty)
		{
			return;
		}

		// Filter to only deterministic call sites with valid interceptable locations
		var staticPipelineCandidates = callSites
			.Where(c => c.IsDeterministic && !string.IsNullOrEmpty(c.InterceptableLocationData))
			.ToList();

		if (staticPipelineCandidates.Count == 0)
		{
			return;
		}

		var sb = new StringBuilder();
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Threading;");
		_ = sb.AppendLine("using System.Threading.Tasks;");
		_ = sb.AppendLine("using Excalibur.Dispatch;");
		_ = sb.AppendLine("namespace System.Runtime.CompilerServices");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]");
		_ = sb.AppendLine("    file sealed class InterceptsLocationAttribute : Attribute");
		_ = sb.AppendLine("    {");
		_ = sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    file static class StaticPipelines");
		_ = sb.AppendLine("    {");
		var ordinal = 0;
		foreach (var callSite in staticPipelineCandidates.OrderBy(static site => site.FilePath, StringComparer.Ordinal)
			.ThenBy(static site => site.Line).ThenBy(static site => site.Column))
		{
			GenerateStaticPipelineMethod(sb, callSite, ordinal++);
		}
		_ = sb.AppendLine($"        public static int InterceptionCount => {staticPipelineCandidates.Count};");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");
		context.AddSource("StaticPipelines.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	/// <summary>
	/// Generates a single static pipeline interceptor method.
	/// </summary>
	private static void GenerateStaticPipelineMethod(StringBuilder sb, PipelineChainInfo callSite, int ordinal)
	{
		var result = callSite.HasResult ? $"IMessageResult<{callSite.ResultTypeFullName}>" : "IMessageResult";
		var arguments = callSite.HasResult
			? $"{callSite.MessageTypeFullName}, {callSite.ResultTypeFullName}"
			: callSite.MessageTypeFullName;
		_ = sb.AppendLine($"        {callSite.InterceptableLocationData}");
		_ = sb.AppendLine($"        internal static Task<{result}> Forward_{ordinal}(");
		_ = sb.AppendLine("            this IDispatcher dispatcher,");
		_ = sb.AppendLine($"            {callSite.MessageTypeFullName} message,");
		_ = sb.AppendLine("            IMessageContext context,");
		_ = sb.AppendLine("            CancellationToken cancellationToken)");
		_ = sb.AppendLine($"            => dispatcher.DispatchAsync<{arguments}>(message, context, cancellationToken);");
	}
}
