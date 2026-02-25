// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.MongoDB.Diagnostics;

/// <summary>
/// Event IDs for MongoDB data access (104000-104999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>104000-104099: Client Management</item>
/// <item>104100-104199: Collection Operations</item>
/// <item>104200-104299: Document Operations</item>
/// <item>104300-104399: Query/Aggregation</item>
/// <item>104400-104499: Change Streams</item>
/// <item>104500-104599: Performance</item>
/// <item>104600-104699: Error Handling</item>
/// </list>
/// </remarks>
public static class DataMongoDbEventId
{
	// ========================================
	// 104000-104099: Client Management
	// ========================================

	/// <summary>MongoDB client created.</summary>
	public const int ClientCreated = 104000;

	/// <summary>MongoDB client disposed.</summary>
	public const int ClientDisposed = 104001;

	/// <summary>Connection string configured.</summary>
	public const int ConnectionStringConfigured = 104002;

	/// <summary>Read preference configured.</summary>
	public const int ReadPreferenceConfigured = 104003;

	/// <summary>Write concern configured.</summary>
	public const int WriteConcernConfigured = 104004;

	/// <summary>Connection pool configured.</summary>
	public const int ConnectionPoolConfigured = 104005;

	// ========================================
	// 104100-104199: Collection Operations
	// ========================================

	/// <summary>Collection created.</summary>
	public const int CollectionCreated = 104100;

	/// <summary>Collection accessed.</summary>
	public const int CollectionAccessed = 104101;

	/// <summary>Collection dropped.</summary>
	public const int CollectionDropped = 104102;

	/// <summary>Index created.</summary>
	public const int IndexCreated = 104103;

	/// <summary>Index dropped.</summary>
	public const int IndexDropped = 104104;

	// ========================================
	// 104200-104299: Document Operations
	// ========================================

	/// <summary>Document inserted.</summary>
	public const int DocumentInserted = 104200;

	/// <summary>Document found.</summary>
	public const int DocumentFound = 104201;

	/// <summary>Document updated.</summary>
	public const int DocumentUpdated = 104202;

	/// <summary>Document replaced.</summary>
	public const int DocumentReplaced = 104203;

	/// <summary>Document deleted.</summary>
	public const int DocumentDeleted = 104204;

	/// <summary>Bulk write executed.</summary>
	public const int BulkWriteExecuted = 104205;

	/// <summary>Documents inserted many.</summary>
	public const int DocumentsInsertedMany = 104206;

	// ========================================
	// 104300-104399: Query/Aggregation
	// ========================================

	/// <summary>Find query executing.</summary>
	public const int FindQueryExecuting = 104300;

	/// <summary>Find query executed.</summary>
	public const int FindQueryExecuted = 104301;

	/// <summary>Aggregation pipeline executing.</summary>
	public const int AggregationExecuting = 104302;

	/// <summary>Aggregation pipeline executed.</summary>
	public const int AggregationExecuted = 104303;

	/// <summary>Count query executed.</summary>
	public const int CountQueryExecuted = 104304;

	/// <summary>Distinct query executed.</summary>
	public const int DistinctQueryExecuted = 104305;

	// ========================================
	// 104400-104499: Change Streams
	// ========================================

	/// <summary>Change stream started.</summary>
	public const int ChangeStreamStarted = 104400;

	/// <summary>Change stream stopped.</summary>
	public const int ChangeStreamStopped = 104401;

	/// <summary>Change event received.</summary>
	public const int ChangeEventReceived = 104402;

	/// <summary>Resume token stored.</summary>
	public const int ResumeTokenStored = 104403;

	/// <summary>Change stream error.</summary>
	public const int ChangeStreamError = 104404;

	// ========================================
	// 104500-104599: Performance
	// ========================================

	/// <summary>Slow operation detected.</summary>
	public const int SlowOperationDetected = 104500;

	/// <summary>Explain plan retrieved.</summary>
	public const int ExplainPlanRetrieved = 104501;

	/// <summary>Server selection completed.</summary>
	public const int ServerSelectionCompleted = 104502;

	/// <summary>Connection checked out.</summary>
	public const int ConnectionCheckedOut = 104503;

	// ========================================
	// 104600-104699: Error Handling
	// ========================================

	/// <summary>Duplicate key error.</summary>
	public const int DuplicateKeyError = 104600;

	/// <summary>Write concern error.</summary>
	public const int WriteConcernError = 104601;

	/// <summary>Document not found.</summary>
	public const int DocumentNotFound = 104602;

	/// <summary>MongoDB exception occurred.</summary>
	public const int MongoDbException = 104603;

