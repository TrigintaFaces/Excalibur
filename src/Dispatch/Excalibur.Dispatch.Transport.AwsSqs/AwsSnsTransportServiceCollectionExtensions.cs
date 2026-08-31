// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SimpleNotificationService;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
///        .EnableEncryption("alias/my-kms-key");
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
	///        .Region("us-east-1");
	/// });
	///
	/// services.AddAwsSnsTransport("payments", sns =>
	/// {
	///     sns.TopicArn("arn:aws:sns:us-west-2:123456789:payments-topic")
	///        .Region("us-west-2");
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

		// The client registration branches on the configured region, so the value is needed eagerly,
		// before the container is built. The consumer's delegate is therefore applied twice, to two
		// instances of the SAME canonical type: once here to decide the registrations, and once inside
		// Configure below against the instance the options system owns, which is what every resolved
		// component reads. Applying one delegate twice is not a copy — there is no field list to fall
		// out of date, so a property the builder sets but the registration forgets to carry cannot
		// exist. Options-configuration delegates are already required to be re-runnable.
		var transportOptions = new AwsSnsOptions();
		configure(new AwsSnsTransportBuilder(transportOptions));

		// Register core AWS SNS services
		RegisterAwsSnsServices(services, name, transportOptions);

		// Register SNS options
		RegisterOptions(services, name, configure);

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
		string name,
		AwsSnsOptions transportOptions)
	{
		// Register AWS SNS client. Every connection setting is mapped onto the SDK's own config: a
		// service URL that did not reach the client would send a host aimed at a local emulator or a
		// VPC endpoint to the real SNS service instead, and static keys that did not reach it would
		// fall back to the ambient credential chain under a configuration that reads as explicit.
		services.TryAddSingleton<IAmazonSimpleNotificationService>(sp =>
			CreateSnsClient(transportOptions.Connection));

		// Register SNS message bus
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IAmazonSimpleNotificationService>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<AwsSnsMessageBus>>();

			return new AwsSnsMessageBus(client, serializer, options, logger);
		});

		// Server-side encryption on SNS is a topic attribute, so a requested KMS key has to be applied
		// to the topic at start-up; without this the key would stay in configuration and never reach AWS.
		// The service is inert unless the consumer asked for encryption.
		// One applier PER NAMED TRANSPORT, each reading its own named options. Registering the type
		// itself would collapse two named transports onto a single applier reading one configuration.
		_ = services.AddSingleton<IHostedService>(sp => new AwsSnsTopicEncryptionService(
			sp.GetRequiredService<IAmazonSimpleNotificationService>(),
			sp.GetRequiredService<IOptionsMonitor<AwsSnsOptions>>(),
			name,
			sp.GetRequiredService<ILogger<AwsSnsTopicEncryptionService>>()));
	}

	/// <summary>
	/// Builds the SNS client from the configured connection options.
	/// </summary>
	/// <param name="connection">The configured connection options.</param>
	/// <returns>The configured SNS client.</returns>
	private static AmazonSimpleNotificationServiceClient CreateSnsClient(AwsSnsConnectionOptions connection)
	{
		var config = new AmazonSimpleNotificationServiceConfig
		{
			MaxErrorRetry = connection.MaxErrorRetry,
			Timeout = connection.Timeout,
			UseHttp = connection.UseHttp,
		};

		if (connection.ServiceUrl is not null)
		{
			// ServiceURL and RegionEndpoint are mutually exclusive in the SDK; an explicit endpoint wins.
			config.ServiceURL = connection.ServiceUrl.ToString();
		}
		else if (!string.IsNullOrEmpty(connection.RegionEndpoint))
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(connection.RegionEndpoint);
		}

		return string.IsNullOrEmpty(connection.AccessKey) || string.IsNullOrEmpty(connection.SecretKey)
			? new AmazonSimpleNotificationServiceClient(config)
			: new AmazonSimpleNotificationServiceClient(
				new Amazon.Runtime.BasicAWSCredentials(connection.AccessKey, connection.SecretKey), config);
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		string name,
		Action<IAwsSnsTransportBuilder> configure)
	{
		// The builder is a VIEW over the instance the options system owns: the consumer's own delegate
		// IS the configure delegate. There is no second options model and no field-by-field carry, so
		// "the builder collected a value the registration forgot to map" is not expressible here.
		// NAMED, so two transports of the same type no longer overwrite one another's configuration.
		_ = services.AddOptions<AwsSnsOptions>(name)
			.Configure(options => configure(new AwsSnsTransportBuilder(options)))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsSnsOptions>, AwsSnsOptionsValidator>());
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
			Excalibur.Dispatch.Transport.TransportLocality.Remote,
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
	/// Configures the AWS SNS options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IAwsSnsTransportBuilder ConfigureOptions(Action<AwsSnsOptions> configure);
}

/// <summary>
/// Implementation of the AWS SNS transport builder.
/// </summary>
internal sealed class AwsSnsTransportBuilder : IAwsSnsTransportBuilder
{
	private readonly AwsSnsOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSnsTransportBuilder"/> class as a view over
	/// <paramref name="options"/>. The builder does not own the instance; each fluent call writes
	/// straight into the caller's options, including the nested connection group, so there is no
	/// second model to translate out of afterwards.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public AwsSnsTransportBuilder(AwsSnsOptions options)
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
		_options.Connection.RegionEndpoint = region;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder EnableEncryption(string kmsMasterKeyId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(kmsMasterKeyId);
		// Both fields are load-bearing: the topic-encryption hosted service returns early unless
		// EnableEncryption is set, and then demands the key.
		_options.EnableEncryption = true;
		_options.KmsMasterKeyId = kmsMasterKeyId;
		return this;
	}

	/// <inheritdoc/>
	public IAwsSnsTransportBuilder ConfigureOptions(Action<AwsSnsOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}
}
