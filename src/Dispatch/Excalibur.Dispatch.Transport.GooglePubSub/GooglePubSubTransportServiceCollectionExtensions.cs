// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Google Pub/Sub transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for Google Pub/Sub transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddGooglePubSubTransport(IServiceCollection, string, Action{IGooglePubSubTransportBuilder})"/>
/// to register a named Google Pub/Sub transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddGooglePubSubTransport("events", pubsub =>
/// {
///     pubsub.ProjectId("my-gcp-project")
///           .TopicId("my-topic")
///           .SubscriptionId("my-subscription")
///           .ConfigureOptions(options => options.MaxPullMessages = 100)
///           .MapTopic&lt;OrderCreated&gt;("orders-topic");
/// });
/// </code>
/// </example>
public static class GooglePubSubTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "google-pubsub";

	/// <summary>
	/// Adds a Google Pub/Sub transport with the specified name and configuration.
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
	/// This is the primary entry point for Google Pub/Sub transport configuration.
	/// It provides access to all fluent builder APIs for publisher, subscriber, and CloudEvents configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different Pub/Sub topics or projects.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddGooglePubSubTransport("orders", pubsub =>
	/// {
	///     pubsub.ProjectId("orders-project")
	///           .TopicId("orders-topic")
	///           .MapTopic&lt;OrderCreated&gt;("orders-topic");
	/// });
	///
	/// services.AddGooglePubSubTransport("analytics", pubsub =>
	/// {
	///     pubsub.ProjectId("analytics-project")
	///           .TopicId("metrics-topic")
	///           .MapTopic&lt;MetricEvent&gt;("metrics-topic");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddGooglePubSubTransport(
		this IServiceCollection services,
		string name,
		Action<IGooglePubSubTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var transportOptions = new GooglePubSubTransportOptions { Name = name };
		var builder = new GooglePubSubTransportBuilder(transportOptions);
		configure(builder);

		// Register core Google Pub/Sub services
		RegisterGooglePubSubServices(services, transportOptions);

		// Register Google Pub/Sub options
		RegisterOptions(services, transportOptions);

		// Register the transport adapter
		RegisterTransportAdapter(services, name);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, transportOptions);

		// Route the rich ITransportSender/ITransportReceiver classes through DI so they are
		// reachable on the AddGooglePubSubTransport path instead of orphaned (kek7vm shared-seam
		// wiring). Ordering / exactly-once / flow-control are layered by the abyfxr child on this seam.
		RegisterTransportSenderReceiver(services, name, transportOptions);

		return services;
	}

	/// <summary>
	/// Adds a Google Pub/Sub transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "google-pubsub".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddGooglePubSubTransport(pubsub =>
	/// {
	///     pubsub.ProjectId("my-project")
	///           .TopicId("my-topic")
	///           .SubscriptionId("my-subscription");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddGooglePubSubTransport(
		this IServiceCollection services,
		Action<IGooglePubSubTransportBuilder> configure)
	{
		return services.AddGooglePubSubTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core Google Pub/Sub services with the service collection.
	/// </summary>
	private static void RegisterGooglePubSubServices(
		IServiceCollection services,
		GooglePubSubTransportOptions transportOptions)
	{
		// Register PublisherClient
		services.TryAddSingleton(sp =>
		{
			var topicName = new TopicName(transportOptions.ProjectId, transportOptions.TopicId);
			return PublisherClient.Create(topicName);
		});

		// Register SubscriberClient if subscription is configured
		if (!string.IsNullOrEmpty(transportOptions.SubscriptionId))
		{
			services.TryAddSingleton(sp =>
			{
				var subscriptionName = new SubscriptionName(
					transportOptions.ProjectId,
					transportOptions.SubscriptionId);

				// abyfxr: the subscriber client uses EmulatorOrProduction so it talks to the SAME endpoint
				// the fail-loud validator checks (PUBSUB_EMULATOR_HOST when set → emulator; absent →
				// production credentials, unchanged from today). A transport that subscribes to production
				// while the validator checks the emulator (or vice-versa) is a false NFR-3 guarantee (SA 17062).
				if (transportOptions.MaxOutstandingMessages > 0 || transportOptions.MaxOutstandingByteCount > 0)
				{
					// abyfxr (FR-A3 c): apply flow-control to the streaming SubscriberClient when configured,
					// bounding outstanding (unacked) messages/bytes. Flow-control is a streaming-SubscriberClient
					// concept and does not apply to the raw-pull receiver path (SA Q2). Zero = SDK default.
					var settings = new SubscriberClient.Settings
					{
						FlowControlSettings = new FlowControlSettings(
							transportOptions.MaxOutstandingMessages > 0 ? transportOptions.MaxOutstandingMessages : null,
							transportOptions.MaxOutstandingByteCount > 0 ? transportOptions.MaxOutstandingByteCount : null),
					};

					return new SubscriberClientBuilder
					{
						SubscriptionName = subscriptionName,
						Settings = settings,
						EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
					}.Build();
				}

				return new SubscriberClientBuilder
				{
					SubscriptionName = subscriptionName,
					EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
				}.Build();
			});

			// abyfxr (FR-A3 a/b): fail-loud startup validation — if ordering/exactly-once is configured,
			// verify (read-only) the deployed subscription actually has it, else throw a clear config error
			// (NFR-3: no silently-inert advertised guarantee). Read-only; never creates the subscription.
			if (transportOptions.EnableMessageOrdering || transportOptions.EnableExactlyOnceDelivery)
			{
				var projectId = transportOptions.ProjectId ?? string.Empty;
				var subscriptionId = transportOptions.SubscriptionId;
				var requireOrdering = transportOptions.EnableMessageOrdering;
				var requireExactlyOnce = transportOptions.EnableExactlyOnceDelivery;

				_ = services.AddSingleton<IHostedService>(sp => new PubSubSubscriptionConfigValidator(
					projectId,
					subscriptionId,
					requireOrdering,
					requireExactlyOnce,
					sp.GetRequiredService<ILogger<PubSubSubscriptionConfigValidator>>()));
			}
		}

		// Register GooglePubSubMessageBus
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<PublisherClient>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;
			var logger = sp.GetRequiredService<ILogger<GooglePubSubMessageBus>>();

			return new GooglePubSubMessageBus(client, serializer, options, logger);
		});

		// Register work distribution strategy (default: round-robin)
		services.TryAddSingleton<IWorkDistributionStrategy, RoundRobinDistributionStrategy>();
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		GooglePubSubTransportOptions transportOptions)
	{
		// Map GooglePubSubTransportOptions to existing GooglePubSubOptions
		_ = services.AddOptions<GooglePubSubOptions>()
			.Configure(options =>
			{
				options.ProjectId = transportOptions.ProjectId ?? string.Empty;
				options.TopicId = transportOptions.TopicId ?? string.Empty;
				options.SubscriptionId = transportOptions.SubscriptionId ?? string.Empty;
				options.Subscriber.MaxPullMessages = transportOptions.MaxPullMessages;
				options.Subscriber.AckDeadlineSeconds = transportOptions.AckDeadlineSeconds;
				options.Subscriber.EnableAutoAckExtension = transportOptions.EnableAutoAckExtension;
				options.MaxConcurrentMessages = transportOptions.MaxConcurrentMessages;
				options.Subscriber.EnableDeadLetterTopic = transportOptions.EnableDeadLetterTopic;
				options.Subscriber.DeadLetterTopicId = transportOptions.DeadLetterTopicId;
				options.Telemetry.EnableOpenTelemetry = transportOptions.EnableOpenTelemetry;
			})
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GooglePubSubOptions>, GooglePubSubOptionsValidator>());
	}

	/// <summary>
	/// Registers the transport adapter with the service collection.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name)
	{
		// Create adapter options
		var adapterOptions = new GooglePubSubTransportAdapterOptions { Name = name };

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<GooglePubSubTransportAdapter>>();
			var messageBus = sp.GetRequiredService<GooglePubSubMessageBus>();
			return new GooglePubSubTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<GooglePubSubTransportAdapter>>();
			var messageBus = sp.GetRequiredService<GooglePubSubMessageBus>();
			return new GooglePubSubTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			GooglePubSubTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<GooglePubSubTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers the rich <see cref="ITransportSender"/> and <see cref="ITransportReceiver"/>
	/// implementations keyed by transport name so they are instantiated and reachable on the
	/// <c>AddGooglePubSubTransport</c> path instead of orphaned (kek7vm). <c>TryAdd*</c> lets a
	/// consumer override the registration (Microsoft-first). The receiver is only registered when a
	/// subscription is configured, mirroring the subscriber registration.
	/// </summary>
	private static void RegisterTransportSenderReceiver(
		IServiceCollection services,
		string name,
		GooglePubSubTransportOptions transportOptions)
	{
		// Only register the sender when a topic is configured, mirroring the receiver's
		// SubscriptionId guard below (kek7vm "each capability registered iff configured").
		// A subscriber-only config (ProjectId + SubscriptionId, no TopicId) must not build
		// new TopicName(projectId, null), which throws ArgumentNullException.
		if (!string.IsNullOrEmpty(transportOptions.TopicId))
		{
			var topicName = new TopicName(transportOptions.ProjectId, transportOptions.TopicId).ToString();

			services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
			{
				var apiClient = PublisherServiceApiClient.Create();
				var logger = sp.GetRequiredService<ILogger<PubSubTransportSender>>();
				return new PubSubTransportSender(apiClient, topicName, logger);
			});
		}

		if (!string.IsNullOrEmpty(transportOptions.SubscriptionId))
		{
			var subscriptionName = new SubscriptionName(
				transportOptions.ProjectId,
				transportOptions.SubscriptionId).ToString();

			services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
			{
				var apiClient = SubscriberServiceApiClient.Create();
				var logger = sp.GetRequiredService<ILogger<PubSubTransportReceiver>>();
				return new PubSubTransportReceiver(apiClient, subscriptionName, logger);
			});
		}
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		GooglePubSubTransportOptions transportOptions)
	{
		// Only register if a subscription is configured
		if (string.IsNullOrEmpty(transportOptions.SubscriptionId))
		{
			return;
		}

		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var subscriber = sp.GetRequiredService<SubscriberClient>();
			var logger = sp.GetRequiredService<ILogger<PubSubTransportSubscriber>>();
			var source = transportOptions.SubscriptionId ?? name;
			var nativeSubscriber = new PubSubTransportSubscriber(subscriber, source, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}
}

