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
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

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

		// This transport branches its REGISTRATION GRAPH on configuration (which clients, hosted
		// services, and decorators exist at all depends on whether a subscription, ordering, or a
		// dead-letter policy was configured), so the values are needed eagerly, before the container is
		// built. That is why the consumer's delegate is applied twice, to two instances:
		//
		//   1. here, to a local instance, to decide WHICH registrations to make;
		//   2. inside Configure below, to the instance the options system owns, which is what every
		//      resolved component actually reads.
		//
		// Applying the same delegate twice is not a copy: there is no field list to fall out of date,
		// so a property the builder can set but the registration forgets to carry is not expressible.
		// Options-configuration delegates are already required to be re-runnable — the options system
		// invokes them per named instance and on reload — so this asks nothing new of the consumer.
		var transportOptions = new GooglePubSubOptions { Name = name };
		var transportBuilder = new GooglePubSubTransportBuilder(transportOptions);
		configure(transportBuilder);

		// The adapter binds the UNNAMED GooglePubSubCloudEventOptions, so this registers unnamed too;
		// a named registration here would configure an instance nothing reads.
		if (transportBuilder.CloudEventsConfigure is not null)
		{
			_ = services.AddOptions<GooglePubSubCloudEventOptions>()
				.Configure(transportBuilder.CloudEventsConfigure)
				.ValidateOnStart();
		}

		// Register core Google Pub/Sub services
		RegisterGooglePubSubServices(services, transportOptions);

		// Register Google Pub/Sub options
		RegisterOptions(services, name, configure);

		// Register the transport adapter
		RegisterTransportAdapter(services, name);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, transportOptions);

		// Route the rich ITransportSender/ITransportReceiver classes through DI so they are
		// reachable on the AddGooglePubSubTransport path instead of orphaned (shared-seam
		// wiring). Ordering / exactly-once / flow-control are layered by the child on this seam.
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
		GooglePubSubOptions transportOptions)
	{
		// Register PublisherClient
		services.TryAddSingleton(sp =>
		{
			var topicName = new TopicName(transportOptions.Connection.ProjectId, transportOptions.Connection.TopicId);
			return PublisherClient.Create(topicName);
		});

		// Register SubscriberClient if subscription is configured
		if (!string.IsNullOrEmpty(transportOptions.Connection.SubscriptionId))
		{
			services.TryAddSingleton(sp =>
			{
				var subscriptionName = new SubscriptionName(
					transportOptions.Connection.ProjectId,
					transportOptions.Connection.SubscriptionId);

				// the subscriber client uses EmulatorOrProduction so it talks to the SAME endpoint
				// the fail-loud validator checks (PUBSUB_EMULATOR_HOST when set → emulator; absent →
				// production credentials, unchanged from today). A transport that subscribes to production
				// while the validator checks the emulator (or vice-versa) is a false guarantee.
				if (transportOptions.Subscriber.FlowControl.MaxOutstandingElementCount > 0 || transportOptions.Subscriber.FlowControl.MaxOutstandingByteCount > 0)
				{
					// Apply flow-control to the streaming SubscriberClient when configured,
					// bounding outstanding (unacked) messages/bytes. Flow-control is a streaming-SubscriberClient
					// concept and does not apply to the raw-pull receiver path (SA Q2). Zero = SDK default.
					var settings = new SubscriberClient.Settings
					{
						FlowControlSettings = new FlowControlSettings(
							transportOptions.Subscriber.FlowControl.MaxOutstandingElementCount > 0 ? transportOptions.Subscriber.FlowControl.MaxOutstandingElementCount : null,
							transportOptions.Subscriber.FlowControl.MaxOutstandingByteCount > 0 ? transportOptions.Subscriber.FlowControl.MaxOutstandingByteCount : null),
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

			// Fail-loud startup validation — if ordering/exactly-once is configured,
			// verify (read-only) the deployed subscription actually has it, else throw a clear config error
			// (no silently-inert advertised guarantee). Read-only; never creates the subscription.
			if (transportOptions.Subscriber.EnableMessageOrdering || transportOptions.Subscriber.EnableExactlyOnceDelivery)
			{
				var projectId = transportOptions.Connection.ProjectId ?? string.Empty;
				var subscriptionId = transportOptions.Connection.SubscriptionId;
				var requireOrdering = transportOptions.Subscriber.EnableMessageOrdering;
				var requireExactlyOnce = transportOptions.Subscriber.EnableExactlyOnceDelivery;

				_ = services.AddSingleton<IHostedService>(sp => new PubSubSubscriptionConfigValidator(
					projectId,
					subscriptionId,
					requireOrdering,
					requireExactlyOnce,
					sp.GetRequiredService<ILogger<PubSubSubscriptionConfigValidator>>()));
			}

			// opt-in auto-apply of the configured dead letter policy. When enabled (and a dead
			// letter topic is configured), attach the policy to the subscription at startup so it is
			// actually honored rather than built but never applied. Default off — provisioning is normally
			// an IaC concern (see PubSubSubscriptionConfigValidator's read-only default).
			if (transportOptions.Subscriber.DeadLetter.AutoApplyPolicy
				&& transportOptions.Subscriber.DeadLetter.Enable
				&& !string.IsNullOrWhiteSpace(transportOptions.Subscriber.DeadLetter.TopicId)
				&& !string.IsNullOrWhiteSpace(transportOptions.Connection.SubscriptionId))
			{
				var projectId = transportOptions.Connection.ProjectId ?? string.Empty;
				var subscriptionId = transportOptions.Connection.SubscriptionId;
				var deadLetterTopicId = transportOptions.Subscriber.DeadLetter.TopicId;
				var maxDeliveryAttempts = transportOptions.Subscriber.DeadLetter.MaxDeliveryAttempts;

				_ = services.AddSingleton<IHostedService>(sp => new PubSubDeadLetterPolicyApplier(
					projectId,
					subscriptionId,
					deadLetterTopicId,
					maxDeliveryAttempts,
					sp.GetRequiredService<ILoggerFactory>()));
			}
		}

		// Register GooglePubSubMessageBus
		//
		// NAMED resolution, matching the named registration in RegisterOptions. Reading the UNNAMED
		// IOptions here would hand every named transport whichever configuration was registered last.
		var transportName = transportOptions.Name ?? Microsoft.Extensions.Options.Options.DefaultName;
		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<PublisherClient>();
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var options = sp.GetRequiredService<IOptionsMonitor<GooglePubSubOptions>>().Get(transportName);
			var logger = sp.GetRequiredService<ILogger<GooglePubSubMessageBus>>();

			return new GooglePubSubMessageBus(
				new TopicPublisherClientAdapter(client), serializer, options, logger);
		});
	}

	/// <summary>
	/// Registers options with the service collection.
	/// </summary>
	private static void RegisterOptions(
		IServiceCollection services,
		string name,
		Action<IGooglePubSubTransportBuilder> configure)
	{
		// The builder is a VIEW over the instance the options system owns. The consumer's own delegate
		// IS the configure delegate, so there is no field-by-field carry between two instances and
		// therefore no field that can be left behind. (The previous form enumerated every field into a
		// second instance of the SAME type — an identity function, guarded by a hand-maintained
		// round-trip test. ValidateOnStart needs a registered Configure delegate, not a copy list.)
		// NAMED, so two transports of the same type no longer overwrite one another's configuration.
		_ = services.AddOptions<GooglePubSubOptions>(name)
			.Configure(options =>
			{
				options.Name = name;
				configure(new GooglePubSubTransportBuilder(options));
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
			Excalibur.Dispatch.Transport.TransportLocality.Remote,
			sp => sp.GetRequiredKeyedService<GooglePubSubTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers the rich <see cref="ITransportSender"/> and <see cref="ITransportReceiver"/>
	/// implementations keyed by transport name so they are instantiated and reachable on the
	/// <c>AddGooglePubSubTransport</c> path instead of orphaned. <c>TryAdd*</c> lets a
	/// consumer override the registration (Microsoft-first). The receiver is only registered when a
	/// subscription is configured, mirroring the subscriber registration.
	/// </summary>
	private static void RegisterTransportSenderReceiver(
		IServiceCollection services,
		string name,
		GooglePubSubOptions transportOptions)
	{
		// Only register the sender when a topic is configured, mirroring the receiver's
		// SubscriptionId guard below ("each capability registered iff configured").
		// A subscriber-only config (ProjectId + SubscriptionId, no TopicId) must not build
		// new TopicName(projectId, null), which throws ArgumentNullException.
		if (!string.IsNullOrEmpty(transportOptions.Connection.TopicId))
		{
			var topicName = new TopicName(transportOptions.Connection.ProjectId, transportOptions.Connection.TopicId).ToString();

			services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
			{
				var apiClient = PublisherServiceApiClient.Create();
				var logger = sp.GetRequiredService<ILogger<PubSubTransportSender>>();
				return new PubSubTransportSender(apiClient, topicName, logger);
			});
		}

		if (!string.IsNullOrEmpty(transportOptions.Connection.SubscriptionId))
		{
			var subscriptionName = new SubscriptionName(
				transportOptions.Connection.ProjectId,
				transportOptions.Connection.SubscriptionId).ToString();

			services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
			{
				var apiClient = SubscriberServiceApiClient.Create();
				var logger = sp.GetRequiredService<ILogger<PubSubTransportReceiver>>();
				// The configured pull size is the surface a consumer sets; the pull request is where it
				// takes effect. Leaving the default here would cap every pull at 10 regardless.
				return new PubSubTransportReceiver(
					apiClient, subscriptionName, logger,
					maxMessages: transportOptions.Subscriber.MaxPullMessages,
					maxPayloadBytes: transportOptions.Subscriber.MaxPayloadBytes,
					hasDeadLetterPolicy: !string.IsNullOrWhiteSpace(transportOptions.Subscriber.DeadLetter.TopicId));
			});
		}
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		GooglePubSubOptions transportOptions)
	{
		// Only register if a subscription is configured
		if (string.IsNullOrEmpty(transportOptions.Connection.SubscriptionId))
		{
			return;
		}

		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var subscriber = sp.GetRequiredService<SubscriberClient>();
			var logger = sp.GetRequiredService<ILogger<PubSubTransportSubscriber>>();
			var source = transportOptions.Connection.SubscriptionId ?? name;
			var nativeSubscriber = new PubSubTransportSubscriber(
					subscriber, source, logger, maxPayloadBytes: transportOptions.Subscriber.MaxPayloadBytes,
					hasDeadLetterPolicy: !string.IsNullOrWhiteSpace(transportOptions.Subscriber.DeadLetter.TopicId));

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
	IGooglePubSubTransportBuilder ConfigureOptions(Action<GooglePubSubOptions> configure);

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
	private readonly GooglePubSubOptions _options;

	/// <summary>
	/// The consumer's CloudEvents delegate, handed to the CloudEvents options registration.
	/// </summary>
	/// <remarks>
	/// The CloudEvents adapter binds <c>IOptions&lt;GooglePubSubCloudEventOptions&gt;</c> from DI, so a
	/// value written onto the transport options object is never read. The delegate is carried to that
	/// registration instead of its values being copied into a nested duplicate.
	/// </remarks>
	internal Action<GooglePubSubCloudEventOptions>? CloudEventsConfigure { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="GooglePubSubTransportBuilder"/> class.
	/// </summary>
	/// <param name="options">The transport options to configure.</param>
	public GooglePubSubTransportBuilder(GooglePubSubOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		_options.Connection.ProjectId = projectId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder TopicId(string topicId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
		_options.Connection.TopicId = topicId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder SubscriptionId(string subscriptionId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
		_options.Connection.SubscriptionId = subscriptionId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ConfigureOptions(Action<GooglePubSubOptions> configure)
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
		_options.Subscriber.DeadLetter.Enable = true;
		_options.Subscriber.DeadLetter.TopicId = deadLetterTopicId;
		return this;
	}

	/// <inheritdoc/>
	public IGooglePubSubTransportBuilder ConfigureCloudEvents(Action<GooglePubSubCloudEventOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		CloudEventsConfigure = configure;

		return this;
	}
}
