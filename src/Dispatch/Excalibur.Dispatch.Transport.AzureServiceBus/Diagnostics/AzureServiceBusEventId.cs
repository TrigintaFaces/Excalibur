// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Event IDs for Azure Service Bus transport (24000-24999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>24000-24099: ServiceBus Core</item>
/// <item>24100-24199: ServiceBus Transport</item>
/// <item>24200-24299: ServiceBus Publisher</item>
/// <item>24300-24399: ServiceBus Consumer</item>
/// <item>24400-24499: EventHubs Core</item>
/// <item>24500-24599: EventHubs Transport</item>
/// <item>24600-24699: EventHubs Pub/Sub</item>
/// <item>24700-24799: StorageQueues Core</item>
/// <item>24800-24899: StorageQueues Transport</item>
/// <item>24900-24999: StorageQueues Consumer</item>
/// </list>
/// </remarks>
public static class AzureServiceBusEventId
{
	// ========================================
	// 24000-24099: ServiceBus Core
	// ========================================

	/// <summary>Azure Service Bus message bus initializing.</summary>
	public const int ServiceBusInitializing = 24000;

	/// <summary>Azure Service Bus message bus starting.</summary>
	public const int ServiceBusStarting = 24001;

	/// <summary>Azure Service Bus message bus stopping.</summary>
	public const int ServiceBusStopping = 24002;

	/// <summary>Azure Service Bus message broker created.</summary>
	public const int MessageBrokerCreated = 24003;

	/// <summary>Azure Service Bus client created.</summary>
	public const int ClientCreated = 24004;

	/// <summary>Azure Service Bus cached publisher returned.</summary>
	public const int CachedPublisher = 24005;

	/// <summary>Azure Service Bus publisher created.</summary>
	public const int PublisherCreated = 24006;

	/// <summary>Azure Service Bus cached consumer returned.</summary>
	public const int CachedConsumer = 24007;

	/// <summary>Azure Service Bus consumer created.</summary>
	public const int ConsumerCreated = 24008;

	/// <summary>Azure Service Bus broker disposing.</summary>
	public const int BrokerDisposing = 24009;

	/// <summary>Azure Service Bus queue created.</summary>
	public const int QueueCreated = 24010;

	/// <summary>Azure Service Bus topic created.</summary>
	public const int TopicCreated = 24011;

	/// <summary>Azure Service Bus subscription created.</summary>
	public const int SubscriptionCreated = 24012;

	/// <summary>Azure Service Bus subscription creating.</summary>
	public const int SubscriptionCreating = 24013;

	/// <summary>Azure Service Bus subscription deleting.</summary>
	public const int SubscriptionDeleting = 24014;

	/// <summary>Azure Service Bus subscription deleted.</summary>
	public const int SubscriptionDeleted = 24015;

	/// <summary>Azure Service Bus subscription not found.</summary>
	public const int SubscriptionNotFound = 24016;

	/// <summary>Azure Service Bus health check failed.</summary>
	public const int HealthCheckFailed = 24017;

	/// <summary>Azure Service Bus destination validation failed.</summary>
	public const int DestinationValidationFailed = 24018;

	/// <summary>Azure Service Bus destination validation failed with exception.</summary>
	public const int DestinationValidationFailedWithException = 24022;

	/// <summary>Azure Service Bus broker disposed.</summary>
	public const int BrokerDisposed = 24019;

	/// <summary>Azure Service Bus initialization failed.</summary>
	public const int InitializationFailed = 24020;

	/// <summary>Azure Service Bus invalid destination.</summary>
	public const int InvalidDestination = 24021;

	/// <summary>Azure Service Bus action sent via message bus.</summary>
	public const int ActionSent = 24023;

	/// <summary>Azure Service Bus event sent via message bus.</summary>
	public const int EventSent = 24024;

	/// <summary>Azure Service Bus document sent via message bus.</summary>
	public const int DocumentSent = 24025;

