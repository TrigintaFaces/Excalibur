// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Fluent builder interface for configuring a RabbitMQ binding between an exchange and a queue.
/// </summary>
/// <remarks>
/// <para>
/// Access this builder through <see cref="IRabbitMQTransportBuilder.ConfigureBinding"/>.
/// </para>
/// <para>
/// Bindings determine how messages are routed from exchanges to queues based on routing keys
/// and exchange type semantics.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// rmq.ConfigureBinding(binding =>
/// {
///     binding.Exchange("events")
///            .Queue("order-handlers")
///            .RoutingKey("orders.*");
/// });
/// </code>
/// </example>
public interface IRabbitMQBindingBuilder
{
	/// <summary>
	/// Sets the source exchange name for the binding.
	/// </summary>
	/// <param name="exchange">The exchange name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="exchange"/> is null or whitespace.</exception>
	IRabbitMQBindingBuilder Exchange(string exchange);

	/// <summary>
	/// Sets the destination queue name for the binding.
	/// </summary>
	/// <param name="queue">The queue name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="queue"/> is null or whitespace.</exception>
	IRabbitMQBindingBuilder Queue(string queue);

	/// <summary>
	/// Sets the routing key pattern for the binding.
	/// </summary>
	/// <param name="routingKey">The routing key pattern.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="routingKey"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// Routing key semantics depend on the exchange type:
	/// </para>
	/// <list type="bullet">
	/// <item><description><b>Direct:</b> Exact match required</description></item>
	/// <item><description><b>Topic:</b> Supports wildcards (* matches one word, # matches zero or more)</description></item>
	/// <item><description><b>Fanout:</b> Routing key is ignored</description></item>
	/// <item><description><b>Headers:</b> Routing key is ignored; headers are used instead</description></item>
	/// </list>
	/// </remarks>
	IRabbitMQBindingBuilder RoutingKey(string routingKey);

	/// <summary>
	/// Sets additional arguments for the binding.
	/// </summary>
	/// <param name="arguments">The arguments dictionary.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
	/// <remarks>
	/// For headers exchanges, use arguments to specify header matching criteria
	/// (e.g., "x-match": "all" or "x-match": "any").
	/// </remarks>
	IRabbitMQBindingBuilder Arguments(IDictionary<string, object> arguments);

	/// <summary>
	/// Adds a single argument for the binding.
	/// </summary>
	/// <param name="key">The argument key.</param>
	/// <param name="value">The argument value.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
	IRabbitMQBindingBuilder WithArgument(string key, object value);
}

/// <summary>
/// Internal implementation of <see cref="IRabbitMQBindingBuilder"/>.
/// </summary>
internal sealed class RabbitMQBindingBuilder : IRabbitMQBindingBuilder
{
	private readonly RabbitMQBindingOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMQBindingBuilder"/> class.
	/// </summary>
	/// <param name="options">The binding options to configure.</param>
	public RabbitMQBindingBuilder(RabbitMQBindingOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IRabbitMQBindingBuilder Exchange(string exchange)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
		_options.Exchange = exchange;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQBindingBuilder Queue(string queue)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queue);
		_options.Queue = queue;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQBindingBuilder RoutingKey(string routingKey)
	{
		ArgumentNullException.ThrowIfNull(routingKey);
		_options.RoutingKey = routingKey;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQBindingBuilder Arguments(IDictionary<string, object> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		foreach (var kvp in arguments)
		{
			_options.Arguments[kvp.Key] = kvp.Value;
		}

		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQBindingBuilder WithArgument(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.Arguments[key] = value;
		return this;
	}
}
