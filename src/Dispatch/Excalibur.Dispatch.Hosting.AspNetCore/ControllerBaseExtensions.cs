// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Extensions for ApiController integration.
/// </summary>
public static class ControllerBaseExtensions
{
	/// <summary>
	/// Dispatches a message to the Dispatch pipeline from an MVC controller.
	/// </summary>
	/// <typeparam name="TMessage">The type of the message to dispatch.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static Task<IMessageResult> DispatchMessageAsync<TMessage>(
		this ControllerBase controller,
		TMessage message,
		CancellationToken cancellationToken, Action<MessageContext>? customizeContext = null)
		where TMessage : class, IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(message);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		return dispatcher.DispatchAsync(message, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches a message with a strongly-typed response to the Dispatch pipeline from an MVC controller.
	/// </summary>
	/// <typeparam name="TMessage">The type of the message to dispatch.</typeparam>
	/// <typeparam name="TResponse">The type of the response value.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="message">The message to dispatch.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static Task<IMessageResult<TResponse>> DispatchMessageAsync<TMessage, TResponse>(
		this ControllerBase controller,
		TMessage message,
		CancellationToken cancellationToken, Action<MessageContext>? customizeContext = null)
		where TMessage : class, IDispatchAction<TResponse>
		where TResponse : class
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(message);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		return dispatcher.DispatchAsync<TMessage, TResponse>(message, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches a message created by a factory to the Dispatch pipeline and returns an HTTP action result.
	/// </summary>
	/// <typeparam name="TMessage">The type of the message to dispatch.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="messageFactory">A function that creates the message from the HTTP context.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="resultFactory">Optional function to convert the dispatch result to an action result. If null, uses default conversion.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static async Task<IActionResult> DispatchMessageAsync<TMessage>(
		this ControllerBase controller,
		Func<HttpContext, TMessage> messageFactory,
		CancellationToken cancellationToken,
		Action<MessageContext>? customizeContext = null,
		Func<ControllerBase, IMessageResult, IActionResult>? resultFactory = null)
		where TMessage : class, IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(messageFactory);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var message = messageFactory(controller.HttpContext);
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		var dispatcherResult = await dispatcher
			.DispatchAsync(message, context, cancellationToken)
			.ConfigureAwait(false);

		return resultFactory != null
			? resultFactory(controller, dispatcherResult)
			: controller.ToHttpActionResult(dispatcherResult);
	}

	/// <summary>
	/// Dispatches a message with a strongly-typed response created by a factory to the Dispatch pipeline and returns an HTTP action result.
	/// </summary>
	/// <typeparam name="TMessage">The type of the message to dispatch.</typeparam>
	/// <typeparam name="TResponse">The type of the response value.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="messageFactory">A function that creates the message from the HTTP context.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="resultFactory">Optional function to convert the dispatch result to an action result. If null, uses default conversion.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static async Task<IActionResult> DispatchMessageAsync<TMessage, TResponse>(
		this ControllerBase controller,
		Func<HttpContext, TMessage> messageFactory,
		CancellationToken cancellationToken,
		Action<MessageContext>? customizeContext = null,
		Func<ControllerBase, IMessageResult<TResponse>, IActionResult>? resultFactory = null)
		where TMessage : class, IDispatchAction<TResponse>
		where TResponse : class
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(messageFactory);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var message = messageFactory(controller.HttpContext);
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		var dispatcherResult = await dispatcher
			.DispatchAsync<TMessage, TResponse>(message, context, cancellationToken)
			.ConfigureAwait(false);

		return resultFactory != null
			? resultFactory(controller, dispatcherResult)
			: controller.ToHttpActionResult(dispatcherResult);
	}

	/// <summary>
	/// Dispatches an event to the Dispatch pipeline from an MVC controller.
	/// </summary>
	/// <typeparam name="TEvent">The type of the event to dispatch.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="event">The event to dispatch.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static Task<IMessageResult> DispatchEventAsync<TEvent>(
		this ControllerBase controller,
		TEvent @event,
		CancellationToken cancellationToken, Action<MessageContext>? customizeContext = null)
		where TEvent : class, IDispatchEvent
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(@event);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		return dispatcher.DispatchAsync(@event, context, cancellationToken);
	}

	/// <summary>
	/// Dispatches an event created by a factory to the Dispatch pipeline and returns an HTTP action result.
	/// </summary>
	/// <typeparam name="TEvent">The type of the event to dispatch.</typeparam>
	/// <param name="controller">The controller instance.</param>
	/// <param name="eventFactory">A function that creates the event from the HTTP context.</param>
	/// <param name="customizeContext">Optional action to customize the message context before dispatching.</param>
	/// <param name="resultFactory">Optional function to convert the dispatch result to an action result. If null, uses default conversion.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A <see cref="Task{TResult}" /> representing the result of the asynchronous operation.</returns>
	public static async Task<IActionResult> DispatchEventAsync<TEvent>(
		this ControllerBase controller,
		Func<HttpContext, TEvent> eventFactory,
		CancellationToken cancellationToken,
		Action<MessageContext>? customizeContext = null,
		Func<ControllerBase, IMessageResult, IActionResult>? resultFactory = null)
		where TEvent : class, IDispatchEvent
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(eventFactory);

		var dispatcher = controller.HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var evt = eventFactory(controller.HttpContext);
		var context = controller.HttpContext.CreateDispatchMessageContext();
		customizeContext?.Invoke(context);

		var dispatcherResult = await dispatcher
			.DispatchAsync(evt, context, cancellationToken)
			.ConfigureAwait(false);

		return resultFactory != null
			? resultFactory(controller, dispatcherResult)
			: controller.ToHttpActionResult(dispatcherResult);
	}
}
