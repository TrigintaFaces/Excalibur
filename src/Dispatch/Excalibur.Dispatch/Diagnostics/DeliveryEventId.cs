// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Event IDs for delivery and background services (40000-40999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>40000-40099: Outbox Background Service</item>
/// <item>40100-40199: Final Dispatch Handler</item>
/// <item>40200-40299: Pipeline Evaluation</item>
/// <item>40300-40399: EventStore Dispatch</item>
/// <item>40400-40499: Deduplication</item>
/// <item>40500-40599: Scheduling Core</item>
/// <item>40600-40699: Cron Scheduling</item>
/// <item>40700-40799: Transport Routing</item>
/// <item>40800-40899: In-Memory Transport</item>
/// <item>40900-40999: Cron Timer Transport</item>
/// </list>
/// </remarks>
public static class DeliveryEventId
{
	// ========================================
	// 40000-40099: Outbox Background Service
	// ========================================

	/// <summary>Outbox service starting up.</summary>
	public const int OutboxServiceStarting = 40000;

	/// <summary>Outbox service shutting down.</summary>
	public const int OutboxServiceStopping = 40001;

	/// <summary>Outbox store not registered.</summary>
	public const int OutboxStoreNotRegistered = 40002;

	/// <summary>Outbox processing error.</summary>
	public const int OutboxProcessingError = 40003;

	/// <summary>No unsent messages in outbox.</summary>
	public const int NoUnsentMessages = 40004;

	/// <summary>Found unsent messages to process.</summary>
	public const int FoundUnsentMessages = 40005;

	/// <summary>Message not ready for delivery.</summary>
	public const int MessageNotReady = 40006;

	/// <summary>Message not eligible for retry.</summary>
	public const int MessageNotEligibleForRetry = 40007;

	/// <summary>Failed to publish message.</summary>
	public const int FailedToPublishOutboxMessage = 40008;

	/// <summary>Outbox processing completed.</summary>
	public const int OutboxProcessingCompleted = 40009;

	/// <summary>Outbox message processing error.</summary>
	public const int OutboxMessageProcessingError = 40010;

	/// <summary>Publishing message from outbox.</summary>
	public const int PublishingOutboxMessage = 40011;

	/// <summary>Message published successfully.</summary>
	public const int MessagePublishedSuccessfully = 40012;

	/// <summary>Failed to publish single message.</summary>
	public const int FailedToPublishSingleMessage = 40013;

	/// <summary>Starting outbox cleanup.</summary>
	public const int StartingOutboxCleanup = 40014;

	/// <summary>Outbox cleanup completed.</summary>
	public const int OutboxCleanupCompleted = 40015;

	/// <summary>Outbox cleanup error.</summary>
	public const int OutboxCleanupError = 40016;

	// ========================================
	// 40100-40199: Final Dispatch Handler
	// ========================================

	/// <summary>Final dispatch handler executing.</summary>
	public const int FinalDispatchExecuting = 40100;

	/// <summary>Final dispatch completed.</summary>
	public const int FinalDispatchCompleted = 40101;

	/// <summary>Final dispatch failed.</summary>
	public const int FinalDispatchFailed = 40102;

	/// <summary>No message bus found for routing.</summary>
	public const int FinalDispatchNoBusFound = 40103;

	/// <summary>Cache hit check during final dispatch.</summary>
	public const int FinalDispatchCacheHitCheck = 40104;

	// ========================================
	// 40200-40299: Pipeline Evaluation
	// ========================================

	/// <summary>No applicable middleware for message kind.</summary>
	public const int NoApplicableMiddleware = 40200;

	/// <summary>Executing middleware pipeline.</summary>
	public const int ExecutingMiddleware = 40201;

	/// <summary>Middleware applicable for message kind.</summary>
	public const int MiddlewareApplicable = 40202;

	/// <summary>Middleware not applicable for message kind.</summary>
	public const int MiddlewareNotApplicable = 40203;

	/// <summary>Applicability evaluation error.</summary>
	public const int ApplicabilityEvaluationError = 40204;

	/// <summary>Middleware excluded from pipeline.</summary>
	public const int MiddlewareExcluded = 40205;

	/// <summary>Middleware requires feature.</summary>
	public const int MiddlewareRequiresFeature = 40206;

	/// <summary>No handlers registered for event type.</summary>
	public const int NoHandlersForEvent = 40207;

	// ========================================
	// 40300-40399: EventStore Dispatch
	// ========================================

