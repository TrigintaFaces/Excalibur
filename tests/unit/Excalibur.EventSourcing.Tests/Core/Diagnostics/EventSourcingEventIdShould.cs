// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Core.Diagnostics;

/// <summary>
/// Unit tests for <see cref="EventSourcingEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Priority", "0")]
public sealed class EventSourcingEventIdShould : UnitTestBase
{
	#region Event Store Core Event ID Tests (110000-110099)

	[Fact]
	public void HaveEventStoreCreatedInEventStoreCoreRange()
	{
		EventSourcingEventId.EventStoreCreated.ShouldBe(110000);
	}

	[Fact]
	public void HaveAllEventStoreCoreEventIdsInExpectedRange()
	{
		EventSourcingEventId.EventStoreCreated.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.EventsAppended.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.EventsLoaded.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.StreamCreated.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.StreamRead.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.StreamDeleted.ShouldBeInRange(110000, 110099);
		EventSourcingEventId.GlobalPositionRetrieved.ShouldBeInRange(110000, 110099);
	}

	#endregion

	#region Event Store Providers Event ID Tests (110100-110199)

	[Fact]
	public void HaveSqlServerEventStoreCreatedInProvidersRange()
	{
		EventSourcingEventId.SqlServerEventStoreCreated.ShouldBe(110100);
	}

	[Fact]
	public void HaveAllEventStoreProvidersEventIdsInExpectedRange()
	{
		EventSourcingEventId.SqlServerEventStoreCreated.ShouldBeInRange(110100, 110199);
		EventSourcingEventId.CosmosDbEventStoreCreated.ShouldBeInRange(110100, 110199);
		EventSourcingEventId.DynamoDbEventStoreCreated.ShouldBeInRange(110100, 110199);
		EventSourcingEventId.FirestoreEventStoreCreated.ShouldBeInRange(110100, 110199);
		EventSourcingEventId.InMemoryEventStoreCreated.ShouldBeInRange(110100, 110199);
	}

	#endregion

	#region Event Serialization Event ID Tests (110200-110299)

	[Fact]
	public void HaveEventSerializedInSerializationRange()
	{
		EventSourcingEventId.EventSerialized.ShouldBe(110200);
	}

	[Fact]
	public void HaveAllEventSerializationEventIdsInExpectedRange()
	{
		EventSourcingEventId.EventSerialized.ShouldBeInRange(110200, 110299);
		EventSourcingEventId.EventDeserialized.ShouldBeInRange(110200, 110299);
		EventSourcingEventId.EventSerializationFailed.ShouldBeInRange(110200, 110299);
		EventSourcingEventId.EventTypeResolved.ShouldBeInRange(110200, 110299);
	}

	#endregion

	#region Aggregate Repository Core Event ID Tests (111000-111099)

	[Fact]
	public void HaveAggregateRepositoryCreatedInAggregateRepositoryRange()
	{
		EventSourcingEventId.AggregateRepositoryCreated.ShouldBe(111000);
	}

	[Fact]
	public void HaveAllAggregateRepositoryCoreEventIdsInExpectedRange()
	{
		EventSourcingEventId.AggregateRepositoryCreated.ShouldBeInRange(111000, 111099);
		EventSourcingEventId.AggregateLoaded.ShouldBeInRange(111000, 111099);
		EventSourcingEventId.AggregateSaved.ShouldBeInRange(111000, 111099);
		EventSourcingEventId.AggregateNotFound.ShouldBeInRange(111000, 111099);
		EventSourcingEventId.AggregateCreated.ShouldBeInRange(111000, 111099);
		EventSourcingEventId.AggregateVersionChecked.ShouldBeInRange(111000, 111099);
	}

	#endregion

	#region Aggregate Operations Event ID Tests (111100-111199)

	[Fact]
	public void HaveDomainEventAppliedInAggregateOperationsRange()
	{
		EventSourcingEventId.DomainEventApplied.ShouldBe(111100);
	}

	[Fact]
	public void HaveAllAggregateOperationsEventIdsInExpectedRange()
	{
		EventSourcingEventId.DomainEventApplied.ShouldBeInRange(111100, 111199);
		EventSourcingEventId.DomainEventsCleared.ShouldBeInRange(111100, 111199);
		EventSourcingEventId.AggregateHydrated.ShouldBeInRange(111100, 111199);
		EventSourcingEventId.AggregateStateReconstructed.ShouldBeInRange(111100, 111199);
		EventSourcingEventId.ConcurrencyConflict.ShouldBeInRange(111100, 111199);
	}

	#endregion

