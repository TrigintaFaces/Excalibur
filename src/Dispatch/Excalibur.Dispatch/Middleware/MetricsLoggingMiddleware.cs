// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Common namespace is deprecated - using Messaging.Abstractions instead
using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for collecting metrics and structured logging data for message processing observability and monitoring.
/// </summary>
/// <remarks>
/// This middleware integrates with OpenTelemetry and .NET metrics to provide comprehensive observability for message processing pipelines. It:
/// <list type="bullet">
/// <item> Tracks message processing rates, latencies, and error rates </item>
/// <item> Logs structured data for message flows with correlation context </item>
/// <item> Emits OpenTelemetry metrics for dashboards and alerting </item>
/// <item> Captures performance characteristics by message type and tenant </item>
/// <item> Records pipeline stage timing and middleware performance </item>
/// <item> Provides extensible hooks for custom metrics collection </item>
/// </list>
/// This middleware runs at the end of the pipeline to capture complete timing and result information from all preceding middleware.
/// </remarks>
public sealed partial class MetricsLoggingMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// OpenTelemetry Metrics.
	/// </summary>
	private static readonly Meter Meter = new(DispatchTelemetryConstants.Meters.Messaging, "1.0.0");

	private static readonly Counter<long> MessageProcessedCounter = Meter.CreateCounter<long>(
		name: "dispatch.messaging.messages.processed",
		unit: "message",
		description: "Total number of messages processed");

	private static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>(
		name: "dispatch.messaging.messages.processing_duration",
		unit: "ms",
		description: "Duration of message processing in milliseconds");

	private static readonly Counter<long> MessageErrorCounter = Meter.CreateCounter<long>(
		name: "dispatch.messaging.messages.errors",
		unit: "error",
		description: "Total number of message processing errors");

	private readonly MetricsLoggingOptions _options;
	private readonly IMessageMetrics _messageMetrics;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<MetricsLoggingMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MetricsLoggingMiddleware"/> class.
	/// Creates a new metrics logging middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for metrics and logging. </param>
	/// <param name="messageMetrics"> Service for collecting message metrics. </param>
	/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
	/// <param name="logger"> Logger for structured logging. </param>
	public MetricsLoggingMiddleware(
		IOptions<MetricsLoggingOptions> options,
		IMessageMetrics messageMetrics,
		ITelemetrySanitizer sanitizer,
		ILogger<MetricsLoggingMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(messageMetrics);
		ArgumentNullException.ThrowIfNull(sanitizer);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_messageMetrics = messageMetrics;
		_sanitizer = sanitizer;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "Metrics collection uses reflection-based message inspection for known types registered at startup.")]
	[UnconditionalSuppressMessage(
			"AotAnalysis",
			"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
			Justification = "Metrics payload sizing relies on JSON serialization and is not supported in AOT scenarios.")]
	public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip metrics collection if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract context information for metrics
		var metricsContext = ExtractMetricsContext(message, context);

		// Start timing
		var stopwatch = ValueStopwatch.StartNew();
		var startTime = DateTimeOffset.UtcNow;

		// Set up OpenTelemetry activity tags
		SetMetricsActivityTags(message, metricsContext);

		LogMetricsCollectionStart(_logger, message.GetType().Name);

		Exception? processingException = null;
		IMessageResult result;

		try
		{
			// Continue pipeline execution
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			processingException = ex;
			throw;
		}
		finally
		{
			// Stop timing
			var endTime = DateTimeOffset.UtcNow;
			var duration = stopwatch.Elapsed;

			// Record metrics and logs
			await RecordProcessingMetricsAsync(
				message,
				metricsContext,
				duration,
				startTime,
				endTime,
				processingException,
				cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	// High-performance LoggerMessage implementations for metrics middleware hot paths (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.MetricsLoggingMiddlewareExecuting, LogLevel.Debug, "Starting metrics collection for message {MessageType}")]
	private static partial void LogMetricsCollectionStart(ILogger logger, string messageType);

	[LoggerMessage(MiddlewareEventId.MetricsRecorded, LogLevel.Debug,
		"Recorded processing metrics for message {MessageType}: {Duration}ms, Success: {Success}")]
	private static partial void LogMetricsCollectionComplete(ILogger logger, string messageType, double duration, bool success);

	[LoggerMessage(MiddlewareEventId.ThroughputMeasured, LogLevel.Warning, "Failed to record metrics for message {MessageType}")]
	private static partial void LogMetricsCollectionError(ILogger logger, Exception exception, string messageType);

	[LoggerMessage(MiddlewareEventId.LatencyMeasured, LogLevel.Information,
		"Message processing completed successfully - Type: {MessageType}, Duration: {Duration}ms, Size: {Size} bytes")]
	private static partial void LogMessageProcessingSuccess(ILogger logger, string messageType, double duration, long size);

	[LoggerMessage(MiddlewareEventId.ErrorRateRecorded, LogLevel.Warning,
		"Message processing completed with error - Type: {MessageType}, Duration: {Duration}ms, Error: {Error}")]
	private static partial void LogMessageProcessingError(ILogger logger, Exception exception, string messageType, double duration,
		string error);

	/// <summary>
	/// Extracts metrics context from the message and context.
	/// </summary>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Middleware.MetricsLoggingMiddleware.EstimateMessageSize(IDispatchMessage)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Middleware.MetricsLoggingMiddleware.EstimateMessageSize(IDispatchMessage)")]
	private static ProcessingMetricsContext ExtractMetricsContext(
		IDispatchMessage message,
		IMessageContext context) =>
		new(
			MessageType: message.GetType().Name,
			MessageKind: DetermineMessageKind(message),
			CorrelationId: GetPropertyValue(context, "CorrelationId"),
			TenantId: GetPropertyValue(context, "TenantId"),
			UserId: GetPropertyValue(context, "UserId"),
			MessageSize: EstimateMessageSize(message),
			QueueName: GetPropertyValue(context, "QueueName"),
			Source: GetPropertyValue(context, "Source"));

	/// <summary>
	/// Determines the message kind for metrics categorization.
	/// </summary>
	private static string DetermineMessageKind(IDispatchMessage message)
	{
		// IDispatchAction, IDispatchEvent, IDispatchDocument interfaces don't exist For now, just return the message type name as the kind
		var messageTypeName = message.GetType().Name;

		if (messageTypeName.Contains("Command", StringComparison.Ordinal) || messageTypeName.Contains("Query", StringComparison.Ordinal))
		{
			return "Action";
		}

		if (messageTypeName.Contains("Event", StringComparison.Ordinal))
		{
			return "Event";
		}

		if (messageTypeName.Contains("Document", StringComparison.Ordinal))
		{
			return "Document";
		}

		return "Unknown";
	}

	/// <summary>
	/// Estimates the size of a message for metrics.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private static long EstimateMessageSize(IDispatchMessage message)
	{
		try
		{
			// Simple estimation - in production, you might use actual serialization
			var json = JsonSerializer.Serialize(message, message.GetType());
			return Encoding.UTF8.GetByteCount(json);
		}
		catch
		{
			// Fall back to a default estimate if serialization fails
			return 1024; // 1KB default
		}
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetPropertyValue(IMessageContext context, string propertyName)
	{
		// Use GetItem instead of Properties
		var value = context.GetItem<object>(propertyName);
		return value?.ToString();
	}

	/// <summary>
	/// Records OpenTelemetry metrics.
	/// </summary>
	private static void RecordOpenTelemetryMetrics(
		KeyValuePair<string, object?>[] tags,
		double durationMs,
		bool isSuccess)
	{
		// Record processing count
		MessageProcessedCounter.Add(1, tags);

		// Record processing duration
		MessageProcessingDuration.Record(durationMs, tags);

		// Record error count if applicable
		if (!isSuccess)
		{
			MessageErrorCounter.Add(1, tags);
		}
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for metrics tracing.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future telemetry enhancements")]
	private void SetMetricsActivityTags(
		IDispatchMessage message,
		ProcessingMetricsContext metricsContext)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("dispatch.messaging.message_type", metricsContext.MessageType);
		_ = activity.SetTag("dispatch.messaging.message_kind", metricsContext.MessageKind);
		_ = activity.SetTag("dispatch.messaging.message_size", metricsContext.MessageSize);

		if (!string.IsNullOrEmpty(metricsContext.TenantId))
		{
			SetSanitizedTag(activity, "dispatch.messaging.tenant_id", metricsContext.TenantId);
		}

		if (!string.IsNullOrEmpty(metricsContext.QueueName))
		{
			_ = activity.SetTag("dispatch.messaging.queue_name", metricsContext.QueueName);
		}
	}

	private void SetSanitizedTag(Activity activity, string tagName, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(tagName, rawValue);
		if (sanitized is not null)
		{
			_ = activity.SetTag(tagName, sanitized);
		}
	}

	/// <summary>
	/// Records processing metrics and structured logs.
	/// </summary>
	private async Task RecordProcessingMetricsAsync(
		IDispatchMessage message,
		ProcessingMetricsContext metricsContext,
		TimeSpan duration,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var isSuccess = exception == null;
		var durationMs = duration.TotalMilliseconds;

		// Prepare metric tags
		var tags = new KeyValuePair<string, object?>[]
		{
			new("message_type", metricsContext.MessageType), new("message_kind", metricsContext.MessageKind),
			new("tenant_id", metricsContext.TenantId ?? "unknown"), new("success", isSuccess),
			new("queue_name", metricsContext.QueueName ?? "default"),
		};

		try
		{
			// Record OpenTelemetry metrics
			if (_options.RecordOpenTelemetryMetrics)
			{
				RecordOpenTelemetryMetrics(tags, durationMs, isSuccess);
			}

			// Record custom metrics
			if (_options.RecordCustomMetrics)
			{
				await _messageMetrics.RecordMessageProcessedAsync(
					metricsContext,
					duration,
					isSuccess,
					cancellationToken).ConfigureAwait(false);
			}

			// Log structured processing data
			if (_options.LogProcessingDetails)
			{
				LogProcessingDetails(message, metricsContext, duration, startTime, endTime, exception);
			}

			LogMetricsCollectionComplete(_logger, metricsContext.MessageType, durationMs, isSuccess);
		}
		catch (Exception metricsException)
		{
			LogMetricsCollectionError(_logger, metricsException, metricsContext.MessageType);
		}
	}

	/// <summary>
	/// Logs detailed processing information.
	/// </summary>
	[SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Parameter reserved for future message-specific logging enhancements")]
	private void LogProcessingDetails(
		IDispatchMessage message,
		ProcessingMetricsContext metricsContext,
		TimeSpan duration,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		Exception? exception)
	{
		_ = exception != null ? LogLevel.Warning : LogLevel.Information;

		using var logScope = _logger.BeginScope(new Dictionary<string, object>
(StringComparer.Ordinal)
		{
			["CorrelationId"] = metricsContext.CorrelationId ?? "unknown",
			["TenantId"] = metricsContext.TenantId ?? "unknown",
			["MessageType"] = metricsContext.MessageType,
			["MessageKind"] = metricsContext.MessageKind,
			["MessageSize"] = metricsContext.MessageSize,
			["ProcessingDurationMs"] = duration.TotalMilliseconds,
			["StartTime"] = startTime.ToString("O"),
			["EndTime"] = endTime.ToString("O"),
		});

		if (exception != null)
		{
			LogMessageProcessingError(_logger, exception, metricsContext.MessageType, duration.TotalMilliseconds, exception.Message);
		}
		else
		{
			LogMessageProcessingSuccess(_logger, metricsContext.MessageType, duration.TotalMilliseconds, metricsContext.MessageSize);
		}
	}

	/// <summary>
	/// Context information for metrics collection.
	/// </summary>
	private readonly record struct ProcessingMetricsContext(
		string MessageType,
		string MessageKind,
		string? CorrelationId,
		string? TenantId,
		string? UserId,
		long MessageSize,
		string? QueueName,
		string? Source);
}
