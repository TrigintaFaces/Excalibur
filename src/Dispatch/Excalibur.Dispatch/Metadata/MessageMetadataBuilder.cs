// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Builder for creating and modifying unified message metadata instances.
/// </summary>
public sealed class MessageMetadataBuilder : IMessageMetadataBuilder
{
	/// <summary>
	/// Dictionary containing message headers.
	/// </summary>
	private readonly Dictionary<string, string> _headers = [];

	/// <summary>
	/// Dictionary containing message attributes.
	/// </summary>
	private readonly Dictionary<string, object> _attributes = [];

	/// <summary>
	/// Dictionary containing message properties.
	/// </summary>
	private readonly Dictionary<string, object> _properties = [];

	/// <summary>
	/// Dictionary containing message items.
	/// </summary>
	private readonly Dictionary<string, object> _items = [];

	/// <summary>
	/// List of user roles associated with the message.
	/// </summary>
	private readonly List<string> _roles = [];

	/// <summary>
	/// List of security claims associated with the message.
	/// </summary>
	private readonly List<Claim> _claims = [];

	/// <summary>
	/// The unique identifier for the message.
	/// </summary>
	private string _messageId = Guid.NewGuid().ToString();

	/// <summary>
	/// The correlation identifier for the message.
	/// </summary>
	private string _correlationId = string.Empty;

	/// <summary>
	/// The type of the message.
	/// </summary>
	private string _messageType = "Unknown";

	/// <summary>
	/// The content type of the message.
	/// </summary>
	private string _contentType = "application/json";

	/// <summary>
	/// The version of the serializer used for the message.
	/// </summary>
	private string _serializerVersion = "1.0";

	/// <summary>
	/// The version of the message format.
	/// </summary>
	private string _messageVersion = "1.0";

	/// <summary>
	/// The version of the message contract.
	/// </summary>
	private string _contractVersion = "1.0.0";

	/// <summary>
	/// The UTC timestamp when the message was created.
	/// </summary>
	private DateTimeOffset _createdTimestampUtc = DateTimeOffset.UtcNow;

	/// <summary>
	/// The causation identifier for the message.
	/// </summary>
	private string? _causationId;

	/// <summary>
	/// An external identifier for the message.
	/// </summary>
	private string? _externalId;

	/// <summary>
	/// The trace parent identifier for distributed tracing.
	/// </summary>
	private string? _traceParent;

	/// <summary>
	/// The trace state for distributed tracing.
	/// </summary>
	private string? _traceState;

	/// <summary>
	/// The baggage information for distributed tracing.
	/// </summary>
	private string? _baggage;

	/// <summary>
	/// The identifier of the user associated with the message.
	/// </summary>
	private string? _userId;

	/// <summary>
	/// The tenant identifier for multi-tenant scenarios.
	/// </summary>
	private string? _tenantId;

	/// <summary>
	/// The content encoding used for the message.
	/// </summary>
	private string? _contentEncoding;

	/// <summary>
	/// The source of the message.
	/// </summary>
	private string? _source;

	/// <summary>
	/// The destination for the message.
	/// </summary>
	private string? _destination;

	/// <summary>
	/// The reply-to address for the message.
	/// </summary>
	private string? _replyTo;

	/// <summary>
	/// The session identifier for the message.
	/// </summary>
	private string? _sessionId;

	/// <summary>
	/// The partition key for the message.
	/// </summary>
	private string? _partitionKey;

	/// <summary>
	/// The routing key for the message.
	/// </summary>
	private string? _routingKey;

	/// <summary>
	/// The group identifier for the message.
	/// </summary>
	private string? _groupId;

	/// <summary>
	/// The sequence number within the message group.
	/// </summary>
	private long? _groupSequence;

	/// <summary>
	/// The UTC timestamp when the message was sent.
	/// </summary>
	private DateTimeOffset? _sentTimestampUtc;

