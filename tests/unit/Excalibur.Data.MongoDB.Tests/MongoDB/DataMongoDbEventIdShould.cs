// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Diagnostics;

namespace Excalibur.Data.Tests.MongoDB;

/// <summary>
/// Unit tests for <see cref="DataMongoDbEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.MongoDB")]
[Trait("Priority", "0")]
public sealed class DataMongoDbEventIdShould : UnitTestBase
{
	#region Client Management Event ID Tests (104000-104099)

	[Fact]
	public void HaveClientCreatedInClientManagementRange()
	{
		DataMongoDbEventId.ClientCreated.ShouldBe(104000);
	}

	[Fact]
	public void HaveAllClientManagementEventIdsInExpectedRange()
	{
		DataMongoDbEventId.ClientCreated.ShouldBeInRange(104000, 104099);
		DataMongoDbEventId.ClientDisposed.ShouldBeInRange(104000, 104099);
		DataMongoDbEventId.ConnectionStringConfigured.ShouldBeInRange(104000, 104099);
		DataMongoDbEventId.ReadPreferenceConfigured.ShouldBeInRange(104000, 104099);
		DataMongoDbEventId.WriteConcernConfigured.ShouldBeInRange(104000, 104099);
		DataMongoDbEventId.ConnectionPoolConfigured.ShouldBeInRange(104000, 104099);
	}

	#endregion

	#region Collection Operations Event ID Tests (104100-104199)

	[Fact]
	public void HaveCollectionCreatedInCollectionOperationsRange()
	{
		DataMongoDbEventId.CollectionCreated.ShouldBe(104100);
	}

