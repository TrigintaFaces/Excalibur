// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Lightweight read-only view over the identity-related fields of a <see cref="MessageEnvelope"/>.
/// </summary>
/// <remarks>
/// This is a zero-allocation facade: it holds a reference to the owning envelope and reads its
/// existing flat backing fields directly, so it does not affect the envelope's pooling or
/// <see cref="MessageEnvelope.Reset"/>/<see cref="MessageEnvelope.Clone"/> semantics. Exposes at most
/// ten properties to satisfy the Microsoft-first focused-value-type design guideline.
/// </remarks>
public readonly record struct EnvelopeIdentity
{
	private readonly MessageEnvelope _envelope;

	internal EnvelopeIdentity(MessageEnvelope envelope) => _envelope = envelope;

	/// <summary>Gets the unique identifier for the message.</summary>
	/// <value> The message identifier or <see langword="null"/>. </value>
	public string? MessageId => _envelope.MessageId;

	/// <summary>Gets the correlation identifier.</summary>
	/// <value> The correlation identifier or <see langword="null"/>. </value>
	public string? CorrelationId => _envelope.CorrelationId;

	/// <summary>Gets the causation identifier.</summary>
	/// <value> The causation identifier or <see langword="null"/>. </value>
	public string? CausationId => _envelope.CausationId;

	/// <summary>Gets the external system identifier.</summary>
	/// <value> The external identifier or <see langword="null"/>. </value>
	public string? ExternalId => _envelope.ExternalId;

	/// <summary>Gets the message type.</summary>
	/// <value> The message type or <see langword="null"/>. </value>
	public string? MessageType => _envelope.MessageType;

	/// <summary>Gets the content type.</summary>
	/// <value> The content type or <see langword="null"/>. </value>
	public string? ContentType => _envelope.ContentType;

	/// <summary>Gets the message source.</summary>
	/// <value> The source or <see langword="null"/>. </value>
	public string? Source => _envelope.Source;

	/// <summary>Gets the message format version.</summary>
	/// <value> The message version or <see langword="null"/>. </value>
	public string? MessageVersion => _envelope.MessageVersion;

	/// <summary>Gets the serializer version.</summary>
	/// <value> The serializer version or <see langword="null"/>. </value>
	public string? SerializerVersion => _envelope.SerializerVersion;

	/// <summary>Gets the contract version.</summary>
	/// <value> The contract version or <see langword="null"/>. </value>
	public string? ContractVersion => _envelope.ContractVersion;
}

/// <summary>
/// Lightweight read-only view over the routing-related fields of a <see cref="MessageEnvelope"/>.
/// </summary>
/// <remarks>
/// Zero-allocation facade over the envelope's existing flat fields; see <see cref="EnvelopeIdentity"/>.
/// </remarks>
public readonly record struct EnvelopeRouting
{
	private readonly MessageEnvelope _envelope;

	internal EnvelopeRouting(MessageEnvelope envelope) => _envelope = envelope;

	/// <summary>Gets the partition key.</summary>
	/// <value> The partition key or <see langword="null"/>. </value>
	public string? PartitionKey => _envelope.PartitionKey;

	/// <summary>Gets the reply-to address.</summary>
	/// <value> The reply-to address or <see langword="null"/>. </value>
	public string? ReplyTo => _envelope.ReplyTo;

	/// <summary>Gets the session identifier.</summary>
	/// <value> The session identifier or <see langword="null"/>. </value>
	public string? SessionId => _envelope.SessionId;

	/// <summary>Gets the message group identifier for FIFO queues.</summary>
	/// <value> The message group identifier or <see langword="null"/>. </value>
	public string? MessageGroupId => _envelope.MessageGroupId;

	/// <summary>Gets the message deduplication identifier.</summary>
	/// <value> The message deduplication identifier or <see langword="null"/>. </value>
	public string? MessageDeduplicationId => _envelope.MessageDeduplicationId;

	/// <summary>Gets the workflow identifier for saga orchestration.</summary>
	/// <value> The workflow identifier or <see langword="null"/>. </value>
	public string? WorkflowId => _envelope.WorkflowId;
}

/// <summary>
/// Lightweight read-only view over the temporal fields of a <see cref="MessageEnvelope"/>.
/// </summary>
/// <remarks>
/// Zero-allocation facade over the envelope's existing flat fields; see <see cref="EnvelopeIdentity"/>.
/// </remarks>
public readonly record struct EnvelopeTiming
{
	private readonly MessageEnvelope _envelope;

	internal EnvelopeTiming(MessageEnvelope envelope) => _envelope = envelope;

	/// <summary>Gets the UTC timestamp when the message was received.</summary>
	/// <value> The received timestamp. </value>
	public DateTimeOffset ReceivedTimestampUtc => _envelope.ReceivedTimestampUtc;

	/// <summary>Gets the UTC timestamp when the message was sent.</summary>
	/// <value> The sent timestamp or <see langword="null"/>. </value>
	public DateTimeOffset? SentTimestampUtc => _envelope.SentTimestampUtc;

	/// <summary>Gets the visibility timeout for message acknowledgment.</summary>
	/// <value> The visibility timeout or <see langword="null"/>. </value>
	public DateTimeOffset? VisibilityTimeout => _envelope.VisibilityTimeout;
}

/// <summary>
/// Lightweight read-only view over the observability fields of a <see cref="MessageEnvelope"/>.
/// </summary>
/// <remarks>
/// Zero-allocation facade over the envelope's existing flat fields; see <see cref="EnvelopeIdentity"/>.
/// </remarks>
public readonly record struct EnvelopeObservability
{
	private readonly MessageEnvelope _envelope;

	internal EnvelopeObservability(MessageEnvelope envelope) => _envelope = envelope;

	/// <summary>Gets the W3C trace parent header.</summary>
	/// <value> The trace parent or <see langword="null"/>. </value>
	public string? TraceParent => _envelope.TraceParent;
}

/// <summary>
/// Lightweight read-only view over the transport/delivery fields of a <see cref="MessageEnvelope"/>.
/// </summary>
/// <remarks>
/// Zero-allocation facade over the envelope's existing flat fields; see <see cref="EnvelopeIdentity"/>.
/// </remarks>
public readonly record struct EnvelopeTransport
{
	private readonly MessageEnvelope _envelope;

	internal EnvelopeTransport(MessageEnvelope envelope) => _envelope = envelope;

	/// <summary>Gets the delivery attempt count.</summary>
	/// <value> The delivery count. </value>
	public int DeliveryCount => _envelope.DeliveryCount;

	/// <summary>Gets the receipt handle for cloud providers.</summary>
	/// <value> The receipt handle or <see langword="null"/>. </value>
	public string? ReceiptHandle => _envelope.ReceiptHandle;

	/// <summary>Gets a value indicating whether this message came from a dead letter queue.</summary>
	/// <value> <see langword="true"/> if dead-lettered; otherwise <see langword="false"/>. </value>
	public bool IsDeadLettered => _envelope.IsDeadLettered;

	/// <summary>Gets the dead letter reason.</summary>
	/// <value> The dead letter reason or <see langword="null"/>. </value>
	public string? DeadLetterReason => _envelope.DeadLetterReason;

	/// <summary>Gets the dead letter error description.</summary>
	/// <value> The dead letter error description or <see langword="null"/>. </value>
	public string? DeadLetterErrorDescription => _envelope.DeadLetterErrorDescription;
}
