// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Diagnostics;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DataDynamoDbEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.DynamoDb")]
[Trait("Priority", "0")]
public sealed class DataDynamoDbEventIdShould : UnitTestBase
{
	#region Client Management Event ID Tests (103000-103099)

	[Fact]
	public void HaveClientCreatedInClientManagementRange()
	{
		DataDynamoDbEventId.ClientCreated.ShouldBe(103000);
	}

	[Fact]
	public void HaveAllClientManagementEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.ClientCreated.ShouldBeInRange(103000, 103099);
		DataDynamoDbEventId.ClientDisposed.ShouldBeInRange(103000, 103099);
		DataDynamoDbEventId.RegionConfigured.ShouldBeInRange(103000, 103099);
		DataDynamoDbEventId.EndpointOverrideConfigured.ShouldBeInRange(103000, 103099);
		DataDynamoDbEventId.CredentialsConfigured.ShouldBeInRange(103000, 103099);
	}

	#endregion

	#region Table Operations Event ID Tests (103100-103199)

	[Fact]
	public void HaveTableCreatedInTableOperationsRange()
	{
		DataDynamoDbEventId.TableCreated.ShouldBe(103100);
	}

	[Fact]
	public void HaveAllTableOperationsEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.TableCreated.ShouldBeInRange(103100, 103199);
		DataDynamoDbEventId.TableDescribed.ShouldBeInRange(103100, 103199);
		DataDynamoDbEventId.TableDeleted.ShouldBeInRange(103100, 103199);
		DataDynamoDbEventId.TableThroughputUpdated.ShouldBeInRange(103100, 103199);
		DataDynamoDbEventId.GlobalSecondaryIndexCreated.ShouldBeInRange(103100, 103199);
	}

	#endregion

	#region Item Operations Event ID Tests (103200-103299)

	[Fact]
	public void HaveItemPutInItemOperationsRange()
	{
		DataDynamoDbEventId.ItemPut.ShouldBe(103200);
	}

	[Fact]
	public void HaveAllItemOperationsEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.ItemPut.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.ItemGet.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.ItemUpdated.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.ItemDeleted.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.BatchGetExecuted.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.BatchWriteExecuted.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.TransactWriteExecuted.ShouldBeInRange(103200, 103299);
		DataDynamoDbEventId.TransactGetExecuted.ShouldBeInRange(103200, 103299);
	}

	#endregion

	#region Query/Scan Event ID Tests (103300-103399)

	[Fact]
	public void HaveQueryExecutingInQueryScanRange()
	{
		DataDynamoDbEventId.QueryExecuting.ShouldBe(103300);
	}

	[Fact]
	public void HaveAllQueryScanEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.QueryExecuting.ShouldBeInRange(103300, 103399);
		DataDynamoDbEventId.QueryExecuted.ShouldBeInRange(103300, 103399);
		DataDynamoDbEventId.ScanExecuting.ShouldBeInRange(103300, 103399);
		DataDynamoDbEventId.ScanExecuted.ShouldBeInRange(103300, 103399);
		DataDynamoDbEventId.PaginationContinued.ShouldBeInRange(103300, 103399);
		DataDynamoDbEventId.FilterExpressionApplied.ShouldBeInRange(103300, 103399);
	}

	#endregion

	#region Streams Event ID Tests (103400-103419)

	[Fact]
	public void HaveStreamEnabledInStreamsRange()
	{
		DataDynamoDbEventId.StreamEnabled.ShouldBe(103400);
	}

	[Fact]
	public void HaveAllStreamsEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.StreamEnabled.ShouldBeInRange(103400, 103419);
		DataDynamoDbEventId.StreamRecordRead.ShouldBeInRange(103400, 103419);
		DataDynamoDbEventId.ShardIteratorObtained.ShouldBeInRange(103400, 103419);
		DataDynamoDbEventId.StreamProcessingCompleted.ShouldBeInRange(103400, 103419);
	}

	#endregion

	#region CDC Processor Event ID Tests (103420-103439)

	[Fact]
	public void HaveCdcProcessorStartingInCdcProcessorRange()
	{
		DataDynamoDbEventId.CdcProcessorStarting.ShouldBe(103420);
	}

	[Fact]
	public void HaveAllCdcProcessorEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.CdcProcessorStarting.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcProcessorStopping.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcBatchReceived.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcPositionConfirmed.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcShardsDiscovered.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcIteratorExpired.ShouldBeInRange(103420, 103439);
		DataDynamoDbEventId.CdcProcessingError.ShouldBeInRange(103420, 103439);
	}

	#endregion

	#region CDC State Store Event ID Tests (103440-103459)

	[Fact]
	public void HaveCdcPositionNotFoundInCdcStateStoreRange()
	{
		DataDynamoDbEventId.CdcPositionNotFound.ShouldBe(103440);
	}

	[Fact]
	public void HaveAllCdcStateStoreEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.CdcPositionNotFound.ShouldBeInRange(103440, 103459);
		DataDynamoDbEventId.CdcPositionDataMissing.ShouldBeInRange(103440, 103459);
		DataDynamoDbEventId.CdcPositionParseFailed.ShouldBeInRange(103440, 103459);
		DataDynamoDbEventId.CdcPositionLoaded.ShouldBeInRange(103440, 103459);
		DataDynamoDbEventId.CdcPositionSaved.ShouldBeInRange(103440, 103459);
		DataDynamoDbEventId.CdcPositionDeleted.ShouldBeInRange(103440, 103459);
	}

	#endregion

	#region Snapshot Store Event ID Tests (103460-103469)

	[Fact]
	public void HaveSnapshotStoreInitializedInSnapshotStoreRange()
	{
		DataDynamoDbEventId.SnapshotStoreInitialized.ShouldBe(103460);
	}

	[Fact]
	public void HaveAllSnapshotStoreEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.SnapshotStoreInitialized.ShouldBeInRange(103460, 103469);
		DataDynamoDbEventId.SnapshotSaved.ShouldBeInRange(103460, 103469);
		DataDynamoDbEventId.SnapshotSkipped.ShouldBeInRange(103460, 103469);
		DataDynamoDbEventId.SnapshotRetrieved.ShouldBeInRange(103460, 103469);
		DataDynamoDbEventId.SnapshotDeleted.ShouldBeInRange(103460, 103469);
		DataDynamoDbEventId.SnapshotDeletedOlderThan.ShouldBeInRange(103460, 103469);
	}

	#endregion

	#region Saga Store Event ID Tests (103470-103479)

	[Fact]
	public void HaveSagaStoreInitializedInSagaStoreRange()
	{
		DataDynamoDbEventId.SagaStoreInitialized.ShouldBe(103470);
	}

	[Fact]
	public void HaveAllSagaStoreEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.SagaStoreInitialized.ShouldBeInRange(103470, 103479);
		DataDynamoDbEventId.SagaLoaded.ShouldBeInRange(103470, 103479);
		DataDynamoDbEventId.SagaSaved.ShouldBeInRange(103470, 103479);
	}

	#endregion

	#region Grant Service Event ID Tests (103480-103489)

	[Fact]
	public void HaveGrantServiceInitializedInGrantServiceRange()
	{
		DataDynamoDbEventId.GrantServiceInitialized.ShouldBe(103480);
	}

	[Fact]
	public void HaveAllGrantServiceEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.GrantServiceInitialized.ShouldBeInRange(103480, 103489);
		DataDynamoDbEventId.GrantSaved.ShouldBeInRange(103480, 103489);
		DataDynamoDbEventId.GrantDeleted.ShouldBeInRange(103480, 103489);
		DataDynamoDbEventId.GrantRevoked.ShouldBeInRange(103480, 103489);
	}

	#endregion

	#region Outbox Store Event ID Tests (103490-103499)

	[Fact]
	public void HaveOutboxMessageStagedInOutboxStoreRange()
	{
		DataDynamoDbEventId.OutboxMessageStaged.ShouldBe(103490);
	}

	[Fact]
	public void HaveAllOutboxStoreEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.OutboxMessageStaged.ShouldBeInRange(103490, 103499);
		DataDynamoDbEventId.OutboxMessageEnqueued.ShouldBeInRange(103490, 103499);
		DataDynamoDbEventId.OutboxMessageSent.ShouldBeInRange(103490, 103499);
		DataDynamoDbEventId.OutboxMessageFailed.ShouldBeInRange(103490, 103499);
		DataDynamoDbEventId.OutboxCleanedUp.ShouldBeInRange(103490, 103499);
		DataDynamoDbEventId.OutboxConcurrencyConflict.ShouldBeInRange(103490, 103499);
	}

	#endregion

	#region Performance Event ID Tests (103500-103599)

	[Fact]
	public void HaveCapacityUnitsConsumedInPerformanceRange()
	{
		DataDynamoDbEventId.CapacityUnitsConsumed.ShouldBe(103500);
	}

	[Fact]
	public void HaveAllPerformanceEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.CapacityUnitsConsumed.ShouldBeInRange(103500, 103599);
		DataDynamoDbEventId.ProvisionedThroughputExceeded.ShouldBeInRange(103500, 103599);
		DataDynamoDbEventId.OnDemandModeActive.ShouldBeInRange(103500, 103599);
		DataDynamoDbEventId.DaxCacheHit.ShouldBeInRange(103500, 103599);
		DataDynamoDbEventId.DaxCacheMiss.ShouldBeInRange(103500, 103599);
	}

	#endregion

	#region Error Handling Event ID Tests (103600-103699)

	[Fact]
	public void HaveConditionalCheckFailedInErrorHandlingRange()
	{
		DataDynamoDbEventId.ConditionalCheckFailed.ShouldBe(103600);
	}

	[Fact]
	public void HaveAllErrorHandlingEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.ConditionalCheckFailed.ShouldBeInRange(103600, 103699);
		DataDynamoDbEventId.ItemNotFound.ShouldBeInRange(103600, 103699);
		DataDynamoDbEventId.TransactionConflict.ShouldBeInRange(103600, 103699);
		DataDynamoDbEventId.ValidationException.ShouldBeInRange(103600, 103699);
		DataDynamoDbEventId.DynamoDbException.ShouldBeInRange(103600, 103699);
	}

	#endregion

	#region Persistence Provider Event ID Tests (103700-103799)

	[Fact]
	public void HaveProviderInitializingInPersistenceProviderRange()
	{
		DataDynamoDbEventId.ProviderInitializing.ShouldBe(103700);
	}

	[Fact]
	public void HaveAllPersistenceProviderEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.ProviderInitializing.ShouldBeInRange(103700, 103799);
		DataDynamoDbEventId.ProviderDisposing.ShouldBeInRange(103700, 103799);
		DataDynamoDbEventId.OperationCompletedWithCapacity.ShouldBeInRange(103700, 103799);
		DataDynamoDbEventId.OperationFailed.ShouldBeInRange(103700, 103799);
	}

	#endregion

	#region Health Check Event ID Tests (103800-103899)

	[Fact]
	public void HaveHealthCheckStartedInHealthCheckRange()
	{
		DataDynamoDbEventId.HealthCheckStarted.ShouldBe(103800);
	}

	[Fact]
	public void HaveAllHealthCheckEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.HealthCheckStarted.ShouldBeInRange(103800, 103899);
		DataDynamoDbEventId.HealthCheckCompleted.ShouldBeInRange(103800, 103899);
		DataDynamoDbEventId.HealthCheckFailed.ShouldBeInRange(103800, 103899);
	}

	#endregion

	#region Streams Subscription Event ID Tests (103900-103949)

	[Fact]
	public void HaveStreamsStartingInStreamsSubscriptionRange()
	{
		DataDynamoDbEventId.StreamsStarting.ShouldBe(103900);
	}

	[Fact]
	public void HaveAllStreamsSubscriptionEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.StreamsStarting.ShouldBeInRange(103900, 103949);
		DataDynamoDbEventId.StreamsStopping.ShouldBeInRange(103900, 103949);
		DataDynamoDbEventId.StreamsReceivedBatch.ShouldBeInRange(103900, 103949);
	}

	#endregion

	#region Inbox Store Event ID Tests (103950-103959)

	[Fact]
	public void HaveInboxMessageStoredInInboxStoreRange()
	{
		DataDynamoDbEventId.InboxMessageStored.ShouldBe(103950);
	}

	[Fact]
	public void HaveAllInboxStoreEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.InboxMessageStored.ShouldBeInRange(103950, 103959);
		DataDynamoDbEventId.InboxMessageAlreadyProcessed.ShouldBeInRange(103950, 103959);
		DataDynamoDbEventId.InboxMessageCompleted.ShouldBeInRange(103950, 103959);
		DataDynamoDbEventId.InboxMessageProcessingError.ShouldBeInRange(103950, 103959);
		DataDynamoDbEventId.InboxMessageStoringError.ShouldBeInRange(103950, 103959);
		DataDynamoDbEventId.InboxCleanupComplete.ShouldBeInRange(103950, 103959);
	}

	#endregion

	#region Activity Group Service Event ID Tests (103960-103969)

	[Fact]
	public void HaveActivityGroupServiceInitializedInActivityGroupRange()
	{
		DataDynamoDbEventId.ActivityGroupServiceInitialized.ShouldBe(103960);
	}

	[Fact]
	public void HaveAllActivityGroupServiceEventIdsInExpectedRange()
	{
		DataDynamoDbEventId.ActivityGroupServiceInitialized.ShouldBeInRange(103960, 103969);
		DataDynamoDbEventId.ActivityGroupGrantInserted.ShouldBeInRange(103960, 103969);
		DataDynamoDbEventId.ActivityGroupGrantsDeletedByUser.ShouldBeInRange(103960, 103969);
		DataDynamoDbEventId.ActivityGroupAllGrantsDeleted.ShouldBeInRange(103960, 103969);
	}

	#endregion

	#region DynamoDb Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInDynamoDbReservedRange()
	{
		// DynamoDb reserved range is 103000-103999
		var allEventIds = GetAllDynamoDbEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(103000, 103999,
				$"Event ID {eventId} is outside DynamoDb reserved range (103000-103999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllDynamoDbEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllDynamoDbEventIds();
		allEventIds.Length.ShouldBeGreaterThan(65);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllDynamoDbEventIds()
	{
		return
		[
			// Client Management (103000-103099)
			DataDynamoDbEventId.ClientCreated,
			DataDynamoDbEventId.ClientDisposed,
			DataDynamoDbEventId.RegionConfigured,
			DataDynamoDbEventId.EndpointOverrideConfigured,
			DataDynamoDbEventId.CredentialsConfigured,

			// Table Operations (103100-103199)
			DataDynamoDbEventId.TableCreated,
			DataDynamoDbEventId.TableDescribed,
			DataDynamoDbEventId.TableDeleted,
			DataDynamoDbEventId.TableThroughputUpdated,
			DataDynamoDbEventId.GlobalSecondaryIndexCreated,

			// Item Operations (103200-103299)
			DataDynamoDbEventId.ItemPut,
			DataDynamoDbEventId.ItemGet,
			DataDynamoDbEventId.ItemUpdated,
			DataDynamoDbEventId.ItemDeleted,
			DataDynamoDbEventId.BatchGetExecuted,
			DataDynamoDbEventId.BatchWriteExecuted,
			DataDynamoDbEventId.TransactWriteExecuted,
			DataDynamoDbEventId.TransactGetExecuted,

			// Query/Scan (103300-103399)
			DataDynamoDbEventId.QueryExecuting,
			DataDynamoDbEventId.QueryExecuted,
			DataDynamoDbEventId.ScanExecuting,
			DataDynamoDbEventId.ScanExecuted,
			DataDynamoDbEventId.PaginationContinued,
			DataDynamoDbEventId.FilterExpressionApplied,

			// Streams (103400-103419)
			DataDynamoDbEventId.StreamEnabled,
			DataDynamoDbEventId.StreamRecordRead,
			DataDynamoDbEventId.ShardIteratorObtained,
			DataDynamoDbEventId.StreamProcessingCompleted,

			// CDC Processor (103420-103439)
			DataDynamoDbEventId.CdcProcessorStarting,
			DataDynamoDbEventId.CdcProcessorStopping,
			DataDynamoDbEventId.CdcBatchReceived,
			DataDynamoDbEventId.CdcPositionConfirmed,
			DataDynamoDbEventId.CdcShardsDiscovered,
			DataDynamoDbEventId.CdcIteratorExpired,
			DataDynamoDbEventId.CdcProcessingError,

			// CDC State Store (103440-103459)
			DataDynamoDbEventId.CdcPositionNotFound,
			DataDynamoDbEventId.CdcPositionDataMissing,
			DataDynamoDbEventId.CdcPositionParseFailed,
			DataDynamoDbEventId.CdcPositionLoaded,
			DataDynamoDbEventId.CdcPositionSaved,
			DataDynamoDbEventId.CdcPositionDeleted,

			// Snapshot Store (103460-103469)
			DataDynamoDbEventId.SnapshotStoreInitialized,
			DataDynamoDbEventId.SnapshotSaved,
			DataDynamoDbEventId.SnapshotSkipped,
			DataDynamoDbEventId.SnapshotRetrieved,
			DataDynamoDbEventId.SnapshotDeleted,
			DataDynamoDbEventId.SnapshotDeletedOlderThan,

			// Saga Store (103470-103479)
			DataDynamoDbEventId.SagaStoreInitialized,
			DataDynamoDbEventId.SagaLoaded,
			DataDynamoDbEventId.SagaSaved,

			// Grant Service (103480-103489)
			DataDynamoDbEventId.GrantServiceInitialized,
			DataDynamoDbEventId.GrantSaved,
			DataDynamoDbEventId.GrantDeleted,
			DataDynamoDbEventId.GrantRevoked,

			// Outbox Store (103490-103499)
			DataDynamoDbEventId.OutboxMessageStaged,
			DataDynamoDbEventId.OutboxMessageEnqueued,
			DataDynamoDbEventId.OutboxMessageSent,
			DataDynamoDbEventId.OutboxMessageFailed,
			DataDynamoDbEventId.OutboxCleanedUp,
			DataDynamoDbEventId.OutboxConcurrencyConflict,

			// Performance (103500-103599)
			DataDynamoDbEventId.CapacityUnitsConsumed,
			DataDynamoDbEventId.ProvisionedThroughputExceeded,
			DataDynamoDbEventId.OnDemandModeActive,
			DataDynamoDbEventId.DaxCacheHit,
			DataDynamoDbEventId.DaxCacheMiss,

			// Error Handling (103600-103699)
			DataDynamoDbEventId.ConditionalCheckFailed,
			DataDynamoDbEventId.ItemNotFound,
			DataDynamoDbEventId.TransactionConflict,
			DataDynamoDbEventId.ValidationException,
			DataDynamoDbEventId.DynamoDbException,

			// Persistence Provider (103700-103799)
			DataDynamoDbEventId.ProviderInitializing,
			DataDynamoDbEventId.ProviderDisposing,
			DataDynamoDbEventId.OperationCompletedWithCapacity,
			DataDynamoDbEventId.OperationFailed,

			// Health Check (103800-103899)
			DataDynamoDbEventId.HealthCheckStarted,
			DataDynamoDbEventId.HealthCheckCompleted,
			DataDynamoDbEventId.HealthCheckFailed,

			// Streams Subscription (103900-103949)
			DataDynamoDbEventId.StreamsStarting,
			DataDynamoDbEventId.StreamsStopping,
			DataDynamoDbEventId.StreamsReceivedBatch,

			// Inbox Store (103950-103959)
			DataDynamoDbEventId.InboxMessageStored,
			DataDynamoDbEventId.InboxMessageAlreadyProcessed,
			DataDynamoDbEventId.InboxMessageCompleted,
			DataDynamoDbEventId.InboxMessageProcessingError,
			DataDynamoDbEventId.InboxMessageStoringError,
			DataDynamoDbEventId.InboxCleanupComplete,

			// Activity Group Service (103960-103969)
			DataDynamoDbEventId.ActivityGroupServiceInitialized,
			DataDynamoDbEventId.ActivityGroupGrantInserted,
			DataDynamoDbEventId.ActivityGroupGrantsDeletedByUser,
			DataDynamoDbEventId.ActivityGroupAllGrantsDeleted
		];
	}

	#endregion
}