	[Fact]
	public void HaveAllCollectionOperationsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.CollectionCreated.ShouldBeInRange(104100, 104199);
		DataMongoDbEventId.CollectionAccessed.ShouldBeInRange(104100, 104199);
		DataMongoDbEventId.CollectionDropped.ShouldBeInRange(104100, 104199);
		DataMongoDbEventId.IndexCreated.ShouldBeInRange(104100, 104199);
		DataMongoDbEventId.IndexDropped.ShouldBeInRange(104100, 104199);
	}

	#endregion

	#region Document Operations Event ID Tests (104200-104299)

	[Fact]
	public void HaveDocumentInsertedInDocumentOperationsRange()
	{
		DataMongoDbEventId.DocumentInserted.ShouldBe(104200);
	}

	[Fact]
	public void HaveAllDocumentOperationsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.DocumentInserted.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.DocumentFound.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.DocumentUpdated.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.DocumentReplaced.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.DocumentDeleted.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.BulkWriteExecuted.ShouldBeInRange(104200, 104299);
		DataMongoDbEventId.DocumentsInsertedMany.ShouldBeInRange(104200, 104299);
	}

	#endregion

	#region Query/Aggregation Event ID Tests (104300-104399)

	[Fact]
	public void HaveFindQueryExecutingInQueryAggregationRange()
	{
		DataMongoDbEventId.FindQueryExecuting.ShouldBe(104300);
	}

	[Fact]
	public void HaveAllQueryAggregationEventIdsInExpectedRange()
	{
		DataMongoDbEventId.FindQueryExecuting.ShouldBeInRange(104300, 104399);
		DataMongoDbEventId.FindQueryExecuted.ShouldBeInRange(104300, 104399);
		DataMongoDbEventId.AggregationExecuting.ShouldBeInRange(104300, 104399);
		DataMongoDbEventId.AggregationExecuted.ShouldBeInRange(104300, 104399);
		DataMongoDbEventId.CountQueryExecuted.ShouldBeInRange(104300, 104399);
		DataMongoDbEventId.DistinctQueryExecuted.ShouldBeInRange(104300, 104399);
	}

	#endregion

	#region Change Streams Event ID Tests (104400-104499)

	[Fact]
	public void HaveChangeStreamStartedInChangeStreamsRange()
	{
		DataMongoDbEventId.ChangeStreamStarted.ShouldBe(104400);
	}

	[Fact]
	public void HaveAllChangeStreamsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.ChangeStreamStarted.ShouldBeInRange(104400, 104499);
		DataMongoDbEventId.ChangeStreamStopped.ShouldBeInRange(104400, 104499);
		DataMongoDbEventId.ChangeEventReceived.ShouldBeInRange(104400, 104499);
		DataMongoDbEventId.ResumeTokenStored.ShouldBeInRange(104400, 104499);
		DataMongoDbEventId.ChangeStreamError.ShouldBeInRange(104400, 104499);
	}

	#endregion

	#region Performance Event ID Tests (104500-104599)

	[Fact]
	public void HaveSlowOperationDetectedInPerformanceRange()
	{
		DataMongoDbEventId.SlowOperationDetected.ShouldBe(104500);
	}

	[Fact]
	public void HaveAllPerformanceEventIdsInExpectedRange()
	{
		DataMongoDbEventId.SlowOperationDetected.ShouldBeInRange(104500, 104599);
		DataMongoDbEventId.ExplainPlanRetrieved.ShouldBeInRange(104500, 104599);
		DataMongoDbEventId.ServerSelectionCompleted.ShouldBeInRange(104500, 104599);
		DataMongoDbEventId.ConnectionCheckedOut.ShouldBeInRange(104500, 104599);
	}

	#endregion

	#region Error Handling Event ID Tests (104600-104699)

	[Fact]
	public void HaveDuplicateKeyErrorInErrorHandlingRange()
	{
		DataMongoDbEventId.DuplicateKeyError.ShouldBe(104600);
	}

	[Fact]
	public void HaveAllErrorHandlingEventIdsInExpectedRange()
	{
		DataMongoDbEventId.DuplicateKeyError.ShouldBeInRange(104600, 104699);
		DataMongoDbEventId.WriteConcernError.ShouldBeInRange(104600, 104699);
		DataMongoDbEventId.DocumentNotFound.ShouldBeInRange(104600, 104699);
		DataMongoDbEventId.MongoDbException.ShouldBeInRange(104600, 104699);
		DataMongoDbEventId.TimeoutException.ShouldBeInRange(104600, 104699);
	}

	#endregion

	#region Retry Policy Event ID Tests (104700-104799)

	[Fact]
	public void HaveMongoOperationRetryInRetryPolicyRange()
	{
		DataMongoDbEventId.MongoOperationRetry.ShouldBe(104700);
	}

	[Fact]
	public void HaveAllRetryPolicyEventIdsInExpectedRange()
	{
		DataMongoDbEventId.MongoOperationRetry.ShouldBeInRange(104700, 104799);
		DataMongoDbEventId.MongoDocumentOperationRetry.ShouldBeInRange(104700, 104799);
	}

	#endregion

	#region Provider Operations Event ID Tests (104800-104819)

	[Fact]
	public void HaveExecutingDataRequestInProviderOperationsRange()
	{
		DataMongoDbEventId.ExecutingDataRequest.ShouldBe(104800);
	}

	[Fact]
	public void HaveAllProviderOperationsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.ExecutingDataRequest.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToExecuteDataRequest.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ExecutingDocumentDataRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.SuccessfullyExecutedDocumentDataRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToExecuteDocumentDataRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ExecutingDataRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToExecuteDataRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ConnectionTestSuccessful.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ConnectionTestFailed.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToRetrieveServerMetadata.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.InitializingProvider.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToRetrieveConnectionPoolStats.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ExecutingDocumentRequest.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToExecuteDocumentRequest.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ExecutingDocumentRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToExecuteDocumentRequestInTransaction.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.ExecutingBatchOfDocumentRequests.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToRetrieveDatabaseStatistics.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.FailedToRetrieveCollectionInfo.ShouldBeInRange(104800, 104819);
		DataMongoDbEventId.DisposingProvider.ShouldBeInRange(104800, 104819);
	}

	#endregion

	#region EventStore Event ID Tests (104820-104839)

	[Fact]
	public void HaveEventsAppendedInEventStoreRange()
	{
		DataMongoDbEventId.EventsAppended.ShouldBe(104820);
	}

	[Fact]
	public void HaveAllEventStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.EventsAppended.ShouldBeInRange(104820, 104839);
		DataMongoDbEventId.ConcurrencyConflict.ShouldBeInRange(104820, 104839);
		DataMongoDbEventId.AppendError.ShouldBeInRange(104820, 104839);
		DataMongoDbEventId.EventDispatched.ShouldBeInRange(104820, 104839);
	}

	#endregion

	#region SnapshotStore Event ID Tests (104840-104859)

	[Fact]
	public void HaveSnapshotSavedInSnapshotStoreRange()
	{
		DataMongoDbEventId.SnapshotSaved.ShouldBe(104840);
	}

	[Fact]
	public void HaveAllSnapshotStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.SnapshotSaved.ShouldBeInRange(104840, 104859);
		DataMongoDbEventId.SnapshotVersionSkipped.ShouldBeInRange(104840, 104859);
		DataMongoDbEventId.SnapshotDeleted.ShouldBeInRange(104840, 104859);
		DataMongoDbEventId.SnapshotOlderDeleted.ShouldBeInRange(104840, 104859);
	}

	#endregion

	#region SagaStore Event ID Tests (104860-104869)

	[Fact]
	public void HaveSagaStateSavedInSagaStoreRange()
	{
		DataMongoDbEventId.SagaStateSaved.ShouldBe(104860);
	}

	[Fact]
	public void HaveAllSagaStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.SagaStateSaved.ShouldBeInRange(104860, 104869);
		DataMongoDbEventId.SagaStateDeleted.ShouldBeInRange(104860, 104869);
		DataMongoDbEventId.SagaStateLoaded.ShouldBeInRange(104860, 104869);
	}

	#endregion

	#region ProjectionStore Event ID Tests (104870-104889)

	[Fact]
	public void HaveProjectionUpsertedInProjectionStoreRange()
	{
		DataMongoDbEventId.ProjectionUpserted.ShouldBe(104870);
	}

	[Fact]
	public void HaveAllProjectionStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.ProjectionUpserted.ShouldBeInRange(104870, 104889);
		DataMongoDbEventId.ProjectionDeleted.ShouldBeInRange(104870, 104889);
		DataMongoDbEventId.ProjectionsDeletedByType.ShouldBeInRange(104870, 104889);
		DataMongoDbEventId.ProjectionStoreInitialized.ShouldBeInRange(104870, 104889);
	}

	#endregion

	#region OutboxStore Event ID Tests (104890-104909)

	[Fact]
	public void HaveMessageStagedInOutboxStoreRange()
	{
		DataMongoDbEventId.MessageStaged.ShouldBe(104890);
	}

	[Fact]
	public void HaveAllOutboxStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.MessageStaged.ShouldBeInRange(104890, 104909);
		DataMongoDbEventId.MessageEnqueued.ShouldBeInRange(104890, 104909);
		DataMongoDbEventId.MessageSent.ShouldBeInRange(104890, 104909);
		DataMongoDbEventId.MessageFailed.ShouldBeInRange(104890, 104909);
		DataMongoDbEventId.MessagesCleanedUp.ShouldBeInRange(104890, 104909);
	}

	#endregion

	#region InboxStore Event ID Tests (104910-104929)

	[Fact]
	public void HaveInboxStoredInInboxStoreRange()
	{
		DataMongoDbEventId.InboxStored.ShouldBe(104910);
	}

	[Fact]
	public void HaveAllInboxStoreEventIdsInExpectedRange()
	{
		DataMongoDbEventId.InboxStored.ShouldBeInRange(104910, 104929);
		DataMongoDbEventId.InboxMarkedComplete.ShouldBeInRange(104910, 104929);
		DataMongoDbEventId.InboxAlreadyProcessed.ShouldBeInRange(104910, 104929);
		DataMongoDbEventId.InboxMarkedFailed.ShouldBeInRange(104910, 104929);
		DataMongoDbEventId.InboxCleanedUp.ShouldBeInRange(104910, 104929);
		DataMongoDbEventId.InboxFirstProcessor.ShouldBeInRange(104910, 104929);
	}

	#endregion

	#region CDC Processor Event ID Tests (104930-104949)

	[Fact]
	public void HaveCdcStartingInCdcProcessorRange()
	{
		DataMongoDbEventId.CdcStarting.ShouldBe(104930);
	}

	[Fact]
	public void HaveAllCdcProcessorEventIdsInExpectedRange()
	{
		DataMongoDbEventId.CdcStarting.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcStopping.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcBatchReceived.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcPositionConfirmed.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcProcessingError.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcResumingFromToken.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcStartingFromBeginning.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcStreamWatching.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcEventProcessed.ShouldBeInRange(104930, 104949);
		DataMongoDbEventId.CdcStreamInvalidated.ShouldBeInRange(104930, 104949);
	}

	#endregion

	#region Grants Event ID Tests (104950-104969)

	[Fact]
	public void HaveGrantSavedInGrantsRange()
	{
		DataMongoDbEventId.GrantSaved.ShouldBe(104950);
	}

	[Fact]
	public void HaveAllGrantsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.GrantSaved.ShouldBeInRange(104950, 104969);
		DataMongoDbEventId.GrantDeleted.ShouldBeInRange(104950, 104969);
		DataMongoDbEventId.GrantNotFound.ShouldBeInRange(104950, 104969);
		DataMongoDbEventId.GrantsListed.ShouldBeInRange(104950, 104969);
		DataMongoDbEventId.GrantServiceInitialized.ShouldBeInRange(104950, 104969);
		DataMongoDbEventId.GrantRevoked.ShouldBeInRange(104950, 104969);
	}

	#endregion

	#region Activity Groups Event ID Tests (104970-104989)

	[Fact]
	public void HaveActivityGroupGrantSavedInActivityGroupsRange()
	{
		DataMongoDbEventId.ActivityGroupGrantSaved.ShouldBe(104970);
	}

	[Fact]
	public void HaveAllActivityGroupsEventIdsInExpectedRange()
	{
		DataMongoDbEventId.ActivityGroupGrantSaved.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupGrantDeleted.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupGrantNotFound.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupGrantsListed.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupServiceInitialized.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupGrantsDeletedByUser.ShouldBeInRange(104970, 104989);
		DataMongoDbEventId.ActivityGroupAllGrantsDeleted.ShouldBeInRange(104970, 104989);
	}

	#endregion

	#region MongoDB Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInMongoDbReservedRange()
	{
		// MongoDB reserved range is 104000-104999
		var allEventIds = GetAllMongoDbEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(104000, 104999,
				$"Event ID {eventId} is outside MongoDB reserved range (104000-104999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllMongoDbEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllMongoDbEventIds();
		allEventIds.Length.ShouldBeGreaterThan(90);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllMongoDbEventIds()
	{
		return
		[
			// Client Management (104000-104099)
			DataMongoDbEventId.ClientCreated,
			DataMongoDbEventId.ClientDisposed,
			DataMongoDbEventId.ConnectionStringConfigured,
			DataMongoDbEventId.ReadPreferenceConfigured,
			DataMongoDbEventId.WriteConcernConfigured,
			DataMongoDbEventId.ConnectionPoolConfigured,

			// Collection Operations (104100-104199)
			DataMongoDbEventId.CollectionCreated,
			DataMongoDbEventId.CollectionAccessed,
			DataMongoDbEventId.CollectionDropped,
			DataMongoDbEventId.IndexCreated,
			DataMongoDbEventId.IndexDropped,

			// Document Operations (104200-104299)
			DataMongoDbEventId.DocumentInserted,
			DataMongoDbEventId.DocumentFound,
			DataMongoDbEventId.DocumentUpdated,
			DataMongoDbEventId.DocumentReplaced,
			DataMongoDbEventId.DocumentDeleted,
			DataMongoDbEventId.BulkWriteExecuted,
			DataMongoDbEventId.DocumentsInsertedMany,

			// Query/Aggregation (104300-104399)
			DataMongoDbEventId.FindQueryExecuting,
			DataMongoDbEventId.FindQueryExecuted,
			DataMongoDbEventId.AggregationExecuting,
			DataMongoDbEventId.AggregationExecuted,
			DataMongoDbEventId.CountQueryExecuted,
			DataMongoDbEventId.DistinctQueryExecuted,

			// Change Streams (104400-104499)
			DataMongoDbEventId.ChangeStreamStarted,
			DataMongoDbEventId.ChangeStreamStopped,
			DataMongoDbEventId.ChangeEventReceived,
			DataMongoDbEventId.ResumeTokenStored,
			DataMongoDbEventId.ChangeStreamError,

			// Performance (104500-104599)
			DataMongoDbEventId.SlowOperationDetected,
			DataMongoDbEventId.ExplainPlanRetrieved,
			DataMongoDbEventId.ServerSelectionCompleted,
			DataMongoDbEventId.ConnectionCheckedOut,

			// Error Handling (104600-104699)
			DataMongoDbEventId.DuplicateKeyError,
			DataMongoDbEventId.WriteConcernError,
			DataMongoDbEventId.DocumentNotFound,
			DataMongoDbEventId.MongoDbException,
			DataMongoDbEventId.TimeoutException,

			// Retry Policy (104700-104799)
			DataMongoDbEventId.MongoOperationRetry,
			DataMongoDbEventId.MongoDocumentOperationRetry,

			// Provider Operations (104800-104819)
			DataMongoDbEventId.ExecutingDataRequest,
			DataMongoDbEventId.FailedToExecuteDataRequest,
			DataMongoDbEventId.ExecutingDocumentDataRequestInTransaction,
			DataMongoDbEventId.SuccessfullyExecutedDocumentDataRequestInTransaction,
			DataMongoDbEventId.FailedToExecuteDocumentDataRequestInTransaction,
			DataMongoDbEventId.ExecutingDataRequestInTransaction,
			DataMongoDbEventId.FailedToExecuteDataRequestInTransaction,
			DataMongoDbEventId.ConnectionTestSuccessful,
			DataMongoDbEventId.ConnectionTestFailed,
			DataMongoDbEventId.FailedToRetrieveServerMetadata,
			DataMongoDbEventId.InitializingProvider,
			DataMongoDbEventId.FailedToRetrieveConnectionPoolStats,
			DataMongoDbEventId.ExecutingDocumentRequest,
			DataMongoDbEventId.FailedToExecuteDocumentRequest,
			DataMongoDbEventId.ExecutingDocumentRequestInTransaction,
			DataMongoDbEventId.FailedToExecuteDocumentRequestInTransaction,
			DataMongoDbEventId.ExecutingBatchOfDocumentRequests,
			DataMongoDbEventId.FailedToRetrieveDatabaseStatistics,
			DataMongoDbEventId.FailedToRetrieveCollectionInfo,
			DataMongoDbEventId.DisposingProvider,

			// EventStore (104820-104839)
			DataMongoDbEventId.EventsAppended,
			DataMongoDbEventId.ConcurrencyConflict,
			DataMongoDbEventId.AppendError,
			DataMongoDbEventId.EventDispatched,

			// SnapshotStore (104840-104859)
			DataMongoDbEventId.SnapshotSaved,
			DataMongoDbEventId.SnapshotVersionSkipped,
			DataMongoDbEventId.SnapshotDeleted,
			DataMongoDbEventId.SnapshotOlderDeleted,

			// SagaStore (104860-104869)
			DataMongoDbEventId.SagaStateSaved,
			DataMongoDbEventId.SagaStateDeleted,
			DataMongoDbEventId.SagaStateLoaded,

			// ProjectionStore (104870-104889)
			DataMongoDbEventId.ProjectionUpserted,
			DataMongoDbEventId.ProjectionDeleted,
			DataMongoDbEventId.ProjectionsDeletedByType,
			DataMongoDbEventId.ProjectionStoreInitialized,

			// OutboxStore (104890-104909)
			DataMongoDbEventId.MessageStaged,
			DataMongoDbEventId.MessageEnqueued,
			DataMongoDbEventId.MessageSent,
			DataMongoDbEventId.MessageFailed,
			DataMongoDbEventId.MessagesCleanedUp,

			// InboxStore (104910-104929)
			DataMongoDbEventId.InboxStored,
			DataMongoDbEventId.InboxMarkedComplete,
			DataMongoDbEventId.InboxAlreadyProcessed,
			DataMongoDbEventId.InboxMarkedFailed,
			DataMongoDbEventId.InboxCleanedUp,
			DataMongoDbEventId.InboxFirstProcessor,

			// CDC Processor (104930-104949)
			DataMongoDbEventId.CdcStarting,
			DataMongoDbEventId.CdcStopping,
			DataMongoDbEventId.CdcBatchReceived,
			DataMongoDbEventId.CdcPositionConfirmed,
			DataMongoDbEventId.CdcProcessingError,
			DataMongoDbEventId.CdcResumingFromToken,
			DataMongoDbEventId.CdcStartingFromBeginning,
			DataMongoDbEventId.CdcStreamWatching,
			DataMongoDbEventId.CdcEventProcessed,
			DataMongoDbEventId.CdcStreamInvalidated,

			// Grants (104950-104969)
			DataMongoDbEventId.GrantSaved,
			DataMongoDbEventId.GrantDeleted,
			DataMongoDbEventId.GrantNotFound,
			DataMongoDbEventId.GrantsListed,
			DataMongoDbEventId.GrantServiceInitialized,
			DataMongoDbEventId.GrantRevoked,

			// Activity Groups (104970-104989)
			DataMongoDbEventId.ActivityGroupGrantSaved,
			DataMongoDbEventId.ActivityGroupGrantDeleted,
			DataMongoDbEventId.ActivityGroupGrantNotFound,
			DataMongoDbEventId.ActivityGroupGrantsListed,
			DataMongoDbEventId.ActivityGroupServiceInitialized,
			DataMongoDbEventId.ActivityGroupGrantsDeletedByUser,
			DataMongoDbEventId.ActivityGroupAllGrantsDeleted
		];
	}

	#endregion
}
