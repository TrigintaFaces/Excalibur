// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Event Hubs transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for Azure Event Hubs transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAzureEventHubsTransport(IServiceCollection, string, Action{IAzureEventHubsTransportBuilder})"/>
/// to register a named Azure Event Hubs transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureEventHubsTransport("telemetry", eh =>
/// {
///     eh.ConnectionString("Endpoint=sb://...")
///       .EventHubName("telemetry-hub")
///       .ConsumerGroup("$Default")
///       .MaxBatchSize(100);
/// });
/// </code>
/// </example>
public static class AzureEventHubsTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "azure-eventhubs";

	/// <summary>
	/// Adds an Azure Event Hubs transport with the specified name and configuration.
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
	/// This is the primary entry point for Azure Event Hubs transport configuration.
	/// It provides access to all fluent builder APIs for Event Hub configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different Event Hubs.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAzureEventHubsTransport("telemetry", eh =>
	/// {
	///     eh.ConnectionString("Endpoint=sb://...")
	///       .EventHubName("telemetry-hub")
	///       .ConsumerGroup("$Default");
	/// });
	///
	/// services.AddAzureEventHubsTransport("analytics", eh =>
	/// {
	///     eh.FullyQualifiedNamespace("myns.servicebus.windows.net")
	///       .UseManagedIdentity()
	///       .EventHubName("analytics-hub");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureEventHubsTransport(
		this IServiceCollection services,
		string name,
		Action<IAzureEventHubsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new AzureEventHubsTransportOptions { Name = name };
		var builder = new AzureEventHubsTransportBuilder(transportOptions);
		configure(builder);

		// Register core Azure Event Hubs services
		RegisterAzureEventHubsServices(services, transportOptions);

		// Register Azure Event Hubs options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter
		RegisterTransportAdapter(services, name);

		return services;
	}

	/// <summary>
	/// Adds an Azure Event Hubs transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "azure-eventhubs".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAzureEventHubsTransport(eh =>
	/// {
	///     eh.ConnectionString("Endpoint=sb://...")
	///       .EventHubName("my-hub");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureEventHubsTransport(
		this IServiceCollection services,
		Action<IAzureEventHubsTransportBuilder> configure)
	{
		return services.AddAzureEventHubsTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core Azure Event Hubs services with the service collection.
	/// </summary>
	private static void RegisterAzureEventHubsServices(
		IServiceCollection services,
		AzureEventHubsTransportOptions transportOptions)
	{
		// Register EventHubProducerClient
		services.TryAddSingleton(sp =>
		{
			if (!string.IsNullOrEmpty(transportOptions.ConnectionString))
			{
				return new EventHubProducerClient(transportOptions.ConnectionString, transportOptions.EventHubName);
			}

			if (!string.IsNullOrEmpty(transportOptions.FullyQualifiedNamespace) && transportOptions.UseManagedIdentity)
			{
				return new EventHubProducerClient(
					transportOptions.FullyQualifiedNamespace,
					transportOptions.EventHubName,
					new DefaultAzureCredential());
			}

			throw new InvalidOperationException(
				"Azure Event Hubs requires either a ConnectionString or FullyQualifiedNamespace with managed identity, and an EventHubName.");
		});

		// Register AzureEventHubMessageBus
		services.TryAddSingleton(sp =>
		{
			var producer = sp.GetRequiredService<EventHubProducerClient>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptions<AzureEventHubOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<AzureEventHubMessageBus>>();

			return new AzureEventHubMessageBus(producer, serializer, options, logger);
		});
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		AzureEventHubsTransportOptions transportOptions)
	{
		// Map AzureEventHubsTransportOptions to existing AzureEventHubOptions
		_ = services.AddOptions<AzureEventHubOptions>()
			.Configure(options =>
			{
				options.ConnectionString = transportOptions.ConnectionString;
				options.FullyQualifiedNamespace = transportOptions.FullyQualifiedNamespace;
				options.EventHubName = transportOptions.EventHubName ?? string.Empty;
				options.ConsumerGroup = transportOptions.ConsumerGroup ?? "$Default";
				options.PrefetchCount = transportOptions.PrefetchCount;
				options.MaxBatchSize = transportOptions.MaxBatchSize;
				options.EnableEncryption = transportOptions.EnableEncryption;
				options.StartingPosition = transportOptions.StartingPosition;
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
		var adapterOptions = new AzureEventHubsTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AzureEventHubsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AzureEventHubMessageBus>();
			return new AzureEventHubsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AzureEventHubsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AzureEventHubMessageBus>();
			return new AzureEventHubsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AzureEventHubsTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AzureEventHubsTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}
}

/// <summary>
/// Builder interface for fluent Azure Event Hubs transport configuration.
/// </summary>
public interface IAzureEventHubsTransportBuilder
{
	/// <summary>
	/// Sets the Event Hub connection string.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the fully qualified namespace for managed identity authentication.
	/// </summary>
	/// <param name="fullyQualifiedNamespace">The namespace (e.g., myns.servicebus.windows.net).</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace);

	/// <summary>
	/// Enables managed identity authentication.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder UseManagedIdentity();

	/// <summary>
	/// Sets the Event Hub name.
	/// </summary>
	/// <param name="eventHubName">The Event Hub name.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder EventHubName(string eventHubName);

	/// <summary>
	/// Sets the consumer group name.
	/// </summary>
	/// <param name="consumerGroup">The consumer group name.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder ConsumerGroup(string consumerGroup);

	/// <summary>
	/// Sets the prefetch count for receivers.
	/// </summary>
	/// <param name="prefetchCount">The prefetch count.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder PrefetchCount(int prefetchCount);

	/// <summary>
	/// Sets the maximum batch size for batch operations.
	/// </summary>
	/// <param name="maxBatchSize">The maximum batch size.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder MaxBatchSize(int maxBatchSize);

	/// <summary>
	/// Sets the starting position for event processing.
	/// </summary>
	/// <param name="startingPosition">The starting position.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder StartingPosition(EventHubStartingPosition startingPosition);

	/// <summary>
	/// Configures the Azure Event Hubs options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IAzureEventHubsTransportBuilder ConfigureOptions(Action<AzureEventHubsTransportOptions> configure);
}

/// <summary>
/// Implementation of the Azure Event Hubs transport builder.
/// </summary>
internal sealed class AzureEventHubsTransportBuilder : IAzureEventHubsTransportBuilder
{
	private readonly AzureEventHubsTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureEventHubsTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AzureEventHubsTransportBuilder(AzureEventHubsTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder FullyQualifiedNamespace(string fullyQualifiedNamespace)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace);
		_options.FullyQualifiedNamespace = fullyQualifiedNamespace;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder UseManagedIdentity()
	{
		_options.UseManagedIdentity = true;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder EventHubName(string eventHubName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventHubName);
		_options.EventHubName = eventHubName;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder ConsumerGroup(string consumerGroup)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
		_options.ConsumerGroup = consumerGroup;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder PrefetchCount(int prefetchCount)
	{
		_options.PrefetchCount = prefetchCount;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder MaxBatchSize(int maxBatchSize)
	{
		_options.MaxBatchSize = maxBatchSize;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder StartingPosition(EventHubStartingPosition startingPosition)
	{
		_options.StartingPosition = startingPosition;
		return this;
	}

	/// <inheritdoc/>
	public IAzureEventHubsTransportBuilder ConfigureOptions(Action<AzureEventHubsTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}
}

/// <summary>
/// Configuration options for Azure Event Hubs transport.
/// </summary>
public sealed class AzureEventHubsTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Event Hub connection string.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified namespace for managed identity authentication.
	/// </summary>
	public string? FullyQualifiedNamespace { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity.
	/// </summary>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the Event Hub name.
	/// </summary>
	public string? EventHubName { get; set; }

	/// <summary>
	/// Gets or sets the consumer group name. Default is "$Default".
	/// </summary>
	public string? ConsumerGroup { get; set; } = "$Default";

	/// <summary>
	/// Gets or sets the prefetch count for receivers. Default is 300.
	/// </summary>
	public int PrefetchCount { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum batch size for batch operations. Default is 100.
	/// </summary>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption.
	/// </summary>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the starting position for event processing. Default is Latest.
	/// </summary>
	public EventHubStartingPosition StartingPosition { get; set; } = EventHubStartingPosition.Latest;
}
