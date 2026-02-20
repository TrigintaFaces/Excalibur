// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.SqlServer.Diagnostics;

/// <summary>
/// Event IDs for SQL Server data access (100000-100999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>100000-100099: Connection Management</item>
/// <item>100100-100199: Query Execution</item>
/// <item>100200-100299: Transaction Management</item>
/// <item>100300-100399: Stored Procedures</item>
/// <item>100400-100499: Bulk Operations</item>
/// <item>100500-100599: Performance</item>
/// <item>100600-100699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataSqlServerEventId
{
	// ========================================
	// 100000-100099: Connection Management
	// ========================================

	/// <summary>SQL Server connection opened.</summary>
	public const int ConnectionOpened = 100000;

	/// <summary>SQL Server connection closed.</summary>
	public const int ConnectionClosed = 100001;

	/// <summary>SQL Server connection pool created.</summary>
	public const int ConnectionPoolCreated = 100002;

	/// <summary>Connection acquired from pool.</summary>
	public const int ConnectionAcquired = 100003;

	/// <summary>Connection returned to pool.</summary>
	public const int ConnectionReturned = 100004;

	/// <summary>Connection failed.</summary>
	public const int ConnectionFailed = 100005;

	/// <summary>Connection retry attempted.</summary>
	public const int ConnectionRetry = 100006;

	// ========================================
	// 100100-100199: Query Execution
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 100100;

	/// <summary>Query executed successfully.</summary>
	public const int QueryExecuted = 100101;

	/// <summary>Query failed.</summary>
	public const int QueryFailed = 100102;

	/// <summary>Query timed out.</summary>
	public const int QueryTimeout = 100103;

	/// <summary>Query plan cached.</summary>
	public const int QueryPlanCached = 100104;

	/// <summary>Parameterized query executed.</summary>
	public const int ParameterizedQueryExecuted = 100105;

	// ========================================
	// 100200-100299: Transaction Management
	// ========================================

	/// <summary>Transaction started.</summary>
	public const int TransactionStarted = 100200;

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 100201;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 100202;

	/// <summary>Savepoint created.</summary>
	public const int SavepointCreated = 100203;

	/// <summary>Savepoint rolled back.</summary>
	public const int SavepointRolledBack = 100204;

	/// <summary>Distributed transaction enlisted.</summary>
	public const int DistributedTransactionEnlisted = 100205;

	// ========================================
	// 100300-100399: Stored Procedures
	// ========================================

	/// <summary>Stored procedure executing.</summary>
	public const int StoredProcedureExecuting = 100300;

	/// <summary>Stored procedure executed.</summary>
	public const int StoredProcedureExecuted = 100301;

	/// <summary>Stored procedure failed.</summary>
	public const int StoredProcedureFailed = 100302;

	/// <summary>Output parameters retrieved.</summary>
	public const int OutputParametersRetrieved = 100303;

	// ========================================
	// 100400-100499: Bulk Operations
	// ========================================

	/// <summary>Bulk insert started.</summary>
	public const int BulkInsertStarted = 100400;

	/// <summary>Bulk insert completed.</summary>
	public const int BulkInsertCompleted = 100401;

	/// <summary>Bulk update started.</summary>
	public const int BulkUpdateStarted = 100402;

	/// <summary>Bulk update completed.</summary>
	public const int BulkUpdateCompleted = 100403;

	/// <summary>Bulk operation failed.</summary>
	public const int BulkOperationFailed = 100404;

	// ========================================
	// 100500-100599: Performance
	// ========================================

	/// <summary>Slow query detected.</summary>
	public const int SlowQueryDetected = 100500;

	/// <summary>Query statistics collected.</summary>
	public const int QueryStatisticsCollected = 100501;

	/// <summary>Index hint applied.</summary>
	public const int IndexHintApplied = 100502;

	/// <summary>Execution plan retrieved.</summary>
	public const int ExecutionPlanRetrieved = 100503;

	// ========================================
	// 100600-100699: Error Handling
	// ========================================

	/// <summary>Deadlock detected.</summary>
	public const int DeadlockDetected = 100600;

	/// <summary>Concurrency exception occurred.</summary>
	public const int ConcurrencyException = 100601;

	/// <summary>Constraint violation detected.</summary>
	public const int ConstraintViolation = 100602;

	/// <summary>SQL error occurred.</summary>
	public const int SqlError = 100603;

	/// <summary>Data truncation warning.</summary>
	public const int DataTruncationWarning = 100604;

	// ========================================
	// 100700-100749: CDC Processor
	// ========================================

	/// <summary>CDC run starting.</summary>
	public const int CdcRunStarting = 100700;

	/// <summary>CDC run completed.</summary>
	public const int CdcRunCompleted = 100701;

	/// <summary>CDC run skipped no changes.</summary>
	public const int CdcRunSkippedNoChanges = 100702;

	/// <summary>CDC run error occurred.</summary>
	public const int CdcRunError = 100703;

	/// <summary>CDC changes retrieved.</summary>
	public const int CdcChangesRetrieved = 100704;

	/// <summary>CDC change processed.</summary>
	public const int CdcChangeProcessed = 100705;

	/// <summary>CDC change processing error.</summary>
	public const int CdcChangeProcessingError = 100706;

	/// <summary>CDC batch completed.</summary>
	public const int CdcBatchCompleted = 100707;

	/// <summary>CDC batch error.</summary>
	public const int CdcBatchError = 100708;

	/// <summary>CDC processor starting.</summary>
	public const int CdcProcessorStarting = 100709;

	/// <summary>CDC processor stopped.</summary>
	public const int CdcProcessorStopped = 100710;

	/// <summary>CDC processor error.</summary>
	public const int CdcProcessorError = 100711;

	/// <summary>CDC table registered.</summary>
	public const int CdcTableRegistered = 100712;

	/// <summary>CDC table unregistered.</summary>
	public const int CdcTableUnregistered = 100713;

	/// <summary>CDC LSN updated.</summary>
	public const int CdcLsnUpdated = 100714;

	/// <summary>CDC LSN retrieved.</summary>
	public const int CdcLsnRetrieved = 100715;

	/// <summary>CDC LSN error.</summary>
	public const int CdcLsnError = 100716;

	/// <summary>CDC enabled on table.</summary>
	public const int CdcEnabledOnTable = 100717;

	/// <summary>CDC disabled on table.</summary>
	public const int CdcDisabledOnTable = 100718;

	/// <summary>CDC validation started.</summary>
	public const int CdcValidationStarted = 100719;

	/// <summary>CDC validation completed.</summary>
	public const int CdcValidationCompleted = 100720;

	/// <summary>CDC validation error.</summary>
	public const int CdcValidationError = 100721;

	/// <summary>CDC cleanup started.</summary>
	public const int CdcCleanupStarted = 100722;

	/// <summary>CDC cleanup completed.</summary>
	public const int CdcCleanupCompleted = 100723;

	/// <summary>CDC cleanup error.</summary>
	public const int CdcCleanupError = 100724;

	/// <summary>CDC handler invoked.</summary>
	public const int CdcHandlerInvoked = 100725;

	/// <summary>CDC handler error.</summary>
	public const int CdcHandlerError = 100726;

	/// <summary>CDC partition processed.</summary>
	public const int CdcPartitionProcessed = 100727;

	/// <summary>CDC partition error.</summary>
	public const int CdcPartitionError = 100728;

	/// <summary>CDC checkpoint created.</summary>
	public const int CdcCheckpointCreated = 100729;

	/// <summary>CDC checkpoint restored.</summary>
	public const int CdcCheckpointRestored = 100730;

	/// <summary>CDC checkpoint error.</summary>
	public const int CdcCheckpointError = 100731;

	/// <summary>CDC retry attempted.</summary>
	public const int CdcRetryAttempted = 100732;

	/// <summary>CDC max retries exceeded.</summary>
	public const int CdcMaxRetriesExceeded = 100733;

	/// <summary>CDC configuration loaded.</summary>
	public const int CdcConfigurationLoaded = 100734;

	/// <summary>CDC configuration error.</summary>
	public const int CdcConfigurationError = 100735;

	/// <summary>CDC connection established.</summary>
	public const int CdcConnectionEstablished = 100736;

	/// <summary>CDC connection error.</summary>
	public const int CdcConnectionError = 100737;

	/// <summary>CDC polling started.</summary>
	public const int CdcPollingStarted = 100738;

	/// <summary>CDC polling stopped.</summary>
	public const int CdcPollingStopped = 100739;

	/// <summary>CDC polling error.</summary>
	public const int CdcPollingError = 100740;

	/// <summary>CDC schema change detected.</summary>
	public const int CdcSchemaChangeDetected = 100741;

	// ========================================
	// 100750-100759: Data Change Event Processor
	// ========================================

	/// <summary>Data change event processed.</summary>
	public const int DataChangeEventProcessed = 100750;

	/// <summary>Data change missing table handler.</summary>
	public const int DataChangeMissingTableHandler = 100751;

	/// <summary>Data change event error.</summary>
	public const int DataChangeEventError = 100752;

	// ========================================
	// 100760-100779: Transaction Scope (Root)
	// ========================================

	/// <summary>Provider enlisted in transaction.</summary>
	public const int ProviderEnlistedInTransaction = 100760;

	/// <summary>Multiple connections warning.</summary>
	public const int MultipleConnectionsWarning = 100761;

	/// <summary>Connection enlisted in transaction.</summary>
	public const int ConnectionEnlistedInTransaction = 100762;

	/// <summary>Committing transaction.</summary>
	public const int CommittingTransaction = 100763;

	/// <summary>Transaction committed successfully.</summary>
	public const int TransactionCommittedSuccessfully = 100764;

	/// <summary>Commit callback error.</summary>
	public const int CommitCallbackError = 100765;

	/// <summary>Commit failed.</summary>
	public const int CommitFailed = 100766;

	/// <summary>Rolling back transaction.</summary>
	public const int RollingBackTransaction = 100767;

	/// <summary>Transaction rolled back successfully.</summary>
	public const int TransactionRolledBackSuccessfully = 100768;

	/// <summary>Rollback callback error.</summary>
	public const int RollbackCallbackError = 100769;

	/// <summary>Rollback failed.</summary>
	public const int RollbackFailed = 100770;

	/// <summary>Savepoint created in transaction.</summary>
	public const int SavepointCreatedInTransaction = 100771;

	/// <summary>Rolled back to savepoint.</summary>
	public const int RolledBackToSavepoint = 100772;

	/// <summary>Release savepoint requested.</summary>
	public const int ReleaseSavepointRequested = 100773;

	/// <summary>Dispose error.</summary>
	public const int DisposeError = 100774;

	/// <summary>Complete callback error.</summary>
	public const int CompleteCallbackError = 100775;

	// ========================================
	// 100780-100809: Persistence Transaction Scope
	// ========================================

	/// <summary>Persistence transaction created.</summary>
	public const int PersistenceTransactionCreated = 100780;

	/// <summary>Persistence provider enlisted.</summary>
	public const int PersistenceProviderEnlisted = 100781;

	/// <summary>Persistence multiple connections warning.</summary>
	public const int PersistenceMultipleConnectionsWarning = 100782;

	/// <summary>Persistence connection enlisted.</summary>
	public const int PersistenceConnectionEnlisted = 100783;

	/// <summary>Persistence committing transaction.</summary>
	public const int PersistenceCommittingTransaction = 100784;

	/// <summary>Persistence transaction committed.</summary>
	public const int PersistenceTransactionCommitted = 100785;

	/// <summary>Persistence commit callback error.</summary>
	public const int PersistenceCommitCallbackError = 100786;

	/// <summary>Persistence commit failed.</summary>
	public const int PersistenceCommitFailed = 100787;

	/// <summary>Persistence rolling back transaction.</summary>
	public const int PersistenceRollingBackTransaction = 100788;

	/// <summary>Persistence transaction rolled back.</summary>
	public const int PersistenceTransactionRolledBack = 100789;

	/// <summary>Persistence rollback callback error.</summary>
	public const int PersistenceRollbackCallbackError = 100790;

	/// <summary>Persistence rollback failed.</summary>
	public const int PersistenceRollbackFailed = 100791;

	/// <summary>Persistence savepoint created.</summary>
	public const int PersistenceSavepointCreated = 100792;

	/// <summary>Persistence rolled back to savepoint.</summary>
	public const int PersistenceRolledBackToSavepoint = 100793;

	/// <summary>Persistence release savepoint.</summary>
	public const int PersistenceReleaseSavepoint = 100794;

	/// <summary>Persistence dispose error.</summary>
	public const int PersistenceDisposeError = 100795;

	/// <summary>Persistence complete callback error.</summary>
	public const int PersistenceCompleteCallbackError = 100796;

	/// <summary>Persistence enlisted connection state.</summary>
	public const int PersistenceEnlistedConnectionState = 100797;

	/// <summary>Persistence transaction not active.</summary>
	public const int PersistenceTransactionNotActive = 100798;

	/// <summary>Persistence connection opening.</summary>
	public const int PersistenceConnectionOpening = 100799;

	/// <summary>Persistence transaction beginning.</summary>
	public const int PersistenceTransactionBeginning = 100800;

	/// <summary>Persistence connection enlisted with transaction.</summary>
	public const int PersistenceConnectionEnlistedWithTransaction = 100801;

	/// <summary>Persistence provider already enlisted.</summary>
	public const int PersistenceProviderAlreadyEnlisted = 100802;

	/// <summary>Persistence connection already enlisted.</summary>
	public const int PersistenceConnectionAlreadyEnlisted = 100803;

	/// <summary>Persistence cannot rollback.</summary>
	public const int PersistenceCannotRollback = 100804;

	/// <summary>Persistence rollback transaction error.</summary>
	public const int PersistenceRollbackTransactionError = 100805;

	/// <summary>Persistence savepoint not found.</summary>
	public const int PersistenceSavepointNotFound = 100806;

	/// <summary>Persistence savepoint released.</summary>
	public const int PersistenceSavepointReleased = 100807;

	/// <summary>Persistence nested scope created.</summary>
	public const int PersistenceNestedScopeCreated = 100808;

	/// <summary>Persistence automatic rollback error.</summary>
	public const int PersistenceAutomaticRollbackError = 100809;

	// ========================================
	// 100810-100829: Persistence Provider
	// ========================================

	/// <summary>Persistence provider initialized.</summary>
	public const int PersistenceProviderInitialized = 100810;

	/// <summary>Persistence connection created.</summary>
	public const int PersistenceConnectionCreated = 100811;

	/// <summary>Persistence connection opened.</summary>
	public const int PersistenceConnectionOpened = 100812;

	/// <summary>Persistence data request executed.</summary>
	public const int PersistenceDataRequestExecuted = 100813;

	/// <summary>Persistence data request error.</summary>
	public const int PersistenceDataRequestError = 100814;

	/// <summary>Persistence transaction scope created.</summary>
	public const int PersistenceTransactionScopeCreated = 100815;

	/// <summary>Persistence batch executed.</summary>
	public const int PersistenceBatchExecuted = 100816;

	/// <summary>Persistence batch error.</summary>
	public const int PersistenceBatchError = 100817;

	/// <summary>Persistence batch in transaction executed.</summary>
	public const int PersistenceBatchInTransactionExecuted = 100818;

	/// <summary>Persistence batch in transaction error.</summary>
	public const int PersistenceBatchInTransactionError = 100819;

	/// <summary>Persistence unsafe SQL pattern.</summary>
	public const int PersistenceUnsafeSqlPattern = 100820;

	/// <summary>Persistence validation error.</summary>
	public const int PersistenceValidationError = 100821;

	/// <summary>Persistence provider already initialized.</summary>
	public const int PersistenceProviderAlreadyInitialized = 100822;

	/// <summary>Persistence provider initialized success.</summary>
	public const int PersistenceProviderInitializedSuccess = 100823;

	/// <summary>Persistence transient retry.</summary>
	public const int PersistenceTransientRetry = 100824;

	/// <summary>Persistence retry attempt.</summary>
	public const int PersistenceRetryAttempt = 100825;

	/// <summary>Persistence failed to retrieve metrics.</summary>
	public const int PersistenceFailedToRetrieveMetrics = 100826;

	/// <summary>Persistence initializing provider.</summary>
	public const int PersistenceInitializingProvider = 100827;

	/// <summary>Persistence connection test failed.</summary>
	public const int PersistenceConnectionTestFailed = 100828;

	// ========================================
	// 100830-100839: Persistence Health Check
	// ========================================

	/// <summary>Persistence health check failed.</summary>
	public const int PersistenceHealthCheckFailed = 100830;

	/// <summary>Persistence connection pool stats error.</summary>
	public const int PersistenceConnectionPoolStatsError = 100831;

	/// <summary>Persistence health check succeeded.</summary>
	public const int PersistenceHealthCheckSucceeded = 100832;

	/// <summary>Persistence health check timeout.</summary>
	public const int PersistenceHealthCheckTimeout = 100833;

	/// <summary>Persistence health check started.</summary>
	public const int PersistenceHealthCheckStarted = 100834;

	/// <summary>Persistence health check completed.</summary>
	public const int PersistenceHealthCheckCompleted = 100835;

	/// <summary>Persistence health check degraded.</summary>
	public const int PersistenceHealthCheckDegraded = 100836;

	/// <summary>Persistence health check unhealthy.</summary>
	public const int PersistenceHealthCheckUnhealthy = 100837;

	/// <summary>Persistence health check unexpected error.</summary>
	public const int PersistenceHealthCheckUnexpectedError = 100838;

	/// <summary>Persistence health check blocking queries error.</summary>
	public const int PersistenceHealthCheckBlockingQueriesError = 100839;

	// ========================================
	// 100840-100849: Health Check Additional
	// ========================================

	/// <summary>Persistence health check deadlocks error.</summary>
	public const int PersistenceHealthCheckDeadlocksError = 100840;

	/// <summary>Persistence health check query performance error.</summary>
	public const int PersistenceHealthCheckQueryPerformanceError = 100841;

	/// <summary>Persistence health check database size error.</summary>
	public const int PersistenceHealthCheckDatabaseSizeError = 100842;

	/// <summary>Persistence health check CDC status error.</summary>
	public const int PersistenceHealthCheckCdcStatusError = 100843;

	// ========================================
	// 100850-100867: Root Persistence Provider
	// ========================================

	/// <summary>Root retry attempt.</summary>
	public const int RootRetryAttempt = 100850;

	/// <summary>Root executing data request.</summary>
	public const int RootExecutingDataRequest = 100851;

	/// <summary>Root executing in transaction.</summary>
	public const int RootExecutingInTransaction = 100852;

	/// <summary>Root connection test successful.</summary>
	public const int RootConnectionTestSuccessful = 100853;

	/// <summary>Root connection test failed.</summary>
	public const int RootConnectionTestFailed = 100854;

	/// <summary>Root executing batch in transaction.</summary>
	public const int RootExecutingBatchInTransaction = 100855;

	/// <summary>Root disposing provider.</summary>
	public const int RootDisposingProvider = 100856;

	/// <summary>Root executing batch.</summary>
	public const int RootExecutingBatch = 100857;

	/// <summary>Root executing bulk data request.</summary>
	public const int RootExecutingBulkDataRequest = 100858;

	/// <summary>Root executing stored procedure.</summary>
	public const int RootExecutingStoredProcedure = 100859;

	/// <summary>Root failed to retrieve database statistics.</summary>
	public const int RootFailedToRetrieveDatabaseStatistics = 100860;

	/// <summary>Root failed to retrieve schema info.</summary>
	public const int RootFailedToRetrieveSchemaInfo = 100861;

	/// <summary>Root failed to retrieve connection pool statistics.</summary>
	public const int RootFailedToRetrieveConnectionPoolStatistics = 100862;

	/// <summary>Root cleared connection pools.</summary>
	public const int RootClearedConnectionPools = 100863;

	/// <summary>Root failed to clear connection pools.</summary>
	public const int RootFailedToClearConnectionPools = 100864;

	/// <summary>Root failed to execute in transaction.</summary>
	public const int RootFailedToExecuteInTransaction = 100865;

	/// <summary>Root failed to retrieve metrics.</summary>
	public const int RootFailedToRetrieveMetrics = 100866;

	/// <summary>Root initializing provider.</summary>
	public const int RootInitializingProvider = 100867;

	// ========================================
	// 100870-100879: Connection Pooling
	// ========================================

	/// <summary>Connection factory create failed.</summary>
	public const int ConnectionFactoryCreateFailed = 100870;

	/// <summary>Connection validation failed trace.</summary>
	public const int ConnectionValidationFailedTrace = 100871;

	/// <summary>Connection validation error.</summary>
	public const int ConnectionValidationError = 100872;

	/// <summary>Connection factory retry.</summary>
	public const int ConnectionFactoryRetry = 100873;

	/// <summary>Connection factory disposed.</summary>
	public const int ConnectionFactoryDisposed = 100874;

	/// <summary>Health checker succeeded.</summary>
	public const int HealthCheckerSucceeded = 100875;

	/// <summary>Health checker failed.</summary>
	public const int HealthCheckerFailed = 100876;

	/// <summary>Health checker error.</summary>
	public const int HealthCheckerError = 100877;

	/// <summary>Connection factory created connection.</summary>
	public const int ConnectionFactoryCreatedConnection = 100878;

	/// <summary>Connection factory repair failed.</summary>
	public const int ConnectionFactoryRepairFailed = 100879;

	// ========================================
	// 100880-100889: Error Handling / Dead Letter
	// ========================================

	/// <summary>Dead letter message stored.</summary>
	public const int DeadLetterMessageStored = 100880;

	/// <summary>Dead letter store error.</summary>
	public const int DeadLetterStoreError = 100881;

	/// <summary>Dead letter retrieval error.</summary>
	public const int DeadLetterRetrievalError = 100882;

	/// <summary>Dead letter cleanup completed.</summary>
	public const int DeadLetterCleanupCompleted = 100883;

	// ========================================
	// 100890-100899: Data Access Policy
	// ========================================

	/// <summary>Circuit breaker opened.</summary>
	public const int CircuitBreakerOpened = 100890;

	/// <summary>Circuit breaker reset.</summary>
	public const int CircuitBreakerReset = 100891;

	/// <summary>SQL operation retry.</summary>
	public const int SqlOperationRetry = 100892;

	// ========================================
	// 100900-100909: Retry Policy
	// ========================================

	/// <summary>SQL Server operation retry.</summary>
	public const int SqlServerOperationRetry = 100900;
}
