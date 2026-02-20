// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Azure.Identity;
using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for Azure Service Bus transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAzureServiceBusTransport(IServiceCollection, string, Action{IAzureServiceBusTransportBuilder})"/>
/// to register a named Azure Service Bus transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusTransport("orders", sb =>
/// {
///     sb.ConnectionString("Endpoint=sb://...")
///       .ConfigureSender(sender => sender.EnableBatching(true))
///       .ConfigureProcessor(processor => processor.MaxConcurrentCalls(20))
///       .MapEntity&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public static class AzureServiceBusTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "azure-servicebus";

	/// <summary>
	/// Adds an Azure Service Bus transport with the specified name and configuration.
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
	/// This is the primary entry point for Azure Service Bus transport configuration.
	/// It provides access to all fluent builder APIs for sender, processor, and CloudEvents configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different Service Bus transports.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAzureServiceBusTransport("orders", sb =>
	/// {
	///     sb.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;...")
	///       .ConfigureProcessor(processor => processor.MaxConcurrentCalls(20))
	///       .MapEntity&lt;OrderCreated&gt;("orders-topic");
	/// });
	///
	/// services.AddAzureServiceBusTransport("payments", sb =>
	/// {
	///     sb.ConnectionString("Endpoint=sb://payments.servicebus.windows.net/;...")
	///       .MapEntity&lt;PaymentReceived&gt;("payments-queue");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureServiceBusTransport(
		this IServiceCollection services,
		string name,
		Action<IAzureServiceBusTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new AzureServiceBusTransportOptions { Name = name };
		var builder = new AzureServiceBusTransportBuilder(transportOptions);
		configure(builder);

		// Register core Azure Service Bus services
		RegisterAzureServiceBusServices(services, transportOptions);

		// Register Azure Service Bus options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter
		RegisterTransportAdapter(services, name);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, transportOptions);

		return services;
	}

	/// <summary>
	/// Adds an Azure Service Bus transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "azure-servicebus".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAzureServiceBusTransport(sb =>
	/// {
	///     sb.ConnectionString("Endpoint=sb://...")
	///       .ConfigureProcessor(processor => processor.MaxConcurrentCalls(20));
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAzureServiceBusTransport(
		this IServiceCollection services,
		Action<IAzureServiceBusTransportBuilder> configure)
	{
		return services.AddAzureServiceBusTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core Azure Service Bus services with the service collection.
	/// </summary>
	private static void RegisterAzureServiceBusServices(
		IServiceCollection services,
		AzureServiceBusTransportOptions transportOptions)
	{
		// Register ServiceBusClient
		services.TryAddSingleton(sp =>
		{
			if (!string.IsNullOrEmpty(transportOptions.ConnectionString))
			{
				var clientOptions = new ServiceBusClientOptions
				{
					TransportType = transportOptions.TransportType
				};
				return new ServiceBusClient(transportOptions.ConnectionString, clientOptions);
			}

			if (!string.IsNullOrEmpty(transportOptions.FullyQualifiedNamespace) && transportOptions.UseManagedIdentity)
			{
				var clientOptions = new ServiceBusClientOptions
				{
					TransportType = transportOptions.TransportType
				};
				return new ServiceBusClient(transportOptions.FullyQualifiedNamespace, new DefaultAzureCredential(), clientOptions);
			}

			throw new InvalidOperationException(
				"Azure Service Bus requires either a ConnectionString or FullyQualifiedNamespace with managed identity.");
		});

		// Register AzureServiceBusMessageBus
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<ServiceBusClient>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<AzureServiceBusMessageBus>>();

			return new AzureServiceBusMessageBus(client, serializer, options, logger);
		});
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		AzureServiceBusTransportOptions transportOptions)
	{
		// Map AzureServiceBusTransportOptions to existing AzureServiceBusOptions
		_ = services.AddOptions<AzureServiceBusOptions>()
			.Configure(options =>
			{
				options.ConnectionString = transportOptions.ConnectionString;
				options.Namespace = transportOptions.FullyQualifiedNamespace ?? string.Empty;
				options.QueueName = transportOptions.Sender.DefaultEntityName ?? string.Empty;
				options.TransportType = transportOptions.TransportType;
				options.MaxConcurrentCalls = transportOptions.Processor.MaxConcurrentCalls;
				options.PrefetchCount = transportOptions.Processor.PrefetchCount;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Map to CloudEvent options
		_ = services.AddOptions<AzureServiceBusCloudEventOptions>()
			.Configure(options =>
			{
				options.EnableDuplicateDetection = transportOptions.CloudEvents.EnableDuplicateDetection;
				options.DuplicateDetectionWindow = transportOptions.CloudEvents.DuplicateDetectionWindow;
				options.MaxDeliveryCount = transportOptions.CloudEvents.MaxDeliveryCount;
				options.EnableDeadLetterQueue = transportOptions.CloudEvents.EnableDeadLetterQueue;
				options.TimeToLive = transportOptions.CloudEvents.TimeToLive;
				options.UseSessionsForOrdering = transportOptions.CloudEvents.UseSessionsForOrdering;
				options.UsePartitionKeys = transportOptions.CloudEvents.UsePartitionKeys;
				options.MaxMessageSizeBytes = transportOptions.CloudEvents.MaxMessageSizeBytes;
				options.EnableScheduledDelivery = transportOptions.CloudEvents.EnableScheduledDelivery;
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
		// Create adapter options from transport options
		var adapterOptions = new AzureServiceBusTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AzureServiceBusTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AzureServiceBusMessageBus>();
			return new AzureServiceBusTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AzureServiceBusTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AzureServiceBusMessageBus>();
			return new AzureServiceBusTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AzureServiceBusTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AzureServiceBusTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		AzureServiceBusTransportOptions transportOptions)
	{
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var client = sp.GetRequiredService<ServiceBusClient>();
			var entityName = transportOptions.Sender.DefaultEntityName ?? name;
			var processor = client.CreateProcessor(entityName);
			var logger = sp.GetRequiredService<ILogger<ServiceBusTransportSubscriber>>();
			var nativeSubscriber = new ServiceBusTransportSubscriber(processor, entityName, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}
}
