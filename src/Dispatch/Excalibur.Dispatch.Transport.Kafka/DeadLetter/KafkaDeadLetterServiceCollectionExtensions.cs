// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

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
	{
		ArgumentNullException.ThrowIfNull(services);

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

		// Register the transport-agnostic IDeadLetterQueueManager
		services.TryAddSingleton<IDeadLetterQueueManager, KafkaDeadLetterQueueManager>();

		return services;
	}
}
