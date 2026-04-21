// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

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

		_ = services.AddSingleton<DispatchMetrics>();
		_ = services.AddSingleton<IDispatchMetrics>(static provider => provider.GetRequiredService<DispatchMetrics>());
		_ = services.AddOptions<ObservabilityOptions>()
			.Configure(static _ => { })
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>());

		return services;
	}

	/// <summary>
	/// Adds Circuit Breaker metrics instrumentation to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddCircuitBreakerMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<CircuitBreakerMetrics>();
		_ = services.AddSingleton<ICircuitBreakerMetrics>(static provider => provider.GetRequiredService<CircuitBreakerMetrics>());

		return services;
	}

	/// <summary>
	/// Adds Dead Letter Queue metrics instrumentation to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDeadLetterQueueMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<DeadLetterQueueMetrics>();
		_ = services.AddSingleton<IDeadLetterQueueMetrics>(static provider => provider.GetRequiredService<DeadLetterQueueMetrics>());

		return services;
	}

	/// <summary>
	/// Adds all Dispatch observability metrics (core, circuit breaker, and DLQ) to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddAllDispatchMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddDispatchMetricsInstrumentation();
		_ = services.AddCircuitBreakerMetrics();
		_ = services.AddDeadLetterQueueMetrics();

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
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"Configuration binding for metrics instrumentation requires dynamic code generation for property reflection and value conversion.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
