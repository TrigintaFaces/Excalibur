// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators.Interception;

/// <summary>
/// Source generator that creates a middleware invoker registry to eliminate interface dispatch overhead.
/// </summary>
/// <remarks>
/// <para>
/// PERF-10: This generator scans for classes implementing <c>IDispatchMiddleware</c> and generates
/// a <c>MiddlewareInvokerRegistry</c> with typed invoker delegates stored in a FrozenDictionary.
/// </para>
/// <para>
/// Unlike <see cref="DispatchInterceptorGenerator"/> which intercepts call sites, this generator
/// creates a runtime registry because middleware types are resolved dynamically at runtime.
/// The registry eliminates interface dispatch by using direct casts to concrete types.
/// </para>
/// <para>
/// Generated code pattern:
/// <code>
/// file static class MiddlewareInvokerRegistry
/// {
///     private static readonly FrozenDictionary&lt;Type, MiddlewareInvoker&gt; _invokers;
///
///     public static Task&lt;IMessageResult&gt; InvokeAsync(
///         IDispatchMiddleware middleware, ...) =&gt;
///         _invokers.TryGetValue(middleware.GetType(), out var invoker)
///             ? invoker(middleware, message, context, next, ct)
///             : middleware.InvokeAsync(message, context, next, ct);
/// }
/// </code>
/// </para>
/// </remarks>
[Generator]
public sealed class MiddlewareInvokerInterceptorGenerator : IIncrementalGenerator
{
	private const string DispatchMiddlewareInterfaceName = "IDispatchMiddleware";
	private const string DispatchMiddlewareInterfaceFullName = "Excalibur.Dispatch.Abstractions.IDispatchMiddleware";

	/// <summary>
	/// Initializes the middleware invoker generator with the given context.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all classes that implement IDispatchMiddleware
		var middlewareTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsMiddlewareCandidate(node),
				transform: static (context, _) => GetMiddlewareInfo(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!)
			.Collect();

