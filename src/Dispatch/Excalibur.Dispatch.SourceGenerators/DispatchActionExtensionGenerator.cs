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
/// Source generator that creates typed extension methods for <see cref="IDispatcher"/> dispatch
/// operations, enabling <c>TResponse</c> type inference without explicit type arguments.
/// </summary>
/// <remarks>
/// <para>
/// For each concrete (non-abstract, non-generic) type implementing <c>IDispatchAction&lt;TResponse&gt;</c>
/// in the consumer's compilation, this generator emits extension methods with the concrete message
/// type as the parameter. C# overload resolution prefers these generated overloads over the
/// reflection-backed fallback in <c>DispatcherContextExtensions</c> because the concrete parameter
/// type is more specific than <c>IDispatchAction&lt;TResponse&gt;</c>.
/// </para>
/// <para>
/// Generated methods are thin <c>[AggressiveInlining]</c> forwarders to the existing
/// <c>DispatchAsync&lt;TMessage, TResponse&gt;</c> extension methods, preserving full <c>TMessage</c>
/// compile-time type information for the dispatcher's ultra-local and direct-local fast paths.
/// </para>
/// </remarks>
[Generator]
public sealed class DispatchActionExtensionGenerator : IIncrementalGenerator
{
	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all types implementing IDispatchAction<T>
		var actionTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsActionCandidate(node),
				transform: static (ctx, _) => GetActionInfo(ctx))
			.Where(static info => info is not null)
			.Select(static (info, _) => info!);

		// Combine with compilation to get assembly name for unique class naming
		var combined = actionTypes.Collect().Combine(context.CompilationProvider);
		context.RegisterSourceOutput(combined, static (spc, source) =>
			GenerateTypedExtensions(spc, source.Left, source.Right.AssemblyName));
	}

	private static bool IsActionCandidate(SyntaxNode node) =>
		node is TypeDeclarationSyntax
		{
			BaseList: not null
		} typeDecl &&
		!typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
		typeDecl.TypeParameterList is null; // Skip open generic types

	private static ActionInfo? GetActionInfo(GeneratorSyntaxContext context)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol)
		{
			return null;
		}

		// Skip abstract, static, and open generic types
		if (typeSymbol.IsAbstract || typeSymbol.IsStatic ||
			typeSymbol.TypeParameters.Length > 0)
		{
			return null;
		}

		// Find IDispatchAction<TResponse> in implemented interfaces
		foreach (var @interface in typeSymbol.AllInterfaces)
		{
			if (!@interface.IsGenericType || @interface.TypeArguments.Length != 1)
			{
				continue;
			}

			if (@interface.Name != "IDispatchAction" ||
				@interface.ContainingNamespace?.ToDisplayString() != "Excalibur.Dispatch")
			{
				continue;
			}

			var responseType = @interface.TypeArguments[0];

			// Skip if response type is an open generic parameter
			if (responseType.TypeKind == TypeKind.TypeParameter)
			{
				continue;
			}

			return new ActionInfo(
				MessageTypeFullName: typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				MessageTypeName: GetSafeIdentifier(typeSymbol),
				ResponseTypeFullName: responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				IsValueType: typeSymbol.IsValueType);
		}

		return null;
	}

	/// <summary>
	/// Generates a file-scoped, deterministic safe identifier from the type symbol
	/// to avoid naming collisions for types with the same <see cref="INamedTypeSymbol.Name"/>
	/// in different namespaces.
	/// </summary>
	private static string GetSafeIdentifier(INamedTypeSymbol symbol)
	{
		// Use full metadata name with dots/plus replaced for method-name safety
		return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			.Replace("global::", "")
			.Replace(".", "_")
			.Replace("+", "_")
			.Replace("<", "_")
			.Replace(">", "_")
			.Replace(",", "_")
			.Replace(" ", "");
	}

	private static void GenerateTypedExtensions(
		SourceProductionContext context,
		ImmutableArray<ActionInfo> actions,
		string? assemblyName)
	{
		if (actions.IsDefaultOrEmpty)
		{
			return;
		}

		// Deduplicate — a type can appear multiple times from different syntax nodes
		var seen = new HashSet<string>();
		var uniqueActions = new List<ActionInfo>();

		foreach (var action in actions)
		{
			if (seen.Add(action.MessageTypeFullName))
			{
				uniqueActions.Add(action);
			}
		}

		if (uniqueActions.Count == 0)
		{
			return;
		}

		// Assembly-unique class name prevents CS0433 when multiple assemblies
		// using this generator are referenced by the same consuming project.
		var safeAssemblyName = SanitizeAssemblyName(assemblyName ?? "Unknown");
		var className = $"TypedDispatchExtensions_{safeAssemblyName}";

		var sb = new StringBuilder(4096);

		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine("// Typed dispatch extension methods generated by Excalibur.Dispatch.SourceGenerators.");
		_ = sb.AppendLine("// These overloads enable TResponse inference without explicit type arguments.");
		_ = sb.AppendLine("// C# overload resolution prefers these concrete-typed methods over the");
		_ = sb.AppendLine("// IDispatchAction<TResponse>-typed fallback in DispatcherContextExtensions.");
		_ = sb.AppendLine($"// Action types discovered: {uniqueActions.Count}");
		_ = sb.AppendLine($"// Assembly: {assemblyName}");
		_ = sb.AppendLine();
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();
		_ = sb.AppendLine("using System.Runtime.CompilerServices;");
		_ = sb.AppendLine("using System.Threading;");
		_ = sb.AppendLine("using System.Threading.Tasks;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Dispatch;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Source-generated typed dispatch extension methods that enable <c>TResponse</c>");
		_ = sb.AppendLine("/// inference from <see cref=\"IDispatchAction{TResponse}\"/> message types.");
		_ = sb.AppendLine("/// These methods are AOT-safe and shadow the reflection-backed fallback overloads");
		_ = sb.AppendLine("/// via C# overload resolution (concrete parameter > interface parameter).");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("[System.CodeDom.Compiler.GeneratedCode(\"Excalibur.Dispatch.SourceGenerators\", \"1.0\")]");
		_ = sb.AppendLine($"public static class {className}");
		_ = sb.AppendLine("{");

		foreach (var action in uniqueActions)
		{
			EmitDispatchAsync(sb, action);
			EmitDispatchAsyncWithContext(sb, action);
			EmitDispatchChildAsync(sb, action);
		}

		_ = sb.AppendLine("}");

		context.AddSource("TypedDispatchExtensions.g.cs",
			SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static void EmitDispatchAsync(StringBuilder sb, ActionInfo action)
	{
		_ = sb.AppendLine();
		_ = sb.AppendLine($"\t/// <summary>");
		_ = sb.AppendLine($"\t/// Dispatches a <see cref=\"{EscapeXml(action.MessageTypeFullName)}\"/> with compile-time type inference.");
		_ = sb.AppendLine($"\t/// </summary>");
		_ = sb.AppendLine($"\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine($"\tpublic static Task<IMessageResult<{action.ResponseTypeFullName}>> DispatchAsync(");
		_ = sb.AppendLine($"\t\tthis IDispatcher dispatcher,");
		_ = sb.AppendLine($"\t\t{action.MessageTypeFullName} message,");
		_ = sb.AppendLine($"\t\tCancellationToken cancellationToken)");
		_ = sb.AppendLine($"\t{{");
		_ = sb.AppendLine($"\t\treturn DispatcherContextExtensions.DispatchAsync<{action.MessageTypeFullName}, {action.ResponseTypeFullName}>(dispatcher, message, cancellationToken);");
		_ = sb.AppendLine($"\t}}");
	}

	private static void EmitDispatchAsyncWithContext(StringBuilder sb, ActionInfo action)
	{
		_ = sb.AppendLine();
		_ = sb.AppendLine($"\t/// <summary>");
		_ = sb.AppendLine($"\t/// Dispatches a <see cref=\"{EscapeXml(action.MessageTypeFullName)}\"/> with explicit context and compile-time type inference.");
		_ = sb.AppendLine($"\t/// </summary>");
		_ = sb.AppendLine($"\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine($"\tpublic static Task<IMessageResult<{action.ResponseTypeFullName}>> DispatchAsync(");
		_ = sb.AppendLine($"\t\tthis IDispatcher dispatcher,");
		_ = sb.AppendLine($"\t\t{action.MessageTypeFullName} message,");
		_ = sb.AppendLine($"\t\tIMessageContext context,");
		_ = sb.AppendLine($"\t\tCancellationToken cancellationToken)");
		_ = sb.AppendLine($"\t{{");
		_ = sb.AppendLine($"\t\treturn dispatcher.DispatchAsync<{action.MessageTypeFullName}, {action.ResponseTypeFullName}>(message, context, cancellationToken);");
		_ = sb.AppendLine($"\t}}");
	}

	private static void EmitDispatchChildAsync(StringBuilder sb, ActionInfo action)
	{
		_ = sb.AppendLine();
		_ = sb.AppendLine($"\t/// <summary>");
		_ = sb.AppendLine($"\t/// Dispatches a <see cref=\"{EscapeXml(action.MessageTypeFullName)}\"/> as a child dispatch with compile-time type inference.");
		_ = sb.AppendLine($"\t/// </summary>");
		_ = sb.AppendLine($"\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine($"\tpublic static Task<IMessageResult<{action.ResponseTypeFullName}>> DispatchChildAsync(");
		_ = sb.AppendLine($"\t\tthis IDispatcher dispatcher,");
		_ = sb.AppendLine($"\t\t{action.MessageTypeFullName} message,");
		_ = sb.AppendLine($"\t\tCancellationToken cancellationToken)");
		_ = sb.AppendLine($"\t{{");
		_ = sb.AppendLine($"\t\treturn DispatcherContextExtensions.DispatchChildAsync<{action.MessageTypeFullName}, {action.ResponseTypeFullName}>(dispatcher, message, cancellationToken);");
		_ = sb.AppendLine($"\t}}");
	}

	private static string EscapeXml(string text) =>
		text.Replace("<", "&lt;").Replace(">", "&gt;");

	private static string SanitizeAssemblyName(string name)
	{
		var sb = new StringBuilder(name.Length);
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (char.IsLetterOrDigit(c) || c == '_')
			{
				_ = sb.Append(c);
			}
			else
			{
				_ = sb.Append('_');
			}
		}

		if (sb.Length == 0 || char.IsDigit(sb[0]))
		{
			_ = sb.Insert(0, '_');
		}

		return sb.ToString();
	}

	private sealed record ActionInfo(
		string MessageTypeFullName,
		string MessageTypeName,
		string ResponseTypeFullName,
		bool IsValueType);
}
