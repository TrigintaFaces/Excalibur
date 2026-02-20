// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Fluent builder interface for configuring a RabbitMQ queue.
/// </summary>
/// <remarks>
/// <para>
/// Access this builder through <see cref="IRabbitMQTransportBuilder.ConfigureQueue"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// rmq.ConfigureQueue(queue =>
/// {
///     queue.Name("order-handlers")
///          .Durable(true)
///          .Exclusive(false)
///          .AutoDelete(false)
///          .PrefetchCount(10)
///          .AutoAck(false);
/// });
/// </code>
/// </example>
public interface IRabbitMQQueueBuilder
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

	/// <summary>
	/// Sets additional arguments for queue declaration.
	/// </summary>
	/// <param name="arguments">The arguments dictionary.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
	IRabbitMQQueueBuilder Arguments(IDictionary<string, object> arguments);

	/// <summary>
	/// Adds a single argument for queue declaration.
	/// </summary>
	/// <param name="key">The argument key.</param>
	/// <param name="value">The argument value.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
	IRabbitMQQueueBuilder WithArgument(string key, object value);
}

/// <summary>
/// Internal implementation of <see cref="IRabbitMQQueueBuilder"/>.
/// </summary>
internal sealed class RabbitMQQueueBuilder : IRabbitMQQueueBuilder
{
	private readonly RabbitMQQueueOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMQQueueBuilder"/> class.
	/// </summary>
	/// <param name="options">The queue options to configure.</param>
	public RabbitMQQueueBuilder(RabbitMQQueueOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder Name(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		_options.Name = name;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder Durable(bool durable = true)
	{
		_options.Durable = durable;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder Exclusive(bool exclusive = false)
	{
		_options.Exclusive = exclusive;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder AutoDelete(bool autoDelete = false)
	{
		_options.AutoDelete = autoDelete;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder PrefetchCount(ushort count)
	{
		_options.PrefetchCount = count;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder AutoAck(bool autoAck = false)
	{
		_options.AutoAck = autoAck;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder MessageTtl(TimeSpan ttl)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(ttl, TimeSpan.Zero);
		_options.MessageTtl = ttl;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder MaxLength(int maxLength)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
		_options.MaxLength = maxLength;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder MaxLengthBytes(long maxBytes)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
		_options.MaxLengthBytes = maxBytes;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder Arguments(IDictionary<string, object> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		foreach (var kvp in arguments)
		{
			_options.Arguments[kvp.Key] = kvp.Value;
		}

		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQQueueBuilder WithArgument(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.Arguments[key] = value;
		return this;
	}
}
