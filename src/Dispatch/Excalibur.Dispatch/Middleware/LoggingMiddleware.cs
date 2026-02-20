// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that provides structured logging for message processing.
/// </summary>
/// <remarks>
/// <para>
/// This middleware logs the start and completion of message processing with configurable
/// log levels, timing information, and optional payload inclusion.
/// </para>
/// </remarks>
/// <param name="options">The logging middleware options.</param>
/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
/// <param name="logger">The logger instance.</param>
[AppliesTo(MessageKinds.All)]
public sealed partial class LoggingMiddleware(
	IOptions<LoggingMiddlewareOptions> options,
	ITelemetrySanitizer sanitizer,
	ILogger<LoggingMiddleware> logger) : IDispatchMiddleware
{
	private readonly LoggingMiddlewareOptions _options = options?.Value ?? new LoggingMiddlewareOptions();
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	private readonly ILogger<LoggingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

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

		var messageType = message.GetType();

		// Check if this message type should be excluded
		if (_options.ExcludeTypes.Contains(messageType))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var messageTypeName = messageType.Name;
		var messageId = context.MessageId ?? string.Empty;
		var correlationId = context.CorrelationId ?? string.Empty;

		// Log start
		if (_options.LogStart)
		{
			if (_options.IncludePayload)
			{
				LogMessageStartWithPayload(messageTypeName, messageId, correlationId, _sanitizer.SanitizePayload(SerializePayload(message)));
			}
			else
			{
				LogMessageStart(messageTypeName, messageId, correlationId);
			}
		}

		var stopwatch = _options.IncludeTiming ? Stopwatch.StartNew() : null;

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			stopwatch?.Stop();

			// Log completion
			if (_options.LogCompletion)
			{
				if (result.IsSuccess)
				{
					LogSuccess(messageTypeName, messageId, stopwatch?.ElapsedMilliseconds ?? 0);
				}
				else
				{
					LogFailure(
						messageTypeName,
						messageId,
						stopwatch?.ElapsedMilliseconds ?? 0,
						result.ProblemDetails?.Detail ?? "Unknown error");
				}
			}

			return result;
		}
		catch (Exception ex)
		{
			stopwatch?.Stop();
			LogException(messageTypeName, messageId, stopwatch?.ElapsedMilliseconds ?? 0, ex);
			throw;
		}
	}

	private static string SerializePayload(IDispatchMessage message)
	{
		try
		{
			return JsonSerializer.Serialize(message, new JsonSerializerOptions
			{
				WriteIndented = false,
				MaxDepth = 3 // Limit depth to avoid circular references
			});
		}
		catch
		{
			return "<serialization failed>";
		}
	}

	[LoggerMessage(MiddlewareEventId.LoggingStarted, LogLevel.Debug,
		"Processing {MessageType} [MessageId={MessageId}, CorrelationId={CorrelationId}]")]
	private partial void LogMessageStart(string messageType, string messageId, string correlationId);

	[LoggerMessage(MiddlewareEventId.LoggingStartedWithPayload, LogLevel.Debug,
		"Processing {MessageType} [MessageId={MessageId}, CorrelationId={CorrelationId}] Payload={Payload}")]
	private partial void LogMessageStartWithPayload(string messageType, string messageId, string correlationId, string payload);

	[LoggerMessage(MiddlewareEventId.LoggingCompleted, LogLevel.Information,
		"Message processed successfully: {MessageType} [MessageId={MessageId}, DurationMs={DurationMs}]")]
	private partial void LogSuccess(string messageType, string messageId, long durationMs);

	[LoggerMessage(MiddlewareEventId.LoggingFailed, LogLevel.Error,
		"Message processing failed: {MessageType} [MessageId={MessageId}, DurationMs={DurationMs}] Error={Error}")]
	private partial void LogFailure(string messageType, string messageId, long durationMs, string error);

	[LoggerMessage(MiddlewareEventId.LoggingException, LogLevel.Error,
		"Message processing threw exception: {MessageType} [MessageId={MessageId}, DurationMs={DurationMs}]")]
	private partial void LogException(string messageType, string messageId, long durationMs, Exception ex);
}
