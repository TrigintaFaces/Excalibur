// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Event IDs for AWS SQS/SNS transport (25000-25999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>25000-25099: Core</item>
/// <item>25100-25199: SQS Core</item>
/// <item>25200-25299: SQS Publisher</item>
/// <item>25300-25399: SQS Consumer</item>
/// <item>25400-25499: SQS Channels</item>
/// <item>25500-25599: SQS High Throughput</item>
/// <item>25600-25699: Long Polling</item>
/// <item>25700-25799: SNS</item>
/// <item>25800-25899: EventBridge</item>
/// </list>
/// </remarks>
public static class AwsSqsEventId
{
	// ========================================
	// 25000-25099: Core (AwsMessageBroker)
	// ========================================

	/// <summary>AWS message broker initializing.</summary>
	public const int BrokerInitializing = 25000;

	/// <summary>AWS message broker starting.</summary>
	public const int BrokerStarting = 25001;

	/// <summary>AWS message broker stopping.</summary>
	public const int BrokerStopping = 25002;

	/// <summary>AWS common logging initialized.</summary>
	public const int CommonLoggingInitialized = 25003;

	/// <summary>AWS credentials validated.</summary>
	public const int CredentialsValidated = 25004;

	/// <summary>AWS region configured.</summary>
	public const int RegionConfigured = 25005;

	// Connection pooling
	/// <summary>Connection acquired from pool.</summary>
	public const int ConnectionAcquired = 25030;

	/// <summary>Connection released to pool.</summary>
	public const int ConnectionReleased = 25031;

	/// <summary>Connection pool exhausted.</summary>
	public const int ConnectionPoolExhausted = 25032;

	// Retry and circuit breaker
	/// <summary>Retry attempt for operation.</summary>
	public const int RetryAttempt = 25033;

	/// <summary>Circuit breaker opened.</summary>
	public const int CircuitBreakerOpened = 25034;

	/// <summary>Circuit breaker closed.</summary>
	public const int CircuitBreakerClosed = 25035;

	// Batch operations
	/// <summary>Batch operation started.</summary>
	public const int BatchOperationStarted = 25036;

	/// <summary>Batch operation completed.</summary>
	public const int BatchOperationCompleted = 25037;

	// Session management
	/// <summary>Session created.</summary>
	public const int SessionCreated = 25038;

	/// <summary>Session expired.</summary>
	public const int SessionExpired = 25039;

	// DLQ processing
	/// <summary>DLQ message processed.</summary>
	public const int DlqMessageProcessed = 25040;

	/// <summary>DLQ message failed permanently.</summary>
	public const int DlqMessageFailed = 25041;

	// Metrics collection
	/// <summary>Metric recorded.</summary>
	public const int MetricRecorded = 25042;

	/// <summary>Metrics batch published.</summary>
	public const int MetricsBatchPublished = 25043;

	/// <summary>Returning cached publisher for destination.</summary>
	public const int CachedPublisher = 25010;

	/// <summary>Creating AWS publisher for destination.</summary>
	public const int PublisherCreated = 25011;

	/// <summary>Returning cached consumer for source.</summary>
	public const int CachedConsumer = 25012;

	/// <summary>Creating AWS consumer for source.</summary>
	public const int ConsumerCreated = 25013;

	/// <summary>Creating SNS subscription.</summary>
	public const int SubscriptionCreating = 25014;

	/// <summary>Successfully created subscription.</summary>
	public const int SubscriptionCreated = 25015;

	/// <summary>Deleting SNS subscription.</summary>
	public const int SubscriptionDeleting = 25016;

	/// <summary>Successfully deleted subscription.</summary>
	public const int SubscriptionDeleted = 25017;

	/// <summary>Subscription not found.</summary>
	public const int SubscriptionNotFound = 25018;

	/// <summary>Health check failed for AWS message broker.</summary>
	public const int BrokerHealthCheckFailed = 25019;

	/// <summary>Destination validation failed.</summary>
	public const int DestinationValidationFailed = 25020;

	/// <summary>Disposing AWS message broker.</summary>
	public const int BrokerDisposing = 25021;

	/// <summary>AWS message broker disposed.</summary>
	public const int BrokerDisposed = 25022;

	// ========================================
	// 25100-25199: SQS Core
	// ========================================

