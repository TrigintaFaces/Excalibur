// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kafka transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for Kafka transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddKafkaTransport(IServiceCollection, string, Action{IKafkaTransportBuilder})"/>
/// to register a named Kafka transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKafkaTransport("events", kafka =>
/// {
///     kafka.BootstrapServers("localhost:9092")
///          .UseSchemaRegistry(registry => registry.Url = "http://localhost:8081")
///          .ConfigureProducer(producer => producer.Acks(KafkaAckLevel.All))
///          .ConfigureConsumer(consumer => consumer.GroupId("my-group"))
///          .MapTopic&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public static class KafkaTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "kafka";

	/// <summary>
	/// Adds a Kafka transport with the specified name and configuration.
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
	/// This is the primary entry point for Kafka transport configuration.
	/// It provides access to all fluent builder APIs for producer, consumer, and schema registry configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different Kafka clusters or configurations.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddKafkaTransport("events", kafka =>
	/// {
	///     kafka.BootstrapServers("broker1:9092,broker2:9092")
	///          .ConfigureProducer(producer => producer.Acks(KafkaAckLevel.All))
	///          .MapTopic&lt;OrderCreated&gt;("orders-topic");
	/// });
	///
	/// services.AddKafkaTransport("analytics", kafka =>
	/// {
	///     kafka.BootstrapServers("analytics-cluster:9092")
	///          .MapTopic&lt;MetricEvent&gt;("metrics-topic");
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
	[RequiresDynamicCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
	public static IServiceCollection AddKafkaTransport(
		this IServiceCollection services,
		string name,
		Action<IKafkaTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new KafkaTransportOptions { Name = name };
		var builder = new KafkaTransportBuilder(transportOptions);
		configure(builder);

		// Register core Kafka services
		RegisterKafkaServices(services, transportOptions);

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, transportOptions);

		return services;
	}

	/// <summary>
	/// Adds a Kafka transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "kafka".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddKafkaTransport(kafka =>
	/// {
	///     kafka.BootstrapServers("localhost:9092")
	///          .ConfigureConsumer(consumer => consumer.GroupId("my-group"));
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
	[RequiresDynamicCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
	public static IServiceCollection AddKafkaTransport(
		this IServiceCollection services,
		Action<IKafkaTransportBuilder> configure)
	{
		return services.AddKafkaTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core Kafka services with the service collection.
	/// </summary>
	private static void RegisterKafkaServices(
		IServiceCollection services,
		KafkaTransportOptions transportOptions)
	{
		// Configure KafkaOptions from the new transport options
		_ = services.AddOptions<KafkaOptions>()
			.Configure(kafkaOptions =>
			{
				kafkaOptions.BootstrapServers = transportOptions.BootstrapServers;

				if (transportOptions.ConsumerOptions is not null)
				{
					kafkaOptions.ConsumerGroup = transportOptions.ConsumerOptions.GroupId;
					kafkaOptions.EnableAutoCommit = transportOptions.ConsumerOptions.EnableAutoCommit;
					kafkaOptions.AutoCommitIntervalMs = (int)transportOptions.ConsumerOptions.AutoCommitInterval.TotalMilliseconds;
					kafkaOptions.SessionTimeoutMs = (int)transportOptions.ConsumerOptions.SessionTimeout.TotalMilliseconds;
					kafkaOptions.MaxPollIntervalMs = (int)transportOptions.ConsumerOptions.MaxPollInterval.TotalMilliseconds;
					kafkaOptions.MaxBatchSize = transportOptions.ConsumerOptions.MaxBatchSize;
					kafkaOptions.AutoOffsetReset = transportOptions.ConsumerOptions.AutoOffsetReset switch
					{
						KafkaOffsetReset.Earliest => "earliest",
						KafkaOffsetReset.Latest => "latest",
						_ => "latest",
					};

					foreach (var config in transportOptions.ConsumerOptions.AdditionalConfig)
					{
						kafkaOptions.AdditionalConfig[config.Key] = config.Value;
					}
				}

				if (transportOptions.ProducerOptions is not null)
				{
					kafkaOptions.AdditionalConfig["client.id"] = transportOptions.ProducerOptions.ClientId;

					foreach (var config in transportOptions.ProducerOptions.AdditionalConfig)
					{
						kafkaOptions.AdditionalConfig[config.Key] = config.Value;
					}
				}
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Configure CloudEventOptions from transport options
		_ = services.AddOptions<KafkaCloudEventOptions>()
			.Configure(cloudEventOptions =>
			{
				if (transportOptions.ProducerOptions is not null)
				{
					cloudEventOptions.CompressionType = transportOptions.ProducerOptions.CompressionType;
					cloudEventOptions.AcknowledgmentLevel = transportOptions.ProducerOptions.Acks;
					cloudEventOptions.EnableIdempotentProducer = transportOptions.ProducerOptions.EnableIdempotence;
					cloudEventOptions.EnableTransactions = transportOptions.ProducerOptions.EnableTransactions;
					cloudEventOptions.TransactionalId = transportOptions.ProducerOptions.TransactionalId;
				}

				if (transportOptions.ConsumerOptions is not null)
				{
					cloudEventOptions.ConsumerGroupId = transportOptions.ConsumerOptions.GroupId;
					cloudEventOptions.OffsetReset = transportOptions.ConsumerOptions.AutoOffsetReset;
				}
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<KafkaCloudEventOptions>>().Value);

		// Register the Kafka producer
		services.TryAddSingleton(sp =>
		{
			var kafkaOptions = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
			var cloudEventOptions = sp.GetService<IOptions<KafkaCloudEventOptions>>()?.Value;
			var config = KafkaProducerConfigBuilder.Build(
				kafkaOptions,
				cloudEventOptions,
				messageBusOptions: null);

			return new ProducerBuilder<string, byte[]>(config).Build();
		});

		// Register the Kafka message bus
		services.TryAddSingleton<KafkaMessageBus>();

		// Register schema registry services if enabled
		if (transportOptions.UseSchemaRegistryEnabled && transportOptions.SchemaRegistry is not null)
		{
			RegisterSchemaRegistryServices(services, transportOptions.Name ?? DefaultTransportName, transportOptions.SchemaRegistry);
		}
	}

	/// <summary>
	/// Registers Confluent Schema Registry services with the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="transportName">The transport name for keyed services.</param>
	/// <param name="options">The Schema Registry configuration options.</param>
	private static void RegisterSchemaRegistryServices(
		IServiceCollection services,
		string transportName,
		ConfluentSchemaRegistryOptions options)
	{
		services.TryAddSingleton(options);

		// Register memory cache for caching decorator
		services.TryAddSingleton<IMemoryCache, MemoryCache>();

		// Register the Confluent Schema Registry client
		services.TryAddSingleton<ConfluentSchemaRegistryClient>();

		// Register caching options
		var cachingOptions = new CachingSchemaRegistryOptions { MaxCacheSize = options.MaxCachedSchemas, CacheCompatibilityResults = true };
		services.TryAddSingleton(cachingOptions);

		// Register ISchemaRegistryClient with or without caching decorator based on options
		if (options.CacheSchemas)
		{
			// Register with caching decorator
			services.TryAddSingleton<ISchemaRegistryClient>(sp =>
			{
				var inner = sp.GetRequiredService<ConfluentSchemaRegistryClient>();
				var cache = sp.GetRequiredService<IMemoryCache>();
				var cacheOptions = sp.GetRequiredService<CachingSchemaRegistryOptions>();
				var logger = sp.GetRequiredService<ILogger<CachingSchemaRegistryClient>>();
				return new CachingSchemaRegistryClient(inner, cache, cacheOptions, logger);
			});
		}
		else
		{
			// Register without caching decorator
			services.TryAddSingleton<ISchemaRegistryClient>(static sp => sp.GetRequiredService<ConfluentSchemaRegistryClient>());
		}

		// Register subject name strategy based on configuration
		services.TryAddSingleton(sp =>
		{
			var schemaOptions = sp.GetRequiredService<ConfluentSchemaRegistryOptions>();
			return schemaOptions.CreateSubjectNameStrategy();
		});

		// Register schema type resolver
		services.TryAddSingleton<ISchemaTypeResolver, DefaultSchemaTypeResolver>();

		// Register Confluent serializer as keyed service for this transport
		_ = services.AddKeyedSingleton<IConfluentFormatSerializer>(
			transportName,
			(sp, _) => new ConfluentJsonSerializer(
				sp.GetRequiredService<ISchemaRegistryClient>(),
				sp.GetRequiredService<ILogger<ConfluentJsonSerializer>>()));

		// Register Confluent deserializer as keyed service for this transport
		_ = services.AddKeyedSingleton<IConfluentFormatDeserializer>(
			transportName,
			(sp, _) => new ConfluentJsonDeserializer(
				sp.GetRequiredService<ISchemaTypeResolver>(),
				sp.GetRequiredService<ISubjectNameStrategy>(),
				sp.GetRequiredService<ILogger<ConfluentJsonDeserializer>>()));
	}

	/// <summary>
	/// Registers the transport adapter with the transport factory.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name)
	{
		// Create adapter options from transport options
		var adapterOptions = new KafkaTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<KafkaTransportAdapter>>();
			var messageBus = sp.GetRequiredService<KafkaMessageBus>();
			return new KafkaTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<KafkaTransportAdapter>>();
			var messageBus = sp.GetRequiredService<KafkaMessageBus>();
			return new KafkaTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			KafkaTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<KafkaTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		KafkaTransportOptions transportOptions)
	{
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var consumer = sp.GetRequiredService<IConsumer<string, byte[]>>();
			var logger = sp.GetRequiredService<ILogger<KafkaTransportSubscriber>>();
			var source = transportOptions.ConsumerOptions?.GroupId ?? name;
			var nativeSubscriber = new KafkaTransportSubscriber(consumer, source, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}
}
