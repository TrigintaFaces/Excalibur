// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="IDispatcher" /> that provide context-aware dispatch operations.
/// These methods automatically use the current ambient context or create a new one if none exists.
/// </summary>
public static class DispatcherContextExtensions
{
	private static readonly ConditionalWeakTable<IDispatcher, ContextFactoryHolder> ContextFactoryCache = new();

	/// <summary>
	/// Dispatches a message using the current ambient context or a new context if none exists.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher" /> is null.</exception>
	/// <remarks>
	/// When called from within a handler (where an ambient context exists), this method reuses the current
	/// context. For top-level dispatches, a new context is created. Use <see cref="DispatchChildAsync{TMessage}(IDispatcher, TMessage, CancellationToken)" />
	/// when dispatching from within a handler to properly propagate causation and correlation identifiers.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult> DispatchAsync<TMessage>(
		this IDispatcher dispatcher,
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		if (MessageContextHolder.Current is null &&
		    message is IDispatchAction localAction &&
		    dispatcher is IDirectLocalDispatcher directLocalDispatcher)
		{
			return DispatchUltraLocalAsync(directLocalDispatcher, localAction, cancellationToken);
		}

		var context = GetOrCreateContext(dispatcher);
		return dispatcher.DispatchAsync(message, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action and returns the response using the current ambient context or a new context if
	/// none exists.
	/// </summary>
	/// <typeparam name="TMessage">The type of action to dispatch.</typeparam>
	/// <typeparam name="TResponse">The type of response expected.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The action to dispatch.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result with response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher" /> is null.</exception>
	/// <remarks>
	/// When called from within a handler (where an ambient context exists), this method reuses the current
	/// context. For top-level dispatches, a new context is created. Use
	/// <see cref="DispatchChildAsync{TMessage,TResponse}" /> when dispatching from within a handler to properly
	/// propagate causation and correlation identifiers.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
		this IDispatcher dispatcher,
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		if (MessageContextHolder.Current is null &&
		    dispatcher is IDirectLocalDispatcher directLocalDispatcher)
		{
			return DispatchUltraLocalWithResponseAsync<TMessage, TResponse>(
				directLocalDispatcher,
				message,
				cancellationToken);
		}

		var context = GetOrCreateContext(dispatcher);
		return dispatcher.DispatchAsync<TMessage, TResponse>(message, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches a message using a child context derived from the current ambient context. Automatically
	/// propagates correlation, tenant, and other cross-cutting identifiers.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when there is no active ambient context.</exception>
	/// <remarks>
	/// This method creates a child context that:
	/// <list type="bullet">
	/// <item>
	/// <description>Copies CorrelationId, TenantId, UserId, SessionId, WorkflowId, TraceParent, Source</description>
	/// </item>
	/// <item>
	/// <description>Sets CausationId to the parent's MessageId (establishing causal chain)</description>
	/// </item>
	/// <item>
	/// <description>Generates a new MessageId for the child message</description>
	/// </item>
	/// </list>
	/// Use this method when dispatching messages from within a handler to maintain proper message lineage and
	/// distributed tracing.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult> DispatchChildAsync<TMessage>(
		this IDispatcher dispatcher,
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		var currentContext = MessageContextHolder.Current
		                     ?? throw new InvalidOperationException(
			                     Resources.DispatcherContextExtensions_ChildMessageRequiresContext);

		var childContext = currentContext.CreateChildContext();
		return dispatcher.DispatchAsync(message, childContext, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action and returns the response using a child context derived from the current ambient
	/// context. Automatically propagates correlation, tenant, and other cross-cutting identifiers.
	/// </summary>
	/// <typeparam name="TMessage">The type of action to dispatch.</typeparam>
	/// <typeparam name="TResponse">The type of response expected.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The action to dispatch.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result with response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when there is no active ambient context.</exception>
	/// <remarks>
	/// This method creates a child context that:
	/// <list type="bullet">
	/// <item>
	/// <description>Copies CorrelationId, TenantId, UserId, SessionId, WorkflowId, TraceParent, Source</description>
	/// </item>
	/// <item>
	/// <description>Sets CausationId to the parent's MessageId (establishing causal chain)</description>
	/// </item>
	/// <item>
	/// <description>Generates a new MessageId for the child message</description>
	/// </item>
	/// </list>
	/// Use this method when dispatching actions from within a handler to maintain proper message lineage and
	/// distributed tracing.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Dispatch selects AOT-safe handler invocation (HandlerInvokerAot) when dynamic code is not supported.")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult<TResponse>> DispatchChildAsync<TMessage, TResponse>(
		this IDispatcher dispatcher,
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		var currentContext = MessageContextHolder.Current
		                     ?? throw new InvalidOperationException(
			                     Resources.DispatcherContextExtensions_ChildActionRequiresContext);

		var childContext = currentContext.CreateChildContext();
		return dispatcher.DispatchAsync<TMessage, TResponse>(message, childContext, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action with an inferred response type. The compiler infers <typeparamref name="TResponse"/>
	/// from the <see cref="IDispatchAction{TResponse}"/> parameter, eliminating the need for explicit
	/// type arguments at the call site.
	/// </summary>
	/// <typeparam name="TResponse">The response type, inferred from the action's interface.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The action to dispatch. Must implement <see cref="IDispatchAction{TResponse}"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result with the typed response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// This overload enables clean call sites without explicit type parameters:
	/// <code>
	/// // Instead of: dispatcher.DispatchAsync&lt;CreateOrderCommand, Guid&gt;(command, ct)
	/// var result = await dispatcher.DispatchAsync(command, ct);
	/// </code>
	/// </para>
	/// <para>
	/// <b>Performance:</b> The first call per concrete message type incurs a one-time reflection
	/// cost to build a cached delegate. Subsequent calls use the cached delegate with near-zero
	/// overhead. When the <c>Excalibur.Dispatch.SourceGenerators</c> package is referenced, the
	/// source generator emits concrete typed overloads that shadow this method via C# overload
	/// resolution, providing zero-reflection dispatch with full AOT compatibility.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult<TResponse>> DispatchAsync<TResponse>(
		this IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		var invoker = TypedDispatchDelegateCache<TResponse>.GetDispatchDelegate(message.GetType());
		return invoker(dispatcher, message, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action with an inferred response type using an explicit message context.
	/// The compiler infers <typeparamref name="TResponse"/> from the <see cref="IDispatchAction{TResponse}"/>
	/// parameter.
	/// </summary>
	/// <typeparam name="TResponse">The response type, inferred from the action's interface.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The action to dispatch. Must implement <see cref="IDispatchAction{TResponse}"/>.</param>
	/// <param name="context">The message context for the dispatch operation.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result with the typed response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> or <paramref name="context"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// This overload enables clean call sites with explicit context without type parameters:
	/// <code>
	/// var result = await dispatcher.DispatchAsync(command, context, ct);
	/// </code>
	/// </para>
	/// <para>
	/// <b>Performance:</b> Same caching behavior as the context-free overload. See
	/// <see cref="DispatchAsync{TResponse}(IDispatcher, IDispatchAction{TResponse}, CancellationToken)"/>
	/// for details.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult<TResponse>> DispatchAsync<TResponse>(
		this IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		ArgumentNullException.ThrowIfNull(context);

		var invoker = TypedDispatchDelegateCache<TResponse>.GetDispatchWithContextDelegate(message.GetType());
		return invoker(dispatcher, message, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches an action with an inferred response type using a child context derived from the
	/// current ambient context. The compiler infers <typeparamref name="TResponse"/> from the
	/// <see cref="IDispatchAction{TResponse}"/> parameter.
	/// </summary>
	/// <typeparam name="TResponse">The response type, inferred from the action's interface.</typeparam>
	/// <param name="dispatcher">The dispatcher instance.</param>
	/// <param name="message">The action to dispatch. Must implement <see cref="IDispatchAction{TResponse}"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the dispatch result with the typed response.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcher"/> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when there is no active ambient context.</exception>
	/// <remarks>
	/// <para>
	/// This overload enables clean call sites for child dispatch without type parameters:
	/// <code>
	/// // Inside a handler — dispatches with proper causation chain
	/// var result = await dispatcher.DispatchChildAsync(subCommand, ct);
	/// </code>
	/// </para>
	/// <para>
	/// <b>Performance:</b> Same caching behavior as the context-free overload. See
	/// <see cref="DispatchAsync{TResponse}(IDispatcher, IDispatchAction{TResponse}, CancellationToken)"/>
	/// for details.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Source-generated typed overloads in TypedDispatchExtensions shadow this method via " +
		                "C# overload resolution when Excalibur.Dispatch.SourceGenerators is referenced (required for AOT).")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<IMessageResult<TResponse>> DispatchChildAsync<TResponse>(
		this IDispatcher dispatcher,
		IDispatchAction<TResponse> message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dispatcher);

		var invoker = TypedDispatchDelegateCache<TResponse>.GetDispatchChildDelegate(message.GetType());
		return invoker(dispatcher, message, cancellationToken);
	}

	/// <summary>
	/// Gets the current ambient context or creates a new one using the factory from the
	/// dispatcher's service provider, falling back to a new <see cref="MessageContext"/>.
	/// </summary>
	/// <param name="dispatcher">The dispatcher to get the service provider from.</param>
	/// <returns>The ambient context or a newly created context instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IMessageContext GetOrCreateContext(IDispatcher dispatcher)
	{
		return MessageContextHolder.Current ?? CreateContextCore(dispatcher);
	}

	/// <summary>
	/// Creates a new message context using the factory from the dispatcher's service provider,
	/// or falls back to a new MessageContext if no factory is available.
	/// </summary>
	/// <param name="dispatcher">The dispatcher to get the service provider from.</param>
	/// <returns>A new message context instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageContext CreateContextCore(IDispatcher dispatcher)
	{
		var factory = ContextFactoryCache.GetValue(
			dispatcher,
			static key => new ContextFactoryHolder(key.ServiceProvider?.GetService<IMessageContextFactory>())).Factory;
		return factory?.CreateContext() ?? new MessageContext();
	}

	[RequiresUnreferencedCode("Direct local dispatch uses reflection-based handler resolution.")]
	[RequiresDynamicCode("Direct local dispatch uses runtime code generation for handler invocation.")]
	private static async Task<IMessageResult> DispatchUltraLocalAsync(
		IDirectLocalDispatcher directLocalDispatcher,
		IDispatchAction action,
		CancellationToken cancellationToken)
	{
		try
		{
			await directLocalDispatcher.DispatchLocalAsync(action, cancellationToken).ConfigureAwait(false);
			return MessageResult.Success();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return CreateLocalFailureResult(ex, "Direct local dispatch failed");
		}
	}

	[RequiresUnreferencedCode("Direct local dispatch uses reflection-based handler resolution.")]
	[RequiresDynamicCode("Direct local dispatch uses runtime code generation for handler invocation.")]
	private static async Task<IMessageResult<TResponse>> DispatchUltraLocalWithResponseAsync<TMessage, TResponse>(
		IDirectLocalDispatcher directLocalDispatcher,
		TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchAction<TResponse>
	{
		try
		{
			var value = await directLocalDispatcher.DispatchLocalAsync<TMessage, TResponse>(message, cancellationToken)
				.ConfigureAwait(false);
			return new SimpleSuccessMessageResultOfT<TResponse>(value, cacheHit: false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return CreateLocalFailureResult<TResponse>(ex, "Direct local dispatch failed");
		}
	}

	private static IMessageResult CreateLocalFailureResult(Exception exception, string title)
	{
		var problem = new MessageProblemDetails
		{
			Type = "dispatch.handler_error",
			Title = title,
			Status = 500,
			Detail = exception.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return MessageResult.Failed(problem);
	}

	private static SimpleMessageResultOfT<TResponse> CreateLocalFailureResult<TResponse>(Exception exception, string title)
	{
		var problem = new MessageProblemDetails
		{
			Type = "dispatch.handler_error",
			Title = title,
			Status = 500,
			Detail = exception.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return new SimpleMessageResultOfT<TResponse>(
			value: default,
			succeeded: false,
			errorMessage: exception.Message,
			cacheHit: false,
			problemDetails: problem);
	}

	private sealed class ContextFactoryHolder(IMessageContextFactory? factory)
	{
		public IMessageContextFactory? Factory { get; } = factory;
	}
}
