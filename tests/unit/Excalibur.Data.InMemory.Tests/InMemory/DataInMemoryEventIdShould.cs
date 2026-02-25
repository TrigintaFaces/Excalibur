// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Diagnostics;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="DataInMemoryEventId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DataInMemoryEventIdShould : UnitTestBase
{
	#region Connection/Initialization Event IDs (105000-105099)

	[Fact]
	public void HaveConnectionTestSuccessfulInConnectionRange()
	{
		// Assert
		DataInMemoryEventId.ConnectionTestSuccessful.ShouldBe(105000);
	}

	[Fact]
	public void HaveConnectionTestFailedInConnectionRange()
	{
		// Assert
		DataInMemoryEventId.ConnectionTestFailed.ShouldBe(105001);
	}

	[Fact]
	public void HaveInitializingProviderInConnectionRange()
	{
		// Assert
		DataInMemoryEventId.InitializingProvider.ShouldBe(105002);
	}

	[Fact]
	public void HaveDisposingProviderInConnectionRange()
	{
		// Assert
		DataInMemoryEventId.DisposingProvider.ShouldBe(105003);
	}

	[Fact]
	public void HaveClearedAllDataInConnectionRange()
	{
		// Assert
		DataInMemoryEventId.ClearedAllData.ShouldBe(105004);
	}

	#endregion Connection/Initialization Event IDs (105000-105099)

	#region Data Request Execution Event IDs (105100-105199)

	[Fact]
	public void HaveExecutingDataRequestInExecutionRange()
	{
		// Assert
		DataInMemoryEventId.ExecutingDataRequest.ShouldBe(105100);
	}

	[Fact]
	public void HaveExecuteDataRequestFailedInExecutionRange()
	{
		// Assert
		DataInMemoryEventId.ExecuteDataRequestFailed.ShouldBe(105101);
	}

	[Fact]
	public void HaveExecutingDataRequestInTransactionInExecutionRange()
	{
		// Assert
		DataInMemoryEventId.ExecutingDataRequestInTransaction.ShouldBe(105102);
	}

	[Fact]
	public void HaveExecuteDataRequestInTransactionFailedInExecutionRange()
	{
		// Assert
		DataInMemoryEventId.ExecuteDataRequestInTransactionFailed.ShouldBe(105103);
	}

	#endregion Data Request Execution Event IDs (105100-105199)

	#region CRUD Operations Event IDs (105200-105299)

	[Fact]
	public void HaveStoredItemInCrudRange()
	{
		// Assert
		DataInMemoryEventId.StoredItem.ShouldBe(105200);
	}

	[Fact]
	public void HaveRetrievedItemInCrudRange()
	{
		// Assert
		DataInMemoryEventId.RetrievedItem.ShouldBe(105201);
	}

	[Fact]
	public void HaveItemNotFoundInCrudRange()
	{
		// Assert
		DataInMemoryEventId.ItemNotFound.ShouldBe(105202);
	}

	[Fact]
	public void HaveRemovedItemInCrudRange()
	{
		// Assert
		DataInMemoryEventId.RemovedItem.ShouldBe(105203);
	}

	#endregion CRUD Operations Event IDs (105200-105299)

	#region Persistence Load Event IDs (105300-105399)

	[Fact]
	public void HaveLoadingPersistedDataInLoadRange()
	{
		// Assert
		DataInMemoryEventId.LoadingPersistedData.ShouldBe(105300);
	}

	[Fact]
	public void HaveLoadingDataFromDiskInLoadRange()
	{
		// Assert
		DataInMemoryEventId.LoadingDataFromDisk.ShouldBe(105301);
	}

	[Fact]
	public void HavePersistenceFileEmptyInLoadRange()
	{
		// Assert
		DataInMemoryEventId.PersistenceFileEmpty.ShouldBe(105302);
	}

	[Fact]
	public void HaveInvalidPersistenceDataFormatInLoadRange()
	{
		// Assert
		DataInMemoryEventId.InvalidPersistenceDataFormat.ShouldBe(105303);
	}

	[Fact]
	public void HaveSuccessfullyLoadedDataInLoadRange()
	{
		// Assert
		DataInMemoryEventId.SuccessfullyLoadedData.ShouldBe(105304);
	}

	[Fact]
	public void HaveFailedToDeserializePersistenceDataInLoadRange()
	{
		// Assert
		DataInMemoryEventId.FailedToDeserializePersistenceData.ShouldBe(105305);
	}

	[Fact]
	public void HavePersistenceFileNotFoundInLoadRange()
	{
		// Assert
		DataInMemoryEventId.PersistenceFileNotFound.ShouldBe(105306);
	}

	[Fact]
	public void HaveAccessDeniedReadingPersistenceFileInLoadRange()
	{
		// Assert
		DataInMemoryEventId.AccessDeniedReadingPersistenceFile.ShouldBe(105307);
	}

	[Fact]
	public void HaveIOErrorReadingPersistenceFileInLoadRange()
	{
		// Assert
		DataInMemoryEventId.IOErrorReadingPersistenceFile.ShouldBe(105308);
	}

	[Fact]
	public void HaveUnexpectedErrorLoadingDataInLoadRange()
	{
		// Assert
		DataInMemoryEventId.UnexpectedErrorLoadingData.ShouldBe(105309);
	}

	#endregion Persistence Load Event IDs (105300-105399)

	#region Persistence Save Event IDs (105400-105499)

	[Fact]
	public void HavePersistingDataToDiskInSaveRange()
	{
		// Assert
		DataInMemoryEventId.PersistingDataToDisk.ShouldBe(105400);
	}

	[Fact]
	public void HaveCreatedDirectoryForPersistenceFileInSaveRange()
	{
		// Assert
		DataInMemoryEventId.CreatedDirectoryForPersistenceFile.ShouldBe(105401);
	}

	[Fact]
	public void HaveSuccessfullyPersistedDataInSaveRange()
	{
		// Assert
		DataInMemoryEventId.SuccessfullyPersistedData.ShouldBe(105402);
	}

	[Fact]
	public void HaveAccessDeniedWritingPersistenceFileInSaveRange()
	{
		// Assert
		DataInMemoryEventId.AccessDeniedWritingPersistenceFile.ShouldBe(105403);
	}

	[Fact]
	public void HaveDirectoryNotFoundForPersistenceFileInSaveRange()
	{
		// Assert
		DataInMemoryEventId.DirectoryNotFoundForPersistenceFile.ShouldBe(105404);
	}

	[Fact]
	public void HaveIOErrorWritingPersistenceFileInSaveRange()
	{
		// Assert
		DataInMemoryEventId.IOErrorWritingPersistenceFile.ShouldBe(105405);
	}

	[Fact]
	public void HaveFailedToSerializeDataInSaveRange()
	{
		// Assert
		DataInMemoryEventId.FailedToSerializeData.ShouldBe(105406);
	}

	[Fact]
	public void HaveUnexpectedErrorPersistingDataInSaveRange()
	{
		// Assert
		DataInMemoryEventId.UnexpectedErrorPersistingData.ShouldBe(105407);
	}

	[Fact]
	public void HaveFailedToPersistOnDisposeInSaveRange()
	{
		// Assert
		DataInMemoryEventId.FailedToPersistOnDispose.ShouldBe(105408);
	}

	[Fact]
	public void HaveFailedToPersistOnAsyncDisposeInSaveRange()
	{
		// Assert
		DataInMemoryEventId.FailedToPersistOnAsyncDispose.ShouldBe(105409);
	}

	#endregion Persistence Save Event IDs (105400-105499)

	#region Transaction Event IDs (105500-105599)

	[Fact]
	public void HaveTransactionCommittedInTransactionRange()
	{
		// Assert
		DataInMemoryEventId.TransactionCommitted.ShouldBe(105500);
	}

	[Fact]
	public void HaveTransactionRolledBackInTransactionRange()
	{
		// Assert
		DataInMemoryEventId.TransactionRolledBack.ShouldBe(105501);
	}

	[Fact]
	public void HaveTransactionDisposedWithoutCommitInTransactionRange()
	{
		// Assert
		DataInMemoryEventId.TransactionDisposedWithoutCommit.ShouldBe(105502);
	}

	#endregion Transaction Event IDs (105500-105599)

	#region Snapshot Store Event IDs (105600-105699)

	[Fact]
	public void HaveSnapshotRetrievedInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotRetrieved.ShouldBe(105600);
	}

	[Fact]
	public void HaveSnapshotNotFoundInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotNotFound.ShouldBe(105601);
	}

	[Fact]
	public void HaveSnapshotSavedInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotSaved.ShouldBe(105602);
	}

	[Fact]
	public void HaveSnapshotDeletedInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotDeleted.ShouldBe(105603);
	}

	[Fact]
	public void HaveSnapshotOlderDeletedInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotOlderDeleted.ShouldBe(105604);
	}

	[Fact]
	public void HaveSnapshotEvictedInSnapshotRange()
	{
		// Assert
		DataInMemoryEventId.SnapshotEvicted.ShouldBe(105605);
	}

	#endregion Snapshot Store Event IDs (105600-105699)

	#region Outbox Store Event IDs (105700-105799)

	[Fact]
	public void HaveOutboxMessageStagedInOutboxRange()
	{
		// Assert
		DataInMemoryEventId.OutboxMessageStaged.ShouldBe(105700);
	}

	[Fact]
	public void HaveOutboxMessageEnqueuedInOutboxRange()
	{
		// Assert
		DataInMemoryEventId.OutboxMessageEnqueued.ShouldBe(105701);
	}

	[Fact]
	public void HaveOutboxMessageSentInOutboxRange()
	{
		// Assert
		DataInMemoryEventId.OutboxMessageSent.ShouldBe(105702);
	}

	[Fact]
	public void HaveOutboxMessageFailedInOutboxRange()
	{
		// Assert
		DataInMemoryEventId.OutboxMessageFailed.ShouldBe(105703);
	}

	[Fact]
	public void HaveOutboxCleanedUpInOutboxRange()
	{
		// Assert
		DataInMemoryEventId.OutboxCleanedUp.ShouldBe(105704);
	}

	#endregion Outbox Store Event IDs (105700-105799)

	#region Event ID Range Validation

	[Fact]
	public void HaveAllConnectionEventIdsInExpectedRange()
	{
		// Assert
		DataInMemoryEventId.ConnectionTestSuccessful.ShouldBeInRange(105000, 105099);
		DataInMemoryEventId.ConnectionTestFailed.ShouldBeInRange(105000, 105099);
		DataInMemoryEventId.InitializingProvider.ShouldBeInRange(105000, 105099);
		DataInMemoryEventId.DisposingProvider.ShouldBeInRange(105000, 105099);
		DataInMemoryEventId.ClearedAllData.ShouldBeInRange(105000, 105099);
	}

	[Fact]
	public void HaveAllExecutionEventIdsInExpectedRange()
	{
		// Assert
		DataInMemoryEventId.ExecutingDataRequest.ShouldBeInRange(105100, 105199);
		DataInMemoryEventId.ExecuteDataRequestFailed.ShouldBeInRange(105100, 105199);
		DataInMemoryEventId.ExecutingDataRequestInTransaction.ShouldBeInRange(105100, 105199);
		DataInMemoryEventId.ExecuteDataRequestInTransactionFailed.ShouldBeInRange(105100, 105199);
	}

	[Fact]
	public void HaveAllCrudEventIdsInExpectedRange()
	{
		// Assert
		DataInMemoryEventId.StoredItem.ShouldBeInRange(105200, 105299);
		DataInMemoryEventId.RetrievedItem.ShouldBeInRange(105200, 105299);
		DataInMemoryEventId.ItemNotFound.ShouldBeInRange(105200, 105299);
		DataInMemoryEventId.RemovedItem.ShouldBeInRange(105200, 105299);
	}

	[Fact]
	public void HaveAllTransactionEventIdsInExpectedRange()
	{
		// Assert
		DataInMemoryEventId.TransactionCommitted.ShouldBeInRange(105500, 105599);
		DataInMemoryEventId.TransactionRolledBack.ShouldBeInRange(105500, 105599);
		DataInMemoryEventId.TransactionDisposedWithoutCommit.ShouldBeInRange(105500, 105599);
	}

	[Fact]
	public void HaveUniqueEventIdsAcrossCategories()
	{
		// Arrange - Collect a sampling of event IDs from different categories
		var eventIds = new[]
		{
			DataInMemoryEventId.ConnectionTestSuccessful,
			DataInMemoryEventId.ExecutingDataRequest,
			DataInMemoryEventId.StoredItem,
			DataInMemoryEventId.LoadingPersistedData,
			DataInMemoryEventId.PersistingDataToDisk,
			DataInMemoryEventId.TransactionCommitted,
			DataInMemoryEventId.SnapshotRetrieved,
			DataInMemoryEventId.OutboxMessageStaged,
		};

		// Assert - All event IDs should be unique
		eventIds.Distinct().Count().ShouldBe(eventIds.Length);
	}

	#endregion Event ID Range Validation
}
