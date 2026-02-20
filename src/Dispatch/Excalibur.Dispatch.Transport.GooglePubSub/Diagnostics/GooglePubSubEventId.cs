// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.GooglePubSub;

/// <summary>
/// Event IDs for Google Pub/Sub transport (23000-23999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>23000-23099: Core</item>
/// <item>23100-23199: Channel Receiver</item>
/// <item>23200-23299: Streaming Pull</item>
/// <item>23300-23399: Ordering</item>
/// <item>23400-23499: Batch Receiving</item>
/// <item>23500-23599: Flow Control</item>
/// <item>23600-23699: Parallel Processing</item>
/// <item>23700-23799: Dead Letter</item>
/// </list>
/// </remarks>
public static class GooglePubSubEventId
{
	// ========================================
	// 23000-23099: Core
	// ========================================

	/// <summary>Google Pub/Sub message bus initializing.</summary>
	public const int MessageBusInitializing = 23000;

	/// <summary>Google Pub/Sub message bus starting.</summary>
	public const int MessageBusStarting = 23001;

	/// <summary>Google Pub/Sub message bus stopping.</summary>
	public const int MessageBusStopping = 23002;

	/// <summary>Google Pub/Sub message broker disposing.</summary>
	public const int MessageBrokerDisposing = 23030;

	/// <summary>Google Pub/Sub message broker disposed.</summary>
	public const int MessageBrokerDisposed = 23031;

	/// <summary>Google Pub/Sub publisher created.</summary>
	public const int PublisherCreated = 23003;

	/// <summary>Google Pub/Sub subscriber created.</summary>
	public const int SubscriberCreated = 23004;

	/// <summary>Google Pub/Sub topic created.</summary>
	public const int TopicCreated = 23010;

	/// <summary>Google Pub/Sub subscription created.</summary>
	public const int SubscriptionCreated = 23011;

	/// <summary>Google Pub/Sub message published.</summary>
	public const int MessagePublished = 23012;

	// GooglePubSubMessageBus
	/// <summary>Sent action via Pub/Sub.</summary>
	public const int SentAction = 23020;

	/// <summary>Published event via Pub/Sub.</summary>
	public const int PublishedEvent = 23021;

	/// <summary>Sent document via Pub/Sub.</summary>
	public const int SentDocument = 23022;

	// Transport Adapter (23050-23069)
	/// <summary>Transport adapter starting.</summary>
	public const int TransportAdapterStarting = 23050;

	/// <summary>Transport adapter stopping.</summary>
	public const int TransportAdapterStopping = 23051;

	/// <summary>Transport adapter receiving message.</summary>
	public const int TransportAdapterReceivingMessage = 23052;

	/// <summary>Transport adapter sending message.</summary>
	public const int TransportAdapterSendingMessage = 23053;

	/// <summary>Transport adapter message processing failed.</summary>
	public const int TransportAdapterMessageProcessingFailed = 23054;

	/// <summary>Transport adapter send failed.</summary>
	public const int TransportAdapterSendFailed = 23055;

	// ========================================
	// 23100-23199: Channel Receiver
	// ========================================

	/// <summary>Google Pub/Sub channel receiver starting.</summary>
	public const int ChannelReceiverStarting = 23100;

	/// <summary>Google Pub/Sub channel receiver stopping.</summary>
	public const int ChannelReceiverStopping = 23101;

	/// <summary>Google Pub/Sub message received.</summary>
	public const int MessageReceived = 23102;

	/// <summary>Google Pub/Sub message acknowledged.</summary>
	public const int MessageAcknowledged = 23103;

	/// <summary>Google Pub/Sub message nacked.</summary>
	public const int MessageNacked = 23104;

	// GooglePubSubChannelReceiver
	/// <summary>Consumption started for subscription.</summary>
	public const int ConsumptionStarted = 23110;

	/// <summary>Batch produced from Pub/Sub.</summary>
	public const int BatchProduced = 23111;

	/// <summary>Message conversion error.</summary>
	public const int MessageConversionError = 23112;

