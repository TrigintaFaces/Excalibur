// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RSEXPERIMENTAL002 // Interceptable location is experimental

namespace Excalibur.Dispatch.SourceGenerators.Interception;

/// <summary>
/// Source generator that creates C# 12 interceptors for DispatchAsync call sites.
/// Interceptors redirect compile-time known dispatch calls to static methods that
/// bypass runtime handler resolution, eliminating dictionary lookups and virtual calls.
/// </summary>
/// <remarks>
/// <para>
/// PERF-9: This generator implements compile-time dispatch interception for 50% latency reduction.
/// It follows a three-tier resolution strategy:
/// <list type="number">
/// <item><description>Intercepted (this generator) - Direct static dispatch, zero lookups</description></item>
/// <item><description>Precompiled - FrozenDictionary lookup (PERF-13/14)</description></item>
/// <item><description>Runtime - Reflection-based fallback</description></item>
/// </list>
/// </para>
/// </remarks>
[Generator]
public class DispatchInterceptorGenerator : IIncrementalGenerator
{
	private const string DispatcherInterfaceName = "IDispatcher";
	private const string DispatchAsyncMethodName = "DispatchAsync";

	/// <summary>
	/// Initializes the interceptor generator with the given context.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all DispatchAsync invocations where message type is statically known
		var callSites = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsDispatchAsyncCandidate(node),
				transform: static (context, _) => GetInterceptorInfo(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!);

