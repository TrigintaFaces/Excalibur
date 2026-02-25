// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AwsSqsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.AwsSqs")]
[Trait("Priority", "0")]
public sealed class AwsSqsEventIdShould : UnitTestBase
{
	#region Core Event ID Tests (25000-25099)

	[Fact]
	public void HaveBrokerInitializingInCoreRange()
	{
		AwsSqsEventId.BrokerInitializing.ShouldBe(25000);
	}

	[Fact]
	public void HaveBrokerStartingInCoreRange()
	{
		AwsSqsEventId.BrokerStarting.ShouldBe(25001);
	}

	[Fact]
	public void HaveBrokerStoppingInCoreRange()
	{
		AwsSqsEventId.BrokerStopping.ShouldBe(25002);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		AwsSqsEventId.BrokerInitializing.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BrokerStarting.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BrokerStopping.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CommonLoggingInitialized.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CredentialsValidated.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.RegionConfigured.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CachedPublisher.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.PublisherCreated.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CachedConsumer.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.ConsumerCreated.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SubscriptionCreating.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SubscriptionCreated.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SubscriptionDeleting.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SubscriptionDeleted.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SubscriptionNotFound.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BrokerHealthCheckFailed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.DestinationValidationFailed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BrokerDisposing.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BrokerDisposed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.ConnectionAcquired.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.ConnectionReleased.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.ConnectionPoolExhausted.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.RetryAttempt.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CircuitBreakerOpened.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.CircuitBreakerClosed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BatchOperationStarted.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.BatchOperationCompleted.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SessionCreated.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.SessionExpired.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.DlqMessageProcessed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.DlqMessageFailed.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.MetricRecorded.ShouldBeInRange(25000, 25099);
		AwsSqsEventId.MetricsBatchPublished.ShouldBeInRange(25000, 25099);
	}

	#endregion

	#region SQS Core Event ID Tests (25100-25199)

	[Fact]
	public void HaveSqsMessageBusInitializingInSqsCoreRange()
	{
		AwsSqsEventId.SqsMessageBusInitializing.ShouldBe(25100);
	}

	[Fact]
	public void HaveSqsMessageBusStartingInSqsCoreRange()
	{
		AwsSqsEventId.SqsMessageBusStarting.ShouldBe(25101);
	}

	[Fact]
	public void HaveAllSqsCoreEventIdsInExpectedRange()
	{
		AwsSqsEventId.SqsMessageBusInitializing.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsMessageBusStarting.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsMessageBusStopping.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsQueueCreated.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsQueueUrlResolved.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsSentAction.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsPublishedEvent.ShouldBeInRange(25100, 25199);
		AwsSqsEventId.SqsSentDocument.ShouldBeInRange(25100, 25199);
	}

	#endregion

	#region SQS Publisher Event ID Tests (25200-25299)

	[Fact]
	public void HaveSqsMessagePublishedInPublisherRange()
	{
		AwsSqsEventId.SqsMessagePublished.ShouldBe(25200);
	}

	[Fact]
	public void HaveSqsBatchPublishedInPublisherRange()
	{
		AwsSqsEventId.SqsBatchPublished.ShouldBe(25201);
	}

	[Fact]
	public void HaveAllSqsPublisherEventIdsInExpectedRange()
	{
		AwsSqsEventId.SqsMessagePublished.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsBatchPublished.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsMessageSent.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsPublishFailed.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsPublisherDisposing.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsChannelMessagePublished.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsChannelPublishError.ShouldBeInRange(25200, 25299);
		AwsSqsEventId.SqsChannelProcessingError.ShouldBeInRange(25200, 25299);
	}

	#endregion

	#region SQS Consumer Event ID Tests (25300-25399)

	[Fact]
	public void HaveSqsConsumerStartedInConsumerRange()
	{
		AwsSqsEventId.SqsConsumerStarted.ShouldBe(25300);
	}

	[Fact]
	public void HaveSqsConsumerStoppedInConsumerRange()
	{
		AwsSqsEventId.SqsConsumerStopped.ShouldBe(25301);
	}

