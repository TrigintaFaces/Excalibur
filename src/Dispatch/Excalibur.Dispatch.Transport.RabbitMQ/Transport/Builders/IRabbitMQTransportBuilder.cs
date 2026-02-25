// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Fluent builder interface for configuring RabbitMQ transport options.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern with single entry point and fluent chaining
/// for configuring RabbitMQ transport connections, exchanges, queues, bindings, and dead letters.
/// </para>
/// <para>
/// Access this builder through
/// <see cref="Microsoft.Extensions.DependencyInjection.RabbitMQTransportServiceCollectionExtensions.AddRabbitMQTransport(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Action{IRabbitMQTransportBuilder})"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMQTransport("events", rmq =>
/// {
///     rmq.HostName("localhost")
///        .Port(5672)
///        .VirtualHost("/")
///        .Credentials("guest", "guest")
///        .ConfigureExchange(exchange =>
///        {
///            exchange.Name("events")
///                    .Type(RabbitMQExchangeType.Topic)
///                    .Durable(true);
///        })
///        .ConfigureQueue(queue =>
///        {
///            queue.Name("order-handlers")
///                 .Durable(true)
///                 .PrefetchCount(10);
///        })
///        .ConfigureBinding(binding =>
///        {
///            binding.Exchange("events")
///                   .Queue("order-handlers")
///                   .RoutingKey("orders.*");
///        })
///        .MapExchange&lt;OrderCreated&gt;("events");
/// });
/// </code>
/// </example>
public interface IRabbitMQTransportBuilder
{
	/// <summary>
	/// Sets the RabbitMQ host name.
	/// </summary>
	/// <param name="hostName">The host name or IP address.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="hostName"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder HostName(string hostName);

	/// <summary>
	/// Sets the RabbitMQ port.
	/// </summary>
	/// <param name="port">The port number (typically 5672 for AMQP, 5671 for AMQPS).</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is not between 1 and 65535.</exception>
	IRabbitMQTransportBuilder Port(int port);

	/// <summary>
	/// Sets the virtual host.
	/// </summary>
	/// <param name="vhost">The virtual host path.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="vhost"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder VirtualHost(string vhost);

	/// <summary>
	/// Sets the credentials for authentication.
	/// </summary>
	/// <param name="username">The username.</param>
	/// <param name="password">The password.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="username"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder Credentials(string username, string password);

	/// <summary>
	/// Sets the AMQP connection string (alternative to individual connection properties).
	/// </summary>
	/// <param name="connectionString">The AMQP connection string (e.g., "amqp://user:pass@host:port/vhost").</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or whitespace.</exception>
	/// <remarks>
	/// When a connection string is provided, it takes precedence over individual
	/// <see cref="HostName"/>, <see cref="Port"/>, <see cref="VirtualHost"/>, and <see cref="Credentials"/> settings.
	/// </remarks>
	IRabbitMQTransportBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Enables SSL/TLS for the connection.
	/// </summary>
	/// <param name="configure">Optional action to configure SSL options.</param>
	/// <returns>The builder for chaining.</returns>
	IRabbitMQTransportBuilder UseSsl(Action<RabbitMQSslOptions>? configure = null);

	/// <summary>
	/// Configures an exchange for this transport.
	/// </summary>
	/// <param name="configure">The exchange configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
	/// <remarks>
	/// Multiple exchanges can be configured by calling this method multiple times.
	/// </remarks>
	IRabbitMQTransportBuilder ConfigureExchange(Action<IRabbitMQExchangeBuilder> configure);

	/// <summary>
	/// Configures a queue for this transport.
	/// </summary>
	/// <param name="configure">The queue configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
	/// <remarks>
	/// Multiple queues can be configured by calling this method multiple times.
	/// </remarks>
	IRabbitMQTransportBuilder ConfigureQueue(Action<IRabbitMQQueueBuilder> configure);

	/// <summary>
	/// Configures a binding between an exchange and a queue.
	/// </summary>
	/// <param name="configure">The binding configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
	/// <remarks>
	/// Multiple bindings can be configured by calling this method multiple times.
	/// </remarks>
	IRabbitMQTransportBuilder ConfigureBinding(Action<IRabbitMQBindingBuilder> configure);

