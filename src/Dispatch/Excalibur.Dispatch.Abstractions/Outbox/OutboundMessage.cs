// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents an outbound message staged in the outbox for reliable delivery.
/// </summary>
/// <remarks>
/// Outbound messages are created during business operations and staged in the outbox within the same transaction. This ensures that
/// messages are only published if the business operation succeeds, maintaining consistency between state changes and published events.
/// </remarks>
public sealed class OutboundMessage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OutboundMessage" /> class.
	/// </summary>
	public OutboundMessage()
	{
		Id = Guid.NewGuid().ToString();
		CreatedAt = DateTimeOffset.UtcNow;
		Status = OutboxStatus.Staged;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboundMessage" /> class with specified values.
	/// </summary>
	/// <param name="messageType"> The fully qualified type name of the message. </param>
	/// <param name="payload"> The serialized message payload. </param>
	/// <param name="destination"> The destination where the message should be delivered. </param>
	/// <param name="headers"> Optional message headers and metadata. </param>
	public OutboundMessage(string messageType, byte[] payload, string destination, IDictionary<string, object>? headers = null)
	{
		Id = Guid.NewGuid().ToString();
		MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
		Payload = payload ?? throw new ArgumentNullException(nameof(payload));
		Destination = destination ?? throw new ArgumentNullException(nameof(destination));
		Headers = headers ?? new Dictionary<string, object>(StringComparer.Ordinal);
		CreatedAt = DateTimeOffset.UtcNow;
		Status = OutboxStatus.Staged;
	}

	/// <summary>
	/// Gets or sets the unique identifier of the outbound message.
	/// </summary>
	/// <value> A unique message identifier, typically a GUID. </value>
	public string Id { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified type name of the message.
	/// </summary>
	/// <value> The .NET type name used for message deserialization and routing. </value>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized message payload.
	/// </summary>
	/// <value> The message data as bytes, typically JSON or binary format. </value>
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// Gets the message headers and metadata.
	/// </summary>
	/// <value> Dictionary containing routing information, correlation IDs, and other message properties. </value>
	public IDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the destination where the message should be delivered.
	/// </summary>
	/// <value> Queue name, topic, endpoint, or routing key for message delivery. </value>
	public string Destination { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the message was created.
	/// </summary>
	/// <value> UTC timestamp of message creation. </value>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message should be delivered.
	/// </summary>
	/// <value> UTC timestamp for scheduled delivery, null for immediate delivery. </value>
	public DateTimeOffset? ScheduledAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was successfully sent.
	/// </summary>
	/// <value> UTC timestamp of successful delivery, null if not yet sent. </value>
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>
	/// Gets or sets the current delivery status of the message.
	/// </summary>
	/// <value> The outbox status indicating the delivery state. </value>
	public OutboxStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts made.
	/// </summary>
	/// <value> The retry count, starting from 0 for the first attempt. </value>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the error message if delivery failed.
	/// </summary>
	/// <value> Error description or exception message, null if no error occurred. </value>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last delivery attempt.
	/// </summary>
	/// <value> UTC timestamp of the most recent delivery attempt. </value>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for tracing.
	/// </summary>
	/// <value> Correlation ID for distributed tracing and message flow tracking. </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the causation identifier linking to the triggering message.
	/// </summary>
	/// <value> ID of the message that caused this message to be created. </value>
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value> Tenant ID for tenant isolation and routing. </value>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the message priority for delivery ordering.
	/// </summary>
	/// <value> Priority level, with higher values indicating higher priority. </value>
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets the partition key for ordered delivery within the same partition.
	/// </summary>
	/// <remarks>
	/// Messages with the same partition key are guaranteed to be delivered in order.
	/// Maps to Kafka partition key, Azure Service Bus session ID, or SQS message group ID.
	/// When null, the transport uses its default partitioning strategy.
	/// </remarks>
	/// <value> The partition key string, or null for default partitioning. </value>
	public string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the group key for logical message grouping.
	/// </summary>
	/// <remarks>
	/// Used for message deduplication and logical grouping across partitions.
	/// Maps to Kafka message key, Azure Service Bus message group, or SQS deduplication ID prefix.
	/// When null, no grouping is applied.
	/// </remarks>
	/// <value> The group key string, or null for no grouping. </value>
	public string? GroupKey { get; set; }

	/// <summary>
	/// Gets or sets the monotonically increasing sequence number for ordering guarantees.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <see cref="PartitionKey"/> is set, messages with the same partition key MUST be
	/// delivered in ascending <see cref="SequenceNumber"/> order. Outbox processors use this
	/// value to restore ordering after batch failures and retries.
	/// </para>
	/// <para>
	/// The value is typically assigned by the outbox store during staging. A value of 0 means
	/// no explicit sequencing was requested -- ordering falls back to <see cref="CreatedAt"/>.
	/// </para>
	/// </remarks>
	/// <value> A monotonically increasing sequence value, or 0 for unsequenced messages. </value>
	public long SequenceNumber { get; set; }

	/// <summary>
	/// Gets or sets the list of target transports for this message.
	/// </summary>
	/// <value> A comma-separated list of transport names, or null for default routing. </value>
	public string? TargetTransports { get; set; }

	/// <summary>
	/// Gets or sets whether this message requires delivery to multiple transports.
	/// </summary>
	/// <value> True if multi-transport delivery is enabled. </value>
	public bool IsMultiTransport { get; set; }

	/// <summary>
	/// Gets the per-transport delivery records.
	/// </summary>
	/// <remarks>
	/// This collection tracks the delivery status of the message to each target transport independently.
	/// It is typically populated by the outbox store when retrieving messages for multi-transport scenarios.
	/// </remarks>
	public ICollection<OutboundMessageTransport> TransportDeliveries { get; } = [];

	/// <summary>
	/// Adds a target transport for delivery.
	/// </summary>
	/// <param name="transportName">The transport name.</param>
	/// <param name="destination">Optional transport-specific destination.</param>
	/// <returns>The created transport delivery record.</returns>
	public OutboundMessageTransport AddTransport(string transportName, string? destination = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		var delivery = new OutboundMessageTransport(Id, transportName) { Destination = destination ?? Destination };

		TransportDeliveries.Add(delivery);
		IsMultiTransport = true;

		// Update TargetTransports string
		UpdateTargetTransportsString();

		return delivery;
	}

	/// <summary>
	/// Gets the delivery record for a specific transport.
	/// </summary>
	/// <param name="transportName">The transport name.</param>
	/// <returns>The transport delivery record, or null if not found.</returns>
	public OutboundMessageTransport? GetTransportDelivery(string transportName)
	{
		return TransportDeliveries.FirstOrDefault(t =>
			string.Equals(t.TransportName, transportName, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Gets all pending transport deliveries.
	/// </summary>
	/// <returns>Transport deliveries that are pending.</returns>
	public IEnumerable<OutboundMessageTransport> GetPendingTransportDeliveries()
	{
		return TransportDeliveries.Where(t => t.Status == TransportDeliveryStatus.Pending);
	}

	/// <summary>
	/// Gets all failed transport deliveries.
	/// </summary>
	/// <returns>Transport deliveries that have failed.</returns>
	public IEnumerable<OutboundMessageTransport> GetFailedTransportDeliveries()
	{
		return TransportDeliveries.Where(t => t.Status == TransportDeliveryStatus.Failed);
	}

	/// <summary>
	/// Determines if all transport deliveries are complete.
	/// </summary>
	/// <returns>True if all transports have been sent or skipped.</returns>
	public bool AreAllTransportsComplete()
	{
		if (!IsMultiTransport || TransportDeliveries.Count == 0)
		{
			return Status == OutboxStatus.Sent;
		}

		return TransportDeliveries.All(t =>
			t.Status is TransportDeliveryStatus.Sent or TransportDeliveryStatus.Skipped);
	}

	/// <summary>
	/// Updates the aggregate status based on transport delivery statuses.
	/// </summary>
	public void UpdateAggregateStatus()
	{
		if (!IsMultiTransport || TransportDeliveries.Count == 0)
		{
			return;
		}

		if (AreAllTransportsComplete())
		{
			MarkSent();
		}
		else if (TransportDeliveries.Any(t => t.Status == TransportDeliveryStatus.Sending))
		{
			Status = OutboxStatus.Sending;
		}
		else if (TransportDeliveries.All(t => t.Status == TransportDeliveryStatus.Failed))
		{
			Status = OutboxStatus.Failed;
			LastError = "All transport deliveries failed";
		}
		else if (TransportDeliveries.Any(t => t.Status == TransportDeliveryStatus.Failed))
		{
			Status = OutboxStatus.PartiallyFailed;
			LastError =
				$"{TransportDeliveries.Count(t => t.Status == TransportDeliveryStatus.Failed)} of {TransportDeliveries.Count} transports failed";
		}
	}

	/// <summary>
	/// Marks the message as currently being sent.
	/// </summary>
	public void MarkSending()
	{
		Status = OutboxStatus.Sending;
		LastAttemptAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Marks the message as successfully sent.
	/// </summary>
	public void MarkSent()
	{
		Status = OutboxStatus.Sent;
		SentAt = DateTimeOffset.UtcNow;
		LastError = null;
	}

	/// <summary>
	/// Marks the message as failed with the specified error.
	/// </summary>
	/// <param name="error"> The error description. </param>
	public void MarkFailed(string error)
	{
		ArgumentException.ThrowIfNullOrEmpty(error);

		Status = OutboxStatus.Failed;
		LastError = error;
		RetryCount++;
		LastAttemptAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Determines if the message is ready for delivery.
	/// </summary>
	/// <returns> True if the message can be sent now, false otherwise. </returns>
	public bool IsReadyForDelivery()
	{
		if (Status != OutboxStatus.Staged)
		{
			return false;
		}

		if (ScheduledAt > DateTimeOffset.UtcNow)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Determines if the message is eligible for retry delivery.
	/// </summary>
	/// <param name="maxRetries"> Maximum number of retry attempts allowed. </param>
	/// <param name="retryDelayMinutes"> Minimum delay between retry attempts in minutes. </param>
	/// <param name="useExponentialBackoff"> Whether to use exponential backoff for retry delays. </param>
	/// <param name="maxRetryDelayMinutes"> Maximum retry delay in minutes when using exponential backoff. </param>
	/// <returns> True if the message can be retried, false otherwise. </returns>
	public bool IsEligibleForRetry(int maxRetries = 3, int retryDelayMinutes = 5, bool useExponentialBackoff = false, int maxRetryDelayMinutes = 30)
	{
		if (Status != OutboxStatus.Failed)
		{
			return false;
		}

		if (RetryCount >= maxRetries)
		{
			return false;
		}

		if (LastAttemptAt.HasValue)
		{
			double delayMinutes = retryDelayMinutes;

			if (useExponentialBackoff && RetryCount > 0)
			{
				// Exponential backoff: baseDelay * 2^(retryCount-1)
				delayMinutes = retryDelayMinutes * Math.Pow(2, RetryCount - 1);
				delayMinutes = Math.Min(delayMinutes, maxRetryDelayMinutes);
			}

			var nextRetryTime = LastAttemptAt.Value.AddMinutes(delayMinutes);
			return DateTimeOffset.UtcNow >= nextRetryTime;
		}

		return true;
	}

	/// <summary>
	/// Gets the age of the message since creation.
	/// </summary>
	/// <returns> The time elapsed since the message was created. </returns>
	public TimeSpan GetAge() => DateTimeOffset.UtcNow - CreatedAt;

	/// <summary>
	/// Gets the age of the message since last delivery attempt.
	/// </summary>
	/// <returns> The time elapsed since the last delivery attempt, null if never attempted. </returns>
	public TimeSpan? GetTimeSinceLastAttempt() => LastAttemptAt.HasValue ? DateTimeOffset.UtcNow - LastAttemptAt.Value : null;

	/// <inheritdoc />
	public override string ToString() => $"OutboundMessage[{Id}]: {MessageType} -> {Destination} - {Status}";

	private void UpdateTargetTransportsString()
	{
		TargetTransports = string.Join(",", TransportDeliveries.Select(t => t.TransportName).Distinct());
	}
}