	/// <summary>SQS message bus initializing.</summary>
	public const int SqsMessageBusInitializing = 25100;

	/// <summary>SQS message bus starting.</summary>
	public const int SqsMessageBusStarting = 25101;

	/// <summary>SQS message bus stopping.</summary>
	public const int SqsMessageBusStopping = 25102;

	/// <summary>SQS queue created.</summary>
	public const int SqsQueueCreated = 25110;

	/// <summary>SQS queue URL resolved.</summary>
	public const int SqsQueueUrlResolved = 25111;

	// SQS Message Bus
	/// <summary>Sent action to SQS.</summary>
	public const int SqsSentAction = 25120;

	/// <summary>Published event to SQS.</summary>
	public const int SqsPublishedEvent = 25121;

	/// <summary>Sent document to SQS.</summary>
	public const int SqsSentDocument = 25122;

	// ========================================
	// 25200-25299: SQS Publisher
	// ========================================

	/// <summary>SQS message published.</summary>
	public const int SqsMessagePublished = 25200;

	/// <summary>SQS batch published.</summary>
	public const int SqsBatchPublished = 25201;

	/// <summary>SQS message sent.</summary>
	public const int SqsMessageSent = 25202;

	/// <summary>SQS publish failed.</summary>
	public const int SqsPublishFailed = 25203;

	/// <summary>Disposing SQS publisher.</summary>
	public const int SqsPublisherDisposing = 25204;

	/// <summary>Channel message published.</summary>
	public const int SqsChannelMessagePublished = 25205;

	/// <summary>Channel publish error.</summary>
	public const int SqsChannelPublishError = 25206;

	/// <summary>SQS batch publish had partial failures.</summary>
	public const int SqsBatchPartialFailure = 25208;

	/// <summary>Channel processing error.</summary>
	public const int SqsChannelProcessingError = 25207;

	// ========================================
	// 25300-25399: SQS Consumer
	// ========================================

	/// <summary>SQS consumer started.</summary>
	public const int SqsConsumerStarted = 25300;

	/// <summary>SQS consumer stopped.</summary>
	public const int SqsConsumerStopped = 25301;

	/// <summary>SQS message received.</summary>
	public const int SqsMessageReceived = 25302;

	/// <summary>SQS message deleted.</summary>
	public const int SqsMessageDeleted = 25303;

	/// <summary>SQS message visibility extended.</summary>
	public const int SqsVisibilityExtended = 25304;

	/// <summary>SQS channel receiver starting.</summary>
	public const int SqsChannelReceiverStarting = 25305;

	/// <summary>SQS channel receiver stopping.</summary>
	public const int SqsChannelReceiverStopping = 25306;

	/// <summary>Failed to receive messages from SQS.</summary>
	public const int SqsReceiveMessagesFailed = 25310;

	/// <summary>Failed to acknowledge message.</summary>
	public const int SqsAcknowledgeMessageFailed = 25311;

	/// <summary>Failed to acknowledge batch.</summary>
	public const int SqsAcknowledgeBatchFailed = 25312;

	/// <summary>Failed to modify message visibility.</summary>
	public const int SqsModifyVisibilityFailed = 25313;

	/// <summary>Disposing SQS consumer.</summary>
	public const int SqsConsumerDisposing = 25314;

	/// <summary>Error processing message.</summary>
	public const int SqsMessageProcessingError = 25315;

	/// <summary>Error in consume loop.</summary>
	public const int SqsConsumeLoopError = 25316;

	/// <summary>Failed to write message to channel.</summary>
	public const int SqsFailedToWriteToChannel = 25317;

	/// <summary>Error in channel reader message pump.</summary>
	public const int SqsChannelReaderMessagePumpError = 25318;

	/// <summary>Failed to deserialize complete message context.</summary>
	public const int SqsFailedToDeserializeContext = 25319;

	/// <summary>Failed to decompress message body.</summary>
	public const int SqsMessageDecompressionFailed = 25330;

	/// <summary>SQS event source subscription started.</summary>
	public const int SqsEventSourceSubscribed = 25331;

	// Consumer logging (AwsSqsConsumerLogging)
	/// <summary>Message received from SQS.</summary>
	public const int ConsumerMessageReceived = 25320;

