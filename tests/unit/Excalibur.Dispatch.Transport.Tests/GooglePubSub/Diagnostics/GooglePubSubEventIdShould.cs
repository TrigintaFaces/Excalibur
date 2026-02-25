// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Diagnostics;

/// <summary>
/// Unit tests for <see cref="GooglePubSubEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.GooglePubSub")]
[Trait("Priority", "0")]
public sealed class GooglePubSubEventIdShould : UnitTestBase
{
	#region Core Event ID Tests (23000-23099)

	[Fact]
	public void HaveMessageBusInitializingInCoreRange()
	{
		GooglePubSubEventId.MessageBusInitializing.ShouldBe(23000);
	}

	[Fact]
	public void HaveMessageBusStartingInCoreRange()
	{
		GooglePubSubEventId.MessageBusStarting.ShouldBe(23001);
	}

	[Fact]
	public void HaveMessageBusStoppingInCoreRange()
	{
		GooglePubSubEventId.MessageBusStopping.ShouldBe(23002);
	}

	[Fact]
	public void HavePublisherCreatedInCoreRange()
	{
		GooglePubSubEventId.PublisherCreated.ShouldBe(23003);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		GooglePubSubEventId.MessageBusInitializing.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.MessageBusStarting.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.MessageBusStopping.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.PublisherCreated.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.SubscriberCreated.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TopicCreated.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.SubscriptionCreated.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.MessagePublished.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.SentAction.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.PublishedEvent.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.SentDocument.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.MessageBrokerDisposing.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.MessageBrokerDisposed.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterStarting.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterStopping.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterReceivingMessage.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterSendingMessage.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterMessageProcessingFailed.ShouldBeInRange(23000, 23099);
		GooglePubSubEventId.TransportAdapterSendFailed.ShouldBeInRange(23000, 23099);
	}

	#endregion

	#region Channel Receiver Event ID Tests (23100-23199)

	[Fact]
	public void HaveChannelReceiverStartingInChannelRange()
	{
		GooglePubSubEventId.ChannelReceiverStarting.ShouldBe(23100);
	}

	[Fact]
	public void HaveChannelReceiverStoppingInChannelRange()
	{
		GooglePubSubEventId.ChannelReceiverStopping.ShouldBe(23101);
	}

	[Fact]
	public void HaveMessageReceivedInChannelRange()
	{
		GooglePubSubEventId.MessageReceived.ShouldBe(23102);
	}

	[Fact]
	public void HaveAllChannelReceiverEventIdsInExpectedRange()
	{
		GooglePubSubEventId.ChannelReceiverStarting.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.ChannelReceiverStopping.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.MessageReceived.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.MessageAcknowledged.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.MessageNacked.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.ConsumptionStarted.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.BatchProduced.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.MessageConversionError.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.MessagesAcknowledged.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.AcknowledgmentError.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.DeadLetterPublishError.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.AckDeadlineExtended.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.AckDeadlineExtensionFailed.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.StreamingPullConnectionStarted.ShouldBeInRange(23100, 23199);
		GooglePubSubEventId.StreamingPullReconnecting.ShouldBeInRange(23100, 23199);
	}

	#endregion

	#region Streaming Pull Event ID Tests (23200-23299)

	[Fact]
	public void HaveStreamingPullStartedInStreamingRange()
	{
		GooglePubSubEventId.StreamingPullStarted.ShouldBe(23200);
	}

	[Fact]
	public void HaveStreamingPullStoppedInStreamingRange()
	{
		GooglePubSubEventId.StreamingPullStopped.ShouldBe(23201);
	}

	[Fact]
	public void HaveAllStreamingPullEventIdsInExpectedRange()
	{
		GooglePubSubEventId.StreamingPullStarted.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamingPullStopped.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamHealthCheck.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamHealthDegraded.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamReconnecting.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamError.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamConnected.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamDisconnected.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.StreamIdle.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.HighErrorRate.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.HighAckFailureRate.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.UnhealthyStreamsFound.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.HealthCheckError.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.TaskCleanupFailed.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.TaskCleanupCancelled.ShouldBeInRange(23200, 23299);
		GooglePubSubEventId.TaskCleanupDisposed.ShouldBeInRange(23200, 23299);
	}