	/// <summary>
	/// The UTC timestamp when the message was received.
	/// </summary>
	private DateTimeOffset? _receivedTimestampUtc;

	/// <summary>
	/// The UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	private DateTimeOffset? _scheduledEnqueueTimeUtc;

	/// <summary>
	/// The time-to-live duration for the message.
	/// </summary>
	private TimeSpan? _timeToLive;

	/// <summary>
	/// The UTC timestamp when the message expires.
	/// </summary>
	private DateTimeOffset? _expiresAtUtc;

	/// <summary>
	/// The number of delivery attempts for the message.
	/// </summary>
	private int _deliveryCount;

	/// <summary>
	/// The maximum number of delivery attempts allowed.
	/// </summary>
	private int? _maxDeliveryCount;

	/// <summary>
	/// The error message from the last delivery attempt.
	/// </summary>
	private string? _lastDeliveryError;

	/// <summary>
	/// The name of the dead letter queue for failed messages.
	/// </summary>
	private string? _deadLetterQueue;

	/// <summary>
	/// The reason the message was sent to the dead letter queue.
	/// </summary>
	private string? _deadLetterReason;

	/// <summary>
	/// The detailed error description for dead letter messages.
	/// </summary>
	private string? _deadLetterErrorDescription;

	/// <summary>
	/// The identifier of the aggregate for event sourcing.
	/// </summary>
	private string? _aggregateId;

	/// <summary>
	/// The type of the aggregate for event sourcing.
	/// </summary>
	private string? _aggregateType;

	/// <summary>
	/// The version of the aggregate for event sourcing.
	/// </summary>
	private long? _aggregateVersion;

	/// <summary>
	/// The name of the event stream.
	/// </summary>
	private string? _streamName;

	/// <summary>
	/// The position within the event stream.
	/// </summary>
	private long? _streamPosition;

	/// <summary>
	/// The global position in the event store.
	/// </summary>
	private long? _globalPosition;

	/// <summary>
	/// The type of the event for event sourcing.
	/// </summary>
	private string? _eventType;

	/// <summary>
	/// The version of the event for event sourcing.
	/// </summary>
	private int? _eventVersion;

	/// <summary>
	/// The priority level of the message.
	/// </summary>
	private int? _priority;

	/// <summary>
	/// Indicates whether the message is durable.
	/// </summary>
	private bool? _durable;

	/// <summary>
	/// Indicates whether the message requires duplicate detection.
	/// </summary>
	private bool? _requiresDuplicateDetection;

	/// <summary>
	/// The time window for duplicate detection.
	/// </summary>
	private TimeSpan? _duplicateDetectionWindow;

	/// <summary>
	/// Gets an optional marker type for tooling.
	/// </summary>
	/// <value>The current <see cref="MarkerType"/> value.</value>
	public Type? MarkerType => null;

	/// <summary>
	/// Sets the message identifier and optionally the correlation identifier if not already set.
	/// </summary>
	/// <param name="messageId"> The unique message identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithMessageId(string messageId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		_messageId = messageId;
		if (string.IsNullOrWhiteSpace(_correlationId))
		{
			_correlationId = messageId;
		}

