// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// High-performance struct for message envelope with minimal allocations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageEnvelopeStruct" /> struct. </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct MessageEnvelopeStruct(
	string messageId,
	ReadOnlyMemory<byte> body,
	long timestampTicks,
	IReadOnlyDictionary<string, string>? headers = null,
	string? correlationId = null,
	string? messageType = null,
	byte priority = 0,
	int timeToLiveSeconds = 0) : IEquatable<MessageEnvelopeStruct>
{
	/// <summary>
	/// Gets the message identifier.
	/// </summary>
	/// <value>
	/// The message identifier.
	/// </value>
	public string MessageId { get; } = messageId ?? throw new ArgumentNullException(nameof(messageId));

	/// <summary>
	/// Gets the message body as bytes.
	/// </summary>
	/// <value>The current <see cref="Body"/> value.</value>
	public ReadOnlyMemory<byte> Body { get; } = body;

	/// <summary>
	/// Gets the timestamp in ticks.
	/// </summary>
	/// <value>The current <see cref="TimestampTicks"/> value.</value>
	public long TimestampTicks { get; } = timestampTicks;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value>The current <see cref="Headers"/> value.</value>
	public IReadOnlyDictionary<string, string>? Headers { get; } = headers;

	/// <summary>
	/// Gets the correlation identifier.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; } = correlationId;

	/// <summary>
	/// Gets the message type.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string? MessageType { get; } = messageType;

	/// <summary>
	/// Gets the message priority.
	/// </summary>
	/// <value>The current <see cref="Priority"/> value.</value>
	public byte Priority { get; } = priority;

	/// <summary>
	/// Gets the time to live in seconds.
	/// </summary>
	/// <value>The current <see cref="TimeToLiveSeconds"/> value.</value>
	public int TimeToLiveSeconds { get; } = timeToLiveSeconds;

	/// <summary>
	/// Gets the timestamp as a DateTime.
	/// </summary>
	/// <value>
	/// The timestamp as a DateTime.
	/// </value>
	public DateTime Timestamp => new(TimestampTicks, DateTimeKind.Utc);

	/// <summary>
	/// Gets the time to live as a TimeSpan.
	/// </summary>
	/// <value>
	/// The time to live as a TimeSpan.
	/// </value>
	public TimeSpan TimeToLive => TimeSpan.FromSeconds(TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified message ID.
	/// </summary>
	public MessageEnvelopeStruct WithMessageId(string messageId) =>
		new(
			messageId,
			Body,
			TimestampTicks,
			Headers,
			CorrelationId,
			MessageType,
			Priority,
			TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified body.
	/// </summary>
	public MessageEnvelopeStruct WithBody(ReadOnlyMemory<byte> body) =>
		new(
			MessageId,
			body,
			TimestampTicks,
			Headers,
			CorrelationId,
			MessageType,
			Priority,
			TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified correlation ID.
	/// </summary>
	public MessageEnvelopeStruct WithCorrelationId(string correlationId) =>
		new(
			MessageId,
			Body,
			TimestampTicks,
			Headers,
			correlationId,
			MessageType,
			Priority,
			TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified message type.
	/// </summary>
	public MessageEnvelopeStruct WithMessageType(string messageType) =>
		new(
			MessageId,
			Body,
			TimestampTicks,
			Headers,
			CorrelationId,
			messageType,
			Priority,
			TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified priority.
	/// </summary>
	public MessageEnvelopeStruct WithPriority(byte priority) =>
		new(
			MessageId,
			Body,
			TimestampTicks,
			Headers,
			CorrelationId,
			MessageType,
			priority,
			TimeToLiveSeconds);

	/// <summary>
	/// Creates a new envelope with the specified time to live.
	/// </summary>
	public MessageEnvelopeStruct WithTimeToLive(TimeSpan timeToLive) =>
		new(
			MessageId,
			Body,
			TimestampTicks,
			Headers,
			CorrelationId,
			MessageType,
			Priority,
			(int)timeToLive.TotalSeconds);

	/// <summary>
	/// Determines whether the specified envelope is equal to the current envelope.
	/// </summary>
	public bool Equals(MessageEnvelopeStruct other) => string.Equals(MessageId, other.MessageId, StringComparison.Ordinal) &&
		Body.Equals(other.Body) &&
		TimestampTicks == other.TimestampTicks &&
string.Equals(CorrelationId, other.CorrelationId, StringComparison.Ordinal) &&
string.Equals(MessageType, other.MessageType, StringComparison.Ordinal) &&
		Priority == other.Priority &&
		TimeToLiveSeconds == other.TimeToLiveSeconds &&
		HeadersEqual(Headers, other.Headers);

	/// <summary>
	/// Determines whether the specified object is equal to the current envelope.
	/// </summary>
	public override bool Equals(object? obj) => obj is MessageEnvelopeStruct other && Equals(other);

	/// <summary>
	/// Returns the hash code for this envelope.
	/// </summary>
	public override int GetHashCode()
	{
		var hash = default(HashCode);
		hash.Add(MessageId);
		hash.Add(Body);
		hash.Add(TimestampTicks);
		hash.Add(CorrelationId);
		hash.Add(MessageType);
		hash.Add(Priority);
		hash.Add(TimeToLiveSeconds);

		if (Headers != null)
		{
			foreach (var kvp in Headers)
			{
				hash.Add(kvp.Key);
				hash.Add(kvp.Value);
			}
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Returns a string representation of this envelope.
	/// </summary>
	public override string ToString() => $"MessageEnvelope[MessageId={MessageId}, Type={MessageType ?? "unknown"}, Size={Body.Length}]";

	/// <summary>
	/// Determines whether two envelopes are equal.
	/// </summary>
	public static bool operator ==(MessageEnvelopeStruct left, MessageEnvelopeStruct right) => left.Equals(right);

	/// <summary>
	/// Determines whether two envelopes are not equal.
	/// </summary>
	public static bool operator !=(MessageEnvelopeStruct left, MessageEnvelopeStruct right) => !left.Equals(right);

	private static bool HeadersEqual(IReadOnlyDictionary<string, string>? left, IReadOnlyDictionary<string, string>? right)
	{
		if (ReferenceEquals(left, right))
		{
			return true;
		}

		if (left == null || right == null)
		{
			return left == null && right == null;
		}

		if (left.Count != right.Count)
		{
			return false;
		}

		foreach (var kvp in left)
		{
			if (!right.TryGetValue(kvp.Key, out var value) || !string.Equals(value, kvp.Value, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}
}