/// <summary>
/// Builder interface for fluent Google Pub/Sub transport configuration.
/// </summary>
public interface IGooglePubSubTransportBuilder
{
	/// <summary>
	/// Sets the Google Cloud project ID.
	/// </summary>
	/// <param name="projectId">The Google Cloud project ID.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder ProjectId(string projectId);

	/// <summary>
	/// Sets the Pub/Sub topic ID for publishing.
	/// </summary>
	/// <param name="topicId">The topic ID.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder TopicId(string topicId);

	/// <summary>
	/// Sets the Pub/Sub subscription ID for receiving messages.
	/// </summary>
	/// <param name="subscriptionId">The subscription ID.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder SubscriptionId(string subscriptionId);

	/// <summary>
	/// Configures the Google Pub/Sub options.
	/// </summary>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder ConfigureOptions(Action<GooglePubSubTransportOptions> configure);

	/// <summary>
	/// Maps a message type to a specific topic.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	/// <param name="topicId">The topic ID for this message type.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder MapTopic<T>(string topicId);

	/// <summary>
	/// Enables dead letter topic for failed messages.
	/// </summary>
	/// <param name="deadLetterTopicId">The dead letter topic ID.</param>
	/// <returns>The builder for chaining.</returns>
	IGooglePubSubTransportBuilder EnableDeadLetter(string deadLetterTopicId);

