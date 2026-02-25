// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Diagnostics;

/// <summary>
/// Event IDs for event sourcing infrastructure (110000-114999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>110000-110999: Event Store Core</item>
/// <item>111000-111999: Aggregate Repository</item>
/// <item>112000-112999: Snapshots</item>
/// <item>113000-113999: Projections</item>
/// <item>114000-114999: Upcasting/Migration</item>
/// </list>
/// </remarks>
public static class EventSourcingEventId
{
	// ========================================
	// 110000-110099: Event Store Core
	// ========================================

	/// <summary>Event store created.</summary>
	public const int EventStoreCreated = 110000;

	/// <summary>Events appended.</summary>
	public const int EventsAppended = 110001;

	/// <summary>Events loaded.</summary>
	public const int EventsLoaded = 110002;

	/// <summary>Stream created.</summary>
	public const int StreamCreated = 110003;

	/// <summary>Stream read.</summary>
	public const int StreamRead = 110004;

	/// <summary>Stream deleted.</summary>
	public const int StreamDeleted = 110005;

	/// <summary>Global position retrieved.</summary>
	public const int GlobalPositionRetrieved = 110006;

	// ========================================
	// 110100-110199: Event Store Providers
	// ========================================

	/// <summary>SQL Server event store created.</summary>
	public const int SqlServerEventStoreCreated = 110100;

	/// <summary>Cosmos DB event store created.</summary>
	public const int CosmosDbEventStoreCreated = 110101;

	/// <summary>DynamoDB event store created.</summary>
	public const int DynamoDbEventStoreCreated = 110102;

	/// <summary>Firestore event store created.</summary>
	public const int FirestoreEventStoreCreated = 110103;

	/// <summary>In-memory event store created.</summary>
	public const int InMemoryEventStoreCreated = 110104;

	// ========================================
	// 110200-110299: Event Serialization
	// ========================================

	/// <summary>Event serialized.</summary>
	public const int EventSerialized = 110200;

	/// <summary>Event deserialized.</summary>
	public const int EventDeserialized = 110201;

	/// <summary>Event serialization failed.</summary>
	public const int EventSerializationFailed = 110202;

	/// <summary>Event type resolved.</summary>
	public const int EventTypeResolved = 110203;

	// ========================================
	// 111000-111099: Aggregate Repository Core
	// ========================================

	/// <summary>Aggregate repository created.</summary>
	public const int AggregateRepositoryCreated = 111000;

	/// <summary>Aggregate loaded.</summary>
	public const int AggregateLoaded = 111001;

	/// <summary>Aggregate saved.</summary>
	public const int AggregateSaved = 111002;

	/// <summary>Aggregate not found.</summary>
	public const int AggregateNotFound = 111003;

	/// <summary>Aggregate created.</summary>
	public const int AggregateCreated = 111004;

	/// <summary>Aggregate version checked.</summary>
	public const int AggregateVersionChecked = 111005;

	// ========================================
	// 111100-111199: Aggregate Operations
	// ========================================

	/// <summary>Domain event applied.</summary>
	public const int DomainEventApplied = 111100;

	/// <summary>Domain events cleared.</summary>
	public const int DomainEventsCleared = 111101;

	/// <summary>Aggregate hydrated.</summary>
	public const int AggregateHydrated = 111102;

	/// <summary>Aggregate state reconstructed.</summary>
	public const int AggregateStateReconstructed = 111103;

	/// <summary>Concurrency conflict detected.</summary>
	public const int ConcurrencyConflict = 111104;

	// ========================================
	// 112000-112099: Snapshot Core
	// ========================================

	/// <summary>Snapshot store created.</summary>
	public const int SnapshotStoreCreated = 112000;

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 112001;

	/// <summary>Snapshot loaded.</summary>
	public const int SnapshotLoaded = 112002;

	/// <summary>Snapshot not found.</summary>
	public const int SnapshotNotFound = 112003;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 112004;

	// ========================================
	// 112100-112199: Snapshot Strategy
	// ========================================

	/// <summary>Snapshot strategy evaluated.</summary>
	public const int SnapshotStrategyEvaluated = 112100;

	/// <summary>Snapshot threshold reached.</summary>
	public const int SnapshotThresholdReached = 112101;

	/// <summary>Snapshot creation triggered.</summary>
	public const int SnapshotCreationTriggered = 112102;

	/// <summary>Snapshot skipped.</summary>
	public const int SnapshotSkipped = 112103;

	// ========================================
	// 113000-113099: Projection Core
	// ========================================

	/// <summary>Projection manager created.</summary>
	public const int ProjectionManagerCreated = 113000;

	/// <summary>Projection started.</summary>
	public const int ProjectionStarted = 113001;

	/// <summary>Projection stopped.</summary>
	public const int ProjectionStopped = 113002;

	/// <summary>Projection event processed.</summary>
	public const int ProjectionEventProcessed = 113003;

	/// <summary>Projection checkpoint saved.</summary>
	public const int ProjectionCheckpointSaved = 113004;

	// ========================================
	// 113100-113199: Projection Operations
	// ========================================

	/// <summary>Projection rebuilt.</summary>
	public const int ProjectionRebuilt = 113100;

	/// <summary>Projection caught up.</summary>
	public const int ProjectionCaughtUp = 113101;

