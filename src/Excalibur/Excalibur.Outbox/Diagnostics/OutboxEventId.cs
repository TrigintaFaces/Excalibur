// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Diagnostics;

/// <summary>
/// Event IDs for outbox pattern infrastructure (130000-134999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>130000-130999: Outbox Core</item>
/// <item>131000-131999: Outbox Processing</item>
/// <item>132000-132999: Inbox Pattern</item>
/// <item>133000-133999: Outbox Storage</item>
/// <item>134000-134999: Outbox Cleanup</item>
/// </list>
/// </remarks>
public static class OutboxEventId
{
	// ========================================
	// 130000-130099: Outbox Core
	// ========================================

	/// <summary>Outbox service created.</summary>
	public const int OutboxServiceCreated = 130000;

	/// <summary>Outbox message stored.</summary>
	public const int OutboxMessageStored = 130001;

	/// <summary>Outbox message published.</summary>
	public const int OutboxMessagePublished = 130002;

	/// <summary>Outbox message failed.</summary>
	public const int OutboxMessageFailed = 130003;

	/// <summary>Outbox batch stored.</summary>
	public const int OutboxBatchStored = 130004;

	// ========================================
	// 130100-130199: Outbox Configuration
	// ========================================

	/// <summary>Outbox configuration loaded.</summary>
	public const int OutboxConfigurationLoaded = 130100;

	/// <summary>Outbox publisher configured.</summary>
	public const int OutboxPublisherConfigured = 130101;

	/// <summary>Outbox retry policy configured.</summary>
	public const int OutboxRetryPolicyConfigured = 130102;

	/// <summary>Outbox batch size configured.</summary>
	public const int OutboxBatchSizeConfigured = 130103;

	// ========================================
	// 131000-131099: Outbox Processing Core
	// ========================================

	/// <summary>Outbox processor started.</summary>
	public const int OutboxProcessorStarted = 131000;

	/// <summary>Outbox processor stopped.</summary>
	public const int OutboxProcessorStopped = 131001;

	/// <summary>Outbox batch processing started.</summary>
	public const int OutboxBatchProcessingStarted = 131002;

	/// <summary>Outbox batch processing completed.</summary>
	public const int OutboxBatchProcessingCompleted = 131003;


	/// <summary>Outbox processing cycle completed.</summary>
	public const int OutboxProcessingCycleCompleted = 131004;

	// ========================================
	// 131100-131199: Outbox Processing Operations
	// ========================================

	/// <summary>Outbox message retry scheduled.</summary>
	public const int OutboxMessageRetryScheduled = 131100;

	/// <summary>Outbox message retrying.</summary>
	public const int OutboxMessageRetrying = 131101;

	/// <summary>Outbox message dead-lettered.</summary>
	public const int OutboxMessageDeadLettered = 131102;

	/// <summary>Outbox message acknowledged.</summary>
	public const int OutboxMessageAcknowledged = 131103;

	/// <summary>Outbox lock acquired.</summary>
	public const int OutboxLockAcquired = 131104;


	/// <summary>Outbox lock released.</summary>
	public const int OutboxLockReleased = 131105;

	// ========================================
	// 131300-131399: Outbox Background Service
	// ========================================

	/// <summary>Outbox background service disabled.</summary>
	public const int OutboxBackgroundServiceDisabled = 131300;

	/// <summary>Outbox background service starting.</summary>
	public const int OutboxBackgroundServiceStarting = 131301;

	/// <summary>Outbox background service error.</summary>
	public const int OutboxBackgroundServiceError = 131302;

	/// <summary>Outbox background service stopped.</summary>
	public const int OutboxBackgroundServiceStopped = 131303;

	/// <summary>Outbox background processed pending messages.</summary>
	public const int OutboxBackgroundProcessedPending = 131304;

	/// <summary>Outbox background processed scheduled messages.</summary>
	public const int OutboxBackgroundProcessedScheduled = 131305;

