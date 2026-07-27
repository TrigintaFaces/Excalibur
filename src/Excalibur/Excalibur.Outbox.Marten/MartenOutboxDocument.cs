// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Marten document representing a staged outbox message.
/// </summary>
/// <remarks>
/// This is the persisted projection of an <see cref="OutboundMessage"/>. Marten uses the
/// <see cref="Id"/> property as the document identity, which lets <c>IDocumentSession.Insert</c>
/// enforce the exactly-once staging invariant: a second stage of the same message id is a real
/// conditional-write conflict, not a silent upsert.
/// </remarks>
internal sealed class MartenOutboxDocument
{
	/// <summary>Gets or sets the message identity (Marten document id).</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the fully qualified message type name.</summary>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>Gets or sets the serialized message payload.</summary>
	public byte[] Payload { get; set; } = [];

	/// <summary>Gets or sets the routing destination.</summary>
	public string Destination { get; set; } = string.Empty;

	/// <summary>Gets or sets the message headers.</summary>
	public Dictionary<string, object> Headers { get; set; } = new(StringComparer.Ordinal);

	/// <summary>Gets or sets the creation timestamp.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Gets or sets the scheduled delivery timestamp, if any.</summary>
	public DateTimeOffset? ScheduledAt { get; set; }

	/// <summary>Gets or sets the successful-send timestamp, if any.</summary>
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>Gets or sets the delivery status.</summary>
	public OutboxStatus Status { get; set; }

	/// <summary>Gets or sets the retry count.</summary>
	public int RetryCount { get; set; }

	/// <summary>Gets or sets the last error, if any.</summary>
	public string? LastError { get; set; }

	/// <summary>Gets or sets the last delivery-attempt timestamp, if any.</summary>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>Gets or sets the correlation identifier.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Gets or sets the causation identifier.</summary>
	public string? CausationId { get; set; }

	/// <summary>Gets or sets the tenant identifier.</summary>
	public string? TenantId { get; set; }

	/// <summary>Gets or sets the delivery priority.</summary>
	public int Priority { get; set; }

	/// <summary>Gets or sets the partition key used to preserve per-partition ordering on the transport.</summary>
	public string? PartitionKey { get; set; }

	/// <summary>Gets or sets the group key that associates related messages.</summary>
	public string? GroupKey { get; set; }

	/// <summary>Gets or sets the store-assigned monotonic sequence number.</summary>
	public long SequenceNumber { get; set; }

	/// <summary>Gets or sets the comma-separated transports this message targets.</summary>
	public string? TargetTransports { get; set; }

	/// <summary>Gets or sets a value indicating whether the message is delivered to more than one transport.</summary>
	/// <value><see langword="true"/> when the message targets multiple transports; otherwise, <see langword="false"/>.</value>
	public bool IsMultiTransport { get; set; }

	/// <summary>
	/// Creates a document from the supplied outbound message.
	/// </summary>
	/// <param name="message"> The outbound message to project. </param>
	/// <returns> A new <see cref="MartenOutboxDocument"/>. </returns>
	public static MartenOutboxDocument FromOutbound(OutboundMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		return new MartenOutboxDocument
		{
			Id = message.Id,
			MessageType = message.MessageType,
			Payload = message.Payload,
			Destination = message.Destination,
			Headers = new Dictionary<string, object>(message.Headers, StringComparer.Ordinal),
			CreatedAt = message.CreatedAt,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			Status = message.Status,
			RetryCount = message.RetryCount,
			LastError = message.LastError,
			LastAttemptAt = message.LastAttemptAt,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId,
			Priority = message.Priority,
			PartitionKey = message.PartitionKey,
			GroupKey = message.GroupKey,
			SequenceNumber = message.SequenceNumber,
			TargetTransports = message.TargetTransports,
			IsMultiTransport = message.IsMultiTransport,
		};
	}

	/// <summary>
	/// Projects this document back to an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <returns> The reconstructed outbound message. </returns>
	public OutboundMessage ToOutbound()
	{
		return new OutboundMessage(MessageType, Payload, Destination, Headers)
		{
			Id = Id,
			CreatedAt = CreatedAt,
			ScheduledAt = ScheduledAt,
			SentAt = SentAt,
			Status = Status,
			RetryCount = RetryCount,
			LastError = LastError,
			LastAttemptAt = LastAttemptAt,
			CorrelationId = CorrelationId,
			CausationId = CausationId,
			TenantId = TenantId,
			Priority = Priority,
			PartitionKey = PartitionKey,
			GroupKey = GroupKey,
			SequenceNumber = SequenceNumber,
			TargetTransports = TargetTransports,
			IsMultiTransport = IsMultiTransport,
		};
	}
}
