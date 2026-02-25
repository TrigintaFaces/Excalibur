// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Event IDs for RabbitMQ transport (21000-21999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>21000-21099: Core (Connection, Channel)</item>
/// <item>21100-21199: Consumer</item>
/// <item>21200-21299: Publisher</item>
/// <item>21300-21399: CloudEvents Integration</item>
/// <item>21400-21499: Error Handling</item>
/// </list>
/// </remarks>
public static class RabbitMqEventId
{
	// ========================================
	// 21000-21099: Core (Connection, Channel)
	// ========================================

	/// <summary>RabbitMQ connection established.</summary>
	public const int ConnectionEstablished = 21000;

	/// <summary>RabbitMQ connection lost.</summary>
	public const int ConnectionLost = 21001;

	/// <summary>RabbitMQ connection recovered.</summary>
	public const int ConnectionRecovered = 21002;

	/// <summary>RabbitMQ channel created.</summary>
	public const int ChannelCreated = 21003;

	/// <summary>RabbitMQ channel closed.</summary>
	public const int ChannelClosed = 21004;

	/// <summary>RabbitMQ message bus initializing.</summary>
	public const int MessageBusInitializing = 21005;

	/// <summary>RabbitMQ message bus starting.</summary>
	public const int MessageBusStarting = 21006;

	/// <summary>RabbitMQ message bus stopping.</summary>
	public const int MessageBusStopping = 21007;

	/// <summary>RabbitMQ transport adapter initialized.</summary>
	public const int TransportAdapterInitialized = 21008;

	/// <summary>RabbitMQ transport adapter starting.</summary>
	public const int TransportAdapterStarting = 21009;

	/// <summary>RabbitMQ exchange declared.</summary>
	public const int ExchangeDeclared = 21010;

	/// <summary>RabbitMQ queue declared.</summary>
	public const int QueueDeclared = 21011;

	/// <summary>RabbitMQ binding created.</summary>
	public const int BindingCreated = 21012;

	/// <summary>RabbitMQ transport adapter stopping.</summary>
	public const int TransportAdapterStopping = 21013;

	/// <summary>RabbitMQ receiving message.</summary>
	public const int ReceivingMessage = 21014;

	/// <summary>RabbitMQ sending message.</summary>
	public const int SendingMessage = 21015;

	/// <summary>RabbitMQ message processing failed.</summary>
	public const int MessageProcessingFailed = 21016;

	/// <summary>RabbitMQ send failed.</summary>
	public const int SendFailed = 21017;

	// ========================================
	// 21100-21199: Consumer
	// ========================================

	/// <summary>RabbitMQ consumer started.</summary>
	public const int ConsumerStarted = 21100;

	/// <summary>RabbitMQ consumer stopped.</summary>
	public const int ConsumerStopped = 21101;

	/// <summary>RabbitMQ message received.</summary>
	public const int MessageReceived = 21102;

	/// <summary>RabbitMQ message acknowledged.</summary>
	public const int MessageAcknowledged = 21103;

	/// <summary>RabbitMQ message rejected.</summary>
	public const int MessageRejected = 21104;

	/// <summary>RabbitMQ message requeued.</summary>
	public const int MessageRequeued = 21105;

	/// <summary>RabbitMQ consumer cancelled.</summary>
	public const int ConsumerCancelled = 21106;

	/// <summary>RabbitMQ channel consumer starting.</summary>
	public const int ChannelConsumerStarting = 21107;

	/// <summary>RabbitMQ channel consumer stopping.</summary>
	public const int ChannelConsumerStopping = 21108;

	/// <summary>RabbitMQ basic consume registered.</summary>
	public const int BasicConsumeRegistered = 21109;

	/// <summary>RabbitMQ batch produced for processing.</summary>
	public const int BatchProduced = 21110;

	/// <summary>RabbitMQ message conversion error.</summary>
	public const int MessageConversionError = 21111;

	/// <summary>RabbitMQ context deserialization failure.</summary>
	public const int ContextDeserializationFailure = 21112;

	/// <summary>RabbitMQ messages acknowledged in batch.</summary>
	public const int MessagesAcknowledgedBatch = 21113;

	/// <summary>RabbitMQ acknowledgment error.</summary>
	public const int AcknowledgmentError = 21114;

