// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

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
	/// <param name="handlerType">The deduplication scope this entry is keyed under, together with <paramref name="messageId"/>.</param>
	/// <param name="messageType">A type name for the message that the message type registry can resolve.</param>
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
	/// Gets or sets the scope this message is deduplicated under, forming the composite key with
	/// <see cref="MessageId"/>.
	/// </summary>
	/// <value>
	/// An opaque scope name. A caller that deduplicates per handler passes that handler's fully qualified type
	/// name, so one message can be processed independently by several handlers. The framework's own inbox
	/// writers deduplicate per <b>message type</b> and pass the message type's fully qualified name, so an entry
	/// written by the framework carries one scope per message type rather than one per handler.
	/// </value>
	public string HandlerType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name under which the message's .NET type is registered.
	/// </summary>
	/// <value>
	/// A type name the message type registry can resolve.
	/// The framework's own inbox writers store the simple name (<c>Type.Name</c>).
	/// An ambiguous simple name — one shared by two registered types — resolves to
	/// <b>nothing</b> rather than to either of them, so a collision fails loudly at resolution rather
	/// than deserializing the payload as the wrong type.
	/// </value>
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
	/// Gets or sets the absolute time before which a failed entry must not be re-claimed for retry.
	/// </summary>
	/// <value>
	/// UTC timestamp computed as <c>now + backoff(attempt)</c> when the entry was marked failed with a
	/// backoff schedule, or <see langword="null"/> when no backoff is recorded (the entry is eligible as
	/// soon as the store's re-admission predicate otherwise allows). Honored by stores implementing
	/// <see cref="IBackoffSchedulableInboxStore"/>.
	/// </value>
	public DateTimeOffset? NextAttemptAt { get; set; }

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
	/// <remarks>
	/// No-op once the entry is <see cref="InboxStatus.Processed"/>. See <see cref="MarkFailed"/> for why
	/// that state absorbs every later transition.
	/// </remarks>
	public void MarkProcessing()
	{
		if (Status == InboxStatus.Processed)
		{
			return;
		}

		Status = InboxStatus.Processing;
		LastAttemptAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Marks the entry as successfully processed.
	/// </summary>
	/// <remarks>
	/// Idempotent: re-marking an already-processed entry leaves <see cref="ProcessedAt"/> at the instant
	/// the message was actually handled rather than restamping it.
	/// </remarks>
	public void MarkProcessed()
	{
		if (Status == InboxStatus.Processed)
		{
			return;
		}

		Status = InboxStatus.Processed;
		ProcessedAt = DateTimeOffset.UtcNow;
		LastError = null;
	}

	/// <summary>
	/// Marks the entry as failed with the specified error.
	/// </summary>
	/// <param name="error"> The error description. </param>
	/// <remarks>
	/// <para>
	/// <b><see cref="InboxStatus.Processed"/> is absorbing: this is a no-op on an entry that has already
	/// been processed.</b> The transition is refused rather than applied because it is not recoverable in
	/// the layer above. A handler that outran its own lease can finish after a second processor has
	/// reclaimed the entry and finalized it; the late caller's finalize then reports "already processed"
	/// and its own error handling calls this method. Demoting the entry to
	/// <see cref="InboxStatus.Failed"/> would make it re-admittable, so the next redelivery would run the
	/// handler a further time and <c>IsProcessedAsync</c> would answer <see langword="false"/> about a
	/// message that was in fact processed.
	/// </para>
	/// <para>
	/// Consumers that need to distinguish "recorded the failure" from "the entry was already terminal"
	/// should read <see cref="Status"/> after the call.
	/// </para>
	/// </remarks>
	public void MarkFailed(string error)
	{
		ArgumentException.ThrowIfNullOrEmpty(error);

		if (Status == InboxStatus.Processed)
		{
			return;
		}

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
