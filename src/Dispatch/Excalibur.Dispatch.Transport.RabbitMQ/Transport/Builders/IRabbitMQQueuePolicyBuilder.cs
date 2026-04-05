// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Defines policy configuration methods for a RabbitMQ queue builder.
/// </summary>
public interface IRabbitMQQueuePolicyBuilder
{
	/// <summary>
	/// Sets whether messages are automatically acknowledged on delivery.
	/// </summary>
	/// <param name="autoAck"><see langword="true"/> for auto-ack; <see langword="false"/> for manual ack.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// Manual acknowledgment (autoAck=false) is recommended for reliable message processing.
	/// </remarks>
	IRabbitMQQueueBuilder AutoAck(bool autoAck = false);

	/// <summary>
	/// Sets the message time-to-live for messages in this queue.
	/// </summary>
	/// <param name="ttl">The TTL duration.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="ttl"/> is negative.</exception>
	IRabbitMQQueueBuilder MessageTtl(TimeSpan ttl);

	/// <summary>
	/// Sets the maximum number of messages allowed in the queue.
	/// </summary>
	/// <param name="maxLength">The maximum message count.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
	IRabbitMQQueueBuilder MaxLength(int maxLength);

	/// <summary>
	/// Sets the maximum total size in bytes for all messages in the queue.
	/// </summary>
	/// <param name="maxBytes">The maximum size in bytes.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBytes"/> is less than 1.</exception>
	IRabbitMQQueueBuilder MaxLengthBytes(long maxBytes);
}