	/// <summary>Messages acknowledged in batch.</summary>
	public const int MessagesAcknowledged = 23113;

	/// <summary>Acknowledgment error.</summary>
	public const int AcknowledgmentError = 23114;

	/// <summary>Dead letter publish error.</summary>
	public const int DeadLetterPublishError = 23115;

	// GooglePubSubChannelReceiverLogging
	/// <summary>Ack deadline extended.</summary>
	public const int AckDeadlineExtended = 23120;

	/// <summary>Ack deadline extension failed.</summary>
	public const int AckDeadlineExtensionFailed = 23121;

	/// <summary>Streaming pull started.</summary>
	public const int StreamingPullConnectionStarted = 23122;

	/// <summary>Streaming pull reconnecting.</summary>
	public const int StreamingPullReconnecting = 23123;

	// ========================================
	// 23200-23299: Streaming Pull
	// ========================================

	/// <summary>Streaming pull started.</summary>
	public const int StreamingPullStarted = 23200;

	/// <summary>Streaming pull stopped.</summary>
	public const int StreamingPullStopped = 23201;

	/// <summary>Stream health check performed.</summary>
	public const int StreamHealthCheck = 23202;

	/// <summary>Stream health degraded.</summary>
	public const int StreamHealthDegraded = 23203;

	/// <summary>Stream reconnecting.</summary>
	public const int StreamReconnecting = 23204;

	// StreamHealthMonitor
	/// <summary>Stream error occurred.</summary>
	public const int StreamError = 23210;

	/// <summary>Stream connected.</summary>
	public const int StreamConnected = 23211;

	/// <summary>Stream disconnected.</summary>
	public const int StreamDisconnected = 23212;

	/// <summary>Stream is idle.</summary>
	public const int StreamIdle = 23213;

	/// <summary>High error rate detected.</summary>
	public const int HighErrorRate = 23214;

	/// <summary>High ack failure rate detected.</summary>
	public const int HighAckFailureRate = 23215;

	/// <summary>Unhealthy streams found.</summary>
	public const int UnhealthyStreamsFound = 23216;

	/// <summary>Health check error.</summary>
	public const int HealthCheckError = 23217;

	// StreamingPullStream
	/// <summary>Task cleanup failed.</summary>
	public const int TaskCleanupFailed = 23220;

	/// <summary>Task cleanup cancelled.</summary>
	public const int TaskCleanupCancelled = 23221;

	/// <summary>Task cleanup disposed.</summary>
	public const int TaskCleanupDisposed = 23222;

	// ========================================
	// 23300-23399: Ordering
	// ========================================

	/// <summary>Ordering key assigned.</summary>
	public const int OrderingKeyAssigned = 23300;

	/// <summary>Ordering key processed.</summary>
	public const int OrderingKeyProcessed = 23301;

	/// <summary>Ordering enabled.</summary>
	public const int OrderingEnabled = 23302;

	/// <summary>Out-of-order message detected.</summary>
	public const int OutOfOrderDetected = 23303;

	// OrderingKeyProcessor
	/// <summary>Ordering key processor started.</summary>
	public const int OrderingProcessorStarted = 23310;

	/// <summary>Ordering key processor shutdown complete.</summary>
	public const int OrderingProcessorShutdown = 23311;

	/// <summary>Ordering key processor shutdown timed out.</summary>
	public const int OrderingProcessorShutdownTimeout = 23312;

	/// <summary>Ordering key worker started.</summary>
	public const int OrderingWorkerStarted = 23313;

	/// <summary>Ordering key worker stopped.</summary>
	public const int OrderingWorkerStopped = 23314;

	/// <summary>Ordering key worker error.</summary>
	public const int OrderingWorkerError = 23315;

	/// <summary>Ordering key message processing error.</summary>
	public const int OrderingMessageProcessingError = 23316;

	/// <summary>Unordered message error.</summary>
	public const int UnorderedMessageError = 23317;