	/// <summary>Event store dispatcher service started.</summary>
	public const int EventStoreServiceStarted = 40300;

	/// <summary>Event store dispatcher error processing.</summary>
	public const int EventStoreErrorProcessing = 40301;

	/// <summary>Event store dispatcher service stopped.</summary>
	public const int EventStoreServiceStopped = 40302;

	/// <summary>Event store dispatcher initialized.</summary>
	public const int EventStoreDispatcherInitialized = 40303;

	// ========================================
	// 40400-40499: Deduplication
	// ========================================

	/// <summary>Deduplicator initialized.</summary>
	public const int DeduplicatorInitialized = 40400;

	/// <summary>Duplicate message detected.</summary>
	public const int DuplicateDetected = 40401;

	/// <summary>Expired entry removed from deduplicator.</summary>
	public const int ExpiredEntryRemoved = 40402;

	/// <summary>Message marked as processed.</summary>
	public const int MessageMarkedProcessed = 40403;

	/// <summary>Cleaned up expired entries.</summary>
	public const int CleanedUpExpiredEntries = 40404;

	/// <summary>Cleared deduplication entries.</summary>
	public const int ClearedEntries = 40405;

	/// <summary>Deduplicator disposed.</summary>
	public const int DeduplicatorDisposed = 40406;

	/// <summary>Scheduled cleanup removed entries.</summary>
	public const int ScheduledCleanupRemoved = 40407;

	/// <summary>Deduplicator statistics.</summary>
	public const int DeduplicatorStats = 40408;

	/// <summary>Scheduled cleanup error.</summary>
	public const int ScheduledCleanupError = 40409;

	// ========================================
	// 40450-40499: Poison Message Handling
	// ========================================

	/// <summary>Poison message detected by composite detector.</summary>
	public const int PoisonMessageDetected = 40450;

	/// <summary>Poison detector evaluation error.</summary>
	public const int PoisonDetectorError = 40451;

	/// <summary>Poison message cleanup service starting.</summary>
	public const int PoisonCleanupStarting = 40452;

	/// <summary>Poison message cleanup service stopping.</summary>
	public const int PoisonCleanupStopping = 40453;

	/// <summary>Poison message cleanup error.</summary>
	public const int PoisonCleanupError = 40454;

	/// <summary>Poison message cleanup completed.</summary>
	public const int PoisonCleanupCompleted = 40455;

	/// <summary>Poison message cleanup statistics.</summary>
	public const int PoisonCleanupStats = 40456;

	/// <summary>Poison message cleanup archive error.</summary>
	public const int PoisonCleanupArchiveError = 40457;

	/// <summary>Poison message cleanup reprocessing error.</summary>
	public const int PoisonCleanupReprocessError = 40458;

	/// <summary>Poison message cleanup no store configured.</summary>
	public const int PoisonCleanupNoStore = 40459;

	/// <summary>Poison message cleanup cycle error.</summary>
	public const int PoisonCleanupCycleError = 40460;

	/// <summary>Poison message handler detected poison message.</summary>
	public const int PoisonHandlerDetected = 40461;

	/// <summary>Poison message handler store error.</summary>
	public const int PoisonHandlerStoreError = 40462;

	/// <summary>Poison message handler no store available.</summary>
	public const int PoisonHandlerNoStore = 40463;

	/// <summary>Poison message handler dead letter error.</summary>
	public const int PoisonHandlerDeadLetterError = 40464;

	/// <summary>Poison message handler notification error.</summary>
	public const int PoisonHandlerNotificationError = 40465;

	/// <summary>Poison message handler stored successfully.</summary>
	public const int PoisonHandlerStored = 40466;

	/// <summary>Poison message handler already in dead letter.</summary>
	public const int PoisonHandlerAlreadyDeadLetter = 40467;

	/// <summary>Poison message handler error during processing.</summary>
	public const int PoisonHandlerProcessingError = 40468;

	/// <summary>Poison message middleware detected poison message.</summary>
	public const int PoisonMiddlewareDetected = 40469;

	/// <summary>Poison message middleware handler error.</summary>
	public const int PoisonMiddlewareHandlerError = 40470;

	/// <summary>Dead letter store message added.</summary>
	public const int DeadLetterMessageAdded = 40471;

	/// <summary>Dead letter store message removed.</summary>
	public const int DeadLetterMessageRemoved = 40472;

