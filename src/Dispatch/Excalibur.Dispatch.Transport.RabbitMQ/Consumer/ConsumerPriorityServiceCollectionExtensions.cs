// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ consumer priority support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Consumer priority allows higher-priority consumers to receive messages before lower-priority ones.
/// This is configured via the <c>x-priority</c> argument on the RabbitMQ consumer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMQConsumerPriority(options =>
/// {
///     options.Enabled = true;
///     options.Priority = 10;
/// });
/// </code>
/// </example>
public static class ConsumerPriorityServiceCollectionExtensions
{
	/// <summary>
	/// Adds RabbitMQ consumer priority support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure consumer priority options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="ConsumerPriorityOptions"/> in the DI container with data annotation
	/// validation and startup validation. The options are consumed by the RabbitMQ consumer
	/// infrastructure to set the <c>x-priority</c> argument on consumer queue declarations.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddRabbitMQConsumerPriority(
		this IServiceCollection services,
		Action<ConsumerPriorityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ConsumerPriorityOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
