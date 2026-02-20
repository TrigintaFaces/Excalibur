// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DynamoDb.Diagnostics;

/// <summary>
/// Event IDs for AWS DynamoDB data access (103000-103999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>103000-103099: Client Management</item>
/// <item>103100-103199: Table Operations</item>
/// <item>103200-103299: Item Operations</item>
/// <item>103300-103399: Query/Scan</item>
/// <item>103400-103499: Streams</item>
/// <item>103500-103599: Performance</item>
/// <item>103600-103699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataDynamoDbEventId
{
	// ========================================
	// 103000-103099: Client Management
	// ========================================

	/// <summary>DynamoDB client created.</summary>
	public const int ClientCreated = 103000;

	/// <summary>DynamoDB client disposed.</summary>
	public const int ClientDisposed = 103001;

	/// <summary>Region configured.</summary>
	public const int RegionConfigured = 103002;

	/// <summary>Endpoint override configured.</summary>
	public const int EndpointOverrideConfigured = 103003;

	/// <summary>Credentials configured.</summary>
	public const int CredentialsConfigured = 103004;

	// ========================================
	// 103100-103199: Table Operations
	// ========================================

	/// <summary>Table created.</summary>
	public const int TableCreated = 103100;

	/// <summary>Table described.</summary>
	public const int TableDescribed = 103101;

	/// <summary>Table deleted.</summary>
	public const int TableDeleted = 103102;

	/// <summary>Table throughput updated.</summary>
	public const int TableThroughputUpdated = 103103;

	/// <summary>Global secondary index created.</summary>
	public const int GlobalSecondaryIndexCreated = 103104;

	// ========================================
	// 103200-103299: Item Operations
	// ========================================

	/// <summary>Item put.</summary>
	public const int ItemPut = 103200;

	/// <summary>Item get.</summary>
	public const int ItemGet = 103201;

	/// <summary>Item updated.</summary>
	public const int ItemUpdated = 103202;

	/// <summary>Item deleted.</summary>
	public const int ItemDeleted = 103203;

	/// <summary>Batch get executed.</summary>
	public const int BatchGetExecuted = 103204;

	/// <summary>Batch write executed.</summary>
	public const int BatchWriteExecuted = 103205;

	/// <summary>Transact write executed.</summary>
	public const int TransactWriteExecuted = 103206;

	/// <summary>Transact get executed.</summary>
	public const int TransactGetExecuted = 103207;

	// ========================================
	// 103300-103399: Query/Scan
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 103300;

	/// <summary>Query executed.</summary>
	public const int QueryExecuted = 103301;

	/// <summary>Scan executing.</summary>
	public const int ScanExecuting = 103302;

	/// <summary>Scan executed.</summary>
	public const int ScanExecuted = 103303;

	/// <summary>Pagination continued.</summary>
	public const int PaginationContinued = 103304;

	/// <summary>Filter expression applied.</summary>
	public const int FilterExpressionApplied = 103305;

	// ========================================
	// 103400-103419: Streams
	// ========================================

	/// <summary>Stream enabled.</summary>
	public const int StreamEnabled = 103400;

	/// <summary>Stream record read.</summary>
	public const int StreamRecordRead = 103401;

	/// <summary>Stream shard iterator obtained.</summary>
	public const int ShardIteratorObtained = 103402;

	/// <summary>Stream processing completed.</summary>
	public const int StreamProcessingCompleted = 103403;

	// ========================================
	// 103420-103439: CDC Processor
	// ========================================

	/// <summary>Starting CDC processor.</summary>
	public const int CdcProcessorStarting = 103420;

	/// <summary>Stopping CDC processor.</summary>
	public const int CdcProcessorStopping = 103421;

	/// <summary>Received CDC batch.</summary>
	public const int CdcBatchReceived = 103422;

	/// <summary>Position confirmed.</summary>
	public const int CdcPositionConfirmed = 103423;

	/// <summary>Shards discovered.</summary>
	public const int CdcShardsDiscovered = 103424;

	/// <summary>Iterator expired.</summary>
	public const int CdcIteratorExpired = 103425;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 103426;

	// ========================================
	// 103440-103459: CDC State Store
	// ========================================

	/// <summary>Position not found.</summary>
	public const int CdcPositionNotFound = 103440;

	/// <summary>Position data missing.</summary>
	public const int CdcPositionDataMissing = 103441;

	/// <summary>Position parse failed.</summary>
	public const int CdcPositionParseFailed = 103442;

	/// <summary>Position loaded.</summary>
	public const int CdcPositionLoaded = 103443;

	/// <summary>Position saved.</summary>
	public const int CdcPositionSaved = 103444;

	/// <summary>Position deleted.</summary>
	public const int CdcPositionDeleted = 103445;

	/// <summary>DynamoDB SDK error during CDC state store operation.</summary>
	public const int CdcStateStoreError = 103446;

	// ========================================
	// 103460-103469: Snapshot Store
	// ========================================

	/// <summary>Snapshot store initialized.</summary>
	public const int SnapshotStoreInitialized = 103460;

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 103461;

	/// <summary>Snapshot skipped (older version).</summary>
	public const int SnapshotSkipped = 103462;

	/// <summary>Snapshot retrieved.</summary>
	public const int SnapshotRetrieved = 103463;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 103464;

	/// <summary>Snapshot deleted older than version.</summary>
	public const int SnapshotDeletedOlderThan = 103465;

	// ========================================
	// 103470-103479: Saga Store
	// ========================================

	/// <summary>Saga store initialized.</summary>
	public const int SagaStoreInitialized = 103470;

	/// <summary>Saga loaded.</summary>
	public const int SagaLoaded = 103471;

	/// <summary>Saga saved.</summary>
	public const int SagaSaved = 103472;

	// ========================================
	// 103480-103489: Grant Service
	// ========================================

	/// <summary>Grant service initialized.</summary>
	public const int GrantServiceInitialized = 103480;

	/// <summary>Grant saved.</summary>
	public const int GrantSaved = 103481;

	/// <summary>Grant deleted.</summary>
	public const int GrantDeleted = 103482;

	/// <summary>Grant revoked.</summary>
	public const int GrantRevoked = 103483;

	// ========================================
	// 103490-103499: Outbox Store
	// ========================================

	/// <summary>Outbox message staged.</summary>
	public const int OutboxMessageStaged = 103490;

	/// <summary>Outbox message enqueued.</summary>
	public const int OutboxMessageEnqueued = 103491;

	/// <summary>Outbox message sent.</summary>
	public const int OutboxMessageSent = 103492;

	/// <summary>Outbox message failed.</summary>
	public const int OutboxMessageFailed = 103493;

	/// <summary>Outbox cleaned up.</summary>
	public const int OutboxCleanedUp = 103494;

	/// <summary>Outbox concurrency conflict.</summary>
	public const int OutboxConcurrencyConflict = 103495;

	// ========================================
	// 103500-103599: Performance
	// ========================================

	/// <summary>Capacity units consumed.</summary>
	public const int CapacityUnitsConsumed = 103500;

	/// <summary>Provisioned throughput exceeded.</summary>
	public const int ProvisionedThroughputExceeded = 103501;

	/// <summary>On-demand mode active.</summary>
	public const int OnDemandModeActive = 103502;

	/// <summary>DAX cache hit.</summary>
	public const int DaxCacheHit = 103503;

	/// <summary>DAX cache miss.</summary>
	public const int DaxCacheMiss = 103504;

	// ========================================
	// 103600-103699: Error Handling
	// ========================================

	/// <summary>Conditional check failed.</summary>
	public const int ConditionalCheckFailed = 103600;

	/// <summary>Item not found.</summary>
	public const int ItemNotFound = 103601;

	/// <summary>Transaction conflict.</summary>
	public const int TransactionConflict = 103602;

	/// <summary>Validation exception.</summary>
	public const int ValidationException = 103603;

	/// <summary>DynamoDB exception occurred.</summary>
	public const int DynamoDbException = 103604;

	// ========================================
	// 103700-103799: Persistence Provider
	// ========================================

	/// <summary>Initializing DynamoDB provider.</summary>
	public const int ProviderInitializing = 103700;

	/// <summary>Disposing DynamoDB provider.</summary>
	public const int ProviderDisposing = 103701;

	/// <summary>Operation completed with capacity.</summary>
	public const int OperationCompletedWithCapacity = 103702;

	/// <summary>Operation failed.</summary>
	public const int OperationFailed = 103703;

	// ========================================
	// 103800-103899: Health Check
	// ========================================

	/// <summary>Health check started.</summary>
	public const int HealthCheckStarted = 103800;

	/// <summary>Health check completed.</summary>
	public const int HealthCheckCompleted = 103801;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 103802;

	// ========================================
	// 103900-103999: Streams Subscription
	// ========================================

	/// <summary>Streams subscription starting.</summary>
	public const int StreamsStarting = 103900;

	/// <summary>Streams subscription stopping.</summary>
	public const int StreamsStopping = 103901;

	/// <summary>Streams received batch.</summary>
	public const int StreamsReceivedBatch = 103902;

	// ========================================
	// 103950-103999: Inbox Store
	// ========================================

	/// <summary>Inbox message stored.</summary>
	public const int InboxMessageStored = 103950;

	/// <summary>Inbox message already processed.</summary>
	public const int InboxMessageAlreadyProcessed = 103951;

	/// <summary>Inbox message completed.</summary>
	public const int InboxMessageCompleted = 103952;

	/// <summary>Inbox message processing error.</summary>
	public const int InboxMessageProcessingError = 103953;

	/// <summary>Inbox message storing error.</summary>
	public const int InboxMessageStoringError = 103954;

	/// <summary>Inbox cleanup complete.</summary>
	public const int InboxCleanupComplete = 103955;

	// ========================================
	// 103960-103969: Activity Group Service
	// ========================================

	/// <summary>Activity group service initialized.</summary>
	public const int ActivityGroupServiceInitialized = 103960;

	/// <summary>Activity group grant inserted.</summary>
	public const int ActivityGroupGrantInserted = 103961;

	/// <summary>Activity group grants deleted by user.</summary>
	public const int ActivityGroupGrantsDeletedByUser = 103962;

	/// <summary>All activity group grants deleted.</summary>
	public const int ActivityGroupAllGrantsDeleted = 103963;
}