	[Fact]
	public void HaveAllSqsConsumerEventIdsInExpectedRange()
	{
		AwsSqsEventId.SqsConsumerStarted.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsConsumerStopped.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsMessageReceived.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsMessageDeleted.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsVisibilityExtended.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsChannelReceiverStarting.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsChannelReceiverStopping.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsReceiveMessagesFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsAcknowledgeMessageFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsAcknowledgeBatchFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsModifyVisibilityFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsConsumerDisposing.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsMessageProcessingError.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsConsumeLoopError.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsFailedToWriteToChannel.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsChannelReaderMessagePumpError.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsFailedToDeserializeContext.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerMessageReceived.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerMessageProcessed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerMessageDeleted.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerContextDeserializationWarning.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerBatchReceived.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerBatchProcessed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerVisibilityExtended.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerVisibilityExtensionFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerMessageProcessingFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.ConsumerMessageDeletionFailed.ShouldBeInRange(25300, 25399);
		AwsSqsEventId.SqsMessageDecompressionFailed.ShouldBeInRange(25300, 25399);
	}

	#endregion

	#region SQS Channels Event ID Tests (25400-25499)

	[Fact]
	public void HaveChannelAdapterInitializedInChannelRange()
	{
		AwsSqsEventId.ChannelAdapterInitialized.ShouldBe(25400);
	}

	[Fact]
	public void HaveChannelMessageProcessedInChannelRange()
	{
		AwsSqsEventId.ChannelMessageProcessed.ShouldBe(25401);
	}

	[Fact]
	public void HaveAllSqsChannelEventIdsInExpectedRange()
	{
		AwsSqsEventId.ChannelAdapterInitialized.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageProcessed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.BatchProcessorStarted.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.BatchProcessorCompleted.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelBatchProduced.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageAcknowledged.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageRejected.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageEnqueuedForDelete.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelBatchDeleteCompleted.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelBatchDeleteFailed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageConsumed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageFailed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelFailedToDeserializeContext.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelFailedToExecuteBatchDelete.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelAdapterStarting.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelAdapterStopping.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelAdapterStopped.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelPollerStarting.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelPollerError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelPollerStopped.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelSendBatchError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelSendBatchFailed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelMessageBatchSendError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessorStarting.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessorStarted.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessorStopping.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessorStopped.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelWorkerStarting.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelWorkerStopped.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessingError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelDeleteProcessorError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelDeleteBatchError.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelDeleteMessageFailed.ShouldBeInRange(25400, 25499);
		AwsSqsEventId.ChannelProcessingFailed.ShouldBeInRange(25400, 25499);
	}

	#endregion

	#region SQS High Throughput Event ID Tests (25500-25599)

	[Fact]
	public void HaveHighThroughputStartedInHighThroughputRange()
	{
		AwsSqsEventId.HighThroughputStarted.ShouldBe(25500);
	}

	[Fact]
	public void HaveHighThroughputStoppedInHighThroughputRange()
	{
		AwsSqsEventId.HighThroughputStopped.ShouldBe(25501);
	}

	[Fact]
	public void HaveAllHighThroughputEventIdsInExpectedRange()
	{
		AwsSqsEventId.HighThroughputStarted.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputStopped.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HostedServiceStarted.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HostedServiceStopped.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.ThroughputMetrics.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputProcessorStarting.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputPollerStarted.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputPollerError.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputPollerStopped.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HighThroughputBatchDeleteError.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HostedServiceStarting.ShouldBeInRange(25500, 25599);
		AwsSqsEventId.HostedServiceError.ShouldBeInRange(25500, 25599);
	}

	#endregion

	#region Long Polling Event ID Tests (25600-25699)

	[Fact]
	public void HaveLongPollingStartedInLongPollingRange()
	{
		AwsSqsEventId.LongPollingStarted.ShouldBe(25600);
	}

	[Fact]
	public void HaveLongPollingStoppedInLongPollingRange()
	{
		AwsSqsEventId.LongPollingStopped.ShouldBe(25601);
	}