	/// <summary>Message processed successfully.</summary>
	public const int ConsumerMessageProcessed = 25321;

	/// <summary>Message deleted from queue.</summary>
	public const int ConsumerMessageDeleted = 25322;

	/// <summary>Context deserialization warning.</summary>
	public const int ConsumerContextDeserializationWarning = 25323;

	/// <summary>Batch received from SQS.</summary>
	public const int ConsumerBatchReceived = 25324;

	/// <summary>Batch processed.</summary>
	public const int ConsumerBatchProcessed = 25325;

	/// <summary>Visibility timeout extended.</summary>
	public const int ConsumerVisibilityExtended = 25326;

	/// <summary>Visibility timeout extension failed.</summary>
	public const int ConsumerVisibilityExtensionFailed = 25327;

	/// <summary>Message processing failed.</summary>
	public const int ConsumerMessageProcessingFailed = 25328;

	/// <summary>Message deletion failed.</summary>
	public const int ConsumerMessageDeletionFailed = 25329;

	// ========================================
	// 25400-25499: SQS Channels
	// ========================================

	/// <summary>SQS channel adapter initialized.</summary>
	public const int ChannelAdapterInitialized = 25400;

	/// <summary>SQS channel message processed.</summary>
	public const int ChannelMessageProcessed = 25401;

	/// <summary>SQS batch processor started.</summary>
	public const int BatchProcessorStarted = 25402;

	/// <summary>SQS batch processor completed.</summary>
	public const int BatchProcessorCompleted = 25403;

	// SqsChannelAdapter
	/// <summary>Starting SQS channel adapter.</summary>
	public const int ChannelAdapterStarting = 25420;

	/// <summary>Stopping SQS channel adapter.</summary>
	public const int ChannelAdapterStopping = 25421;

	/// <summary>SQS channel adapter stopped.</summary>
	public const int ChannelAdapterStopped = 25422;

	/// <summary>Channel poller starting.</summary>
	public const int ChannelPollerStarting = 25423;

	/// <summary>Channel poller error.</summary>
	public const int ChannelPollerError = 25424;

	/// <summary>Channel poller stopped.</summary>
	public const int ChannelPollerStopped = 25425;

	/// <summary>Send batch error.</summary>
	public const int ChannelSendBatchError = 25426;

	/// <summary>Send batch failed.</summary>
	public const int ChannelSendBatchFailed = 25427;

	/// <summary>Message batch send error.</summary>
	public const int ChannelMessageBatchSendError = 25428;

	// SqsChannelMessageProcessor
	/// <summary>Starting SQS channel processor.</summary>
	public const int ChannelProcessorStarting = 25430;

	/// <summary>SQS channel processor started.</summary>
	public const int ChannelProcessorStarted = 25431;

	/// <summary>Stopping SQS channel processor.</summary>
	public const int ChannelProcessorStopping = 25432;

	/// <summary>SQS channel processor stopped.</summary>
	public const int ChannelProcessorStopped = 25433;

	/// <summary>Channel worker starting.</summary>
	public const int ChannelWorkerStarting = 25434;

	/// <summary>Channel worker stopped.</summary>
	public const int ChannelWorkerStopped = 25435;

	/// <summary>Channel processing error.</summary>
	public const int ChannelProcessingError = 25436;

	/// <summary>Channel delete processor error.</summary>
	public const int ChannelDeleteProcessorError = 25437;

	/// <summary>Channel delete batch error.</summary>
	public const int ChannelDeleteBatchError = 25438;

	/// <summary>Channel delete message failed.</summary>
	public const int ChannelDeleteMessageFailed = 25439;

	/// <summary>Channel processing failed.</summary>
	public const int ChannelProcessingFailed = 25440;

	// Channel receiver logging (AwsSqsChannelReceiverLogging)
	/// <summary>Batch produced from SQS.</summary>
	public const int ChannelBatchProduced = 25410;

	/// <summary>Message acknowledged via channel.</summary>
	public const int ChannelMessageAcknowledged = 25411;

	/// <summary>Message rejected via channel.</summary>
	public const int ChannelMessageRejected = 25412;

	/// <summary>Message enqueued for batch delete.</summary>
	public const int ChannelMessageEnqueuedForDelete = 25413;

