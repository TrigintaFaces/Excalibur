// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.Tests.Core.Observability;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingTelemetryConstantsShould
{
	[Fact]
	public void DefineActivitySourceNames()
	{
		EventSourcingActivitySources.EventStore.ShouldBe("Excalibur.EventSourcing.EventStore");
		EventSourcingActivitySources.SnapshotStore.ShouldBe("Excalibur.EventSourcing.SnapshotStore");
	}

	[Fact]
	public void DefineMeterNames()
	{
		EventSourcingMeters.EventStore.ShouldBe("Excalibur.EventSourcing.EventStore");
		EventSourcingMeters.SnapshotStore.ShouldBe("Excalibur.EventSourcing.SnapshotStore");
	}

	[Fact]
	public void DefineActivityOperationNames()
	{
		EventSourcingActivities.Append.ShouldBe("EventStore.Append");
		EventSourcingActivities.Load.ShouldBe("EventStore.Load");
		EventSourcingActivities.GetUndispatched.ShouldBe("EventStore.GetUndispatched");
		EventSourcingActivities.MarkDispatched.ShouldBe("EventStore.MarkDispatched");
		EventSourcingActivities.SaveSnapshot.ShouldBe("SnapshotStore.Save");
		EventSourcingActivities.GetSnapshot.ShouldBe("SnapshotStore.Get");
		EventSourcingActivities.DeleteSnapshots.ShouldBe("SnapshotStore.Delete");
	}

	[Fact]
	public void DefineTagNames()
	{
		EventSourcingTags.AggregateId.ShouldBe("aggregate.id");
		EventSourcingTags.AggregateType.ShouldBe("aggregate.type");
		EventSourcingTags.Version.ShouldBe("event.version");
		EventSourcingTags.FromVersion.ShouldBe("from.version");
		EventSourcingTags.ExpectedVersion.ShouldBe("expected.version");
		EventSourcingTags.EventCount.ShouldBe("event.count");
		EventSourcingTags.EventId.ShouldBe("event.id");
		EventSourcingTags.BatchSize.ShouldBe("batch.size");
		EventSourcingTags.Provider.ShouldBe("store.provider");
		EventSourcingTags.OperationResult.ShouldBe("operation.result");
		EventSourcingTags.ExceptionType.ShouldBe("exception.type");
		EventSourcingTags.ExceptionMessage.ShouldBe("exception.message");
		EventSourcingTags.Operation.ShouldBe("store.operation");
	}

	[Fact]
	public void DefineMetricInstrumentNames()
	{
		EventSourcingMetricNames.EventStoreOperations.ShouldBe("excalibur.eventsourcing.eventstore.operations");
		EventSourcingMetricNames.EventStoreDuration.ShouldBe("excalibur.eventsourcing.eventstore.duration");
		EventSourcingMetricNames.SnapshotStoreOperations.ShouldBe("excalibur.eventsourcing.snapshotstore.operations");
		EventSourcingMetricNames.SnapshotStoreDuration.ShouldBe("excalibur.eventsourcing.snapshotstore.duration");
		EventSourcingMetricNames.EventsAppended.ShouldBe("excalibur.eventsourcing.eventstore.events_appended");
		EventSourcingMetricNames.EventsLoaded.ShouldBe("excalibur.eventsourcing.eventstore.events_loaded");
		EventSourcingMetricNames.AppendDuration.ShouldBe("excalibur.eventsourcing.eventstore.append_duration");
		EventSourcingMetricNames.LoadDuration.ShouldBe("excalibur.eventsourcing.eventstore.load_duration");
	}

	[Fact]
	public void DefineTagValues()
	{
		EventSourcingTagValues.Success.ShouldBe("success");
		EventSourcingTagValues.ConcurrencyConflict.ShouldBe("concurrency_conflict");
		EventSourcingTagValues.Failure.ShouldBe("failure");
		EventSourcingTagValues.NotFound.ShouldBe("not_found");
	}

	[Fact]
	public void UseConsistentNamingConventions()
	{
		// All metric names should follow OpenTelemetry naming: lowercase with dots
		EventSourcingMetricNames.EventStoreOperations.ShouldStartWith("excalibur.");
		EventSourcingMetricNames.EventStoreDuration.ShouldStartWith("excalibur.");
		EventSourcingMetricNames.SnapshotStoreOperations.ShouldStartWith("excalibur.");
		EventSourcingMetricNames.SnapshotStoreDuration.ShouldStartWith("excalibur.");

		// All tag names should use dot notation
		EventSourcingTags.AggregateId.ShouldContain(".");
		EventSourcingTags.AggregateType.ShouldContain(".");
	}
}
