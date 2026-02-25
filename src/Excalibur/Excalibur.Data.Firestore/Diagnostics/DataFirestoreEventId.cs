// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Firestore.Diagnostics;

/// <summary>
/// Event IDs for Google Cloud Firestore data access (105000-105999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>105000-105099: Client Management</item>
/// <item>105100-105199: Collection Operations</item>
/// <item>105200-105299: Document Operations</item>
/// <item>105300-105399: Query Execution</item>
/// <item>105400-105499: Real-Time Listeners</item>
/// <item>105500-105599: Transactions</item>
/// <item>105600-105699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataFirestoreEventId
{
	// ========================================
	// 105000-105099: Client Management
	// ========================================

	/// <summary>Firestore client created.</summary>
	public const int ClientCreated = 105000;

	/// <summary>Firestore client disposed.</summary>
	public const int ClientDisposed = 105001;

	/// <summary>Project ID configured.</summary>
	public const int ProjectIdConfigured = 105002;

	/// <summary>Emulator configured.</summary>
	public const int EmulatorConfigured = 105003;

	/// <summary>Credentials configured.</summary>
	public const int CredentialsConfigured = 105004;

	// ========================================
	// 105100-105199: Collection Operations
	// ========================================

	/// <summary>Collection reference obtained.</summary>
	public const int CollectionReferenceObtained = 105100;

	/// <summary>Collection group queried.</summary>
	public const int CollectionGroupQueried = 105101;

	/// <summary>Subcollection accessed.</summary>
	public const int SubcollectionAccessed = 105102;

	/// <summary>Collection documents listed.</summary>
	public const int CollectionDocumentsListed = 105103;

	// ========================================
	// 105200-105299: Document Operations
	// ========================================

	/// <summary>Document created.</summary>
	public const int DocumentCreated = 105200;

	/// <summary>Document read.</summary>
	public const int DocumentRead = 105201;

	/// <summary>Document set.</summary>
	public const int DocumentSet = 105202;

	/// <summary>Document updated.</summary>
	public const int DocumentUpdated = 105203;

	/// <summary>Document deleted.</summary>
	public const int DocumentDeleted = 105204;

	/// <summary>Batch write executed.</summary>
	public const int BatchWriteExecuted = 105205;

	/// <summary>Document reference obtained.</summary>
	public const int DocumentReferenceObtained = 105206;

	// ========================================
	// 105300-105399: Query Execution
	// ========================================

	/// <summary>Query executing.</summary>
	public const int QueryExecuting = 105300;

	/// <summary>Query executed.</summary>
	public const int QueryExecuted = 105301;

	/// <summary>Query filter applied.</summary>
	public const int QueryFilterApplied = 105302;

	/// <summary>Query cursor used.</summary>
	public const int QueryCursorUsed = 105303;

	/// <summary>Composite index required.</summary>
	public const int CompositeIndexRequired = 105304;

	// ========================================
	// 105400-105499: Real-Time Listeners
	// ========================================

	/// <summary>Snapshot listener added.</summary>
	public const int SnapshotListenerAdded = 105400;

	/// <summary>Snapshot listener removed.</summary>
	public const int SnapshotListenerRemoved = 105401;

	/// <summary>Snapshot received.</summary>
	public const int SnapshotReceived = 105402;

	/// <summary>Document snapshot changed.</summary>
	public const int DocumentSnapshotChanged = 105403;

	/// <summary>Query snapshot received.</summary>
	public const int QuerySnapshotReceived = 105404;

	// ========================================
	// 105500-105599: Transactions
	// ========================================

	/// <summary>Transaction started.</summary>
	public const int TransactionStarted = 105500;

	/// <summary>Transaction committed.</summary>
	public const int TransactionCommitted = 105501;

	/// <summary>Transaction rolled back.</summary>
	public const int TransactionRolledBack = 105502;

	/// <summary>Transaction retried.</summary>
	public const int TransactionRetried = 105503;

	/// <summary>Transaction max attempts exceeded.</summary>
	public const int TransactionMaxAttemptsExceeded = 105504;

	// ========================================
	// 105600-105699: Error Handling
	// ========================================

	/// <summary>Document not found.</summary>
	public const int DocumentNotFound = 105600;

	/// <summary>Permission denied.</summary>
	public const int PermissionDenied = 105601;

	/// <summary>Quota exceeded.</summary>
	public const int QuotaExceeded = 105602;

	/// <summary>Aborted error.</summary>
	public const int AbortedError = 105603;

	/// <summary>Firestore exception occurred.</summary>
	public const int FirestoreException = 105604;

	// ========================================
	// 105700-105799: Persistence Provider
	// ========================================

	/// <summary>Initializing Firestore provider.</summary>
	public const int ProviderInitializing = 105700;

	/// <summary>Disposing Firestore provider.</summary>
	public const int ProviderDisposing = 105701;

	/// <summary>Operation completed.</summary>
	public const int OperationCompleted = 105702;

	/// <summary>Operation failed.</summary>
	public const int OperationFailed = 105703;

	// ========================================
	// 105800-105899: Health Check
	// ========================================

	/// <summary>Health check started.</summary>
	public const int HealthCheckStarted = 105800;

	/// <summary>Health check completed.</summary>
	public const int HealthCheckCompleted = 105801;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 105802;

	// ========================================
	// 105900-105999: CDC Operations
	// ========================================

	/// <summary>CDC processor starting.</summary>
	public const int CdcStarting = 105900;

	/// <summary>CDC processor stopping.</summary>
	public const int CdcStopping = 105901;

	/// <summary>CDC received changes.</summary>
	public const int CdcReceivedChanges = 105902;

	/// <summary>CDC processing change.</summary>
	public const int CdcProcessingChange = 105903;

	/// <summary>CDC confirming position.</summary>
	public const int CdcConfirmingPosition = 105904;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 105905;

	/// <summary>CDC resuming from position.</summary>
	public const int CdcResumingFromPosition = 105906;

	/// <summary>CDC starting from beginning.</summary>
	public const int CdcStartingFromBeginning = 105907;

	/// <summary>CDC saving position.</summary>
	public const int CdcSavingPosition = 105908;

	/// <summary>CDC getting position.</summary>
	public const int CdcGettingPosition = 105909;

	/// <summary>CDC deleting position.</summary>
	public const int CdcDeletingPosition = 105910;

	/// <summary>CDC position not found.</summary>
	public const int CdcPositionNotFound = 105911;

	/// <summary>Listener subscription starting.</summary>
	public const int ListenerStarting = 105912;

	/// <summary>Listener subscription stopping.</summary>
	public const int ListenerStopping = 105913;

	/// <summary>Listener received changes.</summary>
	public const int ListenerReceivedChanges = 105914;

	/// <summary>CDC event dropped (channel full).</summary>
	public const int CdcEventDropped = 105915;

	// ========================================
	// 106000-106099: Snapshot Store
	// ========================================

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 106000;

	/// <summary>Snapshot version skipped (older version).</summary>
	public const int SnapshotVersionSkipped = 106001;

	/// <summary>Snapshot retrieved.</summary>
	public const int SnapshotRetrieved = 106002;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 106003;

	/// <summary>Snapshots deleted older than version.</summary>
	public const int SnapshotsDeletedOlderThan = 106004;

	// ========================================
	// 106100-106199: Grant Service
	// ========================================

	/// <summary>Grant service initialized.</summary>
	public const int GrantServiceInitialized = 106100;

	/// <summary>Grant saved.</summary>
	public const int GrantSaved = 106101;

	/// <summary>Grant deleted.</summary>
	public const int GrantDeleted = 106102;

	/// <summary>Grant revoked.</summary>
	public const int GrantRevoked = 106103;

	// ========================================
	// 106200-106299: Outbox Store
	// ========================================

	/// <summary>Outbox message staged.</summary>
	public const int OutboxMessageStaged = 106200;

	/// <summary>Outbox message enqueued.</summary>
	public const int OutboxMessageEnqueued = 106201;

	/// <summary>Outbox message sent.</summary>
	public const int OutboxMessageSent = 106202;

	/// <summary>Outbox message failed.</summary>
	public const int OutboxMessageFailed = 106203;

	/// <summary>Outbox cleaned up.</summary>
	public const int OutboxCleanedUp = 106204;

	// ========================================
	// 106300-106399: Activity Group Service
	// ========================================

	/// <summary>Activity group service initialized.</summary>
	public const int ActivityGroupServiceInitialized = 106300;

	/// <summary>Activity group grant inserted.</summary>
	public const int ActivityGroupGrantInserted = 106301;

	/// <summary>Activity group grants deleted by user.</summary>
	public const int ActivityGroupGrantsDeletedByUser = 106302;

	/// <summary>All activity group grants deleted.</summary>
	public const int ActivityGroupAllGrantsDeleted = 106303;

	// ========================================
	// 106400-106499: Inbox Store
	// ========================================

	/// <summary>Inbox entry created.</summary>
	public const int InboxEntryCreated = 106400;

	/// <summary>Inbox entry processed.</summary>
	public const int InboxEntryProcessed = 106401;

	/// <summary>TryMarkAsProcessed succeeded.</summary>
	public const int InboxTryMarkProcessedSuccess = 106402;

	/// <summary>TryMarkAsProcessed detected duplicate.</summary>
	public const int InboxTryMarkProcessedDuplicate = 106403;

	/// <summary>Inbox entry failed.</summary>
	public const int InboxEntryFailed = 106404;

	/// <summary>Inbox entries cleaned up.</summary>
	public const int InboxCleanedUp = 106405;

	// ========================================
	// 106500-106599: Saga Store
	// ========================================

	/// <summary>Saga state loaded.</summary>
	public const int SagaLoaded = 106500;

	/// <summary>Saga state saved.</summary>
	public const int SagaSaved = 106501;
}
