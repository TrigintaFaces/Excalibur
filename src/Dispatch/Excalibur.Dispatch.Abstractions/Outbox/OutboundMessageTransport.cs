// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the delivery status of a message to a specific transport.
/// </summary>
/// <remarks>
/// <para>
/// This class tracks the delivery state of a single outbound message to a single transport.
/// When a message needs to be published to multiple transports, each transport has its own
/// <see cref="OutboundMessageTransport"/> record to track success/failure independently.
/// </para>
/// </remarks>
public sealed class OutboundMessageTransport
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutboundMessageTransport"/> class.
	/// </summary>
	public OutboundMessageTransport()
	{
		Id = Guid.NewGuid().ToString();
		CreatedAt = DateTimeOffset.UtcNow;
		Status = TransportDeliveryStatus.Pending;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboundMessageTransport"/> class.
	/// </summary>
	/// <param name="messageId">The ID of the parent outbound message.</param>
	/// <param name="transportName">The name of the target transport.</param>
	public OutboundMessageTransport(string messageId, string transportName)
		: this()
	{
		MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
		TransportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
	}

	/// <summary>
	/// Gets or sets the unique identifier for this transport record.
	/// </summary>
	public string Id { get; set; }

	/// <summary>
	/// Gets or sets the ID of the parent outbound message.
	/// </summary>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the target transport.
	/// </summary>
	public string TransportName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the transport-specific destination (queue, topic, exchange).
	/// </summary>
	public string? Destination { get; set; }

	/// <summary>
	/// Gets or sets the current delivery status for this transport.
	/// </summary>
	public TransportDeliveryStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this record was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when delivery was attempted.
	/// </summary>
	public DateTimeOffset? AttemptedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when delivery was successful.
	/// </summary>
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts.
	/// </summary>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message if delivery failed.
	/// </summary>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets transport-specific metadata as JSON.
	/// </summary>
	public string? TransportMetadata { get; set; }

	/// <summary>
	/// Marks this transport delivery as in progress.
	/// </summary>
	public void MarkSending()
	{
		Status = TransportDeliveryStatus.Sending;
		AttemptedAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Marks this transport delivery as successful.
	/// </summary>
	public void MarkSent()
	{
		Status = TransportDeliveryStatus.Sent;
		SentAt = DateTimeOffset.UtcNow;
		LastError = null;
	}

	/// <summary>
	/// Marks this transport delivery as failed.
	/// </summary>
	/// <param name="error">The error description.</param>
	public void MarkFailed(string error)
	{
		ArgumentException.ThrowIfNullOrEmpty(error);

		Status = TransportDeliveryStatus.Failed;
		LastError = error;
		RetryCount++;
		AttemptedAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Marks this transport delivery as skipped (e.g., due to routing rules).
	/// </summary>
	/// <param name="reason">The reason for skipping.</param>
	public void MarkSkipped(string? reason = null)
	{
		Status = TransportDeliveryStatus.Skipped;
		LastError = reason;
	}

	/// <summary>
	/// Determines if this transport delivery is eligible for retry.
	/// </summary>
	/// <param name="maxRetries">Maximum retry attempts.</param>
	/// <param name="retryDelayMinutes">Minimum delay between retries.</param>
	/// <returns>True if eligible for retry.</returns>
	public bool IsEligibleForRetry(int maxRetries = 3, int retryDelayMinutes = 5)
	{
		if (Status != TransportDeliveryStatus.Failed)
		{
			return false;
		}

		if (RetryCount >= maxRetries)
		{
			return false;
		}

		if (AttemptedAt.HasValue)
		{
			var nextRetryTime = AttemptedAt.Value.AddMinutes(retryDelayMinutes);
			return DateTimeOffset.UtcNow >= nextRetryTime;
		}

		return true;
	}

	/// <inheritdoc/>
	public override string ToString() =>
		$"OutboundMessageTransport[{Id}]: {MessageId} -> {TransportName} - {Status}";
}

/// <summary>
/// Represents the delivery status for a specific transport.
/// </summary>
public enum TransportDeliveryStatus
{
	/// <summary>
	/// Delivery to this transport is pending.
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Delivery to this transport is in progress.
	/// </summary>
	Sending = 1,

	/// <summary>
	/// Delivery to this transport was successful.
	/// </summary>
	Sent = 2,

	/// <summary>
	/// Delivery to this transport failed.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// Delivery to this transport was skipped.
	/// </summary>
	Skipped = 4
}