	/// <summary>Outbox background retried failed messages.</summary>
	public const int OutboxBackgroundRetriedFailed = 131306;

	/// <summary>Outbox background service drain timeout exceeded.</summary>
	public const int OutboxBackgroundServiceDrainTimeout = 131307;

	/// <summary>Inbox background service drain timeout exceeded.</summary>
	public const int InboxBackgroundServiceDrainTimeout = 132005;

	// ========================================
	// 132000-132099: Inbox Core
	// ========================================

	/// <summary>Inbox service created.</summary>
	public const int InboxServiceCreated = 132000;

	/// <summary>Inbox message received.</summary>
	public const int InboxMessageReceived = 132001;

	/// <summary>Inbox message processed.</summary>
	public const int InboxMessageProcessed = 132002;

	/// <summary>Inbox duplicate detected.</summary>
	public const int InboxDuplicateDetected = 132003;

	/// <summary>Inbox message acknowledged.</summary>
	public const int InboxMessageAcknowledged = 132004;

	// ========================================
	// 132100-132199: Inbox Operations
	// ========================================

	/// <summary>Inbox idempotency check passed.</summary>
	public const int InboxIdempotencyCheckPassed = 132100;

	/// <summary>Inbox idempotency check failed.</summary>
	public const int InboxIdempotencyCheckFailed = 132101;

	/// <summary>Inbox message stored.</summary>
	public const int InboxMessageStored = 132102;

	/// <summary>Inbox entry expired.</summary>
	public const int InboxEntryExpired = 132103;

	// ========================================
	// 133000-133099: Outbox Storage Core
	// ========================================

	/// <summary>Outbox store created.</summary>
	public const int OutboxStoreCreated = 133000;

	/// <summary>Outbox messages retrieved.</summary>
	public const int OutboxMessagesRetrieved = 133001;

	/// <summary>Outbox message marked as processed.</summary>
	public const int OutboxMessageMarkedProcessed = 133002;

	/// <summary>Outbox message deleted.</summary>
	public const int OutboxMessageDeleted = 133003;

	// ========================================
	// 133100-133199: Outbox Storage Providers
	// ========================================

	/// <summary>SQL Server outbox store created.</summary>
	public const int SqlServerOutboxStoreCreated = 133100;

	/// <summary>Cosmos DB outbox store created.</summary>
	public const int CosmosDbOutboxStoreCreated = 133101;

	/// <summary>DynamoDB outbox store created.</summary>
	public const int DynamoDbOutboxStoreCreated = 133102;

	/// <summary>Firestore outbox store created.</summary>
	public const int FirestoreOutboxStoreCreated = 133103;

	// ========================================
	// 134000-134099: Outbox Cleanup
	// ========================================

	/// <summary>Outbox cleanup started.</summary>
	public const int OutboxCleanupStarted = 134000;

	/// <summary>Outbox cleanup completed.</summary>
	public const int OutboxCleanupCompleted = 134001;

	/// <summary>Outbox messages purged.</summary>
	public const int OutboxMessagesPurged = 134002;

	/// <summary>Outbox retention policy applied.</summary>
	public const int OutboxRetentionPolicyApplied = 134003;

	/// <summary>Inbox cleanup completed.</summary>
	public const int InboxCleanupCompleted = 134004;

	// ========================================
	// 130200-130299: MessageOutbox
	// ========================================

	/// <summary>Outbox started.</summary>
	public const int MessageOutboxStarted = 130200;

	/// <summary>Outbox error.</summary>
	public const int MessageOutboxError = 130201;

	/// <summary>Outbox stopped.</summary>
	public const int MessageOutboxStopped = 130202;

	/// <summary>No messages to save.</summary>
	public const int NoMessagesToSave = 130203;

	/// <summary>Could not resolve message type.</summary>
	public const int CouldNotResolveMessageType = 130204;

	/// <summary>Failed to deserialize message.</summary>
	public const int FailedToDeserializeMessage = 130205;