	/// <summary>RabbitMQ batch processing error.</summary>
	public const int BatchProcessingError = 21115;

	/// <summary>RabbitMQ acknowledgment failed.</summary>
	public const int AcknowledgmentFailed = 21116;

	// ========================================
	// 21200-21299: Publisher
	// ========================================

	/// <summary>RabbitMQ message published.</summary>
	public const int MessagePublished = 21200;

	/// <summary>RabbitMQ message publish confirmed.</summary>
	public const int PublishConfirmed = 21201;

	/// <summary>RabbitMQ message publish failed.</summary>
	public const int PublishFailed = 21202;

	/// <summary>RabbitMQ batch publish started.</summary>
	public const int BatchPublishStarted = 21203;

	/// <summary>RabbitMQ batch publish completed.</summary>
	public const int BatchPublishCompleted = 21204;

	/// <summary>RabbitMQ action sent.</summary>
	public const int ActionSent = 21205;

	/// <summary>RabbitMQ event published.</summary>
	public const int EventPublished = 21206;

	/// <summary>RabbitMQ document sent.</summary>
	public const int DocumentSent = 21207;

	// ========================================
	// 21300-21399: CloudEvents Integration
	// ========================================

	/// <summary>RabbitMQ CloudEvent received.</summary>
	public const int CloudEventReceived = 21300;

	/// <summary>RabbitMQ CloudEvent published.</summary>
	public const int CloudEventPublished = 21301;

	/// <summary>RabbitMQ CloudEvent conversion.</summary>
	public const int CloudEventConversion = 21302;

	// ========================================
	// 21400-21499: Error Handling
	// ========================================

	/// <summary>RabbitMQ connection error.</summary>
	public const int ConnectionError = 21400;

	/// <summary>RabbitMQ channel error.</summary>
	public const int ChannelError = 21401;

	/// <summary>RabbitMQ consumer error.</summary>
	public const int ConsumerError = 21402;

	/// <summary>RabbitMQ publish error.</summary>
	public const int PublishError = 21403;

	/// <summary>RabbitMQ deserialization error.</summary>
	public const int DeserializationError = 21404;

	/// <summary>RabbitMQ connection blocked.</summary>
	public const int ConnectionBlocked = 21405;

	/// <summary>RabbitMQ connection unblocked.</summary>
	public const int ConnectionUnblocked = 21406;

	// ========================================
	// 21500-21512: Dead Letter Queue
	// ========================================

	/// <summary>RabbitMQ DLQ manager initialized.</summary>
	public const int DlqManagerInitialized = 21500;

	/// <summary>RabbitMQ message moved to DLQ.</summary>
	public const int DlqMessageMoved = 21501;

	/// <summary>RabbitMQ DLQ move failed.</summary>
	public const int DlqMoveFailed = 21502;

	/// <summary>RabbitMQ DLQ messages retrieved.</summary>
	public const int DlqMessagesRetrieved = 21503;

	/// <summary>RabbitMQ DLQ retrieve failed.</summary>
	public const int DlqRetrieveFailed = 21504;

	/// <summary>RabbitMQ DLQ message reprocessed.</summary>
	public const int DlqMessageReprocessed = 21505;

	/// <summary>RabbitMQ DLQ reprocess failed.</summary>
	public const int DlqReprocessFailed = 21506;

	/// <summary>RabbitMQ DLQ statistics retrieved.</summary>
	public const int DlqStatisticsRetrieved = 21507;

	/// <summary>RabbitMQ DLQ purged.</summary>
	public const int DlqPurged = 21508;

	/// <summary>RabbitMQ DLQ purge failed.</summary>
	public const int DlqPurgeFailed = 21509;

	/// <summary>RabbitMQ DLQ message skipped during reprocessing.</summary>
	public const int DlqMessageSkipped = 21510;

	// ========================================
	// 21600-21699: ITransportSender (was ICloudMessagePublisher)
	// ========================================

	/// <summary>Message sent to RabbitMQ exchange.</summary>
	public const int SenderMessagePublished = 21600;

	/// <summary>Batch of messages sent to RabbitMQ exchange.</summary>
	public const int SenderBatchPublished = 21601;

	/// <summary>Scheduled message sent to RabbitMQ exchange.</summary>
	public const int SenderMessageScheduled = 21602;

