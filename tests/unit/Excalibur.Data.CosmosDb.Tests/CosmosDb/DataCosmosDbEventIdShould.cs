// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Diagnostics;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for <see cref="DataCosmosDbEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.CosmosDb")]
[Trait("Priority", "0")]
public sealed class DataCosmosDbEventIdShould : UnitTestBase
{
	#region Client Management Event ID Tests (102000-102099)

	[Fact]
	public void HaveClientCreatedInClientManagementRange()
	{
		DataCosmosDbEventId.ClientCreated.ShouldBe(102000);
	}

	[Fact]
	public void HaveAllClientManagementEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.ClientCreated.ShouldBeInRange(102000, 102099);
		DataCosmosDbEventId.ClientDisposed.ShouldBeInRange(102000, 102099);
		DataCosmosDbEventId.ConnectionModeConfigured.ShouldBeInRange(102000, 102099);
		DataCosmosDbEventId.PreferredRegionsConfigured.ShouldBeInRange(102000, 102099);
		DataCosmosDbEventId.RetryPolicyConfigured.ShouldBeInRange(102000, 102099);
	}

	#endregion

	#region Container Operations Event ID Tests (102100-102199)

	[Fact]
	public void HaveContainerCreatedInContainerOperationsRange()
	{
		DataCosmosDbEventId.ContainerCreated.ShouldBe(102100);
	}

	[Fact]
	public void HaveAllContainerOperationsEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.ContainerCreated.ShouldBeInRange(102100, 102199);
		DataCosmosDbEventId.ContainerRead.ShouldBeInRange(102100, 102199);
		DataCosmosDbEventId.ContainerDeleted.ShouldBeInRange(102100, 102199);
		DataCosmosDbEventId.ContainerThroughputUpdated.ShouldBeInRange(102100, 102199);
		DataCosmosDbEventId.PartitionKeyConfigured.ShouldBeInRange(102100, 102199);
	}

	#endregion

	#region Document Operations Event ID Tests (102200-102299)

	[Fact]
	public void HaveDocumentCreatedInDocumentOperationsRange()
	{
		DataCosmosDbEventId.DocumentCreated.ShouldBe(102200);
	}

	[Fact]
	public void HaveAllDocumentOperationsEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.DocumentCreated.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.DocumentRead.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.DocumentReplaced.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.DocumentUpserted.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.DocumentDeleted.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.DocumentPatched.ShouldBeInRange(102200, 102299);
		DataCosmosDbEventId.TransactionalBatchExecuted.ShouldBeInRange(102200, 102299);
	}

	#endregion

	#region Query Execution Event ID Tests (102300-102399)

	[Fact]
	public void HaveQueryExecutingInQueryExecutionRange()
	{
		DataCosmosDbEventId.QueryExecuting.ShouldBe(102300);
	}

	[Fact]
	public void HaveAllQueryExecutionEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.QueryExecuting.ShouldBeInRange(102300, 102399);
		DataCosmosDbEventId.QueryExecuted.ShouldBeInRange(102300, 102399);
		DataCosmosDbEventId.QueryContinuationUsed.ShouldBeInRange(102300, 102399);
		DataCosmosDbEventId.CrossPartitionQueryExecuted.ShouldBeInRange(102300, 102399);
		DataCosmosDbEventId.QueryMetricsCollected.ShouldBeInRange(102300, 102399);
	}

	#endregion

	#region Change Feed Event ID Tests (102400-102499)

	[Fact]
	public void HaveChangeFeedProcessorStartedInChangeFeedRange()
	{
		DataCosmosDbEventId.ChangeFeedProcessorStarted.ShouldBe(102400);
	}

	[Fact]
	public void HaveAllChangeFeedEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.ChangeFeedProcessorStarted.ShouldBeInRange(102400, 102499);
		DataCosmosDbEventId.ChangeFeedProcessorStopped.ShouldBeInRange(102400, 102499);
		DataCosmosDbEventId.ChangeFeedItemsProcessed.ShouldBeInRange(102400, 102499);
		DataCosmosDbEventId.ChangeFeedLeaseAcquired.ShouldBeInRange(102400, 102499);
		DataCosmosDbEventId.ChangeFeedLeaseReleased.ShouldBeInRange(102400, 102499);
		DataCosmosDbEventId.ChangeFeedError.ShouldBeInRange(102400, 102499);
	}

	#endregion

	#region Performance/RU Event ID Tests (102500-102599)

	[Fact]
	public void HaveRequestUnitsConsumedInPerformanceRange()
	{
		DataCosmosDbEventId.RequestUnitsConsumed.ShouldBe(102500);
	}

	[Fact]
	public void HaveAllPerformanceEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.RequestUnitsConsumed.ShouldBeInRange(102500, 102599);
		DataCosmosDbEventId.RequestRateTooHigh.ShouldBeInRange(102500, 102599);
		DataCosmosDbEventId.ThrottlingDetected.ShouldBeInRange(102500, 102599);
		DataCosmosDbEventId.SessionConsistencyUsed.ShouldBeInRange(102500, 102599);
		DataCosmosDbEventId.PointReadExecuted.ShouldBeInRange(102500, 102599);
	}

	#endregion

	#region Error Handling Event ID Tests (102600-102699)

	[Fact]
	public void HaveConflictDetectedInErrorHandlingRange()
	{
		DataCosmosDbEventId.ConflictDetected.ShouldBe(102600);
	}

	[Fact]
	public void HaveAllErrorHandlingEventIdsInExpectedRange()
	{
		DataCosmosDbEventId.ConflictDetected.ShouldBeInRange(102600, 102699);
		DataCosmosDbEventId.NotFoundResponse.ShouldBeInRange(102600, 102699);
		DataCosmosDbEventId.PreconditionFailed.ShouldBeInRange(102600, 102699);
		DataCosmosDbEventId.ServiceUnavailable.ShouldBeInRange(102600, 102699);
		DataCosmosDbEventId.CosmosException.ShouldBeInRange(102600, 102699);
	}

	#endregion

	#region CosmosDb Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInCosmosDbReservedRange()
	{
		// CosmosDb reserved range is 102000-103799
		var allEventIds = GetAllCosmosDbEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(102000, 103799,
				$"Event ID {eventId} is outside CosmosDb reserved range (102000-103799)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllCosmosDbEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllCosmosDbEventIds();
		allEventIds.Length.ShouldBeGreaterThan(60);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllCosmosDbEventIds()
	{
		return
		[
			// Client Management (102000-102099)
			DataCosmosDbEventId.ClientCreated,
			DataCosmosDbEventId.ClientDisposed,
			DataCosmosDbEventId.ConnectionModeConfigured,
			DataCosmosDbEventId.PreferredRegionsConfigured,
			DataCosmosDbEventId.RetryPolicyConfigured,

			// Container Operations (102100-102199)
			DataCosmosDbEventId.ContainerCreated,
			DataCosmosDbEventId.ContainerRead,
			DataCosmosDbEventId.ContainerDeleted,
			DataCosmosDbEventId.ContainerThroughputUpdated,
			DataCosmosDbEventId.PartitionKeyConfigured,

			// Document Operations (102200-102299)
			DataCosmosDbEventId.DocumentCreated,
			DataCosmosDbEventId.DocumentRead,
			DataCosmosDbEventId.DocumentReplaced,
			DataCosmosDbEventId.DocumentUpserted,
			DataCosmosDbEventId.DocumentDeleted,
			DataCosmosDbEventId.DocumentPatched,
			DataCosmosDbEventId.TransactionalBatchExecuted,

			// Query Execution (102300-102399)
			DataCosmosDbEventId.QueryExecuting,
			DataCosmosDbEventId.QueryExecuted,
			DataCosmosDbEventId.QueryContinuationUsed,
			DataCosmosDbEventId.CrossPartitionQueryExecuted,
			DataCosmosDbEventId.QueryMetricsCollected,

			// Change Feed (102400-102499)
			DataCosmosDbEventId.ChangeFeedProcessorStarted,
			DataCosmosDbEventId.ChangeFeedProcessorStopped,
			DataCosmosDbEventId.ChangeFeedItemsProcessed,
			DataCosmosDbEventId.ChangeFeedLeaseAcquired,
			DataCosmosDbEventId.ChangeFeedLeaseReleased,
			DataCosmosDbEventId.ChangeFeedError,

			// Performance/RU (102500-102599)
			DataCosmosDbEventId.RequestUnitsConsumed,
			DataCosmosDbEventId.RequestRateTooHigh,
			DataCosmosDbEventId.ThrottlingDetected,
			DataCosmosDbEventId.SessionConsistencyUsed,
			DataCosmosDbEventId.PointReadExecuted,

			// Error Handling (102600-102699)
			DataCosmosDbEventId.ConflictDetected,
			DataCosmosDbEventId.NotFoundResponse,
			DataCosmosDbEventId.PreconditionFailed,
			DataCosmosDbEventId.ServiceUnavailable,
			DataCosmosDbEventId.CosmosException,

			// Persistence Provider (102700-102799)
			DataCosmosDbEventId.ProviderInitializing,
			DataCosmosDbEventId.ProviderDisposing,
			DataCosmosDbEventId.OperationCompletedWithCharge,
			DataCosmosDbEventId.OperationFailed,

			// Change Feed Subscription (102800-102899)
			DataCosmosDbEventId.ChangeFeedSubscriptionStarting,
			DataCosmosDbEventId.ChangeFeedSubscriptionStopping,
			DataCosmosDbEventId.ChangeFeedReceivedBatch,

			// Inbox Store (102900-102999)
			DataCosmosDbEventId.InboxMessageStored,
			DataCosmosDbEventId.InboxMessageCompleted,
			DataCosmosDbEventId.InboxFirstProcessor,
			DataCosmosDbEventId.InboxDuplicateDetected,
			DataCosmosDbEventId.InboxMessageFailed,
			DataCosmosDbEventId.InboxCleanupComplete,

			// Snapshot Store (103000-103099)
			DataCosmosDbEventId.SnapshotStoreInitialized,
			DataCosmosDbEventId.SnapshotSaved,
			DataCosmosDbEventId.SnapshotVersionSkipped,
			DataCosmosDbEventId.SnapshotDeleted,
			DataCosmosDbEventId.SnapshotOlderDeleted,

			// Saga Store (103100-103199)
			DataCosmosDbEventId.SagaStoreInitialized,
			DataCosmosDbEventId.SagaStateLoaded,
			DataCosmosDbEventId.SagaStateSaved,

			// Projection Store (103200-103299)
			DataCosmosDbEventId.ProjectionStoreInitialized,
			DataCosmosDbEventId.ProjectionUpserted,
			DataCosmosDbEventId.ProjectionDeleted,

			// Outbox Store (103300-103399)
			DataCosmosDbEventId.OutboxStoreInitialized,
			DataCosmosDbEventId.MessageStaged,
			DataCosmosDbEventId.MessageEnqueued,
			DataCosmosDbEventId.MessageSent,
			DataCosmosDbEventId.MessageFailed,
			DataCosmosDbEventId.MessagesCleanedUp,
			DataCosmosDbEventId.ConcurrencyConflict,

			// CDC Processor (103400-103499)
			DataCosmosDbEventId.CdcStarting,
			DataCosmosDbEventId.CdcStopping,
			DataCosmosDbEventId.CdcPositionRestored,
			DataCosmosDbEventId.CdcPositionConfirmed,
			DataCosmosDbEventId.CdcBatchProcessed,
			DataCosmosDbEventId.CdcProcessingError,
			DataCosmosDbEventId.CdcBatchError,

			// CDC State Store (103500-103599)
			DataCosmosDbEventId.CdcPositionLoaded,
			DataCosmosDbEventId.CdcPositionNotFound,
			DataCosmosDbEventId.CdcPositionSaved,
			DataCosmosDbEventId.CdcPositionDeleted,
			DataCosmosDbEventId.CdcPositionNotFoundForDeletion,

			// Grant Service (103600-103699)
			DataCosmosDbEventId.GrantServiceInitialized,
			DataCosmosDbEventId.GrantSaved,
			DataCosmosDbEventId.GrantDeleted,
			DataCosmosDbEventId.GrantRevoked,

			// Activity Group Service (103700-103799)
			DataCosmosDbEventId.ActivityGroupServiceInitialized,
			DataCosmosDbEventId.ActivityGroupGrantSaved,
			DataCosmosDbEventId.ActivityGroupGrantsDeletedByUser,
			DataCosmosDbEventId.ActivityGroupAllGrantsDeleted
		];
	}

	#endregion
}
