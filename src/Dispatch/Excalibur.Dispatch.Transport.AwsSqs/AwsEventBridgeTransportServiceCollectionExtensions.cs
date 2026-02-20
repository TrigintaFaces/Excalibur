// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.EventBridge;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS EventBridge transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for AWS EventBridge transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAwsEventBridgeTransport(IServiceCollection, string, Action{IAwsEventBridgeTransportBuilder})"/>
/// to register a named AWS EventBridge transport with full fluent configuration support.
/// </para>
/// <para>
/// Note: AWS EventBridge is an event routing service. Events are published to an event bus,
/// and rules route events to targets (Lambda functions, SQS queues, Step Functions, etc.).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsEventBridgeTransport("events", eb =>
/// {
///     eb.EventBusName("my-event-bus")
///       .Region("us-east-1")
///       .DefaultSource("com.myapp")
///       .EnableArchiving(retentionDays: 30)
///       .MapDetailType&lt;OrderCreated&gt;("OrderCreated");
/// });
/// </code>
/// </example>
public static class AwsEventBridgeTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "aws-eventbridge";

	/// <summary>
	/// Adds an AWS EventBridge transport with the specified name and configuration.
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
	/// This is the primary entry point for AWS EventBridge transport configuration.
	/// It provides access to all fluent builder APIs for event bus configuration and archiving.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different EventBridge event buses.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAwsEventBridgeTransport("orders", eb =>
	/// {
	///     eb.EventBusName("orders-bus")
	///       .Region("us-east-1")
	///       .DefaultSource("com.myapp.orders")
	///       .MapDetailType&lt;OrderCreated&gt;("OrderCreated");
	/// });
	///
	/// services.AddAwsEventBridgeTransport("analytics", eb =>
	/// {
	///     eb.EventBusName("analytics-bus")
	///       .Region("us-west-2")
	///       .DefaultSource("com.myapp.analytics")
	///       .EnableArchiving(retentionDays: 90);
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsEventBridgeTransport(
		this IServiceCollection services,
		string name,
		Action<IAwsEventBridgeTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new AwsEventBridgeTransportOptions { Name = name };
		var builder = new AwsEventBridgeTransportBuilder(transportOptions);
		configure(builder);

		// Register core AWS EventBridge services
		RegisterAwsEventBridgeServices(services, transportOptions);

		// Register EventBridge options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name);

		return services;
	}

	/// <summary>
	/// Adds an AWS EventBridge transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "aws-eventbridge".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAwsEventBridgeTransport(eb =>
	/// {
	///     eb.EventBusName("my-event-bus")
	///       .Region("us-east-1")
	///       .DefaultSource("com.myapp");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsEventBridgeTransport(
		this IServiceCollection services,
		Action<IAwsEventBridgeTransportBuilder> configure)
	{
		return services.AddAwsEventBridgeTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core AWS EventBridge services with the service collection.
	/// </summary>
	private static void RegisterAwsEventBridgeServices(
		IServiceCollection services,
		AwsEventBridgeTransportOptions transportOptions)
	{
		// Register AWS EventBridge client
		services.TryAddSingleton<IAmazonEventBridge>(sp =>
		{
			if (!string.IsNullOrEmpty(transportOptions.Region))
			{
				var region = Amazon.RegionEndpoint.GetBySystemName(transportOptions.Region);
				return new AmazonEventBridgeClient(region);
			}

			return new AmazonEventBridgeClient();
		});

		// Register EventBridge message bus
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IAmazonEventBridge>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptions<AwsEventBridgeOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<AwsEventBridgeMessageBus>>();

			return new AwsEventBridgeMessageBus(client, serializer, options, logger);
		});
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		AwsEventBridgeTransportOptions transportOptions)
	{
		// Map AwsEventBridgeTransportOptions to existing AwsEventBridgeOptions
		_ = services.AddOptions<AwsEventBridgeOptions>()
			.Configure(options =>
			{
				options.EventBusName = transportOptions.EventBusName ?? "default";
				options.DefaultSource = transportOptions.DefaultSource ?? "Excalibur.Dispatch.Transport";
				options.DefaultDetailType = transportOptions.DefaultDetailType ?? string.Empty;
				options.EnableArchiving = transportOptions.EnableArchiving;
				options.ArchiveName = transportOptions.ArchiveName;
				options.ArchiveRetentionDays = transportOptions.ArchiveRetentionDays;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();
	}

	/// <summary>
	/// Registers the transport adapter with the transport factory.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name)
	{
		// Create adapter options
		var adapterOptions = new AwsEventBridgeTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsEventBridgeTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsEventBridgeMessageBus>();
			return new AwsEventBridgeTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsEventBridgeTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsEventBridgeMessageBus>();
			return new AwsEventBridgeTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AwsEventBridgeTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AwsEventBridgeTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}
}

/// <summary>
/// Builder interface for fluent AWS EventBridge transport configuration.
/// </summary>
public interface IAwsEventBridgeTransportBuilder
{
	/// <summary>
	/// Sets the event bus name.
	/// </summary>
	/// <param name="eventBusName">The event bus name. Use "default" for the default event bus.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder EventBusName(string eventBusName);

	/// <summary>
	/// Sets the AWS region for the EventBridge client.
	/// </summary>
	/// <param name="region">The AWS region identifier (e.g., "us-east-1").</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder Region(string region);

	/// <summary>
	/// Sets the default source for events.
	/// </summary>
	/// <param name="source">The event source (e.g., "com.myapp").</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder DefaultSource(string source);

	/// <summary>
	/// Sets the default detail type for events.
	/// </summary>
	/// <param name="detailType">The detail type.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder DefaultDetailType(string detailType);

	/// <summary>
	/// Enables event archiving with the specified retention period.
	/// </summary>
	/// <param name="retentionDays">The number of days to retain archived events.</param>
	/// <param name="archiveName">Optional archive name.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder EnableArchiving(int retentionDays = 7, string? archiveName = null);

	/// <summary>
	/// Configures the AWS EventBridge options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder ConfigureOptions(Action<AwsEventBridgeTransportOptions> configure);

	/// <summary>
	/// Maps a message type to a specific detail type.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="detailType">The detail type for this message type.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsEventBridgeTransportBuilder MapDetailType<T>(string detailType);
}

/// <summary>
/// Implementation of the AWS EventBridge transport builder.
/// </summary>
internal sealed class AwsEventBridgeTransportBuilder : IAwsEventBridgeTransportBuilder
{
	private readonly AwsEventBridgeTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsEventBridgeTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AwsEventBridgeTransportBuilder(AwsEventBridgeTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder EventBusName(string eventBusName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventBusName);
		_options.EventBusName = eventBusName;
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		_options.Region = region;
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder DefaultSource(string source)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		_options.DefaultSource = source;
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder DefaultDetailType(string detailType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailType);
		_options.DefaultDetailType = detailType;
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder EnableArchiving(int retentionDays = 7, string? archiveName = null)
	{
		_options.EnableArchiving = true;
		_options.ArchiveRetentionDays = retentionDays;
		_options.ArchiveName = archiveName;
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder ConfigureOptions(Action<AwsEventBridgeTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}

	/// <inheritdoc/>
	public IAwsEventBridgeTransportBuilder MapDetailType<T>(string detailType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailType);
		_options.DetailTypeMappings[typeof(T)] = detailType;
		return this;
	}
}

/// <summary>
/// Configuration options for AWS EventBridge transport.
/// </summary>
public sealed class AwsEventBridgeTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for the EventBridge client.
	/// </summary>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the event bus name.
	/// </summary>
	public string? EventBusName { get; set; }

	/// <summary>
	/// Gets or sets the default source for events.
	/// </summary>
	public string? DefaultSource { get; set; }

	/// <summary>
	/// Gets or sets the default detail type for events.
	/// </summary>
	public string? DefaultDetailType { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable event archiving.
	/// </summary>
	public bool EnableArchiving { get; set; }

	/// <summary>
	/// Gets or sets the archive name.
	/// </summary>
	public string? ArchiveName { get; set; }

	/// <summary>
	/// Gets or sets the archive retention days. Default is 7.
	/// </summary>
	public int ArchiveRetentionDays { get; set; } = 7;

	/// <summary>
	/// Gets the message type to detail type mappings.
	/// </summary>
	public Dictionary<Type, string> DetailTypeMappings { get; } = new();
}