	// ========================================
	// 131200-131399: OutboxProcessor
	// ========================================

	/// <summary>Dispatch failed.</summary>
	public const int OutboxDispatchFailed = 131200;

	/// <summary>Disposing resources.</summary>
	public const int OutboxDisposingResources = 131201;

	/// <summary>Consumer not completed.</summary>
	public const int OutboxConsumerNotCompleted = 131202;

	/// <summary>Consumer timeout during disposal.</summary>
	public const int OutboxConsumerTimeoutDuringDisposal = 131203;

	/// <summary>Error disposing async resources.</summary>
	public const int OutboxErrorDisposingAsyncResources = 131204;

	/// <summary>Producer idle exiting.</summary>
	public const int OutboxProducerIdleExiting = 131205;

	/// <summary>Enqueuing batch records.</summary>
	public const int OutboxEnqueuingBatchRecords = 131206;

	/// <summary>Outbox producer canceled.</summary>
	public const int OutboxProducerCanceled = 131207;

	/// <summary>Error in producer loop.</summary>
	public const int OutboxErrorInProducerLoop = 131208;

	/// <summary>Producer completed.</summary>
	public const int OutboxProducerCompleted = 131209;

	/// <summary>Consumer exiting.</summary>
	public const int OutboxConsumerExiting = 131210;

	/// <summary>Consumer canceled.</summary>
	public const int OutboxConsumerCanceled = 131211;

	/// <summary>Error in consumer loop.</summary>
	public const int OutboxErrorInConsumerLoop = 131212;

	/// <summary>Outbox processing completed.</summary>
	public const int OutboxProcessingCompleted = 131213;

	/// <summary>Dispatching outbox record.</summary>
	public const int DispatchingOutboxRecord = 131214;

	/// <summary>Successfully dispatched outbox record.</summary>
	public const int SuccessfullyDispatchedOutboxRecord = 131215;

	/// <summary>Marked outbox record sent.</summary>
	public const int MarkedOutboxRecordSent = 131216;

	/// <summary>Error dispatching outbox record.</summary>
	public const int ErrorDispatchingOutboxRecord = 131217;

	/// <summary>Disposal requested exiting data.</summary>
	public const int OutboxDisposalRequestedExitingData = 131218;

	/// <summary>Message routed to DLQ.</summary>
	public const int OutboxMessageRoutedToDlq = 131219;

	/// <summary>Circuit breaker open.</summary>
	public const int OutboxCircuitBreakerOpen = 131220;

	/// <summary>Retry with backoff.</summary>
	public const int OutboxRetryWithBackoff = 131221;

	/// <summary>Transactional fallback.</summary>
	public const int OutboxTransactionalFallback = 131222;

	// ========================================
	// 132200-132399: InboxProcessor
	// ========================================

	/// <summary>No inbox record found.</summary>
	public const int InboxNoRecord = 132200;

	/// <summary>Enqueuing batch.</summary>
	public const int InboxEnqueuingBatch = 132201;

	/// <summary>Inbox producer canceled.</summary>
	public const int InboxProducerCanceled = 132202;

	/// <summary>Inbox producer error.</summary>
	public const int InboxProducerError = 132203;

	/// <summary>Inbox producer completed.</summary>
	public const int InboxProducerCompleted = 132204;

	/// <summary>Inbox consumer disposal requested.</summary>
	public const int InboxConsumerDisposalRequested = 132205;

	/// <summary>Inbox consumer exiting.</summary>
	public const int InboxConsumerExiting = 132206;

	/// <summary>Inbox consumer canceled.</summary>
	public const int InboxConsumerCanceled = 132207;

	/// <summary>Inbox consumer error.</summary>
	public const int InboxConsumerError = 132208;

	/// <summary>Inbox processing complete.</summary>
	public const int InboxProcessingComplete = 132209;

	/// <summary>Inbox dispatching message.</summary>
	public const int InboxDispatchingMessage = 132210;

