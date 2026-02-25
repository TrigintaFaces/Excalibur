// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Fluent builder interface for configuring a RabbitMQ exchange.
/// </summary>
/// <remarks>
/// <para>
/// Access this builder through <see cref="IRabbitMQTransportBuilder.ConfigureExchange"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// rmq.ConfigureExchange(exchange =>
/// {
///     exchange.Name("events")
///             .Type(RabbitMQExchangeType.Topic)
///             .Durable(true)
///             .AutoDelete(false);
/// });
/// </code>
/// </example>
public interface IRabbitMQExchangeBuilder
{
	/// <summary>
	/// Sets the exchange name.
	/// </summary>
	/// <param name="name">The exchange name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
	IRabbitMQExchangeBuilder Name(string name);

	/// <summary>
	/// Sets the exchange type.
	/// </summary>
	/// <param name="type">The exchange type (Direct, Topic, Fanout, or Headers).</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQExchangeBuilder Type(RabbitMQExchangeType type);

	/// <summary>
	/// Sets whether the exchange is durable (survives broker restart).
	/// </summary>
	/// <param name="durable"><see langword="true"/> for durable; <see langword="false"/> for transient.</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQExchangeBuilder Durable(bool durable = true);

	/// <summary>
	/// Sets whether the exchange is auto-deleted when no longer used.
	/// </summary>
	/// <param name="autoDelete"><see langword="true"/> for auto-delete; otherwise <see langword="false"/>.</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQExchangeBuilder AutoDelete(bool autoDelete = false);

	/// <summary>
	/// Sets additional arguments for exchange declaration.
	/// </summary>
	/// <param name="arguments">The arguments dictionary.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
	IRabbitMQExchangeBuilder Arguments(IDictionary<string, object> arguments);

	/// <summary>
	/// Adds a single argument for exchange declaration.
	/// </summary>
	/// <param name="key">The argument key.</param>
	/// <param name="value">The argument value.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
	IRabbitMQExchangeBuilder WithArgument(string key, object value);
}

/// <summary>
/// Internal implementation of <see cref="IRabbitMQExchangeBuilder"/>.
/// </summary>
internal sealed class RabbitMQExchangeBuilder : IRabbitMQExchangeBuilder
{
	private readonly RabbitMQExchangeOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMQExchangeBuilder"/> class.
	/// </summary>
	/// <param name="options">The exchange options to configure.</param>
	public RabbitMQExchangeBuilder(RabbitMQExchangeOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder Name(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		_options.Name = name;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder Type(RabbitMQExchangeType type)
	{
		_options.Type = type;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder Durable(bool durable = true)
	{
		_options.Durable = durable;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder AutoDelete(bool autoDelete = false)
	{
		_options.AutoDelete = autoDelete;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder Arguments(IDictionary<string, object> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		foreach (var kvp in arguments)
		{
			_options.Arguments[kvp.Key] = kvp.Value;
		}

		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQExchangeBuilder WithArgument(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_options.Arguments[key] = value;
		return this;
	}
}