		return this;
	}

	/// <summary>
	/// Sets the correlation identifier for message tracing.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier to track related messages. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithCorrelationId(string correlationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the causation identifier for message causality tracking.
	/// </summary>
	/// <param name="causationId"> The identifier of the message that caused this message to be created. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithCausationId(string? causationId)
	{
		_causationId = causationId;
		return this;
	}

	/// <summary>
	/// Sets an external identifier for the message.
	/// </summary>
	/// <param name="externalId"> An external system identifier for the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithExternalId(string? externalId)
	{
		_externalId = externalId;
		return this;
	}

	/// <summary>
	/// Sets the user identifier associated with the message.
	/// </summary>
	/// <param name="userId"> The identifier of the user who created or owns the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithUserId(string? userId)
	{
		_userId = userId;
		return this;
	}

	/// <summary>
	/// Sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <param name="tenantId"> The identifier of the tenant that owns the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithTenantId(string? tenantId)
	{
		_tenantId = tenantId;
		return this;
	}

	/// <summary>
	/// Sets the trace parent identifier for distributed tracing.
	/// </summary>
	/// <param name="traceParent"> The W3C trace parent identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithTraceParent(string? traceParent)
	{
		_traceParent = traceParent;
		return this;
	}

	/// <summary>
	/// Sets the trace state for distributed tracing.
	/// </summary>
	/// <param name="traceState"> The W3C trace state information. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithTraceState(string? traceState)
	{
		_traceState = traceState;
		return this;
	}

	/// <summary>
	/// Sets the baggage information for distributed tracing.
	/// </summary>
	/// <param name="baggage"> The W3C baggage information for cross-service communication. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithBaggage(string? baggage)
	{
		_baggage = baggage;
		return this;
	}

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	/// <param name="messageType"> The type identifier for the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithMessageType(string messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the content type of the message.
	/// </summary>
	/// <param name="contentType"> The MIME type of the message content. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithContentType(string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
		_contentType = contentType;
		return this;
	}

	/// <summary>
	/// Sets the content encoding of the message.
	/// </summary>
	/// <param name="contentEncoding"> The encoding used for the message content. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithContentEncoding(string? contentEncoding)
	{
		_contentEncoding = contentEncoding;
		return this;
	}

	/// <summary>
	/// Sets the message format version.
	/// </summary>
	/// <param name="messageVersion"> The version of the message format. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithMessageVersion(string? messageVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageVersion);
		_messageVersion = messageVersion;
		return this;
	}

	/// <summary>
	/// Sets the serializer version used for the message.
	/// </summary>
	/// <param name="serializerVersion"> The version of the serializer. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithSerializerVersion(string? serializerVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serializerVersion);
		_serializerVersion = serializerVersion;
		return this;
	}

	/// <summary>
	/// Sets the contract version for message compatibility.
	/// </summary>
	/// <param name="contractVersion"> The version of the message contract. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithContractVersion(string? contractVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);
		_contractVersion = contractVersion;
		return this;
	}

	/// <summary>
	/// Sets the source of the message.
	/// </summary>
	/// <param name="source"> The origin or source system of the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithSource(string? source)
	{
		_source = source;
		return this;
	}

	/// <summary>
	/// Sets the destination for the message.
	/// </summary>
	/// <param name="destination"> The target destination or endpoint for the message. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithDestination(string? destination)
	{
		_destination = destination;
		return this;
	}

	/// <summary>
	/// Sets the reply-to address for the message.
	/// </summary>
	/// <param name="replyTo"> The address where replies should be sent. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithReplyTo(string? replyTo)
	{
		_replyTo = replyTo;
		return this;
	}

	/// <summary>
	/// Sets the session identifier for the message.
	/// </summary>
	/// <param name="sessionId"> The session identifier for message grouping. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithSessionId(string? sessionId)
	{
		_sessionId = sessionId;
		return this;
	}

	/// <summary>
	/// Sets the partition key for message distribution.
	/// </summary>
	/// <param name="partitionKey"> The key used to determine message partition. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithPartitionKey(string? partitionKey)
	{
		_partitionKey = partitionKey;
		return this;
	}

	/// <summary>
	/// Sets the routing key for message routing.
	/// </summary>
	/// <param name="routingKey"> The key used for message routing decisions. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithRoutingKey(string? routingKey)
	{
		_routingKey = routingKey;
		return this;
	}

	/// <summary>
	/// Sets the group identifier for message grouping.
	/// </summary>
	/// <param name="groupId"> The identifier for the message group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithGroupId(string? groupId)
	{
		_groupId = groupId;
		return this;
	}

	/// <summary>
	/// Sets the sequence number within the message group.
	/// </summary>
	/// <param name="groupSequence"> The sequence number within the group. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithGroupSequence(long? groupSequence)
	{
		_groupSequence = groupSequence;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was created.
	/// </summary>
	/// <param name="createdTimestampUtc"> The creation timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithCreatedTimestampUtc(DateTimeOffset createdTimestampUtc)
	{
		_createdTimestampUtc = createdTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was sent.
	/// </summary>
	/// <param name="sentTimestampUtc"> The sent timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithSentTimestampUtc(DateTimeOffset? sentTimestampUtc)
	{
		_sentTimestampUtc = sentTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was received.
	/// </summary>
	/// <param name="receivedTimestampUtc"> The received timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithReceivedTimestampUtc(DateTimeOffset? receivedTimestampUtc)
	{
		_receivedTimestampUtc = receivedTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	/// <param name="scheduledEnqueueTimeUtc"> The scheduled enqueue time in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithScheduledEnqueueTimeUtc(DateTimeOffset? scheduledEnqueueTimeUtc)
	{
		_scheduledEnqueueTimeUtc = scheduledEnqueueTimeUtc;
		return this;
	}

	/// <summary>
	/// Sets the time-to-live duration for the message.
	/// </summary>
	/// <param name="timeToLive"> The duration before the message expires. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithTimeToLive(TimeSpan? timeToLive)
	{
		_timeToLive = timeToLive;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message expires.
	/// </summary>
	/// <param name="expiresAtUtc"> The expiration timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithExpiresAtUtc(DateTimeOffset? expiresAtUtc)
	{
		_expiresAtUtc = expiresAtUtc;
		return this;
	}

	/// <summary>
	/// Sets the number of delivery attempts for the message.
	/// </summary>
	/// <param name="deliveryCount"> The current delivery attempt count. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when delivery count is negative. </exception>
	public IMessageMetadataBuilder WithDeliveryCount(int deliveryCount)
	{
		if (deliveryCount < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(deliveryCount), ErrorMessages.DeliveryCountCannotBeNegative);
		}

		_deliveryCount = deliveryCount;
		return this;
	}

	/// <summary>
	/// Sets the maximum number of delivery attempts allowed.
	/// </summary>
	/// <param name="maxDeliveryCount"> The maximum delivery attempt count. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when max delivery count is not positive. </exception>
	public IMessageMetadataBuilder WithMaxDeliveryCount(int? maxDeliveryCount)
	{
		if (maxDeliveryCount is <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDeliveryCount), ErrorMessages.MaxDeliveryCountMustBePositive);
		}

		_maxDeliveryCount = maxDeliveryCount;
		return this;
	}

	/// <summary>
	/// Sets the error message from the last delivery attempt.
	/// </summary>
	/// <param name="lastDeliveryError"> The error message from the most recent delivery attempt. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithLastDeliveryError(string? lastDeliveryError)
	{
		_lastDeliveryError = lastDeliveryError;
		return this;
	}

	/// <summary>
	/// Sets the name of the dead letter queue for failed messages.
	/// </summary>
	/// <param name="deadLetterQueue"> The name of the dead letter queue. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithDeadLetterQueue(string? deadLetterQueue)
	{
		_deadLetterQueue = deadLetterQueue;
		return this;
	}

	/// <summary>
	/// Sets the reason why the message was sent to the dead letter queue.
	/// </summary>
	/// <param name="deadLetterReason"> The reason for dead letter queue placement. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithDeadLetterReason(string? deadLetterReason)
	{
		_deadLetterReason = deadLetterReason;
		return this;
	}

	/// <summary>
	/// Sets the detailed error description for dead letter messages.
	/// </summary>
	/// <param name="deadLetterErrorDescription"> The detailed error description. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithDeadLetterErrorDescription(string? deadLetterErrorDescription)
	{
		_deadLetterErrorDescription = deadLetterErrorDescription;
		return this;
	}

	/// <summary>
	/// Sets the priority level of the message.
	/// </summary>
	/// <param name="priority"> The message priority level (higher numbers indicate higher priority). </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when priority is negative. </exception>
	public IMessageMetadataBuilder WithPriority(int? priority)
	{
		if (priority is < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(priority), ErrorMessages.PriorityCannotBeNegative);
		}

		_priority = priority;
		return this;
	}

	/// <summary>
	/// Sets whether the message is durable and should survive broker restarts.
	/// </summary>
	/// <param name="durable"> True if the message should be persisted durably. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithDurable(bool? durable)
	{
		_durable = durable;
		return this;
	}

	/// <summary>
	/// Sets whether the message requires duplicate detection.
	/// </summary>
	/// <param name="requiresDuplicateDetection"> True if duplicate detection should be enabled. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithRequiresDuplicateDetection(bool? requiresDuplicateDetection)
	{
		_requiresDuplicateDetection = requiresDuplicateDetection;
		return this;
	}

	/// <summary>
	/// Sets the time window for duplicate detection.
	/// </summary>
	/// <param name="duplicateDetectionWindow"> The duration to check for duplicates. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when window is not positive. </exception>
	public IMessageMetadataBuilder WithDuplicateDetectionWindow(TimeSpan? duplicateDetectionWindow)
	{
		if (duplicateDetectionWindow <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(duplicateDetectionWindow),
				ErrorMessages.DuplicateDetectionWindowMustBePositive);
		}

		_duplicateDetectionWindow = duplicateDetectionWindow;
		return this;
	}

	/// <summary>
	/// Sets event sourcing metadata for the message.
	/// </summary>
	/// <param name="aggregateId"> The identifier of the aggregate. </param>
	/// <param name="aggregateType"> The type of the aggregate. </param>
	/// <param name="aggregateVersion"> The version of the aggregate. </param>
	/// <param name="streamName"> The name of the event stream. </param>
	/// <param name="streamPosition"> The position within the stream. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithEventSourcing(
		string? aggregateId = null,
		string? aggregateType = null,
		long? aggregateVersion = null,
		string? streamName = null,
		long? streamPosition = null)
	{
		_aggregateId = aggregateId ?? _aggregateId;
		_aggregateType = aggregateType ?? _aggregateType;
		_aggregateVersion = aggregateVersion ?? _aggregateVersion;
		_streamName = streamName ?? _streamName;
		_streamPosition = streamPosition ?? _streamPosition;
		return this;
	}

	/// <summary>
	/// Sets the global position in the event store.
	/// </summary>
	/// <param name="globalPosition"> The global position number. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when position is negative. </exception>
	public IMessageMetadataBuilder WithGlobalPosition(long globalPosition)
	{
		if (globalPosition < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(globalPosition), ErrorMessages.GlobalPositionCannotBeNegative);
		}

		_globalPosition = globalPosition;
		return this;
	}

	/// <summary>
	/// Sets the type of the event for event sourcing.
	/// </summary>
	/// <param name="eventType"> The type identifier of the event. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithEventType(string eventType)
	{
		_eventType = eventType;
		return this;
	}

	/// <summary>
	/// Sets the version of the event for event sourcing.
	/// </summary>
	/// <param name="eventVersion"> The version number of the event. </param>
	/// <returns> The builder instance for method chaining. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when version is negative. </exception>
	public IMessageMetadataBuilder WithEventVersion(int eventVersion)
	{
		if (eventVersion < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(eventVersion), ErrorMessages.EventVersionCannotBeNegative);
		}

		_eventVersion = eventVersion;
		return this;
	}

	/// <summary>
	/// Sets timing-related metadata for the message.
	/// </summary>
	/// <param name="createdUtc"> The UTC creation timestamp. </param>
	/// <param name="sentUtc"> The UTC sent timestamp. </param>
	/// <param name="scheduledUtc"> The UTC scheduled enqueue timestamp. </param>
	/// <param name="ttl"> The time-to-live duration. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithTiming(
		DateTimeOffset? createdUtc = null,
		DateTimeOffset? sentUtc = null,
		DateTimeOffset? scheduledUtc = null,
		TimeSpan? ttl = null)
	{
		if (createdUtc.HasValue)
		{
			_createdTimestampUtc = createdUtc.Value;
		}

		_sentTimestampUtc = sentUtc ?? _sentTimestampUtc;
		_scheduledEnqueueTimeUtc = scheduledUtc ?? _scheduledEnqueueTimeUtc;
		_timeToLive = ttl ?? _timeToLive;
		return this;
	}

	/// <summary>
	/// Sets the roles associated with the message.
	/// </summary>
	/// <param name="roles"> The collection of user roles. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithRoles(IEnumerable<string>? roles)
	{
		_roles.Clear();
		if (roles != null)
		{
			_roles.AddRange(roles);
		}

		return this;
	}

	/// <summary>
	/// Adds a role to the message metadata.
	/// </summary>
	/// <param name="role"> The role to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddRole(string role)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(role);
		_roles.Add(role);
		return this;
	}

	/// <summary>
	/// Sets the security claims associated with the message.
	/// </summary>
	/// <param name="claims"> The collection of security claims. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithClaims(IEnumerable<Claim>? claims)
	{
		_claims.Clear();
		if (claims != null)
		{
			_claims.AddRange(claims);
		}

		return this;
	}

	/// <summary>
	/// Adds a security claim to the message metadata.
	/// </summary>
	/// <param name="claim"> The claim to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddClaim(Claim claim)
	{
		ArgumentNullException.ThrowIfNull(claim);
		_claims.Add(claim);
		return this;
	}

	/// <summary>
	/// Adds a header to the message metadata.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddHeader(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);
		_headers[key] = value;
		return this;
	}

	/// <summary>
	/// Adds multiple headers to the message metadata.
	/// </summary>
	/// <param name="headers"> The collection of headers to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddHeaders(IEnumerable<KeyValuePair<string, string>> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		foreach (var header in headers)
		{
			_headers[header.Key] = header.Value;
		}

		return this;
	}

	/// <summary>
	/// Adds an attribute to the message metadata.
	/// </summary>
	/// <param name="key"> The attribute key. </param>
	/// <param name="value"> The attribute value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddAttribute(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);
		_attributes[key] = value;
		return this;
	}

	/// <summary>
	/// Adds multiple attributes to the message metadata.
	/// </summary>
	/// <param name="attributes"> The collection of attributes to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		foreach (var attribute in attributes)
		{
			_attributes[attribute.Key] = attribute.Value;
		}

		return this;
	}

	/// <summary>
	/// Adds a property to the message metadata.
	/// </summary>
	/// <param name="key"> The property key. </param>
	/// <param name="value"> The property value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddProperty(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);
		_properties[key] = value;
		return this;
	}

	/// <summary>
	/// Adds multiple properties to the message metadata.
	/// </summary>
	/// <param name="properties"> The collection of properties to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddProperties(IEnumerable<KeyValuePair<string, object>> properties)
	{
		ArgumentNullException.ThrowIfNull(properties);
		foreach (var property in properties)
		{
			_properties[property.Key] = property.Value;
		}

		return this;
	}

	/// <summary>
	/// Adds an item to the message metadata.
	/// </summary>
	/// <param name="key"> The item key. </param>
	/// <param name="value"> The item value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddItem(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);
		_items[key] = value;
		return this;
	}

	/// <summary>
	/// Adds multiple items to the message metadata.
	/// </summary>
	/// <param name="items"> The collection of items to add. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder AddItems(IEnumerable<KeyValuePair<string, object>> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		foreach (var item in items)
		{
			_items[item.Key] = item.Value;
		}

		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was received.
	/// </summary>
	/// <param name="receivedTimestampUtc"> The received timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithReceivedTimestampUtc(DateTimeOffset receivedTimestampUtc)
	{
		_receivedTimestampUtc = receivedTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was sent.
	/// </summary>
	/// <param name="sentTimestampUtc"> The sent timestamp in UTC. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public IMessageMetadataBuilder WithSentTimestampUtc(DateTimeOffset sentTimestampUtc)
	{
		_sentTimestampUtc = sentTimestampUtc;
		return this;
	}

	/// <summary>
	/// Builds and returns the unified message metadata instance.
	/// </summary>
	/// <returns> A new immutable <see cref="IMessageMetadata" /> instance. </returns>
	public IMessageMetadata Build()
	{
		// Ensure correlation ID is set
		if (string.IsNullOrWhiteSpace(_correlationId))
		{
			_correlationId = _messageId;
		}

		// Calculate expiration if TTL is set but expiration is not
		if (_timeToLive.HasValue && !_expiresAtUtc.HasValue && _sentTimestampUtc.HasValue)
		{
			_expiresAtUtc = _sentTimestampUtc.Value.Add(_timeToLive.Value);
		}

		return new MessageMetadata
		{
			// Core Identity
			MessageId = _messageId,
			CorrelationId = _correlationId,
			CausationId = _causationId,
			ExternalId = _externalId,

			// Tracing
			TraceParent = _traceParent,
			TraceState = _traceState,
			Baggage = _baggage,

			// User Identity
			UserId = _userId,
			Roles = new ReadOnlyCollection<string>([.. _roles]),
			Claims = new ReadOnlyCollection<Claim>([.. _claims]),
			TenantId = _tenantId,

			// Message Type
			MessageType = _messageType,
			ContentType = _contentType,
			ContentEncoding = _contentEncoding,
			MessageVersion = _messageVersion,
			SerializerVersion = _serializerVersion,
			ContractVersion = _contractVersion,

			// Routing
			Source = _source,
			Destination = _destination,
			ReplyTo = _replyTo,
			SessionId = _sessionId,
			PartitionKey = _partitionKey,
			RoutingKey = _routingKey,
			GroupId = _groupId,
			GroupSequence = _groupSequence,

			// Timing
			CreatedTimestampUtc = _createdTimestampUtc,
			SentTimestampUtc = _sentTimestampUtc,
			ReceivedTimestampUtc = _receivedTimestampUtc,
			ScheduledEnqueueTimeUtc = _scheduledEnqueueTimeUtc,
			TimeToLive = _timeToLive,
			ExpiresAtUtc = _expiresAtUtc,

			// Delivery State
			DeliveryCount = _deliveryCount,
			MaxDeliveryCount = _maxDeliveryCount,
			LastDeliveryError = _lastDeliveryError,
			DeadLetterQueue = _deadLetterQueue,
			DeadLetterReason = _deadLetterReason,
			DeadLetterErrorDescription = _deadLetterErrorDescription,

			// Event Sourcing
			AggregateId = _aggregateId,
			AggregateType = _aggregateType,
			AggregateVersion = _aggregateVersion,
			StreamName = _streamName,
			StreamPosition = _streamPosition,
			GlobalPosition = _globalPosition,
			EventType = _eventType,
			EventVersion = _eventVersion,

			// QoS
			Priority = _priority,
			Durable = _durable,
			RequiresDuplicateDetection = _requiresDuplicateDetection,
			DuplicateDetectionWindow = _duplicateDetectionWindow,

			// Collections
			Headers = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(_headers, StringComparer.Ordinal)),
			Attributes = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_attributes, StringComparer.Ordinal)),
			Properties = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_properties, StringComparer.Ordinal)),
			Items = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_items, StringComparer.Ordinal)),
		};
	}
}
