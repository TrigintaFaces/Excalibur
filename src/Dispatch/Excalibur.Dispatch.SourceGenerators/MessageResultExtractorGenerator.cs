// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates compile-time message result extractors for handler return types.
/// Generates AOT-compatible result factory methods to avoid MakeGenericMethod/reflection in FinalDispatchHandler.
/// </summary>
[Generator]
public sealed class MessageResultExtractorGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the message result extractor generator with the given context.
	/// Sets up syntax providers to find handler methods with specific return types
	/// and registers source output generation for compile-time result extraction.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all handler methods that return specific types
		var resultTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsHandlerCandidate(s),
				transform: static (ctx, _) => GetResultTypeFromHandler(ctx))
			.Where(static m => m is not null)
			.Collect();

		// Find all global::Excalibur.Dispatch.Abstractions.MessageResult<T> usages in the codebase
		var messageResultTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsMessageResultCandidate(s),
				transform: static (ctx, _) => GetMessageResultType(ctx))
			.Where(static m => m is not null)
			.Collect();

		// Find result types from IDispatchAction<T> and IActionHandler<TAction, TResult> implementations
		var actionResultTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsDispatchActionCandidate(s),
				transform: static (ctx, _) => GetResultTypeFromDispatchAction(ctx))
			.Where(static m => m is not null)
			.Collect();

		// Combine all three sources and generate the result factory registry
		context.RegisterSourceOutput(
			resultTypes.Combine(messageResultTypes).Combine(actionResultTypes),
			static (spc, source) => Execute(spc, source.Left.Left!, source.Left.Right!, source.Right!));
	}

	private static bool IsHandlerCandidate(SyntaxNode node) =>
		node is MethodDeclarationSyntax method &&
		(method.Identifier.Text == "HandleAsync" ||
		 method.Identifier.Text == "ExecuteAsync" ||
		 method.Identifier.Text.EndsWith("Async", StringComparison.Ordinal));

	private static bool IsDispatchActionCandidate(SyntaxNode node) =>
		node is ClassDeclarationSyntax or RecordDeclarationSyntax;

	private static ResultTypeInfo? GetResultTypeFromDispatchAction(GeneratorSyntaxContext context)
	{
		var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
		if (symbol is not INamedTypeSymbol typeSymbol)
		{
			return null;
		}

		// Look for IDispatchAction<T> or IActionHandler<TAction, TResult> in implemented interfaces
		foreach (var iface in typeSymbol.AllInterfaces)
		{
			if (!iface.IsGenericType)
			{
				continue;
			}

			// IDispatchAction<T> - T is the result type
			if (iface.Name == "IDispatchAction" &&
				iface.TypeArguments.Length == 1)
			{
				var resultType = iface.TypeArguments[0];

				// Skip type parameters
				if (resultType.TypeKind == TypeKind.TypeParameter)
				{
					continue;
				}

				// Unwrap Nullable<T> for nullable value types
				if (resultType is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
				{
					resultType = nullable.TypeArguments[0];
				}

				return new ResultTypeInfo(
					resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
					resultType.Name,
					GetSimpleTypeName(resultType));
			}

			// IActionHandler<TAction, TResult> - TResult is the result type
			if (iface.Name == "IActionHandler" &&
				iface.TypeArguments.Length == 2)
			{
				var resultType = iface.TypeArguments[1];

				// Skip type parameters
				if (resultType.TypeKind == TypeKind.TypeParameter)
				{
					continue;
				}

				// Unwrap Nullable<T> for nullable value types
				if (resultType is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
				{
					resultType = nullable.TypeArguments[0];
				}

				return new ResultTypeInfo(
					resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
					resultType.Name,
					GetSimpleTypeName(resultType));
			}
		}

		return null;
	}

	private static bool IsMessageResultCandidate(SyntaxNode node)
	{
		if (node is GenericNameSyntax generic)
		{
			return generic.Identifier.Text is "MessageResult" or
				"IMessageResult";
		}

		return false;
	}

	private static ResultTypeInfo? GetResultTypeFromHandler(GeneratorSyntaxContext context)
	{
		var methodSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
		if (methodSymbol is not IMethodSymbol method)
		{
			return null;
		}

		// Check if this is a handler method
		var returnType = method.ReturnType;
		if (returnType is not INamedTypeSymbol namedReturnType)
		{
			return null;
		}

		// Check for Task<IMessageResult<T>> or Task<global::Excalibur.Dispatch.Abstractions.MessageResult<T>>
		if (namedReturnType.Name == "Task" &&
			namedReturnType.TypeArguments.Length == 1 &&
			namedReturnType.TypeArguments[0] is INamedTypeSymbol innerNamed &&
			innerNamed.IsGenericType &&
			(innerNamed.Name == "IMessageResult" || innerNamed.Name == "MessageResult") &&
			innerNamed.TypeArguments.Length == 1)
		{
			var resultType = innerNamed.TypeArguments[0];

			// Skip type parameters (open generics like T, TResponse, TValue)
			if (resultType.TypeKind == TypeKind.TypeParameter)
			{
				return null;
			}

			return new ResultTypeInfo(
				resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				resultType.Name,
				GetSimpleTypeName(resultType));
		}

		return null;
	}

	private static ResultTypeInfo? GetMessageResultType(GeneratorSyntaxContext context)
	{
		if (context.Node is not GenericNameSyntax genericName)
		{
			return null;
		}

		var typeInfo = context.SemanticModel.GetTypeInfo(genericName);
		if (typeInfo.Type is not INamedTypeSymbol namedType)
		{
			return null;
		}

		if (namedType.Name is "MessageResult" or "IMessageResult" &&
			namedType.TypeArguments.Length == 1)
		{
			var resultType = namedType.TypeArguments[0];

			// Skip type parameters (open generics like T, TResponse, TValue)
			if (resultType.TypeKind == TypeKind.TypeParameter)
			{
				return null;
			}

			return new ResultTypeInfo(
				resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				resultType.Name,
				GetSimpleTypeName(resultType));
		}

		return null;
	}

	private static string GetSimpleTypeName(ITypeSymbol type)
	{
		// Convert type name to a valid C# identifier
		var name = type.Name;

		// Handle nullable types
		if (type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } named)
		{
			name = GetSimpleTypeName(named.TypeArguments[0]) + "Nullable";
		}

		// Handle generic types
		if (type is INamedTypeSymbol { IsGenericType: true } genericType)
		{
			var baseName = genericType.Name;
			var genericArgNames = string.Join("_", genericType.TypeArguments.Select(GetSimpleTypeName));
			name = $"{baseName}_{genericArgNames}";
		}

		// Remove invalid characters
		return name.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "");
	}

	private static void Execute(SourceProductionContext context,
		ImmutableArray<ResultTypeInfo> handlerTypes,
		ImmutableArray<ResultTypeInfo> messageResultTypes,
		ImmutableArray<ResultTypeInfo> actionResultTypes)
	{
		// Combine and deduplicate all result types from all three discovery paths
		var allTypes = handlerTypes.Concat(messageResultTypes).Concat(actionResultTypes)
			.Where(static t => t != null)
			.GroupBy(static t => t.FullTypeName)
			.Select(static g => g.First())
			.OrderBy(static t => t.FullTypeName)
			.ToList();

		// Always emit the registry (even with 0 types) so GetFactory/ExtractReturnValue compile
		var source = GenerateResultFactoryRegistry(allTypes);
		context.AddSource("ResultFactoryRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	private static string GenerateResultFactoryRegistry(List<ResultTypeInfo> resultTypes)
	{
		var sb = new StringBuilder();

		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions.Routing;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions.Validation;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Delivery.Handlers;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Generated result factory registry for AOT-compatible message result creation.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("public static partial class ResultFactoryRegistry");
		_ = sb.AppendLine("{");

		// Generate factory dictionary
		_ = sb.AppendLine(
										" private static readonly Dictionary<Type, Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>> _factories = new()");
		_ = sb.AppendLine(" {");

		foreach (var type in resultTypes)
		{
			_ = sb.AppendLine($" [typeof({type.FullTypeName})] = Create{type.SimpleTypeName}Result,");
		}

		_ = sb.AppendLine(" };");
		_ = sb.AppendLine();

		// Generate GetFactory method
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets a factory for creating MessageResult instances of the specified type.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(
												" internal static Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>? GetFactory(Type resultType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _factories.TryGetValue(resultType, out var factory) ? factory : null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate factory methods
		foreach (var type in resultTypes)
		{
			GenerateFactoryMethod(sb, type);
		}

		// Generate ExtractReturnValue method
		GenerateExtractReturnValueMethod(sb, resultTypes);

		_ = sb.AppendLine("}");

		return sb.ToString();
	}

	private static void GenerateFactoryMethod(StringBuilder sb, ResultTypeInfo type)
	{
		_ = sb.AppendLine($" private static IMessageResult Create{type.SimpleTypeName}Result(");
		_ = sb.AppendLine(" object? returnValue,");
		_ = sb.AppendLine(" RoutingDecision? routingResult,");
		_ = sb.AppendLine(" object? validationResult,");
		_ = sb.AppendLine(" IAuthorizationResult? authorizationResult,");
		_ = sb.AppendLine(" bool cacheHit)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine($" return global::Excalibur.Dispatch.Abstractions.MessageResult.Success<{type.FullTypeName}>(");
		_ = sb.AppendLine($" ({type.FullTypeName})returnValue!,");
		_ = sb.AppendLine(" routingResult,");
		_ = sb.AppendLine(" validationResult,");
		_ = sb.AppendLine(" authorizationResult,");
		_ = sb.AppendLine(" cacheHit);");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateExtractReturnValueMethod(StringBuilder sb, List<ResultTypeInfo> resultTypes)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Extracts the return value from a message result without reflection.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static object? ExtractReturnValue(IMessageResult? result)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return result switch");
		_ = sb.AppendLine(" {");

		foreach (var type in resultTypes)
		{
			_ = sb.AppendLine($" global::Excalibur.Dispatch.Abstractions.IMessageResult<{type.FullTypeName}> typed => typed.ReturnValue,");
		}

		_ = sb.AppendLine(" _ => null");
		_ = sb.AppendLine(" };");
		_ = sb.AppendLine(" }");
	}

	private sealed record ResultTypeInfo(string FullTypeName, string TypeName, string SimpleTypeName);
}
