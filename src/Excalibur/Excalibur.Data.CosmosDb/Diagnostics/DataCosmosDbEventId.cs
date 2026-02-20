// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CosmosDb.Diagnostics;

/// <summary>
/// Event IDs for Azure Cosmos DB data access (102000-102999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>102000-102099: Client Management</item>
/// <item>102100-102199: Container Operations</item>
/// <item>102200-102299: Document Operations</item>
/// <item>102300-102399: Query Execution</item>
/// <item>102400-102499: Change Feed</item>
/// <item>102500-102599: Performance/RU</item>
/// <item>102600-102699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataCosmosDbEventId
{
	// ========================================
	// 102000-102099: Client Management
	// ========================================

	/// <summary>Cosmos DB client created.</summary>
	public const int ClientCreated = 102000;

	/// <summary>Cosmos DB client disposed.</summary>
	public const int ClientDisposed = 102001;

	/// <summary>Connection mode configured.</summary>
	public const int ConnectionModeConfigured = 102002;

	/// <summary>Preferred regions configured.</summary>
	public const int PreferredRegionsConfigured = 102003;

	/// <summary>Client retry policy configured.</summary>
	public const int RetryPolicyConfigured = 102004;

	// ========================================
	// 102100-102199: Container Operations
	// ========================================

	/// <summary>Container created.</summary>
	public const int ContainerCreated = 102100;

	/// <summary>Container read.</summary>
	public const int ContainerRead = 102101;

	/// <summary>Container deleted.</summary>
	public const int ContainerDeleted = 102102;

	/// <summary>Container throughput updated.</summary>
	public const int ContainerThroughputUpdated = 102103;

	/// <summary>Partition key configured.</summary>
	public const int PartitionKeyConfigured = 102104;

	// ========================================
	// 102200-102299: Document Operations
	// ========================================

	/// <summary>Document created.</summary>
	public const int DocumentCreated = 102200;

	/// <summary>Document read.</summary>
	public const int DocumentRead = 102201;

	/// <summary>Document replaced.</summary>
	public const int DocumentReplaced = 102202;

	/// <summary>Document upserted.</summary>
	public const int DocumentUpserted = 102203;

	/// <summary>Document deleted.</summary>
	public const int DocumentDeleted = 102204;

	/// <summary>Document patch applied.</summary>
	public const int DocumentPatched = 102205;

	/// <summary>Transactional batch executed.</summary>
	public const int TransactionalBatchExecuted = 102206;

	// ========================================
	// 102300-102399: Query Execution
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 102300;

	/// <summary>Query executed successfully.</summary>
	public const int QueryExecuted = 102301;

	/// <summary>Query continuation used.</summary>
	public const int QueryContinuationUsed = 102302;

	/// <summary>Cross-partition query executed.</summary>
	public const int CrossPartitionQueryExecuted = 102303;

	/// <summary>Query metrics collected.</summary>
	public const int QueryMetricsCollected = 102304;

	// ========================================
	// 102400-102499: Change Feed
	// ========================================

	/// <summary>Change feed processor started.</summary>
	public const int ChangeFeedProcessorStarted = 102400;

	/// <summary>Change feed processor stopped.</summary>
	public const int ChangeFeedProcessorStopped = 102401;

	/// <summary>Change feed items processed.</summary>
	public const int ChangeFeedItemsProcessed = 102402;

	/// <summary>Change feed lease acquired.</summary>
	public const int ChangeFeedLeaseAcquired = 102403;

	/// <summary>Change feed lease released.</summary>
	public const int ChangeFeedLeaseReleased = 102404;

	/// <summary>Change feed error occurred.</summary>
	public const int ChangeFeedError = 102405;

	// ========================================
	// 102500-102599: Performance/RU
	// ========================================

	/// <summary>Request units consumed.</summary>
	public const int RequestUnitsConsumed = 102500;

	/// <summary>Request rate too high (429).</summary>
	public const int RequestRateTooHigh = 102501;

	/// <summary>Throttling detected.</summary>
	public const int ThrottlingDetected = 102502;

	/// <summary>Session consistency used.</summary>
	public const int SessionConsistencyUsed = 102503;

	/// <summary>Point read executed.</summary>
	public const int PointReadExecuted = 102504;

	// ========================================
	// 102600-102699: Error Handling
	// ========================================

	/// <summary>Conflict detected.</summary>
	public const int ConflictDetected = 102600;

	/// <summary>Not found response.</summary>
	public const int NotFoundResponse = 102601;

	/// <summary>Precondition failed.</summary>
	public const int PreconditionFailed = 102602;

	/// <summary>Service unavailable.</summary>
	public const int ServiceUnavailable = 102603;

	/// <summary>Cosmos exception occurred.</summary>
	public const int CosmosException = 102604;

	// ========================================
	// 102700-102799: Persistence Provider
	// ========================================

	/// <summary>Initializing Cosmos DB provider.</summary>
	public const int ProviderInitializing = 102700;

	/// <summary>Disposing Cosmos DB provider.</summary>
	public const int ProviderDisposing = 102701;

	/// <summary>Operation completed with request charge.</summary>
	public const int OperationCompletedWithCharge = 102702;

	/// <summary>Operation failed.</summary>
	public const int OperationFailed = 102703;

	// ========================================
	// 102800-102899: Change Feed Subscription
	// ========================================

	/// <summary>Change feed subscription starting.</summary>
	public const int ChangeFeedSubscriptionStarting = 102800;

	/// <summary>Change feed subscription stopping.</summary>
	public const int ChangeFeedSubscriptionStopping = 102801;

	/// <summary>Change feed received batch.</summary>
	public const int ChangeFeedReceivedBatch = 102802;

	// ========================================
	// 102900-102999: Inbox Store
	// ========================================

	/// <summary>Inbox message stored.</summary>
	public const int InboxMessageStored = 102900;

	/// <summary>Inbox message marked processed.</summary>
	public const int InboxMessageCompleted = 102901;

	/// <summary>Inbox first processor.</summary>
	public const int InboxFirstProcessor = 102902;

	/// <summary>Inbox duplicate detected.</summary>
	public const int InboxDuplicateDetected = 102903;

	/// <summary>Inbox message marked failed.</summary>
	public const int InboxMessageFailed = 102904;

	/// <summary>Inbox cleanup complete.</summary>
	public const int InboxCleanupComplete = 102905;

	// ========================================
	// 103000-103099: Snapshot Store
	// ========================================

	/// <summary>Snapshot store initialized.</summary>
	public const int SnapshotStoreInitialized = 103000;

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 103001;

	/// <summary>Snapshot version skipped.</summary>
	public const int SnapshotVersionSkipped = 103002;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 103003;

	/// <summary>Snapshot older than version deleted.</summary>
	public const int SnapshotOlderDeleted = 103004;

	// ========================================
	// 103100-103199: Saga Store
	// ========================================

	/// <summary>Saga store initialized.</summary>
	public const int SagaStoreInitialized = 103100;

	/// <summary>Saga state loaded.</summary>
	public const int SagaStateLoaded = 103101;

	/// <summary>Saga state saved.</summary>
	public const int SagaStateSaved = 103102;

	// ========================================
	// 103200-103299: Projection Store
	// ========================================

	/// <summary>Projection store initialized.</summary>
	public const int ProjectionStoreInitialized = 103200;

	/// <summary>Projection upserted.</summary>
	public const int ProjectionUpserted = 103201;

	/// <summary>Projection deleted.</summary>
	public const int ProjectionDeleted = 103202;

	// ========================================
	// 103300-103399: Outbox Store
	// ========================================

	/// <summary>Outbox store initialized.</summary>
	public const int OutboxStoreInitialized = 103300;

	/// <summary>Message staged.</summary>
	public const int MessageStaged = 103301;

	/// <summary>Message enqueued.</summary>
	public const int MessageEnqueued = 103302;

	/// <summary>Message sent.</summary>
	public const int MessageSent = 103303;

	/// <summary>Message failed.</summary>
	public const int MessageFailed = 103304;

	/// <summary>Messages cleaned up.</summary>
	public const int MessagesCleanedUp = 103305;

	/// <summary>Concurrency conflict detected.</summary>
	public const int ConcurrencyConflict = 103306;

	// ========================================
	// 103400-103499: CDC Processor
	// ========================================

	/// <summary>CDC processor starting.</summary>
	public const int CdcStarting = 103400;

	/// <summary>CDC processor stopping.</summary>
	public const int CdcStopping = 103401;

	/// <summary>CDC position restored.</summary>
	public const int CdcPositionRestored = 103402;

	/// <summary>CDC position confirmed.</summary>
	public const int CdcPositionConfirmed = 103403;

	/// <summary>CDC batch processed.</summary>
	public const int CdcBatchProcessed = 103404;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 103405;

	/// <summary>CDC batch error.</summary>
	public const int CdcBatchError = 103406;

	// ========================================
	// 103500-103599: CDC State Store
	// ========================================

	/// <summary>CDC position loaded.</summary>
	public const int CdcPositionLoaded = 103500;

	/// <summary>CDC position not found.</summary>
	public const int CdcPositionNotFound = 103501;

	/// <summary>CDC position saved.</summary>
	public const int CdcPositionSaved = 103502;

	/// <summary>CDC position deleted.</summary>
	public const int CdcPositionDeleted = 103503;

	/// <summary>CDC position not found for deletion.</summary>
	public const int CdcPositionNotFoundForDeletion = 103504;

	// ========================================
	// 103600-103699: Grant Service
	// ========================================

	/// <summary>Grant service initialized.</summary>
	public const int GrantServiceInitialized = 103600;

	/// <summary>Grant saved.</summary>
	public const int GrantSaved = 103601;

	/// <summary>Grant deleted.</summary>
	public const int GrantDeleted = 103602;

	/// <summary>Grant revoked.</summary>
	public const int GrantRevoked = 103603;

	// ========================================
	// 103700-103799: Activity Group Service
	// ========================================

	/// <summary>Activity group service initialized.</summary>
	public const int ActivityGroupServiceInitialized = 103700;

	/// <summary>Activity group grant saved.</summary>
	public const int ActivityGroupGrantSaved = 103701;

	/// <summary>Activity group grants deleted by user.</summary>
	public const int ActivityGroupGrantsDeletedByUser = 103702;

	/// <summary>All activity group grants deleted.</summary>
	public const int ActivityGroupAllGrantsDeleted = 103703;
}