	// ========================================
	// 24100-24199: ServiceBus Transport
	// ========================================

	/// <summary>Service Bus transport adapter initialized.</summary>
	public const int TransportAdapterInitialized = 24100;

	/// <summary>Service Bus transport adapter disposed.</summary>
	public const int TransportAdapterDisposed = 24101;

	/// <summary>Service Bus event source created.</summary>
	public const int EventSourceCreated = 24102;

	/// <summary>Service Bus connection established.</summary>
	public const int ConnectionEstablished = 24103;

	/// <summary>Service Bus transport adapter starting.</summary>
	public const int TransportAdapterStarting = 24104;

	/// <summary>Service Bus transport adapter stopping.</summary>
	public const int TransportAdapterStopping = 24105;

	/// <summary>Service Bus transport adapter receiving message.</summary>
	public const int TransportReceivingMessage = 24106;

	/// <summary>Service Bus transport adapter sending message.</summary>
	public const int TransportSendingMessage = 24107;

	/// <summary>Service Bus transport adapter message processing failed.</summary>
	public const int TransportMessageProcessingFailed = 24108;

	/// <summary>Service Bus transport adapter send failed.</summary>
	public const int TransportSendFailed = 24109;

	/// <summary>Service Bus message processed and completed.</summary>
	public const int MessageProcessed = 24110;

	/// <summary>Service Bus message abandoned due to error.</summary>
	public const int MessageAbandonedWithError = 24111;

	/// <summary>Service Bus processing error.</summary>
	public const int ProcessingError = 24112;

	/// <summary>Service Bus batch produced.</summary>
	public const int BatchProduced = 24113;

	/// <summary>Service Bus batch processing completed.</summary>
	public const int BatchProcessingCompleted = 24114;

	/// <summary>Service Bus processor started.</summary>
	public const int ProcessorStarted = 24115;

	/// <summary>Service Bus processor stopped.</summary>
	public const int ProcessorStopped = 24116;

	/// <summary>Service Bus processor error.</summary>
	public const int ProcessorError = 24117;

	/// <summary>Service Bus message processing failed.</summary>
	public const int MessageProcessingFailed = 24118;

	/// <summary>Service Bus session accepted.</summary>
	public const int SessionAccepted = 24119;

	/// <summary>Service Bus session released.</summary>
	public const int SessionReleased = 24120;

	/// <summary>Service Bus session state updated.</summary>
	public const int SessionStateUpdated = 24121;

	/// <summary>Service Bus prefetch count adjusted.</summary>
	public const int PrefetchCountAdjusted = 24122;

	/// <summary>Service Bus max concurrent calls adjusted.</summary>
	public const int MaxConcurrentCallsAdjusted = 24123;

	/// <summary>Service Bus message lock renewed.</summary>
	public const int MessageLockRenewed = 24124;

	/// <summary>Service Bus message lock renewal failed.</summary>
	public const int MessageLockRenewalFailed = 24125;

	// ========================================
	// 24200-24299: ServiceBus Publisher
	// ========================================

	/// <summary>Service Bus message published.</summary>
	public const int MessagePublished = 24200;

	/// <summary>Service Bus message scheduled.</summary>
	public const int MessageScheduled = 24201;

	/// <summary>Service Bus message cancelled.</summary>
	public const int MessageCancelled = 24202;

	/// <summary>Service Bus batch published.</summary>
	public const int BatchPublished = 24203;

	/// <summary>Service Bus publish error.</summary>
	public const int PublishError = 24204;

	/// <summary>Service Bus batch publish error.</summary>
	public const int BatchPublishError = 24205;

	// ========================================
	// 24300-24399: ServiceBus Consumer
	// ========================================

	/// <summary>Service Bus consumer started.</summary>
	public const int ConsumerStarted = 24300;

	/// <summary>Service Bus consumer stopped.</summary>
	public const int ConsumerStopped = 24301;

