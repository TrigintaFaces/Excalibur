// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates zero-allocation handler invokers.
/// </summary>
[Generator]
public sealed class ZeroAllocHandlerInvokerGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the zero-allocation handler invoker source generator with the given context.
	/// Sets up syntax providers to find handler classes and registers source output generation
	/// for compile-time handler invoker creation to eliminate runtime reflection.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Create an incremental value provider for the compilation
		var compilationProvider = context.CompilationProvider;

		// Create an incremental value provider for class declarations that might implement handler interfaces
		var classDeclarationsProvider = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsHandlerCandidateClass(node),
				transform: static (ctx, _) => GetHandlerClassSymbol(ctx))
			.Where(static result => result is not null)
			.Select(static (result, _) => result!);

		// Combine the compilation and class declarations
		var combined = compilationProvider.Combine(classDeclarationsProvider.Collect());

		// Register the source generator
		context.RegisterSourceOutput(combined, static (ctx, source) => Execute(ctx, source.Left, source.Right));
	}

	private static bool IsHandlerCandidateClass(SyntaxNode node) =>
		node is ClassDeclarationSyntax { BaseList: not null } classDeclaration
		&& !classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword);

	private static INamedTypeSymbol? GetHandlerClassSymbol(GeneratorSyntaxContext context)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		return ModelExtensions.GetDeclaredSymbol(context.SemanticModel, classDeclaration) as INamedTypeSymbol;
	}

	private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<INamedTypeSymbol> classSymbols)
	{
		var handlers = new List<HandlerInfo>();

		// Get handler interfaces
		var handlerInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IDispatchHandler`1");
		var handlerWithResultInterface = compilation.GetTypeByMetadataName("Excalibur.Dispatch.Abstractions.IDispatchHandler`2");

		if (handlerInterface == null && handlerWithResultInterface == null)
		{
			return;
		}

		// Process all handler classes
		foreach (var symbol in classSymbols)
		{
			if (symbol == null || symbol.IsAbstract)
			{
				continue;
			}

			var handlerInfo = AnalyzeHandler(symbol, handlerInterface, handlerWithResultInterface);
			if (handlerInfo != null && handlerInfo.Interfaces.Count != 0)
			{
				handlers.Add(handlerInfo);
			}
		}

		if (handlers.Count != 0)
		{
			GenerateZeroAllocationRegistry(context, handlers);
		}
	}

	private static HandlerInfo? AnalyzeHandler(
		INamedTypeSymbol handlerType,
		INamedTypeSymbol? handlerInterface,
		INamedTypeSymbol? handlerWithResultInterface)
	{
		var info = new HandlerInfo
		{
			HandlerType = handlerType,
			FullName = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SimpleName = handlerType.Name
		};

		// Find all implemented handler interfaces
		foreach (var @interface in handlerType.AllInterfaces)
		{
			if (@interface.IsGenericType)
			{
				var unboundInterface = @interface.ConstructedFrom;

				if (SymbolEqualityComparer.Default.Equals(unboundInterface, handlerInterface))
				{
					// IDispatchHandler<TMessage> - no result
					var messageType = @interface.TypeArguments[0];
					info.Interfaces.Add(new HandlerInterfaceInfo
					{
						InterfaceName = @interface.ToDisplayString(),
						MessageType = messageType,
						HasResult = false,
						ResultType = null
					});
				}
				else if (SymbolEqualityComparer.Default.Equals(unboundInterface, handlerWithResultInterface))
				{
					// IDispatchHandler<TMessage, TResult> - has result
					var messageType = @interface.TypeArguments[0];
					var resultType = @interface.TypeArguments[1];
					info.Interfaces.Add(new HandlerInterfaceInfo
					{
						InterfaceName = @interface.ToDisplayString(),
						MessageType = messageType,
						HasResult = true,
						ResultType = resultType
					});
				}
			}
		}

		return info.Interfaces.Count != 0 ? info : null;
	}

	private static void GenerateZeroAllocationRegistry(SourceProductionContext context, List<HandlerInfo> handlers)
	{
		var sb = new StringBuilder();

		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine("using System.Runtime.CompilerServices;");
		_ = sb.AppendLine("using System.Threading;");
		_ = sb.AppendLine("using System.Threading.Tasks;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Abstractions;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Delivery.Handlers;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Delivery.Handlers;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Generated zero-allocation handler invoker registry.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("public static partial class ZeroAllocationHandlerRegistry");
		_ = sb.AppendLine("{");

		// Generate type mapping dictionaries
		_ = sb.AppendLine(" private static readonly Dictionary<(Type, Type), Type> _resultTypes = new()");
		_ = sb.AppendLine(" {");

		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces.Where(static i => i.HasResult))
			{
				var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var resultTypeName = @interface.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

				_ = sb.AppendLine($" {{ (typeof({handlerTypeName}), typeof({messageTypeName})), typeof({resultTypeName}) }},");
			}
		}

		_ = sb.AppendLine(" };");
		_ = sb.AppendLine();

		// Generate static invoker methods for each handler/message combination
		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces)
			{
				if (@interface.HasResult)
				{
					GenerateResultInvokerMethod(sb, handler, @interface);
				}
				else
				{
					GenerateVoidInvokerMethod(sb, handler, @interface);
				}
			}
		}

		// Generate generic accessor methods
		GenerateGenericAccessorMethods(sb, handlers);

		// Generate dynamic invocation method
		GenerateDynamicInvocationMethod(sb, handlers);

		_ = sb.AppendLine("}");

		context.AddSource("ZeroAllocationHandlerRegistry.g.cs", sb.ToString());
	}

	private static void GenerateResultInvokerMethod(StringBuilder sb, HandlerInfo handler, HandlerInterfaceInfo @interface)
	{
		var methodName = $"InvokeAsync_{handler.SimpleName}_{@interface.MessageType.Name}";
		var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var resultTypeName = @interface.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		_ = sb.AppendLine(" [MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine($" private static async ValueTask<HandlerResult<{resultTypeName}>> {methodName}(");
		_ = sb.AppendLine($" {handlerTypeName} handler,");
		_ = sb.AppendLine($" {messageTypeName} message,");
		_ = sb.AppendLine(" IMessageContext context,");
		_ = sb.AppendLine(" CancellationToken cancellationToken)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" try");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" var result = await handler.HandleAsync(message, context, cancellationToken).ConfigureAwait(false);");
		_ = sb.AppendLine($" return new HandlerResult<{resultTypeName}>(result);");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine(" catch (Exception ex)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine($" return new HandlerResult<{resultTypeName}>(ex);");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateVoidInvokerMethod(StringBuilder sb, HandlerInfo handler, HandlerInterfaceInfo @interface)
	{
		var methodName = $"InvokeVoidAsync_{handler.SimpleName}_{@interface.MessageType.Name}";
		var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		_ = sb.AppendLine(" [MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine($" private static async ValueTask {methodName}(");
		_ = sb.AppendLine($" {handlerTypeName} handler,");
		_ = sb.AppendLine($" {messageTypeName} message,");
		_ = sb.AppendLine(" IMessageContext context,");
		_ = sb.AppendLine(" CancellationToken cancellationToken)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" await handler.HandleAsync(message, context, cancellationToken).ConfigureAwait(false);");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateGenericAccessorMethods(StringBuilder sb, List<HandlerInfo> handlers)
	{
		// Generate GetInvoker<THandler, TMessage, TResult>
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets a zero-allocation invoker for handlers with results.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" [MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine(
			" public static Func<THandler, TMessage, CancellationToken, ValueTask<HandlerResult<TResult>>>? GetInvoker<THandler, TMessage, TResult>()");
		_ = sb.AppendLine(" where TMessage : IDispatchMessage");
		_ = sb.AppendLine(" {");

		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces.Where(static i => i.HasResult))
			{
				var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var resultTypeName = @interface.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var methodName = $"InvokeAsync_{handler.SimpleName}_{@interface.MessageType.Name}";

				_ = sb.AppendLine($" if (typeof(THandler) == typeof({handlerTypeName}) &&");
				_ = sb.AppendLine($" typeof(TMessage) == typeof({messageTypeName}) &&");
				_ = sb.AppendLine($" typeof(TResult) == typeof({resultTypeName}))");
				_ = sb.AppendLine(" {");
				_ = sb.AppendLine(
					$" return (Func<THandler, TMessage, CancellationToken, ValueTask<HandlerResult<TResult>>>)(object){methodName};");
				_ = sb.AppendLine(" }");
				_ = sb.AppendLine();
			}
		}

		_ = sb.AppendLine(" return null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate GetVoidInvoker<THandler, TMessage>
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets a zero-allocation invoker for handlers without results.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" [MethodImpl(MethodImplOptions.AggressiveInlining)]");
		_ = sb.AppendLine(" public static Func<THandler, TMessage, CancellationToken, ValueTask>? GetVoidInvoker<THandler, TMessage>()");
		_ = sb.AppendLine(" where TMessage : IDispatchMessage");
		_ = sb.AppendLine(" {");

		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces.Where(static i => !i.HasResult))
			{
				var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var methodName = $"InvokeVoidAsync_{handler.SimpleName}_{@interface.MessageType.Name}";

				_ = sb.AppendLine($" if (typeof(THandler) == typeof({handlerTypeName}) &&");
				_ = sb.AppendLine($" typeof(TMessage) == typeof({messageTypeName}))");
				_ = sb.AppendLine(" {");
				_ = sb.AppendLine($" return (Func<THandler, TMessage, CancellationToken, ValueTask>)(object){methodName};");
				_ = sb.AppendLine(" }");
				_ = sb.AppendLine();
			}
		}

		_ = sb.AppendLine(" return null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate HasZeroAllocationInvoker
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Checks if a zero-allocation invoker exists for the given types.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool HasZeroAllocationInvoker(Type handlerType, Type messageType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _resultTypes.ContainsKey((handlerType, messageType));");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateDynamicInvocationMethod(StringBuilder sb, List<HandlerInfo> handlers)
	{
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Tries to invoke a handler dynamically without boxing if possible.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool TryInvokeDynamic(");
		_ = sb.AppendLine(" object handler,");
		_ = sb.AppendLine(" IDispatchMessage message,");
		_ = sb.AppendLine(" IMessageContext context,");
		_ = sb.AppendLine(" CancellationToken cancellationToken,");
		_ = sb.AppendLine(" out Task<object?> resultTask)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" var handlerType = handler.GetType();");
		_ = sb.AppendLine(" var messageType = message.GetType();");
		_ = sb.AppendLine();

		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces)
			{
				var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

				_ = sb.AppendLine($" if (handlerType == typeof({handlerTypeName}) && messageType == typeof({messageTypeName}))");
				_ = sb.AppendLine(" {");

				if (@interface.HasResult)
				{
					_ = $"InvokeAsync_{handler.SimpleName}_{@interface.MessageType.Name}";

					_ = @interface.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

					_ = sb.AppendLine($" var typedHandler = ({handlerTypeName})handler;");
					_ = sb.AppendLine($" var typedMessage = ({messageTypeName})message;");
					_ = sb.AppendLine(
						$" resultTask = InvokeDynamicWithResult_{handler.SimpleName}_{@interface.MessageType.Name}(typedHandler, typedMessage, context, cancellationToken);");
					_ = sb.AppendLine(" return true;");
				}
				else
				{
					_ = $"InvokeVoidAsync_{handler.SimpleName}_{@interface.MessageType.Name}";

					_ = sb.AppendLine($" var typedHandler = ({handlerTypeName})handler;");
					_ = sb.AppendLine($" var typedMessage = ({messageTypeName})message;");
					_ = sb.AppendLine(
						$" resultTask = InvokeDynamicVoid_{handler.SimpleName}_{@interface.MessageType.Name}(typedHandler, typedMessage, context, cancellationToken);");
					_ = sb.AppendLine(" return true;");
				}

				_ = sb.AppendLine(" }");
				_ = sb.AppendLine();
			}
		}

		_ = sb.AppendLine(" resultTask = null!;");
		_ = sb.AppendLine(" return false;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// Generate helper methods for dynamic invocation
		foreach (var handler in handlers)
		{
			foreach (var @interface in handler.Interfaces)
			{
				if (@interface.HasResult)
				{
					GenerateDynamicResultHelper(sb, handler, @interface);
				}
				else
				{
					GenerateDynamicVoidHelper(sb, handler, @interface);
				}
			}
		}
	}

	private static void GenerateDynamicResultHelper(StringBuilder sb, HandlerInfo handler, HandlerInterfaceInfo @interface)
	{
		var methodName = $"InvokeDynamicWithResult_{handler.SimpleName}_{@interface.MessageType.Name}";
		var invokerMethod = $"InvokeAsync_{handler.SimpleName}_{@interface.MessageType.Name}";
		var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		_ = @interface.ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		_ = sb.AppendLine($" private static async Task<object?> {methodName}(");
		_ = sb.AppendLine($" {handlerTypeName} handler,");
		_ = sb.AppendLine($" {messageTypeName} message,");
		_ = sb.AppendLine(" IMessageContext context,");
		_ = sb.AppendLine(" CancellationToken cancellationToken)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine($" var result = await {invokerMethod}(handler, message, context, cancellationToken).ConfigureAwait(false);");
		_ = sb.AppendLine(" return result.IsFaulted ? throw result.Exception! : (object?)result.Value;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private static void GenerateDynamicVoidHelper(StringBuilder sb, HandlerInfo handler, HandlerInterfaceInfo @interface)
	{
		var methodName = $"InvokeDynamicVoid_{handler.SimpleName}_{@interface.MessageType.Name}";
		var invokerMethod = $"InvokeVoidAsync_{handler.SimpleName}_{@interface.MessageType.Name}";
		var handlerTypeName = handler.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var messageTypeName = @interface.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		_ = sb.AppendLine($" private static async Task<object?> {methodName}(");
		_ = sb.AppendLine($" {handlerTypeName} handler,");
		_ = sb.AppendLine($" {messageTypeName} message,");
		_ = sb.AppendLine(" IMessageContext context,");
		_ = sb.AppendLine(" CancellationToken cancellationToken)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine($" await {invokerMethod}(handler, message, context, cancellationToken).ConfigureAwait(false);");
		_ = sb.AppendLine(" return null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();
	}

	private sealed class HandlerInfo
	{
		public INamedTypeSymbol HandlerType { get; set; } = null!;
		public string FullName { get; set; } = string.Empty;
		public string SimpleName { get; set; } = string.Empty;
		public List<HandlerInterfaceInfo> Interfaces { get; set; } = [];
	}

	private sealed class HandlerInterfaceInfo
	{
		public string InterfaceName { get; set; } = string.Empty;
		public ITypeSymbol MessageType { get; set; } = null!;
		public bool HasResult { get; set; }
		public ITypeSymbol? ResultType { get; set; }
	}
}

