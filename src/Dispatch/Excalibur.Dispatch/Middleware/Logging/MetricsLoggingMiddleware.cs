// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
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

namespace Excalibur.Dispatch.Middleware.Logging;

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

	private static readonly JsonSerializerOptions MessageSizeSerializerOptions = new();
	private const int MessageSizeEstimatorBufferInitialCapacity = 1024;
	private const int MessageSizeEstimatorMaxRetainedCapacity = 64 * 1024;

	[ThreadStatic]
	private static ArrayBufferWriter<byte>? MessageSizeEstimatorBuffer;

	[ThreadStatic]
	private static Utf8JsonWriter? MessageSizeEstimatorWriter;

	/// <summary>
	/// Caches message kind determination results per type name to avoid repeated string.Contains calls.
	/// </summary>
	private static readonly ConcurrentDictionary<string, string> MessageKindCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Guards the tenant_id metric tag to prevent unbounded cardinality in multi-tenant scenarios.
	/// </summary>
	private static readonly Diagnostics.TagCardinalityGuard TenantIdGuard = new(maxCardinality: 100);

	private readonly MetricsLoggingOptions _options;
	private readonly IMessageMetrics _messageMetrics;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<MetricsLoggingMiddleware> _logger;
	private readonly FrozenSet<string>? _bypassMetricsTypes;
	private readonly double _sampleRate;
	private readonly bool _isMetricsCollectionEnabled;
	private readonly bool _includeMessageSizes;
	private readonly bool _requiresMessageSizeWithoutActivity;

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
		_bypassMetricsTypes = _options.BypassMetricsForTypes is { Length: > 0 } bypassTypes
			? bypassTypes.ToFrozenSet(StringComparer.Ordinal)
			: null;
		_sampleRate = _options.SampleRate;
		_isMetricsCollectionEnabled = _options.Enabled && (_sampleRate > 0d);
		_includeMessageSizes = _options.IncludeMessageSizes;
		_requiresMessageSizeWithoutActivity =
			_includeMessageSizes && (_options.RecordCustomMetrics || _options.LogProcessingDetails);
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

		// Skip metrics collection if disabled globally (including no-sampling profiles).
		if (!_isMetricsCollectionEnabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var messageType = message.GetType().Name;
		if (!ShouldCollectMetrics(messageType))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var activity = Activity.Current;
		var includeMessageSizes = _includeMessageSizes &&
			(_requiresMessageSizeWithoutActivity || activity is not null);

		// Extract context information for metrics
		var metricsContext = ExtractMetricsContext(message, context, messageType, includeMessageSizes);

		// Start timing
		var stopwatch = ValueStopwatch.StartNew();
		var startTime = _options.LogProcessingDetails ? DateTimeOffset.UtcNow : default;

		// Set up OpenTelemetry activity tags
		SetMetricsActivityTags(activity, metricsContext, includeMessageSizes);

		LogMetricsCollectionStart(_logger, messageType);

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
			var endTime = _options.LogProcessingDetails ? DateTimeOffset.UtcNow : default;
			var duration = stopwatch.Elapsed;

			// Record metrics and logs
			await RecordProcessingMetricsAsync(
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
		"Message processing completed successfully - Type: {MessageType}, Kind: {MessageKind}, CorrelationId: {CorrelationId}, TenantId: {TenantId}, Duration: {Duration}ms, Size: {Size} bytes, Start: {StartTime}, End: {EndTime}")]
	private static partial void LogMessageProcessingSuccess(
		ILogger logger,
		string messageType,
		string messageKind,
		string correlationId,
		string tenantId,
		double duration,
		long size,
		DateTimeOffset startTime,
		DateTimeOffset endTime);

	[LoggerMessage(MiddlewareEventId.ErrorRateRecorded, LogLevel.Warning,
		"Message processing completed with error - Type: {MessageType}, Kind: {MessageKind}, CorrelationId: {CorrelationId}, TenantId: {TenantId}, Duration: {Duration}ms, Error: {Error}, Start: {StartTime}, End: {EndTime}")]
	private static partial void LogMessageProcessingError(
		ILogger logger,
		Exception exception,
		string messageType,
		string messageKind,
		string correlationId,
		string tenantId,
		double duration,
		string error,
		DateTimeOffset startTime,
		DateTimeOffset endTime);

	/// <summary>
	/// Extracts metrics context from the message and context.
	/// </summary>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Middleware.MetricsLoggingMiddleware.EstimateMessageSize(IDispatchMessage)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Middleware.MetricsLoggingMiddleware.EstimateMessageSize(IDispatchMessage)")]
	private static ProcessingMetricsContext ExtractMetricsContext(
		IDispatchMessage message,
		IMessageContext context,
		string messageType,
		bool includeMessageSizes) =>
		new(
			MessageType: messageType,
			MessageKind: DetermineMessageKind(messageType),
			CorrelationId: context.CorrelationId,
			TenantId: context.TenantId,
			UserId: context.UserId,
			MessageSize: includeMessageSizes ? EstimateMessageSize(message) : 0,
			QueueName: GetContextStringValue(context, "QueueName"),
			Source: context.Source);

	/// <summary>
	/// Determines the message kind for metrics categorization.
	/// Results are cached per type name to avoid repeated string.Contains calls on the hot path.
	/// </summary>
	private static string DetermineMessageKind(string messageTypeName) =>
		MessageKindCache.GetOrAdd(messageTypeName, static name =>
		{
			if (name.Contains("Command", StringComparison.Ordinal) || name.Contains("Query", StringComparison.Ordinal))
			{
				return "Action";
			}

			if (name.Contains("Event", StringComparison.Ordinal))
			{
				return "Event";
			}

			if (name.Contains("Document", StringComparison.Ordinal))
			{
				return "Document";
			}

			return "Unknown";
		});

	/// <summary>
	/// Estimates the size of a message for metrics.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize(Object, Type, JsonSerializerOptions)")]
	private static long EstimateMessageSize(IDispatchMessage message)
	{
		try
		{
			var buffer = GetMessageSizeEstimatorBuffer();
			var writer = GetMessageSizeEstimatorWriter(buffer);
			JsonSerializer.Serialize(writer, message, message.GetType(), MessageSizeSerializerOptions);
			writer.Flush();
			return buffer.WrittenCount;
		}
		catch
		{
			// Fall back to a default estimate if serialization fails
			return 1024; // 1KB default
		}
	}

	private static ArrayBufferWriter<byte> GetMessageSizeEstimatorBuffer()
	{
		var buffer = MessageSizeEstimatorBuffer;
		if (buffer == null || buffer.Capacity > MessageSizeEstimatorMaxRetainedCapacity)
		{
			buffer = new ArrayBufferWriter<byte>(MessageSizeEstimatorBufferInitialCapacity);
			MessageSizeEstimatorBuffer = buffer;
		}

		buffer.Clear();
		return buffer;
	}

	private static Utf8JsonWriter GetMessageSizeEstimatorWriter(ArrayBufferWriter<byte> buffer)
	{
		var writer = MessageSizeEstimatorWriter;
		if (writer == null)
		{
			writer = new Utf8JsonWriter(buffer);
			MessageSizeEstimatorWriter = writer;
		}
		else
		{
			writer.Reset(buffer);
		}

		return writer;
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetContextStringValue(IMessageContext context, string propertyName)
	{
		var value = context.GetItem<object>(propertyName);
		return value as string ?? value?.ToString();
	}

	/// <summary>
	/// Records OpenTelemetry metrics.
	/// </summary>
	private static void RecordOpenTelemetryMetrics(
		in TagList tags,
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
	private void SetMetricsActivityTags(
		Activity? activity,
		ProcessingMetricsContext metricsContext,
		bool includeMessageSizes)
	{
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("dispatch.messaging.message_type", metricsContext.MessageType);
		_ = activity.SetTag("dispatch.messaging.message_kind", metricsContext.MessageKind);
		if (includeMessageSizes)
		{
			_ = activity.SetTag("dispatch.messaging.message_size", metricsContext.MessageSize);
		}

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
		ProcessingMetricsContext metricsContext,
		TimeSpan duration,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var isSuccess = exception == null;
		var durationMs = duration.TotalMilliseconds;

		try
		{
			// Record OpenTelemetry metrics
			if (_options.RecordOpenTelemetryMetrics)
			{
				var tags = new TagList
				{
					{ "message_type", metricsContext.MessageType },
					{ "message_kind", metricsContext.MessageKind },
					{ "tenant_id", TenantIdGuard.Guard(metricsContext.TenantId ?? "unknown") },
					{ "success", isSuccess },
					{ "queue_name", metricsContext.QueueName ?? "default" },
				};
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
				LogProcessingDetails(metricsContext, duration, startTime, endTime, exception);
			}

			LogMetricsCollectionComplete(_logger, metricsContext.MessageType, durationMs, isSuccess);
		}
		catch (Exception metricsException)
		{
			LogMetricsCollectionError(_logger, metricsException, metricsContext.MessageType);
		}
	}

	[SuppressMessage(
		"Security",
		"CA5394:Do not use insecure randomness",
		Justification = "Sampling is observability-only and not security sensitive.")]
	private bool ShouldCollectMetrics(string messageType)
	{
		if (_bypassMetricsTypes?.Contains(messageType) == true)
		{
			return false;
		}

		var sampleRate = _sampleRate;
		if (!(sampleRate > 0d))
		{
			return false;
		}

		if (sampleRate >= 1d)
		{
			return true;
		}

		return Random.Shared.NextDouble() <= sampleRate;
	}

	/// <summary>
	/// Logs detailed processing information.
	/// </summary>
	private void LogProcessingDetails(
		ProcessingMetricsContext metricsContext,
		TimeSpan duration,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		Exception? exception)
	{
		var logLevel = exception != null ? LogLevel.Warning : LogLevel.Information;
		if (!_logger.IsEnabled(logLevel))
		{
			return;
		}

		var correlationId = metricsContext.CorrelationId ?? "unknown";
		var tenantId = metricsContext.TenantId ?? "unknown";
		if (exception != null)
		{
			LogMessageProcessingError(
				_logger,
				exception,
				metricsContext.MessageType,
				metricsContext.MessageKind,
				correlationId,
				tenantId,
				duration.TotalMilliseconds,
				exception.Message,
				startTime,
				endTime);
		}
		else
		{
			LogMessageProcessingSuccess(
				_logger,
				metricsContext.MessageType,
				metricsContext.MessageKind,
				correlationId,
				tenantId,
				duration.TotalMilliseconds,
				metricsContext.MessageSize,
				startTime,
				endTime);
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