	/// <summary>Ordering key queue removed.</summary>
	public const int OrderingQueueRemoved = 23318;

	// OrderingKeyManager
	/// <summary>Ordering key manager initialized.</summary>
	public const int OrderingManagerInitialized = 23320;

	/// <summary>Out-of-sequence message detected.</summary>
	public const int OutOfSequenceMessage = 23321;

	/// <summary>Ordering key marked as failed.</summary>
	public const int OrderingKeyFailed = 23322;

	/// <summary>Ordering key reset.</summary>
	public const int OrderingKeyReset = 23323;

	/// <summary>Ordering key cleanup completed.</summary>
	public const int OrderingKeyCleanupCompleted = 23324;

	// ========================================
	// 23400-23499: Batch Receiving
	// ========================================

	/// <summary>Batch receive started.</summary>
	public const int BatchReceiveStarted = 23400;

	/// <summary>Batch receive completed.</summary>
	public const int BatchReceiveCompleted = 23401;

	/// <summary>Adaptive batching strategy applied.</summary>
	public const int AdaptiveBatchingApplied = 23402;

	/// <summary>Batch size adjusted.</summary>
	public const int BatchSizeAdjusted = 23403;

	// PubSubBatchReceiver
	/// <summary>Flow control prevented batch receive.</summary>
	public const int FlowControlPreventedReceive = 23410;

	/// <summary>Batch acknowledged.</summary>
	public const int BatchAcknowledged = 23411;

	/// <summary>Acknowledgments failed.</summary>
	public const int BatchAcknowledgmentsFailed = 23412;

	/// <summary>Ack deadline modified.</summary>
	public const int BatchAckDeadlineModified = 23413;

	// AdaptiveBatchingStrategy
	/// <summary>Flow control limiting batch size.</summary>
	public const int AdaptiveFlowControlLimit = 23420;

	/// <summary>High memory pressure detected.</summary>
	public const int AdaptiveMemoryPressure = 23421;

	/// <summary>Batch result recorded.</summary>
	public const int AdaptiveBatchResult = 23422;

	/// <summary>Adaptive batching strategy reset.</summary>
	public const int AdaptiveStrategyReset = 23423;

	/// <summary>Batch size adjusted by adaptive strategy.</summary>
	public const int AdaptiveBatchSizeAdjusted = 23424;

	// ========================================
	// 23500-23599: Flow Control
	// ========================================

	/// <summary>Flow control applied.</summary>
	public const int FlowControlApplied = 23500;

	/// <summary>Flow control released.</summary>
	public const int FlowControlReleased = 23501;

	/// <summary>Subscriber factory created.</summary>
	public const int SubscriberFactoryCreated = 23502;

	/// <summary>Outstanding messages limit reached.</summary>
	public const int OutstandingMessagesLimit = 23503;

	// PubSubSubscriberFactory
	/// <summary>Flow-controlled subscriber created.</summary>
	public const int FlowControlledSubscriberCreated = 23510;

	/// <summary>Message processing error in subscriber.</summary>
	public const int SubscriberMessageProcessingError = 23511;

	// ========================================
	// 23600-23699: Parallel Processing
	// ========================================

	/// <summary>Parallel processing started.</summary>
	public const int ParallelProcessingStarted = 23600;

	/// <summary>Parallel processing completed.</summary>
	public const int ParallelProcessingCompleted = 23601;

	/// <summary>Worker thread started.</summary>
	public const int WorkerThreadStarted = 23602;

	/// <summary>Worker thread stopped.</summary>
	public const int WorkerThreadStopped = 23603;

	// ParallelMessageProcessor
	/// <summary>Parallel processor started.</summary>
	public const int ParallelProcessorStarted = 23610;

	/// <summary>Parallel processor shutdown complete.</summary>
	public const int ParallelProcessorShutdown = 23611;

	/// <summary>Parallel processor shutdown timed out.</summary>
	public const int ParallelProcessorShutdownTimeout = 23612;

	/// <summary>Parallel worker started.</summary>
	public const int ParallelWorkerStarted = 23613;

