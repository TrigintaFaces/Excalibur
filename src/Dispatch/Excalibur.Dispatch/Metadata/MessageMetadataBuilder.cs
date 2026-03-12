// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Metadata;

/// <summary>
/// Builder for creating and modifying unified message metadata instances.
/// </summary>
public sealed partial class MessageMetadataBuilder : IMessageMetadataBuilder
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

	private string _messageId = Guid.NewGuid().ToString();
	private string _correlationId = string.Empty;
	private string _messageType = "Unknown";
	private string _contentType = "application/json";
	private string _serializerVersion = "1.0";
	private string _messageVersion = "1.0";
	private string _contractVersion = "1.0.0";
	private DateTimeOffset _createdTimestampUtc = DateTimeOffset.UtcNow;
	private string? _causationId;
	private string? _externalId;
	private string? _traceParent;
	private string? _traceState;
	private string? _baggage;
	private string? _userId;
	private string? _tenantId;
	private string? _contentEncoding;
	private string? _source;
	private string? _destination;
	private string? _replyTo;
	private string? _sessionId;
	private string? _partitionKey;
	private string? _routingKey;
	private string? _groupId;
	private long? _groupSequence;
	private DateTimeOffset? _sentTimestampUtc;
	private DateTimeOffset? _receivedTimestampUtc;
	private DateTimeOffset? _scheduledEnqueueTimeUtc;
	private TimeSpan? _timeToLive;
	private DateTimeOffset? _expiresAtUtc;
	private int _deliveryCount;
	private int? _maxDeliveryCount;
	private string? _lastDeliveryError;
	private string? _deadLetterQueue;
	private string? _deadLetterReason;
	private string? _deadLetterErrorDescription;
	private string? _aggregateId;
	private string? _aggregateType;
	private long? _aggregateVersion;
	private string? _streamName;
	private long? _streamPosition;
	private long? _globalPosition;
	private string? _eventType;
	private int? _eventVersion;
	private int? _priority;
	private bool? _durable;
	private bool? _requiresDuplicateDetection;
	private TimeSpan? _duplicateDetectionWindow;

	/// <inheritdoc />
	public Type? MarkerType => null;

	/// <inheritdoc />
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

	/// <inheritdoc />
	public IMessageMetadataBuilder WithCorrelationId(string correlationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
		_correlationId = correlationId;
		return this;
	}

	/// <inheritdoc />
	public IMessageMetadataBuilder WithCausationId(string? causationId)
	{
		_causationId = causationId;
		return this;
	}

	// ===== Typed convenience methods (not on interface, called via concrete type or extension methods) =====

	/// <summary>
	/// Sets an external identifier for the message.
	/// </summary>
	public IMessageMetadataBuilder WithExternalId(string? externalId)
	{
		_externalId = externalId;
		return this;
	}

	/// <summary>
	/// Sets the user identifier associated with the message.
	/// </summary>
	public IMessageMetadataBuilder WithUserId(string? userId)
	{
		_userId = userId;
		return this;
	}

	/// <summary>
	/// Sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	public IMessageMetadataBuilder WithTenantId(string? tenantId)
	{
		_tenantId = tenantId;
		return this;
	}

	/// <summary>
	/// Sets the trace parent identifier for distributed tracing.
	/// </summary>
	public IMessageMetadataBuilder WithTraceParent(string? traceParent)
	{
		_traceParent = traceParent;
		return this;
	}

	/// <summary>
	/// Sets the trace state for distributed tracing.
	/// </summary>
	public IMessageMetadataBuilder WithTraceState(string? traceState)
	{
		_traceState = traceState;
		return this;
	}

	/// <summary>
	/// Sets the baggage information for distributed tracing.
	/// </summary>
	public IMessageMetadataBuilder WithBaggage(string? baggage)
	{
		_baggage = baggage;
		return this;
	}

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	public IMessageMetadataBuilder WithMessageType(string messageType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the content type of the message.
	/// </summary>
	public IMessageMetadataBuilder WithContentType(string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
		_contentType = contentType;
		return this;
	}

	/// <summary>
	/// Sets the content encoding of the message.
	/// </summary>
	public IMessageMetadataBuilder WithContentEncoding(string? contentEncoding)
	{
		_contentEncoding = contentEncoding;
		return this;
	}

	/// <summary>
	/// Sets the message format version.
	/// </summary>
	public IMessageMetadataBuilder WithMessageVersion(string? messageVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageVersion);
		_messageVersion = messageVersion;
		return this;
	}

	/// <summary>
	/// Sets the serializer version used for the message.
	/// </summary>
	public IMessageMetadataBuilder WithSerializerVersion(string? serializerVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serializerVersion);
		_serializerVersion = serializerVersion;
		return this;
	}

	/// <summary>
	/// Sets the contract version for message compatibility.
	/// </summary>
	public IMessageMetadataBuilder WithContractVersion(string? contractVersion)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);
		_contractVersion = contractVersion;
		return this;
	}

	/// <summary>
	/// Sets the source of the message.
	/// </summary>
	public IMessageMetadataBuilder WithSource(string? source)
	{
		_source = source;
		return this;
	}

	/// <summary>
	/// Sets the destination for the message.
	/// </summary>
	public IMessageMetadataBuilder WithDestination(string? destination)
	{
		_destination = destination;
		return this;
	}

	/// <summary>
	/// Sets the reply-to address for the message.
	/// </summary>
	public IMessageMetadataBuilder WithReplyTo(string? replyTo)
	{
		_replyTo = replyTo;
		return this;
	}

	/// <summary>
	/// Sets the session identifier for the message.
	/// </summary>
	public IMessageMetadataBuilder WithSessionId(string? sessionId)
	{
		_sessionId = sessionId;
		return this;
	}

	/// <summary>
	/// Sets the partition key for message distribution.
	/// </summary>
	public IMessageMetadataBuilder WithPartitionKey(string? partitionKey)
	{
		_partitionKey = partitionKey;
		return this;
	}

	/// <summary>
	/// Sets the routing key for message routing.
	/// </summary>
	public IMessageMetadataBuilder WithRoutingKey(string? routingKey)
	{
		_routingKey = routingKey;
		return this;
	}

	/// <summary>
	/// Sets the group identifier for message grouping.
	/// </summary>
	public IMessageMetadataBuilder WithGroupId(string? groupId)
	{
		_groupId = groupId;
		return this;
	}

	/// <summary>
	/// Sets the sequence number within the message group.
	/// </summary>
	public IMessageMetadataBuilder WithGroupSequence(long? groupSequence)
	{
		_groupSequence = groupSequence;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was created.
	/// </summary>
	public IMessageMetadataBuilder WithCreatedTimestampUtc(DateTimeOffset createdTimestampUtc)
	{
		_createdTimestampUtc = createdTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was sent.
	/// </summary>
	public IMessageMetadataBuilder WithSentTimestampUtc(DateTimeOffset? sentTimestampUtc)
	{
		_sentTimestampUtc = sentTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was received.
	/// </summary>
	public IMessageMetadataBuilder WithReceivedTimestampUtc(DateTimeOffset? receivedTimestampUtc)
	{
		_receivedTimestampUtc = receivedTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message is scheduled to be enqueued.
	/// </summary>
	public IMessageMetadataBuilder WithScheduledEnqueueTimeUtc(DateTimeOffset? scheduledEnqueueTimeUtc)
	{
		_scheduledEnqueueTimeUtc = scheduledEnqueueTimeUtc;
		return this;
	}

	/// <summary>
	/// Sets the time-to-live duration for the message.
	/// </summary>
	public IMessageMetadataBuilder WithTimeToLive(TimeSpan? timeToLive)
	{
		_timeToLive = timeToLive;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message expires.
	/// </summary>
	public IMessageMetadataBuilder WithExpiresAtUtc(DateTimeOffset? expiresAtUtc)
	{
		_expiresAtUtc = expiresAtUtc;
		return this;
	}

	/// <summary>
	/// Sets the number of delivery attempts for the message.
	/// </summary>
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
	public IMessageMetadataBuilder WithLastDeliveryError(string? lastDeliveryError)
	{
		_lastDeliveryError = lastDeliveryError;
		return this;
	}

	/// <summary>
	/// Sets the name of the dead letter queue for failed messages.
	/// </summary>
	public IMessageMetadataBuilder WithDeadLetterQueue(string? deadLetterQueue)
	{
		_deadLetterQueue = deadLetterQueue;
		return this;
	}

	/// <summary>
	/// Sets the reason why the message was sent to the dead letter queue.
	/// </summary>
	public IMessageMetadataBuilder WithDeadLetterReason(string? deadLetterReason)
	{
		_deadLetterReason = deadLetterReason;
		return this;
	}

	/// <summary>
	/// Sets the detailed error description for dead letter messages.
	/// </summary>
	public IMessageMetadataBuilder WithDeadLetterErrorDescription(string? deadLetterErrorDescription)
	{
		_deadLetterErrorDescription = deadLetterErrorDescription;
		return this;
	}

	/// <summary>
	/// Sets the priority level of the message.
	/// </summary>
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
	public IMessageMetadataBuilder WithDurable(bool? durable)
	{
		_durable = durable;
		return this;
	}

	/// <summary>
	/// Sets whether the message requires duplicate detection.
	/// </summary>
	public IMessageMetadataBuilder WithRequiresDuplicateDetection(bool? requiresDuplicateDetection)
	{
		_requiresDuplicateDetection = requiresDuplicateDetection;
		return this;
	}

	/// <summary>
	/// Sets the time window for duplicate detection.
	/// </summary>
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
	public IMessageMetadataBuilder WithEventType(string eventType)
	{
		_eventType = eventType;
		return this;
	}

	/// <summary>
	/// Sets the version of the event for event sourcing.
	/// </summary>
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
	public IMessageMetadataBuilder AddRole(string role)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(role);
		_roles.Add(role);
		return this;
	}

	/// <summary>
	/// Sets the security claims associated with the message.
	/// </summary>
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
	public IMessageMetadataBuilder AddClaim(Claim claim)
	{
		ArgumentNullException.ThrowIfNull(claim);
		_claims.Add(claim);
		return this;
	}

	/// <inheritdoc />
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
	/// Sets the UTC timestamp when the message was received (non-nullable overload).
	/// </summary>
	public IMessageMetadataBuilder WithReceivedTimestampUtc(DateTimeOffset receivedTimestampUtc)
	{
		_receivedTimestampUtc = receivedTimestampUtc;
		return this;
	}

	/// <summary>
	/// Sets the UTC timestamp when the message was sent (non-nullable overload).
	/// </summary>
	public IMessageMetadataBuilder WithSentTimestampUtc(DateTimeOffset sentTimestampUtc)
	{
		_sentTimestampUtc = sentTimestampUtc;
		return this;
	}

	/// <inheritdoc />
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

		// Build the merged Properties dictionary: explicit properties + moved property values
		var mergedProperties = new Dictionary<string, object>(_properties, StringComparer.Ordinal);
		PopulateMovedProperties(mergedProperties);

		var roles = new ReadOnlyCollection<string>([.. _roles]);
		var claims = new ReadOnlyCollection<Claim>([.. _claims]);

		return new MessageMetadata
		{
			MessageId = _messageId,
			CorrelationId = _correlationId,
			CausationId = _causationId,
			MessageType = _messageType,
			ContentType = _contentType,
			Source = _source,
			CreatedTimestampUtc = _createdTimestampUtc,
			ExternalId = _externalId,
			TraceParent = _traceParent,
			TraceState = _traceState,
			Baggage = _baggage,
			UserId = _userId,
			Roles = roles,
			Claims = claims,
			TenantId = _tenantId,
			ContentEncoding = _contentEncoding,
			MessageVersion = _messageVersion,
			SerializerVersion = _serializerVersion,
			ContractVersion = _contractVersion,
			Destination = _destination,
			ReplyTo = _replyTo,
			SessionId = _sessionId,
			PartitionKey = _partitionKey,
			RoutingKey = _routingKey,
			GroupId = _groupId,
			GroupSequence = _groupSequence,
			SentTimestampUtc = _sentTimestampUtc,
			ReceivedTimestampUtc = _receivedTimestampUtc,
			ScheduledEnqueueTimeUtc = _scheduledEnqueueTimeUtc,
			TimeToLive = _timeToLive,
			ExpiresAtUtc = _expiresAtUtc,
			DeliveryCount = _deliveryCount,
			MaxDeliveryCount = _maxDeliveryCount,
			LastDeliveryError = _lastDeliveryError,
			DeadLetterQueue = _deadLetterQueue,
			DeadLetterReason = _deadLetterReason,
			DeadLetterErrorDescription = _deadLetterErrorDescription,
			AggregateId = _aggregateId,
			AggregateType = _aggregateType,
			AggregateVersion = _aggregateVersion,
			StreamName = _streamName,
			StreamPosition = _streamPosition,
			GlobalPosition = _globalPosition,
			EventType = _eventType,
			EventVersion = _eventVersion,
			Priority = _priority,
			Durable = _durable,
			RequiresDuplicateDetection = _requiresDuplicateDetection,
			DuplicateDetectionWindow = _duplicateDetectionWindow,
			Headers = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(_headers, StringComparer.Ordinal)),
			Properties = new ReadOnlyDictionary<string, object>(mergedProperties),
		};
	}

	/// <summary>
	/// Populates the merged properties dictionary with values from all properties that were moved
	/// off the IMessageMetadata interface into the Properties bag.
	/// </summary>
	private void PopulateMovedProperties(Dictionary<string, object> properties)
	{
		SetIfNotNull(properties, MetadataPropertyKeys.ExternalId, _externalId);
		SetIfNotNull(properties, MetadataPropertyKeys.TraceParent, _traceParent);
		SetIfNotNull(properties, MetadataPropertyKeys.TraceState, _traceState);
		SetIfNotNull(properties, MetadataPropertyKeys.Baggage, _baggage);
		SetIfNotNull(properties, MetadataPropertyKeys.UserId, _userId);
		SetIfNotNull(properties, MetadataPropertyKeys.TenantId, _tenantId);

		if (_roles.Count > 0)
		{
			properties[MetadataPropertyKeys.Roles] = new ReadOnlyCollection<string>([.. _roles]);
		}

		if (_claims.Count > 0)
		{
			properties[MetadataPropertyKeys.Claims] = new ReadOnlyCollection<Claim>([.. _claims]);
		}

		SetIfNotNull(properties, MetadataPropertyKeys.ContentEncoding, _contentEncoding);
		properties[MetadataPropertyKeys.MessageVersion] = _messageVersion;
		properties[MetadataPropertyKeys.SerializerVersion] = _serializerVersion;
		properties[MetadataPropertyKeys.ContractVersion] = _contractVersion;

		SetIfNotNull(properties, MetadataPropertyKeys.Destination, _destination);
		SetIfNotNull(properties, MetadataPropertyKeys.ReplyTo, _replyTo);
		SetIfNotNull(properties, MetadataPropertyKeys.SessionId, _sessionId);
		SetIfNotNull(properties, MetadataPropertyKeys.PartitionKey, _partitionKey);
		SetIfNotNull(properties, MetadataPropertyKeys.RoutingKey, _routingKey);
		SetIfNotNull(properties, MetadataPropertyKeys.GroupId, _groupId);

		if (_groupSequence.HasValue)
		{
			properties[MetadataPropertyKeys.GroupSequence] = _groupSequence.Value;
		}

		if (_sentTimestampUtc.HasValue)
		{
			properties[MetadataPropertyKeys.SentTimestampUtc] = _sentTimestampUtc.Value;
		}

		if (_receivedTimestampUtc.HasValue)
		{
			properties[MetadataPropertyKeys.ReceivedTimestampUtc] = _receivedTimestampUtc.Value;
		}

		if (_scheduledEnqueueTimeUtc.HasValue)
		{
			properties[MetadataPropertyKeys.ScheduledEnqueueTimeUtc] = _scheduledEnqueueTimeUtc.Value;
		}

		if (_timeToLive.HasValue)
		{
			properties[MetadataPropertyKeys.TimeToLive] = _timeToLive.Value;
		}

		if (_expiresAtUtc.HasValue)
		{
			properties[MetadataPropertyKeys.ExpiresAtUtc] = _expiresAtUtc.Value;
		}

		if (_deliveryCount != 0)
		{
			properties[MetadataPropertyKeys.DeliveryCount] = _deliveryCount;
		}

		if (_maxDeliveryCount.HasValue)
		{
			properties[MetadataPropertyKeys.MaxDeliveryCount] = _maxDeliveryCount.Value;
		}

		SetIfNotNull(properties, MetadataPropertyKeys.LastDeliveryError, _lastDeliveryError);
		SetIfNotNull(properties, MetadataPropertyKeys.DeadLetterQueue, _deadLetterQueue);
		SetIfNotNull(properties, MetadataPropertyKeys.DeadLetterReason, _deadLetterReason);
		SetIfNotNull(properties, MetadataPropertyKeys.DeadLetterErrorDescription, _deadLetterErrorDescription);

		if (_priority.HasValue)
		{
			properties[MetadataPropertyKeys.Priority] = _priority.Value;
		}

		if (_durable.HasValue)
		{
			properties[MetadataPropertyKeys.Durable] = _durable.Value;
		}

		if (_requiresDuplicateDetection.HasValue)
		{
			properties[MetadataPropertyKeys.RequiresDuplicateDetection] = _requiresDuplicateDetection.Value;
		}

		if (_duplicateDetectionWindow.HasValue)
		{
			properties[MetadataPropertyKeys.DuplicateDetectionWindow] = _duplicateDetectionWindow.Value;
		}

		SetIfNotNull(properties, MetadataPropertyKeys.AggregateId, _aggregateId);
		SetIfNotNull(properties, MetadataPropertyKeys.AggregateType, _aggregateType);

		if (_aggregateVersion.HasValue)
		{
			properties[MetadataPropertyKeys.AggregateVersion] = _aggregateVersion.Value;
		}

		SetIfNotNull(properties, MetadataPropertyKeys.StreamName, _streamName);

		if (_streamPosition.HasValue)
		{
			properties[MetadataPropertyKeys.StreamPosition] = _streamPosition.Value;
		}

		if (_globalPosition.HasValue)
		{
			properties[MetadataPropertyKeys.GlobalPosition] = _globalPosition.Value;
		}

		SetIfNotNull(properties, MetadataPropertyKeys.EventType, _eventType);

		if (_eventVersion.HasValue)
		{
			properties[MetadataPropertyKeys.EventVersion] = _eventVersion.Value;
		}

		if (_attributes.Count > 0)
		{
			properties[MetadataPropertyKeys.Attributes] =
				new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_attributes, StringComparer.Ordinal));
		}

		if (_items.Count > 0)
		{
			properties[MetadataPropertyKeys.Items] =
				new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_items, StringComparer.Ordinal));
		}
	}

	private static void SetIfNotNull(Dictionary<string, object> properties, string key, string? value)
	{
		if (value != null)
		{
			properties[key] = value;
		}
	}
}
