// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Default implementation of the unified message metadata interface.
/// </summary>
/// <remarks>
/// <para>
/// This class provides an immutable, thread-safe implementation of message metadata that consolidates all metadata requirements across the
/// Excalibur framework. Use the builder pattern via <see cref="ToBuilder" /> or <see cref="MessageMetadataBuilder" /> to create or modify instances.
/// </para>
/// <para>
/// The core dispatch identity fields remain on the root to satisfy the <see cref="IMessageMetadata"/> contract.
/// All other metadata is composed into focused, each-at-most-ten-property value types
/// (<see cref="MessageIdentity"/>, <see cref="MessageRouting"/>, <see cref="MessageTiming"/>,
/// <see cref="MessageObservability"/>, <see cref="MessageDelivery"/>, <see cref="MessageEventSourcing"/>,
/// <see cref="MessageSecurity"/>) following the Microsoft-first sub-option composition pattern.
/// When accessed through the <see cref="IMessageMetadata"/> interface, non-core values are also
/// available via extension methods that read from the <see cref="Properties"/> dictionary.
/// </para>
/// <para>
/// The wire (JSON) shape is preserved as a flat object via <see cref="MessageMetadataJsonConverter"/>
/// so composing the groups does not change serialization output for consumers.
/// </para>
/// </remarks>
[JsonConverter(typeof(MessageMetadataJsonConverter))]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed record MessageMetadata : IMessageMetadata
{
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
		CreatedTimestampUtc = DateTimeOffset.UtcNow;
		Headers = EmptyStringDictionary;
		Attributes = EmptyObjectDictionary;
		Properties = EmptyObjectDictionary;
		Items = EmptyObjectDictionary;
		Identity = new MessageIdentity { MessageVersion = "1.0", SerializerVersion = "1.0", ContractVersion = "1.0.0" };
		Security = new MessageSecurity();
	}

	// ===== Core Identity Fields (on IMessageMetadata interface) =====

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
	/// Gets the source of the message.
	/// </summary>
	/// <value> The current <see cref="Source" /> value. </value>
	public string? Source { get; init; }

	/// <summary>
	/// Gets the UTC timestamp when the message was created.
	/// </summary>
	/// <value> The current <see cref="CreatedTimestampUtc" /> value. </value>
	public required DateTimeOffset CreatedTimestampUtc { get; init; }

	// ===== Collections (on IMessageMetadata interface) =====

	/// <summary>
	/// Gets the dictionary of message properties, including moved metadata fields.
	/// </summary>
	/// <value> The current <see cref="Properties" /> value. </value>
	public IReadOnlyDictionary<string, object> Properties { get; init; }

	/// <summary>
	/// Gets the dictionary of message headers.
	/// </summary>
	/// <value> The current <see cref="Headers" /> value. </value>
	public IReadOnlyDictionary<string, string> Headers { get; init; }

	// ===== Extensibility bags (record-only) =====

	/// <summary>
	/// Gets the dictionary of message attributes.
	/// </summary>
	/// <value> The current <see cref="Attributes" /> value. </value>
	public IReadOnlyDictionary<string, object> Attributes { get; init; }

	/// <summary>
	/// Gets the dictionary of message items.
	/// </summary>
	/// <value> The current <see cref="Items" /> value. </value>
	public IReadOnlyDictionary<string, object> Items { get; init; }

	// ===== Composed focused value-type groups (each <=10 properties) =====

	/// <summary>
	/// Gets the supplemental identity and versioning metadata group.
	/// </summary>
	/// <value> The current <see cref="Identity" /> value. </value>
	public MessageIdentity Identity { get; init; }

	/// <summary>
	/// Gets the routing and addressing metadata group.
	/// </summary>
	/// <value> The current <see cref="Routing" /> value. </value>
	public MessageRouting Routing { get; init; }

	/// <summary>
	/// Gets the temporal metadata group.
	/// </summary>
	/// <value> The current <see cref="Timing" /> value. </value>
	public MessageTiming Timing { get; init; }

	/// <summary>
	/// Gets the distributed-tracing observability metadata group.
	/// </summary>
	/// <value> The current <see cref="Observability" /> value. </value>
	public MessageObservability Observability { get; init; }

	/// <summary>
	/// Gets the delivery and transport-reliability metadata group.
	/// </summary>
	/// <value> The current <see cref="Delivery" /> value. </value>
	public MessageDelivery Delivery { get; init; }

	/// <summary>
	/// Gets the event-sourcing metadata group.
	/// </summary>
	/// <value> The current <see cref="EventSourcing" /> value. </value>
	public MessageEventSourcing EventSourcing { get; init; }

	/// <summary>
	/// Gets the security and tenancy metadata group.
	/// </summary>
	/// <value> The current <see cref="Security" /> value. </value>
	public MessageSecurity Security { get; init; }

	// ===== Interface methods =====

	/// <inheritdoc />
	public IMessageMetadataBuilder ToBuilder()
	{
		var builder = new MessageMetadataBuilder()
			.WithMessageId(MessageId)
			.WithCorrelationId(CorrelationId)
			.WithCausationId(CausationId)
			.WithExternalId(Identity.ExternalId)
			.WithTraceParent(Observability.TraceParent)
			.WithTraceState(Observability.TraceState)
			.WithBaggage(Observability.Baggage)
			.WithUserId(Security.UserId)
			.WithTenantId(Security.TenantId)
			.WithMessageType(MessageType)
			.WithContentType(ContentType)
			.WithContentEncoding(Identity.ContentEncoding)
			.WithMessageVersion(Identity.MessageVersion ?? "1.0")
			.WithSerializerVersion(Identity.SerializerVersion ?? "1.0")
			.WithContractVersion(Identity.ContractVersion ?? "1.0.0")
			.WithSource(Source)
			.WithDestination(Routing.Destination)
			.WithReplyTo(Routing.ReplyTo)
			.WithSessionId(Routing.SessionId)
			.WithPartitionKey(Routing.PartitionKey)
			.WithRoutingKey(Routing.RoutingKey)
			.WithGroupId(Routing.GroupId)
			.WithGroupSequence(Routing.GroupSequence)
			.WithCreatedTimestampUtc(CreatedTimestampUtc)
			.WithSentTimestampUtc(Timing.SentTimestampUtc)
			.WithReceivedTimestampUtc(Timing.ReceivedTimestampUtc)
			.WithScheduledEnqueueTimeUtc(Timing.ScheduledEnqueueTimeUtc)
			.WithTimeToLive(Timing.TimeToLive)
			.WithExpiresAtUtc(Timing.ExpiresAtUtc)
			.WithDeliveryCount(Delivery.DeliveryCount)
			.WithMaxDeliveryCount(Delivery.MaxDeliveryCount)
			.WithLastDeliveryError(Delivery.LastDeliveryError)
			.WithDeadLetterQueue(Delivery.DeadLetterQueue)
			.WithDeadLetterReason(Delivery.DeadLetterReason)
			.WithDeadLetterErrorDescription(Delivery.DeadLetterErrorDescription)
			.WithPriority(Delivery.Priority)
			.WithDurable(Delivery.Durable)
			.WithRequiresDuplicateDetection(Delivery.RequiresDuplicateDetection)
			.WithDuplicateDetectionWindow(Delivery.DuplicateDetectionWindow);

		// Add event sourcing metadata
		if (EventSourcing.AggregateId != null
			|| EventSourcing.AggregateType != null
			|| EventSourcing.AggregateVersion != null
			|| EventSourcing.StreamName != null
			|| EventSourcing.StreamPosition != null)
		{
			_ = builder.WithEventSourcing(
				EventSourcing.AggregateId,
				EventSourcing.AggregateType,
				EventSourcing.AggregateVersion,
				EventSourcing.StreamName,
				EventSourcing.StreamPosition);
		}

		if (EventSourcing.GlobalPosition.HasValue)
		{
			_ = builder.WithGlobalPosition(EventSourcing.GlobalPosition.Value);
		}

		if (EventSourcing.EventType != null)
		{
			_ = builder.WithEventType(EventSourcing.EventType);
		}

		if (EventSourcing.EventVersion.HasValue)
		{
			_ = builder.WithEventVersion(EventSourcing.EventVersion.Value);
		}

		// Add roles and claims
		_ = builder.WithRoles(Security.Roles);
		_ = builder.WithClaims(Security.Claims);

		// Add extensible collections
		_ = builder.AddHeaders(Headers);
		_ = builder.AddAttributes(Attributes);
		_ = builder.AddItems(Items);

		// Add explicit properties (excluding well-known keys that are already handled via typed fields)
		var customProperties = Properties.Where(p => !IsWellKnownPropertyKey(p.Key));
		_ = builder.AddProperties(customProperties);

		return builder;
	}

	// ===== Record-only utility methods (not on interface) =====

	/// <summary>
	/// Creates a deep copy of this metadata instance.
	/// </summary>
	/// <returns> A new instance with the same values as this instance. </returns>
	public IMessageMetadata CloneMetadata() =>

		// Records provide value-based equality and cloning
		this with { };

	/// <summary>
	/// Validates that all required fields are present and valid.
	/// </summary>
	/// <returns> True if the metadata is valid; otherwise, false. </returns>
	public bool Validate() => GetValidationErrors().Count == 0;

	/// <summary>
	/// Gets validation errors if the metadata is invalid.
	/// </summary>
	/// <returns> A collection of validation error messages, or empty if valid. </returns>
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

		if (string.IsNullOrWhiteSpace(Identity.MessageVersion))
		{
			errors.Add("MessageVersion is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(Identity.SerializerVersion))
		{
			errors.Add("SerializerVersion is required and cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(Identity.ContractVersion))
		{
			errors.Add("ContractVersion is required and cannot be empty.");
		}

		// Logical validation
		if (Delivery.DeliveryCount < 0)
		{
			errors.Add("DeliveryCount cannot be negative.");
		}

		if (Delivery.MaxDeliveryCount is <= 0)
		{
			errors.Add("MaxDeliveryCount must be greater than zero if specified.");
		}

		if (Delivery.Priority is < 0)
		{
			errors.Add("Priority cannot be negative.");
		}

		if (Timing.TimeToLive <= TimeSpan.Zero)
		{
			errors.Add("TimeToLive must be positive if specified.");
		}

		if (Timing.ExpiresAtUtc.HasValue && Timing.SentTimestampUtc.HasValue && Timing.ExpiresAtUtc.Value <= Timing.SentTimestampUtc.Value)
		{
			errors.Add("ExpiresAtUtc must be after SentTimestampUtc.");
		}

		if (Timing.ScheduledEnqueueTimeUtc.HasValue && CreatedTimestampUtc > Timing.ScheduledEnqueueTimeUtc.Value)
		{
			errors.Add("ScheduledEnqueueTimeUtc cannot be before CreatedTimestampUtc.");
		}

		if (EventSourcing.AggregateVersion is < 0)
		{
			errors.Add("AggregateVersion cannot be negative.");
		}

		if (EventSourcing.StreamPosition is < 0)
		{
			errors.Add("StreamPosition cannot be negative.");
		}

		if (EventSourcing.GlobalPosition is < 0)
		{
			errors.Add("GlobalPosition cannot be negative.");
		}

		if (EventSourcing.EventVersion is < 0)
		{
			errors.Add("EventVersion cannot be negative.");
		}

		if (Routing.GroupSequence is < 0)
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
			CreatedTimestampUtc = DateTimeOffset.UtcNow,
			Identity = new MessageIdentity { SerializerVersion = "1.0", MessageVersion = "1.0", ContractVersion = "1.0.0" },
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
			.WithTraceParent(legacy.GetTraceParent())
			.WithTenantId(legacy.GetTenantId())
			.WithUserId(legacy.GetUserId())
			.WithContentType(legacy.ContentType)
			.WithSerializerVersion(legacy.GetSerializerVersion())
			.WithMessageVersion(legacy.GetMessageVersion())
			.WithContractVersion(legacy.GetContractVersion());

		// If we have a context, pull additional metadata from it
		if (context != null)
		{
			_ = builder
				.WithMessageId(context.MessageId ?? Guid.NewGuid().ToString())
				.WithExternalId(context.GetExternalId())
				.WithSource(context.GetSource())
				.WithMessageType(context.GetMessageType() ?? "Unknown")
				.WithDeliveryCount(context.GetDeliveryCount())
				.WithPartitionKey(context.PartitionKey())
				.WithReplyTo(context.ReplyTo());

			var receivedTimestamp = context.GetReceivedTimestampUtc();
			if (receivedTimestamp.HasValue && receivedTimestamp.Value != default)
			{
				_ = builder.WithReceivedTimestampUtc(receivedTimestamp.Value);
			}

			var sentTimestamp = context.GetSentTimestampUtc();
			if (sentTimestamp.HasValue)
			{
				_ = builder.WithSentTimestampUtc(sentTimestamp.Value);
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

	/// <summary>
	/// Checks whether a property key is a well-known key managed by the builder's typed fields.
	/// </summary>
	private static bool IsWellKnownPropertyKey(string key)
		=> key is MetadataPropertyKeys.Source
			or MetadataPropertyKeys.MessageType
			or MetadataPropertyKeys.ContentType
			or MetadataPropertyKeys.CreatedTimestampUtc
			or MetadataPropertyKeys.ExternalId
			or MetadataPropertyKeys.TraceParent
			or MetadataPropertyKeys.TraceState
			or MetadataPropertyKeys.Baggage
			or MetadataPropertyKeys.UserId
			or MetadataPropertyKeys.Roles
			or MetadataPropertyKeys.Claims
			or MetadataPropertyKeys.TenantId
			or MetadataPropertyKeys.ContentEncoding
			or MetadataPropertyKeys.MessageVersion
			or MetadataPropertyKeys.SerializerVersion
			or MetadataPropertyKeys.ContractVersion
			or MetadataPropertyKeys.Destination
			or MetadataPropertyKeys.ReplyTo
			or MetadataPropertyKeys.SessionId
			or MetadataPropertyKeys.PartitionKey
			or MetadataPropertyKeys.RoutingKey
			or MetadataPropertyKeys.GroupId
			or MetadataPropertyKeys.GroupSequence
			or MetadataPropertyKeys.SentTimestampUtc
			or MetadataPropertyKeys.ReceivedTimestampUtc
			or MetadataPropertyKeys.ScheduledEnqueueTimeUtc
			or MetadataPropertyKeys.TimeToLive
			or MetadataPropertyKeys.ExpiresAtUtc
			or MetadataPropertyKeys.DeliveryCount
			or MetadataPropertyKeys.MaxDeliveryCount
			or MetadataPropertyKeys.LastDeliveryError
			or MetadataPropertyKeys.DeadLetterQueue
			or MetadataPropertyKeys.DeadLetterReason
			or MetadataPropertyKeys.DeadLetterErrorDescription
			or MetadataPropertyKeys.Priority
			or MetadataPropertyKeys.Durable
			or MetadataPropertyKeys.RequiresDuplicateDetection
			or MetadataPropertyKeys.DuplicateDetectionWindow
			or MetadataPropertyKeys.AggregateId
			or MetadataPropertyKeys.AggregateType
			or MetadataPropertyKeys.AggregateVersion
			or MetadataPropertyKeys.StreamName
			or MetadataPropertyKeys.StreamPosition
			or MetadataPropertyKeys.GlobalPosition
			or MetadataPropertyKeys.EventType
			or MetadataPropertyKeys.EventVersion
			or MetadataPropertyKeys.Attributes
			or MetadataPropertyKeys.Items;
}