	/// <summary>
	/// Configures dead letter exchange handling.
	/// </summary>
	/// <param name="configure">The dead letter configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
	IRabbitMQTransportBuilder ConfigureDeadLetter(Action<IRabbitMQDeadLetterBuilder> configure);

	/// <summary>
	/// Configures CloudEvents options for the transport.
	/// </summary>
	/// <param name="configure">The CloudEvents configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
	IRabbitMQTransportBuilder ConfigureCloudEvents(Action<RabbitMqCloudEventOptions> configure);

	/// <summary>
	/// Maps a message type to a specific exchange for routing.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="exchange">The target exchange name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="exchange"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder MapExchange<TMessage>(string exchange) where TMessage : class;

	/// <summary>
	/// Maps a message type to a specific queue for routing.
	/// </summary>
	/// <typeparam name="TMessage">The message type to map.</typeparam>
	/// <param name="queue">The target queue name.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="queue"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder MapQueue<TMessage>(string queue) where TMessage : class;

	/// <summary>
	/// Sets a prefix to be applied to all exchange names.
	/// </summary>
	/// <param name="prefix">The exchange name prefix (e.g., "myapp-prod-").</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder WithExchangePrefix(string prefix);

	/// <summary>
	/// Sets a prefix to be applied to all queue names.
	/// </summary>
	/// <param name="prefix">The queue name prefix (e.g., "myapp-prod-").</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or whitespace.</exception>
	IRabbitMQTransportBuilder WithQueuePrefix(string prefix);
}

/// <summary>
/// Internal implementation of <see cref="IRabbitMQTransportBuilder"/>.
/// </summary>
internal sealed class RabbitMQTransportBuilder : IRabbitMQTransportBuilder
{
	private readonly RabbitMQTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMQTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public RabbitMQTransportBuilder(RabbitMQTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder HostName(string hostName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
		_options.Connection.HostName = hostName;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder Port(int port)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
		_options.Connection.Port = port;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder VirtualHost(string vhost)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(vhost);
		_options.Connection.VirtualHost = vhost;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder Credentials(string username, string password)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(username);
		_options.Connection.Username = username;
		_options.Connection.Password = password ?? string.Empty;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.Connection.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder UseSsl(Action<RabbitMQSslOptions>? configure = null)
	{
		_options.Connection.UseSsl = true;
		configure?.Invoke(_options.Connection.Ssl);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConfigureExchange(Action<IRabbitMQExchangeBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		var exchangeOptions = new RabbitMQExchangeOptions();
		var builder = new RabbitMQExchangeBuilder(exchangeOptions);
		configure(builder);
		_options.Topology.Exchanges.Add(exchangeOptions);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConfigureQueue(Action<IRabbitMQQueueBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		var queueOptions = new RabbitMQQueueOptions();
		var builder = new RabbitMQQueueBuilder(queueOptions);
		configure(builder);
		_options.Topology.Queues.Add(queueOptions);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConfigureBinding(Action<IRabbitMQBindingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		var bindingOptions = new RabbitMQBindingOptions();
		var builder = new RabbitMQBindingBuilder(bindingOptions);
		configure(builder);
		_options.Topology.Bindings.Add(bindingOptions);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConfigureDeadLetter(Action<IRabbitMQDeadLetterBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_options.EnableDeadLetter = true;
		var builder = new RabbitMQDeadLetterBuilder(_options.DeadLetter);
		configure(builder);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder ConfigureCloudEvents(Action<RabbitMqCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options.CloudEvents);
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder MapExchange<TMessage>(string exchange) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
		_options.Topology.ExchangeMappings[typeof(TMessage)] = exchange;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder MapQueue<TMessage>(string queue) where TMessage : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(queue);
		_options.Topology.QueueMappings[typeof(TMessage)] = queue;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder WithExchangePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.Topology.ExchangePrefix = prefix;
		return this;
	}

	/// <inheritdoc/>
	public IRabbitMQTransportBuilder WithQueuePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.Topology.QueuePrefix = prefix;
		return this;
	}
}
