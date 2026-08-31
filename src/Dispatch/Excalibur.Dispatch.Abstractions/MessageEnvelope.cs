// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch;

/// <summary>
/// Unified message envelope that consolidates all message context implementations into a single, extensible type.
/// </summary>
/// <remarks>
/// This envelope replaces the 20+ separate context implementations with a single unified type that supports:
/// <list type="bullet">
/// <item> Core message properties (ID, correlation, causation, timestamps) </item>
/// <item> Extensible headers dictionary for custom metadata </item>
/// <item> Extensible properties bag for runtime state </item>
/// <item> Provider-specific metadata support </item>
/// <item> Pooling and reset capabilities for high-performance scenarios </item>
/// <item> Proper serialization attributes for JSON and other formats </item>
/// </list>
/// </remarks>
public sealed class MessageEnvelope : IMessageContext, IDisposable
{
	private static readonly RoutingDecision DefaultRoutingDecisionValue = RoutingDecision.Local;
	private readonly ConcurrentDictionary<string, object> _items = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<Type, object> _features = new();
	private readonly ConcurrentDictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
	private IValidationResult _validationResult = new DefaultValidationResult();
	private IAuthorizationResult _authorizationResult = new DefaultAuthorizationResult();
	private RoutingDecision? _routingDecision = DefaultRoutingDecisionValue;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEnvelope" /> class.
	/// </summary>
	public MessageEnvelope()
	{
		MessageId = Uuid7Extensions.GenerateString();
		ReceivedTimestampUtc = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MessageEnvelope" /> class with a message.
	/// </summary>
	/// <param name="message"> The message to encapsulate. </param>
	public MessageEnvelope(IDispatchMessage message)
		: this() =>
		Message = message ?? throw new ArgumentNullException(nameof(message));

	#region Core Message Properties

	/// <inheritdoc />
	[JsonPropertyName("messageId")]
	public string? MessageId { get; set; }

	/// <summary>
	/// Gets or sets the external identifier.
	/// </summary>
	[JsonPropertyName("externalId")]
	public string? ExternalId { get; set; }

	/// <summary>
	/// Gets or sets the user identifier.
	/// </summary>
	[JsonPropertyName("userId")]
	public string? UserId { get; set; }

	/// <inheritdoc />
	[JsonPropertyName("correlationId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? CorrelationId { get; set; }

	/// <inheritdoc />
	[JsonPropertyName("causationId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the W3C trace parent header.
	/// </summary>
	[JsonPropertyName("traceParent")]
	public string? TraceParent { get; set; }

	/// <summary>
	/// Gets or sets the serializer version used to serialize the message.
	/// </summary>
	[JsonPropertyName("serializerVersion")]
	public string? SerializerVersion { get; set; }

	/// <summary>
	/// Gets or sets the message version.
	/// </summary>
	[JsonPropertyName("messageVersion")]
	public string? MessageVersion { get; set; }

	/// <summary>
	/// Gets or sets the contract version.
	/// </summary>
	[JsonPropertyName("contractVersion")]
	public string? ContractVersion { get; set; }

	/// <summary>
	/// Gets or sets the desired message version.
	/// </summary>
	[JsonPropertyName("desiredVersion")]
	public int? DesiredVersion { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier.
	/// </summary>
	[JsonPropertyName("tenantId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	[JsonPropertyName("messageType")]
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the content type.
	/// </summary>
	[JsonPropertyName("contentType")]
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the delivery count.
	/// </summary>
	[JsonPropertyName("deliveryCount")]
	public int DeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets the message subject for legacy compatibility.
	/// </summary>
	[JsonPropertyName("subject")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Subject { get; set; }

	/// <summary>
	/// Gets or sets the message body for legacy compatibility.
	/// </summary>
	[JsonPropertyName("body")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Body { get; set; }

	/// <summary>
	/// Gets or sets the reply-to address for response messages.
	/// </summary>
	[JsonPropertyName("replyTo")]
	public string? ReplyTo { get; set; }

	/// <summary>
	/// Gets or sets the message version metadata.
	/// </summary>
	[JsonIgnore]
	public IMessageVersionMetadata VersionMetadata { get; set; } = new DefaultMessageVersionMetadata();

	/// <summary>
	/// Gets or sets the validation result for the message.
	/// </summary>
	[JsonIgnore]
	public IValidationResult ValidationResult
	{
		get => _validationResult;
		set => _validationResult = value ?? new DefaultValidationResult();
	}

	/// <summary>
	/// Gets or sets the authorization result for the message.
	/// </summary>
	[JsonIgnore]
	public IAuthorizationResult AuthorizationResult
	{
		get => _authorizationResult;
		set => _authorizationResult = value ?? new DefaultAuthorizationResult();
	}

	/// <summary>
	/// Gets or sets the routing decision for the message.
	/// </summary>
	[JsonIgnore]
	public RoutingDecision? RoutingDecision
	{
		get => _routingDecision;
		set => _routingDecision = value;
	}

	/// <inheritdoc />
	[JsonIgnore]
	public IServiceProvider RequestServices { get; set; } = null!;

	/// <summary>
	/// Gets or sets the received timestamp.
	/// </summary>
	[JsonPropertyName("receivedTimestampUtc")]
	public DateTimeOffset ReceivedTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the sent timestamp.
	/// </summary>
	[JsonPropertyName("sentTimestampUtc")]
	public DateTimeOffset? SentTimestampUtc { get; set; }

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	[JsonIgnore]
	public IMessageMetadata? Metadata { get; set; }

	/// <inheritdoc />
	[JsonIgnore]
	public object? Result { get; set; }

	// ===== Grouped facade views (zero-allocation read-only views over the flat backing fields) =====
	// These compose the envelope's flat fields into focused (<=10 property) value-type views without
	// changing the pooled storage layout; Reset()/Clone() continue to operate on the flat fields.

	#endregion Core Message Properties

	#region Extended Properties

	/// <summary>
	/// Gets or sets the message payload.
	/// </summary>
	[JsonIgnore]
	public IDispatchMessage? Message { get; set; }

	/// <summary>
	/// Gets the extensible headers dictionary for custom metadata.
	/// </summary>
	[JsonPropertyName("headers")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public IDictionary<string, string> Headers => _headers;

	/// <inheritdoc />
	[JsonIgnore]
	public IDictionary<string, object> Items => _items;

	/// <inheritdoc />
	[JsonIgnore]
	public IDictionary<Type, object> Features => _features;

	/// <summary>
	/// Gets a value indicating whether the message processing was successful.
	/// </summary>
	[JsonIgnore]
	public bool Success => ValidationResult?.IsValid == true &&
						   AuthorizationResult?.IsAuthorized == true &&
						   (RoutingDecision?.IsSuccess ?? true);

	// ========================================== LEGACY PROPERTIES ==========================================
	// These properties are retained on the concrete class but are no longer part of IMessageContext.
	// Consumers should use the Features dictionary with typed feature interfaces instead.

	#endregion Extended Properties

	#region Cloud Provider Properties

	/// <summary>
	/// Gets or sets the receipt handle for cloud providers (AWS SQS, Azure Service Bus).
	/// </summary>
	[JsonPropertyName("receiptHandle")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ReceiptHandle { get; set; }

	/// <summary>
	/// Gets or sets the visibility timeout for message acknowledgment.
	/// </summary>
	[JsonPropertyName("visibilityTimeout")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public DateTimeOffset? VisibilityTimeout { get; set; }

	/// <summary>
	/// Gets or sets the dead letter reason.
	/// </summary>
	[JsonPropertyName("deadLetterReason")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? DeadLetterReason { get; set; }

	/// <summary>
	/// Gets or sets the dead letter error description.
	/// </summary>
	[JsonPropertyName("deadLetterErrorDescription")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? DeadLetterErrorDescription { get; set; }

	/// <summary>
	/// Gets or sets the message group ID for FIFO queues.
	/// </summary>
	[JsonPropertyName("messageGroupId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the message deduplication ID.
	/// </summary>
	[JsonPropertyName("messageDeduplicationId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? MessageDeduplicationId { get; set; }

	#endregion Cloud Provider Properties

	#region Serverless Properties

	#endregion Serverless Properties

	#region Channel Support

	/// <summary>
	/// Gets or sets an optional acknowledgment callback for channel-based processing.
	/// </summary>
	/// <remarks>
	/// Acknowledgment is transport I/O (e.g. SQS <c>DeleteMessage</c>, RabbitMQ <c>BasicAck</c>) and therefore
	/// honors cancellation via the supplied <see cref="CancellationToken"/>.
	/// </remarks>
	[JsonIgnore]
	public Func<CancellationToken, Task>? AcknowledgeAsync { get; set; }

	/// <summary>
	/// Gets or sets an optional rejection callback for channel-based processing.
	/// </summary>
	/// <remarks>
	/// Rejection is transport I/O and therefore honors cancellation via the supplied <see cref="CancellationToken"/>.
	/// </remarks>
	[JsonIgnore]
	public Func<string?, CancellationToken, Task>? RejectAsync { get; set; }

	#endregion Channel Support

	#region Helper Item Methods (no longer on IMessageContext interface -- use extension methods)

	/// <summary>
	/// Checks if an item exists.
	/// </summary>
	public bool ContainsItem(string key)
	{
		ArgumentNullException.ThrowIfNull(key);
		return _items.ContainsKey(key);
	}

	/// <summary>
	/// Gets an item by key.
	/// </summary>
	public T? GetItem<T>(string key)
	{
		ArgumentNullException.ThrowIfNull(key);
		return _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
	}

	/// <summary>
	/// Gets an item by key with a default value.
	/// </summary>
	public T GetItem<T>(string key, T defaultValue)
	{
		ArgumentNullException.ThrowIfNull(key);
		return _items.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
	}

	/// <summary>
	/// Removes an item by key.
	/// </summary>
	public void RemoveItem(string key)
	{
		ArgumentNullException.ThrowIfNull(key);
		_ = _items.TryRemove(key, out _);
	}

	/// <summary>
	/// Sets an item by key.
	/// </summary>
	public void SetItem<T>(string key, T value)
	{
		ArgumentNullException.ThrowIfNull(key);
		if (value is null)
		{
			RemoveItem(key);
		}
		else
		{
			_items[key] = value;
		}
	}

	#endregion Helper Item Methods

	#region Pooling Support

	/// <summary>
	/// Resets the envelope to its initial state for object pooling scenarios.
	/// </summary>
	public void Reset()
	{
		// Clear all properties
		MessageId = Uuid7Extensions.GenerateString();
		ExternalId = null;
		UserId = null;
		CorrelationId = null;
		CausationId = null;
		TraceParent = null;
		SerializerVersion = null;
		MessageVersion = null;
		ContractVersion = null;
		DesiredVersion = null;
		TenantId = null;
		MessageType = null;
		ContentType = null;
		DeliveryCount = 0;
		ReplyTo = null;

		// Reset timestamps
		ReceivedTimestampUtc = DateTimeOffset.UtcNow;
		SentTimestampUtc = null;

		// Clear cloud provider and serverless properties

		// Clear channel callbacks
		AcknowledgeAsync = null;
		RejectAsync = null;

		// Clear message
		Message = null;

		// Reset results
		_validationResult = new DefaultValidationResult();
		_authorizationResult = new DefaultAuthorizationResult();
		_routingDecision = DefaultRoutingDecisionValue;
		VersionMetadata = new DefaultMessageVersionMetadata();

		// Clear collections
		_items.Clear();
		_features.Clear();
		_headers.Clear();

		// Reset legacy properties

		// Note: RequestServices is not cleared as it's typically managed externally
		RequestServices = null!;
	}

	#endregion Pooling Support

	#region Helper Methods

	/// <summary>
	/// Creates a shallow copy of this envelope.
	/// </summary>
	/// <returns> A new envelope with copied values. </returns>
	public MessageEnvelope Clone()
	{
		var clone = new MessageEnvelope
		{
			MessageId = MessageId,
			ExternalId = ExternalId,
			UserId = UserId,
			CorrelationId = CorrelationId,
			CausationId = CausationId,
			TraceParent = TraceParent,
			SerializerVersion = SerializerVersion,
			MessageVersion = MessageVersion,
			ContractVersion = ContractVersion,
			DesiredVersion = DesiredVersion,
			TenantId = TenantId,
			MessageType = MessageType,
			ContentType = ContentType,
			DeliveryCount = DeliveryCount,
			ReplyTo = ReplyTo,
			ReceivedTimestampUtc = ReceivedTimestampUtc,
			SentTimestampUtc = SentTimestampUtc,
			ReceiptHandle = ReceiptHandle,
			VisibilityTimeout = VisibilityTimeout,
			DeadLetterReason = DeadLetterReason,
			DeadLetterErrorDescription = DeadLetterErrorDescription,
			MessageGroupId = MessageGroupId,
			MessageDeduplicationId = MessageDeduplicationId,
			Message = Message,
			RequestServices = RequestServices,
			VersionMetadata = VersionMetadata,
			ValidationResult = ValidationResult,
			AuthorizationResult = AuthorizationResult,
			RoutingDecision = RoutingDecision,
			AcknowledgeAsync = AcknowledgeAsync,
			RejectAsync = RejectAsync,
		};

		// Copy collections
		CopyCollectionsTo(clone);

		return clone;
	}

	/// <summary>
	/// Gets a value from headers.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <returns> The header value or null if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string? GetHeader(string key) => _headers.GetValueOrDefault(key);

	/// <summary>
	/// Sets a header value.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetHeader(string key, string? value)
	{
		if (value is null)
		{
			_ = _headers.TryRemove(key, out _);
		}
		else
		{
			_headers[key] = value;
		}
	}

	#endregion Helper Methods

	#region IDisposable

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Dispose of any disposable items
		foreach (var item in _items.Values)
		{
			if (item is IDisposable disposable)
			{
				try
				{
					disposable.Dispose();
				}
				catch
				{
					// Suppress exceptions during disposal
				}
			}
		}

		// Clear collections
		_items.Clear();
		_features.Clear();
		_headers.Clear();

		// Note: We don't dispose RequestServices as it's managed externally
	}

	#endregion IDisposable

	#region Private Helper Methods

	/// <summary>
	/// Copies collection data to the cloned envelope.
	/// </summary>
	/// <param name="clone"> The envelope receiving the copied collections. </param>
	private void CopyCollectionsTo(MessageEnvelope clone)
	{
		foreach (var item in _items)
		{
			clone._items[item.Key] = item.Value;
		}

		foreach (var feature in _features)
		{
			clone._features[feature.Key] = feature.Value;
		}

		foreach (var header in _headers)
		{
			clone._headers[header.Key] = header.Value;
		}

	}

	#endregion Private Helper Methods

	#region Default Result Classes

	private sealed class DefaultValidationResult : IValidationResult
	{
		private readonly List<object> _errors = [];

		/// <inheritdoc />
		public bool IsValid { get; set; } = true;

		/// <inheritdoc />
		public IReadOnlyCollection<object> Errors => _errors;

		/// <inheritdoc />
		public static IValidationResult Failed(params object[] errors)
		{
			var result = new DefaultValidationResult { IsValid = false };
			result._errors.AddRange(errors);
			return result;
		}

		/// <inheritdoc />
		public static IValidationResult Success() => new DefaultValidationResult { IsValid = true };
	}

	private sealed class DefaultAuthorizationResult : IAuthorizationResult
	{
		/// <inheritdoc />
		public bool IsAuthorized { get; init; } = true;

		/// <inheritdoc />
		public string? FailureMessage { get; init; }
	}

	private sealed class DefaultMessageVersionMetadata : IMessageVersionMetadata
	{
		/// <summary>
		/// Gets a value indicating whether backward compatibility is supported (legacy compatibility - static property not part of interface).
		/// </summary>
		/// <value> The current <see cref="IsBackwardCompatible" /> value. </value>
		public static bool IsBackwardCompatible => true;

		/// <summary>
		/// Gets the list of supported message versions.
		/// </summary>
		/// <value> The current <see cref="SupportedVersions" /> value. </value>
		public static IReadOnlyList<int> SupportedVersions { get; } = new[] { 1 };

		/// <inheritdoc />
		public int Version { get; set; } = 1;

		/// <inheritdoc />
		public int SchemaVersion { get; set; } = 1;

		/// <inheritdoc />
		public int SerializerVersion { get; set; } = 1;
	}

	#endregion Default Result Classes
}
