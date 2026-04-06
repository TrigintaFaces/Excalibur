// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Service Bus dead letter queue services with the service collection.
/// </summary>
public static class ServiceBusDeadLetterServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Service Bus dead letter queue support with the specified configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An optional action to configure the dead letter queue options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Registers the <see cref="IDeadLetterQueueManager"/> implementation backed by Azure Service Bus
	/// native <c>$DeadLetterQueue</c> subqueue. Requires <c>ServiceBusClient</c> to be registered first
	/// via <c>AddAzureServiceBusTransport</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddAzureServiceBusTransport("orders", asb => { ... });
	/// services.AddServiceBusDeadLetterQueue(dlq =>
	/// {
	///     dlq.EntityPath = "orders";
	///     dlq.MaxBatchSize = 50;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddServiceBusDeadLetterQueue(
		this IServiceCollection services,
		Action<ServiceBusDeadLetterOptions>? configure = null)
		=> AddServiceBusDeadLetterQueue(services, "default", configure);

	/// <summary>
	/// Adds Azure Service Bus dead letter queue support using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ServiceBusDeadLetterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddServiceBusDeadLetterQueue(
		this IServiceCollection services,
		IConfiguration configuration)
		=> AddServiceBusDeadLetterQueue(services, "default", configuration);

	/// <summary>
	/// Adds Azure Service Bus dead letter queue support with the specified transport name and configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="transportName"> The transport name used as the keyed service key. </param>
	/// <param name="configure"> An optional action to configure the dead letter queue options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddServiceBusDeadLetterQueue(
		this IServiceCollection services,
		string transportName,
		Action<ServiceBusDeadLetterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);

		if (configure is not null)
		{
			_ = services.Configure(configure);
		}
		else
		{
			_ = services.Configure<ServiceBusDeadLetterOptions>(_ => { });
		}

		services.AddKeyedSingleton<IDeadLetterQueueManager>(transportName,
			(sp, _) => sp.GetRequiredService<ServiceBusDeadLetterQueueManager>());
		services.TryAddSingleton<ServiceBusDeadLetterQueueManager>();

		return services;
	}

	/// <summary>
	/// Adds Azure Service Bus dead letter queue support with the specified transport name using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="transportName">The transport name used as the keyed service key.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ServiceBusDeadLetterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddServiceBusDeadLetterQueue(
		this IServiceCollection services,
		string transportName,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ServiceBusDeadLetterOptions>().Bind(configuration);

		services.AddKeyedSingleton<IDeadLetterQueueManager>(transportName,
			(sp, _) => sp.GetRequiredService<ServiceBusDeadLetterQueueManager>());
		services.TryAddSingleton<ServiceBusDeadLetterQueueManager>();

		return services;
	}
}
