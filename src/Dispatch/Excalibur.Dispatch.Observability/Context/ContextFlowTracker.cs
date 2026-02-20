// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Main tracking component that records context state at each pipeline stage, detects changes/losses in context fields, correlates context
/// across service boundaries, and tracks context lineage throughout the message processing lifecycle.
/// </summary>
public sealed partial class ContextFlowTracker : IContextFlowTracker, IDisposable, IAsyncDisposable
{
	private readonly ILogger<ContextFlowTracker> _logger;
	private readonly ContextObservabilityOptions _options;
	private readonly IContextFlowMetrics _metrics;
	private readonly ConcurrentDictionary<string, ContextSnapshot> _contextSnapshots;
	private readonly ConcurrentDictionary<string, ContextLineage> _contextLineage;
	private readonly ConcurrentDictionary<string, ContextSnapshot> _latestSnapshotByMessageId;
	private readonly ActivitySource _activitySource;
	private readonly Timer _cleanupTimer;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContextFlowTracker" /> class.
	/// </summary>
	/// <param name="logger"> Logger for diagnostic output. </param>
	/// <param name="options"> Configuration options for context observability. </param>
	/// <param name="metrics"> Metrics collector for context flow tracking. </param>
	public ContextFlowTracker(
		ILogger<ContextFlowTracker> logger,
		IOptions<ContextObservabilityOptions> options,
		IContextFlowMetrics metrics)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_contextSnapshots = new ConcurrentDictionary<string, ContextSnapshot>(StringComparer.Ordinal);
		_contextLineage = new ConcurrentDictionary<string, ContextLineage>(StringComparer.Ordinal);
		_latestSnapshotByMessageId = new ConcurrentDictionary<string, ContextSnapshot>(StringComparer.Ordinal);
		_activitySource = new ActivitySource(ContextObservabilityTelemetryConstants.ActivitySourceName, ContextObservabilityTelemetryConstants.Version);