	/// <summary>Batch delete completed.</summary>
	public const int ChannelBatchDeleteCompleted = 25414;

	/// <summary>Batch delete failed.</summary>
	public const int ChannelBatchDeleteFailed = 25415;

	/// <summary>Message consumed via channel.</summary>
	public const int ChannelMessageConsumed = 25416;

	/// <summary>Message failed via channel.</summary>
	public const int ChannelMessageFailed = 25417;

	/// <summary>Failed to deserialize context via channel.</summary>
	public const int ChannelFailedToDeserializeContext = 25418;

	/// <summary>Failed to execute batch delete.</summary>
	public const int ChannelFailedToExecuteBatchDelete = 25419;

	// SqsBatchProcessorLogging
	/// <summary>Error processing message in batch processor.</summary>
	public const int BatchProcessorMessageError = 25450;

	/// <summary>Batch send failure.</summary>
	public const int BatchProcessorSendFailure = 25451;

	/// <summary>Batch send flush error.</summary>
	public const int BatchProcessorSendFlushError = 25452;

	/// <summary>Batch delete failure.</summary>
	public const int BatchProcessorDeleteFailure = 25453;

	/// <summary>Batch processed successfully.</summary>
	public const int BatchProcessorProcessed = 25454;

	/// <summary>Batch sent to SQS.</summary>
	public const int BatchProcessorSent = 25455;

	/// <summary>Batch deleted from SQS.</summary>
	public const int BatchProcessorDeleted = 25456;

	/// <summary>Batch accumulating.</summary>
	public const int BatchProcessorAccumulating = 25457;

	/// <summary>Batch flush triggered.</summary>
	public const int BatchProcessorFlushTriggered = 25458;

	// ========================================
	// 25500-25599: SQS High Throughput
	// ========================================

	/// <summary>High throughput processor started.</summary>
	public const int HighThroughputStarted = 25500;

	/// <summary>High throughput processor stopped.</summary>
	public const int HighThroughputStopped = 25501;

	/// <summary>Channel processor hosted service started.</summary>
	public const int HostedServiceStarted = 25502;

	/// <summary>Channel processor hosted service stopped.</summary>
	public const int HostedServiceStopped = 25503;

	/// <summary>Throughput metrics collected.</summary>
	public const int ThroughputMetrics = 25504;

	// HighThroughputSqsChannelProcessor
	/// <summary>High throughput processor starting.</summary>
	public const int HighThroughputProcessorStarting = 25510;

	/// <summary>High throughput poller started.</summary>
	public const int HighThroughputPollerStarted = 25511;

	/// <summary>High throughput poller error.</summary>
	public const int HighThroughputPollerError = 25512;

	/// <summary>High throughput poller stopped.</summary>
	public const int HighThroughputPollerStopped = 25513;

	/// <summary>High throughput batch delete error.</summary>
	public const int HighThroughputBatchDeleteError = 25514;

	// SqsChannelProcessorHostedService
	/// <summary>Channel processor hosted service starting.</summary>
	public const int HostedServiceStarting = 25520;

	/// <summary>Channel processor hosted service error.</summary>
	public const int HostedServiceError = 25521;

	// ========================================
	// 25600-25699: Long Polling
	// ========================================

	/// <summary>Long polling receiver started.</summary>
	public const int LongPollingStarted = 25600;

	/// <summary>Long polling receiver stopped.</summary>
	public const int LongPollingStopped = 25601;

	/// <summary>Long polling optimizer applied.</summary>
	public const int LongPollingOptimizerApplied = 25602;

	/// <summary>Channel long polling receiver started.</summary>
	public const int ChannelLongPollingStarted = 25603;

	/// <summary>Long poll timeout.</summary>
	public const int LongPollTimeout = 25604;

	/// <summary>Long poll completed.</summary>
	public const int LongPollCompleted = 25605;

	// ChannelLongPollingReceiver
	/// <summary>Long polling receiver starting.</summary>
	public const int LongPollingReceiverStarting = 25610;

	/// <summary>Long polling receiver stopping.</summary>
	public const int LongPollingReceiverStopping = 25611;

	/// <summary>Long polling receiver stopped with metrics.</summary>
	public const int LongPollingReceiverStoppedWithMetrics = 25612;

