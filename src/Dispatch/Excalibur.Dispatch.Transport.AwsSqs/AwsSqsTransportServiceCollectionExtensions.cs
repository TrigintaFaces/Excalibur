// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Amazon.SQS;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS SQS transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for AWS SQS transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAwsSqsTransport(IServiceCollection, string, Action{IAwsSqsTransportBuilder})"/>
/// to register a named AWS SQS transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport("orders", sqs =>
/// {
///     sqs.UseRegion("us-east-1")
///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
///        .ConfigureFifo(fifo => fifo.ContentBasedDeduplication(true))
///        .ConfigureBatch(batch => batch.SendBatchSize(10))
///        .MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123/orders");
/// });
/// </code>
/// </example>
public static class AwsSqsTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "aws-sqs";

	/// <summary>
	/// Adds an AWS SQS transport with the specified name and configuration.
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
	/// This is the primary entry point for AWS SQS transport configuration.
	/// It provides access to all fluent builder APIs for queue, FIFO, batch, and SNS configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different SQS transports.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAwsSqsTransport("orders", sqs =>
	/// {
	///     sqs.UseRegion("us-east-1")
	///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
	///        .MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123/orders");
	/// });
	///
	/// services.AddAwsSqsTransport("payments", sqs =>
	/// {
	///     sqs.UseRegion("us-west-2")
	///        .MapQueue&lt;PaymentReceived&gt;("https://sqs.us-west-2.amazonaws.com/123/payments");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSqsTransport(
		this IServiceCollection services,
		string name,
		Action<IAwsSqsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var adapterOptions = new AwsSqsTransportAdapterOptions { Name = name };
		var builder = new AwsSqsTransportBuilder(adapterOptions);
		configure(builder);

		// Register core AWS SQS services
		RegisterAwsSqsServices(services, adapterOptions);

		// Flow the configured FIFO selectors to the message bus so ConfigureFifo applies on the
		// wire (MessageGroupId + MessageDeduplicationId) rather than being a silently-inert option.
		if (adapterOptions.HasFifoOptions)
		{
			var fifo = adapterOptions.FifoOptions!;
			_ = services.Configure<AwsSqsFifoOptions>(o =>
			{
				o.ContentBasedDeduplication = fifo.ContentBasedDeduplication;
				o.MessageGroupIdSelector = fifo.MessageGroupIdSelector;
				o.DeduplicationIdSelector = fifo.DeduplicationIdSelector;
			});
		}

		// Configure the AwsSqsOptions the message bus requires (its ctor takes IOptions<AwsSqsOptions>).
		// Nothing else registered it, so the advertised AddAwsSqsTransport(...) ->
		// GetRequiredService<AwsSqsMessageBus>() path threw at runtime on the missing dependency. Map it
		// from the configured adapter options, mirroring the AwsSqsFifoOptions Configure flow above.
		_ = services.AddOptions<AwsSqsOptions>().Configure(o =>
		{
			o.QueueUrl = adapterOptions.HasQueueMappings
				? new Uri(adapterOptions.QueueMappings.Values.First())
				: null;
			o.UseFifoQueue = adapterOptions.HasFifoOptions;
			o.ContentBasedDeduplication =
				adapterOptions.HasFifoOptions && adapterOptions.FifoOptions!.ContentBasedDeduplication;

			if (!string.IsNullOrWhiteSpace(adapterOptions.Region))
			{
				o.Region = adapterOptions.Region;
			}
		});

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name, adapterOptions);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, adapterOptions);

		// Register optional, opt-in startup provisioning (redrive policy + SNS subscriptions).
		RegisterProvisioning(services, adapterOptions);

		// Route the rich ITransportSender/ITransportReceiver classes through DI so configured
		// capabilities (FIFO group/dedup, batching) are reachable on the AddAwsSqsTransport path
		// instead of orphaned (kek7vm shared-seam wiring).
		RegisterTransportSenderReceiver(services, name, adapterOptions);

		return services;
	}

	/// <summary>
	/// Adds an AWS SQS transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "aws-sqs".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAwsSqsTransport(sqs =>
	/// {
	///     sqs.UseRegion("us-east-1")
	///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)));
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSqsTransport(
		this IServiceCollection services,
		Action<IAwsSqsTransportBuilder> configure)
	{
		return services.AddAwsSqsTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core AWS SQS services with the service collection.
	/// </summary>
	private static void RegisterAwsSqsServices(IServiceCollection services, AwsSqsTransportAdapterOptions adapterOptions)
	{
		// Register AWS SQS client honoring the configured region, retry count, and request timeout.
		// Previously the client was constructed with `new AmazonSQSClient()`, silently ignoring these
		// options (unlike the SNS registration, which already honors the region).
		services.TryAddSingleton<IAmazonSQS>(_ => CreateSqsClient(adapterOptions));

		// Ensure IOptions<AwsSqsFifoOptions> resolves for the message bus even when no FIFO queue
		// is configured (defaults to empty options, leaving group/dedup ids unset).
		_ = services.AddOptions<AwsSqsFifoOptions>();

		// Register SQS message bus
		services.TryAddSingleton<AwsSqsMessageBus>();

		// Register SQS channel receiver
		services.TryAddSingleton<AwsSqsChannelReceiver>();
	}

	/// <summary>
	/// Registers the transport adapter with the transport factory.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name,
		AwsSqsTransportAdapterOptions adapterOptions)
	{
		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSqsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSqsMessageBus>();
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSqsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSqsMessageBus>();
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AwsSqsTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AwsSqsTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers the rich <see cref="ITransportSender"/> and <see cref="ITransportReceiver"/>
	/// implementations keyed by transport name so they are instantiated and reachable on the
	/// <c>AddAwsSqsTransport</c> path. Without this, the rich SQS sender/receiver classes are
	/// orphaned and configured capabilities are silently inert. <c>TryAdd*</c> lets a
	/// consumer override the registration (Microsoft-first).
	/// </summary>
	/// <remarks>
	/// <para><b>Registered-iff-configured contract scope.</b> The
	/// "each capability registered only when its config is present" guard is deliberately scoped to
	/// transports whose capability construction is <i>eager</i> — i.e. building the capability from
	/// missing config would throw at registration time. Today that is <b>GooglePubSub only</b>
	/// (<c>new TopicName(projectId, null)</c> throws), so it guards sender on <c>TopicId</c> and
	/// receiver/subscriber on <c>SubscriptionId</c>.</para>
	/// <para>AwsSqs, AzureServiceBus, RabbitMq and Kafka register all three capabilities
	/// <b>unconditionally</b>, and this is <b>intentional</b>: each capability is a <i>lazy factory
	/// lambda</i> that constructs no infrastructure at registration time (the queue URL / entity is
	/// resolved only when the keyed service is first resolved and used). An unused capability is
	/// therefore a harmless never-resolved keyed registration, not an eager failure — adding
	/// <c>IsNullOrEmpty</c> guards here would be code with no defect to prevent. Any future
	/// eager-construct transport MUST adopt the GooglePubSub-style guard. A cross-transport
	/// DI lock binds this scoping non-vacuously (a capability-only config, e.g. SQS subscriber-only).</para>
	/// </remarks>
	private static void RegisterTransportSenderReceiver(
		IServiceCollection services,
		string name,
		AwsSqsTransportAdapterOptions adapterOptions)
	{
		var queueUrl = adapterOptions.HasQueueMappings
			? adapterOptions.QueueMappings.Values.First()
			: name;

		services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var sqsClient = sp.GetRequiredService<IAmazonSQS>();
			var logger = sp.GetRequiredService<ILogger<SqsTransportSender>>();
			return new SqsTransportSender(sqsClient, queueUrl, logger);
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var sqsClient = sp.GetRequiredService<IAmazonSQS>();
			var logger = sp.GetRequiredService<ILogger<SqsTransportReceiver>>();
			return new SqsTransportReceiver(sqsClient, queueUrl, logger);
		});
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		AwsSqsTransportAdapterOptions adapterOptions)
	{
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var sqsClient = sp.GetRequiredService<IAmazonSQS>();
			var logger = sp.GetRequiredService<ILogger<SqsTransportSubscriber>>();
			var queueUrl = adapterOptions.HasQueueMappings
				? adapterOptions.QueueMappings.Values.First()
				: name;
			var nativeSubscriber = new SqsTransportSubscriber(
				sqsClient, name, queueUrl, adapterOptions.VisibilityHeartbeat, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}

	/// <summary>
	/// Creates an <see cref="AmazonSQSClient"/> honoring the configured region, retry count, and timeout.
	/// </summary>
	private static AmazonSQSClient CreateSqsClient(AwsSqsTransportAdapterOptions adapterOptions) =>
		new(CreateSqsConfig(adapterOptions));

	/// <summary>
	/// Builds the <see cref="AmazonSQSConfig"/> honoring the configured region, retry count, and timeout.
	/// Kept separate from client construction so the option-to-config mapping is unit-testable without
	/// resolving AWS credentials.
	/// </summary>
	internal static AmazonSQSConfig CreateSqsConfig(AwsSqsTransportAdapterOptions adapterOptions)
	{
		ArgumentNullException.ThrowIfNull(adapterOptions);

		var config = new AmazonSQSConfig
		{
			MaxErrorRetry = adapterOptions.MaxRetryAttempts,
		};

		if (!string.IsNullOrWhiteSpace(adapterOptions.Region))
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(adapterOptions.Region);
		}

		if (adapterOptions.RequestTimeout is { } timeout)
		{
			config.Timeout = timeout;
		}

		return config;
	}

	/// <summary>
	/// Registers the opt-in provisioning hosted service when provisioning is enabled. The SNS client is
	/// resolved optionally so SQS-only deployments do not require it.
	/// </summary>
	private static void RegisterProvisioning(IServiceCollection services, AwsSqsTransportAdapterOptions adapterOptions)
	{
		if (!adapterOptions.Provisioning.Enabled)
		{
			return;
		}

		_ = services.AddSingleton(sp =>
		{
			var sqsClient = sp.GetRequiredService<IAmazonSQS>();
			var snsClient = sp.GetService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>();
			var logger = sp.GetRequiredService<ILogger<AwsSqsProvisioner>>();
			return new AwsSqsProvisioner(sqsClient, snsClient, logger);
		});

		_ = services.AddSingleton<IHostedService>(sp =>
		{
			var provisioner = sp.GetRequiredService<AwsSqsProvisioner>();
			var logger = sp.GetRequiredService<ILogger<AwsSqsProvisioningHostedService>>();
			return new AwsSqsProvisioningHostedService(provisioner, adapterOptions, logger);
		});
	}
}