	/// <summary>
	/// Configures CloudEvents settings for the Google Pub/Sub transport.
	/// </summary>
	/// <param name="configure">The action to configure CloudEvents options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure CloudEvents-specific settings such as:
	/// </para>
	/// <list type="bullet">
	///   <item><description>Ordering keys for message ordering</description></item>
	///   <item><description>Exactly-once delivery semantics</description></item>
	///   <item><description>Message deduplication</description></item>
	///   <item><description>Compression settings</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// pubsub.ConfigureCloudEvents(ce =>
	/// {
	///     ce.UseOrderingKeys = true;
	///     ce.UseExactlyOnceDelivery = true;
	///     ce.EnableDeduplication = true;
	///     ce.Transport.EnableCompression = true;
	///     ce.Transport.CompressionThreshold = 1024 * 1024;
	/// });
	/// </code>
	/// </example>
	IGooglePubSubTransportBuilder ConfigureCloudEvents(Action<GooglePubSubCloudEventOptions> configure);
}

/// <summary>
/// Implementation of the Google Pub/Sub transport builder.
/// </summary>
internal sealed class GooglePubSubTransportBuilder : IGooglePubSubTransportBuilder
{
	private readonly GooglePubSubTransportOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="GooglePubSubTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public GooglePubSubTransportBuilder(GooglePubSubTransportOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		_options.ProjectId = projectId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder TopicId(string topicId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
		_options.TopicId = topicId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder SubscriptionId(string subscriptionId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
		_options.SubscriptionId = subscriptionId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ConfigureOptions(Action<GooglePubSubTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		configure(_options);
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder MapTopic<T>(string topicId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
		_options.TopicMappings[typeof(T)] = topicId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder EnableDeadLetter(string deadLetterTopicId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterTopicId);
		_options.EnableDeadLetterTopic = true;
		_options.DeadLetterTopicId = deadLetterTopicId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ConfigureCloudEvents(Action<GooglePubSubCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_options.CloudEventOptions ??= new GooglePubSubCloudEventOptions();
		configure(_options.CloudEventOptions);

		return this;
	}
}

/// <summary>
/// Configuration options for Google Pub/Sub transport.
/// </summary>
public sealed class GooglePubSubTransportOptions
{
	/// <summary>
	/// Gets or sets the transport name for multi-transport routing.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the Pub/Sub topic ID for publishing.
	/// </summary>
	public string? TopicId { get; set; }

	/// <summary>
	/// Gets or sets the Pub/Sub subscription ID for receiving messages.
	/// </summary>
	public string? SubscriptionId { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages to pull in a single request. Default is 100.
	/// </summary>
	public int MaxPullMessages { get; set; } = 100;

	/// <summary>
	/// Gets or sets the acknowledgment deadline in seconds. Default is 60.
	/// </summary>
	public int AckDeadlineSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically extend the acknowledgment deadline. Default is true.
	/// </summary>
	public bool EnableAutoAckExtension { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process. Default is 0 (Environment.ProcessorCount * 2).
	/// </summary>
	public int MaxConcurrentMessages { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter topic. Default is false.
	/// </summary>
	public bool EnableDeadLetterTopic { get; set; }

	/// <summary>
	/// Gets or sets the dead letter topic ID.
	/// </summary>
	public string? DeadLetterTopicId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable OpenTelemetry integration. Default is true.
	/// </summary>
	public bool EnableOpenTelemetry { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the subscription requires per-ordering-key FIFO
	/// delivery (abyfxr, FR-A3). When <see langword="true"/>, the transport validates at startup that
	/// the configured subscription has message ordering enabled (read-only <c>GetSubscription</c>) and
	/// throws a clear configuration error if it does not — so a configured-but-unhonored ordering flag
	/// fails loud instead of being silently inert. The producer already stamps the ordering key; FIFO
	/// only holds when the subscription itself is ordering-enabled. Default is false.
	/// </summary>
	public bool EnableMessageOrdering { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the subscription requires exactly-once delivery
	/// semantics (abyfxr, FR-A3). When <see langword="true"/>, the transport validates at startup that
	/// the configured subscription has exactly-once delivery enabled (read-only <c>GetSubscription</c>)
	/// and throws a clear configuration error if it does not. The dedup delivery behavior itself is
	/// enforced by Google Pub/Sub at runtime. Default is false.
	/// </summary>
	public bool EnableExactlyOnceDelivery { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of outstanding (unacknowledged) messages the streaming
	/// subscriber will hold before applying flow control (abyfxr, FR-A3). Zero (default) uses the
	/// Google client default. Applied to the streaming <c>SubscriberClient</c>'s flow-control settings.
	/// </summary>
	public long MaxOutstandingMessages { get; set; }

	/// <summary>
	/// Gets or sets the maximum total size, in bytes, of outstanding (unacknowledged) messages the
	/// streaming subscriber will hold before applying flow control (abyfxr, FR-A3). Zero (default)
	/// uses the Google client default.
	/// </summary>
	public long MaxOutstandingByteCount { get; set; }

	/// <summary>
	/// Gets the message type to topic mappings.
	/// </summary>
	public Dictionary<Type, string> TopicMappings { get; } = new();

	/// <summary>
	/// Gets or sets the CloudEvents configuration options.
	/// </summary>
	public GooglePubSubCloudEventOptions? CloudEventOptions { get; set; }
}
