// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Timeout;

/// <summary>
/// Middleware that applies per-handler timeout limits using linked cancellation tokens.
/// </summary>
/// <remarks>
/// <para>
/// This middleware wraps handler execution with a linked cancellation token source
/// configured with the handler-specific timeout. Timeouts are resolved in order:
/// </para>
/// <list type="number">
/// <item>Context override (<c>HandlerTimeout.Override</c> in Items)</item>
/// <item>Per-handler override from <see cref="HandlerTimeoutOptions.HandlerTimeouts"/></item>
/// <item>Default timeout from <see cref="HandlerTimeoutOptions.DefaultTimeout"/></item>
/// </list>
/// </remarks>
/// <param name="options">The handler timeout configuration.</param>
/// <param name="logger">The logger for diagnostic output.</param>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class HandlerTimeoutMiddleware(
	IOptions<HandlerTimeoutOptions> options,
	ILogger<HandlerTimeoutMiddleware> logger) : IDispatchMiddleware
{
	private readonly HandlerTimeoutOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<HandlerTimeoutMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

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

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var timeout = DetermineTimeout(message, context);
		LogApplyingHandlerTimeout(timeout.TotalMilliseconds, context.MessageId ?? string.Empty, message.GetType().Name);

		try
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(timeout);

			var result = await nextDelegate(message, context, timeoutCts.Token).ConfigureAwait(false);

			return result;
		}
		catch (OperationCanceledException ex) when (
			ex.CancellationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Our timeout fired, not external cancellation
			LogHandlerTimedOut(context.MessageId ?? string.Empty, message.GetType().Name, timeout.TotalMilliseconds);

			context.TimeoutExceeded = true;
			context.TimeoutElapsed = timeout;

			if (_options.ThrowOnTimeout)
			{
				throw new TimeoutException(
					$"Handler processing timed out after {timeout.TotalMilliseconds}ms for message {context.MessageId}");
			}

			return MessageResult.Failed($"Handler timed out after {timeout.TotalMilliseconds}ms");
		}
	}

	[LoggerMessage(MiddlewareEventId.TimeoutMiddlewareExecuting, LogLevel.Debug,
		"Applying handler timeout of {TimeoutMs}ms to message {MessageId} of type {MessageType}")]
	private partial void LogApplyingHandlerTimeout(double timeoutMs, string messageId, string messageType);

	[LoggerMessage(MiddlewareEventId.TimeoutExceeded, LogLevel.Warning,
		"Handler timed out for message {MessageId} of type {MessageType} after {TimeoutMs}ms")]
	private partial void LogHandlerTimedOut(string messageId, string messageType, double timeoutMs);

	/// <summary>
	/// Determines the timeout for the current message/handler combination.
	/// </summary>
	private TimeSpan DetermineTimeout(IDispatchMessage message, IMessageContext context)
	{
		// 1. Check context override
		if (context.ContainsItem("HandlerTimeout.Override"))
		{
			var overrideTimeout = context.GetItem<TimeSpan>("HandlerTimeout.Override");
			if (overrideTimeout > TimeSpan.Zero)
			{
				return overrideTimeout;
			}
		}

		// 2. Check per-handler override by message type name (handler name maps to message type)
		var messageTypeName = message.GetType().Name;
		if (_options.HandlerTimeouts.TryGetValue(messageTypeName, out var handlerTimeout))
		{
			return handlerTimeout;
		}

		// 3. Default timeout
		return _options.DefaultTimeout;
	}
}