	[Fact]
	public void HaveAllLongPollingEventIdsInExpectedRange()
	{
		AwsSqsEventId.LongPollingStarted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingStopped.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingOptimizerApplied.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.ChannelLongPollingStarted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollTimeout.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollCompleted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiverStarting.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiverStopping.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiverStoppedWithMetrics.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollerStarted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollerError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollerStopped.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollerCountAdjusting.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.AdaptivePollingError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceivingMessages.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceivedMessages.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiveError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingMessageProcessingError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingPollingStarted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingPollingError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingPollingStopped.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingVisibilityTimeoutOptimized.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingVisibilityTimeoutOptimizationFailed.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingDeleteMessageFailed.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingBatchDeleteFailed.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingBatchDeleteError.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiverStarted.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingReceiverStopped.ShouldBeInRange(25600, 25699);
		AwsSqsEventId.LongPollingHealthStatusError.ShouldBeInRange(25600, 25699);
	}

	#endregion

	#region SNS Event ID Tests (25700-25799)

	[Fact]
	public void HaveSnsMessageBusInitializingInSnsRange()
	{
		AwsSqsEventId.SnsMessageBusInitializing.ShouldBe(25700);
	}

	[Fact]
	public void HaveSnsMessageBusStartingInSnsRange()
	{
		AwsSqsEventId.SnsMessageBusStarting.ShouldBe(25701);
	}

	[Fact]
	public void HaveAllSnsEventIdsInExpectedRange()
	{
		AwsSqsEventId.SnsMessageBusInitializing.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsMessageBusStarting.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsMessageBusStopping.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsTopicCreated.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsMessagePublished.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsSubscriptionCreated.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsSentAction.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsPublishedEvent.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsSentDocument.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsPublishFailed.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsChannelMessagePublished.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsChannelPublishError.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsChannelProcessingError.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsScheduledNotSupported.ShouldBeInRange(25700, 25799);
		AwsSqsEventId.SnsPublisherDisposing.ShouldBeInRange(25700, 25799);
	}

	#endregion

	#region EventBridge Event ID Tests (25800-25899)

	[Fact]
	public void HaveEventBridgeInitializingInEventBridgeRange()
	{
		AwsSqsEventId.EventBridgeInitializing.ShouldBe(25800);
	}

	[Fact]
	public void HaveEventBridgeStartingInEventBridgeRange()
	{
		AwsSqsEventId.EventBridgeStarting.ShouldBe(25801);
	}

	[Fact]
	public void HaveAllEventBridgeEventIdsInExpectedRange()
	{
		AwsSqsEventId.EventBridgeInitializing.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgeStarting.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgeStopping.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgeEventPublished.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgeRuleCreated.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgePublishedAction.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgePublishedEvent.ShouldBeInRange(25800, 25899);
		AwsSqsEventId.EventBridgeSentDocument.ShouldBeInRange(25800, 25899);
	}

	#endregion

