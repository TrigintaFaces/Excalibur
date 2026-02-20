// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Unified interface for all message metadata across the Dispatch framework. This interface consolidates all metadata requirements from
/// various contexts including messaging, event sourcing, cloud providers, and multi-tenant scenarios.
/// </summary>
/// <remarks>
/// This interface provides a comprehensive metadata model that supports:
/// <list type="bullet">
/// <item> Standard messaging fields (MessageId, CorrelationId, etc.) </item>
/// <item> User identity and security context </item>
/// <item> Routing and transport information </item>
/// <item> Timing and scheduling metadata </item>
/// <item> Extensible collections for provider-specific data </item>
/// <item> Event sourcing and snapshot metadata </item>
/// </list>
/// Implementations should ensure thread-safety for concurrent access scenarios.
/// </remarks>
public interface IMessageMetadata
{
	// ===== Core Identity Fields =====

	/// <summary>
	/// Gets the unique identifier for this message instance.
	/// </summary>
	/// <remarks>
	/// This ID uniquely identifies a specific message and should remain constant through retries and redeliveries. Typically a GUID or UUID7.
	/// </remarks>
	string MessageId { get; init; }

	/// <summary>
	/// Gets the correlation identifier that groups related messages together.
	/// </summary>
	/// <remarks> Flows through all messages in a business transaction or workflow, enabling end-to-end tracing across service boundaries. </remarks>
	string CorrelationId { get; init; }

	/// <summary>
	/// Gets the identifier of the message that caused this message to be created.
	/// </summary>
	/// <remarks> Forms a causality chain for debugging. May be null for root messages that were not caused by another message. </remarks>
	string? CausationId { get; init; }

	/// <summary>
	/// Gets an external identifier for correlation with external systems.
	/// </summary>
	/// <remarks>
	/// Use this to store identifiers from external systems, APIs, or third-party services to maintain traceability across system boundaries.
	/// </remarks>
	string? ExternalId { get; init; }

	// ===== Tracing and Observability =====

	/// <summary>
	/// Gets the W3C trace context header for distributed tracing integration.
	/// </summary>
	/// <remarks>
	/// Contains trace-id and span-id in W3C format. Integrates with OpenTelemetry and other distributed tracing systems for observability.
	/// </remarks>
	string? TraceParent { get; init; }

	/// <summary>
	/// Gets the W3C trace state header for vendor-specific tracing data.
	/// </summary>
	/// <remarks> Contains vendor-specific trace context that complements TraceParent. Format follows W3C Trace Context specification. </remarks>
	string? TraceState { get; init; }

	/// <summary>
	/// Gets the baggage header for context propagation.
	/// </summary>
	/// <remarks>
	/// Contains key-value pairs that propagate across service boundaries. Used for passing user-defined context in distributed systems.
	/// </remarks>
	string? Baggage { get; init; }

	// ===== User Identity and Security Context =====

	/// <summary>
	/// Gets the identifier of the user who initiated this message.
	/// </summary>
	/// <remarks> Used for audit trails and authorization. May be null for system-initiated messages or anonymous operations. </remarks>
	string? UserId { get; init; }

	/// <summary>
	/// Gets the roles associated with the user context.
	/// </summary>
	/// <remarks>
	/// Contains the security roles for authorization decisions. Implementations should return an empty collection rather than null.
	/// </remarks>
	IReadOnlyCollection<string> Roles { get; init; }