	/// <summary>Long poller started.</summary>
	public const int LongPollerStarted = 25613;

	/// <summary>Long poller error.</summary>
	public const int LongPollerError = 25614;

	/// <summary>Long poller stopped.</summary>
	public const int LongPollerStopped = 25615;

	/// <summary>Long poller count adjusting.</summary>
	public const int LongPollerCountAdjusting = 25616;

	/// <summary>Adaptive polling error.</summary>
	public const int AdaptivePollingError = 25617;

	// SqsLongPollingReceiver
	/// <summary>Receiving messages from SQS.</summary>
	public const int LongPollingReceivingMessages = 25620;

	/// <summary>Received messages from SQS.</summary>
	public const int LongPollingReceivedMessages = 25621;

	/// <summary>Error receiving messages.</summary>
	public const int LongPollingReceiveError = 25622;

	/// <summary>Message processing error.</summary>
	public const int LongPollingMessageProcessingError = 25623;

	/// <summary>Polling started for queue.</summary>
	public const int LongPollingPollingStarted = 25624;

	/// <summary>Polling error for queue.</summary>
	public const int LongPollingPollingError = 25625;

	/// <summary>Polling stopped for queue.</summary>
	public const int LongPollingPollingStopped = 25626;

	/// <summary>Visibility timeout optimized.</summary>
	public const int LongPollingVisibilityTimeoutOptimized = 25627;

	/// <summary>Visibility timeout optimization failed.</summary>
	public const int LongPollingVisibilityTimeoutOptimizationFailed = 25628;

	/// <summary>Delete message failed.</summary>
	public const int LongPollingDeleteMessageFailed = 25629;

	/// <summary>Batch delete failed.</summary>
	public const int LongPollingBatchDeleteFailed = 25630;

	/// <summary>Batch delete error.</summary>
	public const int LongPollingBatchDeleteError = 25631;

	/// <summary>Receiver started.</summary>
	public const int LongPollingReceiverStarted = 25632;

	/// <summary>Receiver stopped.</summary>
	public const int LongPollingReceiverStopped = 25633;

	// LongPollingOptimizer
	/// <summary>Health status error.</summary>
	public const int LongPollingHealthStatusError = 25640;

	// ========================================
	// 25700-25799: SNS
	// ========================================

	/// <summary>SNS message bus initializing.</summary>
	public const int SnsMessageBusInitializing = 25700;

	/// <summary>SNS message bus starting.</summary>
	public const int SnsMessageBusStarting = 25701;

	/// <summary>SNS message bus stopping.</summary>
	public const int SnsMessageBusStopping = 25702;

	/// <summary>SNS topic created.</summary>
	public const int SnsTopicCreated = 25710;

	/// <summary>SNS message published.</summary>
	public const int SnsMessagePublished = 25711;

	/// <summary>SNS subscription created.</summary>
	public const int SnsSubscriptionCreated = 25712;

	// SNS Message Bus
	/// <summary>Sent action via SNS.</summary>
	public const int SnsSentAction = 25720;

	/// <summary>Published event via SNS.</summary>
	public const int SnsPublishedEvent = 25721;

	/// <summary>Sent document via SNS.</summary>
	public const int SnsSentDocument = 25722;

	// SNS Publisher
	/// <summary>SNS publish failed.</summary>
	public const int SnsPublishFailed = 25730;

	/// <summary>SNS channel message published.</summary>
	public const int SnsChannelMessagePublished = 25731;

	/// <summary>SNS channel publish error.</summary>
	public const int SnsChannelPublishError = 25732;

	/// <summary>SNS channel processing error.</summary>
	public const int SnsChannelProcessingError = 25733;

	/// <summary>SNS scheduled messages not supported.</summary>
	public const int SnsScheduledNotSupported = 25734;

	/// <summary>Disposing SNS publisher.</summary>
	public const int SnsPublisherDisposing = 25735;

	// ========================================
	// 25800-25899: EventBridge
	// ========================================

	/// <summary>EventBridge message bus initializing.</summary>
	public const int EventBridgeInitializing = 25800;

	/// <summary>EventBridge message bus starting.</summary>
	public const int EventBridgeStarting = 25801;

	/// <summary>EventBridge message bus stopping.</summary>
	public const int EventBridgeStopping = 25802;

