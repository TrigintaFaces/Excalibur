// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ dead letter queue services with the service collection.
/// </summary>
public static class RabbitMqDeadLetterServiceCollectionExtensions
{
	/// <summary>
	/// Adds RabbitMQ dead letter queue support with the specified configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An optional action to configure the dead letter queue options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="IDeadLetterQueueManager"/> implementation backed by RabbitMQ
	/// dead letter exchanges (DLX). Requires RabbitMQ transport to be registered first
	/// via <c>AddRabbitMqTransport</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddRabbitMqTransport("events", rmq => { ... });
	/// services.AddRabbitMqDeadLetterQueue(dlq =>
	/// {
	///     dlq.Exchange = "my-dead-letters";
	///     dlq.QueueName = "my-dlq";
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddRabbitMqDeadLetterQueue(
		this IServiceCollection services,
		Action<RabbitMqDeadLetterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}
		else
		{
			_ = services.Configure<RabbitMqDeadLetterOptions>(_ => { });
		}

		services.TryAddSingleton<IDeadLetterQueueManager, RabbitMqDeadLetterQueueManager>();

		return services;
	}
}
