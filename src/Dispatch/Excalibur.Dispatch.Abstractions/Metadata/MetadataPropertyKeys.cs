// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Well-known property keys for metadata stored in <see cref="IMessageMetadata.Properties"/>
/// and used by builder extension methods on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
/// <remarks>
/// These keys are used by the metadata extension methods (e.g., <c>MetadataIdentityExtensions</c>,
/// <c>MetadataRoutingExtensions</c>) to read and write domain-specific metadata from the Properties dictionary.
/// They are also used by builder extension methods (e.g., <c>MetadataBuilderRoutingExtensions</c>) to set
/// values via <see cref="IMessageMetadataBuilder.WithProperty"/>.
/// </remarks>
public static class MetadataPropertyKeys
{
	// Core (also on IMessageMetadata interface, used by builder extension methods)

	/// <summary> Property key for the source system. Also a core <see cref="IMessageMetadata"/> property. </summary>
	public const string Source = "Source";

	/// <summary> Property key for the message type. Also a core <see cref="IMessageMetadata"/> property. </summary>
	public const string MessageType = "MessageType";

	/// <summary> Property key for the content type. Also a core <see cref="IMessageMetadata"/> property. </summary>
	public const string ContentType = "ContentType";

	/// <summary> Property key for the created timestamp. Also a core <see cref="IMessageMetadata"/> property. </summary>
	public const string CreatedTimestampUtc = "CreatedTimestampUtc";

	// Identity/Security

	/// <summary> Property key for the external identifier. </summary>
	public const string ExternalId = "ExternalId";

	/// <summary> Property key for the W3C trace parent. </summary>
	public const string TraceParent = "TraceParent";

	/// <summary> Property key for the W3C trace state. </summary>
	public const string TraceState = "TraceState";

	/// <summary> Property key for the W3C baggage. </summary>
	public const string Baggage = "Baggage";

	/// <summary> Property key for the user identifier. </summary>
	public const string UserId = "UserId";

	/// <summary> Property key for the user roles collection. </summary>
	public const string Roles = "Roles";

	/// <summary> Property key for the security claims collection. </summary>
	public const string Claims = "Claims";

	/// <summary> Property key for the tenant identifier. </summary>
	public const string TenantId = "TenantId";

	// Versioning

	/// <summary> Property key for the content encoding. </summary>
	public const string ContentEncoding = "ContentEncoding";

	/// <summary> Property key for the message version. </summary>
	public const string MessageVersion = "MessageVersion";

	/// <summary> Property key for the serializer version. </summary>
	public const string SerializerVersion = "SerializerVersion";

	/// <summary> Property key for the contract version. </summary>
	public const string ContractVersion = "ContractVersion";

	// Routing

	/// <summary> Property key for the destination. </summary>
	public const string Destination = "Destination";

	/// <summary> Property key for the reply-to address. </summary>
	public const string ReplyTo = "ReplyTo";

	/// <summary> Property key for the session identifier. </summary>
	public const string SessionId = "SessionId";

	/// <summary> Property key for the partition key. </summary>
	public const string PartitionKey = "PartitionKey";

	/// <summary> Property key for the routing key. </summary>
	public const string RoutingKey = "RoutingKey";

	/// <summary> Property key for the group identifier. </summary>
	public const string GroupId = "GroupId";

	/// <summary> Property key for the group sequence number. </summary>
	public const string GroupSequence = "GroupSequence";

	// Temporal

	/// <summary> Property key for the sent timestamp. </summary>
	public const string SentTimestampUtc = "SentTimestampUtc";

	/// <summary> Property key for the received timestamp. </summary>
	public const string ReceivedTimestampUtc = "ReceivedTimestampUtc";

	/// <summary> Property key for the scheduled enqueue time. </summary>
	public const string ScheduledEnqueueTimeUtc = "ScheduledEnqueueTimeUtc";

	/// <summary> Property key for the time-to-live. </summary>
	public const string TimeToLive = "TimeToLive";

	/// <summary> Property key for the expiration timestamp. </summary>
	public const string ExpiresAtUtc = "ExpiresAtUtc";

	// Transport/Delivery

	/// <summary> Property key for the delivery count. </summary>
	public const string DeliveryCount = "DeliveryCount";

	/// <summary> Property key for the maximum delivery count. </summary>
	public const string MaxDeliveryCount = "MaxDeliveryCount";

	/// <summary> Property key for the last delivery error. </summary>
	public const string LastDeliveryError = "LastDeliveryError";

	/// <summary> Property key for the dead letter queue name. </summary>
	public const string DeadLetterQueue = "DeadLetterQueue";

	/// <summary> Property key for the dead letter reason. </summary>
	public const string DeadLetterReason = "DeadLetterReason";

	/// <summary> Property key for the dead letter error description. </summary>
	public const string DeadLetterErrorDescription = "DeadLetterErrorDescription";

	/// <summary> Property key for the message priority. </summary>
	public const string Priority = "Priority";

	/// <summary> Property key for the durable flag. </summary>
	public const string Durable = "Durable";

	/// <summary> Property key for the duplicate detection flag. </summary>
	public const string RequiresDuplicateDetection = "RequiresDuplicateDetection";

	/// <summary> Property key for the duplicate detection window. </summary>
	public const string DuplicateDetectionWindow = "DuplicateDetectionWindow";

	// Event Sourcing

	/// <summary> Property key for the aggregate identifier. </summary>
	public const string AggregateId = "AggregateId";

	/// <summary> Property key for the aggregate type. </summary>
	public const string AggregateType = "AggregateType";

	/// <summary> Property key for the aggregate version. </summary>
	public const string AggregateVersion = "AggregateVersion";

	/// <summary> Property key for the stream name. </summary>
	public const string StreamName = "StreamName";

	/// <summary> Property key for the stream position. </summary>
	public const string StreamPosition = "StreamPosition";

	/// <summary> Property key for the global position. </summary>
	public const string GlobalPosition = "GlobalPosition";

	/// <summary> Property key for the event type. </summary>
	public const string EventType = "EventType";

	/// <summary> Property key for the event version. </summary>
	public const string EventVersion = "EventVersion";

	// Removed collections (previously on interface)

	/// <summary> Property key for the attributes dictionary. </summary>
	public const string Attributes = "Attributes";

	/// <summary> Property key for the items dictionary. </summary>
	public const string Items = "Items";
}