	/// <summary>Message send failed.</summary>
	public const int SenderPublishError = 21603;

	/// <summary>Batch send failed.</summary>
	public const int SenderBatchPublishError = 21604;

	/// <summary>RabbitMQ publisher confirms flushed.</summary>
	public const int SenderFlushed = 21605;

	/// <summary>RabbitMQ transport sender disposed.</summary>
	public const int SenderDisposed = 21606;

	// ========================================
	// 21700-21799: ITransportReceiver (was ICloudMessageConsumer)
	// ========================================

	/// <summary>Message received from RabbitMQ queue.</summary>
	public const int ReceiverMessageReceived = 21700;

	/// <summary>Message acknowledged (delivery tag acked).</summary>
	public const int ReceiverMessageAcknowledged = 21701;

	/// <summary>Message rejected.</summary>
	public const int ReceiverMessageRejected = 21702;

	/// <summary>Receiver started.</summary>
	public const int ReceiverStarted = 21703;

	/// <summary>Receiver stopped.</summary>
	public const int ReceiverStopped = 21704;

	/// <summary>Receive error.</summary>
	public const int ReceiverReceiveError = 21705;

	/// <summary>Acknowledge error.</summary>
	public const int ReceiverAcknowledgeError = 21706;

	/// <summary>Receiver visibility modified (not natively supported).</summary>
	public const int ReceiverVisibilityModified = 21707;

	/// <summary>Receiver message processing error.</summary>
	public const int ReceiverProcessingError = 21708;

	/// <summary>Receiver event source subscribed.</summary>
	public const int ReceiverEventSourceSubscribed = 21709;

	// ========================================
	// 21800-21819: ITransportSender / ITransportReceiver
	// ========================================

	/// <summary>Transport sender: message sent successfully.</summary>
	public const int TransportSenderMessageSent = 21800;

	/// <summary>Transport sender: send failed.</summary>
	public const int TransportSenderSendFailed = 21801;

	/// <summary>Transport sender: batch sent.</summary>
	public const int TransportSenderBatchSent = 21802;

	/// <summary>Transport sender: disposed.</summary>
	public const int TransportSenderDisposed = 21803;

	/// <summary>Transport receiver: message received.</summary>
	public const int TransportReceiverMessageReceived = 21810;

	/// <summary>Transport receiver: receive error.</summary>
	public const int TransportReceiverReceiveError = 21811;

	/// <summary>Transport receiver: message acknowledged.</summary>
	public const int TransportReceiverMessageAcknowledged = 21812;

	/// <summary>Transport receiver: acknowledge error.</summary>
	public const int TransportReceiverAcknowledgeError = 21813;

	/// <summary>Transport receiver: message rejected.</summary>
	public const int TransportReceiverMessageRejected = 21814;

	/// <summary>Transport receiver: message rejected with requeue.</summary>
	public const int TransportReceiverMessageRejectedRequeue = 21815;

	/// <summary>Transport receiver: reject error.</summary>
	public const int TransportReceiverRejectError = 21816;

	/// <summary>Transport receiver: disposed.</summary>
	public const int TransportReceiverDisposed = 21817;

	// ========================================
	// 21820-21827: ITransportSubscriber
	// ========================================

	/// <summary>Transport subscriber: subscription started.</summary>
	public const int TransportSubscriberStarted = 21820;

	/// <summary>Transport subscriber: message received and dispatched to handler.</summary>
	public const int TransportSubscriberMessageReceived = 21821;

	/// <summary>Transport subscriber: message acknowledged after handler returned Acknowledge.</summary>
	public const int TransportSubscriberMessageAcknowledged = 21822;

	/// <summary>Transport subscriber: message rejected after handler returned Reject.</summary>
	public const int TransportSubscriberMessageRejected = 21823;

	/// <summary>Transport subscriber: message requeued after handler returned Requeue.</summary>
	public const int TransportSubscriberMessageRequeued = 21824;

	/// <summary>Transport subscriber: handler or processing error.</summary>
	public const int TransportSubscriberError = 21825;

	/// <summary>Transport subscriber: subscription stopped.</summary>
	public const int TransportSubscriberStopped = 21826;

	/// <summary>Transport subscriber: disposed.</summary>
	public const int TransportSubscriberDisposed = 21827;
}
