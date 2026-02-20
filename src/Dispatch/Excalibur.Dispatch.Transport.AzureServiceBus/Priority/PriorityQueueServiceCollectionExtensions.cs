// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus priority queue support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Priority queues emulate message prioritization by routing messages to
/// priority-specific queues. Consumers process higher-priority queues first.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAzureServiceBusPriorityQueues(options =>
/// {
///     options.PriorityLevels = 3;
///     options.QueueNameTemplate = "orders-priority-{0}";
///     options.DefaultPriority = 1;
/// });
/// </code>
/// </example>
public static class PriorityQueueServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Service Bus priority queue support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure priority queue options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="AzureServiceBusPriorityOptions"/> in the DI container with
	/// data annotation validation and startup validation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAzureServiceBusPriorityQueues(
		this IServiceCollection services,
		Action<AzureServiceBusPriorityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AzureServiceBusPriorityOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