	#endregion

	#region Ordering Event ID Tests (23300-23399)

	[Fact]
	public void HaveOrderingKeyAssignedInOrderingRange()
	{
		GooglePubSubEventId.OrderingKeyAssigned.ShouldBe(23300);
	}

	[Fact]
	public void HaveOrderingKeyProcessedInOrderingRange()
	{
		GooglePubSubEventId.OrderingKeyProcessed.ShouldBe(23301);
	}

	[Fact]
	public void HaveAllOrderingEventIdsInExpectedRange()
	{
		GooglePubSubEventId.OrderingKeyAssigned.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingKeyProcessed.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingEnabled.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OutOfOrderDetected.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingProcessorStarted.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingProcessorShutdown.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingProcessorShutdownTimeout.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingWorkerStarted.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingWorkerStopped.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingWorkerError.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingMessageProcessingError.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.UnorderedMessageError.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingQueueRemoved.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingManagerInitialized.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OutOfSequenceMessage.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingKeyFailed.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingKeyReset.ShouldBeInRange(23300, 23399);
		GooglePubSubEventId.OrderingKeyCleanupCompleted.ShouldBeInRange(23300, 23399);
	}

	#endregion

	#region Batch Receiving Event ID Tests (23400-23499)

	[Fact]
	public void HaveBatchReceiveStartedInBatchRange()
	{
		GooglePubSubEventId.BatchReceiveStarted.ShouldBe(23400);
	}

	[Fact]
	public void HaveBatchReceiveCompletedInBatchRange()
	{
		GooglePubSubEventId.BatchReceiveCompleted.ShouldBe(23401);
	}

	[Fact]
	public void HaveAllBatchReceivingEventIdsInExpectedRange()
	{
		GooglePubSubEventId.BatchReceiveStarted.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.BatchReceiveCompleted.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveBatchingApplied.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.BatchSizeAdjusted.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.FlowControlPreventedReceive.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.BatchAcknowledged.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.BatchAcknowledgmentsFailed.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.BatchAckDeadlineModified.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveFlowControlLimit.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveMemoryPressure.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveBatchResult.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveStrategyReset.ShouldBeInRange(23400, 23499);
		GooglePubSubEventId.AdaptiveBatchSizeAdjusted.ShouldBeInRange(23400, 23499);
	}

	#endregion

	#region Flow Control Event ID Tests (23500-23599)

	[Fact]
	public void HaveFlowControlAppliedInFlowControlRange()
	{
		GooglePubSubEventId.FlowControlApplied.ShouldBe(23500);
	}

	[Fact]
	public void HaveFlowControlReleasedInFlowControlRange()
	{
		GooglePubSubEventId.FlowControlReleased.ShouldBe(23501);
	}

	[Fact]
	public void HaveAllFlowControlEventIdsInExpectedRange()
	{
		GooglePubSubEventId.FlowControlApplied.ShouldBeInRange(23500, 23599);
		GooglePubSubEventId.FlowControlReleased.ShouldBeInRange(23500, 23599);
		GooglePubSubEventId.SubscriberFactoryCreated.ShouldBeInRange(23500, 23599);
		GooglePubSubEventId.OutstandingMessagesLimit.ShouldBeInRange(23500, 23599);
		GooglePubSubEventId.FlowControlledSubscriberCreated.ShouldBeInRange(23500, 23599);
		GooglePubSubEventId.SubscriberMessageProcessingError.ShouldBeInRange(23500, 23599);
	}

	#endregion

	#region Parallel Processing Event ID Tests (23600-23699)

	[Fact]
	public void HaveParallelProcessingStartedInParallelRange()
	{
		GooglePubSubEventId.ParallelProcessingStarted.ShouldBe(23600);
	}

	[Fact]
	public void HaveParallelProcessingCompletedInParallelRange()
	{
		GooglePubSubEventId.ParallelProcessingCompleted.ShouldBe(23601);
	}

