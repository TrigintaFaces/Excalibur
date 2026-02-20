// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a message entry in the inbox store for duplicate detection and processing tracking.
/// </summary>
/// <remarks>
/// <para>
/// Each inbox entry tracks the complete lifecycle of an incoming message from receipt through processing
/// completion for a specific handler. Entries are keyed by <c>(MessageId, HandlerType)</c>, allowing the same
/// message to be processed independently by multiple handlers.
/// </para>
/// <para>
/// This enables at-most-once processing semantics per handler and provides audit trails for message handling.
/// </para>
/// </remarks>
public sealed class InboxEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InboxEntry" /> class.
	/// </summary>
	public InboxEntry()
	{
		ReceivedAt = DateTimeOffset.UtcNow;
		Status = InboxStatus.Received;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxEntry" /> class with specified values.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing this message.</param>
	/// <param name="messageType">The fully qualified type name of the message.</param>
	/// <param name="payload">The serialized message payload.</param>
	/// <param name="metadata">Additional message metadata.</param>
	public InboxEntry(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object>? metadata = null)
	{
		MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
		HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
		MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
		Payload = payload ?? throw new ArgumentNullException(nameof(payload));
		Metadata = metadata ?? new Dictionary<string, object>(StringComparer.Ordinal);
		ReceivedAt = DateTimeOffset.UtcNow;
		Status = InboxStatus.Received;
	}

	/// <summary>
	/// Gets or sets the unique identifier of the message.
	/// </summary>
	/// <value>The message identifier, typically a GUID or correlation ID.</value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the fully qualified type name of the handler processing this message.
	/// </summary>
	/// <value>The .NET type name of the handler, forming part of the composite key with MessageId.</value>
	public string HandlerType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the fully qualified type name of the message.
	/// </summary>
	/// <value>The .NET type name used for message deserialization and routing.</value>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized message payload.
	/// </summary>
	/// <value> The message data as bytes, typically JSON or binary format. </value>
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// Gets the additional message metadata including headers and context.
	/// </summary>
	/// <value> A dictionary containing headers, correlation IDs, tenant information, and other context data. </value>
	public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the timestamp when the message was received.
	/// </summary>
	/// <value> UTC timestamp of message receipt. </value>
	public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the timestamp when the message processing was completed.
	/// </summary>
	/// <value> UTC timestamp of successful processing completion, null if not yet processed. </value>
	public DateTimeOffset? ProcessedAt { get; set; }

	/// <summary>
	/// Gets or sets the current processing status of the message.
	/// </summary>
	/// <value> The inbox status indicating the processing state. </value>
	public InboxStatus Status { get; set; } = InboxStatus.Received;

	/// <summary>
	/// Gets or sets the error message if processing failed.
	/// </summary>
	/// <value> Error description or exception message, null if no error occurred. </value>
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts made.
	/// </summary>
	/// <value> The retry count, starting from 0 for the first attempt. </value>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last processing attempt.
	/// </summary>
	/// <value> UTC timestamp of the most recent processing attempt. </value>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for tracing.
	/// </summary>
	/// <value> Correlation ID for distributed tracing and message flow tracking. </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <value> Tenant ID for tenant isolation and routing. </value>
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the message source or origin.
	/// </summary>
	/// <value> Source system, queue, or endpoint that produced the message. </value>
	public string? Source { get; set; }

	/// <summary>
	/// Marks the entry as currently being processed.
	/// </summary>
	public void MarkProcessing()
	{
		Status = InboxStatus.Processing;
		LastAttemptAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Marks the entry as successfully processed.
	/// </summary>
	public void MarkProcessed()
	{
		Status = InboxStatus.Processed;
		ProcessedAt = DateTimeOffset.UtcNow;
		LastError = null;
	}

	/// <summary>
	/// Marks the entry as failed with the specified error.
	/// </summary>
	/// <param name="error"> The error description. </param>
	public void MarkFailed(string error)
	{
		ArgumentException.ThrowIfNullOrEmpty(error);

		Status = InboxStatus.Failed;
		LastError = error;
		RetryCount++;
		LastAttemptAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Determines if the entry is eligible for retry processing.
	/// </summary>
	/// <param name="maxRetries"> Maximum number of retry attempts allowed. </param>
	/// <param name="retryDelayMinutes"> Minimum delay between retry attempts in minutes. </param>
	/// <returns> True if the entry can be retried, false otherwise. </returns>
	public bool IsEligibleForRetry(int maxRetries = 3, int retryDelayMinutes = 5)
	{
		if (Status != InboxStatus.Failed)
		{
			return false;
		}

		if (RetryCount >= maxRetries)
		{
			return false;
		}

		if (LastAttemptAt.HasValue)
		{
			var nextRetryTime = LastAttemptAt.Value.AddMinutes(retryDelayMinutes);
			return DateTimeOffset.UtcNow >= nextRetryTime;
		}

		return true;
	}

	/// <inheritdoc />
	public override string ToString() => $"InboxEntry[{MessageId}:{HandlerType}]: {MessageType} - {Status}";
}
