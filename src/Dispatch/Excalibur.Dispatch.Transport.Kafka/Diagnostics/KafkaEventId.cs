// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Event IDs for Kafka transport (22000-22999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>22000-22099: Core (Producer, Consumer)</item>
/// <item>22100-22199: Consumer</item>
/// <item>22200-22299: Schema Registry</item>
/// <item>22300-22399: CloudEvents Integration</item>
/// <item>22400-22499: Error Handling</item>
/// <item>22500-22599: Partitioning</item>
/// </list>
/// </remarks>
public static class KafkaEventId
{
	// ========================================
	// 22000-22099: Core (Producer, Consumer)
	// ========================================

	/// <summary>Kafka producer created.</summary>
	public const int ProducerCreated = 22000;

	/// <summary>Kafka producer disposed.</summary>
	public const int ProducerDisposed = 22001;

	/// <summary>Kafka consumer created.</summary>
	public const int ConsumerCreated = 22002;

	/// <summary>Kafka consumer disposed.</summary>
	public const int ConsumerDisposed = 22003;

	/// <summary>Kafka message bus initializing.</summary>
	public const int MessageBusInitializing = 22004;

	/// <summary>Kafka message bus starting.</summary>
	public const int MessageBusStarting = 22005;

	/// <summary>Kafka message bus stopping.</summary>
	public const int MessageBusStopping = 22006;

	/// <summary>Kafka transport adapter initialized.</summary>
	public const int TransportAdapterInitialized = 22007;

	/// <summary>Kafka transport adapter starting.</summary>
	public const int TransportAdapterStarting = 22008;

	/// <summary>Kafka transport adapter stopping.</summary>
	public const int TransportAdapterStopping = 22009;

	/// <summary>Kafka topic created.</summary>
	public const int TopicCreated = 22010;

	/// <summary>Kafka topic subscribed.</summary>
	public const int TopicSubscribed = 22011;

	/// <summary>Kafka topic unsubscribed.</summary>
	public const int TopicUnsubscribed = 22012;

	/// <summary>Kafka transaction initialized.</summary>
	public const int TransactionInitialized = 22013;

	/// <summary>Kafka transaction begun.</summary>
	public const int TransactionBegin = 22014;

	/// <summary>Kafka transaction committed.</summary>
	public const int TransactionCommitted = 22015;

	/// <summary>Kafka transaction aborted.</summary>
	public const int TransactionAborted = 22016;

	// ========================================
	// 22100-22199: Consumer
	// ========================================

	/// <summary>Kafka consumer started.</summary>
	public const int ConsumerStarted = 22100;

	/// <summary>Kafka consumer stopped.</summary>
	public const int ConsumerStopped = 22101;

	/// <summary>Kafka message received.</summary>
	public const int MessageReceived = 22102;

	/// <summary>Kafka message committed.</summary>
	public const int MessageCommitted = 22103;

	/// <summary>Kafka consumer poll completed.</summary>
	public const int ConsumerPollCompleted = 22104;

	/// <summary>Kafka consumer rebalance.</summary>
	public const int ConsumerRebalance = 22105;

	/// <summary>Kafka partitions assigned.</summary>
	public const int PartitionsAssigned = 22106;

	/// <summary>Kafka partitions revoked.</summary>
	public const int PartitionsRevoked = 22107;

	/// <summary>Kafka channel consumer starting.</summary>
	public const int ChannelConsumerStarting = 22108;

	/// <summary>Kafka channel consumer stopping.</summary>
	public const int ChannelConsumerStopping = 22109;

	/// <summary>Kafka batch produced for processing.</summary>
	public const int BatchProduced = 22110;

	/// <summary>Kafka produce error.</summary>
	public const int ProduceError = 22111;

	/// <summary>Kafka message deserialization failure.</summary>
	public const int DeserializationFailure = 22112;

	/// <summary>Kafka context deserialization failure.</summary>
	public const int ContextDeserializationFailure = 22113;

	/// <summary>Kafka offsets committed.</summary>
	public const int OffsetsCommitted = 22114;

	/// <summary>Kafka commit offsets failure.</summary>
	public const int CommitOffsetsFailure = 22115;

	/// <summary>Kafka commit offsets error.</summary>
	public const int CommitOffsetsError = 22116;

	/// <summary>Kafka message conversion error.</summary>
	public const int MessageConversionError = 22117;

	/// <summary>Kafka message rejected.</summary>
	public const int MessageRejected = 22118;

