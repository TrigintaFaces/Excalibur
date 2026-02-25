// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Observability;

/// <summary>
/// Activity source name constants for Event Sourcing telemetry.
/// </summary>
public static class EventSourcingActivitySources
{
	/// <summary>
	/// Activity source name for event store operations.
	/// </summary>
	public const string EventStore = "Excalibur.EventSourcing.EventStore";

	/// <summary>
	/// Activity source name for snapshot store operations.
	/// </summary>
	public const string SnapshotStore = "Excalibur.EventSourcing.SnapshotStore";
}

/// <summary>
/// Meter name constants for Event Sourcing metrics.
/// </summary>
public static class EventSourcingMeters
{
	/// <summary>
	/// Meter name for event store metrics.
	/// </summary>
	public const string EventStore = "Excalibur.EventSourcing.EventStore";

	/// <summary>
	/// Meter name for snapshot store metrics.
	/// </summary>
	public const string SnapshotStore = "Excalibur.EventSourcing.SnapshotStore";
}

/// <summary>
/// Activity operation name constants for Event Sourcing.
/// </summary>
public static class EventSourcingActivities
{
	/// <summary>
	/// Activity name for append event operation.
	/// </summary>
	public const string Append = "EventStore.Append";

	/// <summary>
	/// Activity name for load events operation.
	/// </summary>
	public const string Load = "EventStore.Load";

	/// <summary>
	/// Activity name for get undispatched events operation.
	/// </summary>
	public const string GetUndispatched = "EventStore.GetUndispatched";

	/// <summary>
	/// Activity name for mark event dispatched operation.
	/// </summary>
	public const string MarkDispatched = "EventStore.MarkDispatched";

	/// <summary>
	/// Activity name for save snapshot operation.
	/// </summary>
	public const string SaveSnapshot = "SnapshotStore.Save";

	/// <summary>
	/// Activity name for get snapshot operation.
	/// </summary>
	public const string GetSnapshot = "SnapshotStore.Get";

	/// <summary>
	/// Activity name for delete snapshots operation.
	/// </summary>
	public const string DeleteSnapshots = "SnapshotStore.Delete";
}

/// <summary>
/// Tag name constants following OpenTelemetry semantic conventions.
/// </summary>
public static class EventSourcingTags
{
	/// <summary>
	/// Aggregate identifier tag.
	/// </summary>
	public const string AggregateId = "aggregate.id";

	/// <summary>
	/// Aggregate type name tag.
	/// </summary>
	public const string AggregateType = "aggregate.type";

	/// <summary>
	/// Event/snapshot version tag.
	/// </summary>
	public const string Version = "event.version";

	/// <summary>
	/// From version for partial loading tag.
	/// </summary>
	public const string FromVersion = "from.version";

	/// <summary>
	/// Expected version for optimistic concurrency tag.
	/// </summary>
	public const string ExpectedVersion = "expected.version";

	/// <summary>
	/// Number of events tag.
	/// </summary>
	public const string EventCount = "event.count";

	/// <summary>
	/// Event identifier tag.
	/// </summary>
	public const string EventId = "event.id";

	/// <summary>
	/// Batch size tag.
	/// </summary>
	public const string BatchSize = "batch.size";

	/// <summary>
	/// Provider name tag.
	/// </summary>
	public const string Provider = "store.provider";

	/// <summary>
	/// Operation result tag.
	/// </summary>
	public const string OperationResult = "operation.result";

	/// <summary>
	/// Exception type tag.
	/// </summary>
	public const string ExceptionType = "exception.type";

	/// <summary>
	/// Exception message tag.
	/// </summary>
	public const string ExceptionMessage = "exception.message";

	/// <summary>
	/// Operation name tag (e.g., "load", "append", "save_snapshot").
	/// </summary>
	public const string Operation = "store.operation";
}

/// <summary>
/// Metric instrument name constants for Event Sourcing.
/// </summary>
public static class EventSourcingMetricNames
{
	/// <summary>
	/// Counter: number of event store operations.
	/// </summary>
	public const string EventStoreOperations = "excalibur.eventsourcing.eventstore.operations";

	/// <summary>
	/// Histogram: duration of event store operations in seconds.
	/// </summary>
	public const string EventStoreDuration = "excalibur.eventsourcing.eventstore.duration";

	/// <summary>
	/// Counter: number of snapshot store operations.
	/// </summary>
	public const string SnapshotStoreOperations = "excalibur.eventsourcing.snapshotstore.operations";

	/// <summary>
	/// Histogram: duration of snapshot store operations in seconds.
	/// </summary>
	public const string SnapshotStoreDuration = "excalibur.eventsourcing.snapshotstore.duration";

	/// <summary>
	/// Counter: total number of events appended to event stores.
	/// </summary>
	public const string EventsAppended = "excalibur.eventsourcing.eventstore.events_appended";

	/// <summary>
	/// Counter: total number of events loaded from event stores.
	/// </summary>
	public const string EventsLoaded = "excalibur.eventsourcing.eventstore.events_loaded";

	/// <summary>
	/// Histogram: duration of event store append operations in seconds.
	/// </summary>
	public const string AppendDuration = "excalibur.eventsourcing.eventstore.append_duration";

	/// <summary>
	/// Histogram: duration of event store load operations in seconds.
	/// </summary>
	public const string LoadDuration = "excalibur.eventsourcing.eventstore.load_duration";
}

/// <summary>
/// Tag value constants for operation results.
/// </summary>
public static class EventSourcingTagValues
{
	/// <summary>
	/// Success result value.
	/// </summary>
	public const string Success = "success";

	/// <summary>
	/// Concurrency conflict result value.
	/// </summary>
	public const string ConcurrencyConflict = "concurrency_conflict";

	/// <summary>
	/// Failure result value.
	/// </summary>
	public const string Failure = "failure";

	/// <summary>
	/// Not found result value.
	/// </summary>
	public const string NotFound = "not_found";
}
