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
/// The builder is composed of focused sub-interfaces following the Interface Segregation Principle:
/// <list type="bullet">
/// <item><description><see cref="IRabbitMQConnectionBuilder"/> -- connection settings (host, port, credentials).</description></item>
/// <item><description><see cref="IRabbitMQTopologyBuilder"/> -- topology configuration (exchanges, queues, bindings).</description></item>
/// <item><description><see cref="IRabbitMQRoutingBuilder"/> -- message routing and SSL.</description></item>
/// </list>
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
public interface IRabbitMQTransportBuilder : IRabbitMQConnectionBuilder, IRabbitMQTopologyBuilder, IRabbitMQRoutingBuilder
{
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
