// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Builders;

/// <summary>
/// Fluent builder for creating <see cref="TransportReceivedMessage"/> instances in tests.
/// Provides sensible defaults for all properties with override capability.
/// </summary>
/// <remarks>
/// <para>
/// Useful for testing receivers and subscriber handlers with realistic received messages:
/// </para>
/// <para>
/// Example:
/// <code>
/// var received = new ReceivedMessageBuilder()
///     .WithBody("test-payload")
///     .WithMessageType("OrderPlaced")
///     .WithDeliveryCount(1)
///     .Build();
///
/// receiver.Enqueue(received);
/// </code>
/// </para>
/// </remarks>
public sealed class ReceivedMessageBuilder
{
	private string? _id;
	private ReadOnlyMemory<byte>? _body;
	private string? _contentType;
	private string? _messageType;
	private string? _correlationId;
	private string? _subject;
	private int _deliveryCount = 1;
	private DateTimeOffset? _enqueuedAt;
	private string? _source;
	private string? _partitionKey;
	private string? _messageGroupId;
	private DateTimeOffset? _lockExpiresAt;
	private readonly Dictionary<string, object> _properties = [];
	private readonly Dictionary<string, object> _providerData = [];

	/// <summary>
	/// Sets the message ID. If not set, a new GUID is generated.
	/// </summary>
	/// <param name="id">The message ID.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithId(string id)
	{
		_id = id;
		return this;
	}

	/// <summary>
	/// Sets the message body from a byte array.
	/// </summary>
	/// <param name="body">The message body.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithBody(byte[] body)
	{
		_body = body;
		return this;
	}

	/// <summary>
	/// Sets the message body from a string using UTF-8 encoding.
	/// Also sets ContentType to "application/json" if not already set.
	/// </summary>
	/// <param name="body">The message body as a string.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithBody(string body)
	{
		_body = Encoding.UTF8.GetBytes(body);
		_contentType ??= "application/json";
		return this;
	}

	/// <summary>
	/// Sets the content type of the message body.
	/// </summary>
	/// <param name="contentType">The MIME content type.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithContentType(string contentType)
	{
		_contentType = contentType;
		return this;
	}

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	/// <param name="messageType">The message type.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithMessageType(string messageType)
	{
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID.
	/// </summary>
	/// <param name="correlationId">The correlation ID.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithCorrelationId(string correlationId)
	{
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the message subject/label.
	/// </summary>
	/// <param name="subject">The subject.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithSubject(string subject)
	{
		_subject = subject;
		return this;
	}

	/// <summary>
	/// Sets the delivery count.
	/// </summary>
	/// <param name="deliveryCount">The number of delivery attempts.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithDeliveryCount(int deliveryCount)
	{
		_deliveryCount = deliveryCount;
		return this;
	}

	/// <summary>
	/// Sets the enqueued timestamp.
	/// </summary>
	/// <param name="enqueuedAt">The enqueue timestamp.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithEnqueuedAt(DateTimeOffset enqueuedAt)
	{
		_enqueuedAt = enqueuedAt;
		return this;
	}

	/// <summary>
	/// Sets the source queue or subscription.
	/// </summary>
	/// <param name="source">The source name.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithSource(string source)
	{
		_source = source;
		return this;
	}

	/// <summary>
	/// Sets the partition key.
	/// </summary>
	/// <param name="partitionKey">The partition key.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithPartitionKey(string partitionKey)
	{
		_partitionKey = partitionKey;
		return this;
	}

	/// <summary>
	/// Sets the message group ID.
	/// </summary>
	/// <param name="messageGroupId">The message group ID.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithMessageGroupId(string messageGroupId)
	{
		_messageGroupId = messageGroupId;
		return this;
	}

	/// <summary>
	/// Sets the lock expiration timestamp.
	/// </summary>
	/// <param name="lockExpiresAt">The lock expiration timestamp.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithLockExpiresAt(DateTimeOffset lockExpiresAt)
	{
		_lockExpiresAt = lockExpiresAt;
		return this;
	}

	/// <summary>
	/// Adds a custom property to the message.
	/// </summary>
	/// <param name="key">The property key.</param>
	/// <param name="value">The property value.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithProperty(string key, object value)
	{
		_properties[key] = value;
		return this;
	}

	/// <summary>
	/// Adds provider-specific data to the message.
	/// </summary>
	/// <param name="key">The provider data key.</param>
	/// <param name="value">The provider data value.</param>
	/// <returns>This builder for chaining.</returns>
	public ReceivedMessageBuilder WithProviderData(string key, object value)
	{
		_providerData[key] = value;
		return this;
	}

	/// <summary>
	/// Builds a <see cref="TransportReceivedMessage"/> with the configured properties.
	/// </summary>
	/// <returns>A new received message.</returns>
	public TransportReceivedMessage Build()
	{
		return new TransportReceivedMessage
		{
			Id = _id ?? Guid.NewGuid().ToString(),
			Body = _body ?? ReadOnlyMemory<byte>.Empty,
			ContentType = _contentType,
			MessageType = _messageType,
			CorrelationId = _correlationId,
			Subject = _subject,
			DeliveryCount = _deliveryCount,
			EnqueuedAt = _enqueuedAt ?? DateTimeOffset.UtcNow,
			Source = _source,
			PartitionKey = _partitionKey,
			MessageGroupId = _messageGroupId,
			LockExpiresAt = _lockExpiresAt,
			Properties = new Dictionary<string, object>(_properties, StringComparer.Ordinal),
			ProviderData = new Dictionary<string, object>(_providerData),
		};
	}

	/// <summary>
	/// Builds multiple received messages with unique IDs.
	/// Each message gets its own unique ID. Other properties are shared across all messages.
	/// </summary>
	/// <param name="count">Number of messages to build.</param>
	/// <returns>A list of received messages.</returns>
	public List<TransportReceivedMessage> BuildMany(int count)
	{
		return [.. Enumerable.Range(0, count).Select(_ => new TransportReceivedMessage
		{
			Id = Guid.NewGuid().ToString(),
			Body = _body ?? ReadOnlyMemory<byte>.Empty,
			ContentType = _contentType,
			MessageType = _messageType,
			CorrelationId = _correlationId,
			Subject = _subject,
			DeliveryCount = _deliveryCount,
			EnqueuedAt = _enqueuedAt ?? DateTimeOffset.UtcNow,
			Source = _source,
			PartitionKey = _partitionKey,
			MessageGroupId = _messageGroupId,
			LockExpiresAt = _lockExpiresAt,
			Properties = new Dictionary<string, object>(_properties, StringComparer.Ordinal),
			ProviderData = new Dictionary<string, object>(_providerData),
		})];
	}
}