	/// <summary>Inbox dispatch success.</summary>
	public const int InboxDispatchSuccess = 132211;

	/// <summary>Inbox dispatch error.</summary>
	public const int InboxDispatchError = 132212;

	/// <summary>Inbox disposing resources.</summary>
	public const int InboxDisposingResources = 132213;

	/// <summary>Inbox consumer not completed.</summary>
	public const int InboxConsumerNotCompleted = 132214;

	/// <summary>Inbox consumer timeout.</summary>
	public const int InboxConsumerTimeout = 132215;

	/// <summary>Inbox dispose error.</summary>
	public const int InboxDisposeError = 132216;

	/// <summary>Inbox message routed to DLQ.</summary>
	public const int InboxMessageRoutedToDlq = 132217;

	/// <summary>Inbox circuit breaker open.</summary>
	public const int InboxCircuitBreakerOpen = 132218;

	/// <summary>Inbox retry with backoff.</summary>
	public const int InboxRetryWithBackoff = 132219;

	// ========================================
	// 133200-133299: Cosmos DB Cloud Outbox
	// ========================================

	/// <summary>Cosmos DB outbox store initializing.</summary>
	public const int CosmosDbOutboxStoreInitializing = 133200;

	/// <summary>Cosmos DB outbox operation completed.</summary>
	public const int CosmosDbOutboxOperationCompleted = 133201;

	/// <summary>Cosmos DB outbox operation failed.</summary>
	public const int CosmosDbOutboxOperationFailed = 133202;

	/// <summary>Cosmos DB change feed subscription starting.</summary>
	public const int CosmosDbChangeFeedStarting = 133203;

	/// <summary>Cosmos DB change feed subscription stopping.</summary>
	public const int CosmosDbChangeFeedStopping = 133204;

	/// <summary>Cosmos DB change feed batch received.</summary>
	public const int CosmosDbChangeFeedBatchReceived = 133205;

	// ========================================
	// 133300-133399: DynamoDB Cloud Outbox
	// ========================================

	/// <summary>DynamoDB outbox store initializing.</summary>
	public const int DynamoDbOutboxStoreInitializing = 133300;

	/// <summary>DynamoDB outbox operation completed.</summary>
	public const int DynamoDbOutboxOperationCompleted = 133301;

	/// <summary>DynamoDB outbox operation failed.</summary>
	public const int DynamoDbOutboxOperationFailed = 133302;

	/// <summary>DynamoDB streams subscription starting.</summary>
	public const int DynamoDbStreamsStarting = 133303;

	/// <summary>DynamoDB streams subscription stopping.</summary>
	public const int DynamoDbStreamsStopping = 133304;

	/// <summary>DynamoDB streams batch received.</summary>
	public const int DynamoDbStreamsBatchReceived = 133305;

	// ========================================
	// 133400-133499: Firestore Cloud Outbox
	// ========================================

	/// <summary>Firestore outbox store initializing.</summary>
	public const int FirestoreOutboxStoreInitializing = 133400;

	/// <summary>Firestore outbox operation completed.</summary>
	public const int FirestoreOutboxOperationCompleted = 133401;

	/// <summary>Firestore outbox operation failed.</summary>
	public const int FirestoreOutboxOperationFailed = 133402;

	/// <summary>Firestore listener subscription starting.</summary>
	public const int FirestoreListenerStarting = 133403;

	/// <summary>Firestore listener subscription stopping.</summary>
	public const int FirestoreListenerStopping = 133404;

	/// <summary>Firestore listener batch received.</summary>
	public const int FirestoreListenerBatchReceived = 133405;

	// ========================================
	// 132400-132499: MessageInbox
	// ========================================

	/// <summary>Starting inbox dispatch.</summary>
	public const int MessageInboxDispatchStarting = 132400;

	/// <summary>Completed inbox dispatch.</summary>
	public const int MessageInboxDispatchCompleted = 132401;
}
