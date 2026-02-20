// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates compile-time cache info extraction for types implementing ICacheable&lt;T&gt;.
/// This eliminates reflection when determining cache keys, tags, and expiration for dispatch actions.
/// </summary>
[Generator]
public sealed class CacheInfoSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the cache info source generator with the given context.
	/// Sets up syntax providers to find types implementing ICacheable&lt;T&gt; and types with CacheAttribute,
	/// then registers source output generation for compile-time cache information extraction.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all types that implement ICacheable<T> in current compilation
		var cacheableTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsCandidateType(node),
				transform: static (context, _) => GetCacheableTypeInfo(context))
			.Where(static typeInfo => typeInfo != null)
			.Select(static (typeInfo, _) => typeInfo!);

		// Find all types with CacheAttribute
		var attributeTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsCandidateType(node),
				transform: static (context, _) => GetCacheAttributeTypeInfo(context))
			.Where(static typeInfo => typeInfo != null)
			.Select(static (typeInfo, _) => typeInfo!);

		// Combine and generate
		var combinedData = cacheableTypes.Collect()
			.Combine(attributeTypes.Collect());

		context.RegisterSourceOutput(combinedData, GenerateCacheInfoExtractors);
	}

	private static bool IsCandidateType(SyntaxNode node) =>
		node is ClassDeclarationSyntax { BaseList: not null } or
			RecordDeclarationSyntax { BaseList: not null } or
			StructDeclarationSyntax { BaseList: not null };

	private static CacheableTypeInfo? GetCacheableTypeInfo(GeneratorSyntaxContext context)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract ||
			typeSymbol.IsGenericType)
		{
			return null;
		}

		// Check if type implements ICacheable<T>
		INamedTypeSymbol? cacheableInterface = null;
		ITypeSymbol? resultType = null;

		foreach (var @interface in typeSymbol
					 .AllInterfaces
					 .Where(static @interface => @interface.IsGenericType &&
												 @interface.ConstructedFrom?.Name == "ICacheable" &&
												 @interface.TypeArguments.Length == 1))
		{
			cacheableInterface = @interface;
			resultType = @interface.TypeArguments[0];
			break;
		}

		if (cacheableInterface == null)
		{
			return null;
		}

		// Check if type also implements IDispatchAction
		var implementsDispatchAction = typeSymbol.AllInterfaces
			.Any(static i => i.Name == "IDispatchAction");

		if (!implementsDispatchAction)
		{
			return null;
		}

		return new CacheableTypeInfo
		{
			FullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SimpleName = typeSymbol.Name,
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			ResultTypeName = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			InterfaceTypeName = cacheableInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			AssemblyName = typeSymbol.ContainingAssembly.Name
		};
	}

	private static CacheAttributeTypeInfo? GetCacheAttributeTypeInfo(GeneratorSyntaxContext context)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract ||
			typeSymbol.IsGenericType)
		{
			return null;
		}

		// Check if type has CacheAttribute
		var cacheAttribute = typeSymbol.GetAttributes()
			.FirstOrDefault(static a => a.AttributeClass?.Name == "CacheAttribute");

		if (cacheAttribute == null)
		{
			return null;
		}

		// Extract attribute data
		var expirationSeconds = 60;
		var tags = new List<string>();
		string? policyTypeName = null;

		foreach (var arg in cacheAttribute
					 .ConstructorArguments
					 .Where(static arg => arg.Type?.Name == "Int32"))
		{
			expirationSeconds = (int)arg.Value!;
		}

		foreach (var namedArg in cacheAttribute.NamedArguments)
		{
			switch (namedArg.Key)
			{
				case "ExpirationSeconds":
					expirationSeconds = (int)namedArg.Value.Value!;
					break;
				case "Tags":
					if (namedArg.Value.Values.Length > 0)
					{
						tags = [.. namedArg.Value.Values.Select(static v => v.Value?.ToString() ?? "")];
					}

					break;
				case "PolicyType":
					policyTypeName = namedArg.Value.Value?.ToString();
					break;
				default:
					break;
			}
		}

		return new CacheAttributeTypeInfo
		{
			FullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SimpleName = typeSymbol.Name,
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			ExpirationSeconds = expirationSeconds,
			Tags = [.. tags],
			PolicyTypeName = policyTypeName,
			AssemblyName = typeSymbol.ContainingAssembly.Name
		};
	}

	private static void GenerateCacheInfoExtractors(SourceProductionContext context,
		(ImmutableArray<CacheableTypeInfo> cacheableTypes, ImmutableArray<CacheAttributeTypeInfo> attributeTypes) data)
	{
		// Generate the unified extractor class
		var source = GenerateUnifiedExtractorClass(data.cacheableTypes, data.attributeTypes);
		context.AddSource("CacheInfoExtractor.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	private static string GenerateUnifiedExtractorClass(ImmutableArray<CacheableTypeInfo> cacheableTypes,
		ImmutableArray<CacheAttributeTypeInfo> attributeTypes)
	{
		var sb = new StringBuilder();

		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Total ICacheable types: {cacheableTypes.Length}");
		_ = sb.AppendLine($"// Total CacheAttribute types: {attributeTypes.Length}");
		_ = sb.AppendLine();

		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Caching;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Caching;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Compile-time generated cache info extractor for AOT compatibility.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("[UnconditionalSuppressMessage(\"AOT\", \"IL2026:RequiresUnreferencedCode\")]");
		_ = sb.AppendLine("[UnconditionalSuppressMessage(\"AOT\", \"IL3050:RequiresDynamicCode\")]");
		_ = sb.AppendLine("internal static partial class CacheInfoExtractor");
		_ = sb.AppendLine("{");

		// Generate IsCacheable method
		GenerateIsCacheableMethod(sb, cacheableTypes);

		// Generate HasCacheAttribute method
		GenerateHasCacheAttributeMethod(sb, attributeTypes);

		// Generate ExtractReturnValue method
		GenerateExtractReturnValueMethod(sb, cacheableTypes);

		// Generate GetCacheableInfo method
		GenerateGetCacheableInfoMethod(sb, cacheableTypes);

		// Generate GetCacheAttributeInfo method
		GenerateGetCacheAttributeInfoMethod(sb, attributeTypes);

		// Generate InvokeCachePolicy method
		GenerateInvokeCachePolicyMethod(sb, attributeTypes);

		_ = sb.AppendLine("}");

		return sb.ToString();
	}

	private static void GenerateIsCacheableMethod(StringBuilder sb, ImmutableArray<CacheableTypeInfo> types)
	{
		_ = sb.AppendLine("\tpublic static bool IsCacheable(Type messageType)");
		_ = sb.AppendLine("\t{");
		_ = sb.AppendLine("\t\tif (messageType == null) return false;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("\t\treturn messageType.FullName switch");
		_ = sb.AppendLine("\t\t{");

		foreach (var type in types)
		{
			_ = sb.AppendLine($"\t\t\t\"{type.FullName.Replace("global::", "")}\" => true,");
		}

		_ = sb.AppendLine("\t\t\t_ => false");
		_ = sb.AppendLine("\t\t};");
		_ = sb.AppendLine("\t}");
		_ = sb.AppendLine();
	}

	private static void GenerateHasCacheAttributeMethod(StringBuilder sb, ImmutableArray<CacheAttributeTypeInfo> types)
	{
		_ = sb.AppendLine(" public static bool HasCacheAttribute(Type messageType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" if (messageType == null) return false;");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" return messageType.FullName switch");
		_ = sb.AppendLine(" {");

		foreach (var type in types)
		{
			_ = sb.AppendLine($" \"{type.FullName.Replace("global::", "")}\" => true,");
		}

		_ = sb.AppendLine(" _ => false");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateExtractReturnValueMethod(StringBuilder sb, ImmutableArray<CacheableTypeInfo> types)
	{
		_ = sb.AppendLine(" public static object? ExtractReturnValue(object messageResult)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" if (messageResult == null) return null;");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" return messageResult switch");
		_ = sb.AppendLine(" {");

		foreach (var type in types)
		{
			_ = sb.AppendLine($" Excalibur.Dispatch.Messaging.MessageResult<{type.ResultTypeName}> typedResult => typedResult.Result,");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateGetCacheableInfoMethod(StringBuilder sb, ImmutableArray<CacheableTypeInfo> types)
	{
		_ = sb.AppendLine(" public static CacheableInfo? GetCacheableInfo(object message)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" if (message == null) return null;");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" return message switch");
		_ = sb.AppendLine(" {");

		foreach (var type in types)
		{
			var varName = type.SimpleName.ToUpperInvariant();
			_ = sb.AppendLine($" {type.FullName} {varName} => new CacheableInfo");
			_ = sb.AppendLine(" {");
			_ = sb.AppendLine($" Interface = typeof({type.InterfaceTypeName}),");
			_ = sb.AppendLine($" ShouldCache = (msg, result) => (({type.FullName})msg).ShouldCache(result),");
			_ = sb.AppendLine($" GetExpirationSeconds = msg => (({type.FullName})msg).ExpirationSeconds,");
			_ = sb.AppendLine($" GetTags = msg => (({type.FullName})msg).GetCacheTags(),");
			_ = sb.AppendLine($" GetCacheKey = msg => (({type.FullName})msg).GetCacheKey(),");
			_ = sb.AppendLine($" ResultType = typeof({type.ResultTypeName})");
			_ = sb.AppendLine(" },");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateGetCacheAttributeInfoMethod(StringBuilder sb, ImmutableArray<CacheAttributeTypeInfo> types)
	{
		_ = sb.AppendLine(" public static CacheAttributeInfo? GetCacheAttributeInfo(object message)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" if (message == null) return null;");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" return message switch");
		_ = sb.AppendLine(" {");

		foreach (var type in types)
		{
			var varName = type.SimpleName.ToUpperInvariant();
			_ = sb.AppendLine($" {type.FullName} {varName} => new CacheAttributeInfo");
			_ = sb.AppendLine(" {");
			_ = sb.AppendLine($" ExpirationSeconds = {type.ExpirationSeconds},");
			_ = sb.Append(" Tags = new[] { ");
			if (type.Tags.Length > 0)
			{
				_ = sb.Append(string.Join(", ", type.Tags.Select(static t => $"\"{t}\"")));
			}

			_ = sb.AppendLine(" },");
			if (!string.IsNullOrEmpty(type.PolicyTypeName))
			{
				_ = sb.AppendLine($" PolicyType = typeof({type.PolicyTypeName}),");
			}
			else
			{
				_ = sb.AppendLine(" PolicyType = null,");
			}

			_ = sb.AppendLine($" GetCacheKey = msg => (({type.FullName})msg).GetCacheKey()");
			_ = sb.AppendLine(" },");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateInvokeCachePolicyMethod(StringBuilder sb, ImmutableArray<CacheAttributeTypeInfo> types)
	{
		_ = sb.AppendLine(" public static object InvokeCachePolicy(object policyInstance, object message, object? result)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" if (policyInstance == null || message == null) return true;");
		_ = sb.AppendLine();

		// Group by policy type
		var policyGroups = types
			.Where(static t => !string.IsNullOrEmpty(t.PolicyTypeName))
			.GroupBy(static t => t.PolicyTypeName)
			.ToList();

		if (policyGroups.Count > 0)
		{
			_ = sb.AppendLine(" return (policyInstance, message) switch");
			_ = sb.AppendLine(" {");

			foreach (var group in policyGroups)
			{
				foreach (var type in group)
				{
					_ = sb.AppendLine($" ({group.Key} policy, {type.FullName} msg) => policy.ShouldCache(msg, result),");
				}
			}

			_ = sb.AppendLine(" _ => true");
			_ = sb.AppendLine(" };");
		}
		else
		{
			_ = sb.AppendLine(" return true;");
		}

		_ = sb.AppendLine(" }");
	}

	private sealed class CacheableTypeInfo
	{
		public string FullName { get; set; } = string.Empty;
		public string SimpleName { get; set; } = string.Empty;
		public string Namespace { get; set; } = string.Empty;
		public string ResultTypeName { get; set; } = string.Empty;
		public string InterfaceTypeName { get; set; } = string.Empty;
		public string AssemblyName { get; set; } = string.Empty;
	}

	private sealed class CacheAttributeTypeInfo
	{
		public string FullName { get; set; } = string.Empty;
		public string SimpleName { get; set; } = string.Empty;
		public string Namespace { get; set; } = string.Empty;
		public int ExpirationSeconds { get; set; }
		public string[] Tags { get; set; } = [];
		public string? PolicyTypeName { get; set; }
		public string AssemblyName { get; set; } = string.Empty;
	}
}