	/// <summary>Dead letter store message retrieved.</summary>
	public const int DeadLetterMessageRetrieved = 40473;

	/// <summary>Dead letter store statistics.</summary>
	public const int DeadLetterStoreStats = 40474;

	/// <summary>Poison message auto-cleanup disabled.</summary>
	public const int PoisonCleanupDisabled = 40475;

	/// <summary>Poison message alert threshold exceeded.</summary>
	public const int PoisonAlertThresholdExceeded = 40476;

	/// <summary>Top poison message type statistics.</summary>
	public const int PoisonAlertTopMessageType = 40477;

	/// <summary>Top poison message reason statistics.</summary>
	public const int PoisonAlertTopReason = 40478;

	/// <summary>Poison message alert check error.</summary>
	public const int PoisonAlertCheckError = 40479;

	/// <summary>Poison message replay - message not found.</summary>
	public const int PoisonReplayNotFound = 40480;

	/// <summary>Poison message replay - message type not found.</summary>
	public const int PoisonReplayTypeNotFound = 40481;

	/// <summary>Poison message replay - not a dispatch message.</summary>
	public const int PoisonReplayNotDispatchMessage = 40482;

	/// <summary>Poison message replay succeeded.</summary>
	public const int PoisonReplaySuccess = 40483;

	/// <summary>Poison message replay failed.</summary>
	public const int PoisonReplayFailed = 40484;

	/// <summary>Poison message replay error.</summary>
	public const int PoisonReplayError = 40485;

	/// <summary>Dead letter message marked as replayed.</summary>
	public const int DeadLetterMessageReplayed = 40486;

	/// <summary>Dead letter cleanup completed.</summary>
	public const int DeadLetterCleanupCompleted = 40487;

	// ========================================
	// 40500-40599: Scheduling Core
	// ========================================

	/// <summary>Scheduled message service starting.</summary>
	public const int ScheduledServiceStarting = 40500;

	/// <summary>Scheduled message service stopping.</summary>
	public const int ScheduledServiceStopping = 40501;

	/// <summary>Message scheduled for delivery.</summary>
	public const int MessageScheduled = 40502;

	/// <summary>Scheduled message delivered.</summary>
	public const int ScheduledMessageDelivered = 40503;

	/// <summary>Scheduled message cancelled.</summary>
	public const int ScheduledMessageCancelled = 40504;

	/// <summary>Unknown scheduled message type.</summary>
	public const int ScheduledUnknownMessageType = 40505;

	/// <summary>Scheduled message deserialization failed.</summary>
	public const int ScheduledDeserializationFailed = 40506;

	/// <summary>Error processing scheduled messages.</summary>
	public const int ScheduledProcessingError = 40507;

	/// <summary>Schedule disabled due to missed executions.</summary>
	public const int ScheduledDisabled = 40508;

	/// <summary>Found missed scheduled executions.</summary>
	public const int ScheduledMissedExecutions = 40509;

	/// <summary>Unknown missed execution behavior.</summary>
	public const int ScheduledUnknownBehavior = 40510;

	/// <summary>Unsupported message type for scheduling.</summary>
	public const int ScheduledUnsupportedMessageType = 40511;

	/// <summary>Next execution time calculated.</summary>
	public const int ScheduledNextExecution = 40512;

	/// <summary>Timezone lookup failed.</summary>
	public const int ScheduledTimezoneLookupFailed = 40513;

	/// <summary>Scheduled recurring message with interval.</summary>
	public const int ScheduledRecurringWithInterval = 40514;

	/// <summary>Scheduled service has stopped (completed shutdown).</summary>
	public const int ScheduledServiceStopped = 40515;

	/// <summary>Scheduled message processed successfully.</summary>
	public const int ScheduledMessageProcessed = 40516;

	/// <summary>Timeout occurred during scheduled message processing loop.</summary>
	public const int ScheduledTimeoutDuringProcessing = 40517;

	/// <summary>Timeout processing individual scheduled message.</summary>
	public const int ScheduledTimeoutProcessingMessage = 40518;

	/// <summary>Error processing individual scheduled message.</summary>
	public const int ScheduledErrorProcessingMessage = 40519;

	/// <summary>Unknown dispatch type for scheduled message.</summary>
	public const int ScheduledUnknownDispatchType = 40520;

	// ========================================
	// 40600-40699: Cron Scheduling
	// ========================================

	/// <summary>Cron scheduler started.</summary>
	public const int CronSchedulerStarted = 40600;

