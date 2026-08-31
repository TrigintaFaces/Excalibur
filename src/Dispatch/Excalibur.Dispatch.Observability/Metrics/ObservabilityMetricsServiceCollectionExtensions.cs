// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Dispatch metrics services.
/// </summary>
public static class ObservabilityMetricsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Dispatch metrics instrumentation to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDispatchMetricsInstrumentation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<DispatchMetrics>();
		services.TryAddSingleton<IDispatchMetrics>(static provider => provider.GetRequiredService<DispatchMetrics>());
		_ = services.AddOptions<ObservabilityOptions>()
			.Configure(static _ => { })
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds all Dispatch observability metrics to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	/// <remarks>
	/// Circuit-breaker and dead-letter metrics are emitted directly by the core middleware meters
	/// (<c>Excalibur.Dispatch.CircuitBreakerMiddleware</c> / <c>Excalibur.Dispatch.PoisonMessage</c>) and
	/// need no separate registration; subscribe to them via <c>AddDispatchInstrumentation()</c>.
	/// </remarks>
	public static IServiceCollection AddAllDispatchMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddDispatchMetricsInstrumentation();

		return services;
	}

	/// <summary>
	/// Adds Dispatch metrics instrumentation with configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configure is null. </exception>
	public static IServiceCollection AddDispatchMetricsInstrumentation(
		this IServiceCollection services,
		Action<ObservabilityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddDispatchMetricsInstrumentation();
		_ = services.AddOptions<ObservabilityOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds Dispatch metrics instrumentation with configuration from IConfiguration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	public static IServiceCollection AddDispatchMetricsInstrumentation(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddDispatchMetricsInstrumentation();
		_ = services.AddOptions<ObservabilityOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());

		return services;
	}
}