	/// <summary>Parallel worker stopped.</summary>
	public const int ParallelWorkerStopped = 23614;

	/// <summary>Parallel worker error.</summary>
	public const int ParallelWorkerError = 23615;

	/// <summary>Parallel message processing error.</summary>
	public const int ParallelMessageProcessingError = 23616;

	/// <summary>Ordering key assigned to worker.</summary>
	public const int ParallelOrderingKeyAssigned = 23617;

	// ========================================
	// 23700-23799: Dead Letter
	// ========================================

	/// <summary>Message moved to dead letter topic.</summary>
	public const int MovedToDeadLetter = 23700;

	/// <summary>Dead letter queue processed.</summary>
	public const int DeadLetterProcessed = 23701;

	/// <summary>Retry policy applied.</summary>
	public const int RetryPolicyApplied = 23702;

	/// <summary>Max delivery attempts reached.</summary>
	public const int MaxDeliveryAttemptsReached = 23703;

	// PubSubDeadLetterQueueManager
	/// <summary>Dead letter policy configured.</summary>
	public const int DeadLetterPolicyConfigured = 23710;

	/// <summary>Subscription not found.</summary>
	public const int DeadLetterSubscriptionNotFound = 23711;

	/// <summary>Message moved to dead letter queue.</summary>
	public const int MessageMovedToDeadLetter = 23712;

	/// <summary>Exception caused dead lettering.</summary>
	public const int ExceptionCausedDeadLettering = 23713;

	/// <summary>Failed to parse dead letter message.</summary>
	public const int DeadLetterParseFailed = 23714;

	/// <summary>Retrieved dead letter messages.</summary>
	public const int DeadLetterMessagesRetrieved = 23715;

	/// <summary>Failed to reprocess message.</summary>
	public const int DeadLetterReprocessFailed = 23716;

	/// <summary>Reprocessed dead letter messages.</summary>
	public const int DeadLetterMessagesReprocessed = 23717;

	/// <summary>Failed to deserialize DLQ metadata.</summary>
	public const int DeadLetterMetadataDeserializeFailed = 23718;

	/// <summary>Reprocessed single message.</summary>
	public const int DeadLetterMessageReprocessed = 23719;

	/// <summary>Purged messages from the dead letter queue.</summary>
	public const int DeadLetterQueuePurged = 23704;

	// RetryPolicyManager
	/// <summary>Retry attempt logged.</summary>
	public const int RetryAttemptLogged = 23720;

	/// <summary>Adapted retry policy for low success rate.</summary>
	public const int RetryAdaptedLowSuccessRate = 23721;

	/// <summary>Adapted retry policy for high success rate.</summary>
	public const int RetryAdaptedHighSuccessRate = 23722;

	/// <summary>Retry warning logged.</summary>
	public const int RetryWarning = 23723;

	/// <summary>Circuit breaker opened.</summary>
	public const int CircuitBreakerOpened = 23724;

	/// <summary>Circuit breaker reset.</summary>
	public const int CircuitBreakerReset = 23725;

	// ========================================
	// 23800-23899: Error Handling
	// ========================================

	/// <summary>Google Pub/Sub publisher error.</summary>
	public const int PublisherError = 23800;

	/// <summary>Google Pub/Sub subscriber error.</summary>
	public const int SubscriberError = 23801;

	/// <summary>Failed to receive messages from Pub/Sub.</summary>
	public const int SubscriberReceiveFailed = 23810;

	/// <summary>Failed to acknowledge a Pub/Sub message.</summary>
	public const int SubscriberAcknowledgeFailed = 23811;

	/// <summary>Failed to acknowledge a Pub/Sub batch.</summary>
	public const int SubscriberAcknowledgeBatchFailed = 23812;

	/// <summary>Failed to modify Pub/Sub message visibility.</summary>
	public const int SubscriberModifyVisibilityFailed = 23813;

	/// <summary>Pub/Sub event source subscribed.</summary>
	public const int SubscriberEventSourceSubscribed = 23814;

