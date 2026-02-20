// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Default implementation of the unified message metadata interface.
/// </summary>
/// <remarks>
/// This class provides an immutable, thread-safe implementation of message metadata that consolidates all metadata requirements across the
/// Dispatch framework. Use the builder pattern via <see cref="ToBuilder" /> or <see cref="MessageMetadataBuilder" /> to create or modify instances.
/// </remarks>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed record MessageMetadata : IMessageMetadata
{
	private static readonly IReadOnlyCollection<string> EmptyRoles = new ReadOnlyCollection<string>([]);
	private static readonly IReadOnlyCollection<Claim> EmptyClaims = new ReadOnlyCollection<Claim>([]);

	private static readonly IReadOnlyDictionary<string, string> EmptyStringDictionary =
		new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

	private static readonly IReadOnlyDictionary<string, object> EmptyObjectDictionary =
		new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(StringComparer.Ordinal));

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageMetadata" /> class.
	/// </summary>
	public MessageMetadata()
	{
		MessageId = Guid.NewGuid().ToString();
		CorrelationId = MessageId;
		MessageType = string.Empty;
		ContentType = "application/json";
		SerializerVersion = "1.0";
		MessageVersion = "1.0";
		ContractVersion = "1.0.0";
		CreatedTimestampUtc = DateTimeOffset.UtcNow;
		Roles = EmptyRoles;
		Claims = EmptyClaims;
		Headers = EmptyStringDictionary;
		Attributes = EmptyObjectDictionary;
		Properties = EmptyObjectDictionary;
		Items = EmptyObjectDictionary;
	}

	// Core Identity Fields

	/// <summary>
	/// Gets the unique identifier for this message.
	/// </summary>
	/// <value> The current <see cref="MessageId" /> value. </value>
	public required string MessageId { get; init; }

	/// <summary>
	/// Gets the correlation identifier for tracking related messages.
	/// </summary>
	/// <value> The current <see cref="CorrelationId" /> value. </value>
	public required string CorrelationId { get; init; }

	/// <summary>
	/// Gets the causation identifier that tracks what caused this message to be created.
	/// </summary>
	/// <value> The current <see cref="CausationId" /> value. </value>
	public string? CausationId { get; init; }

	/// <summary>
	/// Gets an external system identifier for the message.
	/// </summary>
	/// <value> The current <see cref="ExternalId" /> value. </value>
	public string? ExternalId { get; init; }

	// Tracing and Observability

	/// <summary>
	/// Gets the W3C trace parent identifier for distributed tracing.
	/// </summary>
	/// <value> The current <see cref="TraceParent" /> value. </value>
	public string? TraceParent { get; init; }

	/// <summary>
	/// Gets the W3C trace state information for distributed tracing.
	/// </summary>
	/// <value> The current <see cref="TraceState" /> value. </value>
	public string? TraceState { get; init; }

	/// <summary>
	/// Gets the W3C baggage information for cross-service communication.
	/// </summary>
	/// <value> The current <see cref="Baggage" /> value. </value>
	public string? Baggage { get; init; }

	// User Identity and Security Context

	/// <summary>
	/// Gets the identifier of the user associated with the message.
	/// </summary>
	/// <value> The current <see cref="UserId" /> value. </value>
	public string? UserId { get; init; }

	/// <summary>
	/// Gets the collection of user roles associated with the message.
	/// </summary>
	/// <value> The current <see cref="Roles" /> value. </value>
	public IReadOnlyCollection<string> Roles { get; init; }

	/// <summary>
	/// Gets the collection of security claims associated with the message.
	/// </summary>
	/// <value> The current <see cref="Claims" /> value. </value>
	public IReadOnlyCollection<Claim> Claims { get; init; }

	/// <summary>
	/// Gets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value> The current <see cref="TenantId" /> value. </value>
	public string? TenantId { get; init; }

	// Message Type and Versioning

	/// <summary>
	/// Gets the type identifier for the message.
	/// </summary>
	/// <value> The current <see cref="MessageType" /> value. </value>
	public required string MessageType { get; init; }

	/// <summary>
	/// Gets the MIME type of the message content.
	/// </summary>
	/// <value> The current <see cref="ContentType" /> value. </value>
	public required string ContentType { get; init; }

	/// <summary>
	/// Gets the encoding used for the message content.
	/// </summary>
	/// <value> The current <see cref="ContentEncoding" /> value. </value>
	public string? ContentEncoding { get; init; }

	/// <summary>
	/// Gets the version of the message format.
	/// </summary>
	/// <value> The current <see cref="MessageVersion" /> value. </value>
	public required string MessageVersion { get; init; }

	/// <summary>
	/// Gets the version of the serializer used for the message.
	/// </summary>
	/// <value> The current <see cref="SerializerVersion" /> value. </value>
	public required string SerializerVersion { get; init; }

	/// <summary>
	/// Gets the version of the message contract.
	/// </summary>
	/// <value> The current <see cref="ContractVersion" /> value. </value>
	public required string ContractVersion { get; init; }

	// Routing and Transport

	/// <summary>
	/// Gets the source of the message.
	/// </summary>
	/// <value> The current <see cref="Source" /> value. </value>
	public string? Source { get; init; }

	/// <summary>
	/// Gets the destination for the message.
	/// </summary>
	/// <value> The current <see cref="Destination" /> value. </value>
	public string? Destination { get; init; }

	/// <summary>
	/// Gets the reply-to address for the message.
	/// </summary>
	/// <value> The current <see cref="ReplyTo" /> value. </value>
	public string? ReplyTo { get; init; }

	/// <summary>
	/// Gets the session identifier for the message.
	/// </summary>
	/// <value> The current <see cref="SessionId" /> value. </value>
	public string? SessionId { get; init; }

	/// <summary>
	/// Gets the partition key for message distribution.
	/// </summary>
	/// <value> The current <see cref="PartitionKey" /> value. </value>
	public string? PartitionKey { get; init; }

	/// <summary>
	/// Gets the routing key for message routing decisions.
	/// </summary>
	/// <value> The current <see cref="RoutingKey" /> value. </value>
	public string? RoutingKey { get; init; }

	/// <summary>
	/// Gets the group identifier for message grouping.
	/// </summary>
	/// <value> The current <see cref="GroupId" /> value. </value>
	public string? GroupId { get; init; }

	/// <summary>
	/// Gets the sequence number within the message group.
	/// </summary>
	/// <value> The current <see cref="GroupSequence" /> value. </value>
	public long? GroupSequence { get; init; }

	// Timing and Scheduling

	/// <summary>
	/// Gets the UTC timestamp when the message was created.
	/// </summary>
	/// <value> The current <see cref="CreatedTimestampUtc" /> value. </value>
	public required DateTimeOffset CreatedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was sent.
	/// </summary>
	/// <value> The current <see cref="SentTimestampUtc" /> value. </value>
	public DateTimeOffset? SentTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was received.
	/// </summary>
	/// <value> The current <see cref="ReceivedTimestampUtc" /> value. </value>
	public DateTimeOffset? ReceivedTimestampUtc { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	/// <value> The current <see cref="ScheduledEnqueueTimeUtc" /> value. </value>
	public DateTimeOffset? ScheduledEnqueueTimeUtc { get; init; }

	/// <summary>
	/// Gets the time-to-live duration for the message.
	/// </summary>
	/// <value> The current <see cref="TimeToLive" /> value. </value>
	public TimeSpan? TimeToLive { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message expires.
	/// </summary>
	/// <value> The current <see cref="ExpiresAtUtc" /> value. </value>
	public DateTimeOffset? ExpiresAtUtc { get; init; }

	// Delivery and Processing State

	/// <summary>
	/// Gets the number of delivery attempts for the message.
	/// </summary>
	/// <value> The current <see cref="DeliveryCount" /> value. </value>
	public int DeliveryCount { get; init; }

	/// <summary>
	/// Gets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <value> The current <see cref="MaxDeliveryCount" /> value. </value>
	public int? MaxDeliveryCount { get; init; }

	/// <summary>
	/// Gets the error message from the last delivery attempt.
	/// </summary>
	/// <value> The current <see cref="LastDeliveryError" /> value. </value>
	public string? LastDeliveryError { get; init; }

	/// <summary>
	/// Gets the name of the dead letter queue for failed messages.
	/// </summary>
	/// <value> The current <see cref="DeadLetterQueue" /> value. </value>
	public string? DeadLetterQueue { get; init; }

	/// <summary>
	/// Gets the reason why the message was sent to the dead letter queue.
	/// </summary>
	/// <value> The current <see cref="DeadLetterReason" /> value. </value>
	public string? DeadLetterReason { get; init; }

	/// <summary>
	/// Gets the detailed error description for dead letter messages.
	/// </summary>
	/// <value> The current <see cref="DeadLetterErrorDescription" /> value. </value>
	public string? DeadLetterErrorDescription { get; init; }

	// Event Sourcing Specific

	/// <summary>
	/// Gets the identifier of the aggregate for event sourcing.
	/// </summary>
	/// <value> The current <see cref="AggregateId" /> value. </value>
	public string? AggregateId { get; init; }

	/// <summary>
	/// Gets the type of the aggregate for event sourcing.
	/// </summary>
	/// <value> The current <see cref="AggregateType" /> value. </value>
	public string? AggregateType { get; init; }

	/// <summary>
	/// Gets the version of the aggregate for event sourcing.
	/// </summary>
	/// <value> The current <see cref="AggregateVersion" /> value. </value>
	public long? AggregateVersion { get; init; }

	/// <summary>
	/// Gets the name of the event stream.
	/// </summary>
	/// <value> The current <see cref="StreamName" /> value. </value>
	public string? StreamName { get; init; }

	/// <summary>
	/// Gets the position within the event stream.
	/// </summary>
	/// <value> The current <see cref="StreamPosition" /> value. </value>
	public long? StreamPosition { get; init; }

	/// <summary>
	/// Gets the global position in the event store.
	/// </summary>
	/// <value> The current <see cref="GlobalPosition" /> value. </value>
	public long? GlobalPosition { get; init; }

	/// <summary>
	/// Gets the type of the event for event sourcing.
	/// </summary>
	/// <value> The current <see cref="EventType" /> value. </value>
	public string? EventType { get; init; }

	/// <summary>
	/// Gets the version of the event for event sourcing.
	/// </summary>
	/// <value> The current <see cref="EventVersion" /> value. </value>
	public int? EventVersion { get; init; }

	// Priority and Quality of Service

	/// <summary>
	/// Gets the priority level of the message.
	/// </summary>
	/// <value> The current <see cref="Priority" /> value. </value>
	public int? Priority { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message is durable and should survive broker restarts.
	/// </summary>
	/// <value> The current <see cref="Durable" /> value. </value>
	public bool? Durable { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message requires duplicate detection.
	/// </summary>
	/// <value> The current <see cref="RequiresDuplicateDetection" /> value. </value>
	public bool? RequiresDuplicateDetection { get; init; }

	/// <summary>
	/// Gets the time window for duplicate detection.
	/// </summary>
	/// <value> The current <see cref="DuplicateDetectionWindow" /> value. </value>
	public TimeSpan? DuplicateDetectionWindow { get; init; }

	// Extensible Collections

	/// <summary>
	/// Gets the dictionary of message headers.
	/// </summary>
	/// <value> The current <see cref="Headers" /> value. </value>
	public IReadOnlyDictionary<string, string> Headers { get; init; }

	/// <summary>
	/// Gets the dictionary of message attributes.
	/// </summary>
	/// <value> The current <see cref="Attributes" /> value. </value>
	public IReadOnlyDictionary<string, object> Attributes { get; init; }

	/// <summary>
	/// Gets the dictionary of message properties.
	/// </summary>
	/// <value> The current <see cref="Properties" /> value. </value>
	public IReadOnlyDictionary<string, object> Properties { get; init; }

	/// <summary>
	/// Gets the dictionary of message items.
	/// </summary>
	/// <value> The current <see cref="Items" /> value. </value>
	public IReadOnlyDictionary<string, object> Items { get; init; }

	/// <inheritdoc />
	public IMessageMetadataBuilder ToBuilder()
	{
		var builder = new MessageMetadataBuilder()
			.WithMessageId(MessageId)
			.WithCorrelationId(CorrelationId)
			.WithCausationId(CausationId)
			.WithExternalId(ExternalId)
			.WithTraceParent(TraceParent)
			.WithTraceState(TraceState)
			.WithBaggage(Baggage)
			.WithUserId(UserId)
			.WithTenantId(TenantId)
			.WithMessageType(MessageType)
			.WithContentType(ContentType)
			.WithContentEncoding(ContentEncoding)
			.WithMessageVersion(MessageVersion)
			.WithSerializerVersion(SerializerVersion)
			.WithContractVersion(ContractVersion)
			.WithSource(Source)
			.WithDestination(Destination)
			.WithReplyTo(ReplyTo)
			.WithSessionId(SessionId)
			.WithPartitionKey(PartitionKey)
			.WithRoutingKey(RoutingKey)
			.WithGroupId(GroupId)
			.WithGroupSequence(GroupSequence)
			.WithCreatedTimestampUtc(CreatedTimestampUtc)
			.WithSentTimestampUtc(SentTimestampUtc)
			.WithReceivedTimestampUtc(ReceivedTimestampUtc)
			.WithScheduledEnqueueTimeUtc(ScheduledEnqueueTimeUtc)
			.WithTimeToLive(TimeToLive)
			.WithExpiresAtUtc(ExpiresAtUtc)
			.WithDeliveryCount(DeliveryCount)
			.WithMaxDeliveryCount(MaxDeliveryCount)
			.WithLastDeliveryError(LastDeliveryError)
			.WithDeadLetterQueue(DeadLetterQueue)
			.WithDeadLetterReason(DeadLetterReason)
			.WithDeadLetterErrorDescription(DeadLetterErrorDescription)
			.WithPriority(Priority)
			.WithDurable(Durable)
			.WithRequiresDuplicateDetection(RequiresDuplicateDetection)
			.WithDuplicateDetectionWindow(DuplicateDetectionWindow);

		// Add event sourcing metadata
		if (AggregateId != null || AggregateType != null || AggregateVersion != null || StreamName != null || StreamPosition != null)
		{
			_ = builder.WithEventSourcing(AggregateId, AggregateType, AggregateVersion, StreamName, StreamPosition);
		}

		if (GlobalPosition.HasValue)
		{
			_ = builder.WithGlobalPosition(GlobalPosition.Value);
		}

		if (EventType != null)
		{
			_ = builder.WithEventType(EventType);
		}

		if (EventVersion.HasValue)
		{
			_ = builder.WithEventVersion(EventVersion.Value);
		}

		// Add roles and claims
		_ = builder.WithRoles(Roles);
		_ = builder.WithClaims(Claims);

		// Add extensible collections
		_ = builder.AddHeaders(Headers);
		_ = builder.AddAttributes(Attributes);
		_ = builder.AddProperties(Properties);
		_ = builder.AddItems(Items);

		return builder;
	}

	/// <inheritdoc />
	public IMessageMetadata CloneMetadata() =>

		// Records provide value-based equality and cloning
		this with { };

	/// <inheritdoc />
	public bool Validate() => GetValidationErrors().Count == 0;

	/// <inheritdoc />
	public IReadOnlyCollection<string> GetValidationErrors()
	{
		var errors = new List<string>();

		// Required fields validation
		if (string.IsNullOrWhiteSpace(MessageId))
		{
			errors.Add("MessageId is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(CorrelationId))
		{
			errors.Add("CorrelationId is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(MessageType))
		{
			errors.Add("MessageType is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(ContentType))
		{
			errors.Add("ContentType is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(MessageVersion))
		{
			errors.Add("MessageVersion is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(SerializerVersion))
		{
			errors.Add("SerializerVersion is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(ContractVersion))
		{
			errors.Add("ContractVersion is required and cannot be empty.");
		}

		// Logical validation
		if (DeliveryCount < 0)
		{
			errors.Add("DeliveryCount cannot be negative.");
		}

		if (MaxDeliveryCount is <= 0)
		{
			errors.Add("MaxDeliveryCount must be greater than zero if specified.");
		}

		if (Priority is < 0)
		{
			errors.Add("Priority cannot be negative.");
		}

		if (TimeToLive <= TimeSpan.Zero)
		{
			errors.Add("TimeToLive must be positive if specified.");
		}

		if (ExpiresAtUtc.HasValue && SentTimestampUtc.HasValue && ExpiresAtUtc.Value <= SentTimestampUtc.Value)
		{
			errors.Add("ExpiresAtUtc must be after SentTimestampUtc.");
		}

		if (ScheduledEnqueueTimeUtc.HasValue && CreatedTimestampUtc > ScheduledEnqueueTimeUtc.Value)
		{
			errors.Add("ScheduledEnqueueTimeUtc cannot be before CreatedTimestampUtc.");
		}

		if (AggregateVersion is < 0)
		{
			errors.Add("AggregateVersion cannot be negative.");
		}

		if (StreamPosition is < 0)
		{
			errors.Add("StreamPosition cannot be negative.");
		}

		if (GlobalPosition is < 0)
		{
			errors.Add("GlobalPosition cannot be negative.");
		}

		if (EventVersion is < 0)
		{
			errors.Add("EventVersion cannot be negative.");
		}

		if (GroupSequence is < 0)
		{
			errors.Add("GroupSequence cannot be negative.");
		}

		return errors.AsReadOnly();
	}

	/// <summary>
	/// Creates a new metadata instance with default values.
	/// </summary>
	/// <returns> A new <see cref="MessageMetadata" /> instance with default values. </returns>
	public static MessageMetadata CreateDefault()
	{
		var messageId = Guid.NewGuid().ToString();
		return new MessageMetadata
		{
			MessageId = messageId,
			CorrelationId = messageId,
			MessageType = "Unknown",
			ContentType = "application/json",
			SerializerVersion = "1.0",
			MessageVersion = "1.0",
			ContractVersion = "1.0.0",
			CreatedTimestampUtc = DateTimeOffset.UtcNow,
		};
	}

	/// <summary>
	/// Creates a new metadata instance from an existing IMessageMetadata.
	/// </summary>
	/// <param name="legacy"> The legacy metadata to convert. </param>
	/// <param name="context"> Optional message context for additional metadata. </param>
	/// <returns> A new <see cref="MessageMetadata" /> instance converted from the legacy metadata. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when legacy is null. </exception>
	public static MessageMetadata FromLegacyMetadata(IMessageMetadata legacy, IMessageContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(legacy);

		var builder = new MessageMetadataBuilder()
			.WithCorrelationId(legacy.CorrelationId)
			.WithCausationId(legacy.CausationId)
			.WithTraceParent(legacy.TraceParent)
			.WithTenantId(legacy.TenantId)
			.WithUserId(legacy.UserId)
			.WithContentType(legacy.ContentType)
			.WithSerializerVersion(legacy.SerializerVersion)
			.WithMessageVersion(legacy.MessageVersion)
			.WithContractVersion(legacy.ContractVersion);

		// If we have a context, pull additional metadata from it
		if (context != null)
		{
			_ = builder
				.WithMessageId(context.MessageId ?? Guid.NewGuid().ToString())
				.WithExternalId(context.ExternalId)
				.WithSource(context.Source)
				.WithMessageType(context.MessageType ?? "Unknown")
				.WithDeliveryCount(context.DeliveryCount)
				.WithPartitionKey(context.PartitionKey())
				.WithReplyTo(context.ReplyTo());

			if (context.ReceivedTimestampUtc != default)
			{
				_ = builder.WithReceivedTimestampUtc(context.ReceivedTimestampUtc);
			}

			if (context.SentTimestampUtc.HasValue)
			{
				_ = builder.WithSentTimestampUtc(context.SentTimestampUtc.Value);
			}

			// Copy items collection
			if (context.Items.Count > 0)
			{
				_ = builder.AddItems(context.Items);
			}
		}
		else
		{
			// Provide reasonable defaults when no context is available
			_ = builder
				.WithMessageId(Guid.NewGuid().ToString())
				.WithMessageType("Unknown");
		}

		return (MessageMetadata)builder.Build();
	}
}
