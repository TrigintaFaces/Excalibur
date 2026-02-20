// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Builder interface for creating immutable message metadata instances. Provides a fluent API for constructing message metadata with
/// comprehensive envelope properties, trace context, and extensibility points.
/// </summary>
public interface IMessageMetadataBuilder
{
	/// <summary>
	/// Gets an optional marker type for tooling.
	/// </summary>
	/// <value> The optional marker type for tooling. </value>
	Type? MarkerType { get; }

	// Core Identity

	/// <summary>
	/// Sets the message identifier and optionally the correlation identifier if not already set.
	/// </summary>
	/// <param name="messageId"> The unique message identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithMessageId(string messageId);

	/// <summary>
	/// Sets the correlation identifier for message tracing.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier to track related messages. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithCorrelationId(string correlationId);

	/// <summary>
	/// Sets the causation identifier linking this message to its immediate cause.
	/// </summary>
	/// <param name="causationId"> The identifier of the message that caused this message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithCausationId(string? causationId);

	/// <summary>
	/// Sets an external identifier from an external system or integration.
	/// </summary>
	/// <param name="externalId"> The external system identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithExternalId(string? externalId);

	// User & Tenant Context

	/// <summary>
	/// Sets the user identifier associated with this message.
	/// </summary>
	/// <param name="userId"> The identifier of the user. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithUserId(string? userId);

	/// <summary>
	/// Sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <param name="tenantId"> The identifier of the tenant. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithTenantId(string? tenantId);

	// Trace Context

	/// <summary>
	/// Sets the W3C trace parent for distributed tracing.
	/// </summary>
	/// <param name="traceParent"> The W3C trace parent header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithTraceParent(string? traceParent);

	/// <summary>
	/// Sets the W3C trace state for distributed tracing.
	/// </summary>
	/// <param name="traceState"> The W3C trace state header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithTraceState(string? traceState);

	/// <summary>
	/// Sets the W3C baggage for distributed tracing context propagation.
	/// </summary>
	/// <param name="baggage"> The W3C baggage header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithBaggage(string? baggage);

	// Message Type & Content

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	/// <param name="messageType"> The type identifier for the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithMessageType(string messageType);

	/// <summary>
	/// Sets the content type of the message payload.
	/// </summary>
	/// <param name="contentType"> The MIME type or content type identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithContentType(string contentType);

	/// <summary>
	/// Sets the serializer version used to encode the message.
	/// </summary>
	/// <param name="serializerVersion"> The version of the serializer. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithSerializerVersion(string? serializerVersion);

	/// <summary>
	/// Sets the message schema version.
	/// </summary>
	/// <param name="messageVersion"> The version of the message schema. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithMessageVersion(string? messageVersion);

	/// <summary>
	/// Sets the contract version for message compatibility.
	/// </summary>
	/// <param name="contractVersion"> The version of the message contract. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithContractVersion(string? contractVersion);

	/// <summary>
	/// Sets the content encoding of the message.
	/// </summary>
	/// <param name="contentEncoding"> The encoding used for the message content. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithContentEncoding(string? contentEncoding);

	// Routing & Destination

	/// <summary>
	/// Sets the source system or component that originated the message.
	/// </summary>
	/// <param name="source"> The identifier of the source system. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithSource(string? source);

	/// <summary>
	/// Sets the destination system or component for the message.
	/// </summary>
	/// <param name="destination"> The identifier of the destination system. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDestination(string? destination);

	/// <summary>
	/// Sets the session identifier for message grouping.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithSessionId(string? sessionId);

	/// <summary>
	/// Sets the partition key for message routing and ordering.
	/// </summary>
	/// <param name="partitionKey"> The partition key value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithPartitionKey(string? partitionKey);

	/// <summary>
	/// Sets the group identifier for message grouping.
	/// </summary>
	/// <param name="groupId"> The identifier for the message group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithGroupId(string? groupId);

	/// <summary>
	/// Sets the sequence number within the message group.
	/// </summary>
	/// <param name="groupSequence"> The sequence number within the group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithGroupSequence(long? groupSequence);

	/// <summary>
	/// Sets the reply-to address for request-response patterns.
	/// </summary>
	/// <param name="replyTo"> The reply-to address or identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithReplyTo(string? replyTo);

	/// <summary>
	/// Sets the routing key for message routing.
	/// </summary>
	/// <param name="routingKey"> The key used for message routing decisions. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithRoutingKey(string? routingKey);

	// Delivery & Timing

	/// <summary>
	/// Sets the delivery attempt count for retry tracking.
	/// </summary>
	/// <param name="deliveryCount"> The number of delivery attempts. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDeliveryCount(int deliveryCount);

	/// <summary>
	/// Sets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <param name="maxDeliveryCount"> The maximum delivery attempt count. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when max delivery count is not positive. </exception>
	IMessageMetadataBuilder WithMaxDeliveryCount(int? maxDeliveryCount);

	/// <summary>
	/// Sets the error message from the last delivery attempt.
	/// </summary>
	/// <param name="lastDeliveryError"> The error message from the most recent delivery attempt. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithLastDeliveryError(string? lastDeliveryError);

	/// <summary>
	/// Sets the name of the dead letter queue for failed messages.
	/// </summary>
	/// <param name="deadLetterQueue"> The name of the dead letter queue. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDeadLetterQueue(string? deadLetterQueue);

	/// <summary>
	/// Sets the UTC timestamp when the message was created.
	/// </summary>
	/// <param name="createdTimestampUtc"> The creation timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithCreatedTimestampUtc(DateTimeOffset createdTimestampUtc);