	#region Snapshot Core Event ID Tests (112000-112099)

	[Fact]
	public void HaveSnapshotStoreCreatedInSnapshotCoreRange()
	{
		EventSourcingEventId.SnapshotStoreCreated.ShouldBe(112000);
	}

	[Fact]
	public void HaveAllSnapshotCoreEventIdsInExpectedRange()
	{
		EventSourcingEventId.SnapshotStoreCreated.ShouldBeInRange(112000, 112099);
		EventSourcingEventId.SnapshotSaved.ShouldBeInRange(112000, 112099);
		EventSourcingEventId.SnapshotLoaded.ShouldBeInRange(112000, 112099);
		EventSourcingEventId.SnapshotNotFound.ShouldBeInRange(112000, 112099);
		EventSourcingEventId.SnapshotDeleted.ShouldBeInRange(112000, 112099);
	}

	#endregion

	#region Snapshot Strategy Event ID Tests (112100-112199)

	[Fact]
	public void HaveSnapshotStrategyEvaluatedInSnapshotStrategyRange()
	{
		EventSourcingEventId.SnapshotStrategyEvaluated.ShouldBe(112100);
	}

	[Fact]
	public void HaveAllSnapshotStrategyEventIdsInExpectedRange()
	{
		EventSourcingEventId.SnapshotStrategyEvaluated.ShouldBeInRange(112100, 112199);
		EventSourcingEventId.SnapshotThresholdReached.ShouldBeInRange(112100, 112199);
		EventSourcingEventId.SnapshotCreationTriggered.ShouldBeInRange(112100, 112199);
		EventSourcingEventId.SnapshotSkipped.ShouldBeInRange(112100, 112199);
	}

	#endregion

	#region Projection Core Event ID Tests (113000-113099)

	[Fact]
	public void HaveProjectionManagerCreatedInProjectionCoreRange()
	{
		EventSourcingEventId.ProjectionManagerCreated.ShouldBe(113000);
	}

	[Fact]
	public void HaveAllProjectionCoreEventIdsInExpectedRange()
	{
		EventSourcingEventId.ProjectionManagerCreated.ShouldBeInRange(113000, 113099);
		EventSourcingEventId.ProjectionStarted.ShouldBeInRange(113000, 113099);
		EventSourcingEventId.ProjectionStopped.ShouldBeInRange(113000, 113099);
		EventSourcingEventId.ProjectionEventProcessed.ShouldBeInRange(113000, 113099);
		EventSourcingEventId.ProjectionCheckpointSaved.ShouldBeInRange(113000, 113099);
	}

	#endregion

	#region Projection Operations Event ID Tests (113100-113199)

	[Fact]
	public void HaveProjectionRebuiltInProjectionOperationsRange()
	{
		EventSourcingEventId.ProjectionRebuilt.ShouldBe(113100);
	}

	[Fact]
	public void HaveAllProjectionOperationsEventIdsInExpectedRange()
	{
		EventSourcingEventId.ProjectionRebuilt.ShouldBeInRange(113100, 113199);
		EventSourcingEventId.ProjectionCaughtUp.ShouldBeInRange(113100, 113199);
		EventSourcingEventId.ProjectionBehind.ShouldBeInRange(113100, 113199);
		EventSourcingEventId.ProjectionError.ShouldBeInRange(113100, 113199);
		EventSourcingEventId.ProjectionBatchProcessed.ShouldBeInRange(113100, 113199);
	}

	#endregion

	#region Upcasting Core Event ID Tests (114000-114099)

	[Fact]
	public void HaveUpcasterRegistryCreatedInUpcastingCoreRange()
	{
		EventSourcingEventId.UpcasterRegistryCreated.ShouldBe(114000);
	}

	[Fact]
	public void HaveAllUpcastingCoreEventIdsInExpectedRange()
	{
		EventSourcingEventId.UpcasterRegistryCreated.ShouldBeInRange(114000, 114099);
		EventSourcingEventId.EventUpcasted.ShouldBeInRange(114000, 114099);
		EventSourcingEventId.UpcasterChainExecuted.ShouldBeInRange(114000, 114099);
		EventSourcingEventId.UpcasterRegistered.ShouldBeInRange(114000, 114099);
		EventSourcingEventId.UpcastingSkipped.ShouldBeInRange(114000, 114099);
	}

	#endregion

	#region Event Migration Event ID Tests (114100-114199)

	[Fact]
	public void HaveEventMigrationStartedInEventMigrationRange()
	{
		EventSourcingEventId.EventMigrationStarted.ShouldBe(114100);
	}