	#region AWS SQS Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInAwsSqsReservedRange()
	{
		// AWS SQS reserved range is 25000-26999 (extended for Glue and Transport Adapter)
		var allEventIds = GetAllAwsSqsEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(25000, 26999,
				$"Event ID {eventId} is outside AWS SQS reserved range (25000-26999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAwsSqsEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAwsSqsEventIds();
		allEventIds.Length.ShouldBeGreaterThan(200);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAwsSqsEventIds()
	{
		return
		[
			// Core (25000-25099)
			AwsSqsEventId.BrokerInitializing,
			AwsSqsEventId.BrokerStarting,
			AwsSqsEventId.BrokerStopping,
			AwsSqsEventId.CommonLoggingInitialized,
			AwsSqsEventId.CredentialsValidated,
			AwsSqsEventId.RegionConfigured,
			AwsSqsEventId.CachedPublisher,
			AwsSqsEventId.PublisherCreated,
			AwsSqsEventId.CachedConsumer,
			AwsSqsEventId.ConsumerCreated,
			AwsSqsEventId.SubscriptionCreating,
			AwsSqsEventId.SubscriptionCreated,
			AwsSqsEventId.SubscriptionDeleting,
			AwsSqsEventId.SubscriptionDeleted,
			AwsSqsEventId.SubscriptionNotFound,
			AwsSqsEventId.BrokerHealthCheckFailed,
			AwsSqsEventId.DestinationValidationFailed,
			AwsSqsEventId.BrokerDisposing,
			AwsSqsEventId.BrokerDisposed,
			AwsSqsEventId.ConnectionAcquired,
			AwsSqsEventId.ConnectionReleased,
			AwsSqsEventId.ConnectionPoolExhausted,
			AwsSqsEventId.RetryAttempt,
			AwsSqsEventId.CircuitBreakerOpened,
			AwsSqsEventId.CircuitBreakerClosed,
			AwsSqsEventId.BatchOperationStarted,
			AwsSqsEventId.BatchOperationCompleted,
			AwsSqsEventId.SessionCreated,
			AwsSqsEventId.SessionExpired,
			AwsSqsEventId.DlqMessageProcessed,
			AwsSqsEventId.DlqMessageFailed,
			AwsSqsEventId.MetricRecorded,
			AwsSqsEventId.MetricsBatchPublished,

			// SQS Core (25100-25199)
			AwsSqsEventId.SqsMessageBusInitializing,
			AwsSqsEventId.SqsMessageBusStarting,
			AwsSqsEventId.SqsMessageBusStopping,
			AwsSqsEventId.SqsQueueCreated,
			AwsSqsEventId.SqsQueueUrlResolved,
			AwsSqsEventId.SqsSentAction,
			AwsSqsEventId.SqsPublishedEvent,
			AwsSqsEventId.SqsSentDocument,

			// SQS Publisher (25200-25299)
			AwsSqsEventId.SqsMessagePublished,
			AwsSqsEventId.SqsBatchPublished,
			AwsSqsEventId.SqsMessageSent,
			AwsSqsEventId.SqsPublishFailed,
			AwsSqsEventId.SqsPublisherDisposing,
			AwsSqsEventId.SqsChannelMessagePublished,
			AwsSqsEventId.SqsChannelPublishError,
			AwsSqsEventId.SqsChannelProcessingError,

			// SQS Consumer (25300-25399)
			AwsSqsEventId.SqsConsumerStarted,
			AwsSqsEventId.SqsConsumerStopped,
			AwsSqsEventId.SqsMessageReceived,
			AwsSqsEventId.SqsMessageDeleted,
			AwsSqsEventId.SqsVisibilityExtended,
			AwsSqsEventId.SqsChannelReceiverStarting,
			AwsSqsEventId.SqsChannelReceiverStopping,
			AwsSqsEventId.SqsReceiveMessagesFailed,
			AwsSqsEventId.SqsAcknowledgeMessageFailed,
			AwsSqsEventId.SqsAcknowledgeBatchFailed,
			AwsSqsEventId.SqsModifyVisibilityFailed,
			AwsSqsEventId.SqsConsumerDisposing,
			AwsSqsEventId.SqsMessageProcessingError,
			AwsSqsEventId.SqsConsumeLoopError,
			AwsSqsEventId.SqsFailedToWriteToChannel,
			AwsSqsEventId.SqsChannelReaderMessagePumpError,
			AwsSqsEventId.SqsFailedToDeserializeContext,
			AwsSqsEventId.ConsumerMessageReceived,
			AwsSqsEventId.ConsumerMessageProcessed,
			AwsSqsEventId.ConsumerMessageDeleted,
			AwsSqsEventId.ConsumerContextDeserializationWarning,
			AwsSqsEventId.ConsumerBatchReceived,
			AwsSqsEventId.ConsumerBatchProcessed,
			AwsSqsEventId.ConsumerVisibilityExtended,
			AwsSqsEventId.ConsumerVisibilityExtensionFailed,
			AwsSqsEventId.ConsumerMessageProcessingFailed,
			AwsSqsEventId.ConsumerMessageDeletionFailed,
			AwsSqsEventId.SqsMessageDecompressionFailed,

			// SQS Channels (25400-25499)
			AwsSqsEventId.ChannelAdapterInitialized,
			AwsSqsEventId.ChannelMessageProcessed,
			AwsSqsEventId.BatchProcessorStarted,
			AwsSqsEventId.BatchProcessorCompleted,
			AwsSqsEventId.ChannelBatchProduced,
			AwsSqsEventId.ChannelMessageAcknowledged,
			AwsSqsEventId.ChannelMessageRejected,
			AwsSqsEventId.ChannelMessageEnqueuedForDelete,
			AwsSqsEventId.ChannelBatchDeleteCompleted,
			AwsSqsEventId.ChannelBatchDeleteFailed,
			AwsSqsEventId.ChannelMessageConsumed,
			AwsSqsEventId.ChannelMessageFailed,
			AwsSqsEventId.ChannelFailedToDeserializeContext,
			AwsSqsEventId.ChannelFailedToExecuteBatchDelete,
			AwsSqsEventId.ChannelAdapterStarting,
			AwsSqsEventId.ChannelAdapterStopping,
			AwsSqsEventId.ChannelAdapterStopped,
			AwsSqsEventId.ChannelPollerStarting,
			AwsSqsEventId.ChannelPollerError,
			AwsSqsEventId.ChannelPollerStopped,
			AwsSqsEventId.ChannelSendBatchError,
			AwsSqsEventId.ChannelSendBatchFailed,
			AwsSqsEventId.ChannelMessageBatchSendError,
			AwsSqsEventId.ChannelProcessorStarting,
			AwsSqsEventId.ChannelProcessorStarted,
			AwsSqsEventId.ChannelProcessorStopping,
			AwsSqsEventId.ChannelProcessorStopped,
			AwsSqsEventId.ChannelWorkerStarting,
			AwsSqsEventId.ChannelWorkerStopped,
			AwsSqsEventId.ChannelProcessingError,
			AwsSqsEventId.ChannelDeleteProcessorError,
			AwsSqsEventId.ChannelDeleteBatchError,
			AwsSqsEventId.ChannelDeleteMessageFailed,
			AwsSqsEventId.ChannelProcessingFailed,
			AwsSqsEventId.BatchProcessorMessageError,
			AwsSqsEventId.BatchProcessorSendFailure,
			AwsSqsEventId.BatchProcessorSendFlushError,
			AwsSqsEventId.BatchProcessorDeleteFailure,
			AwsSqsEventId.BatchProcessorProcessed,
			AwsSqsEventId.BatchProcessorSent,
			AwsSqsEventId.BatchProcessorDeleted,
			AwsSqsEventId.BatchProcessorAccumulating,
			AwsSqsEventId.BatchProcessorFlushTriggered,

			// SQS High Throughput (25500-25599)
			AwsSqsEventId.HighThroughputStarted,
			AwsSqsEventId.HighThroughputStopped,
			AwsSqsEventId.HostedServiceStarted,
			AwsSqsEventId.HostedServiceStopped,
			AwsSqsEventId.ThroughputMetrics,
			AwsSqsEventId.HighThroughputProcessorStarting,
			AwsSqsEventId.HighThroughputPollerStarted,
			AwsSqsEventId.HighThroughputPollerError,
			AwsSqsEventId.HighThroughputPollerStopped,
			AwsSqsEventId.HighThroughputBatchDeleteError,
			AwsSqsEventId.HostedServiceStarting,
			AwsSqsEventId.HostedServiceError,

			// Long Polling (25600-25699)
			AwsSqsEventId.LongPollingStarted,
			AwsSqsEventId.LongPollingStopped,
			AwsSqsEventId.LongPollingOptimizerApplied,
			AwsSqsEventId.ChannelLongPollingStarted,
			AwsSqsEventId.LongPollTimeout,
			AwsSqsEventId.LongPollCompleted,
			AwsSqsEventId.LongPollingReceiverStarting,
			AwsSqsEventId.LongPollingReceiverStopping,
			AwsSqsEventId.LongPollingReceiverStoppedWithMetrics,
			AwsSqsEventId.LongPollerStarted,
			AwsSqsEventId.LongPollerError,
			AwsSqsEventId.LongPollerStopped,
			AwsSqsEventId.LongPollerCountAdjusting,
			AwsSqsEventId.AdaptivePollingError,
			AwsSqsEventId.LongPollingReceivingMessages,
			AwsSqsEventId.LongPollingReceivedMessages,
			AwsSqsEventId.LongPollingReceiveError,
			AwsSqsEventId.LongPollingMessageProcessingError,
			AwsSqsEventId.LongPollingPollingStarted,
			AwsSqsEventId.LongPollingPollingError,
			AwsSqsEventId.LongPollingPollingStopped,
			AwsSqsEventId.LongPollingVisibilityTimeoutOptimized,
			AwsSqsEventId.LongPollingVisibilityTimeoutOptimizationFailed,
			AwsSqsEventId.LongPollingDeleteMessageFailed,
			AwsSqsEventId.LongPollingBatchDeleteFailed,
			AwsSqsEventId.LongPollingBatchDeleteError,
			AwsSqsEventId.LongPollingReceiverStarted,
			AwsSqsEventId.LongPollingReceiverStopped,
			AwsSqsEventId.LongPollingHealthStatusError,

			// SNS (25700-25799)
			AwsSqsEventId.SnsMessageBusInitializing,
			AwsSqsEventId.SnsMessageBusStarting,
			AwsSqsEventId.SnsMessageBusStopping,
			AwsSqsEventId.SnsTopicCreated,
			AwsSqsEventId.SnsMessagePublished,
			AwsSqsEventId.SnsSubscriptionCreated,
			AwsSqsEventId.SnsSentAction,
			AwsSqsEventId.SnsPublishedEvent,
			AwsSqsEventId.SnsSentDocument,
			AwsSqsEventId.SnsPublishFailed,
			AwsSqsEventId.SnsChannelMessagePublished,
			AwsSqsEventId.SnsChannelPublishError,
			AwsSqsEventId.SnsChannelProcessingError,
			AwsSqsEventId.SnsScheduledNotSupported,
			AwsSqsEventId.SnsPublisherDisposing,

			// EventBridge (25800-25899)
			AwsSqsEventId.EventBridgeInitializing,
			AwsSqsEventId.EventBridgeStarting,
			AwsSqsEventId.EventBridgeStopping,
			AwsSqsEventId.EventBridgeEventPublished,
			AwsSqsEventId.EventBridgeRuleCreated,
			AwsSqsEventId.EventBridgePublishedAction,
			AwsSqsEventId.EventBridgePublishedEvent,
			AwsSqsEventId.EventBridgeSentDocument,

			// Transport Adapter (26010-26099)
			AwsSqsEventId.TransportAdapterStarting,
			AwsSqsEventId.TransportAdapterStopping,
			AwsSqsEventId.TransportAdapterReceivingMessage,
			AwsSqsEventId.TransportAdapterSendingMessage,
			AwsSqsEventId.TransportAdapterMessageProcessingFailed,
			AwsSqsEventId.TransportAdapterSendFailed,
			AwsSqsEventId.TransportAdapterInitialized,
			AwsSqsEventId.TransportAdapterDisposed,

			// ITransportSender / ITransportReceiver (26100-26117)
			AwsSqsEventId.TransportSenderMessageSent,
			AwsSqsEventId.TransportSenderSendFailed,
			AwsSqsEventId.TransportSenderBatchSent,
			AwsSqsEventId.TransportSenderBatchSendFailed,
			AwsSqsEventId.TransportSenderDisposed,
			AwsSqsEventId.TransportReceiverMessageReceived,
			AwsSqsEventId.TransportReceiverReceiveError,
			AwsSqsEventId.TransportReceiverMessageAcknowledged,
			AwsSqsEventId.TransportReceiverAcknowledgeError,
			AwsSqsEventId.TransportReceiverMessageRejected,
			AwsSqsEventId.TransportReceiverMessageRejectedRequeue,
			AwsSqsEventId.TransportReceiverRejectError,
			AwsSqsEventId.TransportReceiverDisposed,

			// ITransportSubscriber (26120-26127)
			AwsSqsEventId.TransportSubscriberStarted,
			AwsSqsEventId.TransportSubscriberMessageReceived,
			AwsSqsEventId.TransportSubscriberMessageAcknowledged,
			AwsSqsEventId.TransportSubscriberMessageRejected,
			AwsSqsEventId.TransportSubscriberMessageRequeued,
			AwsSqsEventId.TransportSubscriberError,
			AwsSqsEventId.TransportSubscriberStopped,
			AwsSqsEventId.TransportSubscriberDisposed,

			// Missing from previous ranges
			AwsSqsEventId.SqsBatchPartialFailure,
			AwsSqsEventId.SqsEventSourceSubscribed,
		];
	}

	#endregion
}
