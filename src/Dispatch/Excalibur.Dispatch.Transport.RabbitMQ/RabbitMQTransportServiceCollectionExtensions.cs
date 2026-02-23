// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for RabbitMQ transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddRabbitMQTransport(IServiceCollection, string, Action{IRabbitMQTransportBuilder})"/>
/// to register a named RabbitMQ transport with full fluent configuration support.
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
public static class RabbitMQTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "rabbitmq";

	/// <summary>
	/// Adds a RabbitMQ transport with the specified name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for RabbitMQ transport configuration.
	/// It provides access to all fluent builder APIs for exchange, queue, binding, and dead letter configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different RabbitMQ transports.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddRabbitMQTransport("orders", rmq =>
	/// {
	///     rmq.HostName("orders.rabbitmq.local")
	///        .ConfigureExchange(e => e.Name("orders").Type(RabbitMQExchangeType.Topic))
	///        .MapExchange&lt;OrderCreated&gt;("orders");
	/// });
	///
	/// services.AddRabbitMQTransport("payments", rmq =>
	/// {
	///     rmq.HostName("payments.rabbitmq.local")
	///        .ConfigureQueue(q => q.Name("payments"))
	///        .MapQueue&lt;PaymentReceived&gt;("payments");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddRabbitMQTransport(
		this IServiceCollection services,
		string name,
		Action<IRabbitMQTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new RabbitMQTransportOptions { Name = name };
		var builder = new RabbitMQTransportBuilder(transportOptions);
		configure(builder);

		// Register core RabbitMQ services
		RegisterRabbitMQServices(services, transportOptions);

		// Register RabbitMQ options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter
		RegisterTransportAdapter(services, name);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, transportOptions);

		return services;
	}

	/// <summary>
	/// Adds a RabbitMQ transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "rabbitmq".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddRabbitMQTransport(rmq =>
	/// {
	///     rmq.HostName("localhost")
	///        .Credentials("guest", "guest")
	///        .ConfigureExchange(e => e.Name("events").Type(RabbitMQExchangeType.Topic));
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddRabbitMQTransport(
		this IServiceCollection services,
		Action<IRabbitMQTransportBuilder> configure)
	{
		return services.AddRabbitMQTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core RabbitMQ services with the service collection.
	/// </summary>
	private static void RegisterRabbitMQServices(
		IServiceCollection services,
		RabbitMQTransportOptions transportOptions)
	{
		// Register ConnectionFactory
		services.TryAddSingleton<IConnectionFactory>(sp =>
		{
			var factory = new ConnectionFactory();

			var connection = transportOptions.Connection;

			if (!string.IsNullOrEmpty(connection.ConnectionString))
			{
				factory.Uri = new Uri(connection.ConnectionString);
			}
			else
			{
				factory.HostName = connection.HostName;
				factory.Port = connection.Port;
				factory.VirtualHost = connection.VirtualHost;
				factory.UserName = connection.Username;
				factory.Password = connection.Password;
			}

			factory.AutomaticRecoveryEnabled = true;
			factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);

			if (connection.UseSsl)
			{
				factory.Ssl = new SslOption
				{
					Enabled = true,
					ServerName = connection.Ssl.ServerName ?? string.Empty,
					CertPath = connection.Ssl.CertificatePath ?? string.Empty,
					CertPassphrase = connection.Ssl.CertificatePassphrase ?? string.Empty,
				};
			}

			return factory;
		});

		// Register IConnection
		services.TryAddSingleton(sp =>
			ExecuteSync(() => sp.GetRequiredService<IConnectionFactory>().CreateConnectionAsync()));

		// Register IChannel
		services.TryAddSingleton(sp =>
		{
			var connection = sp.GetRequiredService<IConnection>();
			var cloudEventOptions = sp.GetService<IOptions<RabbitMqCloudEventOptions>>()?.Value;
			var publisherConfirms = cloudEventOptions?.EnablePublisherConfirms == true;
			var createOptions = new CreateChannelOptions(
				publisherConfirms,
				publisherConfirms,
				outstandingPublisherConfirmationsRateLimiter: null,
				consumerDispatchConcurrency: null);

			return ExecuteSync(() => connection.CreateChannelAsync(createOptions));
		});

		// Register TopologyInitializer
		services.TryAddSingleton(sp =>
		{
			var rabbitOptions = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
			var cloudEventOptions = sp.GetService<IOptions<RabbitMqCloudEventOptions>>()?.Value;
			var logger = sp.GetService<ILogger<RabbitMqTopologyInitializer>>();
			return new RabbitMqTopologyInitializer(rabbitOptions, cloudEventOptions, logger);
		});

		// Register RabbitMqMessageBus
		services.TryAddSingleton<RabbitMqMessageBus>();

	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		RabbitMQTransportOptions transportOptions)
	{
		// Map RabbitMQTransportOptions to existing RabbitMqOptions
		_ = services.AddOptions<RabbitMqOptions>()
			.Configure(options =>
			{
				options.ConnectionString = transportOptions.Connection.ConnectionString ?? string.Empty;

				// Set exchange from first configured exchange if available
				if (transportOptions.Topology.Exchanges.Count > 0)
				{
					options.Exchange = transportOptions.Topology.Exchanges[0].Name;
				}

				// Set queue from first configured queue if available
				if (transportOptions.Topology.Queues.Count > 0)
				{
					var queue = transportOptions.Topology.Queues[0];
					options.QueueName = queue.Name;
					options.QueueDurable = queue.Durable;
					options.QueueExclusive = queue.Exclusive;
					options.QueueAutoDelete = queue.AutoDelete;
					options.PrefetchCount = queue.PrefetchCount;
					options.AutoAck = queue.AutoAck;
				}

				// Set dead letter options
				if (transportOptions.EnableDeadLetter)
				{
					options.EnableDeadLetterExchange = true;
					options.DeadLetterExchange = transportOptions.DeadLetter.Exchange;
					options.DeadLetterRoutingKey = transportOptions.DeadLetter.RoutingKey;
				}
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Map to CloudEvent options
		_ = services.AddOptions<RabbitMqCloudEventOptions>()
			.Configure(options =>
			{
				// Copy CloudEvents settings from transport options
				var cloudEvents = transportOptions.CloudEvents;
				options.ExchangeType = cloudEvents.ExchangeType;
				options.Persistence = cloudEvents.Persistence;
				options.RoutingStrategy = cloudEvents.RoutingStrategy;
				options.EnablePublisherConfirms = cloudEvents.EnablePublisherConfirms;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();
	}

	/// <summary>
	/// Registers the transport adapter with the service collection.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name)
	{
		// Create adapter options
		var adapterOptions = new RabbitMQTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<RabbitMQTransportAdapter>>();
			var messageBus = sp.GetRequiredService<RabbitMqMessageBus>();
			return new RabbitMQTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<RabbitMQTransportAdapter>>();
			var messageBus = sp.GetRequiredService<RabbitMqMessageBus>();
			return new RabbitMQTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			RabbitMQTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<RabbitMQTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	// RabbitMQ.Client exposes async-only connection/channel creation while DI factories are synchronous.
	// Bridge at composition root to avoid Task.Run thread hops; runtime execution remains async in transport operations.
#pragma warning disable RS0030 // Sync-over-async bridge is constrained to DI composition root.
	private static T ExecuteSync<T>(Func<Task<T>> operation) =>
		operation().Result;
#pragma warning restore RS0030

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		RabbitMQTransportOptions transportOptions)
	{
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var channel = sp.GetRequiredService<IChannel>();
			var logger = sp.GetRequiredService<ILogger<RabbitMqTransportSubscriber>>();
			var queueName = transportOptions.Topology.Queues.Count > 0
				? transportOptions.Topology.Queues[0].Name
				: name;
			var nativeSubscriber = new RabbitMqTransportSubscriber(channel, queueName, queueName, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}
}
