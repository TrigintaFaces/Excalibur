// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Thrown by the ordering-validation middleware when a received message carries an ordering sequence
/// that is not strictly greater than the last sequence already processed for the same ordering key —
/// i.e. an out-of-order or duplicate delivery.
/// </summary>
/// <remarks>
/// Ordering validation is an opt-in feature: it only enforces when a first-party transport (or the
/// consumer) has stamped a monotonic sequence via
/// <see cref="OrderingContextExtensions.SetOrderingSequence(IMessageContext, long, string?)"/>. When
/// enabled it fails <strong>closed</strong> — a message that would violate per-key ordering is rejected
/// by throwing this exception rather than being processed out of order. Consumers that opt into ordering
/// can catch this to divert the message (e.g. to a re-sequencing buffer or dead-letter path).
/// </remarks>
public sealed class OutOfOrderMessageException : Exception, IFailClosedException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutOfOrderMessageException"/> class.
	/// </summary>
	/// <param name="orderingKey"> The ordering key (partition/stream) the sequence violation occurred on. </param>
	/// <param name="receivedSequence"> The sequence carried by the rejected message. </param>
	/// <param name="lastProcessedSequence"> The last sequence already processed for the ordering key. </param>
	public OutOfOrderMessageException(string orderingKey, long receivedSequence, long lastProcessedSequence)
		: base($"Message with ordering sequence {receivedSequence} on key '{orderingKey}' is out of order: "
			+ $"the last processed sequence was {lastProcessedSequence} and a strictly greater sequence was required.")
	{
		OrderingKey = orderingKey;
		ReceivedSequence = receivedSequence;
		LastProcessedSequence = lastProcessedSequence;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutOfOrderMessageException"/> class with a specified message.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	public OutOfOrderMessageException(string message)
		: base(message) => OrderingKey = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutOfOrderMessageException"/> class with a specified
	/// message and inner exception.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	/// <param name="innerException"> The exception that caused this exception. </param>
	public OutOfOrderMessageException(string message, Exception innerException)
		: base(message, innerException) => OrderingKey = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutOfOrderMessageException"/> class.
	/// </summary>
	public OutOfOrderMessageException()
		: base("A message was received out of order for its ordering key.") => OrderingKey = string.Empty;

	/// <summary>
	/// Gets the ordering key (partition/stream) the sequence violation occurred on.
	/// </summary>
	public string OrderingKey { get; }

	/// <summary>
	/// Gets the sequence carried by the rejected message.
	/// </summary>
	public long ReceivedSequence { get; }

	/// <summary>
	/// Gets the last sequence already processed for the ordering key.
	/// </summary>
	public long LastProcessedSequence { get; }
}