	/// <summary>EventBridge event published.</summary>
	public const int EventBridgeEventPublished = 25810;

	/// <summary>EventBridge rule created.</summary>
	public const int EventBridgeRuleCreated = 25811;

	// EventBridge Message Bus
	/// <summary>Published action to EventBridge.</summary>
	public const int EventBridgePublishedAction = 25820;

	/// <summary>Published event to EventBridge.</summary>
	public const int EventBridgePublishedEvent = 25821;

	/// <summary>Sent document to EventBridge.</summary>
	public const int EventBridgeSentDocument = 25822;

	// ========================================
	// 26010-26099: Transport Adapter
	// ========================================

	/// <summary>Transport adapter starting.</summary>
	public const int TransportAdapterStarting = 26010;

	/// <summary>Transport adapter stopping.</summary>
	public const int TransportAdapterStopping = 26011;

	/// <summary>Transport adapter receiving message.</summary>
	public const int TransportAdapterReceivingMessage = 26012;

	/// <summary>Transport adapter sending message.</summary>
	public const int TransportAdapterSendingMessage = 26013;

	/// <summary>Transport adapter message processing failed.</summary>
	public const int TransportAdapterMessageProcessingFailed = 26014;

	/// <summary>Transport adapter send failed.</summary>
	public const int TransportAdapterSendFailed = 26015;

	/// <summary>Transport adapter initialized.</summary>
	public const int TransportAdapterInitialized = 26016;

	/// <summary>Transport adapter disposed.</summary>
	public const int TransportAdapterDisposed = 26017;

	// ========================================
	// 26100-26119: ITransportSender / ITransportReceiver
	// ========================================

	/// <summary>Transport sender: message sent successfully.</summary>
	public const int TransportSenderMessageSent = 26100;

	/// <summary>Transport sender: send failed.</summary>
	public const int TransportSenderSendFailed = 26101;

	/// <summary>Transport sender: batch sent.</summary>
	public const int TransportSenderBatchSent = 26102;

	/// <summary>Transport sender: batch send failed.</summary>
	public const int TransportSenderBatchSendFailed = 26103;

	/// <summary>Transport sender: disposed.</summary>
	public const int TransportSenderDisposed = 26104;

	/// <summary>Transport receiver: message received.</summary>
	public const int TransportReceiverMessageReceived = 26110;

	/// <summary>Transport receiver: receive error.</summary>
	public const int TransportReceiverReceiveError = 26111;

	/// <summary>Transport receiver: message acknowledged.</summary>
	public const int TransportReceiverMessageAcknowledged = 26112;

	/// <summary>Transport receiver: acknowledge error.</summary>
	public const int TransportReceiverAcknowledgeError = 26113;

	/// <summary>Transport receiver: message rejected.</summary>
	public const int TransportReceiverMessageRejected = 26114;

	/// <summary>Transport receiver: message rejected with requeue.</summary>
	public const int TransportReceiverMessageRejectedRequeue = 26115;

	/// <summary>Transport receiver: reject error.</summary>
	public const int TransportReceiverRejectError = 26116;

	/// <summary>Transport receiver: disposed.</summary>
	public const int TransportReceiverDisposed = 26117;

	// ========================================
	// 26120-26127: ITransportSubscriber
	// ========================================

	/// <summary>Transport subscriber: subscription started.</summary>
	public const int TransportSubscriberStarted = 26120;

	/// <summary>Transport subscriber: message received.</summary>
	public const int TransportSubscriberMessageReceived = 26121;

	/// <summary>Transport subscriber: message acknowledged.</summary>
	public const int TransportSubscriberMessageAcknowledged = 26122;

	/// <summary>Transport subscriber: message rejected.</summary>
	public const int TransportSubscriberMessageRejected = 26123;

	/// <summary>Transport subscriber: message requeued.</summary>
	public const int TransportSubscriberMessageRequeued = 26124;

	/// <summary>Transport subscriber: error processing message.</summary>
	public const int TransportSubscriberError = 26125;

	/// <summary>Transport subscriber: subscription stopped.</summary>
	public const int TransportSubscriberStopped = 26126;

	/// <summary>Transport subscriber: disposed.</summary>
	public const int TransportSubscriberDisposed = 26127;
}