	/// <summary>Timeout exception.</summary>
	public const int TimeoutException = 104604;

	// ========================================
	// 104700-104799: Retry Policy
	// ========================================

	/// <summary>MongoDB operation retry.</summary>
	public const int MongoOperationRetry = 104700;

	/// <summary>MongoDB document operation retry.</summary>
	public const int MongoDocumentOperationRetry = 104701;

	// ========================================
	// 104800-104899: Provider Operations
	// ========================================

	/// <summary>Executing data request.</summary>
	public const int ExecutingDataRequest = 104800;

	/// <summary>Failed to execute data request.</summary>
	public const int FailedToExecuteDataRequest = 104801;

	/// <summary>Executing document data request in transaction.</summary>
	public const int ExecutingDocumentDataRequestInTransaction = 104802;

	/// <summary>Successfully executed document data request in transaction.</summary>
	public const int SuccessfullyExecutedDocumentDataRequestInTransaction = 104803;

	/// <summary>Failed to execute document data request in transaction.</summary>
	public const int FailedToExecuteDocumentDataRequestInTransaction = 104804;

	/// <summary>Executing data request in transaction.</summary>
	public const int ExecutingDataRequestInTransaction = 104805;

	/// <summary>Failed to execute data request in transaction.</summary>
	public const int FailedToExecuteDataRequestInTransaction = 104806;

	/// <summary>Connection test successful.</summary>
	public const int ConnectionTestSuccessful = 104807;

	/// <summary>Connection test failed.</summary>
	public const int ConnectionTestFailed = 104808;

	/// <summary>Failed to retrieve server metadata.</summary>
	public const int FailedToRetrieveServerMetadata = 104809;

	/// <summary>Initializing provider.</summary>
	public const int InitializingProvider = 104810;

	/// <summary>Failed to retrieve connection pool stats.</summary>
	public const int FailedToRetrieveConnectionPoolStats = 104811;

	/// <summary>Executing document request.</summary>
	public const int ExecutingDocumentRequest = 104812;

	/// <summary>Failed to execute document request.</summary>
	public const int FailedToExecuteDocumentRequest = 104813;

	/// <summary>Executing document request in transaction.</summary>
	public const int ExecutingDocumentRequestInTransaction = 104814;

	/// <summary>Failed to execute document request in transaction.</summary>
	public const int FailedToExecuteDocumentRequestInTransaction = 104815;

	/// <summary>Executing batch of document requests.</summary>
	public const int ExecutingBatchOfDocumentRequests = 104816;

	/// <summary>Failed to retrieve database statistics.</summary>
	public const int FailedToRetrieveDatabaseStatistics = 104817;

	/// <summary>Failed to retrieve collection info.</summary>
	public const int FailedToRetrieveCollectionInfo = 104818;

	/// <summary>Disposing provider.</summary>
	public const int DisposingProvider = 104819;

	// ========================================
	// 104820-104839: EventStore
	// ========================================

	/// <summary>Events appended to stream.</summary>
	public const int EventsAppended = 104820;

	/// <summary>Concurrency conflict detected.</summary>
	public const int ConcurrencyConflict = 104821;

	/// <summary>Append error occurred.</summary>
	public const int AppendError = 104822;

	/// <summary>Event dispatched.</summary>
	public const int EventDispatched = 104823;

	// ========================================
	// 104840-104859: SnapshotStore
	// ========================================

	/// <summary>Snapshot saved.</summary>
	public const int SnapshotSaved = 104840;

	/// <summary>Snapshot version skipped.</summary>
	public const int SnapshotVersionSkipped = 104841;

	/// <summary>Snapshot deleted.</summary>
	public const int SnapshotDeleted = 104842;

	/// <summary>Snapshot older than version deleted.</summary>
	public const int SnapshotOlderDeleted = 104843;

	// ========================================
	// 104860-104869: SagaStore
	// ========================================

	/// <summary>Saga state saved.</summary>
	public const int SagaStateSaved = 104860;

	/// <summary>Saga state deleted.</summary>
	public const int SagaStateDeleted = 104861;

	/// <summary>Saga state loaded.</summary>
	public const int SagaStateLoaded = 104862;

	// ========================================
	// 104870-104889: ProjectionStore
	// ========================================

	/// <summary>Projection upserted.</summary>
	public const int ProjectionUpserted = 104870;

	/// <summary>Projection deleted.</summary>
	public const int ProjectionDeleted = 104871;

	/// <summary>Projections deleted by type.</summary>
	public const int ProjectionsDeletedByType = 104872;