	/// <summary>Service Bus message received.</summary>
	public const int MessageReceived = 24302;

	/// <summary>Service Bus message completed.</summary>
	public const int MessageCompleted = 24303;

	/// <summary>Service Bus message abandoned.</summary>
	public const int MessageAbandoned = 24304;

	/// <summary>Service Bus message dead-lettered.</summary>
	public const int MessageDeadLettered = 24305;

	/// <summary>Service Bus channel receiver starting.</summary>
	public const int ChannelReceiverStarting = 24306;

	/// <summary>Service Bus channel receiver stopping.</summary>
	public const int ChannelReceiverStopping = 24307;

	/// <summary>Service Bus message acknowledged.</summary>
	public const int MessageAcknowledged = 24308;

	/// <summary>Service Bus message rejected.</summary>
	public const int MessageRejected = 24309;

	/// <summary>Service Bus message visibility modified.</summary>
	public const int VisibilityModified = 24310;

	/// <summary>Service Bus receive error.</summary>
	public const int ReceiveError = 24311;

	/// <summary>Service Bus acknowledge error.</summary>
	public const int AcknowledgeError = 24312;

	/// <summary>Service Bus event source subscribed.</summary>
	public const int EventSourceSubscribed = 24313;

	// Session Consumer (24320-24339)

	/// <summary>Service Bus session message received.</summary>
	public const int SessionMessageReceived = 24320;

	/// <summary>Service Bus session message acknowledged.</summary>
	public const int SessionMessageAcknowledged = 24321;

	/// <summary>Service Bus session message rejected.</summary>
	public const int SessionMessageRejected = 24322;

	/// <summary>Service Bus session visibility modified.</summary>
	public const int SessionVisibilityModified = 24323;

	/// <summary>Service Bus session receive error.</summary>
	public const int SessionReceiveError = 24324;

	/// <summary>Service Bus session acknowledge error.</summary>
	public const int SessionAcknowledgeError = 24325;

	/// <summary>Service Bus session lock lost.</summary>
	public const int SessionLockLost = 24326;

	/// <summary>Service Bus session event source subscribed.</summary>
	public const int SessionEventSourceSubscribed = 24327;

	// ========================================
	// 24400-24499: EventHubs Core
	// ========================================

	/// <summary>Azure Event Hubs message bus initializing.</summary>
	public const int EventHubsInitializing = 24400;

	/// <summary>Azure Event Hubs message bus starting.</summary>
	public const int EventHubsStarting = 24401;

	/// <summary>Azure Event Hubs message bus stopping.</summary>
	public const int EventHubsStopping = 24402;

	/// <summary>Azure Event Hubs message broker created.</summary>
	public const int EventHubsBrokerCreated = 24403;

	/// <summary>Azure Event Hub created.</summary>
	public const int EventHubCreated = 24410;

	/// <summary>Azure Event Hub consumer group created.</summary>
	public const int ConsumerGroupCreated = 24411;

	/// <summary>Event Hubs cached publisher returned.</summary>
	public const int EventHubsCachedPublisher = 24412;

	/// <summary>Event Hubs publisher created.</summary>
	public const int EventHubsPublisherCreated = 24413;

	/// <summary>Event Hubs cached consumer returned.</summary>
	public const int EventHubsCachedConsumer = 24414;

	/// <summary>Event Hubs consumer created.</summary>
	public const int EventHubsConsumerCreated = 24415;

	/// <summary>Event Hubs invalid destination.</summary>
	public const int EventHubsInvalidDestination = 24416;

	/// <summary>Event Hubs health check failed.</summary>
	public const int EventHubsHealthCheckFailed = 24417;

	/// <summary>Event Hubs destination validation failed.</summary>
	public const int EventHubsDestinationValidationFailed = 24418;

	/// <summary>Event Hubs destination validation failed with exception.</summary>
	public const int EventHubsDestinationValidationFailedWithException = 24419;

