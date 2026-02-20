// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Builder for creating MessageEnvelopeStruct instances with pooled resources.
/// </summary>
public struct MessageEnvelopeBuilder : IEquatable<MessageEnvelopeBuilder>
{
	private string _messageId;
	private ReadOnlyMemory<byte> _body;
	private long _timestampTicks;
	private Dictionary<string, string>? _headers;
	private string _correlationId;
	private string _messageType;
	private byte _priority;
	private int _timeToLiveSeconds;

	/// <summary>
	/// Determines whether two builders are equal.
	/// </summary>
	public static bool operator ==(MessageEnvelopeBuilder left, MessageEnvelopeBuilder right) => left.Equals(right);

	/// <summary>
	/// Determines whether two builders are not equal.
	/// </summary>
	public static bool operator !=(MessageEnvelopeBuilder left, MessageEnvelopeBuilder right) => !left.Equals(right);

	/// <summary>
	/// Sets the message identifier for the envelope.
	/// </summary>
	/// <param name="messageId"> The unique identifier for the message. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithMessageId(string messageId)
	{
		_messageId = messageId;
		return this;
	}

	/// <summary>
	/// Sets the message body content as a byte array.
	/// </summary>
	/// <param name="body"> The serialized message body content. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithBody(ReadOnlyMemory<byte> body)
	{
		_body = body;
		return this;
	}

	/// <summary>
	/// Sets the message timestamp using a DateTime value.
	/// </summary>
	/// <param name="timestamp"> The timestamp when the message was created. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithTimestamp(DateTime timestamp)
	{
		_timestampTicks = timestamp.Ticks;
		return this;
	}

	/// <summary>
	/// Sets the message timestamp using tick values for high-precision timing.
	/// </summary>
	/// <param name="timestampTicks"> The timestamp in ticks (100-nanosecond intervals). </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithTimestampTicks(long timestampTicks)
	{
		_timestampTicks = timestampTicks;
		return this;
	}

	/// <summary>
	/// Adds or updates a single header in the message envelope.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithHeader(string key, string value)
	{
		_headers ??= [];
		_headers[key] = value;
		return this;
	}

	/// <summary>
	/// Adds or updates multiple headers in the message envelope.
	/// </summary>
	/// <param name="headers"> An array of key-value pairs representing the headers to add. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when headers is null. </exception>
	public MessageEnvelopeBuilder WithHeaders(params (string key, string value)[] headers)
	{
		ArgumentNullException.ThrowIfNull(headers);
		_headers ??= [];
		foreach (var (key, value) in headers)
		{
			_headers[key] = value;
		}

		return this;
	}

	/// <summary>
	/// Sets the correlation identifier for message tracing and correlation.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier that links related messages. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithCorrelationId(string correlationId)
	{
		_correlationId = correlationId;
		return this;
	}

	/// <summary>
	/// Sets the message type identifier for routing and deserialization.
	/// </summary>
	/// <param name="messageType"> The type identifier of the message content. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithMessageType(string messageType)
	{
		_messageType = messageType;
		return this;
	}

	/// <summary>
	/// Sets the message priority level for queue processing order.
	/// </summary>
	/// <param name="priority"> The priority level (0-255, with higher values indicating higher priority). </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithPriority(byte priority)
	{
		_priority = priority;
		return this;
	}

	/// <summary>
	/// Sets the time-to-live duration for the message before it expires.
	/// </summary>
	/// <param name="timeToLive"> The maximum duration the message should remain valid. </param>
	/// <returns> The current builder instance for fluent chaining. </returns>
	public MessageEnvelopeBuilder WithTimeToLive(TimeSpan timeToLive)
	{
		_timeToLiveSeconds = (int)timeToLive.TotalSeconds;
		return this;
	}

	/// <summary>
	/// Constructs the final MessageEnvelopeStruct with all configured properties.
	/// </summary>
	/// <returns> A new MessageEnvelopeStruct instance containing all the configured values. </returns>
	public MessageEnvelopeStruct Build()
	{
		if (string.IsNullOrEmpty(_messageId))
		{
			_messageId = Guid.NewGuid().ToString();
		}

		if (_timestampTicks == default)
		{
			_timestampTicks = DateTimeOffset.UtcNow.Ticks;
		}

		return new MessageEnvelopeStruct(
			_messageId,
			_body,
			_timestampTicks,
			_headers,
			_correlationId,
			_messageType,
			_priority,
			_timeToLiveSeconds);
	}

	/// <summary>
	/// Releases any resources associated with the builder (no resources to dispose for this struct).
	/// </summary>
	// R0.8: Make method static - Dispose() should remain instance method even when empty, for semantic correctness
#pragma warning disable MA0038

	public readonly void Dispose()
	{
		/* Nothing to dispose */
	}

#pragma warning restore MA0038

	/// <summary>
	/// Determines whether the specified builder is equal to the current builder.
	/// </summary>
	public readonly bool Equals(MessageEnvelopeBuilder other) => string.Equals(_messageId, other._messageId, StringComparison.Ordinal) &&
		_body.Equals(other._body) &&
		_timestampTicks == other._timestampTicks &&
string.Equals(_correlationId, other._correlationId, StringComparison.Ordinal) &&
string.Equals(_messageType, other._messageType, StringComparison.Ordinal) &&
		_priority == other._priority &&
		_timeToLiveSeconds == other._timeToLiveSeconds &&
		((_headers == null && other._headers == null) ||
		 (_headers != null && other._headers != null && _headers.Count == other._headers.Count &&
		  _headers.All(kvp => other._headers.TryGetValue(kvp.Key, out var value) && string.Equals(value, kvp.Value, StringComparison.Ordinal))));

	/// <summary>
	/// Determines whether the specified object is equal to the current builder.
	/// </summary>
	public override readonly bool Equals(object? obj) => obj is MessageEnvelopeBuilder other && Equals(other);

	/// <summary>
	/// Returns the hash code for this builder.
	/// </summary>
	public override readonly int GetHashCode()
	{
		var hash = default(HashCode);
		hash.Add(_messageId);
		hash.Add(_body);
		hash.Add(_timestampTicks);
		hash.Add(_correlationId);
		hash.Add(_messageType);
		hash.Add(_priority);
		hash.Add(_timeToLiveSeconds);
		if (_headers != null)
		{
			foreach (var kvp in _headers.OrderBy(static x => x.Key, StringComparer.Ordinal))
			{
				hash.Add(kvp.Key);
				hash.Add(kvp.Value);
			}
		}

		return hash.ToHashCode();
	}
}
