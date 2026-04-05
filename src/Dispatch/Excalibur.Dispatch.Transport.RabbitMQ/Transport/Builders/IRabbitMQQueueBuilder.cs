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
public interface IRabbitMQQueueBuilder : IRabbitMQQueueDefinitionBuilder, IRabbitMQQueuePolicyBuilder, IRabbitMQQueueArgumentsBuilder
{
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
