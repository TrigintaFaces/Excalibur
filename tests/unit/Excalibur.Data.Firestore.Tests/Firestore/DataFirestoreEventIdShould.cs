// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="DataFirestoreEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.Firestore")]
[Trait("Priority", "0")]
public sealed class DataFirestoreEventIdShould : UnitTestBase
{
	#region Client Management Event ID Tests (105000-105099)

	[Fact]
	public void HaveClientCreatedInClientManagementRange()
	{
		DataFirestoreEventId.ClientCreated.ShouldBe(105000);
	}

	[Fact]
	public void HaveAllClientManagementEventIdsInExpectedRange()
	{
		DataFirestoreEventId.ClientCreated.ShouldBeInRange(105000, 105099);
		DataFirestoreEventId.ClientDisposed.ShouldBeInRange(105000, 105099);
		DataFirestoreEventId.ProjectIdConfigured.ShouldBeInRange(105000, 105099);
		DataFirestoreEventId.EmulatorConfigured.ShouldBeInRange(105000, 105099);
		DataFirestoreEventId.CredentialsConfigured.ShouldBeInRange(105000, 105099);
	}

	#endregion

	#region Collection Operations Event ID Tests (105100-105199)

	[Fact]
	public void HaveCollectionReferenceObtainedInCollectionOperationsRange()
	{
		DataFirestoreEventId.CollectionReferenceObtained.ShouldBe(105100);
	}

	[Fact]
	public void HaveAllCollectionOperationsEventIdsInExpectedRange()
	{
		DataFirestoreEventId.CollectionReferenceObtained.ShouldBeInRange(105100, 105199);
		DataFirestoreEventId.CollectionGroupQueried.ShouldBeInRange(105100, 105199);
		DataFirestoreEventId.SubcollectionAccessed.ShouldBeInRange(105100, 105199);
		DataFirestoreEventId.CollectionDocumentsListed.ShouldBeInRange(105100, 105199);
	}

	#endregion

	#region Document Operations Event ID Tests (105200-105299)

	[Fact]
	public void HaveDocumentCreatedInDocumentOperationsRange()
	{
		DataFirestoreEventId.DocumentCreated.ShouldBe(105200);
	}

	[Fact]
	public void HaveAllDocumentOperationsEventIdsInExpectedRange()
	{
		DataFirestoreEventId.DocumentCreated.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.DocumentRead.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.DocumentSet.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.DocumentUpdated.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.DocumentDeleted.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.BatchWriteExecuted.ShouldBeInRange(105200, 105299);
		DataFirestoreEventId.DocumentReferenceObtained.ShouldBeInRange(105200, 105299);
	}

	#endregion

	#region Query Execution Event ID Tests (105300-105399)

	[Fact]
	public void HaveQueryExecutingInQueryExecutionRange()
	{
		DataFirestoreEventId.QueryExecuting.ShouldBe(105300);
	}

	[Fact]
	public void HaveAllQueryExecutionEventIdsInExpectedRange()
	{
		DataFirestoreEventId.QueryExecuting.ShouldBeInRange(105300, 105399);
		DataFirestoreEventId.QueryExecuted.ShouldBeInRange(105300, 105399);
		DataFirestoreEventId.QueryFilterApplied.ShouldBeInRange(105300, 105399);
		DataFirestoreEventId.QueryCursorUsed.ShouldBeInRange(105300, 105399);
		DataFirestoreEventId.CompositeIndexRequired.ShouldBeInRange(105300, 105399);
	}

	#endregion

	#region Real-Time Listeners Event ID Tests (105400-105499)

	[Fact]
	public void HaveSnapshotListenerAddedInRealTimeListenersRange()
	{
		DataFirestoreEventId.SnapshotListenerAdded.ShouldBe(105400);
	}

	[Fact]
	public void HaveAllRealTimeListenersEventIdsInExpectedRange()
	{
		DataFirestoreEventId.SnapshotListenerAdded.ShouldBeInRange(105400, 105499);
		DataFirestoreEventId.SnapshotListenerRemoved.ShouldBeInRange(105400, 105499);
		DataFirestoreEventId.SnapshotReceived.ShouldBeInRange(105400, 105499);
		DataFirestoreEventId.DocumentSnapshotChanged.ShouldBeInRange(105400, 105499);
		DataFirestoreEventId.QuerySnapshotReceived.ShouldBeInRange(105400, 105499);
	}

	#endregion

	#region Transactions Event ID Tests (105500-105599)

	[Fact]
	public void HaveTransactionStartedInTransactionsRange()
	{
		DataFirestoreEventId.TransactionStarted.ShouldBe(105500);
	}