	/// <summary>Event Hubs broker disposing.</summary>
	public const int EventHubsBrokerDisposing = 24420;

	/// <summary>Event Hubs broker disposed.</summary>
	public const int EventHubsBrokerDisposed = 24421;

	/// <summary>Event Hubs action sent.</summary>
	public const int EventHubsActionSent = 24422;

	/// <summary>Event Hubs event published.</summary>
	public const int EventHubsEventSent = 24423;

	/// <summary>Event Hubs document sent.</summary>
	public const int EventHubsDocumentSent = 24424;

	// ========================================
	// 24500-24599: EventHubs Transport
	// ========================================

	/// <summary>Event Hubs transport adapter initialized.</summary>
	public const int EventHubsTransportInitialized = 24500;

	/// <summary>Event Hubs transport adapter disposed.</summary>
	public const int EventHubsTransportDisposed = 24501;

	/// <summary>Event Hubs partition assigned.</summary>
	public const int PartitionAssigned = 24502;

	/// <summary>Event Hubs partition lost.</summary>
	public const int PartitionLost = 24503;

	/// <summary>Event Hubs transport starting.</summary>
	public const int EventHubsTransportStarting = 24504;

	/// <summary>Event Hubs transport stopping.</summary>
	public const int EventHubsTransportStopping = 24505;

	/// <summary>Event Hubs transport receiving message.</summary>
	public const int EventHubsTransportReceivingMessage = 24506;

	/// <summary>Event Hubs transport sending message.</summary>
	public const int EventHubsTransportSendingMessage = 24507;

	/// <summary>Event Hubs transport message processing failed.</summary>
	public const int EventHubsTransportMessageProcessingFailed = 24508;

	/// <summary>Event Hubs transport send failed.</summary>
	public const int EventHubsTransportSendFailed = 24509;

	// ========================================
	// 24600-24699: EventHubs Pub/Sub
	// ========================================

	/// <summary>Event Hubs event published.</summary>
	public const int EventHubsEventPublished = 24600;

	/// <summary>Event Hubs event received.</summary>
	public const int EventHubsEventReceived = 24601;

	/// <summary>Event Hubs batch published.</summary>
	public const int EventHubsBatchPublished = 24602;

	/// <summary>Event Hubs checkpoint created.</summary>
	public const int CheckpointCreated = 24603;

	// EventHubs Publisher (24610-24629)

	/// <summary>Event Hubs publishing message.</summary>
	public const int EventHubsPublishingMessage = 24610;

	/// <summary>Event Hubs message published successfully.</summary>
	public const int EventHubsMessagePublished = 24611;

	/// <summary>Event Hubs publishing batch.</summary>
	public const int EventHubsPublishingBatch = 24612;

	/// <summary>Event Hubs batch published summary.</summary>
	public const int EventHubsBatchPublishedSummary = 24613;

	/// <summary>Event Hubs channel publish failed.</summary>
	public const int EventHubsChannelPublishFailed = 24614;

	/// <summary>Event Hubs channel cancelled.</summary>
	public const int EventHubsChannelCancelled = 24615;

	/// <summary>Event Hubs channel failed.</summary>
	public const int EventHubsChannelFailed = 24616;

	/// <summary>Event Hubs publishing with partition.</summary>
	public const int EventHubsPublishingWithPartition = 24617;

	// EventHubs Consumer (24630-24659)

	/// <summary>Event Hubs processing started.</summary>
	public const int EventHubsProcessingStarted = 24630;

	/// <summary>Event Hubs processing stopped.</summary>
	public const int EventHubsProcessingStopped = 24631;

	/// <summary>Event Hubs event received with details.</summary>
	public const int EventHubsEventReceivedDetailed = 24632;

	/// <summary>Event Hubs event processed.</summary>
	public const int EventHubsEventProcessed = 24633;

