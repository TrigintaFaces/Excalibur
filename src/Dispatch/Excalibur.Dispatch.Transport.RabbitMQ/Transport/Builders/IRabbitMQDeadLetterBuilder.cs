// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Fluent builder interface for configuring RabbitMQ dead letter exchange (DLX) handling.
/// </summary>
/// <remarks>
/// <para>
/// Access this builder through <see cref="IRabbitMQTransportBuilder.ConfigureDeadLetter"/>.
/// </para>
/// <para>
/// Dead letter exchanges receive messages that cannot be delivered or processed:
/// </para>
/// <list type="bullet">
/// <item><description>Messages rejected (basic.reject or basic.nack) with requeue=false</description></item>
/// <item><description>Messages that exceed TTL</description></item>
/// <item><description>Messages that exceed queue length limits</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// rmq.ConfigureDeadLetter(dlx =>
/// {
///     dlx.Exchange("dead-letters")
///        .Queue("dead-letter-queue")
///        .RoutingKey("#")
///        .MaxRetries(3)
///        .RetryDelay(TimeSpan.FromSeconds(30));
/// });
/// </code>
/// </example>
public interface IRabbitMQDeadLetterBuilder
{
	/// <summary>
	/// Sets the dead letter exchange name.
	/// </summary>
	/// <param name="exchange">The DLX name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="exchange"/> is null or whitespace.</exception>
	IRabbitMQDeadLetterBuilder Exchange(string exchange);

	/// <summary>
	/// Sets the dead letter queue name.
	/// </summary>
	/// <param name="queue">The DLQ name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="queue"/> is null or whitespace.</exception>
	IRabbitMQDeadLetterBuilder Queue(string queue);

	/// <summary>
	/// Sets the routing key for dead letter messages.
	/// </summary>
	/// <param name="routingKey">The routing key pattern.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="routingKey"/> is null.</exception>
	IRabbitMQDeadLetterBuilder RoutingKey(string routingKey);

	/// <summary>
	/// Sets the maximum number of retry attempts before a message is dead-lettered.
	/// </summary>
	/// <param name="maxRetries">The maximum retry count.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRetries"/> is negative.</exception>
	IRabbitMQDeadLetterBuilder MaxRetries(int maxRetries);

	/// <summary>
	/// Sets the delay between retry attempts.
	/// </summary>
	/// <param name="delay">The retry delay duration.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="delay"/> is negative.</exception>
	IRabbitMQDeadLetterBuilder RetryDelay(TimeSpan delay);
}

/// <summary>
/// Internal implementation of <see cref="IRabbitMQDeadLetterBuilder"/>.
/// </summary>
internal sealed class RabbitMQDeadLetterBuilder : IRabbitMQDeadLetterBuilder
{
	private readonly RabbitMQDeadLetterOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMQDeadLetterBuilder"/> class.
	/// </summary>
	/// <param name="options">The dead letter options to configure.</param>
	public RabbitMQDeadLetterBuilder(RabbitMQDeadLetterOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IRabbitMQDeadLetterBuilder Exchange(string exchange)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
		_options.Exchange = exchange;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQDeadLetterBuilder Queue(string queue)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queue);
		_options.Queue = queue;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQDeadLetterBuilder RoutingKey(string routingKey)
	{
		ArgumentNullException.ThrowIfNull(routingKey);
		_options.RoutingKey = routingKey;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQDeadLetterBuilder MaxRetries(int maxRetries)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
		_options.MaxRetries = maxRetries;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQDeadLetterBuilder RetryDelay(TimeSpan delay)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
		_options.RetryDelay = delay;
		return this;
	}
}
