// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Postgres.Diagnostics;

/// <summary>
/// Event IDs for Postgres data access (101000-101999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>101000-101099: Connection Management</item>
/// <item>101100-101199: Query Execution</item>
/// <item>101200-101299: Transaction Management</item>
/// <item>101300-101399: Npgsql Integration</item>
/// <item>101400-101499: Array/JSON Operations</item>
/// <item>101500-101599: Performance</item>
/// <item>101600-101699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataPostgresEventId
{
	// ========================================
	// 101000-101099: Connection Management
	// ========================================

	/// <summary>Postgres connection opened.</summary>
	public const int ConnectionOpened = 101000;

	/// <summary>Postgres connection closed.</summary>
	public const int ConnectionClosed = 101001;

	/// <summary>Connection pool created.</summary>
	public const int ConnectionPoolCreated = 101002;

	/// <summary>Connection acquired from pool.</summary>
	public const int ConnectionAcquired = 101003;

	/// <summary>Connection returned to pool.</summary>
	public const int ConnectionReturned = 101004;

	/// <summary>Connection failed.</summary>
	public const int ConnectionFailed = 101005;

	/// <summary>SSL connection established.</summary>
	public const int SslConnectionEstablished = 101006;

	// ========================================
	// 101100-101199: Query Execution
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 101100;

	/// <summary>Query executed successfully.</summary>
	public const int QueryExecuted = 101101;

	/// <summary>Query failed.</summary>
	public const int QueryFailed = 101102;

	/// <summary>Query timed out.</summary>
	public const int QueryTimeout = 101103;

	/// <summary>Prepared statement created.</summary>
	public const int PreparedStatementCreated = 101104;

	/// <summary>Copy operation executed.</summary>
	public const int CopyOperationExecuted = 101105;

	// ========================================
	// 101200-101299: Transaction Management
	// ========================================

	/// <summary>Transaction started.</summary>
	public const int TransactionStarted = 101200;

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 101201;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 101202;

	/// <summary>Savepoint created.</summary>
	public const int SavepointCreated = 101203;

	/// <summary>Savepoint released.</summary>
	public const int SavepointReleased = 101204;

	/// <summary>Advisory lock acquired.</summary>
	public const int AdvisoryLockAcquired = 101205;

	/// <summary>Advisory lock released.</summary>
	public const int AdvisoryLockReleased = 101206;

	// ========================================
	// 101300-101399: Npgsql Integration
	// ========================================

	/// <summary>Npgsql data source created.</summary>
	public const int NpgsqlDataSourceCreated = 101300;

	/// <summary>Npgsql multiplexing enabled.</summary>
	public const int NpgsqlMultiplexingEnabled = 101301;

	/// <summary>Type mapping configured.</summary>
	public const int TypeMappingConfigured = 101302;

	/// <summary>Notification received.</summary>
	public const int NotificationReceived = 101303;

	// ========================================
	// 101400-101499: Array/JSON Operations
	// ========================================

	/// <summary>Array parameter bound.</summary>
	public const int ArrayParameterBound = 101400;

	/// <summary>JSON column read.</summary>
	public const int JsonColumnRead = 101401;

	/// <summary>JSON column written.</summary>
	public const int JsonColumnWritten = 101402;

	/// <summary>JSONB operation executed.</summary>
	public const int JsonbOperationExecuted = 101403;

	// ========================================
	// 101500-101599: Performance
	// ========================================

	/// <summary>Slow query detected.</summary>
	public const int SlowQueryDetected = 101500;

	/// <summary>Query statistics collected.</summary>
	public const int QueryStatisticsCollected = 101501;

	/// <summary>Execution plan retrieved.</summary>
	public const int ExecutionPlanRetrieved = 101502;

	/// <summary>Wait event detected.</summary>
	public const int WaitEventDetected = 101503;

	// ========================================
	// 101600-101699: Error Handling
	// ========================================

	/// <summary>Deadlock detected.</summary>
	public const int DeadlockDetected = 101600;

	/// <summary>Serialization failure.</summary>
	public const int SerializationFailure = 101601;

	/// <summary>Unique violation detected.</summary>
	public const int UniqueViolation = 101602;

	/// <summary>Foreign key violation detected.</summary>
	public const int ForeignKeyViolation = 101603;

	/// <summary>Postgres error occurred.</summary>
	public const int PostgresError = 101604;

	// ========================================
	// 101700-101799: Retry Policy
	// ========================================

	/// <summary>Retry attempt for transient error.</summary>
	public const int RetryAttempt = 101700;

	// ========================================
	// 101800-101899: Persistence Provider
	// ========================================

	/// <summary>Executing data request.</summary>
	public const int ExecutingDataRequest = 101800;

	/// <summary>Executing data request in transaction.</summary>
	public const int ExecutingDataRequestInTransaction = 101801;

	/// <summary>Failed to execute data request.</summary>
	public const int FailedToExecuteDataRequest = 101802;

	/// <summary>Connection test successful.</summary>
	public const int ConnectionTestSuccessful = 101803;

	/// <summary>Connection test failed.</summary>
	public const int ConnectionTestFailed = 101804;

	/// <summary>Failed to retrieve metrics.</summary>
	public const int FailedToRetrieveMetrics = 101805;

	/// <summary>Initializing provider.</summary>
	public const int InitializingProvider = 101806;

	/// <summary>Failed to retrieve connection pool statistics.</summary>
	public const int FailedToRetrieveConnectionPoolStatistics = 101807;

	/// <summary>Disposing provider.</summary>
	public const int DisposingProvider = 101808;

	/// <summary>Cleared connection pools.</summary>
	public const int ClearedConnectionPools = 101809;

	/// <summary>Error disposing provider.</summary>
	public const int ErrorDisposingProvider = 101810;

	/// <summary>Persistence provider retry attempt.</summary>
	public const int PersistenceRetryAttempt = 101811;

	// ========================================
	// 101900-101999: Outbox Store
	// ========================================

	/// <summary>Saving outbox messages.</summary>
	public const int OutboxSaveMessages = 101900;

	/// <summary>Reserving outbox messages.</summary>
	public const int OutboxReserveMessages = 101901;

	/// <summary>Unreserving outbox messages.</summary>
	public const int OutboxUnreserveMessages = 101902;

	/// <summary>Deleting outbox record.</summary>
	public const int OutboxDeleteRecord = 101903;

	/// <summary>Increasing outbox attempts.</summary>
	public const int OutboxIncreaseAttempts = 101904;

	/// <summary>Moving to dead letter.</summary>
	public const int OutboxMoveToDeadLetter = 101905;

	/// <summary>Outbox batch operation.</summary>
	public const int OutboxBatchOperation = 101906;

	/// <summary>Outbox operation completed.</summary>
	public const int OutboxOperationCompleted = 101907;

	/// <summary>Failed to convert outbox message.</summary>
	public const int OutboxConvertMessageFailed = 101908;

	/// <summary>Get failed messages not supported.</summary>
	public const int OutboxGetFailedMessagesNotSupported = 101909;

	/// <summary>Get scheduled messages not supported.</summary>
	public const int OutboxGetScheduledMessagesNotSupported = 101910;

	/// <summary>Cleanup sent messages not needed.</summary>
	public const int OutboxCleanupSentMessagesNotNeeded = 101911;

	/// <summary>Get statistics basic.</summary>
	public const int OutboxGetStatisticsBasic = 101912;

	// ========================================
	// 102000-102099: Dead Letter Store
	// ========================================

	/// <summary>Stored dead letter message.</summary>
	public const int StoredDeadLetterMessage = 102000;

	/// <summary>Marked dead letter message as replayed.</summary>
	public const int MarkedDeadLetterMessageAsReplayed = 102001;

	/// <summary>Deleted dead letter message.</summary>
	public const int DeletedDeadLetterMessage = 102002;

	/// <summary>Cleaned up old dead letter messages.</summary>
	public const int CleanedUpOldDeadLetterMessages = 102003;

	// ========================================
	// 102100-102199: Health Checker
	// ========================================

	/// <summary>Health check succeeded.</summary>
	public const int HealthCheckSucceeded = 102100;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 102101;

	/// <summary>Quick health check failed.</summary>
	public const int QuickCheckFailed = 102102;

	// ========================================
	// 102200-102299: Connection Factory
	// ========================================

	/// <summary>Created Postgres connection.</summary>
	public const int CreatedPostgresConnection = 102200;

	/// <summary>Failed to create Postgres connection.</summary>
	public const int CreatePostgresConnectionFailed = 102201;

	/// <summary>Connection validation failed.</summary>
	public const int ConnectionValidationFailed = 102202;

	/// <summary>Repair connection failed.</summary>
	public const int RepairConnectionFailed = 102203;

	/// <summary>Dispose connection warning.</summary>
	public const int DisposeConnectionWarning = 102204;

	// ========================================
	// 102300-102399: CDC Processor
	// ========================================

	/// <summary>CDC processor starting.</summary>
	public const int CdcProcessorStarting = 102300;

	/// <summary>CDC resuming from position.</summary>
	public const int CdcResumingFromPosition = 102301;

	/// <summary>CDC connected to replication stream.</summary>
	public const int CdcConnectedToReplicationStream = 102302;

	/// <summary>CDC created replication slot.</summary>
	public const int CdcCreatedReplicationSlot = 102303;

	/// <summary>CDC replication slot already exists.</summary>
	public const int CdcReplicationSlotExists = 102304;

	/// <summary>CDC processed change.</summary>
	public const int CdcProcessedChange = 102305;

	/// <summary>CDC confirmed position.</summary>
	public const int CdcConfirmedPosition = 102306;

	/// <summary>CDC processor stopping.</summary>
	public const int CdcProcessorStopping = 102307;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 102308;

	// ========================================
	// 107000-107099: Audit Store
	// ========================================

	/// <summary>Audit event stored.</summary>
	public const int AuditEventStored = 107000;

	/// <summary>Audit event retrieved.</summary>
	public const int AuditEventRetrieved = 107001;

	/// <summary>Audit query executed.</summary>
	public const int AuditQueryExecuted = 107002;

	/// <summary>Audit integrity verified.</summary>
	public const int AuditIntegrityVerified = 107003;

	/// <summary>Audit store initialized.</summary>
	public const int AuditStoreInitialized = 107004;

	/// <summary>Audit store error.</summary>
	public const int AuditStoreError = 107005;

	// ========================================
	// 107100-107199: Leader Election
	// ========================================

	/// <summary>Leader election started.</summary>
	public const int LeaderElectionStarted = 107100;

	/// <summary>Leader election stopped.</summary>
	public const int LeaderElectionStopped = 107101;

	/// <summary>Lock acquisition failed.</summary>
	public const int LockAcquisitionFailed = 107102;

	/// <summary>Lock acquisition error.</summary>
	public const int LockAcquisitionError = 107103;

	/// <summary>Lock released.</summary>
	public const int LockReleased = 107104;

	/// <summary>Lock release error.</summary>
	public const int LockReleaseError = 107105;

	/// <summary>Leader election renewal error.</summary>
	public const int LeaderElectionError = 107106;

	/// <summary>Became leader.</summary>
	public const int BecameLeader = 107107;

	/// <summary>Lost leadership.</summary>
	public const int LostLeadership = 107108;

	/// <summary>Leader election dispose error.</summary>
	public const int LeaderElectionDisposeError = 107109;
}