	/// <summary>Event Hubs event processing failed.</summary>
	public const int EventHubsEventProcessingFailed = 24634;

	/// <summary>Event Hubs checkpoint failed.</summary>
	public const int EventHubsCheckpointFailed = 24635;

	/// <summary>Event Hubs message acknowledged.</summary>
	public const int EventHubsMessageAcknowledged = 24636;

	/// <summary>Event Hubs batch acknowledged.</summary>
	public const int EventHubsBatchAcknowledged = 24637;

	/// <summary>Event Hubs message rejected.</summary>
	public const int EventHubsMessageRejected = 24638;

	/// <summary>Event Hubs visibility not supported.</summary>
	public const int EventHubsVisibilityNotSupported = 24639;

	/// <summary>Event Hubs consumer already started.</summary>
	public const int EventHubsAlreadyStarted = 24640;

	/// <summary>Event Hubs CloudEvent parsed.</summary>
	public const int EventHubsCloudEventParsed = 24641;

	// ========================================
	// 24700-24799: StorageQueues Core (MessageBroker)
	// ========================================

	/// <summary>Azure Storage Queue message broker initializing.</summary>
	public const int StorageQueueInitializing = 24700;

	/// <summary>Azure Storage Queue message broker starting.</summary>
	public const int StorageQueueStarting = 24701;

	/// <summary>Azure Storage Queue message broker stopping.</summary>
	public const int StorageQueueStopping = 24702;

	/// <summary>Azure Storage Queue created.</summary>
	public const int StorageQueueCreated = 24710;

	/// <summary>Storage Queue cached publisher returned.</summary>
	public const int StorageQueueCachedPublisher = 24711;

	/// <summary>Storage Queue publisher created.</summary>
	public const int StorageQueuePublisherCreated = 24712;

	/// <summary>Storage Queue cached consumer returned.</summary>
	public const int StorageQueueCachedConsumer = 24713;

	/// <summary>Storage Queue consumer created.</summary>
	public const int StorageQueueConsumerCreated = 24714;

	/// <summary>Storage Queue invalid destination.</summary>
	public const int StorageQueueInvalidDestination = 24715;

	/// <summary>Storage Queue health check failed.</summary>
	public const int StorageQueueHealthCheckFailed = 24716;

	/// <summary>Storage Queue destination validation failed.</summary>
	public const int StorageQueueDestinationValidationFailed = 24717;

	/// <summary>Storage Queue destination validation failed with exception.</summary>
	public const int StorageQueueDestinationValidationFailedWithException = 24718;

	/// <summary>Storage Queue broker disposing.</summary>
	public const int StorageQueueBrokerDisposing = 24719;

	/// <summary>Storage Queue broker disposed.</summary>
	public const int StorageQueueBrokerDisposed = 24720;

	// ========================================
	// 24800-24899: StorageQueues Transport/Publisher
	// ========================================

	/// <summary>Storage Queue transport adapter initialized.</summary>
	public const int StorageQueueTransportInitialized = 24800;

	/// <summary>Storage Queue transport adapter disposed.</summary>
	public const int StorageQueueTransportDisposed = 24801;

	/// <summary>Storage Queue message published.</summary>
	public const int StorageQueueMessagePublished = 24802;

	/// <summary>Storage Queue transport starting.</summary>
	public const int StorageQueueTransportStarting = 24803;

	/// <summary>Storage Queue transport stopping.</summary>
	public const int StorageQueueTransportStopping = 24804;

	/// <summary>Storage Queue transport receiving message.</summary>
	public const int StorageQueueTransportReceivingMessage = 24805;

	/// <summary>Storage Queue transport sending message.</summary>
	public const int StorageQueueTransportSendingMessage = 24806;

	/// <summary>Storage Queue transport message processing failed.</summary>
	public const int StorageQueueTransportMessageProcessingFailed = 24807;

	/// <summary>Storage Queue transport send failed.</summary>
	public const int StorageQueueTransportSendFailed = 24808;