	[Fact]
	public void HaveAllParallelProcessingEventIdsInExpectedRange()
	{
		GooglePubSubEventId.ParallelProcessingStarted.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelProcessingCompleted.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.WorkerThreadStarted.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.WorkerThreadStopped.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelProcessorStarted.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelProcessorShutdown.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelProcessorShutdownTimeout.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelWorkerStarted.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelWorkerStopped.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelWorkerError.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelMessageProcessingError.ShouldBeInRange(23600, 23699);
		GooglePubSubEventId.ParallelOrderingKeyAssigned.ShouldBeInRange(23600, 23699);
	}

	#endregion

	#region Dead Letter Event ID Tests (23700-23799)

	[Fact]
	public void HaveMovedToDeadLetterInDeadLetterRange()
	{
		GooglePubSubEventId.MovedToDeadLetter.ShouldBe(23700);
	}

	[Fact]
	public void HaveDeadLetterProcessedInDeadLetterRange()
	{
		GooglePubSubEventId.DeadLetterProcessed.ShouldBe(23701);
	}

	[Fact]
	public void HaveAllDeadLetterEventIdsInExpectedRange()
	{
		GooglePubSubEventId.MovedToDeadLetter.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterProcessed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.RetryPolicyApplied.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.MaxDeliveryAttemptsReached.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterPolicyConfigured.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterSubscriptionNotFound.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.MessageMovedToDeadLetter.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.ExceptionCausedDeadLettering.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterParseFailed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterMessagesRetrieved.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterReprocessFailed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterMessagesReprocessed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterMetadataDeserializeFailed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.DeadLetterMessageReprocessed.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.RetryAttemptLogged.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.RetryAdaptedLowSuccessRate.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.RetryAdaptedHighSuccessRate.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.RetryWarning.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.CircuitBreakerOpened.ShouldBeInRange(23700, 23799);
		GooglePubSubEventId.CircuitBreakerReset.ShouldBeInRange(23700, 23799);
	}

	#endregion

	#region Error Handling Event ID Tests (23800-23899)

	[Fact]
	public void HavePublisherErrorInErrorRange()
	{
		GooglePubSubEventId.PublisherError.ShouldBe(23800);
	}

	[Fact]
	public void HaveSubscriberErrorInErrorRange()
	{
		GooglePubSubEventId.SubscriberError.ShouldBe(23801);
	}

	[Fact]
	public void HaveAllErrorEventIdsInExpectedRange()
	{
		GooglePubSubEventId.PublisherError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.DeserializationError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.ConnectionError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberReceiveFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberAcknowledgeFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberAcknowledgeBatchFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberModifyVisibilityFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberConsumeLoopError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberNotFound.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberChannelReaderError.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.SubscriberDisposed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.HealthCheckFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.DestinationValidationFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.PublisherPublishFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.PublisherBatchPublishFailed.ShouldBeInRange(23800, 23899);
		GooglePubSubEventId.PublisherChannelError.ShouldBeInRange(23800, 23899);
	}

	#endregion

