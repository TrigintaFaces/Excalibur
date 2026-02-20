// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

/// <summary>
/// Middleware that routes exceptions to typed exception handlers registered in the DI container.
/// </summary>
/// <remarks>
/// <para>
/// This middleware catches exceptions from downstream pipeline components and attempts to route
/// them to a registered <see cref="ITypedExceptionHandler{TException}"/> for the exception's type.
/// If no handler is found for the exact type, the exception type hierarchy is walked upward.
/// If no handler handles the exception, it is re-thrown.
/// </para>
/// <para>
/// <see cref="OperationCanceledException"/> is never routed to handlers and always propagates.
/// </para>
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving typed handlers.</param>
/// <param name="logger">The logger for diagnostic output.</param>
[AppliesTo(MessageKinds.All)]
public sealed partial class TypedExceptionHandlerMiddleware(
	IServiceProvider serviceProvider,
	ILogger<TypedExceptionHandlerMiddleware> logger) : IDispatchMiddleware
{
	private const int MaxCachedHandlerTypes = 1024;
	private static readonly ConcurrentDictionary<Type, Type> HandlerTypeCache = new();

	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ILogger<TypedExceptionHandlerMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.ErrorHandling;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		try
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Never handle cancellation - propagate for proper cancellation handling
			throw;
		}
		catch (Exception ex)
		{
			var result = await TryHandleExceptionAsync(ex, message, context, cancellationToken).ConfigureAwait(false);
			if (result.IsHandled)
			{
				LogExceptionHandled(ex.GetType().Name, context.MessageId ?? string.Empty);
				return result.Result!;
			}

			LogExceptionNotHandled(ex.GetType().Name, context.MessageId ?? string.Empty);
			throw;
		}
	}

	[LoggerMessage(MiddlewareEventId.ExceptionMapped, LogLevel.Information,
		"Typed exception handler handled {ExceptionType} for message {MessageId}")]
	private partial void LogExceptionHandled(string exceptionType, string messageId);

	[LoggerMessage(MiddlewareEventId.UnhandledExceptionCaught, LogLevel.Debug,
		"No typed exception handler found for {ExceptionType} on message {MessageId}")]
	private partial void LogExceptionNotHandled(string exceptionType, string messageId);

	/// <summary>
	/// Attempts to find and invoke a typed exception handler for the given exception.
	/// </summary>
	private async ValueTask<ExceptionHandlerResult> TryHandleExceptionAsync(
		Exception exception,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Walk the exception type hierarchy to find a matching handler
		var exceptionType = exception.GetType();

		while (exceptionType != null && exceptionType != typeof(object))
		{
			var handlerType = ResolveHandlerType(exceptionType);
			var handler = _serviceProvider.GetService(handlerType);

			if (handler != null)
			{
				return await InvokeHandlerAsync(handler, exception, message, context, cancellationToken)
					.ConfigureAwait(false);
			}

			exceptionType = exceptionType.BaseType;
		}

		return ExceptionHandlerResult.NotHandled();
	}

	/// <summary>
	/// Resolves the <c>ITypedExceptionHandler&lt;TException&gt;</c> type for the given exception type.
	/// </summary>
	private static Type ResolveHandlerType(Type exceptionType)
	{
		if (HandlerTypeCache.TryGetValue(exceptionType, out var cached))
		{
			return cached;
		}

		var handlerType = typeof(ITypedExceptionHandler<>).MakeGenericType(exceptionType);

		// Bounded cache: skip caching when full
		if (HandlerTypeCache.Count < MaxCachedHandlerTypes)
		{
			HandlerTypeCache.TryAdd(exceptionType, handlerType);
		}

		return handlerType;
	}

	/// <summary>
	/// Invokes the handler using the HandleAsync convention.
	/// </summary>
	private static async ValueTask<ExceptionHandlerResult> InvokeHandlerAsync(
		object handler,
		Exception exception,
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Use dynamic dispatch to call HandleAsync on the handler
		// This avoids reflection overhead on each call while remaining type-safe
		var handleMethod = handler.GetType().GetMethod("HandleAsync");
		if (handleMethod == null)
		{
			return ExceptionHandlerResult.NotHandled();
		}

		var task = (ValueTask<ExceptionHandlerResult>)handleMethod.Invoke(
			handler,
			[exception, message, context, cancellationToken])!;

		return await task.ConfigureAwait(false);
	}
}