	/// <summary>Storage Queue publishing message.</summary>
	public const int StorageQueuePublishingMessage = 24810;

	/// <summary>Storage Queue publishing batch.</summary>
	public const int StorageQueuePublishingBatch = 24811;

	/// <summary>Storage Queue batch published summary.</summary>
	public const int StorageQueueBatchPublishedSummary = 24812;

	/// <summary>Storage Queue channel cancelled.</summary>
	public const int StorageQueueChannelCancelled = 24813;

	/// <summary>Storage Queue channel failed.</summary>
	public const int StorageQueueChannelFailed = 24814;

	// ========================================
	// 24900-24999: StorageQueues Consumer
	// ========================================

	/// <summary>Storage Queue consumer started.</summary>
	public const int StorageQueueConsumerStarted = 24900;

	/// <summary>Storage Queue consumer stopped.</summary>
	public const int StorageQueueConsumerStopped = 24901;

	/// <summary>Storage Queue message received.</summary>
	public const int StorageQueueMessageReceived = 24902;

	/// <summary>Storage Queue message deleted.</summary>
	public const int StorageQueueMessageDeleted = 24903;

	/// <summary>Storage Queue message processor started.</summary>
	public const int MessageProcessorStarted = 24904;

	/// <summary>Storage Queue metrics collected.</summary>
	public const int QueueMetricsCollected = 24905;

	/// <summary>Storage Queue message acknowledged.</summary>
	public const int StorageQueueMessageAcknowledged = 24906;

	/// <summary>Storage Queue batch acknowledged.</summary>
	public const int StorageQueueBatchAcknowledged = 24907;

	/// <summary>Storage Queue message rejected.</summary>
	public const int StorageQueueMessageRejected = 24908;

	/// <summary>Storage Queue message visibility extended.</summary>
	public const int StorageQueueVisibilityExtended = 24909;

	/// <summary>Storage Queue visibility extension failed.</summary>
	public const int StorageQueueVisibilityExtensionFailed = 24910;

	/// <summary>Storage Queue already started.</summary>
	public const int StorageQueueAlreadyStarted = 24911;

	/// <summary>Storage Queue starting processing.</summary>
	public const int StorageQueueStartingProcessing = 24912;

	/// <summary>Storage Queue stopping processing.</summary>
	public const int StorageQueueStoppingProcessing = 24913;

	/// <summary>Storage Queue messages received from queue.</summary>
	public const int StorageQueueMessagesReceived = 24914;

	/// <summary>Storage Queue no messages available.</summary>
	public const int StorageQueueNoMessages = 24915;

	/// <summary>Storage Queue receive failed.</summary>
	public const int StorageQueueReceiveFailed = 24916;

	/// <summary>Storage Queue message processing started.</summary>
	public const int StorageQueueMessageProcessingStarted = 24917;

	/// <summary>Storage Queue message processed successfully.</summary>
	public const int StorageQueueMessageProcessed = 24918;

	/// <summary>Storage Queue message processing failed.</summary>
	public const int StorageQueueMessageProcessingFailed = 24919;

	/// <summary>Storage Queue batch completed.</summary>
	public const int StorageQueueBatchCompleted = 24920;

	/// <summary>Storage Queue queue health checked.</summary>
	public const int StorageQueueHealthChecked = 24921;

	/// <summary>Storage Queue processing iteration completed.</summary>
	public const int StorageQueueIterationCompleted = 24922;

	/// <summary>Storage Queue CloudEvent parsed.</summary>
	public const int StorageQueueCloudEventParsed = 24923;

	/// <summary>Storage Queue message parsed as envelope.</summary>
	public const int StorageQueueEnvelopeParsed = 24924;

	/// <summary>Storage Queue message converted successfully.</summary>
	public const int StorageQueueMessageConverted = 24925;

	/// <summary>Storage Queue invalid message type.</summary>
	public const int StorageQueueInvalidMessageType = 24926;

