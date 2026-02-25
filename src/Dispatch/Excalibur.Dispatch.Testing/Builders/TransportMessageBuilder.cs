// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Builders;

/// <summary>
/// Fluent builder for creating <see cref="TransportMessage"/> instances in tests.
/// Provides sensible defaults for all properties with override capability.
/// </summary>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// var message = new TransportMessageBuilder()
///     .WithBody("test-payload")
///     .WithMessageType("OrderPlaced")
///     .WithCorrelationId("corr-123")
///     .Build();
/// </code>
/// </para>
/// </remarks>
public sealed class TransportMessageBuilder
{
	private string? _id;
	private ReadOnlyMemory<byte>? _body;
	private string? _contentType;
	private string? _messageType;
	private string? _correlationId;
	private string? _subject;
	private TimeSpan? _timeToLive;
	private DateTimeOffset? _createdAt;
	private readonly Dictionary<string, object> _properties = [];

	/// <summary>
	/// Sets the message ID. If not set, a new GUID is generated.
	/// </summary>
	/// <param name="id">The message ID.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithId(string id)
	{
		_id = id;
		return this;
	}

	/// <summary>
	/// Sets the message body from a byte array.
	/// </summary>
	/// <param name="body">The message body.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithBody(byte[] body)
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
	public TransportMessageBuilder WithBody(string body)
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
	public TransportMessageBuilder WithContentType(string contentType)
	{
		_contentType = contentType;
		return this;
	}

	/// <summary>
	/// Sets the message type identifier.
	/// </summary>
	/// <param name="messageType">The message type.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithMessageType(string messageType)
	{
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the correlation ID.
	/// </summary>
	/// <param name="correlationId">The correlation ID.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithCorrelationId(string correlationId)
	{
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the message subject/label.
	/// </summary>
	/// <param name="subject">The subject.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithSubject(string subject)
	{
		_subject = subject;
		return this;
	}

	/// <summary>
	/// Sets the message time-to-live.
	/// </summary>
	/// <param name="timeToLive">The time-to-live duration.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithTimeToLive(TimeSpan timeToLive)
	{
		_timeToLive = timeToLive;
		return this;
	}

	/// <summary>
	/// Sets the creation timestamp.
	/// </summary>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithCreatedAt(DateTimeOffset createdAt)
	{
		_createdAt = createdAt;
		return this;
	}

	/// <summary>
	/// Adds a custom property to the message.
	/// </summary>
	/// <param name="key">The property key.</param>
	/// <param name="value">The property value.</param>
	/// <returns>This builder for chaining.</returns>
	public TransportMessageBuilder WithProperty(string key, object value)
	{
		_properties[key] = value;
		return this;
	}

	/// <summary>
	/// Builds a <see cref="TransportMessage"/> with the configured properties.
	/// </summary>
	/// <returns>A new transport message.</returns>
	public TransportMessage Build()
	{
		var message = new TransportMessage
		{
			Id = _id ?? Guid.NewGuid().ToString(),
			Body = _body ?? ReadOnlyMemory<byte>.Empty,
			ContentType = _contentType,
			MessageType = _messageType,
			CorrelationId = _correlationId,
			Subject = _subject,
			TimeToLive = _timeToLive,
			CreatedAt = _createdAt ?? DateTimeOffset.UtcNow,
		};

		foreach (var property in _properties)
		{
			message.Properties[property.Key] = property.Value;
		}

		return message;
	}

	/// <summary>
	/// Builds multiple transport messages with unique IDs.
	/// Each message gets its own unique ID. Other properties are shared across all messages.
	/// </summary>
	/// <param name="count">Number of messages to build.</param>
	/// <returns>A list of transport messages.</returns>
	public List<TransportMessage> BuildMany(int count)
	{
		return [.. Enumerable.Range(0, count).Select(_ =>
		{
			var message = new TransportMessage
			{
				Id = Guid.NewGuid().ToString(),
				Body = _body ?? ReadOnlyMemory<byte>.Empty,
				ContentType = _contentType,
				MessageType = _messageType,
				CorrelationId = _correlationId,
				Subject = _subject,
				TimeToLive = _timeToLive,
				CreatedAt = _createdAt ?? DateTimeOffset.UtcNow,
			};

			foreach (var property in _properties)
			{
				message.Properties[property.Key] = property.Value;
			}

			return message;
		})];
	}
}
