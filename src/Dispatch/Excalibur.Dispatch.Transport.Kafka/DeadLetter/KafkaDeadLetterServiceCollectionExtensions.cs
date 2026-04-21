// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kafka dead letter queue services with the service collection.
/// </summary>
public static class KafkaDeadLetterServiceCollectionExtensions
{
	/// <summary>
	/// Adds Kafka dead letter queue support with the specified configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An optional action to configure the dead letter queue options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="IDeadLetterQueueManager"/> implementation backed by Kafka topics.
	/// Dead letter topics follow the naming convention <c>{original-topic}.dead-letter</c> by default.
	/// </para>
	/// <para>
	/// Requires Kafka transport to be registered first via <c>AddKafkaTransport</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddKafkaTransport("events", kafka => { ... });
	/// services.AddKafkaDeadLetterQueue(dlq =>
	/// {
	///     dlq.TopicSuffix = ".dlq";
	///     dlq.ConsumerGroupId = "my-dlq-processor";
	///     dlq.MaxDeliveryAttempts = 3;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddKafkaDeadLetterQueue(
		this IServiceCollection services,
		Action<KafkaDeadLetterOptions>? configure = null)
		=> AddKafkaDeadLetterQueue(services, "default", configure);

	/// <summary>
	/// Adds Kafka dead letter queue support using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="KafkaDeadLetterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddKafkaDeadLetterQueue(
		this IServiceCollection services,
		IConfiguration configuration)
		=> AddKafkaDeadLetterQueue(services, "default", configuration);

	/// <summary>
	/// Adds Kafka dead letter queue support with the specified transport name and configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="transportName"> The transport name used as the keyed service key. </param>
	/// <param name="configure"> An optional action to configure the dead letter queue options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddKafkaDeadLetterQueue(
		this IServiceCollection services,
		string transportName,
		Action<KafkaDeadLetterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}
		else
		{
			_ = services.Configure<KafkaDeadLetterOptions>(_ => { });
		}

		// Register internal DLQ components
		services.TryAddSingleton<KafkaDeadLetterProducer>();
		services.TryAddSingleton<KafkaDeadLetterConsumer>();

		// Register the transport-agnostic IDeadLetterQueueManager (keyed by transport name)
		services.AddKeyedSingleton<IDeadLetterQueueManager>(transportName,
			(sp, _) => sp.GetRequiredService<KafkaDeadLetterQueueManager>());
		services.TryAddSingleton<KafkaDeadLetterQueueManager>();

		return services;
	}

	/// <summary>
	/// Adds Kafka dead letter queue support with the specified transport name using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="transportName">The transport name used as the keyed service key.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="KafkaDeadLetterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddKafkaDeadLetterQueue(
		this IServiceCollection services,
		string transportName,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<KafkaDeadLetterOptions>().Bind(configuration);

		// Register internal DLQ components
		services.TryAddSingleton<KafkaDeadLetterProducer>();
		services.TryAddSingleton<KafkaDeadLetterConsumer>();

		// Register the transport-agnostic IDeadLetterQueueManager (keyed by transport name)
		services.AddKeyedSingleton<IDeadLetterQueueManager>(transportName,
			(sp, _) => sp.GetRequiredService<KafkaDeadLetterQueueManager>());
		services.TryAddSingleton<KafkaDeadLetterQueueManager>();

		return services;
	}
}