	/// <summary>Kafka CloudEvent mapper resolved.</summary>
	public const int CloudEventMapperResolved = 22119;

	/// <summary>Kafka consume error.</summary>
	public const int ConsumeError = 22120;

	/// <summary>Kafka offset commit failed.</summary>
	public const int OffsetCommitFailed = 22121;

	/// <summary>Kafka partition end of file.</summary>
	public const int PartitionEof = 22122;

	/// <summary>Kafka consumer lag detected.</summary>
	public const int ConsumerLag = 22123;

	/// <summary>Kafka receiving message.</summary>
	public const int ReceivingMessage = 22124;

	/// <summary>Kafka sending message.</summary>
	public const int SendingMessage = 22125;

	/// <summary>Kafka message processing failed.</summary>
	public const int MessageProcessingFailed = 22126;

	/// <summary>Kafka send failed.</summary>
	public const int SendFailed = 22127;

	/// <summary>Kafka action sent.</summary>
	public const int ActionSent = 22128;

	/// <summary>Kafka event published.</summary>
	public const int EventPublished = 22129;

	/// <summary>Kafka document sent.</summary>
	public const int DocumentSent = 22130;

	/// <summary>Kafka publishing message.</summary>
	public const int PublishingMessage = 22131;

	/// <summary>Kafka message published.</summary>
	public const int MessagePublishedWithSize = 22132;

	/// <summary>Kafka event source subscribed to a topic.</summary>
	public const int EventSourceSubscribed = 22133;

	// ========================================
	// 22200-22299: Schema Registry
	// ========================================

	/// <summary>Schema registered.</summary>
	public const int SchemaRegistered = 22200;

	/// <summary>Schema retrieved.</summary>
	public const int SchemaRetrieved = 22201;

	/// <summary>Schema cached.</summary>
	public const int SchemaCached = 22202;

	/// <summary>Schema registry client created.</summary>
	public const int SchemaRegistryClientCreated = 22203;

	/// <summary>Schema validation passed.</summary>
	public const int SchemaValidationPassed = 22204;

	/// <summary>Schema validation failed.</summary>
	public const int SchemaValidationFailed = 22205;

	/// <summary>Schema evolution detected.</summary>
	public const int SchemaEvolutionDetected = 22206;

	/// <summary>JSON serializer created.</summary>
	public const int JsonSerializerCreated = 22210;

	/// <summary>Getting schema ID for subject.</summary>
	public const int GettingSchemaId = 22211;

	/// <summary>Schema ID retrieved for subject.</summary>
	public const int SchemaIdRetrieved = 22212;

	/// <summary>Getting schema by ID.</summary>
	public const int GettingSchemaById = 22213;

	/// <summary>Schema retrieval error.</summary>
	public const int SchemaRetrievalError = 22214;

	/// <summary>Registering schema for subject.</summary>
	public const int RegisteringSchema = 22215;

	/// <summary>Schema registration error.</summary>
	public const int SchemaRegistrationError = 22216;

	/// <summary>Checking schema compatibility.</summary>
	public const int CheckingCompatibility = 22217;

	/// <summary>Schema compatibility result.</summary>
	public const int CompatibilityResult = 22218;

	/// <summary>Schema compatibility check error.</summary>
	public const int CompatibilityCheckError = 22219;

	/// <summary>Schema cache hit.</summary>
	public const int SchemaCacheHit = 22220;

	/// <summary>Schema cache miss.</summary>
	public const int SchemaCacheMiss = 22221;

	/// <summary>Schema cache hit by ID.</summary>
	public const int SchemaCacheHitById = 22222;

	/// <summary>Schema cache miss by ID.</summary>
	public const int SchemaCacheMissById = 22223;

	/// <summary>Serializing message.</summary>
	public const int SerializingMessage = 22224;

	/// <summary>Schema ID resolved.</summary>
	public const int SchemaIdResolved = 22225;

	/// <summary>Serialization complete.</summary>
	public const int SerializationComplete = 22226;

	// 22230-22239: Zero-Copy Serialization (Sprint 478)

	/// <summary>Zero-copy serialization started.</summary>
	public const int ZeroCopySerializationStarted = 22230;

	/// <summary>Zero-copy header written to buffer.</summary>
	public const int ZeroCopyHeaderWritten = 22231;

	/// <summary>Zero-copy JSON payload written to buffer.</summary>
	public const int ZeroCopyPayloadWritten = 22232;