	[Fact]
	public void HaveAllTransactionsEventIdsInExpectedRange()
	{
		DataFirestoreEventId.TransactionStarted.ShouldBeInRange(105500, 105599);
		DataFirestoreEventId.TransactionCommitted.ShouldBeInRange(105500, 105599);
		DataFirestoreEventId.TransactionRolledBack.ShouldBeInRange(105500, 105599);
		DataFirestoreEventId.TransactionRetried.ShouldBeInRange(105500, 105599);
		DataFirestoreEventId.TransactionMaxAttemptsExceeded.ShouldBeInRange(105500, 105599);
	}

	#endregion

	#region Error Handling Event ID Tests (105600-105699)

	[Fact]
	public void HaveDocumentNotFoundInErrorHandlingRange()
	{
		DataFirestoreEventId.DocumentNotFound.ShouldBe(105600);
	}

	[Fact]
	public void HaveAllErrorHandlingEventIdsInExpectedRange()
	{
		DataFirestoreEventId.DocumentNotFound.ShouldBeInRange(105600, 105699);
		DataFirestoreEventId.PermissionDenied.ShouldBeInRange(105600, 105699);
		DataFirestoreEventId.QuotaExceeded.ShouldBeInRange(105600, 105699);
		DataFirestoreEventId.AbortedError.ShouldBeInRange(105600, 105699);
		DataFirestoreEventId.FirestoreException.ShouldBeInRange(105600, 105699);
	}

	#endregion

	#region Persistence Provider Event ID Tests (105700-105799)

	[Fact]
	public void HaveProviderInitializingInPersistenceProviderRange()
	{
		DataFirestoreEventId.ProviderInitializing.ShouldBe(105700);
	}

	[Fact]
	public void HaveAllPersistenceProviderEventIdsInExpectedRange()
	{
		DataFirestoreEventId.ProviderInitializing.ShouldBeInRange(105700, 105799);
		DataFirestoreEventId.ProviderDisposing.ShouldBeInRange(105700, 105799);
		DataFirestoreEventId.OperationCompleted.ShouldBeInRange(105700, 105799);
		DataFirestoreEventId.OperationFailed.ShouldBeInRange(105700, 105799);
	}

	#endregion

	#region Health Check Event ID Tests (105800-105899)

	[Fact]
	public void HaveHealthCheckStartedInHealthCheckRange()
	{
		DataFirestoreEventId.HealthCheckStarted.ShouldBe(105800);
	}

	[Fact]
	public void HaveAllHealthCheckEventIdsInExpectedRange()
	{
		DataFirestoreEventId.HealthCheckStarted.ShouldBeInRange(105800, 105899);
		DataFirestoreEventId.HealthCheckCompleted.ShouldBeInRange(105800, 105899);
		DataFirestoreEventId.HealthCheckFailed.ShouldBeInRange(105800, 105899);
	}

	#endregion

	#region CDC Operations Event ID Tests (105900-105999)

	[Fact]
	public void HaveCdcStartingInCdcOperationsRange()
	{
		DataFirestoreEventId.CdcStarting.ShouldBe(105900);
	}

	[Fact]
	public void HaveAllCdcOperationsEventIdsInExpectedRange()
	{
		DataFirestoreEventId.CdcStarting.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcStopping.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcReceivedChanges.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcProcessingChange.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcConfirmingPosition.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcProcessingError.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcResumingFromPosition.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcStartingFromBeginning.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcSavingPosition.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcGettingPosition.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcDeletingPosition.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.CdcPositionNotFound.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.ListenerStarting.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.ListenerStopping.ShouldBeInRange(105900, 105999);
		DataFirestoreEventId.ListenerReceivedChanges.ShouldBeInRange(105900, 105999);
	}

	#endregion

	#region Snapshot Store Event ID Tests (106000-106099)

	[Fact]
	public void HaveSnapshotSavedInSnapshotStoreRange()
	{
		DataFirestoreEventId.SnapshotSaved.ShouldBe(106000);
	}

	[Fact]
	public void HaveAllSnapshotStoreEventIdsInExpectedRange()
	{
		DataFirestoreEventId.SnapshotSaved.ShouldBeInRange(106000, 106099);
		DataFirestoreEventId.SnapshotVersionSkipped.ShouldBeInRange(106000, 106099);
		DataFirestoreEventId.SnapshotRetrieved.ShouldBeInRange(106000, 106099);
		DataFirestoreEventId.SnapshotDeleted.ShouldBeInRange(106000, 106099);
		DataFirestoreEventId.SnapshotsDeletedOlderThan.ShouldBeInRange(106000, 106099);
	}

	#endregion

	#region Grant Service Event ID Tests (106100-106199)

