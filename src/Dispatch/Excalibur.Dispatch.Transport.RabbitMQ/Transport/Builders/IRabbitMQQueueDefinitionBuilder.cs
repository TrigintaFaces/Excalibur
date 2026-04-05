// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Defines queue definition methods for a RabbitMQ queue builder.
/// </summary>
public interface IRabbitMQQueueDefinitionBuilder
{
	/// <summary>
	/// Sets the queue name.
	/// </summary>
	/// <param name="name">The queue name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
	IRabbitMQQueueBuilder Name(string name);

	/// <summary>
	/// Sets whether the queue is durable (survives broker restart).
	/// </summary>
	/// <param name="durable"><see langword="true"/> for durable; <see langword="false"/> for transient.</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQQueueBuilder Durable(bool durable = true);

	/// <summary>
	/// Sets whether the queue is exclusive to the current connection.
	/// </summary>
	/// <param name="exclusive"><see langword="true"/> for exclusive; otherwise <see langword="false"/>.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// Exclusive queues are automatically deleted when the connection closes.
	/// </remarks>
	IRabbitMQQueueBuilder Exclusive(bool exclusive = false);

	/// <summary>
	/// Sets whether the queue is auto-deleted when all consumers disconnect.
	/// </summary>
	/// <param name="autoDelete"><see langword="true"/> for auto-delete; otherwise <see langword="false"/>.</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQQueueBuilder AutoDelete(bool autoDelete = false);

	/// <summary>
	/// Sets the prefetch count (number of unacknowledged messages the broker will deliver).
	/// </summary>
	/// <param name="count">The prefetch count.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
	IRabbitMQQueueBuilder PrefetchCount(ushort count);
}
