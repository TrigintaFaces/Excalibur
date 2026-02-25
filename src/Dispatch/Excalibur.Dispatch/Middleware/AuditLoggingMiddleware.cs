// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that provides audit logging for message processing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AuditLoggingMiddleware" /> class. </remarks>
/// <param name="options"> The audit logging options. </param>
/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
/// <param name="logger"> The logger. </param>
[AppliesTo(MessageKinds.All)]
public sealed partial class AuditLoggingMiddleware(IOptions<AuditLoggingOptions> options, ITelemetrySanitizer sanitizer, ILogger<AuditLoggingMiddleware> logger)
	: IDispatchMiddleware
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.AuditLoggingMiddleware, "1.0.0");

	private readonly AuditLoggingOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));

	private readonly ILogger<AuditLoggingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Logging;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Audit payload logging is optional and relies on known message types registered at startup.")]
	[UnconditionalSuppressMessage(
		"AotAnalysis",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Audit payload logging relies on JSON serialization and is not supported in AOT scenarios.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		using var activity = ActivitySource.StartActivity("AuditLoggingMiddleware.Invoke");
		_ = (activity?.SetTag("message.id", context.MessageId ?? string.Empty));
		_ = (activity?.SetTag("message.type", message.GetType().Name));

		var stopwatch = ValueStopwatch.StartNew();
		var userId = GetUserId(context);
		var correlationId = GetCorrelationId(context);
		var messageType = message.GetType().Name;

		// Log message processing start
		var messageId = Guid.TryParse(context.MessageId, out var parsedId) ? parsedId : Guid.Empty;
		LogMessageProcessingStarted(messageId, messageType, userId, correlationId);

		// Add audit context to activity — sanitize PII
		if (activity is not null)
		{
			SetSanitizedTag(activity, "audit.user_id", userId);
			_ = activity.SetTag("audit.correlation_id", correlationId);
		}

		if (_options.LogMessagePayload && ShouldLogPayload(message))
		{
			LogMessagePayload(message, messageId);
		}

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				LogMessageProcessingCompleted(messageId, messageType, userId, correlationId,
					true, (long)stopwatch.Elapsed.TotalMilliseconds);

				_ = (activity?.SetTag("audit.success", value: true));
				_ = (activity?.SetTag("audit.duration_ms", (long)stopwatch.Elapsed.TotalMilliseconds));
				_ = (activity?.SetStatus(ActivityStatusCode.Ok));
			}
			else
			{
				LogMessageProcessingFailed(messageId, messageType, userId, correlationId,
					result.ErrorMessage ?? "Unknown error", (long)stopwatch.Elapsed.TotalMilliseconds, null);

				_ = (activity?.SetTag("audit.success", value: false));
				_ = (activity?.SetTag("audit.duration_ms", (long)stopwatch.Elapsed.TotalMilliseconds));
				_ = (activity?.SetTag("audit.error", result.ErrorMessage));
				_ = (activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage));
			}

			return result;
		}
		catch (Exception ex)
		{
			var sanitizedError = ex.GetSanitizedErrorDescription(_sanitizer);
			LogMessageProcessingFailed(messageId, messageType, userId, correlationId,
				sanitizedError, (long)stopwatch.Elapsed.TotalMilliseconds, ex);

			_ = (activity?.SetTag("audit.success", value: false));
			_ = (activity?.SetTag("audit.duration_ms", (long)stopwatch.Elapsed.TotalMilliseconds));
			_ = (activity?.SetTag("audit.exception", ex.GetType().Name));
			activity?.SetSanitizedErrorStatus(ex, _sanitizer);

			throw;
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.AuditLoggingMiddlewareExecuting, LogLevel.Information,
		"AUDIT: Message processing started - MessageId: {MessageId}, MessageType: {MessageType}, UserId: {UserId}, CorrelationId: {CorrelationId}")]
	private partial void LogMessageProcessingStarted(Guid messageId, string messageType, string? userId, string? correlationId);

	[LoggerMessage(MiddlewareEventId.AuditLogEntryCreated, LogLevel.Information,
		"AUDIT: Message processing completed - MessageId: {MessageId}, MessageType: {MessageType}, UserId: {UserId}, CorrelationId: {CorrelationId}, Success: {Success}, Duration: {DurationMs}ms")]
	private partial void LogMessageProcessingCompleted(Guid messageId, string messageType, string? userId, string? correlationId,
		bool success, long durationMs);

	[LoggerMessage(MiddlewareEventId.AuditLogWriteFailed, LogLevel.Warning,
		"AUDIT: Message processing failed - MessageId: {MessageId}, MessageType: {MessageType}, UserId: {UserId}, CorrelationId: {CorrelationId}, Error: {Error}, Duration: {DurationMs}ms")]
	private partial void LogMessageProcessingFailed(Guid messageId, string messageType, string? userId, string? correlationId, string error,
		long durationMs, Exception? ex);

	[LoggerMessage(MiddlewareEventId.SensitiveDataMasked, LogLevel.Debug,
		"AUDIT: Message payload - MessageId: {MessageId}, Payload: {PayloadJson}")]
	private partial void LogAuditMessagePayload(Guid messageId, string payloadJson);

	[LoggerMessage(MiddlewareEventId.SensitiveDataMasked + 4, LogLevel.Debug,
		"AUDIT: Message payload too large - MessageId: {MessageId}, PayloadLength: {PayloadLength}")]
	private partial void LogAuditMessagePayloadTooLarge(Guid messageId, int payloadLength);

	[LoggerMessage(MiddlewareEventId.SensitiveDataMasked + 5, LogLevel.Warning,
		"AUDIT: Failed to serialize message payload - MessageId: {MessageId}")]
	private partial void LogFailedToSerializeMessagePayloadForAudit(Guid messageId, Exception ex);

	private void SetSanitizedTag(Activity activity, string tagName, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(tagName, rawValue);
		if (sanitized is not null)
		{
			_ = activity.SetTag(tagName, sanitized);
		}
	}

	private string? GetUserId(IMessageContext context)
	{
		if (_options.UserIdExtractor != null)
		{
			return _options.UserIdExtractor(context);
		}

		// Try to get from context properties
		if (context.Properties.TryGetValue("UserId", out var userIdObj) && userIdObj is string userId)
		{
			return userId;
		}

		// Try to get from headers
		if (context.Properties.TryGetValue("Headers", out var headersObj) &&
			headersObj is Dictionary<string, object> headers && headers.TryGetValue("UserId", out var headerUserId) &&
			headerUserId is string headerUserIdStr)
		{
			return headerUserIdStr;
		}

		return null;
	}

	private string? GetCorrelationId(IMessageContext context)
	{
		if (_options.CorrelationIdExtractor != null)
		{
			return _options.CorrelationIdExtractor(context);
		}

		// Try to get from context properties
		if (context.Properties.TryGetValue("CorrelationId", out var correlationIdObj) && correlationIdObj is string correlationId)
		{
			return correlationId;
		}

		// Try to get from headers
		if (context.Properties.TryGetValue("Headers", out var headersObj) &&
			headersObj is Dictionary<string, object> headers && headers.TryGetValue("CorrelationId", out var headerCorrelationId) &&
			headerCorrelationId is string headerCorrelationIdStr)
		{
			return headerCorrelationIdStr;
		}

		return null;
	}

	private bool ShouldLogPayload(IDispatchMessage message)
	{
		if (_options.PayloadFilter != null)
		{
			return _options.PayloadFilter(message);
		}

		// Default: log payload for all messages unless it's too large
		return true;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void LogMessagePayload(IDispatchMessage message, Guid messageId)
	{
		try
		{
			var payloadJson = JsonSerializer.Serialize(
				message,
				new JsonSerializerOptions { WriteIndented = false, MaxDepth = _options.MaxPayloadDepth });

			if (payloadJson.Length <= _options.MaxPayloadSize)
			{
				LogAuditMessagePayload(messageId, payloadJson);
			}
			else
			{
				LogAuditMessagePayloadTooLarge(messageId, payloadJson.Length);
			}
		}
		catch (Exception ex)
		{
			LogFailedToSerializeMessagePayloadForAudit(messageId, ex);
		}
	}
}