	[Fact]
	public void HaveGrantServiceInitializedInGrantServiceRange()
	{
		DataFirestoreEventId.GrantServiceInitialized.ShouldBe(106100);
	}

	[Fact]
	public void HaveAllGrantServiceEventIdsInExpectedRange()
	{
		DataFirestoreEventId.GrantServiceInitialized.ShouldBeInRange(106100, 106199);
		DataFirestoreEventId.GrantSaved.ShouldBeInRange(106100, 106199);
		DataFirestoreEventId.GrantDeleted.ShouldBeInRange(106100, 106199);
		DataFirestoreEventId.GrantRevoked.ShouldBeInRange(106100, 106199);
	}

	#endregion

	#region Outbox Store Event ID Tests (106200-106299)

	[Fact]
	public void HaveOutboxMessageStagedInOutboxStoreRange()
	{
		DataFirestoreEventId.OutboxMessageStaged.ShouldBe(106200);
	}

	[Fact]
	public void HaveAllOutboxStoreEventIdsInExpectedRange()
	{
		DataFirestoreEventId.OutboxMessageStaged.ShouldBeInRange(106200, 106299);
		DataFirestoreEventId.OutboxMessageEnqueued.ShouldBeInRange(106200, 106299);
		DataFirestoreEventId.OutboxMessageSent.ShouldBeInRange(106200, 106299);
		DataFirestoreEventId.OutboxMessageFailed.ShouldBeInRange(106200, 106299);
		DataFirestoreEventId.OutboxCleanedUp.ShouldBeInRange(106200, 106299);
	}

	#endregion

	#region Activity Group Service Event ID Tests (106300-106399)

	[Fact]
	public void HaveActivityGroupServiceInitializedInActivityGroupRange()
	{
		DataFirestoreEventId.ActivityGroupServiceInitialized.ShouldBe(106300);
	}

	[Fact]
	public void HaveAllActivityGroupServiceEventIdsInExpectedRange()
	{
		DataFirestoreEventId.ActivityGroupServiceInitialized.ShouldBeInRange(106300, 106399);
		DataFirestoreEventId.ActivityGroupGrantInserted.ShouldBeInRange(106300, 106399);
		DataFirestoreEventId.ActivityGroupGrantsDeletedByUser.ShouldBeInRange(106300, 106399);
		DataFirestoreEventId.ActivityGroupAllGrantsDeleted.ShouldBeInRange(106300, 106399);
	}

	#endregion

	#region Inbox Store Event ID Tests (106400-106499)

	[Fact]
	public void HaveInboxEntryCreatedInInboxStoreRange()
	{
		DataFirestoreEventId.InboxEntryCreated.ShouldBe(106400);
	}

	[Fact]
	public void HaveAllInboxStoreEventIdsInExpectedRange()
	{
		DataFirestoreEventId.InboxEntryCreated.ShouldBeInRange(106400, 106499);
		DataFirestoreEventId.InboxEntryProcessed.ShouldBeInRange(106400, 106499);
		DataFirestoreEventId.InboxTryMarkProcessedSuccess.ShouldBeInRange(106400, 106499);
		DataFirestoreEventId.InboxTryMarkProcessedDuplicate.ShouldBeInRange(106400, 106499);
		DataFirestoreEventId.InboxEntryFailed.ShouldBeInRange(106400, 106499);
		DataFirestoreEventId.InboxCleanedUp.ShouldBeInRange(106400, 106499);
	}

	#endregion

