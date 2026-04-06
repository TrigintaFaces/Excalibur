// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
	/// <see cref="IValidateOptions{TOptions}"/> validation and startup validation.
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
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AzureServiceBusPriorityOptions>, AzureServiceBusPriorityOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds Azure Service Bus priority queue support using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AzureServiceBusPriorityOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAzureServiceBusPriorityQueues(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AzureServiceBusPriorityOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AzureServiceBusPriorityOptions>, AzureServiceBusPriorityOptionsValidator>());

		return services;
	}
}
