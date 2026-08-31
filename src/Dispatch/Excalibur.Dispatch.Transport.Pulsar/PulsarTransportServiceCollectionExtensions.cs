// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Pulsar;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Apache Pulsar transport with the service collection.
/// </summary>
/// <remarks>
/// This registers the Pulsar transport <em>primitives</em>: a keyed
/// <see cref="ITransportSender"/> and <see cref="ITransportReceiver"/> that send, receive, and
/// acknowledge messages against a Pulsar broker over the DotPulsar client. Resolve them by transport
/// name to move messages directly. High-level integration into the dispatch pipeline (an
/// <c>ITransportAdapter</c> that publishes and consumes typed dispatch messages end-to-end) is not part
/// of this registration and is provided separately; request/reply is not natively supported.
/// </remarks>
/// <example>
/// <code>
/// services.AddPulsarTransport("events", pulsar =>
/// {
///     pulsar.ServiceUrl("pulsar://localhost:6650")
///           .Topic("orders")
///           .SubscriptionName("order-processors")
///           .SubscriptionType(PulsarSubscriptionType.Shared);
/// });
/// </code>
/// </example>
public static class PulsarTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "pulsar";

	/// <summary>
	/// The producer send-queue depth. The DotPulsar <c>ProducerOptions(topic, schema)</c> constructor
	/// leaves <c>MaxPendingMessages</c> at 0, which <c>CreateProducer</c> rejects; this restores DotPulsar's
	/// conventional default so producers construct without requiring the consumer to configure it.
	/// </summary>
	private const uint DefaultMaxPendingMessages = 1000;

	/// <summary>
	/// Adds a Pulsar transport with the specified name and configuration.
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
	public static IServiceCollection AddPulsarTransport(
		this IServiceCollection services,
		string name,
		Action<IPulsarTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// The builder is a VIEW over the options instance the options system owns, not a second
		// instance whose contents are copied across afterwards. The consumer's fluent calls mutate the
		// live options directly, so "the builder collected a value the registration forgot to copy"
		// is not expressible here.
		_ = services.AddOptions<PulsarOptions>(name)
			.Configure(options => configure(new PulsarTransportBuilder(options)))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PulsarOptions>, PulsarOptionsValidator>());

		RegisterPulsarClientAndChannels(services, name);

		return services;
	}

	/// <summary>
	/// Registers the DotPulsar client, producer, consumer, and the keyed
	/// <see cref="ITransportSender"/>/<see cref="ITransportReceiver"/> for the named transport.
	/// <c>TryAdd*</c> lets a consumer override any registration (Microsoft-first).
	/// </summary>
	private static void RegisterPulsarClientAndChannels(IServiceCollection services, string name)
	{
		// One IPulsarClient per named transport, keyed by name.
		services.TryAddKeyedSingleton<IPulsarClient>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get(name);

			// The sender, the receiver and every channel below share this one client, so refusing here
			// refuses the whole transport — and it happens when the transport is resolved, not on the
			// first publish.
			PulsarSecurityPosture.RequireSecureServiceUrl(options);

			return PulsarClient.Builder()
				.ServiceUrl(new Uri(options.ServiceUrl))
				.Build();
		});

		services.TryAddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get(name);
			var client = sp.GetRequiredKeyedService<IPulsarClient>(name);
			var producer = client.CreateProducer(new ProducerOptions<byte[]>(options.Topic, Schema.ByteArray)
			{
				MaxPendingMessages = DefaultMaxPendingMessages,
			});
			var logger = sp.GetRequiredService<ILogger<PulsarTransportSender>>();
			return new PulsarTransportSender(producer, options.Topic, logger);
		});

		services.TryAddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get(name);
			var client = sp.GetRequiredKeyedService<IPulsarClient>(name);
			// InitialPosition is set EXPLICITLY, at the value already in force. The client defaults it
			// to Latest whether or not we say so, and an inherited default is one nobody chose and
			// nobody can find: a reader asking "where does a new subscriber start?" had to know
			// DotPulsar's defaults to answer it. The Kafka transport already answers that question in
			// its own options, so this is consistency, not a behaviour change.
			var consumerOptions = new ConsumerOptions<byte[]>(options.SubscriptionName, options.Topic, Schema.ByteArray)
			{
				SubscriptionType = MapSubscriptionType(options.SubscriptionType),
				InitialPosition = MapInitialPosition(options.SubscriptionInitialPosition),
			};
			var consumer = client.CreateConsumer(consumerOptions);
			var logger = sp.GetRequiredService<ILogger<PulsarTransportReceiver>>();
			return new PulsarTransportReceiver(consumer, options.SubscriptionName, logger, options.Receive.MaxPayloadBytes, options.Receive.MaxBatchSize);
		});

		// The full IMessageBus publisher for the dispatch pipeline, keyed by transport name so it
		// coexists with the sender/receiver primitives. It owns its own producer bound to the
		// configured topic and serializes via IPayloadSerializer. TryAdd* lets a consumer override.
		services.TryAddKeyedSingleton<IMessageBus>(name, (sp, _) =>
		{
			var options = sp.GetRequiredService<IOptionsMonitor<PulsarOptions>>().Get(name);
			var client = sp.GetRequiredKeyedService<IPulsarClient>(name);
			var producer = client.CreateProducer(new ProducerOptions<byte[]>(options.Topic, Schema.ByteArray)
			{
				MaxPendingMessages = DefaultMaxPendingMessages,
			});
			var serializer = sp.GetRequiredService<IPayloadSerializer>();
			var logger = sp.GetRequiredService<ILogger<PulsarMessageBus>>();
			return new PulsarMessageBus(producer, serializer, options.Topic, logger);
		});
	}

	private static SubscriptionType MapSubscriptionType(PulsarSubscriptionType subscriptionType) => subscriptionType switch
	{
		PulsarSubscriptionType.Exclusive => SubscriptionType.Exclusive,
		PulsarSubscriptionType.Failover => SubscriptionType.Failover,
		PulsarSubscriptionType.KeyShared => SubscriptionType.KeyShared,
		_ => SubscriptionType.Shared,
	};

	// The fall-through is Latest deliberately, matching both the client's default and the Kafka
	// transport's offset-reset default. An unrecognised value must not silently become Earliest: that
	// would turn a typo into a full replay of a topic's retained history on the next new subscription.
	private static SubscriptionInitialPosition MapInitialPosition(PulsarSubscriptionInitialPosition position) => position switch
	{
		PulsarSubscriptionInitialPosition.Earliest => SubscriptionInitialPosition.Earliest,
		_ => SubscriptionInitialPosition.Latest,
	};

	/// <summary>
	/// Adds a Pulsar transport with the default name (<c>pulsar</c>) and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddPulsarTransport(
		this IServiceCollection services,
		Action<IPulsarTransportBuilder> configure)
	{
		return services.AddPulsarTransport(DefaultTransportName, configure);
	}
}
