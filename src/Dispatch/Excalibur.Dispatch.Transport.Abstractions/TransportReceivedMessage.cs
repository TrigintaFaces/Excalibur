// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents a message received from a transport.
/// This is the slim replacement for <c>MessageEnvelope</c> in the transport layer,
/// following the Microsoft.Extensions.AI minimal-type pattern.
/// </summary>
/// <remarks>
/// Provider-specific data (lock tokens, receipt handles, consumer offsets, delivery tags)
/// flows via <see cref="ProviderData"/> so the core interface stays transport-agnostic.
/// </remarks>
public sealed class TransportReceivedMessage
{
	/// <summary>
	/// Gets or sets the unique identifier of the message.
	/// </summary>
	/// <value>The message identifier.</value>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message body content.
	/// </summary>
	/// <value>The message body as a byte buffer.</value>
	public ReadOnlyMemory<byte> Body { get; set; }

	/// <summary>
	/// Gets or sets the content type of the message body.
	/// </summary>
	/// <value>The MIME content type of the body.</value>
	public string? ContentType { get; set; }

	/// <summary>
	/// Gets or sets the message type identifier for routing and processing.
	/// </summary>
	/// <value>The message type identifier.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the correlation identifier for message tracking across services.
	/// </summary>
	/// <value>The correlation identifier.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the subject/label of the message.
	/// </summary>
	/// <value>The message subject.</value>
	public string? Subject { get; set; }

	/// <summary>
	/// Gets or sets the number of delivery attempts for this message.
	/// </summary>
	/// <value>The delivery attempt count.</value>
	public int DeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets the time when the message was enqueued at the broker.
	/// </summary>
	/// <value>The enqueue timestamp.</value>
	public DateTimeOffset EnqueuedAt { get; set; }

	/// <summary>
	/// Gets or sets the source queue or subscription where this message was received.
	/// </summary>
	/// <value>The source name.</value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the partition key for the message.
	/// </summary>
	/// <value>The partition key.</value>
	public string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the message group ID (session ID in Azure, ordering key in Google).
	/// </summary>
	/// <value>The message group identifier.</value>
	public string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the time when the message lock expires (for peek-lock scenarios).
	/// </summary>
	/// <value>The lock expiration timestamp, or <see langword="null"/> if not locked.</value>
	public DateTimeOffset? LockExpiresAt { get; set; }

	/// <summary>
	/// Gets the message properties/attributes received from the broker.
	/// </summary>
	/// <value>The read-only message properties dictionary.</value>
	public IReadOnlyDictionary<string, object> Properties { get; init; } =
		new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets provider-specific data: lock tokens, receipt handles, consumer offsets, delivery tags.
	/// </summary>
	/// <remarks>
	/// Transport implementations store their native acknowledgment tokens here.
	/// For example, AWS SQS stores <c>ReceiptHandle</c>, Azure Service Bus stores <c>LockToken</c>,
	/// Kafka stores <c>Offset</c> and <c>Partition</c>.
	/// </remarks>
	/// <value>The provider-specific data dictionary.</value>
	public Dictionary<string, object> ProviderData { get; init; } = [];
}