	/// <summary>Zero-copy serialization complete.</summary>
	public const int ZeroCopySerializationComplete = 22233;

	// ========================================
	// 22300-22399: CloudEvents Integration
	// ========================================

	/// <summary>Kafka CloudEvent received.</summary>
	public const int CloudEventReceived = 22300;

	/// <summary>Kafka CloudEvent published.</summary>
	public const int CloudEventPublished = 22301;

	/// <summary>Kafka CloudEvent adapter initialized.</summary>
	public const int CloudEventAdapterInitialized = 22302;

	/// <summary>Kafka CloudEvent to transport error.</summary>
	public const int CloudEventToTransportError = 22303;

	/// <summary>Kafka CloudEvent from transport error.</summary>
	public const int CloudEventFromTransportError = 22304;

	// ========================================
	// 22400-22499: Error Handling
	// ========================================

	/// <summary>Kafka producer error.</summary>
	public const int ProducerError = 22400;

	/// <summary>Kafka consumer error.</summary>
	public const int ConsumerError = 22401;

	/// <summary>Kafka delivery failed.</summary>
	public const int DeliveryFailed = 22402;

	/// <summary>Kafka deserialization error.</summary>
	public const int DeserializationError = 22403;

	/// <summary>Kafka schema registry error.</summary>
	public const int SchemaRegistryError = 22404;

	/// <summary>Kafka connection error.</summary>
	public const int ConnectionError = 22405;

	/// <summary>Kafka transaction error.</summary>
	public const int TransactionError = 22406;

	/// <summary>Kafka transaction abort failed.</summary>
	public const int TransactionAbortFailed = 22407;

	/// <summary>Kafka transaction initialization failed.</summary>
	public const int TransactionInitializationFailed = 22408;

	// ========================================
	// 22500-22599: Partitioning
	// ========================================

	/// <summary>Kafka partition selected.</summary>
	public const int PartitionSelected = 22500;

	/// <summary>Kafka message published.</summary>
	public const int MessagePublished = 22501;

	/// <summary>Kafka batch publish started.</summary>
	public const int BatchPublishStarted = 22502;

	/// <summary>Kafka batch publish completed.</summary>
	public const int BatchPublishCompleted = 22503;

	/// <summary>Kafka delivery report received.</summary>
	public const int DeliveryReportReceived = 22504;

	// ========================================
	// 22600-22699: Dead Letter Queue
	// ========================================

	/// <summary>Message moved to dead letter topic.</summary>
	public const int DlqMessageMoved = 22600;

	/// <summary>Failed to move message to dead letter topic.</summary>
	public const int DlqMoveFailed = 22601;

	/// <summary>Dead letter messages retrieved.</summary>
	public const int DlqMessagesRetrieved = 22602;

	/// <summary>Failed to retrieve dead letter messages.</summary>
	public const int DlqRetrieveFailed = 22603;

	/// <summary>Dead letter message reprocessed successfully.</summary>
	public const int DlqMessageReprocessed = 22604;

	/// <summary>Dead letter message reprocessing failed.</summary>
	public const int DlqReprocessFailed = 22605;

	/// <summary>Dead letter queue statistics retrieved.</summary>
	public const int DlqStatisticsRetrieved = 22606;

	/// <summary>Dead letter queue purged.</summary>
	public const int DlqPurged = 22607;

	/// <summary>Dead letter queue purge failed.</summary>
	public const int DlqPurgeFailed = 22608;

	/// <summary>Dead letter queue manager initialized.</summary>
	public const int DlqManagerInitialized = 22609;

	/// <summary>Dead letter message skipped by filter.</summary>
	public const int DlqMessageSkipped = 22610;

	/// <summary>Dead letter queue consumer started.</summary>
	public const int DlqConsumerStarted = 22611;

	/// <summary>Dead letter queue consumer stopped.</summary>
	public const int DlqConsumerStopped = 22612;

	/// <summary>Dead letter message produced to original topic during reprocessing.</summary>
	public const int DlqProducedToOriginalTopic = 22613;

	/// <summary>Dead letter messages peeked (non-destructive read) for statistics.</summary>
	public const int DlqMessagesPeeked = 22614;

	/// <summary>DLQ consumer subscribed to a topic.</summary>
	public const int DlqTopicSubscribed = 22615;

	// ========================================
	// 22700-22799: ITransportSender (was ICloudMessagePublisher)
	// ========================================

