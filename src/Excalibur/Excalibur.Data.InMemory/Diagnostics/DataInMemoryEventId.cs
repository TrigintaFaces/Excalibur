// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.InMemory.Diagnostics;

/// <summary>
/// Event IDs for in-memory persistence provider (105000-105999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>105000-105099: Connection/Initialization</item>
/// <item>105100-105199: Data Request Execution</item>
/// <item>105200-105299: CRUD Operations</item>
/// <item>105300-105399: Persistence (Load)</item>
/// <item>105400-105499: Persistence (Save)</item>
/// <item>105500-105599: Transaction</item>
/// </list>
/// </remarks>
public static class DataInMemoryEventId
{
	// ========================================
	// 105000-105099: Connection/Initialization
	// ========================================

	/// <summary>Connection test successful.</summary>
	public const int ConnectionTestSuccessful = 105000;

	/// <summary>Connection test failed.</summary>
	public const int ConnectionTestFailed = 105001;

	/// <summary>Initializing provider.</summary>
	public const int InitializingProvider = 105002;

	/// <summary>Disposing provider.</summary>
	public const int DisposingProvider = 105003;

	/// <summary>Cleared all data.</summary>
	public const int ClearedAllData = 105004;

	// ========================================
	// 105100-105199: Data Request Execution
	// ========================================

	/// <summary>Executing data request.</summary>
	public const int ExecutingDataRequest = 105100;

	/// <summary>Data request execution failed.</summary>
	public const int ExecuteDataRequestFailed = 105101;

	/// <summary>Executing data request in transaction.</summary>
	public const int ExecutingDataRequestInTransaction = 105102;

	/// <summary>Data request in transaction failed.</summary>
	public const int ExecuteDataRequestInTransactionFailed = 105103;

	// ========================================
	// 105200-105299: CRUD Operations
	// ========================================

	/// <summary>Item stored.</summary>
	public const int StoredItem = 105200;

	/// <summary>Item retrieved.</summary>
	public const int RetrievedItem = 105201;

	/// <summary>Item not found.</summary>
	public const int ItemNotFound = 105202;

	/// <summary>Item removed.</summary>
	public const int RemovedItem = 105203;

	// ========================================
	// 105300-105399: Persistence Load
	// ========================================

	/// <summary>Loading persisted data.</summary>
	public const int LoadingPersistedData = 105300;

	/// <summary>Loading data from disk.</summary>
	public const int LoadingDataFromDisk = 105301;

	/// <summary>Persistence file empty.</summary>
	public const int PersistenceFileEmpty = 105302;

	/// <summary>Invalid persistence data format.</summary>
	public const int InvalidPersistenceDataFormat = 105303;

	/// <summary>Successfully loaded data.</summary>
	public const int SuccessfullyLoadedData = 105304;

	/// <summary>Failed to deserialize persistence data.</summary>
	public const int FailedToDeserializePersistenceData = 105305;

	/// <summary>Persistence file not found.</summary>
	public const int PersistenceFileNotFound = 105306;

	/// <summary>Access denied reading persistence file.</summary>
	public const int AccessDeniedReadingPersistenceFile = 105307;

	/// <summary>I/O error reading persistence file.</summary>
	public const int IOErrorReadingPersistenceFile = 105308;

	/// <summary>Unexpected error loading data.</summary>
	public const int UnexpectedErrorLoadingData = 105309;

	// ========================================
	// 105400-105499: Persistence Save
	// ========================================

	/// <summary>Persisting data to disk.</summary>
	public const int PersistingDataToDisk = 105400;

	/// <summary>Created directory for persistence file.</summary>
	public const int CreatedDirectoryForPersistenceFile = 105401;

	/// <summary>Successfully persisted data.</summary>
	public const int SuccessfullyPersistedData = 105402;

	/// <summary>Access denied writing persistence file.</summary>
	public const int AccessDeniedWritingPersistenceFile = 105403;

	/// <summary>Directory not found for persistence file.</summary>
	public const int DirectoryNotFoundForPersistenceFile = 105404;

	/// <summary>I/O error writing persistence file.</summary>
	public const int IOErrorWritingPersistenceFile = 105405;

	/// <summary>Failed to serialize data.</summary>
	public const int FailedToSerializeData = 105406;

	/// <summary>Unexpected error persisting data.</summary>
	public const int UnexpectedErrorPersistingData = 105407;

	/// <summary>Failed to persist on dispose.</summary>
	public const int FailedToPersistOnDispose = 105408;

	/// <summary>Failed to persist on async dispose.</summary>
	public const int FailedToPersistOnAsyncDispose = 105409;

	// ========================================
	// 105500-105599: Transaction
	// ========================================

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 105500;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 105501;

	/// <summary>Transaction disposed without commit.</summary>
	public const int TransactionDisposedWithoutCommit = 105502;

	// ========================================
	// 105600-105699: Snapshot Store
	// ========================================

	/// <summary>Snapshot retrieved.</summary>
	public const int SnapshotRetrieved = 105600;

	/// <summary>Snapshot not found.</summary>
	public const int SnapshotNotFound = 105601;

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 105602;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 105603;

	/// <summary>Snapshot older versions deleted.</summary>
	public const int SnapshotOlderDeleted = 105604;

	/// <summary>Snapshot evicted.</summary>
	public const int SnapshotEvicted = 105605;

	// ========================================
	// 105700-105799: Outbox Store
	// ========================================

	/// <summary>Outbox message staged.</summary>
	public const int OutboxMessageStaged = 105700;

	/// <summary>Outbox message enqueued.</summary>
	public const int OutboxMessageEnqueued = 105701;

	/// <summary>Outbox message sent.</summary>
	public const int OutboxMessageSent = 105702;

	/// <summary>Outbox message failed.</summary>
	public const int OutboxMessageFailed = 105703;

	/// <summary>Outbox cleaned up.</summary>
	public const int OutboxCleanedUp = 105704;
}