	/// <summary>
	/// Sets the timestamp when the message was received.
	/// </summary>
	/// <param name="receivedTimestampUtc"> The UTC timestamp of message receipt. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithReceivedTimestampUtc(DateTimeOffset? receivedTimestampUtc);

	/// <summary>
	/// Sets the timestamp when the message was sent.
	/// </summary>
	/// <param name="sentTimestampUtc"> The UTC timestamp of message sending. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithSentTimestampUtc(DateTimeOffset? sentTimestampUtc);

	/// <summary>
	/// Sets the UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	/// <param name="scheduledEnqueueTimeUtc"> The scheduled enqueue time in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithScheduledEnqueueTimeUtc(DateTimeOffset? scheduledEnqueueTimeUtc);

	/// <summary>
	/// Sets the time-to-live duration for the message.
	/// </summary>
	/// <param name="timeToLive"> The duration before the message expires. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithTimeToLive(TimeSpan? timeToLive);

	/// <summary>
	/// Sets the UTC timestamp when the message expires.
	/// </summary>
	/// <param name="expiresAtUtc"> The expiration timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithExpiresAtUtc(DateTimeOffset? expiresAtUtc);

	/// <summary>
	/// Sets the reason why the message was sent to the dead letter queue.
	/// </summary>
	/// <param name="deadLetterReason"> The reason for dead letter queue placement. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDeadLetterReason(string? deadLetterReason);

	/// <summary>
	/// Sets the detailed error description for dead letter messages.
	/// </summary>
	/// <param name="deadLetterErrorDescription"> The detailed error description. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDeadLetterErrorDescription(string? deadLetterErrorDescription);

	// Quality of Service

	/// <summary>
	/// Sets the priority level of the message.
	/// </summary>
	/// <param name="priority"> The message priority level (higher numbers indicate higher priority). </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when priority is negative. </exception>
	IMessageMetadataBuilder WithPriority(int? priority);

	/// <summary>
	/// Sets whether the message is durable and should survive broker restarts.
	/// </summary>
	/// <param name="durable"> True if the message should be persisted durably. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithDurable(bool? durable);

	/// <summary>
	/// Sets whether the message requires duplicate detection.
	/// </summary>
	/// <param name="requiresDuplicateDetection"> True if duplicate detection should be enabled. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithRequiresDuplicateDetection(bool? requiresDuplicateDetection);

	/// <summary>
	/// Sets the time window for duplicate detection.
	/// </summary>
	/// <param name="duplicateDetectionWindow"> The duration to check for duplicates. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when window is not positive. </exception>
	IMessageMetadataBuilder WithDuplicateDetectionWindow(TimeSpan? duplicateDetectionWindow);

	// Event Sourcing

	/// <summary>
	/// Sets event sourcing metadata for the message.
	/// </summary>
	/// <param name="aggregateId"> The identifier of the aggregate. </param>
	/// <param name="aggregateType"> The type of the aggregate. </param>
	/// <param name="aggregateVersion"> The version of the aggregate. </param>
	/// <param name="streamName"> The name of the event stream. </param>
	/// <param name="streamPosition"> The position within the stream. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithEventSourcing(
		string? aggregateId = null,
		string? aggregateType = null,
		long? aggregateVersion = null,
		string? streamName = null,
		long? streamPosition = null);

	/// <summary>
	/// Sets the global position in the event store.
	/// </summary>
	/// <param name="globalPosition"> The global position number. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when position is negative. </exception>
	IMessageMetadataBuilder WithGlobalPosition(long globalPosition);

	/// <summary>
	/// Sets the type of the event for event sourcing.
	/// </summary>
	/// <param name="eventType"> The type identifier of the event. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithEventType(string eventType);

	/// <summary>
	/// Sets the version of the event for event sourcing.
	/// </summary>
	/// <param name="eventVersion"> The version number of the event. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when version is negative. </exception>
	IMessageMetadataBuilder WithEventVersion(int eventVersion);

	// Headers & Attributes

	/// <summary>
	/// Adds a single header to the message metadata.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddHeader(string key, string value);

	/// <summary>
	/// Adds multiple headers to the message metadata.
	/// </summary>
	/// <param name="headers"> The collection of headers to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddHeaders(IEnumerable<KeyValuePair<string, string>> headers);

	/// <summary>
	/// Adds multiple attributes to the message metadata.
	/// </summary>
	/// <param name="attributes"> The collection of attributes to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddAttributes(IEnumerable<KeyValuePair<string, object>> attributes);

	/// <summary>
	/// Adds multiple properties to the message metadata.
	/// </summary>
	/// <param name="properties"> The collection of properties to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddProperties(IEnumerable<KeyValuePair<string, object>> properties);

	/// <summary>
	/// Adds multiple items to the message metadata dictionary.
	/// </summary>
	/// <param name="items"> The collection of items to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddItems(IEnumerable<KeyValuePair<string, object>> items);

	// Security

	/// <summary>
	/// Sets the security roles associated with the message.
	/// </summary>
	/// <param name="roles"> The collection of role identifiers. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithRoles(IEnumerable<string> roles);

	/// <summary>
	/// Sets the security claims associated with the message.
	/// </summary>
	/// <param name="claims"> The collection of security claims. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder WithClaims(IEnumerable<Claim>? claims);

	/// <summary>
	/// Adds a single security role to the message metadata.
	/// </summary>
	/// <param name="role"> The role identifier to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	IMessageMetadataBuilder AddRole(string role);

	// Build

	/// <summary>
	/// Builds the immutable message metadata instance from the configured values.
	/// </summary>
	/// <returns> An immutable <see cref="IMessageMetadata" /> instance. </returns>
	IMessageMetadata Build();
}
