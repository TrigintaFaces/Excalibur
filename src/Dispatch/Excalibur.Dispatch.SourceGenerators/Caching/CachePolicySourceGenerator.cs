// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates compile-time cache policy registration for types implementing ICacheable&lt;T&gt; or having CacheResultAttribute.
/// This enables AOT-compatible cache policy management by generating static switch expressions for cache information extraction.
/// </summary>
[Generator]
public sealed class CachePolicySourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the source generator by registering syntax providers to find cacheable types and cache policy types.
	/// </summary>
	/// <param name="context">The initialization context provided by the compiler.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all types that implement ICacheable<T> or have CacheResultAttribute
		var cacheableTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsCacheableCandidate(s),
				transform: static (ctx, _) => GetCacheableTypeInfo(ctx))
			.Where(static m => m is not null)
			.Collect();

		// Find all types that implement IResultCachePolicy<T>
		var cachePolicyTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsCachePolicyCandidate(s),
				transform: static (ctx, _) => GetCachePolicyTypeInfo(ctx))
			.Where(static m => m is not null)
			.Collect();

		// Generate the cache info registry
		context.RegisterSourceOutput(
			cacheableTypes.Combine(cachePolicyTypes),
			static (spc, source) => Execute(spc, source.Left!, source.Right!));
	}

	/// <summary>
	/// Determines if a syntax node is a candidate for cacheable type analysis.
	/// </summary>
	/// <param name="node">The syntax node to evaluate.</param>
	/// <returns>True if the node is a class, record, or struct declaration that could potentially implement ICacheable&lt;T&gt;.</returns>
	private static bool IsCacheableCandidate(SyntaxNode node) =>
		node is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax;

	/// <summary>
	/// Determines if a syntax node is a candidate for cache policy type analysis.
	/// </summary>
	/// <param name="node">The syntax node to evaluate.</param>
	/// <returns>True if the node is a class declaration with a base list that could potentially implement IResultCachePolicy&lt;T&gt;.</returns>
	private static bool IsCachePolicyCandidate(SyntaxNode node) => node is ClassDeclarationSyntax { BaseList: not null };

	/// <summary>
	/// Extracts cacheable type information from a syntax context by analyzing types that implement ICacheable&lt;T&gt; or have CacheResultAttribute.
	/// </summary>
	/// <param name="context">The generator syntax context containing the type symbol to analyze.</param>
	/// <returns>CacheableTypeInfo if the type is cacheable, otherwise null.</returns>
	private static CacheableTypeInfo? GetCacheableTypeInfo(GeneratorSyntaxContext context)
	{
		var typeSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
		if (typeSymbol is not INamedTypeSymbol namedType)
		{
			return null;
		}

		// Check for ICacheable<T> interface
		var cacheableInterface = namedType.AllInterfaces
			.FirstOrDefault(static i => i.IsGenericType &&
										i.ConstructedFrom.ToDisplayString() == "Excalibur.Dispatch.Caching.ICacheable<T>");

		// Check for CacheResultAttribute
		var hasCacheAttribute = namedType.GetAttributes()
			.Any(static a => a.AttributeClass?.ToDisplayString() == "Excalibur.Dispatch.Caching.CacheResultAttribute");

		if (cacheableInterface == null && !hasCacheAttribute)
		{
			return null;
		}

		return new CacheableTypeInfo
		{
			TypeSymbol = namedType,
			CacheableInterface = cacheableInterface,
			HasCacheAttribute = hasCacheAttribute
		};
	}

	/// <summary>
	/// Extracts cache policy type information from a syntax context by analyzing types that implement IResultCachePolicy&lt;T&gt;.
	/// </summary>
	/// <param name="context">The generator syntax context containing the type symbol to analyze.</param>
	/// <returns>CachePolicyTypeInfo if the type implements IResultCachePolicy&lt;T&gt;, otherwise null.</returns>
	private static CachePolicyTypeInfo? GetCachePolicyTypeInfo(GeneratorSyntaxContext context)
	{
		var typeSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
		if (typeSymbol is not INamedTypeSymbol namedType)
		{
			return null;
		}

		// Check if implements IResultCachePolicy<T>
		var policyInterface = namedType.AllInterfaces
			.FirstOrDefault(static i => i.IsGenericType &&
										i.ConstructedFrom.ToDisplayString() == "Excalibur.Dispatch.Caching.IResultCachePolicy<T>");

		if (policyInterface == null)
		{
			return null;
		}

		return new CachePolicyTypeInfo
		{
			TypeSymbol = namedType,
			PolicyInterface = policyInterface,
			MessageType = policyInterface.TypeArguments[0] as INamedTypeSymbol
		};
	}

	/// <summary>
	/// Executes the source code generation by combining cacheable types and policy types to generate the cache registry.
	/// </summary>
	/// <param name="context">The source production context for adding generated source code.</param>
	/// <param name="cacheableTypes">Collection of cacheable type information extracted from the compilation.</param>
	/// <param name="policyTypes">Collection of cache policy type information extracted from the compilation.</param>
	private static void Execute(SourceProductionContext context,
		ImmutableArray<CacheableTypeInfo> cacheableTypes,
		ImmutableArray<CachePolicyTypeInfo> policyTypes)
	{
		if (cacheableTypes.IsDefaultOrEmpty && policyTypes.IsDefaultOrEmpty)
		{
			return;
		}

		var source = GenerateCacheRegistry(cacheableTypes, policyTypes);
		context.AddSource("CacheInfoRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	/// <summary>
	/// Generates the cache registry source code that provides compile-time cache information extraction for AOT compatibility.
	/// </summary>
	/// <param name="cacheableTypes">Collection of types that implement ICacheable or have CacheResultAttribute.</param>
	/// <param name="policyTypes">Collection of types that implement IResultCachePolicy.</param>
	/// <returns>The generated C# source code for the cache registry class.</returns>
	private static string GenerateCacheRegistry(ImmutableArray<CacheableTypeInfo> cacheableTypes,
		ImmutableArray<CachePolicyTypeInfo> policyTypes)
	{
		var sb = new StringBuilder();

		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Caching;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Generated cache information registry for AOT-compatible caching.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("public static class CacheInfoRegistry");
		_ = sb.AppendLine("{");

		// Generate cacheable type extractors
		GenerateCacheableExtractors(sb, cacheableTypes);

		// Generate cache policy invokers
		GenerateCachePolicyInvokers(sb, policyTypes);

		// Generate result value extractors
		GenerateResultValueExtractors(sb, cacheableTypes);

		_ = sb.AppendLine("}");

		return sb.ToString();
	}

	/// <summary>
	/// Generates the cache extractor methods for types that implement ICacheable&lt;T&gt; interface.
	/// </summary>
	/// <param name="sb">The StringBuilder to append the generated code to.</param>
	/// <param name="cacheableTypes">Collection of cacheable type information to generate extractors for.</param>
	private static void GenerateCacheableExtractors(StringBuilder sb, ImmutableArray<CacheableTypeInfo> cacheableTypes)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets cache information for a message if it implements ICacheable.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static CacheableInfo? GetCacheableInfo(IDispatchMessage message)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return message switch");
		_ = sb.AppendLine(" {");

		foreach (var info in cacheableTypes.Where(static t => t.CacheableInterface != null))
		{
			var typeName = info.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var returnType = info.CacheableInterface.TypeArguments[0];

			_ = sb.AppendLine($" {typeName} msg => new CacheableInfo");
			_ = sb.AppendLine(" {");
			_ = sb.AppendLine(
				$" ShouldCache = (returnValue) => msg.ShouldCache(({returnType.ToDisplayString()})returnValue),");
			_ = sb.AppendLine(" ExpirationSeconds = msg.ExpirationSeconds,");
			_ = sb.AppendLine(" GetTags = () => msg.GetCacheTags() ?? Array.Empty<string>()");
			_ = sb.AppendLine(" },");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate attribute-based cache info extraction
		GenerateAttributeCacheableExtractors(sb, cacheableTypes);

		// Generate fast type checking methods
		GenerateCacheableTypeChecks(sb, cacheableTypes);
	}

	/// <summary>
	/// Generates cache extractor methods for types that have CacheResultAttribute applied.
	/// </summary>
	/// <param name="sb">The StringBuilder to append the generated code to.</param>
	/// <param name="cacheableTypes">Collection of cacheable type information to generate attribute extractors for.</param>
	private static void GenerateAttributeCacheableExtractors(StringBuilder sb, ImmutableArray<CacheableTypeInfo> cacheableTypes)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets cache attribute information for a message if it has CacheResultAttribute.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static CacheAttributeInfo? GetCacheAttributeInfo(IDispatchMessage message)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return message switch");
		_ = sb.AppendLine(" {");

		foreach (var info in cacheableTypes.Where(static t => t.HasCacheAttribute))
		{
			var typeName = info.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var cacheAttr = info.TypeSymbol.GetAttributes()
				.FirstOrDefault(static a => a.AttributeClass?.Name == "CacheResultAttribute");

			if (cacheAttr != null)
			{
				var expirationSeconds = GetAttributeValue(cacheAttr, "ExpirationSeconds", 60);
				var tags = GetAttributeArrayValue(cacheAttr, "Tags");
				var onlyIfSuccess = GetAttributeValue(cacheAttr, "OnlyIfSuccess", true);
				var ignoreNullResult = GetAttributeValue(cacheAttr, "IgnoreNullResult", true);

				_ = sb.AppendLine($" {typeName} _ => new CacheAttributeInfo");
				_ = sb.AppendLine(" {");
				_ = sb.AppendLine($" ExpirationSeconds = {expirationSeconds},");
				_ = sb.AppendLine($" Tags = new[] {{ {string.Join(", ", tags.Select(static t => $"\"{t}\""))} }},");
				_ = sb.AppendLine($" OnlyIfSuccess = {onlyIfSuccess.ToString().ToUpperInvariant()},");
				_ = sb.AppendLine($" IgnoreNullResult = {ignoreNullResult.ToString().ToUpperInvariant()}");
				_ = sb.AppendLine(" },");
			}
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	/// <summary>
	/// Generates fast type checking methods for cacheable types without using reflection.
	/// </summary>
	/// <param name="sb">The StringBuilder to append the generated code to.</param>
	/// <param name="cacheableTypes">Collection of cacheable type information to generate type checks for.</param>
	private static void GenerateCacheableTypeChecks(StringBuilder sb, ImmutableArray<CacheableTypeInfo> cacheableTypes)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Quickly checks if a message type implements ICacheable without reflection.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool IsCacheable(Type messageType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return messageType switch");
		_ = sb.AppendLine(" {");

		foreach (var info in cacheableTypes.Where(static t => t.CacheableInterface != null))
		{
			var typeName = info.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			_ = sb.AppendLine($" Type t when t == typeof({typeName}) => true,");
		}

		_ = sb.AppendLine(" _ => false");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Quickly checks if a message type has CacheResultAttribute without reflection.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool HasCacheAttribute(Type messageType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return messageType switch");
		_ = sb.AppendLine(" {");

		foreach (var info in cacheableTypes.Where(static t => t.HasCacheAttribute))
		{
			var typeName = info.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			_ = sb.AppendLine($" Type t when t == typeof({typeName}) => true,");
		}

		_ = sb.AppendLine(" _ => false");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	/// <summary>
	/// Extracts an integer value from a named argument of an attribute.
	/// </summary>
	/// <param name="attr">The attribute data to search.</param>
	/// <param name="name">The name of the attribute argument to find.</param>
	/// <param name="defaultValue">The default value to return if the argument is not found or is not an integer.</param>
	/// <returns>The integer value of the named argument, or the default value if not found.</returns>
	private static int GetAttributeValue(AttributeData attr, string name, int defaultValue)
	{
		foreach (var arg in attr.NamedArguments)
		{
			if (arg.Key == name && arg.Value.Value is int value)
			{
				return value;
			}
		}

		return defaultValue;
	}

	/// <summary>
	/// Extracts a boolean value from a named argument of an attribute.
	/// </summary>
	/// <param name="attr">The attribute data to search.</param>
	/// <param name="name">The name of the attribute argument to find.</param>
	/// <param name="defaultValue">The default value to return if the argument is not found or is not a boolean.</param>
	/// <returns>The boolean value of the named argument, or the default value if not found.</returns>
	private static bool GetAttributeValue(AttributeData attr, string name, bool defaultValue)
	{
		foreach (var arg in attr.NamedArguments)
		{
			if (arg.Key == name && arg.Value.Value is bool value)
			{
				return value;
			}
		}

		return defaultValue;
	}

	/// <summary>
	/// Extracts a string array value from a named argument of an attribute.
	/// </summary>
	/// <param name="attr">The attribute data to search.</param>
	/// <param name="name">The name of the attribute argument to find.</param>
	/// <returns>The string array value of the named argument, or an empty array if not found.</returns>
	private static string[] GetAttributeArrayValue(AttributeData attr, string name)
	{
		foreach (var arg in attr.NamedArguments)
		{
			if (arg.Key == name && arg.Value.Values != null)
			{
				return [.. arg.Value.Values.Select(static v => v.Value?.ToString() ?? "")];
			}
		}

		return [];
	}

	/// <summary>
	/// Generates source code for cache policy invoker methods in the compiled output.
	/// </summary>
	/// <param name="sb">The StringBuilder to append the generated source code to.</param>
	/// <param name="policyTypes">The collection of cache policy type information to generate invokers for.</param>
	private static void GenerateCachePolicyInvokers(StringBuilder sb, ImmutableArray<CachePolicyTypeInfo> policyTypes)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Invokes the appropriate cache policy for a message type.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool? InvokeCachePolicy(object policy, IDispatchMessage message, object? result)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return (policy, message) switch");
		_ = sb.AppendLine(" {");

		foreach (var info in policyTypes.Where(static p => p.MessageType != null))
		{
			var policyType = info.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var messageType = info.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			_ = sb.AppendLine($" ({policyType} p, {messageType} m) => p.ShouldCache(m, result),");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	/// <summary>
	/// Generates result value extractor methods for different message result types.
	/// </summary>
	/// <param name="sb">The StringBuilder to append the generated code to.</param>
	/// <param name="cacheableTypes">Collection of cacheable type information to generate result extractors for.</param>
	private static void GenerateResultValueExtractors(StringBuilder sb, ImmutableArray<CacheableTypeInfo> cacheableTypes)
	{
		// Get unique return types from cacheable interfaces
		var returnTypes = cacheableTypes
			.Where(static t => t.CacheableInterface != null)
			.Select(static t => t.CacheableInterface.TypeArguments[0])
			.Distinct(SymbolEqualityComparer.Default)
			.Cast<ITypeSymbol>()
			.ToList();

		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Extracts the return value from a message result.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static object? ExtractReturnValue(IMessageResult result)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return result switch");
		_ = sb.AppendLine(" {");

		foreach (var returnType in returnTypes)
		{
			var typeName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			_ = sb.AppendLine($" Excalibur.Dispatch.Abstractions.MessageResult<{typeName}> typed => typed.ReturnValue,");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate the CacheableInfo class
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Holds cacheable information for a message.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public sealed class CacheableInfo");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" public required Func<object?, bool> ShouldCache { get; init; }");
		_ = sb.AppendLine(" public required int ExpirationSeconds { get; init; }");
		_ = sb.AppendLine(" public required Func<string[]> GetTags { get; init; }");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate the CacheAttributeInfo class
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Holds cache attribute information for a message.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public sealed class CacheAttributeInfo");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" public required int ExpirationSeconds { get; init; }");
		_ = sb.AppendLine(" public required string[] Tags { get; init; }");
		_ = sb.AppendLine(" public required bool OnlyIfSuccess { get; init; }");
		_ = sb.AppendLine(" public required bool IgnoreNullResult { get; init; }");
		_ = sb.AppendLine(" }");
	}

	private sealed class CacheableTypeInfo
	{
		public INamedTypeSymbol TypeSymbol { get; set; } = null!;
		public INamedTypeSymbol? CacheableInterface { get; set; }
		public bool HasCacheAttribute { get; set; }
	}

	private sealed class CachePolicyTypeInfo
	{
		public INamedTypeSymbol TypeSymbol { get; set; } = null!;
		public INamedTypeSymbol PolicyInterface { get; set; } = null!;
		public INamedTypeSymbol? MessageType { get; set; }
	}
}