	#region Google Pub/Sub Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInGooglePubSubReservedRange()
	{
		// Google Pub/Sub reserved range is 23000-23999
		var allEventIds = GetAllGooglePubSubEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(23000, 23999,
				$"Event ID {eventId} is outside Google Pub/Sub reserved range (23000-23999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllGooglePubSubEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllGooglePubSubEventIds();
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllGooglePubSubEventIds()
	{
		return
		[
			// Core (23000-23099)
			GooglePubSubEventId.MessageBusInitializing,
			GooglePubSubEventId.MessageBusStarting,
			GooglePubSubEventId.MessageBusStopping,
			GooglePubSubEventId.PublisherCreated,
			GooglePubSubEventId.SubscriberCreated,
			GooglePubSubEventId.TopicCreated,
			GooglePubSubEventId.SubscriptionCreated,
			GooglePubSubEventId.MessagePublished,
			GooglePubSubEventId.SentAction,
			GooglePubSubEventId.PublishedEvent,
			GooglePubSubEventId.SentDocument,
			GooglePubSubEventId.MessageBrokerDisposing,
			GooglePubSubEventId.MessageBrokerDisposed,
			GooglePubSubEventId.TransportAdapterStarting,
			GooglePubSubEventId.TransportAdapterStopping,
			GooglePubSubEventId.TransportAdapterReceivingMessage,
			GooglePubSubEventId.TransportAdapterSendingMessage,
			GooglePubSubEventId.TransportAdapterMessageProcessingFailed,
			GooglePubSubEventId.TransportAdapterSendFailed,

			// Channel Receiver (23100-23199)
			GooglePubSubEventId.ChannelReceiverStarting,
			GooglePubSubEventId.ChannelReceiverStopping,
			GooglePubSubEventId.MessageReceived,
			GooglePubSubEventId.MessageAcknowledged,
			GooglePubSubEventId.MessageNacked,
			GooglePubSubEventId.ConsumptionStarted,
			GooglePubSubEventId.BatchProduced,
			GooglePubSubEventId.MessageConversionError,
			GooglePubSubEventId.MessagesAcknowledged,
			GooglePubSubEventId.AcknowledgmentError,
			GooglePubSubEventId.DeadLetterPublishError,
			GooglePubSubEventId.AckDeadlineExtended,
			GooglePubSubEventId.AckDeadlineExtensionFailed,
			GooglePubSubEventId.StreamingPullConnectionStarted,
			GooglePubSubEventId.StreamingPullReconnecting,

			// Streaming Pull (23200-23299)
			GooglePubSubEventId.StreamingPullStarted,
			GooglePubSubEventId.StreamingPullStopped,
			GooglePubSubEventId.StreamHealthCheck,
			GooglePubSubEventId.StreamHealthDegraded,
			GooglePubSubEventId.StreamReconnecting,
			GooglePubSubEventId.StreamError,
			GooglePubSubEventId.StreamConnected,
			GooglePubSubEventId.StreamDisconnected,
			GooglePubSubEventId.StreamIdle,
			GooglePubSubEventId.HighErrorRate,
			GooglePubSubEventId.HighAckFailureRate,
			GooglePubSubEventId.UnhealthyStreamsFound,
			GooglePubSubEventId.HealthCheckError,
			GooglePubSubEventId.TaskCleanupFailed,
			GooglePubSubEventId.TaskCleanupCancelled,
			GooglePubSubEventId.TaskCleanupDisposed,

			// Ordering (23300-23399)
			GooglePubSubEventId.OrderingKeyAssigned,
			GooglePubSubEventId.OrderingKeyProcessed,
			GooglePubSubEventId.OrderingEnabled,
			GooglePubSubEventId.OutOfOrderDetected,
			GooglePubSubEventId.OrderingProcessorStarted,
			GooglePubSubEventId.OrderingProcessorShutdown,
			GooglePubSubEventId.OrderingProcessorShutdownTimeout,
			GooglePubSubEventId.OrderingWorkerStarted,
			GooglePubSubEventId.OrderingWorkerStopped,
			GooglePubSubEventId.OrderingWorkerError,
			GooglePubSubEventId.OrderingMessageProcessingError,
			GooglePubSubEventId.UnorderedMessageError,
			GooglePubSubEventId.OrderingQueueRemoved,
			GooglePubSubEventId.OrderingManagerInitialized,
			GooglePubSubEventId.OutOfSequenceMessage,
			GooglePubSubEventId.OrderingKeyFailed,
			GooglePubSubEventId.OrderingKeyReset,
			GooglePubSubEventId.OrderingKeyCleanupCompleted,

			// Batch Receiving (23400-23499)
			GooglePubSubEventId.BatchReceiveStarted,
			GooglePubSubEventId.BatchReceiveCompleted,
			GooglePubSubEventId.AdaptiveBatchingApplied,
			GooglePubSubEventId.BatchSizeAdjusted,
			GooglePubSubEventId.FlowControlPreventedReceive,
			GooglePubSubEventId.BatchAcknowledged,
			GooglePubSubEventId.BatchAcknowledgmentsFailed,
			GooglePubSubEventId.BatchAckDeadlineModified,
			GooglePubSubEventId.AdaptiveFlowControlLimit,
			GooglePubSubEventId.AdaptiveMemoryPressure,
			GooglePubSubEventId.AdaptiveBatchResult,
			GooglePubSubEventId.AdaptiveStrategyReset,
			GooglePubSubEventId.AdaptiveBatchSizeAdjusted,

			// Flow Control (23500-23599)
			GooglePubSubEventId.FlowControlApplied,
			GooglePubSubEventId.FlowControlReleased,
			GooglePubSubEventId.SubscriberFactoryCreated,
			GooglePubSubEventId.OutstandingMessagesLimit,
			GooglePubSubEventId.FlowControlledSubscriberCreated,
			GooglePubSubEventId.SubscriberMessageProcessingError,

			// Parallel Processing (23600-23699)
			GooglePubSubEventId.ParallelProcessingStarted,
			GooglePubSubEventId.ParallelProcessingCompleted,
			GooglePubSubEventId.WorkerThreadStarted,
			GooglePubSubEventId.WorkerThreadStopped,
			GooglePubSubEventId.ParallelProcessorStarted,
			GooglePubSubEventId.ParallelProcessorShutdown,
			GooglePubSubEventId.ParallelProcessorShutdownTimeout,
			GooglePubSubEventId.ParallelWorkerStarted,
			GooglePubSubEventId.ParallelWorkerStopped,
			GooglePubSubEventId.ParallelWorkerError,
			GooglePubSubEventId.ParallelMessageProcessingError,
			GooglePubSubEventId.ParallelOrderingKeyAssigned,

			// Dead Letter (23700-23799)
			GooglePubSubEventId.MovedToDeadLetter,
			GooglePubSubEventId.DeadLetterProcessed,
			GooglePubSubEventId.RetryPolicyApplied,
			GooglePubSubEventId.MaxDeliveryAttemptsReached,
			GooglePubSubEventId.DeadLetterPolicyConfigured,
			GooglePubSubEventId.DeadLetterSubscriptionNotFound,
			GooglePubSubEventId.MessageMovedToDeadLetter,
			GooglePubSubEventId.ExceptionCausedDeadLettering,
			GooglePubSubEventId.DeadLetterParseFailed,
			GooglePubSubEventId.DeadLetterMessagesRetrieved,
			GooglePubSubEventId.DeadLetterReprocessFailed,
			GooglePubSubEventId.DeadLetterMessagesReprocessed,
			GooglePubSubEventId.DeadLetterMetadataDeserializeFailed,
			GooglePubSubEventId.DeadLetterMessageReprocessed,
			GooglePubSubEventId.RetryAttemptLogged,
			GooglePubSubEventId.RetryAdaptedLowSuccessRate,
			GooglePubSubEventId.RetryAdaptedHighSuccessRate,
			GooglePubSubEventId.RetryWarning,
			GooglePubSubEventId.CircuitBreakerOpened,
			GooglePubSubEventId.CircuitBreakerReset,

			// Error Handling (23800-23899)
			GooglePubSubEventId.PublisherError,
			GooglePubSubEventId.SubscriberError,
			GooglePubSubEventId.DeserializationError,
			GooglePubSubEventId.ConnectionError,
			GooglePubSubEventId.SubscriberReceiveFailed,
			GooglePubSubEventId.SubscriberAcknowledgeFailed,
			GooglePubSubEventId.SubscriberAcknowledgeBatchFailed,
			GooglePubSubEventId.SubscriberModifyVisibilityFailed,
			GooglePubSubEventId.SubscriberConsumeLoopError,
			GooglePubSubEventId.SubscriberNotFound,
			GooglePubSubEventId.SubscriberChannelReaderError,
			GooglePubSubEventId.SubscriberDisposed,
			GooglePubSubEventId.HealthCheckFailed,
			GooglePubSubEventId.DestinationValidationFailed,
			GooglePubSubEventId.PublisherPublishFailed,
			GooglePubSubEventId.PublisherBatchPublishFailed,
			GooglePubSubEventId.PublisherChannelError
		];
	}

	#endregion
}