	[Fact]
	public void HaveAllEventMigrationEventIdsInExpectedRange()
	{
		EventSourcingEventId.EventMigrationStarted.ShouldBeInRange(114100, 114199);
		EventSourcingEventId.EventMigrationCompleted.ShouldBeInRange(114100, 114199);
		EventSourcingEventId.EventMigrated.ShouldBeInRange(114100, 114199);
		EventSourcingEventId.EventMigrationFailed.ShouldBeInRange(114100, 114199);
		EventSourcingEventId.SchemaVersionUpdated.ShouldBeInRange(114100, 114199);
	}

	#endregion

	#region Event Version Manager Event ID Tests (114200-114299)

	[Fact]
	public void HaveEventUpgraderRegisteredInVersionManagerRange()
	{
		EventSourcingEventId.EventUpgraderRegistered.ShouldBe(114200);
	}

	[Fact]
	public void HaveAllEventVersionManagerEventIdsInExpectedRange()
	{
		EventSourcingEventId.EventUpgraderRegistered.ShouldBeInRange(114200, 114299);
		EventSourcingEventId.EventUpgrading.ShouldBeInRange(114200, 114299);
	}

	#endregion

	#region Cloud Event Stores Event ID Tests (114300-114399)

	[Fact]
	public void HaveCloudStoreAppendingEventsInCloudEventStoresRange()
	{
		EventSourcingEventId.CloudStoreAppendingEvents.ShouldBe(114300);
	}

	[Fact]
	public void HaveAllCloudEventStoresEventIdsInExpectedRange()
	{
		EventSourcingEventId.CloudStoreAppendingEvents.ShouldBeInRange(114300, 114399);
		EventSourcingEventId.CloudStoreEventsAppended.ShouldBeInRange(114300, 114399);
		EventSourcingEventId.CloudStoreConcurrencyConflict.ShouldBeInRange(114300, 114399);
		EventSourcingEventId.CloudStoreLoadingEvents.ShouldBeInRange(114300, 114399);
		EventSourcingEventId.CloudStoreInitializing.ShouldBeInRange(114300, 114399);
		EventSourcingEventId.CloudStoreLoadedEvents.ShouldBeInRange(114300, 114399);
	}

	#endregion

	#region GDPR Erasure Event ID Tests (114500-114599)

	[Fact]
	public void HaveErasureContributorStartingInErasureRange()
	{
		EventSourcingEventId.ErasureContributorStarting.ShouldBe(114500);
	}

	[Fact]
	public void HaveAllGdprErasureEventIdsInExpectedRange()
	{
		EventSourcingEventId.ErasureContributorStarting.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureNoAggregatesFound.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureAggregatesResolved.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureAggregateAlreadyErased.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureAggregateCompleted.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureSnapshotsDeleted.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureAggregateFailed.ShouldBeInRange(114500, 114599);
		EventSourcingEventId.ErasureContributorCompleted.ShouldBeInRange(114500, 114599);
	}

	#endregion