	/// <summary>Cron scheduler stopped.</summary>
	public const int CronSchedulerStopped = 40601;

	/// <summary>Cron job registered.</summary>
	public const int CronJobRegistered = 40602;

	/// <summary>Cron job triggered.</summary>
	public const int CronJobTriggered = 40603;

	/// <summary>Recurring dispatch scheduled.</summary>
	public const int RecurringDispatchScheduled = 40604;

	/// <summary>Cron expression parsed successfully.</summary>
	public const int CronExpressionParsed = 40605;

	/// <summary>Cron expression parse failed.</summary>
	public const int CronExpressionParseFailed = 40606;

	/// <summary>Adjusted invalid DST time.</summary>
	public const int CronDstAdjustment = 40607;

	/// <summary>Error handling DST transition.</summary>
	public const int CronDstTransitionError = 40608;

	// ========================================
	// 40700-40799: Transport Routing
	// ========================================

	/// <summary>Transport adapter router started.</summary>
	public const int TransportAdapterRouterStarted = 40700;

	/// <summary>Transport route resolved.</summary>
	public const int TransportRouteResolved = 40701;

	/// <summary>Transport router middleware executing.</summary>
	public const int TransportRouterExecuting = 40702;

	/// <summary>No transport adapter found.</summary>
	public const int NoTransportAdapterFound = 40703;

	/// <summary>Routing message through transport adapter.</summary>
	public const int TransportRoutingMessage = 40704;

	/// <summary>Transport routing succeeded.</summary>
	public const int TransportRoutingSuccess = 40705;

	/// <summary>Transport routing failed.</summary>
	public const int TransportRoutingFailure = 40706;

	/// <summary>Transport routing error.</summary>
	public const int TransportRoutingError = 40707;

	/// <summary>Routing batch of messages.</summary>
	public const int TransportRoutingBatch = 40708;

	/// <summary>Adapter already registered.</summary>
	public const int TransportAdapterAlreadyRegistered = 40709;

	/// <summary>Adapter registered successfully.</summary>
	public const int TransportAdapterRegistered = 40710;

	/// <summary>Adapter registration failed.</summary>
	public const int TransportAdapterRegistrationFailed = 40711;

	/// <summary>Adapter unregister attempt for non-existent adapter.</summary>
	public const int TransportAdapterUnregisterAttempt = 40712;

	/// <summary>Adapter unregistered successfully.</summary>
	public const int TransportAdapterUnregistered = 40713;

	/// <summary>Adapter unregistration failed.</summary>
	public const int TransportAdapterUnregistrationFailed = 40714;

	/// <summary>Health check for adapter.</summary>
	public const int TransportAdapterHealthCheck = 40715;

	/// <summary>Health check failed for adapter.</summary>
	public const int TransportAdapterHealthCheckFailed = 40716;

	/// <summary>Message kind not accepted by binding.</summary>
	public const int TransportMessageKindNotAccepted = 40717;

	// ========================================
	// 40800-40899: In-Memory Transport
	// ========================================

	/// <summary>In-memory transport adapter started.</summary>
	public const int InMemoryTransportStarted = 40800;

	/// <summary>In-memory message published.</summary>
	public const int InMemoryMessagePublished = 40801;

	/// <summary>In-memory message received.</summary>
	public const int InMemoryMessageReceived = 40802;

	/// <summary>In-memory transport adapter stopping.</summary>
	public const int InMemoryTransportStopping = 40803;

	/// <summary>In-memory message processing failed.</summary>
	public const int InMemoryProcessingFailed = 40804;

	// ========================================
	// 40900-40999: Cron Timer Transport
	// ========================================

	/// <summary>Cron timer transport started.</summary>
	public const int CronTimerTransportStarted = 40900;

	/// <summary>Cron timer fired.</summary>
	public const int CronTimerFired = 40901;

	/// <summary>Cron timer transport stopped.</summary>
	public const int CronTimerTransportStopped = 40902;

	/// <summary>Next cron timer occurrence scheduled.</summary>
	public const int CronTimerNextOccurrence = 40903;

	/// <summary>Cron timer execution failed.</summary>
	public const int CronTimerExecutionFailed = 40904;

	/// <summary>Skipping overlapping cron timer execution.</summary>
	public const int CronTimerSkippingOverlap = 40905;

	/// <summary>SendAsync not supported on cron timer (trigger-only transport).</summary>
	public const int CronTimerSendNotSupported = 40906;
}
