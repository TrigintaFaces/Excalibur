// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SimpleNotificationService;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS SNS transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for AWS SNS transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAwsSnsTransport(IServiceCollection, string, Action{IAwsSnsTransportBuilder})"/>
/// to register a named AWS SNS transport with full fluent configuration support.
/// </para>
/// <para>
/// Note: AWS SNS is a pub/sub service for publishing messages to topics.
/// Subscribers (SQS queues, Lambda functions, HTTP endpoints, etc.) receive the messages.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSnsTransport("notifications", sns =>
/// {
///     sns.TopicArn("arn:aws:sns:us-east-1:123456789:my-topic")
///        .Region("us-east-1")
///        .EnableEncryption("alias/my-kms-key")
///        .MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123456789:orders-topic");
/// });
/// </code>
/// </example>
public static class AwsSnsTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "aws-sns";

	/// <summary>
	/// Adds an AWS SNS transport with the specified name and configuration.
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
	/// This is the primary entry point for AWS SNS transport configuration.
	/// It provides access to all fluent builder APIs for topic configuration and encryption.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different SNS topics.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAwsSnsTransport("orders", sns =>
	/// {
	///     sns.TopicArn("arn:aws:sns:us-east-1:123456789:orders-topic")
	///        .Region("us-east-1")
	///        .MapTopic&lt;OrderCreated&gt;("arn:aws:sns:us-east-1:123456789:orders-topic");
	/// });
	///
	/// services.AddAwsSnsTransport("payments", sns =>
	/// {
	///     sns.TopicArn("arn:aws:sns:us-west-2:123456789:payments-topic")
	///        .Region("us-west-2")
	///        .MapTopic&lt;PaymentReceived&gt;("arn:aws:sns:us-west-2:123456789:payments-topic");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSnsTransport(
		this IServiceCollection services,
		string name,
		Action<IAwsSnsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new AwsSnsTransportOptions { Name = name };
		var builder = new AwsSnsTransportBuilder(transportOptions);
		configure(builder);

		// Register core AWS SNS services
		RegisterAwsSnsServices(services, transportOptions);

		// Register SNS options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name);

		return services;
	}

	/// <summary>
	/// Adds an AWS SNS transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "aws-sns".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAwsSnsTransport(sns =>
	/// {
	///     sns.TopicArn("arn:aws:sns:us-east-1:123456789:my-topic")
	///        .Region("us-east-1");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSnsTransport(
		this IServiceCollection services,
		Action<IAwsSnsTransportBuilder> configure)
	{
		return services.AddAwsSnsTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core AWS SNS services with the service collection.
	/// </summary>
	private static void RegisterAwsSnsServices(
		IServiceCollection services,
		AwsSnsTransportOptions transportOptions)
	{
		// Register AWS SNS client
		services.TryAddSingleton<IAmazonSimpleNotificationService>(sp =>
		{
			if (!string.IsNullOrEmpty(transportOptions.Region))
			{
				var region = Amazon.RegionEndpoint.GetBySystemName(transportOptions.Region);
				return new AmazonSimpleNotificationServiceClient(region);
			}

			return new AmazonSimpleNotificationServiceClient();
		});

		// Register SNS message bus
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IAmazonSimpleNotificationService>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptions<AwsSnsOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<AwsSnsMessageBus>>();

			return new AwsSnsMessageBus(client, serializer, options, logger);
		});
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		AwsSnsTransportOptions transportOptions)
	{
		// Map AwsSnsTransportOptions to existing AwsSnsOptions
		_ = services.AddOptions<AwsSnsOptions>()
			.Configure(options =>
			{
				options.TopicArn = transportOptions.TopicArn ?? string.Empty;
				options.EnableEncryption = transportOptions.EnableEncryption;
				options.KmsMasterKeyId = transportOptions.KmsMasterKeyId;
				options.ContentBasedDeduplication = transportOptions.ContentBasedDeduplication;
				options.RawMessageDelivery = transportOptions.RawMessageDelivery;
				options.RegionEndpoint = transportOptions.Region;
				options.MaxErrorRetry = transportOptions.MaxErrorRetry;
				options.Timeout = transportOptions.Timeout;
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
		var adapterOptions = new AwsSnsTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSnsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSnsMessageBus>();
			return new AwsSnsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSnsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSnsMessageBus>();
			return new AwsSnsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AwsSnsTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AwsSnsTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}
}

/// <summary>
/// Builder interface for fluent AWS SNS transport configuration.
/// </summary>
public interface IAwsSnsTransportBuilder
{
	/// <summary>
	/// Sets the default topic ARN for publishing messages.
	/// </summary>
	/// <param name="topicArn">The SNS topic ARN.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder TopicArn(string topicArn);

	/// <summary>
	/// Sets the AWS region for the SNS client.
	/// </summary>
	/// <param name="region">The AWS region identifier (e.g., "us-east-1").</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder Region(string region);

	/// <summary>
	/// Enables encryption with the specified KMS key.
	/// </summary>
	/// <param name="kmsMasterKeyId">The KMS master key ID or alias.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder EnableEncryption(string kmsMasterKeyId);

	/// <summary>
	/// Enables content-based deduplication for FIFO topics.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder EnableContentBasedDeduplication();

	/// <summary>
	/// Enables raw message delivery for subscriptions.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder EnableRawMessageDelivery();

	/// <summary>
	/// Configures the AWS SNS options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder ConfigureOptions(Action<AwsSnsTransportOptions> configure);

	/// <summary>
	/// Maps a message type to a specific topic ARN.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="topicArn">The topic ARN for this message type.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder MapTopic<T>(string topicArn);
}

/// <summary>
/// Implementation of the AWS SNS transport builder.
/// </summary>
internal sealed class AwsSnsTransportBuilder : IAwsSnsTransportBuilder
{
	private readonly AwsSnsTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSnsTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AwsSnsTransportBuilder(AwsSnsTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder TopicArn(string topicArn)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);
		_options.TopicArn = topicArn;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		_options.Region = region;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder EnableEncryption(string kmsMasterKeyId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(kmsMasterKeyId);
		_options.EnableEncryption = true;
		_options.KmsMasterKeyId = kmsMasterKeyId;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder EnableContentBasedDeduplication()
	{
		_options.ContentBasedDeduplication = true;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder EnableRawMessageDelivery()
	{
		_options.RawMessageDelivery = true;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder ConfigureOptions(Action<AwsSnsTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder MapTopic<T>(string topicArn)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicArn);
		_options.TopicMappings[typeof(T)] = topicArn;
		return this;
	}
}

/// <summary>
/// Configuration options for AWS SNS transport.
/// </summary>
public sealed class AwsSnsTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the AWS region for the SNS client.
	/// </summary>
	public string? Region { get; set; }

	/// <summary>
	/// Gets or sets the default topic ARN for publishing messages.
	/// </summary>
	public string? TopicArn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable encryption.
	/// </summary>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the KMS master key ID for encryption.
	/// </summary>
	public string? KmsMasterKeyId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content-based deduplication.
	/// </summary>
	public bool ContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use raw message delivery.
	/// </summary>
	public bool RawMessageDelivery { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of error retries. Default is 3.
	/// </summary>
	public int MaxErrorRetry { get; set; } = 3;

	/// <summary>
	/// Gets or sets the request timeout. Default is 30 seconds.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the message type to topic ARN mappings.
	/// </summary>
	public Dictionary<Type, string> TopicMappings { get; } = new();
}
