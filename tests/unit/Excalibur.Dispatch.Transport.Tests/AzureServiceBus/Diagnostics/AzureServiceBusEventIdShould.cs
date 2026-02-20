// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AzureServiceBus;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AzureServiceBusEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.AzureServiceBus")]
[Trait("Priority", "0")]
public sealed class AzureServiceBusEventIdShould : UnitTestBase
{
	#region ServiceBus Core Event ID Tests (24000-24099)

	[Fact]
	public void HaveServiceBusInitializingInCoreRange()
	{
		AzureServiceBusEventId.ServiceBusInitializing.ShouldBe(24000);
	}

	[Fact]
	public void HaveServiceBusStartingInCoreRange()
	{
		AzureServiceBusEventId.ServiceBusStarting.ShouldBe(24001);
	}

	[Fact]
	public void HaveServiceBusStoppingInCoreRange()
	{
		AzureServiceBusEventId.ServiceBusStopping.ShouldBe(24002);
	}

	[Fact]
	public void HaveMessageBrokerCreatedInCoreRange()
	{
		AzureServiceBusEventId.MessageBrokerCreated.ShouldBe(24003);
	}

	[Fact]
	public void HaveAllServiceBusCoreEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.ServiceBusInitializing.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ServiceBusStarting.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ServiceBusStopping.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.MessageBrokerCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ClientCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.CachedPublisher.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.PublisherCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.CachedConsumer.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ConsumerCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.BrokerDisposing.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.QueueCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.TopicCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.SubscriptionCreated.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.SubscriptionCreating.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.SubscriptionDeleting.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.SubscriptionDeleted.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.SubscriptionNotFound.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.HealthCheckFailed.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.DestinationValidationFailed.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.BrokerDisposed.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.InitializationFailed.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.InvalidDestination.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.DestinationValidationFailedWithException.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ActionSent.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.EventSent.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.DocumentSent.ShouldBeInRange(24000, 24099);
		AzureServiceBusEventId.ServiceBusError.ShouldBeInRange(24000, 24099);
	}

	#endregion

	#region ServiceBus Transport Event ID Tests (24100-24199)

	[Fact]
	public void HaveTransportAdapterInitializedInTransportRange()
	{
		AzureServiceBusEventId.TransportAdapterInitialized.ShouldBe(24100);
	}

	[Fact]
	public void HaveTransportAdapterDisposedInTransportRange()
	{
		AzureServiceBusEventId.TransportAdapterDisposed.ShouldBe(24101);
	}

	[Fact]
	public void HaveAllServiceBusTransportEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.TransportAdapterInitialized.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportAdapterDisposed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.EventSourceCreated.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.ConnectionEstablished.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportAdapterStarting.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportAdapterStopping.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportReceivingMessage.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportSendingMessage.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportMessageProcessingFailed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.TransportSendFailed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MessageProcessed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MessageAbandonedWithError.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.ProcessingError.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.BatchProduced.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.BatchProcessingCompleted.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.ProcessorStarted.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.ProcessorStopped.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.ProcessorError.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MessageProcessingFailed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.SessionAccepted.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.SessionReleased.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.SessionStateUpdated.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.PrefetchCountAdjusted.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MaxConcurrentCallsAdjusted.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MessageLockRenewed.ShouldBeInRange(24100, 24199);
		AzureServiceBusEventId.MessageLockRenewalFailed.ShouldBeInRange(24100, 24199);
	}

	#endregion

	#region ServiceBus Publisher Event ID Tests (24200-24299)

	[Fact]
	public void HaveMessagePublishedInPublisherRange()
	{
		AzureServiceBusEventId.MessagePublished.ShouldBe(24200);
	}

	[Fact]
	public void HaveMessageScheduledInPublisherRange()
	{
		AzureServiceBusEventId.MessageScheduled.ShouldBe(24201);
	}

	[Fact]
	public void HaveAllServiceBusPublisherEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.MessagePublished.ShouldBeInRange(24200, 24299);
		AzureServiceBusEventId.MessageScheduled.ShouldBeInRange(24200, 24299);
		AzureServiceBusEventId.MessageCancelled.ShouldBeInRange(24200, 24299);
		AzureServiceBusEventId.BatchPublished.ShouldBeInRange(24200, 24299);
		AzureServiceBusEventId.PublishError.ShouldBeInRange(24200, 24299);
		AzureServiceBusEventId.BatchPublishError.ShouldBeInRange(24200, 24299);
	}

	#endregion

	#region ServiceBus Consumer Event ID Tests (24300-24399)

	[Fact]
	public void HaveConsumerStartedInConsumerRange()
	{
		AzureServiceBusEventId.ConsumerStarted.ShouldBe(24300);
	}

	[Fact]
	public void HaveConsumerStoppedInConsumerRange()
	{
		AzureServiceBusEventId.ConsumerStopped.ShouldBe(24301);
	}

	[Fact]
	public void HaveAllServiceBusConsumerEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.ConsumerStarted.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.ConsumerStopped.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageReceived.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageCompleted.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageAbandoned.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageDeadLettered.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.ChannelReceiverStarting.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.ChannelReceiverStopping.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageAcknowledged.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.MessageRejected.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.VisibilityModified.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.ReceiveError.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.AcknowledgeError.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionMessageReceived.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionMessageAcknowledged.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionMessageRejected.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionVisibilityModified.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionReceiveError.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionAcknowledgeError.ShouldBeInRange(24300, 24399);
		AzureServiceBusEventId.SessionLockLost.ShouldBeInRange(24300, 24399);
	}

	#endregion

	#region EventHubs Core Event ID Tests (24400-24499)

	[Fact]
	public void HaveEventHubsInitializingInEventHubsCoreRange()
	{
		AzureServiceBusEventId.EventHubsInitializing.ShouldBe(24400);
	}

	[Fact]
	public void HaveEventHubsStartingInEventHubsCoreRange()
	{
		AzureServiceBusEventId.EventHubsStarting.ShouldBe(24401);
	}

	[Fact]
	public void HaveAllEventHubsCoreEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.EventHubsInitializing.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsStarting.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsStopping.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsBrokerCreated.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubCreated.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.ConsumerGroupCreated.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsCachedPublisher.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsPublisherCreated.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsCachedConsumer.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsConsumerCreated.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsInvalidDestination.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsHealthCheckFailed.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsDestinationValidationFailed.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsDestinationValidationFailedWithException.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsBrokerDisposing.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsBrokerDisposed.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsActionSent.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsEventSent.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsDocumentSent.ShouldBeInRange(24400, 24499);
		AzureServiceBusEventId.EventHubsError.ShouldBeInRange(24400, 24499);
	}

	#endregion

	#region EventHubs Transport Event ID Tests (24500-24599)

	[Fact]
	public void HaveEventHubsTransportInitializedInTransportRange()
	{
		AzureServiceBusEventId.EventHubsTransportInitialized.ShouldBe(24500);
	}

	[Fact]
	public void HaveEventHubsTransportDisposedInTransportRange()
	{
		AzureServiceBusEventId.EventHubsTransportDisposed.ShouldBe(24501);
	}

	[Fact]
	public void HaveAllEventHubsTransportEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.EventHubsTransportInitialized.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportDisposed.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.PartitionAssigned.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.PartitionLost.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportStarting.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportStopping.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportReceivingMessage.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportSendingMessage.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportMessageProcessingFailed.ShouldBeInRange(24500, 24599);
		AzureServiceBusEventId.EventHubsTransportSendFailed.ShouldBeInRange(24500, 24599);
	}

	#endregion

	#region EventHubs Pub/Sub Event ID Tests (24600-24699)

	[Fact]
	public void HaveEventHubsEventPublishedInPubSubRange()
	{
		AzureServiceBusEventId.EventHubsEventPublished.ShouldBe(24600);
	}

	[Fact]
	public void HaveEventHubsEventReceivedInPubSubRange()
	{
		AzureServiceBusEventId.EventHubsEventReceived.ShouldBe(24601);
	}

	[Fact]
	public void HaveAllEventHubsPubSubEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.EventHubsEventPublished.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsEventReceived.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsBatchPublished.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.CheckpointCreated.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsPublishingMessage.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsMessagePublished.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsPublishingBatch.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsBatchPublishedSummary.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsChannelPublishFailed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsChannelCancelled.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsChannelFailed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsPublishingWithPartition.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsProcessingStarted.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsProcessingStopped.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsEventReceivedDetailed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsEventProcessed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsEventProcessingFailed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsCheckpointFailed.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsMessageAcknowledged.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsBatchAcknowledged.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsMessageRejected.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsVisibilityNotSupported.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsAlreadyStarted.ShouldBeInRange(24600, 24699);
		AzureServiceBusEventId.EventHubsCloudEventParsed.ShouldBeInRange(24600, 24699);
	}

	#endregion

	#region StorageQueues Core Event ID Tests (24700-24799)

	[Fact]
	public void HaveStorageQueueInitializingInStorageQueuesCoreRange()
	{
		AzureServiceBusEventId.StorageQueueInitializing.ShouldBe(24700);
	}

	[Fact]
	public void HaveStorageQueueStartingInStorageQueuesCoreRange()
	{
		AzureServiceBusEventId.StorageQueueStarting.ShouldBe(24701);
	}

	[Fact]
	public void HaveAllStorageQueuesCoreEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.StorageQueueInitializing.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueStarting.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueStopping.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueCreated.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueCachedPublisher.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueuePublisherCreated.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueCachedConsumer.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueConsumerCreated.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueInvalidDestination.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueHealthCheckFailed.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueDestinationValidationFailed.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueDestinationValidationFailedWithException.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueBrokerDisposing.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueBrokerDisposed.ShouldBeInRange(24700, 24799);
		AzureServiceBusEventId.StorageQueueError.ShouldBeInRange(24700, 24799);
	}

	#endregion

	#region StorageQueues Transport Event ID Tests (24800-24899)

	[Fact]
	public void HaveStorageQueueTransportInitializedInTransportRange()
	{
		AzureServiceBusEventId.StorageQueueTransportInitialized.ShouldBe(24800);
	}

	[Fact]
	public void HaveStorageQueueTransportDisposedInTransportRange()
	{
		AzureServiceBusEventId.StorageQueueTransportDisposed.ShouldBe(24801);
	}

	[Fact]
	public void HaveAllStorageQueuesTransportEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.StorageQueueTransportInitialized.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportDisposed.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueMessagePublished.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportStarting.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportStopping.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportReceivingMessage.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportSendingMessage.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportMessageProcessingFailed.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueTransportSendFailed.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueuePublishingMessage.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueuePublishingBatch.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueBatchPublishedSummary.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueChannelCancelled.ShouldBeInRange(24800, 24899);
		AzureServiceBusEventId.StorageQueueChannelFailed.ShouldBeInRange(24800, 24899);
	}

	#endregion

	#region StorageQueues Consumer Event ID Tests (24900-24999)

	[Fact]
	public void HaveStorageQueueConsumerStartedInConsumerRange()
	{
		AzureServiceBusEventId.StorageQueueConsumerStarted.ShouldBe(24900);
	}

	[Fact]
	public void HaveStorageQueueConsumerStoppedInConsumerRange()
	{
		AzureServiceBusEventId.StorageQueueConsumerStopped.ShouldBe(24901);
	}

	[Fact]
	public void HaveAllStorageQueuesConsumerEventIdsInExpectedRange()
	{
		AzureServiceBusEventId.StorageQueueConsumerStarted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueConsumerStopped.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageReceived.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageDeleted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.MessageProcessorStarted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.QueueMetricsCollected.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageAcknowledged.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueBatchAcknowledged.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageRejected.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueVisibilityExtended.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueVisibilityExtensionFailed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueAlreadyStarted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueStartingProcessing.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueStoppingProcessing.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessagesReceived.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueNoMessages.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueReceiveFailed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageProcessingStarted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageProcessed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageProcessingFailed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueBatchCompleted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueHealthChecked.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueIterationCompleted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueCloudEventParsed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueEnvelopeParsed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMessageConverted.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueInvalidMessageType.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueDeadLettered.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueRejectedNotDeadLettered.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsMessageProcessed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsBatchProcessed.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsReceiveOperation.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsDeleteOperation.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsVisibilityUpdate.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsHealthRecorded.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsDisposing.ShouldBeInRange(24900, 24999);
		AzureServiceBusEventId.StorageQueueMetricsDisposalError.ShouldBeInRange(24900, 24999);
	}

	#endregion

	#region Azure Service Bus Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInAzureServiceBusReservedRange()
	{
		// Azure Service Bus reserved range is 24000-24999
		var allEventIds = GetAllAzureServiceBusEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(24000, 24999,
				$"Event ID {eventId} is outside Azure Service Bus reserved range (24000-24999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAzureServiceBusEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAzureServiceBusEventIds();
		allEventIds.Length.ShouldBeGreaterThan(150);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAzureServiceBusEventIds()
	{
		return
		[
			// ServiceBus Core (24000-24099)
			AzureServiceBusEventId.ServiceBusInitializing,
			AzureServiceBusEventId.ServiceBusStarting,
			AzureServiceBusEventId.ServiceBusStopping,
			AzureServiceBusEventId.MessageBrokerCreated,
			AzureServiceBusEventId.ClientCreated,
			AzureServiceBusEventId.CachedPublisher,
			AzureServiceBusEventId.PublisherCreated,
			AzureServiceBusEventId.CachedConsumer,
			AzureServiceBusEventId.ConsumerCreated,
			AzureServiceBusEventId.BrokerDisposing,
			AzureServiceBusEventId.QueueCreated,
			AzureServiceBusEventId.TopicCreated,
			AzureServiceBusEventId.SubscriptionCreated,
			AzureServiceBusEventId.SubscriptionCreating,
			AzureServiceBusEventId.SubscriptionDeleting,
			AzureServiceBusEventId.SubscriptionDeleted,
			AzureServiceBusEventId.SubscriptionNotFound,
			AzureServiceBusEventId.HealthCheckFailed,
			AzureServiceBusEventId.DestinationValidationFailed,
			AzureServiceBusEventId.BrokerDisposed,
			AzureServiceBusEventId.InitializationFailed,
			AzureServiceBusEventId.InvalidDestination,
			AzureServiceBusEventId.DestinationValidationFailedWithException,
			AzureServiceBusEventId.ActionSent,
			AzureServiceBusEventId.EventSent,
			AzureServiceBusEventId.DocumentSent,
			AzureServiceBusEventId.ServiceBusError,

			// ServiceBus Transport (24100-24199)
			AzureServiceBusEventId.TransportAdapterInitialized,
			AzureServiceBusEventId.TransportAdapterDisposed,
			AzureServiceBusEventId.EventSourceCreated,
			AzureServiceBusEventId.ConnectionEstablished,
			AzureServiceBusEventId.TransportAdapterStarting,
			AzureServiceBusEventId.TransportAdapterStopping,
			AzureServiceBusEventId.TransportReceivingMessage,
			AzureServiceBusEventId.TransportSendingMessage,
			AzureServiceBusEventId.TransportMessageProcessingFailed,
			AzureServiceBusEventId.TransportSendFailed,
			AzureServiceBusEventId.MessageProcessed,
			AzureServiceBusEventId.MessageAbandonedWithError,
			AzureServiceBusEventId.ProcessingError,
			AzureServiceBusEventId.BatchProduced,
			AzureServiceBusEventId.BatchProcessingCompleted,
			AzureServiceBusEventId.ProcessorStarted,
			AzureServiceBusEventId.ProcessorStopped,
			AzureServiceBusEventId.ProcessorError,
			AzureServiceBusEventId.MessageProcessingFailed,
			AzureServiceBusEventId.SessionAccepted,
			AzureServiceBusEventId.SessionReleased,
			AzureServiceBusEventId.SessionStateUpdated,
			AzureServiceBusEventId.PrefetchCountAdjusted,
			AzureServiceBusEventId.MaxConcurrentCallsAdjusted,
			AzureServiceBusEventId.MessageLockRenewed,
			AzureServiceBusEventId.MessageLockRenewalFailed,

			// ServiceBus Publisher (24200-24299)
			AzureServiceBusEventId.MessagePublished,
			AzureServiceBusEventId.MessageScheduled,
			AzureServiceBusEventId.MessageCancelled,
			AzureServiceBusEventId.BatchPublished,
			AzureServiceBusEventId.PublishError,
			AzureServiceBusEventId.BatchPublishError,

			// ServiceBus Consumer (24300-24399)
			AzureServiceBusEventId.ConsumerStarted,
			AzureServiceBusEventId.ConsumerStopped,
			AzureServiceBusEventId.MessageReceived,
			AzureServiceBusEventId.MessageCompleted,
			AzureServiceBusEventId.MessageAbandoned,
			AzureServiceBusEventId.MessageDeadLettered,
			AzureServiceBusEventId.ChannelReceiverStarting,
			AzureServiceBusEventId.ChannelReceiverStopping,
			AzureServiceBusEventId.MessageAcknowledged,
			AzureServiceBusEventId.MessageRejected,
			AzureServiceBusEventId.VisibilityModified,
			AzureServiceBusEventId.ReceiveError,
			AzureServiceBusEventId.AcknowledgeError,
			AzureServiceBusEventId.SessionMessageReceived,
			AzureServiceBusEventId.SessionMessageAcknowledged,
			AzureServiceBusEventId.SessionMessageRejected,
			AzureServiceBusEventId.SessionVisibilityModified,
			AzureServiceBusEventId.SessionReceiveError,
			AzureServiceBusEventId.SessionAcknowledgeError,
			AzureServiceBusEventId.SessionLockLost,

			// EventHubs Core (24400-24499)
			AzureServiceBusEventId.EventHubsInitializing,
			AzureServiceBusEventId.EventHubsStarting,
			AzureServiceBusEventId.EventHubsStopping,
			AzureServiceBusEventId.EventHubsBrokerCreated,
			AzureServiceBusEventId.EventHubCreated,
			AzureServiceBusEventId.ConsumerGroupCreated,
			AzureServiceBusEventId.EventHubsCachedPublisher,
			AzureServiceBusEventId.EventHubsPublisherCreated,
			AzureServiceBusEventId.EventHubsCachedConsumer,
			AzureServiceBusEventId.EventHubsConsumerCreated,
			AzureServiceBusEventId.EventHubsInvalidDestination,
			AzureServiceBusEventId.EventHubsHealthCheckFailed,
			AzureServiceBusEventId.EventHubsDestinationValidationFailed,
			AzureServiceBusEventId.EventHubsDestinationValidationFailedWithException,
			AzureServiceBusEventId.EventHubsBrokerDisposing,
			AzureServiceBusEventId.EventHubsBrokerDisposed,
			AzureServiceBusEventId.EventHubsActionSent,
			AzureServiceBusEventId.EventHubsEventSent,
			AzureServiceBusEventId.EventHubsDocumentSent,
			AzureServiceBusEventId.EventHubsError,

			// EventHubs Transport (24500-24599)
			AzureServiceBusEventId.EventHubsTransportInitialized,
			AzureServiceBusEventId.EventHubsTransportDisposed,
			AzureServiceBusEventId.PartitionAssigned,
			AzureServiceBusEventId.PartitionLost,
			AzureServiceBusEventId.EventHubsTransportStarting,
			AzureServiceBusEventId.EventHubsTransportStopping,
			AzureServiceBusEventId.EventHubsTransportReceivingMessage,
			AzureServiceBusEventId.EventHubsTransportSendingMessage,
			AzureServiceBusEventId.EventHubsTransportMessageProcessingFailed,
			AzureServiceBusEventId.EventHubsTransportSendFailed,

			// EventHubs Pub/Sub (24600-24699)
			AzureServiceBusEventId.EventHubsEventPublished,
			AzureServiceBusEventId.EventHubsEventReceived,
			AzureServiceBusEventId.EventHubsBatchPublished,
			AzureServiceBusEventId.CheckpointCreated,
			AzureServiceBusEventId.EventHubsPublishingMessage,
			AzureServiceBusEventId.EventHubsMessagePublished,
			AzureServiceBusEventId.EventHubsPublishingBatch,
			AzureServiceBusEventId.EventHubsBatchPublishedSummary,
			AzureServiceBusEventId.EventHubsChannelPublishFailed,
			AzureServiceBusEventId.EventHubsChannelCancelled,
			AzureServiceBusEventId.EventHubsChannelFailed,
			AzureServiceBusEventId.EventHubsPublishingWithPartition,
			AzureServiceBusEventId.EventHubsProcessingStarted,
			AzureServiceBusEventId.EventHubsProcessingStopped,
			AzureServiceBusEventId.EventHubsEventReceivedDetailed,
			AzureServiceBusEventId.EventHubsEventProcessed,
			AzureServiceBusEventId.EventHubsEventProcessingFailed,
			AzureServiceBusEventId.EventHubsCheckpointFailed,
			AzureServiceBusEventId.EventHubsMessageAcknowledged,
			AzureServiceBusEventId.EventHubsBatchAcknowledged,
			AzureServiceBusEventId.EventHubsMessageRejected,
			AzureServiceBusEventId.EventHubsVisibilityNotSupported,
			AzureServiceBusEventId.EventHubsAlreadyStarted,
			AzureServiceBusEventId.EventHubsCloudEventParsed,

			// StorageQueues Core (24700-24799)
			AzureServiceBusEventId.StorageQueueInitializing,
			AzureServiceBusEventId.StorageQueueStarting,
			AzureServiceBusEventId.StorageQueueStopping,
			AzureServiceBusEventId.StorageQueueCreated,
			AzureServiceBusEventId.StorageQueueCachedPublisher,
			AzureServiceBusEventId.StorageQueuePublisherCreated,
			AzureServiceBusEventId.StorageQueueCachedConsumer,
			AzureServiceBusEventId.StorageQueueConsumerCreated,
			AzureServiceBusEventId.StorageQueueInvalidDestination,
			AzureServiceBusEventId.StorageQueueHealthCheckFailed,
			AzureServiceBusEventId.StorageQueueDestinationValidationFailed,
			AzureServiceBusEventId.StorageQueueDestinationValidationFailedWithException,
			AzureServiceBusEventId.StorageQueueBrokerDisposing,
			AzureServiceBusEventId.StorageQueueBrokerDisposed,
			AzureServiceBusEventId.StorageQueueError,

			// StorageQueues Transport (24800-24899)
			AzureServiceBusEventId.StorageQueueTransportInitialized,
			AzureServiceBusEventId.StorageQueueTransportDisposed,
			AzureServiceBusEventId.StorageQueueMessagePublished,
			AzureServiceBusEventId.StorageQueueTransportStarting,
			AzureServiceBusEventId.StorageQueueTransportStopping,
			AzureServiceBusEventId.StorageQueueTransportReceivingMessage,
			AzureServiceBusEventId.StorageQueueTransportSendingMessage,
			AzureServiceBusEventId.StorageQueueTransportMessageProcessingFailed,
			AzureServiceBusEventId.StorageQueueTransportSendFailed,
			AzureServiceBusEventId.StorageQueuePublishingMessage,
			AzureServiceBusEventId.StorageQueuePublishingBatch,
			AzureServiceBusEventId.StorageQueueBatchPublishedSummary,
			AzureServiceBusEventId.StorageQueueChannelCancelled,
			AzureServiceBusEventId.StorageQueueChannelFailed,

			// StorageQueues Consumer (24900-24999)
			AzureServiceBusEventId.StorageQueueConsumerStarted,
			AzureServiceBusEventId.StorageQueueConsumerStopped,
			AzureServiceBusEventId.StorageQueueMessageReceived,
			AzureServiceBusEventId.StorageQueueMessageDeleted,
			AzureServiceBusEventId.MessageProcessorStarted,
			AzureServiceBusEventId.QueueMetricsCollected,
			AzureServiceBusEventId.StorageQueueMessageAcknowledged,
			AzureServiceBusEventId.StorageQueueBatchAcknowledged,
			AzureServiceBusEventId.StorageQueueMessageRejected,
			AzureServiceBusEventId.StorageQueueVisibilityExtended,
			AzureServiceBusEventId.StorageQueueVisibilityExtensionFailed,
			AzureServiceBusEventId.StorageQueueAlreadyStarted,
			AzureServiceBusEventId.StorageQueueStartingProcessing,
			AzureServiceBusEventId.StorageQueueStoppingProcessing,
			AzureServiceBusEventId.StorageQueueMessagesReceived,
			AzureServiceBusEventId.StorageQueueNoMessages,
			AzureServiceBusEventId.StorageQueueReceiveFailed,
			AzureServiceBusEventId.StorageQueueMessageProcessingStarted,
			AzureServiceBusEventId.StorageQueueMessageProcessed,
			AzureServiceBusEventId.StorageQueueMessageProcessingFailed,
			AzureServiceBusEventId.StorageQueueBatchCompleted,
			AzureServiceBusEventId.StorageQueueHealthChecked,
			AzureServiceBusEventId.StorageQueueIterationCompleted,
			AzureServiceBusEventId.StorageQueueCloudEventParsed,
			AzureServiceBusEventId.StorageQueueEnvelopeParsed,
			AzureServiceBusEventId.StorageQueueMessageConverted,
			AzureServiceBusEventId.StorageQueueInvalidMessageType,
			AzureServiceBusEventId.StorageQueueDeadLettered,
			AzureServiceBusEventId.StorageQueueRejectedNotDeadLettered,
			AzureServiceBusEventId.StorageQueueMetricsMessageProcessed,
			AzureServiceBusEventId.StorageQueueMetricsBatchProcessed,
			AzureServiceBusEventId.StorageQueueMetricsReceiveOperation,
			AzureServiceBusEventId.StorageQueueMetricsDeleteOperation,
			AzureServiceBusEventId.StorageQueueMetricsVisibilityUpdate,
			AzureServiceBusEventId.StorageQueueMetricsHealthRecorded,
			AzureServiceBusEventId.StorageQueueMetricsDisposing,
			AzureServiceBusEventId.StorageQueueMetricsDisposalError
		];
	}

	#endregion
}
