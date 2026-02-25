// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Excalibur.Saga.Storage;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring saga timeout services.
/// </summary>
public static class SagaTimeoutServiceCollectionExtensions
{
	/// <summary>
	/// Adds the saga timeout delivery background service with in-memory storage.
	/// Use this for testing and development scenarios.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// <list type="bullet">
	/// <item><description><see cref="InMemorySagaTimeoutStore"/> as <see cref="ISagaTimeoutStore"/></description></item>
	/// <item><description><see cref="SagaTimeoutDeliveryService"/> as a hosted service</description></item>
	/// <item><description>Default <see cref="SagaTimeoutOptions"/> configuration</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>Warning:</b> In-memory storage does not persist timeouts across process restarts.
	/// Use <c>AddSqlServerSagaTimeoutStore</c> for production deployments.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSagaTimeoutDelivery(this IServiceCollection services)
	{
		return services.AddSagaTimeoutDelivery(_ => { });
	}

	/// <summary>
	/// Adds the saga timeout delivery background service with in-memory storage and custom options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure timeout options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaTimeoutDelivery(
		this IServiceCollection services,
		Action<SagaTimeoutOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register options
		_ = services.AddOptions<SagaTimeoutOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register in-memory store as default (can be overridden)
		services.TryAddSingleton<InMemorySagaTimeoutStore>();
		services.TryAddSingleton<ISagaTimeoutStore>(sp => sp.GetRequiredService<InMemorySagaTimeoutStore>());

		// Register hosted service
		_ = services.AddHostedService<SagaTimeoutDeliveryService>();

		return services;
	}

	/// <summary>
	/// Adds the saga timeout delivery background service without registering a timeout store.
	/// Use this when you register a custom <see cref="ISagaTimeoutStore"/> implementation separately.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure timeout options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaTimeoutDeliveryService(
		this IServiceCollection services,
		Action<SagaTimeoutOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		var optionsBuilder = services.AddOptions<SagaTimeoutOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register hosted service only (store must be registered separately)
		_ = services.AddHostedService<SagaTimeoutDeliveryService>();

		return services;
	}
}
