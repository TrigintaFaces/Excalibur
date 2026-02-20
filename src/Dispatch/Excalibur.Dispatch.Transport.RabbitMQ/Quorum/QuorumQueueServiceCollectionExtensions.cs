// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ quorum queue support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Quorum queues are replicated queues using the Raft consensus protocol for strong
/// data safety guarantees. They replace classic mirrored queues for scenarios
/// requiring high availability and message durability.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqQuorumQueues(options =>
/// {
///     options.DeliveryLimit = 5;
///     options.DeadLetterStrategy = DeadLetterStrategy.AtLeastOnce;
///     options.QuorumSize = 3;
/// });
/// </code>
/// </example>
public static class QuorumQueueServiceCollectionExtensions
{
	/// <summary>
	/// Adds RabbitMQ quorum queue support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure quorum queue options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="QuorumQueueOptions"/> in the DI container with data annotation
	/// validation and startup validation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddRabbitMqQuorumQueues(
		this IServiceCollection services,
		Action<QuorumQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<QuorumQueueOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