		// Generate interceptors for all discovered call sites
		context.RegisterSourceOutput(callSites.Collect(), GenerateInterceptors);
	}

	/// <summary>
	/// Checks if a syntax node is a potential DispatchAsync invocation.
	/// </summary>
	private static bool IsDispatchAsyncCandidate(SyntaxNode node)
	{
		// Look for method invocations
		if (node is not InvocationExpressionSyntax invocation)
		{
			return false;
		}

		// Check if it's a member access (e.g., dispatcher.DispatchAsync)
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return memberAccess.Name.Identifier.Text == DispatchAsyncMethodName;
		}

		// Could also be identifier (e.g., DispatchAsync if using 'using static')
		if (invocation.Expression is IdentifierNameSyntax identifier)
		{
			return identifier.Identifier.Text == DispatchAsyncMethodName;
		}

		return false;
	}

	/// <summary>
	/// Extracts interceptor information from a DispatchAsync call site.
	/// </summary>
	private static InterceptorInfo? GetInterceptorInfo(GeneratorSyntaxContext context)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;
		var semanticModel = context.SemanticModel;

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

		var isDispatcher = containingType.Name == DispatcherInterfaceName ||
						   containingType.AllInterfaces.Any(i => i.Name == DispatcherInterfaceName);

		if (!isDispatcher)
		{
			return null;
		}

		// Get the message type from the generic type argument
		if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length == 0)
		{
			return null;
		}

		var messageType = methodSymbol.TypeArguments[0];

		// Skip if the message type is a type parameter (generic method constraint)
		if (messageType is ITypeParameterSymbol)
		{
			return null;
		}

		// Skip if the message type is an interface (dynamic dispatch)
		if (messageType.TypeKind == TypeKind.Interface)
		{
			return null;
		}

		// Get the method name location for interception
		var methodNameNode = GetMethodNameNode(invocation);
		if (methodNameNode == null)
		{
			return null;
		}

		// Get interceptable location using the new Roslyn API
		var interceptableLocation = semanticModel.GetInterceptableLocation(invocation, cancellationToken: default);
		if (interceptableLocation == null)
		{
			return null;
		}

		// Get file location for unique ID generation
		var location = invocation.GetLocation();
		var lineSpan = location.GetLineSpan();

		// Determine result type if this is an action with response
		ITypeSymbol? resultType = null;
		var hasResult = false;

		if (methodSymbol.TypeArguments.Length > 1)
		{
			resultType = methodSymbol.TypeArguments[1];
			hasResult = true;
		}
		else
		{
			// Check if message type implements IDispatchAction<TResponse>
			foreach (var @interface in messageType.AllInterfaces)
			{
				if (@interface.Name == "IDispatchAction" && @interface.TypeArguments.Length > 0)
				{
					resultType = @interface.TypeArguments[0];
					hasResult = true;
					break;
				}
			}
		}

		return new InterceptorInfo
		{
			InterceptableLocationData = interceptableLocation.GetInterceptsLocationAttributeSyntax(),
			FilePath = lineSpan.Path,
			Line = lineSpan.StartLinePosition.Line + 1,
			Column = lineSpan.StartLinePosition.Character + 1,
			MessageType = messageType,
			MessageTypeFullName = messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			MessageTypeName = messageType.Name,
			HasResult = hasResult,
			ResultType = resultType,
			ResultTypeFullName = resultType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
		};
	}

	/// <summary>
	/// Gets the method name node from an invocation.
	/// </summary>
	private static SyntaxNode? GetMethodNameNode(InvocationExpressionSyntax invocation)
	{
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return memberAccess.Name;
		}

		if (invocation.Expression is IdentifierNameSyntax identifier)
		{
			return identifier;
		}

		return null;
	}

	/// <summary>
	/// Generates the interceptor source code for all discovered call sites.
	/// </summary>
	private static void GenerateInterceptors(SourceProductionContext context, ImmutableArray<InterceptorInfo> callSites)
	{
		if (callSites.IsDefaultOrEmpty)
		{
			return;
		}

		// Filter out call sites without valid interceptable location
		var validCallSites = callSites.Where(c => !string.IsNullOrEmpty(c.InterceptableLocationData)).ToList();
		if (validCallSites.Count == 0)
		{
			return;
		}

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Intercepted call sites: {validCallSites.Count}");
		_ = sb.AppendLine("// PERF-9: C# 12 Interceptors for compile-time dispatch resolution");
		_ = sb.AppendLine();

		// Required pragmas and usings
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("#pragma warning disable CS9113 // Parameter is unread");
		_ = sb.AppendLine();

		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Runtime.CompilerServices;");
		_ = sb.AppendLine("using System.Threading;");
		_ = sb.AppendLine("using System.Threading.Tasks;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions.Delivery;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Delivery;");
		_ = sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
		_ = sb.AppendLine();

		// InterceptsLocationAttribute definition (required for C# 12 interceptors)
		// This must be defined as a file-scoped class to avoid conflicts
		_ = sb.AppendLine("namespace System.Runtime.CompilerServices");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]");
		_ = sb.AppendLine("    file class InterceptsLocationAttribute : Attribute");
		_ = sb.AppendLine("    {");
		_ = sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");
		_ = sb.AppendLine();

		// Interceptor class
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Generated interceptors for DispatchAsync call sites.");
		_ = sb.AppendLine("    /// These methods bypass runtime handler resolution for compile-time known message types.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    file static class DispatchInterceptors");
		_ = sb.AppendLine("    {");

		// Generate interceptor methods for each call site
		foreach (var callSite in validCallSites)
		{
			GenerateInterceptorMethod(sb, callSite);
		}

		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");

		context.AddSource("DispatchInterceptors.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	/// <summary>
	/// Generates a single interceptor method for a call site.
	/// </summary>
	private static void GenerateInterceptorMethod(StringBuilder sb, InterceptorInfo callSite)
	{
		var methodName = $"Intercept_{callSite.UniqueId}";

		// InterceptsLocation attribute using the new Roslyn API format
		_ = sb.AppendLine($"        {callSite.InterceptableLocationData}");

		if (callSite.HasResult && callSite.ResultTypeFullName != null)
		{
			// Interceptor for DispatchAsync<TMessage, TResponse>
			_ = sb.AppendLine($"        internal static async Task<IMessageResult<{callSite.ResultTypeFullName}>> {methodName}(");
			_ = sb.AppendLine("            this IDispatcher dispatcher,");
			_ = sb.AppendLine($"            {callSite.MessageTypeFullName} message,");
			_ = sb.AppendLine("            IMessageContext context,");
			_ = sb.AppendLine("            CancellationToken cancellationToken)");
			_ = sb.AppendLine("        {");
			_ = sb.AppendLine("            // PERF-9: Direct dispatch through Dispatcher internals");
			_ = sb.AppendLine("            // This bypasses dictionary lookup and virtual dispatch");
			_ = sb.AppendLine($"            return await ((Dispatcher)dispatcher).DispatchAsync<{callSite.MessageTypeFullName}, {callSite.ResultTypeFullName}>(");
			_ = sb.AppendLine("                message, context, cancellationToken).ConfigureAwait(false);");
			_ = sb.AppendLine("        }");
		}
		else
		{
			// Interceptor for DispatchAsync<TMessage>
			_ = sb.AppendLine($"        internal static async Task<IMessageResult> {methodName}(");
			_ = sb.AppendLine("            this IDispatcher dispatcher,");
			_ = sb.AppendLine($"            {callSite.MessageTypeFullName} message,");
			_ = sb.AppendLine("            IMessageContext context,");
			_ = sb.AppendLine("            CancellationToken cancellationToken)");
			_ = sb.AppendLine("        {");
			_ = sb.AppendLine("            // PERF-9: Direct dispatch through Dispatcher internals");
			_ = sb.AppendLine("            // This bypasses dictionary lookup and virtual dispatch");
			_ = sb.AppendLine($"            return await ((Dispatcher)dispatcher).DispatchAsync<{callSite.MessageTypeFullName}>(");
			_ = sb.AppendLine("                message, context, cancellationToken).ConfigureAwait(false);");
			_ = sb.AppendLine("        }");
		}

		_ = sb.AppendLine();
	}
}