		// Generate the invoker registry
		context.RegisterSourceOutput(middlewareTypes, GenerateInvokerRegistry);
	}

	/// <summary>
	/// Checks if a syntax node is a potential middleware class.
	/// </summary>
	private static bool IsMiddlewareCandidate(SyntaxNode node)
	{
		// Look for class declarations with base types
		if (node is not ClassDeclarationSyntax classDecl)
		{
			return false;
		}

		// Must have base list (implements interface or inherits)
		if (classDecl.BaseList == null)
		{
			return false;
		}

		// Must not be abstract
		if (classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword)))
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Extracts middleware information from a class declaration.
	/// </summary>
	private static MiddlewareInterceptorInfo? GetMiddlewareInfo(GeneratorSyntaxContext context)
	{
		var classDecl = (ClassDeclarationSyntax)context.Node;
		var semanticModel = context.SemanticModel;

		// Get the type symbol using pattern matching
		if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol typeSymbol)
		{
			return null;
		}

		// Skip abstract classes and generic type definitions
		if (typeSymbol.IsAbstract || typeSymbol.IsGenericType)
		{
			return null;
		}

		// Check if it implements IDispatchMiddleware
		var implementsMiddleware = typeSymbol.AllInterfaces.Any(i =>
			i.Name == DispatchMiddlewareInterfaceName ||
			i.ToDisplayString() == DispatchMiddlewareInterfaceFullName);

		if (!implementsMiddleware)
		{
			return null;
		}

		// Check if the type has a public InvokeAsync method (not just explicit interface implementation)
		// This is required because the generated code uses direct type cast and method call
		var hasPublicInvokeAsync = typeSymbol.GetMembers("InvokeAsync")
			.OfType<IMethodSymbol>()
			.Any(m => m.DeclaredAccessibility == Accessibility.Public &&
					  !m.IsStatic &&
					  m.Parameters.Length == 4);

		if (!hasPublicInvokeAsync)
		{
			return null;
		}

		// Check for common middleware attributes
		var hasAppliesToAttribute = typeSymbol.GetAttributes().Any(a =>
			a.AttributeClass is { Name: "AppliesToAttribute" or "AppliesTo" });

		var hasExcludeKindsAttribute = typeSymbol.GetAttributes().Any(a =>
			a.AttributeClass is { Name: "ExcludeKindsAttribute" or "ExcludeKinds" });

		// Check if Stage property is overridden
		var overridesStage = typeSymbol.GetMembers("Stage")
			.OfType<IPropertySymbol>()
			.Any(p => !p.IsAbstract);

		return new MiddlewareInterceptorInfo
		{
			MiddlewareType = typeSymbol,
			MiddlewareTypeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			MiddlewareTypeName = typeSymbol.Name,
			Namespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
			HasAppliesToAttribute = hasAppliesToAttribute,
			HasExcludeKindsAttribute = hasExcludeKindsAttribute,
			OverridesStage = overridesStage
		};
	}

	/// <summary>
	/// Generates the middleware invoker registry source code.
	/// </summary>
	private static void GenerateInvokerRegistry(
		SourceProductionContext context,
		ImmutableArray<MiddlewareInterceptorInfo> middlewareTypes)
	{
		// Deduplicate middleware types (same type might be discovered multiple times)
		var uniqueTypes = middlewareTypes
			.GroupBy(m => m.MiddlewareTypeFullName)
			.Select(g => g.First())
			.OrderBy(m => m.MiddlewareTypeFullName)
			.ToList();

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Discovered middleware types: {uniqueTypes.Count}");
		_ = sb.AppendLine("// PERF-10: Middleware invoker registry for eliminating interface dispatch");
		_ = sb.AppendLine();

		// Required pragmas and usings
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();

		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Frozen;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine("using System.Threading;");
		_ = sb.AppendLine("using System.Threading.Tasks;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions;");
		_ = sb.AppendLine();

		// Invoker registry class
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Generated middleware invoker registry that eliminates interface dispatch overhead.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    /// <remarks>");
		_ = sb.AppendLine("    /// PERF-10: Maps middleware types to typed invoker delegates, allowing direct method");
		_ = sb.AppendLine("    /// calls via cast rather than interface dispatch. Falls back to interface dispatch");
		_ = sb.AppendLine("    /// for unknown middleware types (plugins, tests, dynamic registration).");
		_ = sb.AppendLine("    /// </remarks>");
		_ = sb.AppendLine("    file static class MiddlewareInvokerRegistry");
		_ = sb.AppendLine("    {");

		// Delegate type definition
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Delegate type for typed middleware invocation.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private delegate ValueTask<IMessageResult> MiddlewareInvoker(");
		_ = sb.AppendLine("            IDispatchMiddleware middleware,");
		_ = sb.AppendLine("            IDispatchMessage message,");
		_ = sb.AppendLine("            IMessageContext context,");
		_ = sb.AppendLine("            DispatchRequestDelegate next,");
		_ = sb.AppendLine("            CancellationToken cancellationToken);");
		_ = sb.AppendLine();

		// FrozenDictionary field
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Frozen dictionary mapping middleware types to typed invokers.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static readonly FrozenDictionary<Type, MiddlewareInvoker> _invokers;");
		_ = sb.AppendLine();

		// Hot reload detection
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Cached hot reload detection result.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static readonly bool _isHotReloadEnabled;");
		_ = sb.AppendLine();

		// Static constructor
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Static constructor initializes the invoker registry.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        static MiddlewareInvokerRegistry()");
		_ = sb.AppendLine("        {");
		_ = sb.AppendLine("            _isHotReloadEnabled = IsHotReloadEnabled();");
		_ = sb.AppendLine();
		_ = sb.AppendLine("            var dict = new Dictionary<Type, MiddlewareInvoker>();");

		// Generate invoker for each middleware type
		foreach (var middleware in uniqueTypes)
		{
			_ = sb.AppendLine();
			_ = sb.AppendLine($"            // {middleware.MiddlewareTypeName}");
			_ = sb.AppendLine($"            dict[typeof({middleware.MiddlewareTypeFullName})] = static (m, msg, ctx, next, ct) =>");
			_ = sb.AppendLine($"                (({middleware.MiddlewareTypeFullName})m).InvokeAsync(msg, ctx, next, ct);");
		}

		_ = sb.AppendLine();
		_ = sb.AppendLine("            _invokers = dict.ToFrozenDictionary();");
		_ = sb.AppendLine("        }");
		_ = sb.AppendLine();

		// InvokeAsync method
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Invokes middleware using typed delegate when available, falling back to interface dispatch.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"middleware\">The middleware instance to invoke.</param>");
		_ = sb.AppendLine("        /// <param name=\"message\">The message being processed.</param>");
		_ = sb.AppendLine("        /// <param name=\"context\">The message context.</param>");
		_ = sb.AppendLine("        /// <param name=\"next\">The next delegate in the pipeline.</param>");
		_ = sb.AppendLine("        /// <param name=\"cancellationToken\">Cancellation token.</param>");
		_ = sb.AppendLine("        /// <returns>The message result from middleware execution.</returns>");
		_ = sb.AppendLine("        public static ValueTask<IMessageResult> InvokeAsync(");
		_ = sb.AppendLine("            IDispatchMiddleware middleware,");
		_ = sb.AppendLine("            IDispatchMessage message,");
		_ = sb.AppendLine("            IMessageContext context,");
		_ = sb.AppendLine("            DispatchRequestDelegate next,");
		_ = sb.AppendLine("            CancellationToken cancellationToken)");
		_ = sb.AppendLine("        {");
		_ = sb.AppendLine("            // Skip registry in hot reload mode for compatibility");
		_ = sb.AppendLine("            if (_isHotReloadEnabled)");
		_ = sb.AppendLine("            {");
		_ = sb.AppendLine("                return middleware.InvokeAsync(message, context, next, cancellationToken);");
		_ = sb.AppendLine("            }");
		_ = sb.AppendLine();
		_ = sb.AppendLine("            // Try optimized path via typed invoker");
		_ = sb.AppendLine("            if (_invokers.TryGetValue(middleware.GetType(), out var invoker))");
		_ = sb.AppendLine("            {");
		_ = sb.AppendLine("                return invoker(middleware, message, context, next, cancellationToken);");
		_ = sb.AppendLine("            }");
		_ = sb.AppendLine();
		_ = sb.AppendLine("            // Fallback to interface dispatch for unknown types");
		_ = sb.AppendLine("            return middleware.InvokeAsync(message, context, next, cancellationToken);");
		_ = sb.AppendLine("        }");
		_ = sb.AppendLine();

		// TryGetInvoker method for diagnostics
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Checks if a typed invoker exists for the given middleware type.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"middlewareType\">The middleware type to check.</param>");
		_ = sb.AppendLine("        /// <returns>True if a typed invoker exists; otherwise, false.</returns>");
		_ = sb.AppendLine("        public static bool HasTypedInvoker(Type middlewareType) =>");
		_ = sb.AppendLine("            _invokers.ContainsKey(middlewareType);");
		_ = sb.AppendLine();

		// Hot reload detection helper
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Detects if hot reload is enabled via environment variables.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static bool IsHotReloadEnabled()");
		_ = sb.AppendLine("        {");
		_ = sb.AppendLine("            // Check DOTNET_WATCH environment variable");
		_ = sb.AppendLine("            var dotnetWatch = Environment.GetEnvironmentVariable(\"DOTNET_WATCH\");");
		_ = sb.AppendLine("            if (string.Equals(dotnetWatch, \"1\", StringComparison.OrdinalIgnoreCase) ||");
		_ = sb.AppendLine("                string.Equals(dotnetWatch, \"true\", StringComparison.OrdinalIgnoreCase))");
		_ = sb.AppendLine("            {");
		_ = sb.AppendLine("                return true;");
		_ = sb.AppendLine("            }");
		_ = sb.AppendLine();
		_ = sb.AppendLine("            // Check DOTNET_MODIFIABLE_ASSEMBLIES for hot reload metadata");
		_ = sb.AppendLine("            var modifiableAssemblies = Environment.GetEnvironmentVariable(\"DOTNET_MODIFIABLE_ASSEMBLIES\");");
		_ = sb.AppendLine("            if (string.Equals(modifiableAssemblies, \"debug\", StringComparison.OrdinalIgnoreCase))");
		_ = sb.AppendLine("            {");
		_ = sb.AppendLine("                return true;");
		_ = sb.AppendLine("            }");
		_ = sb.AppendLine();
		_ = sb.AppendLine("            return false;");
		_ = sb.AppendLine("        }");
		_ = sb.AppendLine();

		// Count property for diagnostics
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the number of middleware types with typed invokers.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        public static int Count => _invokers.Count;");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");

		context.AddSource("MiddlewareInvokers.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}
}