		// Setup cleanup timer to remove old snapshots
		_cleanupTimer = new Timer(
			CleanupOldSnapshots,
			state: null,
			TimeSpan.FromMinutes(5),
			TimeSpan.FromMinutes(5));
	}

	/// <summary>
	/// Records the current state of a message context at a specific pipeline stage.
	/// </summary>
	/// <param name="context"> The message context to track. </param>
	/// <param name="stage"> The current pipeline stage. </param>
	/// <param name="metadata"> Additional metadata about the tracking point. </param>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public void RecordContextState(IMessageContext context, string stage, IReadOnlyDictionary<string, object>? metadata = null)
	{
		if (context == null)
		{
			LogNullContextAttempted(stage);
			return;
		}

		using var activity = _activitySource.StartActivity();
		_ = (activity?.SetTag("stage", stage));
		_ = (activity?.SetTag("message.id", context.MessageId));
		_ = (activity?.SetTag("correlation.id", context.CorrelationId));

		try
		{
			var snapshot = CreateSnapshot(context, stage, metadata);
			var key = GenerateSnapshotKey(context, stage);

			_ = _contextSnapshots.AddOrUpdate(key, static (_, arg) => arg, static (_, _, arg) => arg, snapshot);

			// Track lineage
			TrackLineage(context, snapshot);

			// Detect changes from previous stage (uses O(1) lookup via _latestSnapshotByMessageId)
			DetectContextChanges(context, stage);

			// Store as latest snapshot for this messageId for fast lookup in next stage
			var messageId = context.MessageId ?? "unknown";
			_latestSnapshotByMessageId[messageId] = snapshot;

			// Update metrics
			_metrics.RecordContextSnapshot(stage, snapshot.FieldCount, snapshot.SizeBytes);

			LogContextStateRecorded(context.MessageId ?? "unknown", stage, snapshot.FieldCount);
		}
		catch (InvalidOperationException ex)
		{
			LogRecordStateInvalidOperation(stage, ex);
			_metrics.RecordContextError("record_state", stage);
		}
		catch (ArgumentException ex)
		{
			LogRecordStateArgument(stage, ex);
			_metrics.RecordContextError("record_state", stage);
		}
	}

	/// <summary>
	/// Detects changes or losses in context fields between pipeline stages.
	/// </summary>
	/// <param name="context"> The current message context. </param>
	/// <param name="fromStage"> The previous pipeline stage. </param>
	/// <param name="toStage"> The current pipeline stage. </param>
	/// <returns> A collection of detected changes. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public IEnumerable<ContextChange> DetectChanges(IMessageContext context, string fromStage, string toStage)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(fromStage);
		ArgumentNullException.ThrowIfNull(toStage);

		var changes = new List<ContextChange>();

		try
		{
			var fromKey = GenerateSnapshotKey(context, fromStage);
			var toKey = GenerateSnapshotKey(context, toStage);

			if (_contextSnapshots.TryGetValue(fromKey, out var fromSnapshot) &&
				_contextSnapshots.TryGetValue(toKey, out var toSnapshot))
			{
				var fromFields = new HashSet<string>(fromSnapshot.Fields.Keys, StringComparer.Ordinal);
				var toFields = new HashSet<string>(toSnapshot.Fields.Keys, StringComparer.Ordinal);

				DetectRemovedFields(changes, fromFields, toFields, fromSnapshot, toStage);
				DetectAddedFields(changes, fromFields, toFields, toSnapshot, toStage);
				DetectModifiedFields(changes, fromFields, toFields, fromSnapshot, toSnapshot, toStage);

				if (changes.Count != 0)
				{
					LogContextChangesDetected(changes.Count, fromStage, toStage, context.MessageId ?? "unknown");
				}
			}
		}
		catch (InvalidOperationException ex)
		{
			LogDetectChangesInvalidOperation(fromStage, toStage, ex);
			_metrics.RecordContextError("detect_changes", toStage);
		}
		catch (ArgumentException ex)
		{
			LogDetectChangesArgument(fromStage, toStage, ex);
			_metrics.RecordContextError("detect_changes", toStage);
		}

		return changes;
	}

	/// <summary>
	/// Correlates context across service boundaries for distributed tracing.
	/// </summary>
	/// <param name="context"> The message context. </param>
	/// <param name="serviceBoundary"> The service boundary identifier. </param>
	public void CorrelateAcrossBoundary(IMessageContext context, string serviceBoundary)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(serviceBoundary);

		using var activity = _activitySource.StartActivity("CorrelateAcrossBoundary", ActivityKind.Client);
		_ = activity?.SetTag("service.boundary", serviceBoundary);
		_ = activity?.SetTag("correlation.id", context.CorrelationId);

		try
		{
			var correlationId = context.CorrelationId ?? context.MessageId ?? Guid.NewGuid().ToString();
			var originMessageId = context.MessageId;

			// Record the service boundary transition
			var lineage = _contextLineage.GetOrAdd(
				correlationId,
				static (key, arg) => new ContextLineage
				{
					CorrelationId = key,
					OriginMessageId = arg,
					StartTime = DateTimeOffset.UtcNow,
					Snapshots = [],
					ServiceBoundaries = [],
				},
				originMessageId);

			lineage.ServiceBoundaries.Add(new ServiceBoundaryTransition
			{
				ServiceName = serviceBoundary,
				Timestamp = DateTimeOffset.UtcNow,
				ContextPreserved = true,
			});

			_metrics.RecordCrossBoundaryTransition(serviceBoundary, contextPreserved: true);

			LogContextCorrelated(serviceBoundary, correlationId);
		}
		catch (InvalidOperationException ex)
		{
			LogCorrelateBoundaryInvalidOperation(serviceBoundary, ex);
		}
		catch (ArgumentException ex)
		{
			LogCorrelateBoundaryArgumentError(ex, serviceBoundary);
		}
	}

	/// <summary>
	/// Gets the complete context lineage for a specific correlation ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID. </param>
	/// <returns> The context lineage or null if not found. </returns>
	public ContextLineage? GetContextLineage(string correlationId) =>
		_contextLineage.GetValueOrDefault(correlationId);

	/// <summary>
	/// Gets all snapshots for a specific message.
	/// </summary>
	/// <param name="messageId"> The message ID. </param>
	/// <returns> Collection of snapshots for the message. </returns>
	public IEnumerable<ContextSnapshot> GetMessageSnapshots(string messageId) =>
		_contextSnapshots.Where(kvp => string.Equals(kvp.Value.MessageId, messageId, StringComparison.Ordinal))
						 .Select(kvp => kvp.Value)
						 .OrderBy(s => s.Timestamp);

	/// <summary>
	/// Validates that the context contains all required fields.
	/// </summary>
	/// <param name="context"> The message context to validate. </param>
	/// <returns> True if context is valid; otherwise false. </returns>
	public bool ValidateContextIntegrity(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var requiredFields = _options.Fields.RequiredContextFields ??
		[
			nameof(context.MessageId),
			nameof(context.CorrelationId),
			nameof(context.MessageType),
		];

		foreach (var field in requiredFields)
		{
			var value = field switch
			{
				nameof(context.MessageId) => context.MessageId,
				nameof(context.CorrelationId) => context.CorrelationId,
				nameof(context.MessageType) => context.MessageType,
				_ => context.Items.TryGetValue(field, out var v) ? v : null,
			};

			if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
			{
				LogContextIntegrityCheckFailed(field);
				return false;
			}
		}

		return true;
	}

	/// <summary> Releases all resources used by the <see cref="ContextFlowTracker"/>. </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		lock (_lock)
		{
			if (_disposed)
			{
				return;
			}

			_cleanupTimer?.Dispose();
			_activitySource?.Dispose();
			_contextSnapshots.Clear();
			_contextLineage.Clear();
			_latestSnapshotByMessageId.Clear();

			_disposed = true;
		}
	}

	/// <summary>
	/// Asynchronously releases resources, ensuring the cleanup timer callback has completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// DisposeAsync on Timer waits for any in-flight callback to complete
		await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
		_activitySource.Dispose();
		_contextSnapshots.Clear();
		_contextLineage.Clear();
		_latestSnapshotByMessageId.Clear();
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static bool AreValuesEqual(object? value1, object? value2)
	{
		if (ReferenceEquals(value1, value2))
		{
			return true;
		}

		if (value1 == null || value2 == null)
		{
			return false;
		}

		// Handle different types
		if (value1.GetType() != value2.GetType())
		{
			return false;
		}

		// Use JSON serialization for complex object comparison
		if (value1 is not string && !value1.GetType().IsPrimitive)
		{
			var json1 = JsonSerializer.Serialize(value1);
			var json2 = JsonSerializer.Serialize(value2);
			return string.Equals(json1, json2, StringComparison.Ordinal);
		}

		return value1.Equals(value2);
	}

	private static string GenerateSnapshotKey(IMessageContext context, string stage)
	{
		var messageId = context.MessageId ?? Guid.NewGuid().ToString();
		return $"{messageId}:{stage}:{DateTimeOffset.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)}";
	}

	private void DetectRemovedFields(
		List<ContextChange> changes,
		HashSet<string> fromFields,
		HashSet<string> toFields,
		ContextSnapshot fromSnapshot,
		string toStage)
	{
		foreach (var field in fromFields.Except(toFields, StringComparer.Ordinal))
		{
			changes.Add(new ContextChange
			{
				FieldName = field,
				ChangeType = ContextChangeType.Removed,
				FromValue = fromSnapshot.Fields[field],
				ToValue = null,
				Stage = toStage,
				Timestamp = DateTimeOffset.UtcNow,
			});

			_metrics.RecordContextMutation(ContextChangeType.Removed, field, toStage);
		}
	}

	private void DetectAddedFields(
		List<ContextChange> changes,
		HashSet<string> fromFields,
		HashSet<string> toFields,
		ContextSnapshot toSnapshot,
		string toStage)
	{
		foreach (var field in toFields.Except(fromFields, StringComparer.Ordinal))
		{
			changes.Add(new ContextChange
			{
				FieldName = field,
				ChangeType = ContextChangeType.Added,
				FromValue = null,
				ToValue = toSnapshot.Fields[field],
				Stage = toStage,
				Timestamp = DateTimeOffset.UtcNow,
			});

			_metrics.RecordContextMutation(ContextChangeType.Added, field, toStage);
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void DetectModifiedFields(
		List<ContextChange> changes,
		HashSet<string> fromFields,
		HashSet<string> toFields,
		ContextSnapshot fromSnapshot,
		ContextSnapshot toSnapshot,
		string toStage)
	{
		foreach (var field in fromFields.Intersect(toFields, StringComparer.Ordinal))
		{
			var fromValue = fromSnapshot.Fields[field];
			var toValue = toSnapshot.Fields[field];

			if (!AreValuesEqual(fromValue, toValue))
			{
				changes.Add(new ContextChange
				{
					FieldName = field,
					ChangeType = ContextChangeType.Modified,
					FromValue = fromValue,
					ToValue = toValue,
					Stage = toStage,
					Timestamp = DateTimeOffset.UtcNow,
				});

				_metrics.RecordContextMutation(ContextChangeType.Modified, field, toStage);
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private ContextSnapshot CreateSnapshot(IMessageContext context, string stage, IReadOnlyDictionary<string, object>? metadata)
	{
		var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			// Capture all context properties
			["MessageId"] = context.MessageId,
			["ExternalId"] = context.ExternalId,
			["UserId"] = context.UserId,
			["CorrelationId"] = context.CorrelationId,
			["CausationId"] = context.CausationId,
			["TraceParent"] = context.TraceParent,
			["SerializerVersion"] = context.SerializerVersion(),
			["MessageVersion"] = context.MessageVersion(),
			["ContractVersion"] = context.ContractVersion(),
			["DesiredVersion"] = context.DesiredVersion(),
			["TenantId"] = context.TenantId,
			["Source"] = context.Source,
			["MessageType"] = context.MessageType,
			["ContentType"] = context.ContentType,
			["DeliveryCount"] = context.DeliveryCount,
			["PartitionKey"] = context.PartitionKey(),
			["ReplyTo"] = context.ReplyTo(),
			["ReceivedTimestampUtc"] = context.ReceivedTimestampUtc,
			["SentTimestampUtc"] = context.SentTimestampUtc,
		};

		// Capture custom items (with size limit)
		if (_options.CaptureCustomItems)
		{
			foreach (var item in context.Items.Take(_options.Limits.MaxCustomItemsToCapture))
			{
				fields[$"Item_{item.Key}"] = item.Value;
			}
		}

		// Calculate snapshot size
		var json = JsonSerializer.Serialize(fields);
		var sizeBytes = Encoding.UTF8.GetByteCount(json);

		return new ContextSnapshot
		{
			MessageId = context.MessageId ?? string.Empty,
			Stage = stage,
			Timestamp = DateTimeOffset.UtcNow,
			Fields = fields,
			FieldCount = fields.Count,
			SizeBytes = sizeBytes,
			Metadata = metadata ?? new Dictionary<string, object>(StringComparer.Ordinal),
		};
	}

	private void TrackLineage(IMessageContext context, ContextSnapshot snapshot)
	{
		var correlationKey = context.CorrelationId ?? context.MessageId ?? Guid.NewGuid().ToString();
		var originMessageId = context.MessageId;

		var lineage = _contextLineage.AddOrUpdate(
			correlationKey,
			static (key, arg) => new ContextLineage
			{
				CorrelationId = key,
				OriginMessageId = arg.originMessageId,
				StartTime = DateTimeOffset.UtcNow,
				Snapshots = [arg.snapshot],
				ServiceBoundaries = [],
			},
			static (_, existing, arg) =>
			{
				existing.Snapshots.Add(arg.snapshot);
				return existing;
			},
			(originMessageId, snapshot));

		// Limit lineage history size
		if (lineage.Snapshots.Count > _options.Limits.MaxSnapshotsPerLineage)
		{
			lineage.Snapshots.RemoveAt(0);
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private void DetectContextChanges(IMessageContext context, string currentStage)
	{
		// O(1) lookup for the previous snapshot using the messageId index
		var messageId = context.MessageId ?? "unknown";
		if (_latestSnapshotByMessageId.TryGetValue(messageId, out var previousSnapshot) &&
			!string.Equals(previousSnapshot.Stage, currentStage, StringComparison.Ordinal))
		{
			foreach (var change in DetectChanges(context, previousSnapshot.Stage, currentStage))
			{
				LogContextChange(change, context.MessageId);
			}
		}
	}

	private void LogContextChange(ContextChange change, string? messageId)
	{
		if (change.ChangeType == ContextChangeType.Removed)
		{
			LogContextFieldRemovedWarning(change.FieldName, change.ChangeType, change.Stage, messageId, change.FromValue, change.ToValue);
		}
		else
		{
			LogContextFieldChangedDebug(change.FieldName, change.ChangeType, change.Stage, messageId, change.FromValue, change.ToValue);
		}
	}

	private void CleanupOldSnapshots(object? state)
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			var cutoffTime = DateTimeOffset.UtcNow.Subtract(_options.Limits.SnapshotRetentionPeriod);

			var keysToRemove = _contextSnapshots
				.Where(kvp => kvp.Value.Timestamp < cutoffTime)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				_ = _contextSnapshots.TryRemove(key, out _);
			}

			// Clean up old lineage data
			var lineageKeysToRemove = _contextLineage
				.Where(kvp => kvp.Value.StartTime < cutoffTime)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in lineageKeysToRemove)
			{
				_ = _contextLineage.TryRemove(key, out _);
			}

			if (keysToRemove.Count != 0 || lineageKeysToRemove.Count != 0)
			{
				LogSnapshotCleanup(keysToRemove.Count, lineageKeysToRemove.Count);
			}
		}
		catch (InvalidOperationException ex)
		{
			LogCleanupInvalidOperationError(ex);
		}
		catch (ArgumentException ex)
		{
			LogCleanupArgumentError(ex);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(ObservabilityEventId.NullContextAttempted, LogLevel.Warning,
		"Attempted to record null context at stage {Stage}")]
	private partial void LogNullContextAttempted(string stage);

	[LoggerMessage(ObservabilityEventId.ContextStateRecorded, LogLevel.Debug,
		"Recorded context state for message {MessageId} at stage {Stage} with {FieldCount} fields")]
	private partial void LogContextStateRecorded(string messageId, string stage, int fieldCount);

	[LoggerMessage(ObservabilityEventId.ContextChangesDetected, LogLevel.Information,
		"Detected {ChangeCount} context changes between stages {FromStage} and {ToStage} for message {MessageId}")]
	private partial void LogContextChangesDetected(int changeCount, string fromStage, string toStage, string messageId);

	[LoggerMessage(ObservabilityEventId.ContextCorrelated, LogLevel.Debug,
		"Correlated context across boundary {ServiceBoundary} for correlation {CorrelationId}")]
	private partial void LogContextCorrelated(string serviceBoundary, string correlationId);

	[LoggerMessage(ObservabilityEventId.CorrelateBoundaryArgumentError, LogLevel.Error,
		"Failed to correlate context across boundary {ServiceBoundary} - invalid argument")]
	private partial void LogCorrelateBoundaryArgumentError(Exception ex, string serviceBoundary);

	[LoggerMessage(ObservabilityEventId.ContextIntegrityCheckFailed, LogLevel.Warning,
		"Context integrity check failed: missing required field {Field}")]
	private partial void LogContextIntegrityCheckFailed(string field);

	[LoggerMessage(ObservabilityEventId.ContextFieldRemovedWarning, LogLevel.Warning,
		"Context field {FieldName} was {ChangeType} at stage {Stage} for message {MessageId}. Previous value: {FromValue}, New value: {ToValue}")]
	private partial void LogContextFieldRemovedWarning(string fieldName, ContextChangeType changeType, string stage, string? messageId, object? fromValue, object? toValue);

	[LoggerMessage(ObservabilityEventId.ContextFieldChangedDebug, LogLevel.Debug,
		"Context field {FieldName} was {ChangeType} at stage {Stage} for message {MessageId}. Previous value: {FromValue}, New value: {ToValue}")]
	private partial void LogContextFieldChangedDebug(string fieldName, ContextChangeType changeType, string stage, string? messageId, object? fromValue, object? toValue);

	[LoggerMessage(ObservabilityEventId.SnapshotCleanup, LogLevel.Debug,
		"Cleaned up {SnapshotCount} old snapshots and {LineageCount} old lineage records")]
	private partial void LogSnapshotCleanup(int snapshotCount, int lineageCount);

	[LoggerMessage(ObservabilityEventId.CleanupInvalidOperationError, LogLevel.Error,
		"Error during snapshot cleanup - invalid operation")]
	private partial void LogCleanupInvalidOperationError(Exception ex);

	[LoggerMessage(ObservabilityEventId.CleanupArgumentError, LogLevel.Error,
		"Error during snapshot cleanup - invalid argument")]
	private partial void LogCleanupArgumentError(Exception ex);

	[LoggerMessage(ObservabilityEventId.RecordStateInvalidOperation, LogLevel.Error,
		"Failed to record context state at stage {Stage} - invalid operation")]
	private partial void LogRecordStateInvalidOperation(string stage, Exception ex);

	[LoggerMessage(ObservabilityEventId.RecordStateArgument, LogLevel.Error,
		"Failed to record context state at stage {Stage} - invalid argument")]
	private partial void LogRecordStateArgument(string stage, Exception ex);

	[LoggerMessage(ObservabilityEventId.DetectChangesInvalidOperation, LogLevel.Error,
		"Failed to detect context changes between {FromStage} and {ToStage} - invalid operation")]
	private partial void LogDetectChangesInvalidOperation(string fromStage, string toStage, Exception ex);

	[LoggerMessage(ObservabilityEventId.DetectChangesArgument, LogLevel.Error,
		"Failed to detect context changes between {FromStage} and {ToStage} - invalid argument")]
	private partial void LogDetectChangesArgument(string fromStage, string toStage, Exception ex);

	[LoggerMessage(ObservabilityEventId.CorrelateBoundaryInvalidOperation, LogLevel.Error,
		"Failed to correlate context across boundary {ServiceBoundary} - invalid operation")]
	private partial void LogCorrelateBoundaryInvalidOperation(string serviceBoundary, Exception ex);
}
