// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Core interface for message metadata across the Excalibur framework. Contains only the essential
/// envelope fields needed for dispatching and correlation. Domain-specific metadata (identity, routing,
/// temporal, transport, event sourcing) is accessed via extension methods on the Properties dictionary.
/// </summary>
/// <remarks>
/// <para>
/// This minimal interface follows the Microsoft design pattern of keeping interfaces small (max 5 methods + properties bag)
/// and using extension methods for optional/domain-specific concerns. Properties that were previously on this interface
/// are now available through typed extension methods in <c>Excalibur.Dispatch.Abstractions.Metadata</c>:
/// </para>
/// <list type="bullet">
/// <item><description><c>MetadataIdentityExtensions</c> -- ExternalId, TraceParent, TraceState, Baggage, UserId, Roles, Claims, TenantId</description></item>
/// <item><description><c>MetadataVersioningExtensions</c> -- ContentEncoding, MessageVersion, SerializerVersion, ContractVersion</description></item>
/// <item><description><c>MetadataRoutingExtensions</c> -- Destination, ReplyTo, SessionId, PartitionKey, RoutingKey, GroupId, GroupSequence</description></item>
/// <item><description><c>MetadataTemporalExtensions</c> -- SentTimestampUtc, ReceivedTimestampUtc, ScheduledEnqueueTimeUtc, TimeToLive, ExpiresAtUtc</description></item>
/// <item><description><c>MetadataTransportExtensions</c> -- DeliveryCount, MaxDeliveryCount, DeadLetter*, Priority, Durable, DuplicateDetection</description></item>
/// <item><description><c>MetadataEventSourcingExtensions</c> -- AggregateId, AggregateType, AggregateVersion, Stream*, GlobalPosition, EventType, EventVersion</description></item>
/// </list>
/// <para>Implementations should ensure thread-safety for concurrent access scenarios.</para>
/// </remarks>
public interface IMessageMetadata
{
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
	/// Gets the MIME type of the message payload serialization format.
	/// </summary>
	/// <remarks>
	/// Common values include "application/json", "application/x-msgpack", "application/x-protobuf", or "application/octet-stream". Used to
	/// select the appropriate deserializer.
	/// </remarks>
	string ContentType { get; init; }

	/// <summary>
	/// Gets the fully qualified type name of the message.
	/// </summary>
	/// <remarks>
	/// Contains the CLR type name for deserialization and routing. Format is typically "Namespace.TypeName, AssemblyName".
	/// </remarks>
	string MessageType { get; init; }

	/// <summary>
	/// Gets the source system or service that originated this message.
	/// </summary>
	/// <remarks>
	/// Typically contains the service name, application name, or endpoint that created the message. Useful for debugging and monitoring.
	/// </remarks>
	string? Source { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when this message was created.
	/// </summary>
	/// <remarks>
	/// Set by the sender to indicate when the message was originally created. Should always be in UTC to avoid timezone issues.
	/// </remarks>
	DateTimeOffset CreatedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the collection of custom properties for extensible metadata storage.
	/// </summary>
	/// <remarks>
	/// General-purpose extension point for additional metadata. Domain-specific metadata (identity, routing,
	/// temporal, transport, event sourcing) is stored here and accessed via typed extension methods.
	/// Implementations should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, object> Properties { get; init; }

	/// <summary>
	/// Gets the collection of custom headers for this message.
	/// </summary>
	/// <remarks>
	/// Used for provider-specific metadata that doesn't fit standard fields. Common in HTTP-based transports and cloud provider
	/// implementations. Implementations should return an empty dictionary rather than null.
	/// </remarks>
	IReadOnlyDictionary<string, string> Headers { get; init; }

	/// <summary>
	/// Creates a mutable builder from this metadata instance.
	/// </summary>
	/// <returns> A builder that can be used to create a modified copy of this metadata. </returns>
	IMessageMetadataBuilder ToBuilder();
}