	/// <summary>
	/// Gets the security claims associated with the message context.
	/// </summary>
	/// <remarks>
	/// Contains the full set of claims for fine-grained authorization. Implementations should return an empty collection rather than null.
	/// </remarks>
	IReadOnlyCollection<Claim> Claims { get; init; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant message routing and isolation.
	/// </summary>
	/// <remarks>
	/// Used to ensure messages are processed in the correct tenant context and to enforce data isolation in multi-tenant systems.
	/// </remarks>
	string? TenantId { get; init; }

	// ===== Message Type and Versioning =====

	/// <summary>
	/// Gets the fully qualified type name of the message.
	/// </summary>
	/// <remarks>
	/// Contains the CLR type name for deserialization and routing. Format is typically
	/// "Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement.TypeName, AssemblyName".
	/// </remarks>
	string MessageType { get; init; }

	/// <summary>
	/// Gets the MIME type of the message payload serialization format.
	/// </summary>
	/// <remarks>
	/// Common values include "application/json", "application/x-msgpack", "application/x-protobuf", or "application/octet-stream". Used to
	/// select the appropriate deserializer.
	/// </remarks>
	string ContentType { get; init; }

	/// <summary>
	/// Gets the encoding of the message content.
	/// </summary>
	/// <remarks> Specifies the character encoding (e.g., "utf-8", "utf-16"). If null, UTF-8 is assumed for text-based content types. </remarks>
	string? ContentEncoding { get; init; }

	/// <summary>
	/// Gets the schema version of the message payload structure.
	/// </summary>
	/// <remarks> Indicates the version of the message contract. Used by versioning middleware to apply migrations between versions. </remarks>
	string MessageVersion { get; init; }

	/// <summary>
	/// Gets the version of the serializer used to encode the message.
	/// </summary>
	/// <remarks>
	/// Enables backward compatibility when serialization libraries are upgraded. Format is typically "major.minor" (e.g., "1.0", "2.1").
	/// </remarks>
	string SerializerVersion { get; init; }

	/// <summary>
	/// Gets the overall API contract version this message adheres Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration.
	/// </summary>
	/// <remarks>
	/// Represents a higher-level versioning scheme that may encompass multiple message versions within a service or bounded context.
	/// </remarks>
	string ContractVersion { get; init; }

	// ===== Routing and Transport =====

	/// <summary>
	/// Gets the source system or service that originated this message.
	/// </summary>
	/// <remarks>
	/// Typically contains the service name, application name, or endpoint that created the message. Useful for debugging and monitoring.
	/// </remarks>
	string? Source { get; init; }

	/// <summary>
	/// Gets the intended destination for this message.
	/// </summary>
	/// <remarks> May contain a queue name, topic, exchange, or service endpoint. Used for routing decisions in message brokers. </remarks>
	string? Destination { get; init; }

	/// <summary>
	/// Gets the address where responses to this message should be sent.
	/// </summary>
	/// <remarks>
	/// Used in request-reply patterns to indicate where the response message should be routed. May contain a queue name, topic, or endpoint URL.
	/// </remarks>
	string? ReplyTo { get; init; }

	/// <summary>
	/// Gets the session identifier for ordered message processing.
	/// </summary>
	/// <remarks> Used by message brokers (e.g., Azure Service Bus) to ensure FIFO processing of related messages within a session. </remarks>
	string? SessionId { get; init; }

	/// <summary>
	/// Gets the partition key for message ordering and distribution.
	/// </summary>
	/// <remarks>
	/// Used by message brokers to ensure ordered processing of related messages and to distribute load across partitions. Messages with the
	/// same partition key are processed in order.
	/// </remarks>
	string? PartitionKey { get; init; }

	/// <summary>
	/// Gets the routing key for topic-based routing.
	/// </summary>
	/// <remarks>
	/// Used in publish-subscribe patterns (e.g., RabbitMQ) to route messages to appropriate subscribers based on routing rules.
	/// </remarks>
	string? RoutingKey { get; init; }

	/// <summary>
	/// Gets the group identifier for message grouping.
	/// </summary>
	/// <remarks> Used to group related messages for batch processing or to ensure they are handled by the same consumer instance. </remarks>
	string? GroupId { get; init; }

	/// <summary>
	/// Gets the sequence number within a group or session.
	/// </summary>
	/// <remarks> Used to maintain order within a group of related messages. Typically increments for each message in the group. </remarks>
	long? GroupSequence { get; init; }

	// ===== Timing and Scheduling =====

	/// <summary>
	/// Gets the UTC timestamp when this message was created.
	/// </summary>
	/// <remarks>
	/// Set by the sender to indicate when the message was originally created. Should always be in UTC to avoid timezone Handlers.TestInfrastructure.
	/// </remarks>
	DateTimeOffset CreatedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this message was sent.
	/// </summary>
	/// <remarks> May differ from CreatedTimestampUtc if the message was created but held before sending (e.g., in a batch or transaction). </remarks>
	DateTimeOffset? SentTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this message was received for processing.
	/// </summary>
	/// <remarks>
	/// Set by the messaging infrastructure when the message enters the pipeline. Used for performance monitoring and SLA tracking.
	/// </remarks>
	DateTimeOffset? ReceivedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this message should be delivered.
	/// </summary>
	/// <remarks> Used for scheduled/delayed message delivery. If set, the message should not be processed before this time. </remarks>
	DateTimeOffset? ScheduledEnqueueTimeUtc { get; init; }

	/// <summary>
	/// Gets the time-to-live for this message.
	/// </summary>
	/// <remarks>
	/// Indicates how long the message is valid. After this duration from SentTimestampUtc, the message should be discarded or dead-lettered.
	/// </remarks>
	TimeSpan? TimeToLive { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this message expires.
	/// </summary>
	/// <remarks> Calculated from SentTimestampUtc + TimeToLive or set explicitly. Messages past this time should not be processed. </remarks>
	DateTimeOffset? ExpiresAtUtc { get; init; }

	// ===== Delivery and Processing State =====

	/// <summary>
	/// Gets the number of times this message has been delivered.
	/// </summary>
	/// <remarks>
	/// Incremented on each delivery attempt. Used for retry logic and dead-letter handling when messages exceed maximum delivery attempts.
	/// </remarks>
	int DeliveryCount { get; init; }

	/// <summary>
	/// Gets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <remarks> After this many attempts, the message should be moved to a dead-letter queue or discarded based on configuration. </remarks>
	int? MaxDeliveryCount { get; init; }

	/// <summary>
	/// Gets the reason for the last delivery failure.
	/// </summary>
	/// <remarks> Contains error information from the last failed processing attempt. Useful for debugging and dead-letter queue analysis. </remarks>
	string? LastDeliveryError { get; init; }

	/// <summary>
	/// Gets the name of the dead-letter queue for this message.
	/// </summary>
	/// <remarks> Specifies where the message should be sent if it cannot be processed after maximum retry attempts. </remarks>
	string? DeadLetterQueue { get; init; }

	/// <summary>
	/// Gets the reason why the message was dead-lettered.
	/// </summary>
	/// <remarks> Set when a message is moved to the dead-letter queue to indicate why it could not be processed successfully. </remarks>
	string? DeadLetterReason { get; init; }

	/// <summary>
	/// Gets the error description for dead-lettered messages.
	/// </summary>
	/// <remarks> Provides detailed error information for messages in the dead-letter queue. </remarks>
	string? DeadLetterErrorDescription { get; init; }

	// ===== Event Sourcing Specific =====

	/// <summary>
	/// Gets the aggregate identifier for event sourcing scenarios.
	/// </summary>
	/// <remarks> Identifies the aggregate root that this event belongs to in event-sourced systems. </remarks>
	string? AggregateId { get; init; }

	/// <summary>
	/// Gets the type of the aggregate for event sourcing.
	/// </summary>
	/// <remarks> Specifies the type of aggregate root this event applies Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </remarks>
	string? AggregateType { get; init; }

	/// <summary>
	/// Gets the version of the aggregate after this event.
	/// </summary>
	/// <remarks> Used for optimistic concurrency control in event-sourced systems. Increments with each event applied to the aggregate. </remarks>
	long? AggregateVersion { get; init; }

	/// <summary>
	/// Gets the name of the event stream.
	/// </summary>
	/// <remarks> Identifies the stream this event belongs to in an event store. </remarks>
	string? StreamName { get; init; }

	/// <summary>
	/// Gets the position of this event in the stream.
	/// </summary>
	/// <remarks> The sequential position of this event within its stream. Used for ordering and idempotency checks. </remarks>
	long? StreamPosition { get; init; }

	/// <summary>
	/// Gets the global position of this event in the event store.
	/// </summary>
	/// <remarks> The position of this event across all streams in the event store. Used for global ordering and projections. </remarks>
	long? GlobalPosition { get; init; }

	/// <summary>
	/// Gets the event type for event sourcing.
	/// </summary>
	/// <remarks> A string identifier for the type of domain event. Used for event deserialization and handling dispatch. </remarks>
	string? EventType { get; init; }

	/// <summary>
	/// Gets the event version for schema evolution.
	/// </summary>
	/// <remarks> Version of the event schema to support event evolution and backward compatibility. </remarks>
	int? EventVersion { get; init; }

	// ===== Priority and Quality of Service =====

	/// <summary>
	/// Gets the priority level of this message.
	/// </summary>
	/// <remarks> Higher values indicate higher priority. Used by message brokers to prioritize message delivery and processing order. </remarks>
	int? Priority { get; init; }

	/// <summary>
	/// Gets a value indicating whether this message requires durable storage.
	/// </summary>
	/// <remarks>
	/// If true, the message must be persisted to durable storage before acknowledgment. If false, the message may be kept only in memory.
	/// </remarks>
	bool? Durable { get; init; }

	/// <summary>
	/// Gets a value indicating whether duplicate detection should be applied.
	/// </summary>
	/// <remarks> If true, the messaging infrastructure should detect and discard duplicate messages based on MessageId. </remarks>
	bool? RequiresDuplicateDetection { get; init; }

	/// <summary>
	/// Gets the window for duplicate detection.
	/// </summary>
	/// <remarks> How long the messaging system should remember message IDs for duplicate detection purposes. </remarks>
	TimeSpan? DuplicateDetectionWindow { get; init; }

	// ===== Extensible Collections =====

	/// <summary>
	/// Gets the collection of custom headers for this message.
	/// </summary>
	/// <remarks>
	/// Used for provider-specific metadata that doesn't fit standard fields. Common in HTTP-based transports and cloud provider
	/// implementations. Implementations should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, string> Headers { get; init; }

	/// <summary>
	/// Gets the collection of message attributes.
	/// </summary>
	/// <remarks>
	/// Used by cloud providers (e.g., AWS SQS/SNS) for message metadata. Values can be strings, numbers, or binary data. Implementations
	/// should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, object> Attributes { get; init; }

	/// <summary>
	/// Gets the collection of custom properties.
	/// </summary>
	/// <remarks>
	/// General-purpose extension point for additional metadata. Used by various message brokers for custom routing and filtering.
	/// Implementations should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, object> Properties { get; init; }

	/// <summary>
	/// Gets the collection of items for pipeline processing.
	/// </summary>
	/// <remarks>
	/// Used by middleware to share data during message processing. This collection is typically mutable during processing but should be
	/// immutable once the message is sent or stored. Implementations should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, object> Items { get; init; }

	// ===== Utility Methods =====

	/// <summary>
	/// Creates a mutable builder from this metadata instance.
	/// </summary>
	/// <returns> A builder that can be used to create a modified copy of this metadata. </returns>
	IMessageMetadataBuilder ToBuilder();

	/// <summary>
	/// Creates a deep copy of this metadata instance.
	/// </summary>
	/// <returns> A new instance with the same values as this instance. </returns>
	IMessageMetadata CloneMetadata();

	/// <summary>
	/// Validates that all required fields are present and valid.
	/// </summary>
	/// <returns> True if the metadata is valid; otherwise, false. </returns>
	bool Validate();

	/// <summary>
	/// Gets validation errors if the metadata is invalid.
	/// </summary>
	/// <returns> A collection of validation error messages, or empty if valid. </returns>
	IReadOnlyCollection<string> GetValidationErrors();
}