	#region Event Sourcing Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInEventSourcingReservedRange()
	{
		// Event Sourcing reserved range is 110000-114999
		var allEventIds = GetAllEventSourcingEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(110000, 114999,
				$"Event ID {eventId} is outside Event Sourcing reserved range (110000-114999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllEventSourcingEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllEventSourcingEventIds();
		allEventIds.Length.ShouldBeGreaterThan(60);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllEventSourcingEventIds()
	{
		return
		[
			// Event Store Core (110000-110099)
			EventSourcingEventId.EventStoreCreated,
			EventSourcingEventId.EventsAppended,
			EventSourcingEventId.EventsLoaded,
			EventSourcingEventId.StreamCreated,
			EventSourcingEventId.StreamRead,
			EventSourcingEventId.StreamDeleted,
			EventSourcingEventId.GlobalPositionRetrieved,

			// Event Store Providers (110100-110199)
			EventSourcingEventId.SqlServerEventStoreCreated,
			EventSourcingEventId.CosmosDbEventStoreCreated,
			EventSourcingEventId.DynamoDbEventStoreCreated,
			EventSourcingEventId.FirestoreEventStoreCreated,
			EventSourcingEventId.InMemoryEventStoreCreated,

			// Event Serialization (110200-110299)
			EventSourcingEventId.EventSerialized,
			EventSourcingEventId.EventDeserialized,
			EventSourcingEventId.EventSerializationFailed,
			EventSourcingEventId.EventTypeResolved,

			// Aggregate Repository Core (111000-111099)
			EventSourcingEventId.AggregateRepositoryCreated,
			EventSourcingEventId.AggregateLoaded,
			EventSourcingEventId.AggregateSaved,
			EventSourcingEventId.AggregateNotFound,
			EventSourcingEventId.AggregateCreated,
			EventSourcingEventId.AggregateVersionChecked,

			// Aggregate Operations (111100-111199)
			EventSourcingEventId.DomainEventApplied,
			EventSourcingEventId.DomainEventsCleared,
			EventSourcingEventId.AggregateHydrated,
			EventSourcingEventId.AggregateStateReconstructed,
			EventSourcingEventId.ConcurrencyConflict,

			// Snapshot Core (112000-112099)
			EventSourcingEventId.SnapshotStoreCreated,
			EventSourcingEventId.SnapshotSaved,
			EventSourcingEventId.SnapshotLoaded,
			EventSourcingEventId.SnapshotNotFound,
			EventSourcingEventId.SnapshotDeleted,

			// Snapshot Strategy (112100-112199)
			EventSourcingEventId.SnapshotStrategyEvaluated,
			EventSourcingEventId.SnapshotThresholdReached,
			EventSourcingEventId.SnapshotCreationTriggered,
			EventSourcingEventId.SnapshotSkipped,

			// Projection Core (113000-113099)
			EventSourcingEventId.ProjectionManagerCreated,
			EventSourcingEventId.ProjectionStarted,
			EventSourcingEventId.ProjectionStopped,
			EventSourcingEventId.ProjectionEventProcessed,
			EventSourcingEventId.ProjectionCheckpointSaved,

			// Projection Operations (113100-113199)
			EventSourcingEventId.ProjectionRebuilt,
			EventSourcingEventId.ProjectionCaughtUp,
			EventSourcingEventId.ProjectionBehind,
			EventSourcingEventId.ProjectionError,
			EventSourcingEventId.ProjectionBatchProcessed,

			// Upcasting Core (114000-114099)
			EventSourcingEventId.UpcasterRegistryCreated,
			EventSourcingEventId.EventUpcasted,
			EventSourcingEventId.UpcasterChainExecuted,
			EventSourcingEventId.UpcasterRegistered,
			EventSourcingEventId.UpcastingSkipped,

			// Event Migration (114100-114199)
			EventSourcingEventId.EventMigrationStarted,
			EventSourcingEventId.EventMigrationCompleted,
			EventSourcingEventId.EventMigrated,
			EventSourcingEventId.EventMigrationFailed,
			EventSourcingEventId.SchemaVersionUpdated,

			// Event Version Manager (114200-114299)
			EventSourcingEventId.EventUpgraderRegistered,
			EventSourcingEventId.EventUpgrading,

			// Cloud Event Stores (114300-114399)
			EventSourcingEventId.CloudStoreAppendingEvents,
			EventSourcingEventId.CloudStoreEventsAppended,
			EventSourcingEventId.CloudStoreConcurrencyConflict,
			EventSourcingEventId.CloudStoreLoadingEvents,
			EventSourcingEventId.CloudStoreInitializing,
			EventSourcingEventId.CloudStoreLoadedEvents,

			// Schema Migration (114400-114499)
			EventSourcingEventId.MigratorCreated,
			EventSourcingEventId.MigrationStarted,
			EventSourcingEventId.MigrationCompleted,
			EventSourcingEventId.MigrationFailed,
			EventSourcingEventId.MigrationApplied,
			EventSourcingEventId.RollbackStarted,
			EventSourcingEventId.RollbackCompleted,
			EventSourcingEventId.RollbackFailed,
			EventSourcingEventId.MigrationLockAcquired,
			EventSourcingEventId.MigrationLockReleased,
			EventSourcingEventId.MigrationLockFailed,
			EventSourcingEventId.MigrationHistoryCreated,
			EventSourcingEventId.PendingMigrationsFound,
			EventSourcingEventId.NoPendingMigrations,

			// Snapshot Version Manager (114250-114299)
			EventSourcingEventId.SnapshotUpgraderRegistered,
			EventSourcingEventId.SnapshotUpgrading,

			// GDPR Erasure (114500-114599)
			EventSourcingEventId.ErasureContributorStarting,
			EventSourcingEventId.ErasureNoAggregatesFound,
			EventSourcingEventId.ErasureAggregatesResolved,
			EventSourcingEventId.ErasureAggregateAlreadyErased,
			EventSourcingEventId.ErasureAggregateCompleted,
			EventSourcingEventId.ErasureSnapshotsDeleted,
			EventSourcingEventId.ErasureAggregateFailed,
			EventSourcingEventId.ErasureContributorCompleted
		];
	}

	#endregion
}
