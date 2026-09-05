// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch;

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
	/// When called from within a handler (where an ambient context exists), this method dispatches a
	/// <b>child</b> by default — a fresh <see cref="IMessageContext.MessageId"/> with
	/// <see cref="IMessageContext.CausationId"/> set to the parent's message id, propagating correlation,
	/// identity, and routing — mirroring <c>Activity</c>/OpenTelemetry <c>StartActivity</c>. For top-level
	/// dispatches (no ambient context) a fresh root context is created. To deliberately reuse the same
	/// context instead of childing, pass it explicitly to the
	/// <see cref="IDispatcher.DispatchAsync{TMessage}(TMessage, IMessageContext, CancellationToken)"/> overload.
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

		var current = MessageContextHolder.Current;
		if (current is not null)
		{
			// A child of the ambient context was not rented from the factory, so there is nothing to return.
			return dispatcher.DispatchAsync(message, current.CreateChildContext(), cancellationToken);
		}

		var factory = GetContextFactory(dispatcher);
		if (factory is null)
		{
			return dispatcher.DispatchAsync(message, new MessageContext(), cancellationToken);
		}

		var rented = factory.CreateContext();
		var dispatchTask = dispatcher.DispatchAsync(message, rented, cancellationToken);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			factory.Return(rented);
			return dispatchTask;
		}

		return AwaitAndReturnContextAsync(dispatchTask, factory, rented);
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
	/// When called from within a handler (where an ambient context exists), this method dispatches a
	/// <b>child</b> by default — a fresh <see cref="IMessageContext.MessageId"/> with
	/// <see cref="IMessageContext.CausationId"/> set to the parent's message id, propagating correlation,
	/// identity, and routing — mirroring <c>Activity</c>/OpenTelemetry <c>StartActivity</c>. For top-level
	/// dispatches (no ambient context) a fresh root context is created. To deliberately reuse the same
	/// context instead of childing, pass it explicitly to the
	/// <see cref="IDispatcher.DispatchAsync{TMessage, TResponse}(TMessage, IMessageContext, CancellationToken)"/> overload.
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

		var current = MessageContextHolder.Current;
		if (current is not null)
		{
			// A child of the ambient context was not rented from the factory, so there is nothing to return.
			return dispatcher.DispatchAsync<TMessage, TResponse>(message, current.CreateChildContext(), cancellationToken);
		}

		var factory = GetContextFactory(dispatcher);
		if (factory is null)
		{
			return dispatcher.DispatchAsync<TMessage, TResponse>(message, new MessageContext(), cancellationToken);
		}

		var rented = factory.CreateContext();
		var dispatchTask = dispatcher.DispatchAsync<TMessage, TResponse>(message, rented, cancellationToken);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			factory.Return(rented);
			return dispatchTask;
		}

		return AwaitAndReturnContextAsync(dispatchTask, factory, rented);
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
	/// Returns a <b>child</b> of the current ambient context (fresh message id, causation linked to the
	/// parent, cross-cutting identifiers propagated) when one exists, or a fresh root context created via
	/// the dispatcher's <see cref="IMessageContextFactory"/> (falling back to a new
	/// <see cref="MessageContext"/>) when there is no ambient context.
	/// </summary>
	/// <param name="dispatcher">The dispatcher to get the service provider from.</param>
	/// <returns>A child of the ambient context, or a newly created root context.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static IMessageContext GetOrCreateChildContext(IDispatcher dispatcher)
	{
		var current = MessageContextHolder.Current;
		return current is null ? CreateContextCore(dispatcher) : current.CreateChildContext();
	}

	/// <summary>
	/// Creates a new message context using the factory from the dispatcher's service provider,
	/// or falls back to a new MessageContext if no factory is available.
	/// </summary>
	/// <param name="dispatcher">The dispatcher to get the service provider from.</param>
	/// <returns>A new message context instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageContext CreateContextCore(IDispatcher dispatcher) =>
		GetContextFactory(dispatcher)?.CreateContext() ?? new MessageContext();

	/// <summary>
	/// Resolves (and caches per dispatcher) the <see cref="IMessageContextFactory"/> a rented root context
	/// must be returned to.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageContextFactory? GetContextFactory(IDispatcher dispatcher) =>
		ContextFactoryCache.GetValue(
			dispatcher,
			static key => new ContextFactoryHolder(key.ServiceProvider?.GetService<IMessageContextFactory>())).Factory;

	/// <summary>
	/// Returns the rented context only after the dispatch has fully completed, so no continuation can still
	/// be holding it when the factory resets and recycles it.
	/// </summary>
	private static async Task<IMessageResult> AwaitAndReturnContextAsync(
		Task<IMessageResult> dispatchTask,
		IMessageContextFactory? factory,
		IMessageContext context)
	{
		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			factory?.Return(context);
		}
	}

	/// <inheritdoc cref="AwaitAndReturnContextAsync(Task{IMessageResult}, IMessageContextFactory, IMessageContext)"/>
	private static async Task<IMessageResult<TResponse>> AwaitAndReturnContextAsync<TResponse>(
		Task<IMessageResult<TResponse>> dispatchTask,
		IMessageContextFactory? factory,
		IMessageContext context)
	{
		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			factory?.Return(context);
		}
	}

	private sealed class ContextFactoryHolder(IMessageContextFactory? factory)
	{
		public IMessageContextFactory? Factory { get; } = factory;
	}


}
