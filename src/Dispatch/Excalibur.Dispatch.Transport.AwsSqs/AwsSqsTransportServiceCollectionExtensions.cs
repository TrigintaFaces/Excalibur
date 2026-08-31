// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Amazon.SQS;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Transport.AwsSqs;
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

		// The adapter binds the UNNAMED AwsSqsCloudEventOptions, so this registers unnamed too.
		// A transport that also copies values into these options registers that copy separately;
		// where both apply, the later registration wins.
		if (builder.CloudEventsConfigure is not null)
		{
			_ = services.AddOptions<AwsSqsCloudEventOptions>()
				.Configure(builder.CloudEventsConfigure)
				.ValidateOnStart();
		}

		// Register core AWS SQS services
		RegisterAwsSqsServices(services, name, adapterOptions);

		// Flow the configured FIFO selectors to the message bus so ConfigureFifo applies on the
		// wire (MessageGroupId + MessageDeduplicationId) rather than being a silently-inert option.
		if (adapterOptions.HasFifoOptions)
		{
			var fifo = adapterOptions.FifoOptions!;

			// Named as well as unnamed: two named SQS transports each keep their own FIFO selectors,
			// while the unnamed instance stays available to AwsSqsMessageBus, whose constructor takes
			// IOptions<AwsSqsFifoOptions>.
			_ = services.Configure<AwsSqsFifoOptions>(name, MapFifo);
			_ = services.Configure<AwsSqsFifoOptions>(MapFifo);

			void MapFifo(AwsSqsFifoOptions o)
			{
				o.ContentBasedDeduplication = fifo.ContentBasedDeduplication;
				o.MessageGroupIdSelector = fifo.MessageGroupIdSelector;
				o.DeduplicationIdSelector = fifo.DeduplicationIdSelector;
			}
		}

		// Configure the AwsSqsOptions the message bus requires (its ctor takes IOptions<AwsSqsOptions>).
		// Nothing else registered it, so the advertised AddAwsSqsTransport(...) ->
		// GetRequiredService<AwsSqsMessageBus>() path threw at runtime on the missing dependency. Map it
		// from the configured adapter options, mirroring the AwsSqsFifoOptions Configure flow above.
		// NAMED, so two named SQS transports in one container no longer write the same instance and let
		// the second silently replace the first. The unnamed instance is configured as well and keeps its
		// existing last-registration-wins behaviour, because AwsSqsMessageBus takes
		// IOptions<AwsSqsOptions> in its constructor and would otherwise resolve an empty object.
		_ = services.AddOptions<AwsSqsOptions>(name).Configure(MapSqs).ValidateOnStart();
		_ = services.AddOptions<AwsSqsOptions>().Configure(MapSqs).ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsSqsOptions>>(new AwsSqsOptionsValidator()));

		// One mapping, applied to both registrations, so a property added here cannot reach one and miss
		// the other.
		void MapSqs(AwsSqsOptions o)
		{
			o.QueueUrl = adapterOptions.HasQueueMappings
				? new Uri(adapterOptions.QueueMappings.Values.First())
				: null;
			if (!string.IsNullOrWhiteSpace(adapterOptions.Region))
			{
				o.Region = adapterOptions.Region;
			}
		}

		// Server-side encryption on SQS is a queue attribute, so a requested KMS key has to be applied to
		// the queue at start-up; without this the key would stay in configuration and never reach AWS.
		// The service is inert unless the consumer asked for encryption.
		//
		// Registered PER NAME with a factory rather than by type: TryAddEnumerable de-duplicates by
		// implementation type, so two named SQS transports would otherwise share one applier reading one
		// unnamed configuration, and a per-transport KMS key could not reach its own queue.
		_ = services.AddSingleton<IHostedService>(sp => new AwsSqsQueueEncryptionService(
			sp.GetRequiredKeyedService<IAmazonSQS>(name),
			sp.GetRequiredService<IOptionsMonitor<AwsSqsOptions>>(),
			name,
			sp.GetRequiredService<ILogger<AwsSqsQueueEncryptionService>>()));

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name, adapterOptions);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, adapterOptions);

		// Register optional, opt-in startup provisioning (redrive policy + SNS subscriptions).
		RegisterProvisioning(services, name, adapterOptions);

		// Route the rich ITransportSender/ITransportReceiver classes through DI so configured
		// capabilities (FIFO group/dedup, batching) are reachable on the AddAwsSqsTransport path
		// instead of orphaned (shared-seam wiring).
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
	private static void RegisterAwsSqsServices(IServiceCollection services, string name, AwsSqsTransportAdapterOptions adapterOptions)
	{
		// Register AWS SQS client honoring the configured region, retry count, and request timeout.
		// Previously the client was constructed with `new AmazonSQSClient()`, silently ignoring these
		// options (unlike the SNS registration, which already honors the region).
		//
		// Keyed by transport name: TryAddSingleton-by-type de-duplicates, so a second named SQS
		// transport contributed no registration and both names resolved the first transport's
		// client/bus, silently sending every named transport to the first transport's queue.
		services.TryAddKeyedSingleton<IAmazonSQS>(name, (_, _) => CreateSqsClient(adapterOptions));

		// Ensure IOptions<AwsSqsFifoOptions> resolves for the message bus even when no FIFO queue
		// is configured (defaults to empty options, leaving group/dedup ids unset).
		_ = services.AddOptions<AwsSqsFifoOptions>();

		// Register SQS message bus
		services.TryAddKeyedSingleton<AwsSqsMessageBus>(name, (sp, key) =>
		{
			var client = sp.GetRequiredKeyedService<IAmazonSQS>(key);
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptionsMonitor<AwsSqsOptions>>().Get(name);
			var fifoOptions = sp.GetRequiredService<IOptionsMonitor<AwsSqsFifoOptions>>().Get(name);
			var logger = sp.GetRequiredService<ILogger<AwsSqsMessageBus>>();
			return new AwsSqsMessageBus(
				client, serializer, Microsoft.Extensions.Options.Options.Create(options),
				Microsoft.Extensions.Options.Options.Create(fifoOptions), logger);
		});

		// Unkeyed convenience registrations for the single-transport host and for consumers of a
		// separate entry point (e.g. AddSqsChannel) that resolve IAmazonSQS unkeyed. TryAdd*, so the
		// first-registered named transport wins -- a multi-transport host must resolve the keyed
		// client/bus by name instead.
		services.TryAddSingleton(sp => sp.GetRequiredKeyedService<IAmazonSQS>(name));
		services.TryAddSingleton(sp => sp.GetRequiredKeyedService<AwsSqsMessageBus>(name));
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
			var messageBus = sp.GetRequiredKeyedService<AwsSqsMessageBus>(name);
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSqsTransportAdapter>>();
			var messageBus = sp.GetRequiredKeyedService<AwsSqsMessageBus>(name);
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AwsSqsTransportAdapter.TransportTypeName,
			Excalibur.Dispatch.Transport.TransportLocality.Remote,
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
			var sqsClient = sp.GetRequiredKeyedService<IAmazonSQS>(name);
			var logger = sp.GetRequiredService<ILogger<SqsTransportSender>>();
			return new SqsTransportSender(sqsClient, queueUrl, logger);
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var sqsClient = sp.GetRequiredKeyedService<IAmazonSQS>(name);
			var logger = sp.GetRequiredService<ILogger<SqsTransportReceiver>>();
			// The queue options are the surface a consumer configures through ConfigureQueue(...); the
			// receive call is where they take effect. Passing the defaults instead would leave a
			// configured long-poll window and visibility timeout with no observable behaviour.
			var queueOptions = adapterOptions.QueueOptions;
			return new SqsTransportReceiver(
				sqsClient,
				queueUrl,
				logger,
				waitTimeSeconds: queueOptions?.ReceiveWaitTimeSeconds ?? 20,
				visibilityTimeoutSeconds: queueOptions is null
					? 30
					: (int)queueOptions.VisibilityTimeout.TotalSeconds,
				maxPayloadBytes: adapterOptions.MaxPayloadBytes);
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
			var sqsClient = sp.GetRequiredKeyedService<IAmazonSQS>(name);
			var logger = sp.GetRequiredService<ILogger<SqsTransportSubscriber>>();
			var queueUrl = adapterOptions.HasQueueMappings
				? adapterOptions.QueueMappings.Values.First()
				: name;
			var queueOptions = adapterOptions.QueueOptions;
			var nativeSubscriber = new SqsTransportSubscriber(
				sqsClient, name, queueUrl, adapterOptions.VisibilityHeartbeat, logger,
				maxPayloadBytes: adapterOptions.MaxPayloadBytes,
				waitTimeSeconds: queueOptions?.ReceiveWaitTimeSeconds ?? 20,
				visibilityTimeoutSeconds: queueOptions is null
					? null
					: (int)queueOptions.VisibilityTimeout.TotalSeconds);

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
	private static void RegisterProvisioning(IServiceCollection services, string name, AwsSqsTransportAdapterOptions adapterOptions)
	{
		if (!adapterOptions.Provisioning.Enabled)
		{
			return;
		}

		_ = services.AddSingleton(sp =>
		{
			var sqsClient = sp.GetRequiredKeyedService<IAmazonSQS>(name);
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
