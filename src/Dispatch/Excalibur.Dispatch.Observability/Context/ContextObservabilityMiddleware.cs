// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Middleware that instruments each pipeline stage with context observability, adding trace spans for context operations, recording context
/// snapshots, and detecting context mutations throughout the message processing pipeline.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ContextObservabilityMiddleware" /> class. </remarks>
/// <param name="logger"> Logger for diagnostic output. </param>
/// <param name="tracker"> Context flow tracker for recording context state. </param>
/// <param name="metrics"> Metrics collector for context flow. </param>
/// <param name="traceEnricher"> Trace enricher for adding context to spans. </param>
/// <param name="options"> Configuration options. </param>
public sealed partial class ContextObservabilityMiddleware(
	ILogger<ContextObservabilityMiddleware> logger,
	IContextFlowTracker tracker,
	IContextFlowMetrics metrics,
	IContextTraceEnricher traceEnricher,
	IOptions<ContextObservabilityOptions> options) : IDispatchMiddleware, IDisposable, IAsyncDisposable
{
	private static readonly CompositeFormat ContextIntegrityValidationFailedFormat =
		CompositeFormat.Parse(Resources.ContextObservabilityMiddleware_ContextIntegrityValidationFailedFormat);

	private static readonly CompositeFormat ContextSizeExceededFormat =
		CompositeFormat.Parse(Resources.ContextObservabilityMiddleware_ContextSizeExceededFormat);

	private readonly ILogger<ContextObservabilityMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IContextFlowTracker _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
	private readonly IContextFlowMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly IContextTraceEnricher _traceEnricher = traceEnricher ?? throw new ArgumentNullException(nameof(traceEnricher));
	private readonly ContextObservabilityOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ActivitySource _activitySource = new(ContextObservabilityTelemetryConstants.MiddlewareActivitySourceName, "1.0.0");

	/// <summary>
	/// Gets the stage in the dispatch pipeline where this middleware executes.
	/// </summary>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <summary>
	/// Invokes the middleware to track context flow through the pipeline.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="context"> The message context being processed. </param>
	/// <param name="nextDelegate"> The next middleware in the pipeline. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> The result of processing the message. </returns>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Context observability captures diagnostic snapshots and handles trimmed data defensively.")]
	[UnconditionalSuppressMessage(
		"Aot",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Context observability uses JSON serialization for diagnostics.")]
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
			// Observability disabled, pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var stageName = GetCurrentStageName(context);
		using var activity = _activitySource.StartActivity($"ContextObservability.{stageName}");

		// Enrich the activity with context information
		_traceEnricher.EnrichActivity(activity, context);

		var stopwatch = ValueStopwatch.StartNew();
		ContextSnapshot? beforeSnapshot = null;
		ContextSnapshot? afterSnapshot = null;

		try
		{
			// Capture context state before processing
			beforeSnapshot = CaptureContextSnapshot(context, $"{stageName}.Before");
			_tracker.RecordContextState(context, $"{stageName}.Before", GetStageMetadata(context));

			// Validate context integrity
			ValidateContextIntegrityAndFail(context, stageName);

			// Process the message
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			// Capture after state and record success
			afterSnapshot = ProcessSuccessPath(context, beforeSnapshot, stageName, stopwatch);

			return result;
		}
		catch (Exception ex)
		{
			HandlePipelineException(ex, context, stageName, stopwatch);
			throw;
		}
		finally
		{
			EmitDiagnosticEventIfEnabled(context, stageName, beforeSnapshot, afterSnapshot, GetElapsedMilliseconds(stopwatch));
		}
	}

	/// <summary> Releases all resources used by the <see cref="ContextObservabilityMiddleware"/>. </summary>
	public void Dispose() => _activitySource?.Dispose();

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_activitySource?.Dispose();
		return default;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static object? SerializeValue(object? value)
	{
		if (value == null)
		{
			return null;
		}

		// Handle primitive types directly
		if (value.GetType().IsPrimitive || value is string || value is decimal)
		{
			return value;
		}

		// Handle dates
		if (value is DateTime dt)
		{
			return dt.ToString("O");
		}

		if (value is DateTimeOffset dto)
		{
			return dto.ToString("O");
		}

		// For complex objects, serialize to JSON string
		try
		{
			return JsonSerializer.Serialize(value);
		}
		catch (NotSupportedException)
		{
			return value.ToString();
		}
		catch (ArgumentException)
		{
			return value.ToString();
		}
	}

	// R0.8: Unused parameter
#pragma warning disable RCS1163, IDE0060

	private static Dictionary<string, object> GetStageMetadata(IMessageContext context) =>
		new(StringComparer.Ordinal)
		{
			["thread_id"] = Environment.CurrentManagedThreadId,
			["machine_name"] = Environment.MachineName,
			["process_id"] = Environment.ProcessId,
			["timestamp_utc"] = DateTimeOffset.UtcNow.ToString("O"),
		};

#pragma warning restore RCS1163, IDE0060

	private void ValidateContextIntegrityAndFail(IMessageContext context, string stageName)
	{
		if (_options.ValidateContextIntegrity)
		{
			var isValid = _tracker.ValidateContextIntegrity(context);
			if (!isValid)
			{
				LogContextIntegrityValidationFailed(stageName, context.MessageId);

				_metrics.RecordContextValidationFailure(stageName);

				if (_options.FailOnIntegrityViolation)
				{
					throw new ContextIntegrityException(
						string.Format(
							CultureInfo.CurrentCulture,
							ContextIntegrityValidationFailedFormat,
							stageName));
				}
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private ContextSnapshot ProcessSuccessPath(
		IMessageContext context,
		ContextSnapshot? beforeSnapshot,
		string stageName,
		ValueStopwatch stopwatch)
	{
		// Capture context state after processing
		var afterSnapshot = CaptureContextSnapshot(context, $"{stageName}.After");
		_tracker.RecordContextState(context, $"{stageName}.After", GetStageMetadata(context));

		// Detect mutations
		DetectAndRecordMutations(context, beforeSnapshot, afterSnapshot, stageName);

		// Check for size threshold violations
		CheckContextSize(afterSnapshot, stageName);

		// Record success metrics
		_metrics.RecordPipelineStageLatency(stageName, GetElapsedMilliseconds(stopwatch));
		_metrics.RecordContextPreservationSuccess(stageName);

		return afterSnapshot;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void HandlePipelineException(Exception ex, IMessageContext context, string stageName, ValueStopwatch stopwatch)
	{
		// Record failure metrics
		_metrics.RecordPipelineStageLatency(stageName, GetElapsedMilliseconds(stopwatch));
		_metrics.RecordContextError("pipeline_exception", stageName);

		// Log the exception with context details
		LogPipelineStageException(ex, stageName, context.MessageId, context.CorrelationId);

		// Capture error state if enabled
		if (_options.CaptureErrorStates)
		{
			var errorMetadata = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["error_type"] = ex.GetType().Name,
				["error_message"] = ex.Message,
				["stack_trace"] = _options.Tracing.IncludeStackTraceInErrors ? ex.StackTrace ?? string.Empty : "[redacted]",
			};

			_tracker.RecordContextState(context, $"{stageName}.Error", errorMetadata);
		}
	}

	private void EmitDiagnosticEventIfEnabled(
		IMessageContext context,
		string stageName,
		ContextSnapshot? beforeSnapshot,
		ContextSnapshot? afterSnapshot,
		long elapsedMilliseconds)
	{
		if (_options.EmitDiagnosticEvents)
		{
			EmitDiagnosticEvent(context, stageName, beforeSnapshot, afterSnapshot, elapsedMilliseconds);
		}
	}

	private static long GetElapsedMilliseconds(ValueStopwatch stopwatch) => stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond;

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private ContextSnapshot CaptureContextSnapshot(IMessageContext context, string stage)
	{
		var fields = new Dictionary<string, object?>(StringComparer.Ordinal);

		CaptureContextProperties(context, fields);
		CaptureCustomItems(context, fields);

		var json = JsonSerializer.Serialize(fields);
		var sizeBytes = Encoding.UTF8.GetByteCount(json);

		return new ContextSnapshot
		{
			MessageId = context.MessageId ?? "unknown",
			Stage = stage,
			Timestamp = DateTimeOffset.UtcNow,
			Fields = fields,
			FieldCount = fields.Count,
			SizeBytes = sizeBytes,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
		};
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void CaptureContextProperties(IMessageContext context, Dictionary<string, object?> fields)
	{
		foreach (var prop in typeof(IMessageContext).GetProperties())
		{
			try
			{
				var value = prop.GetValue(context);
				if (value != null || _options.Tracing.IncludeNullFields)
				{
					fields[prop.Name] = SerializeValue(value);
				}
			}
			catch (InvalidOperationException ex)
			{
				LogPropertyCaptureInvalidOperation(ex, prop.Name);
				fields[prop.Name] = "[error capturing value]";
			}
			catch (ArgumentException ex)
			{
				LogPropertyCaptureInvalidArgument(ex, prop.Name);
				fields[prop.Name] = "[error capturing value]";
			}
			catch (TargetParameterCountException ex)
			{
				LogPropertyCaptureParameterMismatch(ex, prop.Name);
				fields[prop.Name] = "[error capturing value]";
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void CaptureCustomItems(IMessageContext context, Dictionary<string, object?> fields)
	{
		if (_options.CaptureCustomItems && context.Items.Any())
		{
			var itemsToCapture = context.Items
				.Take(_options.Limits.MaxCustomItemsToCapture)
				.ToDictionary(kvp => $"Item_{kvp.Key}", kvp => SerializeValue(kvp.Value), StringComparer.Ordinal);

			foreach (var item in itemsToCapture)
			{
				fields[item.Key] = item.Value;
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void DetectAndRecordMutations(
		IMessageContext context,
		ContextSnapshot? before,
		ContextSnapshot? after,
		string stage)
	{
		if (before == null || after == null)
		{
			return;
		}

		var mutations = new List<ContextMutation>();

		DetectAddedFields(before, after, stage, mutations);
		DetectRemovedFields(context, before, after, stage, mutations);
		DetectModifiedFields(before, after, stage, mutations);

		// Store mutations in context for diagnostic purposes
		if (mutations.Count != 0 && _options.Tracing.StoreMutationsInContext)
		{
			var mutationKey = $"ContextMutations_{stage}";
			context.SetItem(mutationKey, mutations);
		}
	}

	private void DetectAddedFields(ContextSnapshot before, ContextSnapshot after, string stage, List<ContextMutation> mutations)
	{
		foreach (var field in after.Fields.Keys.Except(before.Fields.Keys, StringComparer.Ordinal))
		{
			mutations.Add(new ContextMutation { Field = field, Type = MutationType.Added, NewValue = after.Fields[field], Stage = stage });

			_metrics.RecordContextMutation(ContextChangeType.Added, field, stage);
		}
	}

	private void DetectRemovedFields(IMessageContext context, ContextSnapshot before, ContextSnapshot after, string stage,
		List<ContextMutation> mutations)
	{
		foreach (var field in before.Fields.Keys.Except(after.Fields.Keys, StringComparer.Ordinal))
		{
			mutations.Add(
				new ContextMutation { Field = field, Type = MutationType.Removed, OldValue = before.Fields[field], Stage = stage });

			_metrics.RecordContextMutation(ContextChangeType.Removed, field, stage);

			// Log warning for critical field removal
			if (_options.Fields.CriticalFields?.Contains(field, StringComparer.Ordinal) == true)
			{
				LogCriticalFieldRemoved(field, stage, context.MessageId);
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void DetectModifiedFields(ContextSnapshot before, ContextSnapshot after, string stage, List<ContextMutation> mutations)
	{
		foreach (var field in before.Fields.Keys.Intersect(after.Fields.Keys, StringComparer.Ordinal))
		{
			var beforeValue = JsonSerializer.Serialize(before.Fields[field]);
			var afterValue = JsonSerializer.Serialize(after.Fields[field]);

			if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
			{
				mutations.Add(new ContextMutation
				{
					Field = field,
					Type = MutationType.Modified,
					OldValue = before.Fields[field],
					NewValue = after.Fields[field],
					Stage = stage,
				});

				_metrics.RecordContextMutation(ContextChangeType.Modified, field, stage);

				// Log info for tracked field modifications
				if (_options.Fields.TrackedFields?.Contains(field, StringComparer.Ordinal) == true)
				{
					LogTrackedFieldModified(field, stage, beforeValue, afterValue);
				}
			}
		}
	}

	private void CheckContextSize(ContextSnapshot snapshot, string stage)
	{
		if (snapshot.SizeBytes > _options.Limits.MaxContextSizeBytes)
		{
			LogContextSizeExceeded(snapshot.SizeBytes, _options.Limits.MaxContextSizeBytes, stage, snapshot.MessageId);
			_metrics.RecordContextSizeThresholdExceeded(stage, snapshot.SizeBytes);

			if (_options.Limits.FailOnSizeThresholdExceeded)
			{
				throw new ContextSizeExceededException(
					string.Format(
						CultureInfo.InvariantCulture,
						ContextSizeExceededFormat,
						snapshot.SizeBytes,
						_options.Limits.MaxContextSizeBytes));
			}
		}

		_metrics.RecordContextSize(stage, snapshot.SizeBytes);
	}

	private void EmitDiagnosticEvent(
		IMessageContext context,
		string stage,
		ContextSnapshot? before,
		ContextSnapshot? after,
		long elapsedMs)
	{
		var diagnosticEvent = new ContextFlowDiagnosticEvent
		{
			MessageId = context.MessageId,
			CorrelationId = context.CorrelationId,
			Stage = stage,
			Timestamp = DateTimeOffset.UtcNow,
			ElapsedMilliseconds = elapsedMs,
			FieldCountBefore = before?.FieldCount ?? 0,
			FieldCountAfter = after?.FieldCount ?? 0,
			SizeBytesBefore = before?.SizeBytes ?? 0,
			SizeBytesAfter = after?.SizeBytes ?? 0,
			IntegrityValid = _tracker.ValidateContextIntegrity(context),
		};

		LogContextFlowDiagnostic(
			diagnosticEvent.Stage,
			diagnosticEvent.MessageId,
			diagnosticEvent.FieldCountBefore,
			diagnosticEvent.FieldCountAfter,
			diagnosticEvent.SizeBytesBefore,
			diagnosticEvent.SizeBytesAfter,
			diagnosticEvent.ElapsedMilliseconds);
	}

	private string GetCurrentStageName(IMessageContext context)
	{
		// Try to get stage from context items
		if (context.ContainsItem("PipelineStage"))
		{
			return context.GetItem<string>("PipelineStage") ?? "Unknown";
		}

		// Fallback to middleware stage
		return Stage?.ToString() ?? "Unknown";
	}

	// Source-generated logging methods
	[LoggerMessage(ObservabilityEventId.ContextIntegrityValidationFailed, LogLevel.Warning,
		"Context integrity validation failed at stage {Stage} for message {MessageId}")]
	private partial void LogContextIntegrityValidationFailed(string stage, string? messageId);

	[LoggerMessage(ObservabilityEventId.PipelineStageException, LogLevel.Error,
		"Exception in pipeline stage {Stage} for message {MessageId} with correlation {CorrelationId}")]
	private partial void LogPipelineStageException(Exception ex, string stage, string? messageId, string? correlationId);

	[LoggerMessage(ObservabilityEventId.PropertyCaptureInvalidOperation, LogLevel.Debug,
		"Failed to capture property {PropertyName} - invalid operation")]
	private partial void LogPropertyCaptureInvalidOperation(Exception ex, string propertyName);

	[LoggerMessage(ObservabilityEventId.PropertyCaptureInvalidArgument, LogLevel.Debug,
		"Failed to capture property {PropertyName} - invalid argument")]
	private partial void LogPropertyCaptureInvalidArgument(Exception ex, string propertyName);

	[LoggerMessage(ObservabilityEventId.PropertyCaptureParameterMismatch, LogLevel.Debug,
		"Failed to capture property {PropertyName} - parameter count mismatch")]
	private partial void LogPropertyCaptureParameterMismatch(Exception ex, string propertyName);

	[LoggerMessage(ObservabilityEventId.CriticalFieldRemoved, LogLevel.Warning,
		"Critical context field {Field} was removed at stage {Stage} for message {MessageId}")]
	private partial void LogCriticalFieldRemoved(string field, string stage, string? messageId);

	[LoggerMessage(ObservabilityEventId.TrackedFieldModified, LogLevel.Information,
		"Tracked context field {Field} was modified at stage {Stage}: {OldValue} -> {NewValue}")]
	private partial void LogTrackedFieldModified(string field, string stage, string oldValue, string newValue);

	[LoggerMessage(ObservabilityEventId.ContextSizeExceeded, LogLevel.Warning,
		"Context size {SizeBytes} exceeds threshold {MaxSize} at stage {Stage} for message {MessageId}")]
	private partial void LogContextSizeExceeded(int sizeBytes, int maxSize, string stage, string messageId);

	[LoggerMessage(ObservabilityEventId.ContextFlowDiagnostic, LogLevel.Debug,
		"ContextFlow diagnostic: Stage={Stage}, MessageId={MessageId}, FieldsBefore={FieldsBefore}, FieldsAfter={FieldsAfter}, SizeBefore={SizeBefore}, SizeAfter={SizeAfter}, ElapsedMs={ElapsedMs}")]
	private partial void LogContextFlowDiagnostic(string stage, string? messageId, int fieldsBefore, int fieldsAfter, int sizeBefore,
		int sizeAfter, long elapsedMs);
}
