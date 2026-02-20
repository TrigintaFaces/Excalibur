// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

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
	/// context. For top-level dispatches, a new context is created. Use <see cref="DispatchChildAsync{TMessage}" />
	/// when dispatching from within a handler to properly propagate causation and correlation identifiers.
	/// </remarks>
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

		var context = MessageContextHolder.Current ?? CreateContext(dispatcher);
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

		var context = MessageContextHolder.Current ?? CreateContext(dispatcher);
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
	/// Creates a new message context using the factory from the dispatcher's service provider,
	/// or falls back to a new MessageContext if no factory is available.
	/// </summary>
	/// <param name="dispatcher">The dispatcher to get the service provider from.</param>
	/// <returns>A new message context instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static IMessageContext CreateContext(IDispatcher dispatcher)
	{
		var factory = dispatcher.ServiceProvider?.GetService<IMessageContextFactory>();
		return factory?.CreateContext() ?? new MessageContext();
	}

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
			return MessageResult.Success<TResponse>(value);
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

	private static IMessageResult<TResponse> CreateLocalFailureResult<TResponse>(Exception exception, string title)
	{
		var problem = new MessageProblemDetails
		{
			Type = "dispatch.handler_error",
			Title = title,
			Status = 500,
			Detail = exception.Message,
			Instance = Guid.NewGuid().ToString(),
		};

		return MessageResult.Failed<TResponse>(exception.Message, problem);
	}
}