	/// <summary>Storage Queue message sent to dead letter.</summary>
	public const int StorageQueueDeadLettered = 24927;

	/// <summary>Storage Queue message rejected but not dead lettered.</summary>
	public const int StorageQueueRejectedNotDeadLettered = 24928;

	// StorageQueues Metrics (24930-24949)

	/// <summary>Storage Queue message processing recorded.</summary>
	public const int StorageQueueMetricsMessageProcessed = 24930;

	/// <summary>Storage Queue batch processing recorded.</summary>
	public const int StorageQueueMetricsBatchProcessed = 24931;

	/// <summary>Storage Queue receive operation recorded.</summary>
	public const int StorageQueueMetricsReceiveOperation = 24932;

	/// <summary>Storage Queue delete operation recorded.</summary>
	public const int StorageQueueMetricsDeleteOperation = 24933;

	/// <summary>Storage Queue visibility timeout update recorded.</summary>
	public const int StorageQueueMetricsVisibilityUpdate = 24934;

	/// <summary>Storage Queue health recorded.</summary>
	public const int StorageQueueMetricsHealthRecorded = 24935;

	/// <summary>Storage Queue metrics collector disposing.</summary>
	public const int StorageQueueMetricsDisposing = 24936;

	/// <summary>Storage Queue metrics collector disposal error.</summary>
	public const int StorageQueueMetricsDisposalError = 24937;

	// ========================================
	// Error Handling (spread across categories)
	// ========================================

	/// <summary>Azure Service Bus error.</summary>
	public const int ServiceBusError = 24090;

	/// <summary>Azure Event Hubs error.</summary>
	public const int EventHubsError = 24490;

	/// <summary>Azure Storage Queue error.</summary>
	public const int StorageQueueError = 24790;

	// ========================================
	// 24950-24962: Service Bus Dead Letter Queue
	// ========================================

	/// <summary>Service Bus DLQ manager initialized.</summary>
	public const int DlqManagerInitialized = 24950;

	/// <summary>Service Bus message moved to DLQ.</summary>
	public const int DlqMessageMoved = 24951;

	/// <summary>Service Bus DLQ move failed.</summary>
	public const int DlqMoveFailed = 24952;

	/// <summary>Service Bus DLQ messages retrieved.</summary>
	public const int DlqMessagesRetrieved = 24953;

	/// <summary>Service Bus DLQ retrieve failed.</summary>
	public const int DlqRetrieveFailed = 24954;

	/// <summary>Service Bus DLQ message reprocessed.</summary>
	public const int DlqMessageReprocessed = 24955;

	/// <summary>Service Bus DLQ reprocess failed.</summary>
	public const int DlqReprocessFailed = 24956;

	/// <summary>Service Bus DLQ statistics retrieved.</summary>
	public const int DlqStatisticsRetrieved = 24957;

	/// <summary>Service Bus DLQ purged.</summary>
	public const int DlqPurged = 24958;

	/// <summary>Service Bus DLQ purge failed.</summary>
	public const int DlqPurgeFailed = 24959;

	/// <summary>Service Bus DLQ message skipped during reprocessing.</summary>
	public const int DlqMessageSkipped = 24960;

	// ========================================
	// 24970-24989: ITransportSender / ITransportReceiver
	// ========================================

	/// <summary>Transport sender: message sent successfully.</summary>
	public const int TransportSenderMessageSent = 24970;

	/// <summary>Transport sender: send failed.</summary>
	public const int TransportSenderSendFailed = 24971;

	/// <summary>Transport sender: batch sent.</summary>
	public const int TransportSenderBatchSent = 24972;

	/// <summary>Transport sender: batch send failed.</summary>
	public const int TransportSenderBatchSendFailed = 24973;

	/// <summary>Transport sender: disposed.</summary>
	public const int TransportSenderDisposed = 24974;

