// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DeliveryEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait("Priority", "0")]
public sealed class DeliveryEventIdShould : UnitTestBase
{
	#region Outbox Background Service Event IDs (40000-40099)

	[Fact]
	public void HaveOutboxServiceStartingInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxServiceStarting.ShouldBe(40000);
	}

	[Fact]
	public void HaveOutboxServiceStoppingInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxServiceStopping.ShouldBe(40001);
	}

	[Fact]
	public void HaveOutboxStoreNotRegisteredInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxStoreNotRegistered.ShouldBe(40002);
	}

	[Fact]
	public void HaveOutboxProcessingErrorInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxProcessingError.ShouldBe(40003);
	}

	[Fact]
	public void HaveNoUnsentMessagesInOutboxBackgroundRange()
	{
		DeliveryEventId.NoUnsentMessages.ShouldBe(40004);
	}

	[Fact]
	public void HaveFoundUnsentMessagesInOutboxBackgroundRange()
	{
		DeliveryEventId.FoundUnsentMessages.ShouldBe(40005);
	}

	[Fact]
	public void HaveMessageNotReadyInOutboxBackgroundRange()
	{
		DeliveryEventId.MessageNotReady.ShouldBe(40006);
	}

	[Fact]
	public void HaveMessageNotEligibleForRetryInOutboxBackgroundRange()
	{
		DeliveryEventId.MessageNotEligibleForRetry.ShouldBe(40007);
	}

	[Fact]
	public void HaveFailedToPublishOutboxMessageInOutboxBackgroundRange()
	{
		DeliveryEventId.FailedToPublishOutboxMessage.ShouldBe(40008);
	}

	[Fact]
	public void HaveOutboxProcessingCompletedInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxProcessingCompleted.ShouldBe(40009);
	}

	[Fact]
	public void HaveOutboxMessageProcessingErrorInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxMessageProcessingError.ShouldBe(40010);
	}

	[Fact]
	public void HavePublishingOutboxMessageInOutboxBackgroundRange()
	{
		DeliveryEventId.PublishingOutboxMessage.ShouldBe(40011);
	}

	[Fact]
	public void HaveMessagePublishedSuccessfullyInOutboxBackgroundRange()
	{
		DeliveryEventId.MessagePublishedSuccessfully.ShouldBe(40012);
	}

	[Fact]
	public void HaveFailedToPublishSingleMessageInOutboxBackgroundRange()
	{
		DeliveryEventId.FailedToPublishSingleMessage.ShouldBe(40013);
	}

	[Fact]
	public void HaveStartingOutboxCleanupInOutboxBackgroundRange()
	{
		DeliveryEventId.StartingOutboxCleanup.ShouldBe(40014);
	}

	[Fact]
	public void HaveOutboxCleanupCompletedInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxCleanupCompleted.ShouldBe(40015);
	}

	[Fact]
	public void HaveOutboxCleanupErrorInOutboxBackgroundRange()
	{
		DeliveryEventId.OutboxCleanupError.ShouldBe(40016);
	}

	#endregion Outbox Background Service Event IDs (40000-40099)

	#region Final Dispatch Handler Event IDs (40100-40199)

	[Fact]
	public void HaveFinalDispatchExecutingInFinalDispatchRange()
	{
		DeliveryEventId.FinalDispatchExecuting.ShouldBe(40100);
	}

	[Fact]
	public void HaveFinalDispatchCompletedInFinalDispatchRange()
	{
		DeliveryEventId.FinalDispatchCompleted.ShouldBe(40101);
	}

	[Fact]
	public void HaveFinalDispatchFailedInFinalDispatchRange()
	{
		DeliveryEventId.FinalDispatchFailed.ShouldBe(40102);
	}

	[Fact]
	public void HaveFinalDispatchNoBusFoundInFinalDispatchRange()
	{
		DeliveryEventId.FinalDispatchNoBusFound.ShouldBe(40103);
	}

	[Fact]
	public void HaveFinalDispatchCacheHitCheckInFinalDispatchRange()
	{
		DeliveryEventId.FinalDispatchCacheHitCheck.ShouldBe(40104);
	}

	#endregion Final Dispatch Handler Event IDs (40100-40199)

	#region Pipeline Evaluation Event IDs (40200-40299)

	[Fact]
	public void HaveNoApplicableMiddlewareInPipelineEvaluationRange()
	{
		DeliveryEventId.NoApplicableMiddleware.ShouldBe(40200);
	}

	[Fact]
	public void HaveExecutingMiddlewareInPipelineEvaluationRange()
	{
		DeliveryEventId.ExecutingMiddleware.ShouldBe(40201);
	}

	[Fact]
	public void HaveMiddlewareApplicableInPipelineEvaluationRange()
	{
		DeliveryEventId.MiddlewareApplicable.ShouldBe(40202);
	}

	[Fact]
	public void HaveMiddlewareNotApplicableInPipelineEvaluationRange()
	{
		DeliveryEventId.MiddlewareNotApplicable.ShouldBe(40203);
	}

	[Fact]
	public void HaveApplicabilityEvaluationErrorInPipelineEvaluationRange()
	{
		DeliveryEventId.ApplicabilityEvaluationError.ShouldBe(40204);
	}

	[Fact]
	public void HaveMiddlewareExcludedInPipelineEvaluationRange()
	{
		DeliveryEventId.MiddlewareExcluded.ShouldBe(40205);
	}

	[Fact]
	public void HaveMiddlewareRequiresFeatureInPipelineEvaluationRange()
	{
		DeliveryEventId.MiddlewareRequiresFeature.ShouldBe(40206);
	}

	[Fact]
	public void HaveNoHandlersForEventInPipelineEvaluationRange()
	{
		DeliveryEventId.NoHandlersForEvent.ShouldBe(40207);
	}

	#endregion Pipeline Evaluation Event IDs (40200-40299)

	#region EventStore Dispatch Event IDs (40300-40399)

	[Fact]
	public void HaveEventStoreServiceStartedInEventStoreRange()
	{
		DeliveryEventId.EventStoreServiceStarted.ShouldBe(40300);
	}

	[Fact]
	public void HaveEventStoreErrorProcessingInEventStoreRange()
	{
		DeliveryEventId.EventStoreErrorProcessing.ShouldBe(40301);
	}

	[Fact]
	public void HaveEventStoreServiceStoppedInEventStoreRange()
	{
		DeliveryEventId.EventStoreServiceStopped.ShouldBe(40302);
	}

	[Fact]
	public void HaveEventStoreDispatcherInitializedInEventStoreRange()
	{
		DeliveryEventId.EventStoreDispatcherInitialized.ShouldBe(40303);
	}

	#endregion EventStore Dispatch Event IDs (40300-40399)

	#region Deduplication Event IDs (40400-40499)

	[Fact]
	public void HaveDeduplicatorInitializedInDeduplicationRange()
	{
		DeliveryEventId.DeduplicatorInitialized.ShouldBe(40400);
	}

	[Fact]
	public void HaveDuplicateDetectedInDeduplicationRange()
	{
		DeliveryEventId.DuplicateDetected.ShouldBe(40401);
	}

	[Fact]
	public void HaveExpiredEntryRemovedInDeduplicationRange()
	{
		DeliveryEventId.ExpiredEntryRemoved.ShouldBe(40402);
	}

	[Fact]
	public void HaveMessageMarkedProcessedInDeduplicationRange()
	{
		DeliveryEventId.MessageMarkedProcessed.ShouldBe(40403);
	}

	[Fact]
	public void HaveCleanedUpExpiredEntriesInDeduplicationRange()
	{
		DeliveryEventId.CleanedUpExpiredEntries.ShouldBe(40404);
	}

	[Fact]
	public void HaveClearedEntriesInDeduplicationRange()
	{
		DeliveryEventId.ClearedEntries.ShouldBe(40405);
	}

	[Fact]
	public void HaveDeduplicatorDisposedInDeduplicationRange()
	{
		DeliveryEventId.DeduplicatorDisposed.ShouldBe(40406);
	}

	[Fact]
	public void HaveScheduledCleanupRemovedInDeduplicationRange()
	{
		DeliveryEventId.ScheduledCleanupRemoved.ShouldBe(40407);
	}

	[Fact]
	public void HaveDeduplicatorStatsInDeduplicationRange()
	{
		DeliveryEventId.DeduplicatorStats.ShouldBe(40408);
	}

	[Fact]
	public void HaveScheduledCleanupErrorInDeduplicationRange()
	{
		DeliveryEventId.ScheduledCleanupError.ShouldBe(40409);
	}

	#endregion Deduplication Event IDs (40400-40499)

	#region Poison Message Handling Event IDs (40450-40499)

	[Fact]
	public void HavePoisonMessageDetectedInPoisonRange()
	{
		DeliveryEventId.PoisonMessageDetected.ShouldBe(40450);
	}

	[Fact]
	public void HavePoisonDetectorErrorInPoisonRange()
	{
		DeliveryEventId.PoisonDetectorError.ShouldBe(40451);
	}

	[Fact]
	public void HavePoisonCleanupStartingInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupStarting.ShouldBe(40452);
	}

	[Fact]
	public void HavePoisonCleanupStoppingInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupStopping.ShouldBe(40453);
	}

	[Fact]
	public void HavePoisonCleanupErrorInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupError.ShouldBe(40454);
	}

	[Fact]
	public void HavePoisonCleanupCompletedInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupCompleted.ShouldBe(40455);
	}

	[Fact]
	public void HavePoisonCleanupStatsInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupStats.ShouldBe(40456);
	}

	[Fact]
	public void HavePoisonCleanupArchiveErrorInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupArchiveError.ShouldBe(40457);
	}

	[Fact]
	public void HavePoisonCleanupReprocessErrorInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupReprocessError.ShouldBe(40458);
	}

	[Fact]
	public void HavePoisonCleanupNoStoreInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupNoStore.ShouldBe(40459);
	}

	[Fact]
	public void HavePoisonCleanupCycleErrorInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupCycleError.ShouldBe(40460);
	}

	[Fact]
	public void HavePoisonHandlerDetectedInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerDetected.ShouldBe(40461);
	}

	[Fact]
	public void HavePoisonHandlerStoreErrorInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerStoreError.ShouldBe(40462);
	}

	[Fact]
	public void HavePoisonHandlerNoStoreInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerNoStore.ShouldBe(40463);
	}

	[Fact]
	public void HavePoisonHandlerDeadLetterErrorInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerDeadLetterError.ShouldBe(40464);
	}

	[Fact]
	public void HavePoisonHandlerNotificationErrorInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerNotificationError.ShouldBe(40465);
	}

	[Fact]
	public void HavePoisonHandlerStoredInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerStored.ShouldBe(40466);
	}

	[Fact]
	public void HavePoisonHandlerAlreadyDeadLetterInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerAlreadyDeadLetter.ShouldBe(40467);
	}

	[Fact]
	public void HavePoisonHandlerProcessingErrorInPoisonRange()
	{
		DeliveryEventId.PoisonHandlerProcessingError.ShouldBe(40468);
	}

	[Fact]
	public void HavePoisonMiddlewareDetectedInPoisonRange()
	{
		DeliveryEventId.PoisonMiddlewareDetected.ShouldBe(40469);
	}

	[Fact]
	public void HavePoisonMiddlewareHandlerErrorInPoisonRange()
	{
		DeliveryEventId.PoisonMiddlewareHandlerError.ShouldBe(40470);
	}

	[Fact]
	public void HaveDeadLetterMessageAddedInPoisonRange()
	{
		DeliveryEventId.DeadLetterMessageAdded.ShouldBe(40471);
	}

	[Fact]
	public void HaveDeadLetterMessageRemovedInPoisonRange()
	{
		DeliveryEventId.DeadLetterMessageRemoved.ShouldBe(40472);
	}

	[Fact]
	public void HaveDeadLetterMessageRetrievedInPoisonRange()
	{
		DeliveryEventId.DeadLetterMessageRetrieved.ShouldBe(40473);
	}

	[Fact]
	public void HaveDeadLetterStoreStatsInPoisonRange()
	{
		DeliveryEventId.DeadLetterStoreStats.ShouldBe(40474);
	}

	[Fact]
	public void HavePoisonCleanupDisabledInPoisonRange()
	{
		DeliveryEventId.PoisonCleanupDisabled.ShouldBe(40475);
	}

	[Fact]
	public void HavePoisonAlertThresholdExceededInPoisonRange()
	{
		DeliveryEventId.PoisonAlertThresholdExceeded.ShouldBe(40476);
	}

	[Fact]
	public void HavePoisonAlertTopMessageTypeInPoisonRange()
	{
		DeliveryEventId.PoisonAlertTopMessageType.ShouldBe(40477);
	}

	[Fact]
	public void HavePoisonAlertTopReasonInPoisonRange()
	{
		DeliveryEventId.PoisonAlertTopReason.ShouldBe(40478);
	}

	[Fact]
	public void HavePoisonAlertCheckErrorInPoisonRange()
	{
		DeliveryEventId.PoisonAlertCheckError.ShouldBe(40479);
	}

	[Fact]
	public void HavePoisonReplayNotFoundInPoisonRange()
	{
		DeliveryEventId.PoisonReplayNotFound.ShouldBe(40480);
	}

	[Fact]
	public void HavePoisonReplayTypeNotFoundInPoisonRange()
	{
		DeliveryEventId.PoisonReplayTypeNotFound.ShouldBe(40481);
	}

	[Fact]
	public void HavePoisonReplayNotDispatchMessageInPoisonRange()
	{
		DeliveryEventId.PoisonReplayNotDispatchMessage.ShouldBe(40482);
	}

	[Fact]
	public void HavePoisonReplaySuccessInPoisonRange()
	{
		DeliveryEventId.PoisonReplaySuccess.ShouldBe(40483);
	}

	[Fact]
	public void HavePoisonReplayFailedInPoisonRange()
	{
		DeliveryEventId.PoisonReplayFailed.ShouldBe(40484);
	}

	[Fact]
	public void HavePoisonReplayErrorInPoisonRange()
	{
		DeliveryEventId.PoisonReplayError.ShouldBe(40485);
	}

	[Fact]
	public void HaveDeadLetterMessageReplayedInPoisonRange()
	{
		DeliveryEventId.DeadLetterMessageReplayed.ShouldBe(40486);
	}

	[Fact]
	public void HaveDeadLetterCleanupCompletedInPoisonRange()
	{
		DeliveryEventId.DeadLetterCleanupCompleted.ShouldBe(40487);
	}

	#endregion Poison Message Handling Event IDs (40450-40499)

	#region Scheduling Core Event IDs (40500-40599)

	[Fact]
	public void HaveScheduledServiceStartingInSchedulingRange()
	{
		DeliveryEventId.ScheduledServiceStarting.ShouldBe(40500);
	}

	[Fact]
	public void HaveScheduledServiceStoppingInSchedulingRange()
	{
		DeliveryEventId.ScheduledServiceStopping.ShouldBe(40501);
	}

	[Fact]
	public void HaveMessageScheduledInSchedulingRange()
	{
		DeliveryEventId.MessageScheduled.ShouldBe(40502);
	}

	[Fact]
	public void HaveScheduledMessageDeliveredInSchedulingRange()
	{
		DeliveryEventId.ScheduledMessageDelivered.ShouldBe(40503);
	}

	[Fact]
	public void HaveScheduledMessageCancelledInSchedulingRange()
	{
		DeliveryEventId.ScheduledMessageCancelled.ShouldBe(40504);
	}

	[Fact]
	public void HaveScheduledUnknownMessageTypeInSchedulingRange()
	{
		DeliveryEventId.ScheduledUnknownMessageType.ShouldBe(40505);
	}

	[Fact]
	public void HaveScheduledDeserializationFailedInSchedulingRange()
	{
		DeliveryEventId.ScheduledDeserializationFailed.ShouldBe(40506);
	}

	[Fact]
	public void HaveScheduledProcessingErrorInSchedulingRange()
	{
		DeliveryEventId.ScheduledProcessingError.ShouldBe(40507);
	}

	[Fact]
	public void HaveScheduledDisabledInSchedulingRange()
	{
		DeliveryEventId.ScheduledDisabled.ShouldBe(40508);
	}

	[Fact]
	public void HaveScheduledMissedExecutionsInSchedulingRange()
	{
		DeliveryEventId.ScheduledMissedExecutions.ShouldBe(40509);
	}

	[Fact]
	public void HaveScheduledUnknownBehaviorInSchedulingRange()
	{
		DeliveryEventId.ScheduledUnknownBehavior.ShouldBe(40510);
	}

	[Fact]
	public void HaveScheduledUnsupportedMessageTypeInSchedulingRange()
	{
		DeliveryEventId.ScheduledUnsupportedMessageType.ShouldBe(40511);
	}

	[Fact]
	public void HaveScheduledNextExecutionInSchedulingRange()
	{
		DeliveryEventId.ScheduledNextExecution.ShouldBe(40512);
	}

	[Fact]
	public void HaveScheduledTimezoneLookupFailedInSchedulingRange()
	{
		DeliveryEventId.ScheduledTimezoneLookupFailed.ShouldBe(40513);
	}

	[Fact]
	public void HaveScheduledRecurringWithIntervalInSchedulingRange()
	{
		DeliveryEventId.ScheduledRecurringWithInterval.ShouldBe(40514);
	}

	[Fact]
	public void HaveScheduledServiceStoppedInSchedulingRange()
	{
		DeliveryEventId.ScheduledServiceStopped.ShouldBe(40515);
	}

	[Fact]
	public void HaveScheduledMessageProcessedInSchedulingRange()
	{
		DeliveryEventId.ScheduledMessageProcessed.ShouldBe(40516);
	}

	[Fact]
	public void HaveScheduledTimeoutDuringProcessingInSchedulingRange()
	{
		DeliveryEventId.ScheduledTimeoutDuringProcessing.ShouldBe(40517);
	}

	[Fact]
	public void HaveScheduledTimeoutProcessingMessageInSchedulingRange()
	{
		DeliveryEventId.ScheduledTimeoutProcessingMessage.ShouldBe(40518);
	}

	[Fact]
	public void HaveScheduledErrorProcessingMessageInSchedulingRange()
	{
		DeliveryEventId.ScheduledErrorProcessingMessage.ShouldBe(40519);
	}

	[Fact]
	public void HaveScheduledUnknownDispatchTypeInSchedulingRange()
	{
		DeliveryEventId.ScheduledUnknownDispatchType.ShouldBe(40520);
	}

	#endregion Scheduling Core Event IDs (40500-40599)

	#region Cron Scheduling Event IDs (40600-40699)

	[Fact]
	public void HaveCronSchedulerStartedInCronRange()
	{
		DeliveryEventId.CronSchedulerStarted.ShouldBe(40600);
	}

	[Fact]
	public void HaveCronSchedulerStoppedInCronRange()
	{
		DeliveryEventId.CronSchedulerStopped.ShouldBe(40601);
	}

	[Fact]
	public void HaveCronJobRegisteredInCronRange()
	{
		DeliveryEventId.CronJobRegistered.ShouldBe(40602);
	}

	[Fact]
	public void HaveCronJobTriggeredInCronRange()
	{
		DeliveryEventId.CronJobTriggered.ShouldBe(40603);
	}

	[Fact]
	public void HaveRecurringDispatchScheduledInCronRange()
	{
		DeliveryEventId.RecurringDispatchScheduled.ShouldBe(40604);
	}

	[Fact]
	public void HaveCronExpressionParsedInCronRange()
	{
		DeliveryEventId.CronExpressionParsed.ShouldBe(40605);
	}

	[Fact]
	public void HaveCronExpressionParseFailedInCronRange()
	{
		DeliveryEventId.CronExpressionParseFailed.ShouldBe(40606);
	}

	[Fact]
	public void HaveCronDstAdjustmentInCronRange()
	{
		DeliveryEventId.CronDstAdjustment.ShouldBe(40607);
	}

	[Fact]
	public void HaveCronDstTransitionErrorInCronRange()
	{
		DeliveryEventId.CronDstTransitionError.ShouldBe(40608);
	}

	#endregion Cron Scheduling Event IDs (40600-40699)

	#region Transport Routing Event IDs (40700-40799)

	[Fact]
	public void HaveTransportAdapterRouterStartedInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterRouterStarted.ShouldBe(40700);
	}

	[Fact]
	public void HaveTransportRouteResolvedInTransportRoutingRange()
	{
		DeliveryEventId.TransportRouteResolved.ShouldBe(40701);
	}

	[Fact]
	public void HaveTransportRouterExecutingInTransportRoutingRange()
	{
		DeliveryEventId.TransportRouterExecuting.ShouldBe(40702);
	}

	[Fact]
	public void HaveNoTransportAdapterFoundInTransportRoutingRange()
	{
		DeliveryEventId.NoTransportAdapterFound.ShouldBe(40703);
	}

	[Fact]
	public void HaveTransportRoutingMessageInTransportRoutingRange()
	{
		DeliveryEventId.TransportRoutingMessage.ShouldBe(40704);
	}

	[Fact]
	public void HaveTransportRoutingSuccessInTransportRoutingRange()
	{
		DeliveryEventId.TransportRoutingSuccess.ShouldBe(40705);
	}

	[Fact]
	public void HaveTransportRoutingFailureInTransportRoutingRange()
	{
		DeliveryEventId.TransportRoutingFailure.ShouldBe(40706);
	}

	[Fact]
	public void HaveTransportRoutingErrorInTransportRoutingRange()
	{
		DeliveryEventId.TransportRoutingError.ShouldBe(40707);
	}

	[Fact]
	public void HaveTransportRoutingBatchInTransportRoutingRange()
	{
		DeliveryEventId.TransportRoutingBatch.ShouldBe(40708);
	}

	[Fact]
	public void HaveTransportAdapterAlreadyRegisteredInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterAlreadyRegistered.ShouldBe(40709);
	}

	[Fact]
	public void HaveTransportAdapterRegisteredInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterRegistered.ShouldBe(40710);
	}

	[Fact]
	public void HaveTransportAdapterRegistrationFailedInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterRegistrationFailed.ShouldBe(40711);
	}

	[Fact]
	public void HaveTransportAdapterUnregisterAttemptInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterUnregisterAttempt.ShouldBe(40712);
	}

	[Fact]
	public void HaveTransportAdapterUnregisteredInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterUnregistered.ShouldBe(40713);
	}

	[Fact]
	public void HaveTransportAdapterUnregistrationFailedInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterUnregistrationFailed.ShouldBe(40714);
	}

	[Fact]
	public void HaveTransportAdapterHealthCheckInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterHealthCheck.ShouldBe(40715);
	}

	[Fact]
	public void HaveTransportAdapterHealthCheckFailedInTransportRoutingRange()
	{
		DeliveryEventId.TransportAdapterHealthCheckFailed.ShouldBe(40716);
	}

	[Fact]
	public void HaveTransportMessageKindNotAcceptedInTransportRoutingRange()
	{
		DeliveryEventId.TransportMessageKindNotAccepted.ShouldBe(40717);
	}

	#endregion Transport Routing Event IDs (40700-40799)

	#region In-Memory Transport Event IDs (40800-40899)

	[Fact]
	public void HaveInMemoryTransportStartedInInMemoryRange()
	{
		DeliveryEventId.InMemoryTransportStarted.ShouldBe(40800);
	}

	[Fact]
	public void HaveInMemoryMessagePublishedInInMemoryRange()
	{
		DeliveryEventId.InMemoryMessagePublished.ShouldBe(40801);
	}

	[Fact]
	public void HaveInMemoryMessageReceivedInInMemoryRange()
	{
		DeliveryEventId.InMemoryMessageReceived.ShouldBe(40802);
	}

	[Fact]
	public void HaveInMemoryTransportStoppingInInMemoryRange()
	{
		DeliveryEventId.InMemoryTransportStopping.ShouldBe(40803);
	}

	[Fact]
	public void HaveInMemoryProcessingFailedInInMemoryRange()
	{
		DeliveryEventId.InMemoryProcessingFailed.ShouldBe(40804);
	}

	#endregion In-Memory Transport Event IDs (40800-40899)

	#region Cron Timer Transport Event IDs (40900-40999)

	[Fact]
	public void HaveCronTimerTransportStartedInCronTimerRange()
	{
		DeliveryEventId.CronTimerTransportStarted.ShouldBe(40900);
	}

	[Fact]
	public void HaveCronTimerFiredInCronTimerRange()
	{
		DeliveryEventId.CronTimerFired.ShouldBe(40901);
	}

	[Fact]
	public void HaveCronTimerTransportStoppedInCronTimerRange()
	{
		DeliveryEventId.CronTimerTransportStopped.ShouldBe(40902);
	}

	[Fact]
	public void HaveCronTimerNextOccurrenceInCronTimerRange()
	{
		DeliveryEventId.CronTimerNextOccurrence.ShouldBe(40903);
	}

	[Fact]
	public void HaveCronTimerExecutionFailedInCronTimerRange()
	{
		DeliveryEventId.CronTimerExecutionFailed.ShouldBe(40904);
	}

	[Fact]
	public void HaveCronTimerSkippingOverlapInCronTimerRange()
	{
		DeliveryEventId.CronTimerSkippingOverlap.ShouldBe(40905);
	}

	[Fact]
	public void HaveCronTimerSendNotSupportedInCronTimerRange()
	{
		DeliveryEventId.CronTimerSendNotSupported.ShouldBe(40906);
	}

	#endregion Cron Timer Transport Event IDs (40900-40999)

	#region Event ID Range Validation

	[Fact]
	public void HaveAllOutboxBackgroundEventIdsInExpectedRange()
	{
		DeliveryEventId.OutboxServiceStarting.ShouldBeInRange(40000, 40099);
		DeliveryEventId.OutboxServiceStopping.ShouldBeInRange(40000, 40099);
		DeliveryEventId.OutboxCleanupError.ShouldBeInRange(40000, 40099);
	}

	[Fact]
	public void HaveAllFinalDispatchEventIdsInExpectedRange()
	{
		DeliveryEventId.FinalDispatchExecuting.ShouldBeInRange(40100, 40199);
		DeliveryEventId.FinalDispatchCompleted.ShouldBeInRange(40100, 40199);
		DeliveryEventId.FinalDispatchCacheHitCheck.ShouldBeInRange(40100, 40199);
	}

	[Fact]
	public void HaveAllPipelineEvaluationEventIdsInExpectedRange()
	{
		DeliveryEventId.NoApplicableMiddleware.ShouldBeInRange(40200, 40299);
		DeliveryEventId.ExecutingMiddleware.ShouldBeInRange(40200, 40299);
		DeliveryEventId.NoHandlersForEvent.ShouldBeInRange(40200, 40299);
	}

	[Fact]
	public void HaveAllEventStoreEventIdsInExpectedRange()
	{
		DeliveryEventId.EventStoreServiceStarted.ShouldBeInRange(40300, 40399);
		DeliveryEventId.EventStoreErrorProcessing.ShouldBeInRange(40300, 40399);
		DeliveryEventId.EventStoreDispatcherInitialized.ShouldBeInRange(40300, 40399);
	}

	[Fact]
	public void HaveAllDeduplicationEventIdsInExpectedRange()
	{
		DeliveryEventId.DeduplicatorInitialized.ShouldBeInRange(40400, 40499);
		DeliveryEventId.DuplicateDetected.ShouldBeInRange(40400, 40499);
		DeliveryEventId.ScheduledCleanupError.ShouldBeInRange(40400, 40499);
	}

	[Fact]
	public void HaveAllSchedulingEventIdsInExpectedRange()
	{
		DeliveryEventId.ScheduledServiceStarting.ShouldBeInRange(40500, 40599);
		DeliveryEventId.MessageScheduled.ShouldBeInRange(40500, 40599);
		DeliveryEventId.ScheduledUnknownDispatchType.ShouldBeInRange(40500, 40599);
	}

	[Fact]
	public void HaveAllCronSchedulingEventIdsInExpectedRange()
	{
		DeliveryEventId.CronSchedulerStarted.ShouldBeInRange(40600, 40699);
		DeliveryEventId.CronJobTriggered.ShouldBeInRange(40600, 40699);
		DeliveryEventId.CronDstTransitionError.ShouldBeInRange(40600, 40699);
	}

	[Fact]
	public void HaveAllTransportRoutingEventIdsInExpectedRange()
	{
		DeliveryEventId.TransportAdapterRouterStarted.ShouldBeInRange(40700, 40799);
		DeliveryEventId.TransportRouteResolved.ShouldBeInRange(40700, 40799);
		DeliveryEventId.TransportMessageKindNotAccepted.ShouldBeInRange(40700, 40799);
	}

	[Fact]
	public void HaveAllInMemoryEventIdsInExpectedRange()
	{
		DeliveryEventId.InMemoryTransportStarted.ShouldBeInRange(40800, 40899);
		DeliveryEventId.InMemoryMessagePublished.ShouldBeInRange(40800, 40899);
		DeliveryEventId.InMemoryProcessingFailed.ShouldBeInRange(40800, 40899);
	}

	[Fact]
	public void HaveAllCronTimerEventIdsInExpectedRange()
	{
		DeliveryEventId.CronTimerTransportStarted.ShouldBeInRange(40900, 40999);
		DeliveryEventId.CronTimerFired.ShouldBeInRange(40900, 40999);
		DeliveryEventId.CronTimerSendNotSupported.ShouldBeInRange(40900, 40999);
	}

	#endregion Event ID Range Validation

	#region Event ID Uniqueness

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllDeliveryEventIds();

		allEventIds.Distinct().Count().ShouldBe(allEventIds.Length,
			"All DeliveryEventId constants should have unique values");
	}

	[Fact]
	public void HaveCorrectTotalNumberOfEventIds()
	{
		var allEventIds = GetAllDeliveryEventIds();

		// DeliveryEventId has a large number of event IDs
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion Event ID Uniqueness

	#region Helper Methods

	private static int[] GetAllDeliveryEventIds()
	{
		return
		[
			// Outbox Background Service
			DeliveryEventId.OutboxServiceStarting,
			DeliveryEventId.OutboxServiceStopping,
			DeliveryEventId.OutboxStoreNotRegistered,
			DeliveryEventId.OutboxProcessingError,
			DeliveryEventId.NoUnsentMessages,
			DeliveryEventId.FoundUnsentMessages,
			DeliveryEventId.MessageNotReady,
			DeliveryEventId.MessageNotEligibleForRetry,
			DeliveryEventId.FailedToPublishOutboxMessage,
			DeliveryEventId.OutboxProcessingCompleted,
			DeliveryEventId.OutboxMessageProcessingError,
			DeliveryEventId.PublishingOutboxMessage,
			DeliveryEventId.MessagePublishedSuccessfully,
			DeliveryEventId.FailedToPublishSingleMessage,
			DeliveryEventId.StartingOutboxCleanup,
			DeliveryEventId.OutboxCleanupCompleted,
			DeliveryEventId.OutboxCleanupError,

			// Final Dispatch Handler
			DeliveryEventId.FinalDispatchExecuting,
			DeliveryEventId.FinalDispatchCompleted,
			DeliveryEventId.FinalDispatchFailed,
			DeliveryEventId.FinalDispatchNoBusFound,
			DeliveryEventId.FinalDispatchCacheHitCheck,

			// Pipeline Evaluation
			DeliveryEventId.NoApplicableMiddleware,
			DeliveryEventId.ExecutingMiddleware,
			DeliveryEventId.MiddlewareApplicable,
			DeliveryEventId.MiddlewareNotApplicable,
			DeliveryEventId.ApplicabilityEvaluationError,
			DeliveryEventId.MiddlewareExcluded,
			DeliveryEventId.MiddlewareRequiresFeature,
			DeliveryEventId.NoHandlersForEvent,

			// EventStore Dispatch
			DeliveryEventId.EventStoreServiceStarted,
			DeliveryEventId.EventStoreErrorProcessing,
			DeliveryEventId.EventStoreServiceStopped,
			DeliveryEventId.EventStoreDispatcherInitialized,

			// Deduplication
			DeliveryEventId.DeduplicatorInitialized,
			DeliveryEventId.DuplicateDetected,
			DeliveryEventId.ExpiredEntryRemoved,
			DeliveryEventId.MessageMarkedProcessed,
			DeliveryEventId.CleanedUpExpiredEntries,
			DeliveryEventId.ClearedEntries,
			DeliveryEventId.DeduplicatorDisposed,
			DeliveryEventId.ScheduledCleanupRemoved,
			DeliveryEventId.DeduplicatorStats,
			DeliveryEventId.ScheduledCleanupError,

			// Poison Message Handling
			DeliveryEventId.PoisonMessageDetected,
			DeliveryEventId.PoisonDetectorError,
			DeliveryEventId.PoisonCleanupStarting,
			DeliveryEventId.PoisonCleanupStopping,
			DeliveryEventId.PoisonCleanupError,
			DeliveryEventId.PoisonCleanupCompleted,
			DeliveryEventId.PoisonCleanupStats,
			DeliveryEventId.PoisonCleanupArchiveError,
			DeliveryEventId.PoisonCleanupReprocessError,
			DeliveryEventId.PoisonCleanupNoStore,
			DeliveryEventId.PoisonCleanupCycleError,
			DeliveryEventId.PoisonHandlerDetected,
			DeliveryEventId.PoisonHandlerStoreError,
			DeliveryEventId.PoisonHandlerNoStore,
			DeliveryEventId.PoisonHandlerDeadLetterError,
			DeliveryEventId.PoisonHandlerNotificationError,
			DeliveryEventId.PoisonHandlerStored,
			DeliveryEventId.PoisonHandlerAlreadyDeadLetter,
			DeliveryEventId.PoisonHandlerProcessingError,
			DeliveryEventId.PoisonMiddlewareDetected,
			DeliveryEventId.PoisonMiddlewareHandlerError,
			DeliveryEventId.DeadLetterMessageAdded,
			DeliveryEventId.DeadLetterMessageRemoved,
			DeliveryEventId.DeadLetterMessageRetrieved,
			DeliveryEventId.DeadLetterStoreStats,
			DeliveryEventId.PoisonCleanupDisabled,
			DeliveryEventId.PoisonAlertThresholdExceeded,
			DeliveryEventId.PoisonAlertTopMessageType,
			DeliveryEventId.PoisonAlertTopReason,
			DeliveryEventId.PoisonAlertCheckError,
			DeliveryEventId.PoisonReplayNotFound,
			DeliveryEventId.PoisonReplayTypeNotFound,
			DeliveryEventId.PoisonReplayNotDispatchMessage,
			DeliveryEventId.PoisonReplaySuccess,
			DeliveryEventId.PoisonReplayFailed,
			DeliveryEventId.PoisonReplayError,
			DeliveryEventId.DeadLetterMessageReplayed,
			DeliveryEventId.DeadLetterCleanupCompleted,

			// Scheduling Core
			DeliveryEventId.ScheduledServiceStarting,
			DeliveryEventId.ScheduledServiceStopping,
			DeliveryEventId.MessageScheduled,
			DeliveryEventId.ScheduledMessageDelivered,
			DeliveryEventId.ScheduledMessageCancelled,
			DeliveryEventId.ScheduledUnknownMessageType,
			DeliveryEventId.ScheduledDeserializationFailed,
			DeliveryEventId.ScheduledProcessingError,
			DeliveryEventId.ScheduledDisabled,
			DeliveryEventId.ScheduledMissedExecutions,
			DeliveryEventId.ScheduledUnknownBehavior,
			DeliveryEventId.ScheduledUnsupportedMessageType,
			DeliveryEventId.ScheduledNextExecution,
			DeliveryEventId.ScheduledTimezoneLookupFailed,
			DeliveryEventId.ScheduledRecurringWithInterval,
			DeliveryEventId.ScheduledServiceStopped,
			DeliveryEventId.ScheduledMessageProcessed,
			DeliveryEventId.ScheduledTimeoutDuringProcessing,
			DeliveryEventId.ScheduledTimeoutProcessingMessage,
			DeliveryEventId.ScheduledErrorProcessingMessage,
			DeliveryEventId.ScheduledUnknownDispatchType,

			// Cron Scheduling
			DeliveryEventId.CronSchedulerStarted,
			DeliveryEventId.CronSchedulerStopped,
			DeliveryEventId.CronJobRegistered,
			DeliveryEventId.CronJobTriggered,
			DeliveryEventId.RecurringDispatchScheduled,
			DeliveryEventId.CronExpressionParsed,
			DeliveryEventId.CronExpressionParseFailed,
			DeliveryEventId.CronDstAdjustment,
			DeliveryEventId.CronDstTransitionError,

			// Transport Routing
			DeliveryEventId.TransportAdapterRouterStarted,
			DeliveryEventId.TransportRouteResolved,
			DeliveryEventId.TransportRouterExecuting,
			DeliveryEventId.NoTransportAdapterFound,
			DeliveryEventId.TransportRoutingMessage,
			DeliveryEventId.TransportRoutingSuccess,
			DeliveryEventId.TransportRoutingFailure,
			DeliveryEventId.TransportRoutingError,
			DeliveryEventId.TransportRoutingBatch,
			DeliveryEventId.TransportAdapterAlreadyRegistered,
			DeliveryEventId.TransportAdapterRegistered,
			DeliveryEventId.TransportAdapterRegistrationFailed,
			DeliveryEventId.TransportAdapterUnregisterAttempt,
			DeliveryEventId.TransportAdapterUnregistered,
			DeliveryEventId.TransportAdapterUnregistrationFailed,
			DeliveryEventId.TransportAdapterHealthCheck,
			DeliveryEventId.TransportAdapterHealthCheckFailed,
			DeliveryEventId.TransportMessageKindNotAccepted,

			// In-Memory Transport
			DeliveryEventId.InMemoryTransportStarted,
			DeliveryEventId.InMemoryMessagePublished,
			DeliveryEventId.InMemoryMessageReceived,
			DeliveryEventId.InMemoryTransportStopping,
			DeliveryEventId.InMemoryProcessingFailed,

			// Cron Timer Transport
			DeliveryEventId.CronTimerTransportStarted,
			DeliveryEventId.CronTimerFired,
			DeliveryEventId.CronTimerTransportStopped,
			DeliveryEventId.CronTimerNextOccurrence,
			DeliveryEventId.CronTimerExecutionFailed,
			DeliveryEventId.CronTimerSkippingOverlap,
			DeliveryEventId.CronTimerSendNotSupported
		];
	}

	#endregion Helper Methods
}
