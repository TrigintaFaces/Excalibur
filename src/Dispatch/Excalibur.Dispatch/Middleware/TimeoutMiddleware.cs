// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that implements timeout handling for message processing operations. Ensures operations complete within specified time limits
/// and provides proper timeout handling.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimeoutMiddleware" /> class. </remarks>
/// <param name="logger"> The logger for diagnostic output. </param>
/// <param name="options"> The timeout configuration options. </param>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class TimeoutMiddleware(
	ILogger<TimeoutMiddleware> logger,
	IOptions<TimeoutOptions> options) : IDispatchMiddleware, IAsyncDisposable
{
	private readonly ILogger<TimeoutMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly TimeoutOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ActivitySource _activitySource = new("Excalibur.Dispatch.Middleware.Timeout", "1.0.0");

	/// <summary>
	/// Gets the stage in the dispatch pipeline where this middleware executes.
	/// </summary>
	/// <value>The current <see cref="Stage"/> value.</value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <summary>
	/// Gets the message kinds this middleware applies to.
	/// </summary>
	/// <value>The current <see cref="ApplicableMessageKinds"/> value.</value>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

	/// <summary>
	/// Invokes the middleware to apply timeout handling to message processing.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <param name="nextDelegate"> The next middleware in the pipeline. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="MessageTimeoutException"></exception>
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

		using var activity = _activitySource.StartActivity("timeout-processing");
		_ = (activity?.SetTag("message.id", context.MessageId));
		_ = (activity?.SetTag("message.type", message.GetType().Name));
		_ = (activity?.SetTag("timeout.enabled", value: true));

		var timeout = DetermineTimeout(message, context);

		_ = (activity?.SetTag("timeout.duration_ms", timeout.TotalMilliseconds));

		LogApplyingTimeout(_logger, timeout.TotalMilliseconds, context.MessageId ?? string.Empty, message.GetType().Name);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Create timeout cancellation token
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(timeout);

			// Execute with timeout
			var result = await nextDelegate(message, context, timeoutCts.Token).ConfigureAwait(false);

			_ = (activity?.SetTag("timeout.elapsed_ms", stopwatch.Elapsed.TotalMilliseconds));
			_ = (activity?.SetTag("timeout.exceeded", value: false));
			_ = (activity?.SetStatus(ActivityStatusCode.Ok));

			LogMessageCompleted(_logger, context.MessageId ?? string.Empty, stopwatch.Elapsed.TotalMilliseconds, timeout.TotalMilliseconds);

			return result;
		}
		catch (OperationCanceledException ex) when (
			ex.CancellationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// This is our timeout, not external cancellation
			_ = (activity?.SetTag("timeout.elapsed_ms", stopwatch.Elapsed.TotalMilliseconds));
			_ = (activity?.SetTag("timeout.exceeded", value: true));
			_ = (activity?.SetStatus(ActivityStatusCode.Error, "Operation timed out"));
			activity?.RecordException(ex);

			var timeoutException = new MessageTimeoutException(
				$"Message processing timed out after {timeout.TotalMilliseconds}ms for message {context.MessageId}",
				ex)
			{
				MessageId = context.MessageId,
				MessageType = message.GetType().Name,
				TimeoutDuration = timeout,
				ElapsedTime = stopwatch.Elapsed,
			};

			LogMessageTimedOut(_logger, context.MessageId ?? string.Empty, message.GetType().Name, stopwatch.Elapsed.TotalMilliseconds,
				timeout.TotalMilliseconds, timeoutException);

			// Store timeout information in context for downstream handlers
			context.SetItem("Timeout.Exceeded", value: true);
			context.SetItem("Timeout.ElapsedTime", stopwatch.Elapsed);
			context.SetItem("Timeout.ConfiguredTimeout", timeout);

			if (_options.ThrowOnTimeout)
			{
				throw timeoutException;
			}

			// Return timeout result instead of throwing
			return new TimeoutMessageResult(timeoutException);
		}
		catch (Exception ex)
		{
			_ = (activity?.SetTag("timeout.elapsed_ms", stopwatch.Elapsed.TotalMilliseconds));
			_ = (activity?.SetTag("timeout.exceeded", value: false));
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			activity?.RecordException(ex);

			LogProcessingError(_logger, context.MessageId ?? string.Empty, message.GetType().Name, stopwatch.Elapsed.TotalMilliseconds, ex);

			throw;
		}
	}

	/// <summary>
	/// Disposes resources used by the middleware.
	/// </summary>
	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_activitySource.Dispose();
		return ValueTask.CompletedTask;
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.TimeoutMiddlewareExecuting, LogLevel.Debug,
		"Applying timeout of {TimeoutMs}ms to message {MessageId} of type {MessageType}")]
	private static partial void LogApplyingTimeout(
		ILogger logger,
		double timeoutMs,
		string messageId,
		string messageType);

	[LoggerMessage(MiddlewareEventId.TimeoutCompleted, LogLevel.Debug,
		"Message {MessageId} completed in {ElapsedMs}ms (timeout: {TimeoutMs}ms)")]
	private static partial void LogMessageCompleted(
		ILogger logger,
		string messageId,
		double elapsedMs,
		double timeoutMs);

	[LoggerMessage(MiddlewareEventId.TimeoutExceeded, LogLevel.Warning,
		"Message {MessageId} of type {MessageType} timed out after {ElapsedMs}ms (limit: {TimeoutMs}ms)")]
	private static partial void LogMessageTimedOut(
		ILogger logger,
		string messageId,
		string messageType,
		double elapsedMs,
		double timeoutMs,
		Exception ex);

	[LoggerMessage(MiddlewareEventId.TimeoutProcessingError, LogLevel.Error,
		"Error processing message {MessageId} of type {MessageType} after {ElapsedMs}ms")]
	private static partial void LogProcessingError(
		ILogger logger,
		string messageId,
		string messageType,
		double elapsedMs,
		Exception ex);

	[LoggerMessage(MiddlewareEventId.TimeoutContextOverride, LogLevel.Debug,
		"Using context override timeout of {TimeoutMs}ms for message {MessageId}")]
	private static partial void LogContextOverrideTimeout(
		ILogger logger,
		double timeoutMs,
		string messageId);

	[LoggerMessage(MiddlewareEventId.TimeoutMessageTypeSpecific, LogLevel.Debug,
		"Using message type specific timeout of {TimeoutMs}ms for {MessageType}")]
	private static partial void LogMessageTypeSpecificTimeout(
		ILogger logger,
		double timeoutMs,
		string messageType);

	[LoggerMessage(MiddlewareEventId.TimeoutMessageKind, LogLevel.Debug,
		"Using {MessageKind} timeout of {TimeoutMs}ms for message {MessageId}")]
	private static partial void LogMessageKindTimeout(
		ILogger logger,
		MessageKinds messageKind,
		double timeoutMs,
		string messageId);

	/// <summary>
	/// Gets the message kind for the given message using direct type checks instead of reflection.
	/// </summary>
	/// <param name="message"> The message to classify. </param>
	/// <returns> The message kind. </returns>
	private static MessageKinds GetMessageKind(IDispatchMessage message)
	{
		// Direct type checks against well-known interfaces (AOT-safe, no reflection)
		if (message is IDispatchAction)
		{
			return MessageKinds.Action;
		}

		if (message is IDispatchEvent)
		{
			return MessageKinds.Event;
		}

		if (message is IDispatchDocument)
		{
			return MessageKinds.Document;
		}

		// Fallback classification based on naming conventions
		var typeName = message.GetType().Name;
		if (typeName.EndsWith("Command", StringComparison.Ordinal) || typeName.EndsWith("Action", StringComparison.Ordinal))
		{
			return MessageKinds.Action;
		}

		if (typeName.EndsWith("Event", StringComparison.Ordinal) || typeName.EndsWith("Notification", StringComparison.Ordinal))
		{
			return MessageKinds.Event;
		}

		if (typeName.EndsWith("Document", StringComparison.Ordinal) || typeName.EndsWith("Query", StringComparison.Ordinal))
		{
			return MessageKinds.Document;
		}

		return MessageKinds.Action; // Default to Action for unknown types
	}

	/// <summary>
	/// Determines the appropriate timeout for the given message and context.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The timeout duration to apply. </returns>
	private TimeSpan DetermineTimeout(IDispatchMessage message, IMessageContext context)
	{
		// Check for message-specific timeout in context
		if (context.ContainsItem("Timeout.Override"))
		{
			var overrideTimeout = context.GetItem<TimeSpan>("Timeout.Override");
			if (overrideTimeout > TimeSpan.Zero)
			{
				LogContextOverrideTimeout(_logger, overrideTimeout.TotalMilliseconds, context.MessageId ?? string.Empty);
				return overrideTimeout;
			}
		}

		// Check for message type-specific timeout
		var messageType = message.GetType();
		if (_options.MessageTypeTimeouts.TryGetValue(messageType.Name, out var specificTimeout))
		{
			LogMessageTypeSpecificTimeout(_logger, specificTimeout.TotalMilliseconds, messageType.Name);
			return specificTimeout;
		}

		// Check for message kind-specific timeout
		var messageKind = GetMessageKind(message);
		var kindTimeout = messageKind switch
		{
			MessageKinds.Action => _options.ActionTimeout,
			MessageKinds.Event => _options.EventTimeout,
			MessageKinds.Document => _options.DocumentTimeout,
			_ => _options.DefaultTimeout,
		};

		LogMessageKindTimeout(_logger, messageKind, kindTimeout.TotalMilliseconds, context.MessageId ?? string.Empty);

		return kindTimeout;
	}
}