	/// <summary>Message sent to Kafka topic.</summary>
	public const int SenderMessagePublished = 22700;

	/// <summary>Batch of messages sent to Kafka topic.</summary>
	public const int SenderBatchPublished = 22701;

	/// <summary>Scheduled message sent to Kafka topic.</summary>
	public const int SenderMessageScheduled = 22702;

	/// <summary>Message send failed.</summary>
	public const int SenderPublishError = 22703;

	/// <summary>Batch send failed.</summary>
	public const int SenderBatchPublishError = 22704;

	/// <summary>Kafka producer flushed.</summary>
	public const int SenderFlushed = 22705;

	/// <summary>Kafka transport sender disposed.</summary>
	public const int SenderDisposed = 22706;

	// ========================================
	// 22800-22899: ITransportReceiver (was ICloudMessageConsumer)
	// ========================================

	/// <summary>Message received from Kafka topic.</summary>
	public const int ReceiverMessageReceived = 22800;

	/// <summary>Message acknowledged (offset committed).</summary>
	public const int ReceiverMessageAcknowledged = 22801;

	/// <summary>Message rejected.</summary>
	public const int ReceiverMessageRejected = 22802;

	/// <summary>Receiver started.</summary>
	public const int ReceiverStarted = 22803;

	/// <summary>Receiver stopped.</summary>
	public const int ReceiverStopped = 22804;

	/// <summary>Receive error.</summary>
	public const int ReceiverReceiveError = 22805;

	/// <summary>Acknowledge error.</summary>
	public const int ReceiverAcknowledgeError = 22806;

	/// <summary>Receiver visibility modified (not supported).</summary>
	public const int ReceiverVisibilityModified = 22807;

	/// <summary>Receiver message processing error.</summary>
	public const int ReceiverProcessingError = 22808;

	/// <summary>Receiver event source subscribed.</summary>
	public const int ReceiverEventSourceSubscribed = 22809;

	// ========================================
	// 22900-22919: ITransportSender / ITransportReceiver
	// ========================================

	/// <summary>Transport sender: message sent successfully.</summary>
	public const int TransportSenderMessageSent = 22900;

	/// <summary>Transport sender: send failed.</summary>
	public const int TransportSenderSendFailed = 22901;

	/// <summary>Transport sender: batch sent.</summary>
	public const int TransportSenderBatchSent = 22902;

	/// <summary>Transport sender: producer flushed.</summary>
	public const int TransportSenderFlushed = 22903;

	/// <summary>Transport sender: disposed.</summary>
	public const int TransportSenderDisposed = 22904;

	/// <summary>Transport receiver: message received.</summary>
	public const int TransportReceiverMessageReceived = 22910;

	/// <summary>Transport receiver: receive error.</summary>
	public const int TransportReceiverReceiveError = 22911;

	/// <summary>Transport receiver: message acknowledged.</summary>
	public const int TransportReceiverMessageAcknowledged = 22912;

	/// <summary>Transport receiver: acknowledge error.</summary>
	public const int TransportReceiverAcknowledgeError = 22913;

	/// <summary>Transport receiver: message rejected.</summary>
	public const int TransportReceiverMessageRejected = 22914;

	/// <summary>Transport receiver: message rejected with requeue.</summary>
	public const int TransportReceiverMessageRejectedRequeue = 22915;

	/// <summary>Transport receiver: disposed.</summary>
	public const int TransportReceiverDisposed = 22916;

	// ========================================
	// 22920-22927: ITransportSubscriber
	// ========================================

	/// <summary>Transport subscriber: subscription started.</summary>
	public const int TransportSubscriberStarted = 22920;

	/// <summary>Transport subscriber: message received.</summary>
	public const int TransportSubscriberMessageReceived = 22921;

	/// <summary>Transport subscriber: message acknowledged.</summary>
	public const int TransportSubscriberMessageAcknowledged = 22922;

	/// <summary>Transport subscriber: message rejected.</summary>
	public const int TransportSubscriberMessageRejected = 22923;

	/// <summary>Transport subscriber: message requeued.</summary>
	public const int TransportSubscriberMessageRequeued = 22924;

	/// <summary>Transport subscriber: error processing message.</summary>
	public const int TransportSubscriberError = 22925;

	/// <summary>Transport subscriber: subscription stopped.</summary>
	public const int TransportSubscriberStopped = 22926;

	/// <summary>Transport subscriber: disposed.</summary>
	public const int TransportSubscriberDisposed = 22927;
}