	/// <summary>Projection behind.</summary>
	public const int ProjectionBehind = 113102;

	/// <summary>Projection error occurred.</summary>
	public const int ProjectionError = 113103;

	/// <summary>Projection batch processed.</summary>
	public const int ProjectionBatchProcessed = 113104;

	// ========================================
	// 114000-114099: Upcasting Core
	// ========================================

	/// <summary>Upcaster registry created.</summary>
	public const int UpcasterRegistryCreated = 114000;

	/// <summary>Event upcasted.</summary>
	public const int EventUpcasted = 114001;

	/// <summary>Upcaster chain executed.</summary>
	public const int UpcasterChainExecuted = 114002;

	/// <summary>Upcaster registered.</summary>
	public const int UpcasterRegistered = 114003;

	/// <summary>Upcasting skipped.</summary>
	public const int UpcastingSkipped = 114004;

	// ========================================
	// 114100-114199: Event Migration
	// ========================================

	/// <summary>Event migration started.</summary>
	public const int EventMigrationStarted = 114100;

	/// <summary>Event migration completed.</summary>
	public const int EventMigrationCompleted = 114101;

	/// <summary>Event migrated.</summary>
	public const int EventMigrated = 114102;

	/// <summary>Event migration failed.</summary>
	public const int EventMigrationFailed = 114103;

	/// <summary>Schema version updated.</summary>
	public const int SchemaVersionUpdated = 114104;

	// ========================================
	// 114200-114299: Event Version Manager
	// ========================================

	/// <summary>Event upgrader registered.</summary>
	public const int EventUpgraderRegistered = 114200;

	/// <summary>Event upgrading in progress.</summary>
	public const int EventUpgrading = 114201;

	// ========================================
	// 114250-114299: Snapshot Version Manager
	// ========================================

	/// <summary>Snapshot upgrader registered.</summary>
	public const int SnapshotUpgraderRegistered = 114250;

	/// <summary>Snapshot upgrading in progress.</summary>
	public const int SnapshotUpgrading = 114251;

	// ========================================
	// 114300-114399: Cloud Event Stores
	// ========================================

	/// <summary>Appending events to cloud event store.</summary>
	public const int CloudStoreAppendingEvents = 114300;

	/// <summary>Events appended to cloud event store.</summary>
	public const int CloudStoreEventsAppended = 114301;

	/// <summary>Concurrency conflict in cloud event store.</summary>
	public const int CloudStoreConcurrencyConflict = 114302;

	/// <summary>Loading events from cloud event store.</summary>
	public const int CloudStoreLoadingEvents = 114303;

	/// <summary>Cloud event store initializing.</summary>
	public const int CloudStoreInitializing = 114304;

	/// <summary>Cloud event store loaded events.</summary>
	public const int CloudStoreLoadedEvents = 114305;

	// ========================================
	// 114400-114499: Schema Migration
	// ========================================

	/// <summary>Schema migrator created.</summary>
	public const int MigratorCreated = 114400;

	/// <summary>Schema migration started.</summary>
	public const int MigrationStarted = 114401;

	/// <summary>Schema migration completed.</summary>
	public const int MigrationCompleted = 114402;

	/// <summary>Schema migration failed.</summary>
	public const int MigrationFailed = 114403;

	/// <summary>Individual migration applied.</summary>
	public const int MigrationApplied = 114404;

	/// <summary>Schema rollback started.</summary>
	public const int RollbackStarted = 114405;

	/// <summary>Schema rollback completed.</summary>
	public const int RollbackCompleted = 114406;

	/// <summary>Schema rollback failed.</summary>
	public const int RollbackFailed = 114407;

	/// <summary>Advisory lock acquired for migration.</summary>
	public const int MigrationLockAcquired = 114410;

	/// <summary>Advisory lock released after migration.</summary>
	public const int MigrationLockReleased = 114411;

	/// <summary>Failed to acquire migration lock.</summary>
	public const int MigrationLockFailed = 114412;

	/// <summary>Migration history table created.</summary>
	public const int MigrationHistoryCreated = 114413;

	/// <summary>Pending migrations found.</summary>
	public const int PendingMigrationsFound = 114414;

	/// <summary>No pending migrations.</summary>
	public const int NoPendingMigrations = 114415;

	// ========================================
	// 114500-114599: GDPR Erasure
	// ========================================

	/// <summary>Event store erasure contributor starting.</summary>
	public const int ErasureContributorStarting = 114500;

	/// <summary>No aggregates found for data subject during erasure.</summary>
	public const int ErasureNoAggregatesFound = 114501;

	/// <summary>Aggregates resolved for erasure.</summary>
	public const int ErasureAggregatesResolved = 114502;

	/// <summary>Aggregate already erased, skipping.</summary>
	public const int ErasureAggregateAlreadyErased = 114503;

	/// <summary>Aggregate events erased successfully.</summary>
	public const int ErasureAggregateCompleted = 114504;

	/// <summary>Snapshots deleted for erased aggregate.</summary>
	public const int ErasureSnapshotsDeleted = 114505;

	/// <summary>Aggregate erasure failed.</summary>
	public const int ErasureAggregateFailed = 114506;

	/// <summary>Event store erasure contributor completed.</summary>
	public const int ErasureContributorCompleted = 114507;
}
