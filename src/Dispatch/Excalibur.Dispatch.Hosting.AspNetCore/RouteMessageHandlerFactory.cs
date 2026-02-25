// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Factory methods for creating route handler delegates that integrate with the Dispatch pipeline.
/// </summary>
public static class RouteMessageHandlerFactory
{
	/// <summary>
	/// Handler delegate for minimal APIs with separate request and message types (non-generic result).
	/// </summary>
	public static Delegate CreateMessageHandler<TRequest, TAction>(
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TRequest : class
		where TAction : class, IDispatchAction =>
		async (
			[AsParameters] TRequest request,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var action = actionFactory(request, httpContext);
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync(action, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};

	/// <summary>
	/// Simplified handler delegate when request directly serves as action (non-generic result).
	/// </summary>
	public static Delegate CreateMessageHandler<TAction>(
		Func<HttpContext, IMessageResult, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TAction : class, IDispatchAction =>
		async (
			[AsParameters] TAction action,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync(action, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};

	/// <summary>
	/// Handler delegate for minimal APIs with separate request/message and strongly-typed response.
	/// </summary>
	public static Delegate CreateMessageHandler<TRequest, TAction, TResponse>(
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult<TResponse>, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TRequest : class
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		async (
			[AsParameters] TRequest request,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var action = actionFactory(request, httpContext);
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync<TAction, TResponse>(action, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};

	/// <summary>
	/// Simplified handler delegate when request directly serves as action (strongly-typed response).
	/// </summary>
	public static Delegate CreateMessageHandler<TAction, TResponse>(
		Func<HttpContext, IMessageResult<TResponse>, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		async (
			[AsParameters] TAction action,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync<TAction, TResponse>(action, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};

	/// <summary>
	/// Handler delegate for minimal APIs that dispatches events to the Dispatch pipeline from a request DTO.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the event is created.</typeparam>
	/// <typeparam name="TEvent">The type of the event to dispatch, must implement <see cref="IDispatchEvent"/>.</typeparam>
	/// <param name="eventFactory">A function that creates the event from the request DTO and HTTP context.</param>
	/// <param name="responseFactory">A function that converts the dispatch result to an HTTP result.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <returns>A delegate that can be registered as a route handler.</returns>
	public static Delegate CreateEventHandler<TRequest, TEvent>(
		Func<TRequest, HttpContext, TEvent> eventFactory,
		Func<HttpContext, IMessageResult, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TRequest : class
		where TEvent : class, IDispatchEvent =>
		async (
			[AsParameters] TRequest request,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var evt = eventFactory(request, httpContext);
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync(evt, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};

	/// <summary>
	/// Simplified handler delegate for minimal APIs when the request directly serves as the event to dispatch.
	/// </summary>
	/// <typeparam name="TEvent">The type of the event to dispatch, must implement <see cref="IDispatchEvent"/>.</typeparam>
	/// <param name="responseFactory">A function that converts the dispatch result to an HTTP result.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <returns>A delegate that can be registered as a route handler.</returns>
	public static Delegate CreateEventHandler<TEvent>(
		Func<HttpContext, IMessageResult, IResult> responseFactory,
		Action<MessageContext>? customizeContext = null)
		where TEvent : class, IDispatchEvent =>
		async (
			[AsParameters] TEvent evt,
			[FromServices] IDispatcher dispatcher,
			CancellationToken cancellationToken,
			HttpContext httpContext) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			customizeContext?.Invoke(context);

			var dispatcherResult = await dispatcher
				.DispatchAsync(evt, context, cancellationToken)
				.ConfigureAwait(false);

			return responseFactory(httpContext, dispatcherResult);
		};
}