	#region Firestore Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInFirestoreReservedRange()
	{
		// Firestore reserved range is 105000-106499
		var allEventIds = GetAllFirestoreEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(105000, 106499,
				$"Event ID {eventId} is outside Firestore reserved range (105000-106499)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllFirestoreEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllFirestoreEventIds();
		allEventIds.Length.ShouldBeGreaterThan(70);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllFirestoreEventIds()
	{
		return
		[
			// Client Management (105000-105099)
			DataFirestoreEventId.ClientCreated,
			DataFirestoreEventId.ClientDisposed,
			DataFirestoreEventId.ProjectIdConfigured,
			DataFirestoreEventId.EmulatorConfigured,
			DataFirestoreEventId.CredentialsConfigured,

			// Collection Operations (105100-105199)
			DataFirestoreEventId.CollectionReferenceObtained,
			DataFirestoreEventId.CollectionGroupQueried,
			DataFirestoreEventId.SubcollectionAccessed,
			DataFirestoreEventId.CollectionDocumentsListed,

			// Document Operations (105200-105299)
			DataFirestoreEventId.DocumentCreated,
			DataFirestoreEventId.DocumentRead,
			DataFirestoreEventId.DocumentSet,
			DataFirestoreEventId.DocumentUpdated,
			DataFirestoreEventId.DocumentDeleted,
			DataFirestoreEventId.BatchWriteExecuted,
			DataFirestoreEventId.DocumentReferenceObtained,

			// Query Execution (105300-105399)
			DataFirestoreEventId.QueryExecuting,
			DataFirestoreEventId.QueryExecuted,
			DataFirestoreEventId.QueryFilterApplied,
			DataFirestoreEventId.QueryCursorUsed,
			DataFirestoreEventId.CompositeIndexRequired,

			// Real-Time Listeners (105400-105499)
			DataFirestoreEventId.SnapshotListenerAdded,
			DataFirestoreEventId.SnapshotListenerRemoved,
			DataFirestoreEventId.SnapshotReceived,
			DataFirestoreEventId.DocumentSnapshotChanged,
			DataFirestoreEventId.QuerySnapshotReceived,

			// Transactions (105500-105599)
			DataFirestoreEventId.TransactionStarted,
			DataFirestoreEventId.TransactionCommitted,
			DataFirestoreEventId.TransactionRolledBack,
			DataFirestoreEventId.TransactionRetried,
			DataFirestoreEventId.TransactionMaxAttemptsExceeded,

			// Error Handling (105600-105699)
			DataFirestoreEventId.DocumentNotFound,
			DataFirestoreEventId.PermissionDenied,
			DataFirestoreEventId.QuotaExceeded,
			DataFirestoreEventId.AbortedError,
			DataFirestoreEventId.FirestoreException,

			// Persistence Provider (105700-105799)
			DataFirestoreEventId.ProviderInitializing,
			DataFirestoreEventId.ProviderDisposing,
			DataFirestoreEventId.OperationCompleted,
			DataFirestoreEventId.OperationFailed,

			// Health Check (105800-105899)
			DataFirestoreEventId.HealthCheckStarted,
			DataFirestoreEventId.HealthCheckCompleted,
			DataFirestoreEventId.HealthCheckFailed,

			// CDC Operations (105900-105999)
			DataFirestoreEventId.CdcStarting,
			DataFirestoreEventId.CdcStopping,
			DataFirestoreEventId.CdcReceivedChanges,
			DataFirestoreEventId.CdcProcessingChange,
			DataFirestoreEventId.CdcConfirmingPosition,
			DataFirestoreEventId.CdcProcessingError,
			DataFirestoreEventId.CdcResumingFromPosition,
			DataFirestoreEventId.CdcStartingFromBeginning,
			DataFirestoreEventId.CdcSavingPosition,
			DataFirestoreEventId.CdcGettingPosition,
			DataFirestoreEventId.CdcDeletingPosition,
			DataFirestoreEventId.CdcPositionNotFound,
			DataFirestoreEventId.ListenerStarting,
			DataFirestoreEventId.ListenerStopping,
			DataFirestoreEventId.ListenerReceivedChanges,

			// Snapshot Store (106000-106099)
			DataFirestoreEventId.SnapshotSaved,
			DataFirestoreEventId.SnapshotVersionSkipped,
			DataFirestoreEventId.SnapshotRetrieved,
			DataFirestoreEventId.SnapshotDeleted,
			DataFirestoreEventId.SnapshotsDeletedOlderThan,

			// Grant Service (106100-106199)
			DataFirestoreEventId.GrantServiceInitialized,
			DataFirestoreEventId.GrantSaved,
			DataFirestoreEventId.GrantDeleted,
			DataFirestoreEventId.GrantRevoked,

			// Outbox Store (106200-106299)
			DataFirestoreEventId.OutboxMessageStaged,
			DataFirestoreEventId.OutboxMessageEnqueued,
			DataFirestoreEventId.OutboxMessageSent,
			DataFirestoreEventId.OutboxMessageFailed,
			DataFirestoreEventId.OutboxCleanedUp,

			// Activity Group Service (106300-106399)
			DataFirestoreEventId.ActivityGroupServiceInitialized,
			DataFirestoreEventId.ActivityGroupGrantInserted,
			DataFirestoreEventId.ActivityGroupGrantsDeletedByUser,
			DataFirestoreEventId.ActivityGroupAllGrantsDeleted,

			// Inbox Store (106400-106499)
			DataFirestoreEventId.InboxEntryCreated,
			DataFirestoreEventId.InboxEntryProcessed,
			DataFirestoreEventId.InboxTryMarkProcessedSuccess,
			DataFirestoreEventId.InboxTryMarkProcessedDuplicate,
			DataFirestoreEventId.InboxEntryFailed,
			DataFirestoreEventId.InboxCleanedUp
		];
	}

	#endregion
}