	/// <summary>Error in Pub/Sub consume loop.</summary>
	public const int SubscriberConsumeLoopError = 23815;

	/// <summary>Pub/Sub subscription not found.</summary>
	public const int SubscriberNotFound = 23816;

	/// <summary>Error in Pub/Sub channel reader.</summary>
	public const int SubscriberChannelReaderError = 23817;

	/// <summary>Pub/Sub consumer disposed.</summary>
	public const int SubscriberDisposed = 23818;

	/// <summary>Pub/Sub health check failed.</summary>
	public const int HealthCheckFailed = 23820;

	/// <summary>Pub/Sub destination validation failed.</summary>
	public const int DestinationValidationFailed = 23821;

	/// <summary>Failed to publish message to Pub/Sub.</summary>
	public const int PublisherPublishFailed = 23830;

	/// <summary>Failed to publish batch to Pub/Sub.</summary>
	public const int PublisherBatchPublishFailed = 23831;

	/// <summary>Pub/Sub publish channel error.</summary>
	public const int PublisherChannelError = 23832;

	/// <summary>Google Pub/Sub deserialization error.</summary>
	public const int DeserializationError = 23802;

	/// <summary>Google Pub/Sub connection error.</summary>
	public const int ConnectionError = 23803;

	// ========================================
	// 23900-23919: ITransportSender / ITransportReceiver
	// ========================================

	/// <summary>Transport sender: message sent successfully.</summary>
	public const int TransportSenderMessageSent = 23900;

	/// <summary>Transport sender: send failed.</summary>
	public const int TransportSenderSendFailed = 23901;

	/// <summary>Transport sender: batch sent.</summary>
	public const int TransportSenderBatchSent = 23902;

	/// <summary>Transport sender: batch send failed.</summary>
	public const int TransportSenderBatchSendFailed = 23903;

	/// <summary>Transport sender: disposed.</summary>
	public const int TransportSenderDisposed = 23904;

	/// <summary>Transport receiver: message received.</summary>
	public const int TransportReceiverMessageReceived = 23910;

	/// <summary>Transport receiver: receive error.</summary>
	public const int TransportReceiverReceiveError = 23911;

	/// <summary>Transport receiver: message acknowledged.</summary>
	public const int TransportReceiverMessageAcknowledged = 23912;

	/// <summary>Transport receiver: acknowledge error.</summary>
	public const int TransportReceiverAcknowledgeError = 23913;

	/// <summary>Transport receiver: message rejected.</summary>
	public const int TransportReceiverMessageRejected = 23914;

	/// <summary>Transport receiver: message rejected with requeue.</summary>
	public const int TransportReceiverMessageRejectedRequeue = 23915;

	/// <summary>Transport receiver: reject error.</summary>
	public const int TransportReceiverRejectError = 23916;

	/// <summary>Transport receiver: disposed.</summary>
	public const int TransportReceiverDisposed = 23917;

	// ========================================
	// 23920-23927: ITransportSubscriber
	// ========================================

	/// <summary>Transport subscriber: subscription started.</summary>
	public const int TransportSubscriberStarted = 23920;

	/// <summary>Transport subscriber: message received.</summary>
	public const int TransportSubscriberMessageReceived = 23921;

	/// <summary>Transport subscriber: message acknowledged.</summary>
	public const int TransportSubscriberMessageAcknowledged = 23922;

	/// <summary>Transport subscriber: message rejected.</summary>
	public const int TransportSubscriberMessageRejected = 23923;

	/// <summary>Transport subscriber: message requeued.</summary>
	public const int TransportSubscriberMessageRequeued = 23924;

	/// <summary>Transport subscriber: error processing message.</summary>
	public const int TransportSubscriberError = 23925;

	/// <summary>Transport subscriber: subscription stopped.</summary>
	public const int TransportSubscriberStopped = 23926;

	/// <summary>Transport subscriber: disposed.</summary>
	public const int TransportSubscriberDisposed = 23927;
}