	/// <summary>Transport receiver: message received.</summary>
	public const int TransportReceiverMessageReceived = 24980;

	/// <summary>Transport receiver: receive error.</summary>
	public const int TransportReceiverReceiveError = 24981;

	/// <summary>Transport receiver: message acknowledged.</summary>
	public const int TransportReceiverMessageAcknowledged = 24982;

	/// <summary>Transport receiver: acknowledge error.</summary>
	public const int TransportReceiverAcknowledgeError = 24983;

	/// <summary>Transport receiver: message rejected.</summary>
	public const int TransportReceiverMessageRejected = 24984;

	/// <summary>Transport receiver: message rejected with requeue.</summary>
	public const int TransportReceiverMessageRejectedRequeue = 24985;

	/// <summary>Transport receiver: reject error.</summary>
	public const int TransportReceiverRejectError = 24986;

	/// <summary>Transport receiver: disposed.</summary>
	public const int TransportReceiverDisposed = 24987;

	// ========================================
	// 24990-24997: ITransportSubscriber
	// ========================================

	/// <summary>Transport subscriber: subscription started.</summary>
	public const int TransportSubscriberStarted = 24990;

	/// <summary>Transport subscriber: message received and dispatched to handler.</summary>
	public const int TransportSubscriberMessageReceived = 24991;

	/// <summary>Transport subscriber: message acknowledged after handler returned Acknowledge.</summary>
	public const int TransportSubscriberMessageAcknowledged = 24992;

	/// <summary>Transport subscriber: message rejected (dead-lettered) after handler returned Reject.</summary>
	public const int TransportSubscriberMessageRejected = 24993;

	/// <summary>Transport subscriber: message requeued (abandoned) after handler returned Requeue.</summary>
	public const int TransportSubscriberMessageRequeued = 24994;

	/// <summary>Transport subscriber: handler or processing error.</summary>
	public const int TransportSubscriberError = 24995;

	/// <summary>Transport subscriber: subscription stopped.</summary>
	public const int TransportSubscriberStopped = 24996;

	/// <summary>Transport subscriber: disposed.</summary>
	public const int TransportSubscriberDisposed = 24997;

	// ========================================
	// 24998: Reserved
	// ========================================

	// ========================================
	// 25100-25119: Event Grid Sender
	// ========================================

	/// <summary>Event Grid transport sender: message sent successfully.</summary>
	public const int EventGridSenderMessageSent = 25100;

	/// <summary>Event Grid transport sender: send failed.</summary>
	public const int EventGridSenderSendFailed = 25101;

	/// <summary>Event Grid transport sender: batch sent.</summary>
	public const int EventGridSenderBatchSent = 25102;

	/// <summary>Event Grid transport sender: batch send failed.</summary>
	public const int EventGridSenderBatchSendFailed = 25103;

	/// <summary>Event Grid transport sender: disposed.</summary>
	public const int EventGridSenderDisposed = 25104;

	// ========================================
	// 25120-25139: Event Grid Subscriber
	// ========================================

	/// <summary>Event Grid transport subscriber: subscription started.</summary>
	public const int EventGridSubscriberStarted = 25120;

	/// <summary>Event Grid transport subscriber: event received.</summary>
	public const int EventGridSubscriberEventReceived = 25121;

	/// <summary>Event Grid transport subscriber: event acknowledged.</summary>
	public const int EventGridSubscriberEventAcknowledged = 25122;

	/// <summary>Event Grid transport subscriber: event rejected.</summary>
	public const int EventGridSubscriberEventRejected = 25123;

	/// <summary>Event Grid transport subscriber: error.</summary>
	public const int EventGridSubscriberError = 25124;

	/// <summary>Event Grid transport subscriber: subscription stopped.</summary>
	public const int EventGridSubscriberStopped = 25125;

	/// <summary>Event Grid transport subscriber: disposed.</summary>
	public const int EventGridSubscriberDisposed = 25126;
}