	/// <summary>Projection store initialized.</summary>
	public const int ProjectionStoreInitialized = 104873;

	// ========================================
	// 104890-104909: OutboxStore
	// ========================================

	/// <summary>Message staged.</summary>
	public const int MessageStaged = 104890;

	/// <summary>Message enqueued.</summary>
	public const int MessageEnqueued = 104891;

	/// <summary>Message sent.</summary>
	public const int MessageSent = 104892;

	/// <summary>Message failed.</summary>
	public const int MessageFailed = 104893;

	/// <summary>Messages cleaned up.</summary>
	public const int MessagesCleanedUp = 104894;

	// ========================================
	// 104910-104929: InboxStore
	// ========================================

	/// <summary>Inbox message stored.</summary>
	public const int InboxStored = 104910;

	/// <summary>Inbox message marked complete.</summary>
	public const int InboxMarkedComplete = 104911;

	/// <summary>Inbox message already processed.</summary>
	public const int InboxAlreadyProcessed = 104912;

	/// <summary>Inbox message marked failed.</summary>
	public const int InboxMarkedFailed = 104913;

	/// <summary>Inbox cleaned up.</summary>
	public const int InboxCleanedUp = 104914;

	/// <summary>First inbox processor.</summary>
	public const int InboxFirstProcessor = 104915;

	// ========================================
	// 104930-104949: CDC Processor
	// ========================================

	/// <summary>CDC processor starting.</summary>
	public const int CdcStarting = 104930;

	/// <summary>CDC processor stopping.</summary>
	public const int CdcStopping = 104931;

	/// <summary>CDC batch received.</summary>
	public const int CdcBatchReceived = 104932;

	/// <summary>CDC position confirmed.</summary>
	public const int CdcPositionConfirmed = 104933;

	/// <summary>CDC processing error.</summary>
	public const int CdcProcessingError = 104934;

	/// <summary>CDC resuming from token.</summary>
	public const int CdcResumingFromToken = 104935;

	/// <summary>CDC starting from beginning.</summary>
	public const int CdcStartingFromBeginning = 104936;

	/// <summary>CDC stream watching.</summary>
	public const int CdcStreamWatching = 104937;

	/// <summary>CDC event processed.</summary>
	public const int CdcEventProcessed = 104938;

	/// <summary>CDC stream invalidated.</summary>
	public const int CdcStreamInvalidated = 104939;

	// ========================================
	// 104950-104969: Grants
	// ========================================

	/// <summary>Grant saved.</summary>
	public const int GrantSaved = 104950;

	/// <summary>Grant deleted.</summary>
	public const int GrantDeleted = 104951;

	/// <summary>Grant not found.</summary>
	public const int GrantNotFound = 104952;

	/// <summary>Grants listed.</summary>
	public const int GrantsListed = 104953;

	/// <summary>Grant service initialized.</summary>
	public const int GrantServiceInitialized = 104954;

	/// <summary>Grant revoked.</summary>
	public const int GrantRevoked = 104955;

	// ========================================
	// 104970-104989: Activity Groups
	// ========================================

	/// <summary>Activity group grant saved.</summary>
	public const int ActivityGroupGrantSaved = 104970;

	/// <summary>Activity group grant deleted.</summary>
	public const int ActivityGroupGrantDeleted = 104971;

	/// <summary>Activity group grant not found.</summary>
	public const int ActivityGroupGrantNotFound = 104972;

	/// <summary>Activity group grants listed.</summary>
	public const int ActivityGroupGrantsListed = 104973;

	/// <summary>Activity group service initialized.</summary>
	public const int ActivityGroupServiceInitialized = 104974;

	/// <summary>Activity group grants deleted by user.</summary>
	public const int ActivityGroupGrantsDeletedByUser = 104975;

	/// <summary>All activity group grants deleted.</summary>
	public const int ActivityGroupAllGrantsDeleted = 104976;

	// ========================================
	// 104990-104999: Leader Election
	// ========================================

	/// <summary>Leader election started.</summary>
	public const int LeaderElectionStarted = 104990;

	/// <summary>Leader election stopped.</summary>
	public const int LeaderElectionStopped = 104991;

	/// <summary>Became leader.</summary>
	public const int LeaderElectionBecameLeader = 104992;

	/// <summary>Lost leadership.</summary>
	public const int LeaderElectionLostLeadership = 104993;

	/// <summary>Leader election error.</summary>
	public const int LeaderElectionError = 104994;

	/// <summary>Leader election dispose error.</summary>
	public const int LeaderElectionDisposeError = 104995;
}
