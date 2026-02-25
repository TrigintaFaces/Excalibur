// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using IBindingConfigBuilder = Excalibur.Dispatch.Abstractions.Configuration.ITransportBindingBuilder;
using Transport = Excalibur.Dispatch.Configuration.Transport;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring transport bindings with IServiceCollection.
/// </summary>
public static class ServiceCollectionTransportExtensions
{
	/// <summary>
	/// Adds event binding configuration to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration action for bindings. </param>
	/// <returns> The service collection for fluent configuration. </returns>
	public static IServiceCollection AddEventBindings(
		this IServiceCollection services,
		Action<IBindingConfigBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure single registry instances across all calls
		var transportRegistry = GetOrCreateTransportRegistry(services);
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership transferred to DI container
		var bindingRegistry = GetOrCreateBindingRegistry(services);
#pragma warning restore CA2000

		// Create and configure binding builder
		var bindingBuilder = new Transport.BindingConfigurationBuilder(transportRegistry, bindingRegistry);
		configure(bindingBuilder);

		return services;
	}

	/// <summary>
	/// Gets the existing <see cref="TransportRegistry"/> from DI or creates and registers a new one.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The singleton <see cref="TransportRegistry"/> instance. </returns>
	/// <remarks>
	/// <para>
	/// This method is public to allow transport-specific packages to register their adapters
	/// with the shared <see cref="TransportRegistry"/>. Using this method ensures that all
	/// transport adapters are managed by the same registry instance, enabling unified lifecycle
	/// management via <see cref="TransportAdapterHostedService"/>.
	/// </para>
	/// </remarks>
	public static TransportRegistry GetOrCreateTransportRegistry(IServiceCollection services)
	{
		// Check if already registered
		var existingDescriptor =
			services.FirstOrDefault(d => d.ServiceType == typeof(TransportRegistry) && d.ImplementationInstance is not null);

		if (existingDescriptor?.ImplementationInstance is TransportRegistry existing)
		{
			return existing;
		}

		// Create new instance and register
		var registry = new TransportRegistry();
		services.TryAddSingleton(registry);
		return registry;
	}

	/// <summary>
	/// Adds transport adapter lifecycle management as a hosted service.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action for hosted service options. </param>
	/// <returns> The service collection for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This registers a hosted service that manages the lifecycle of transport adapters.
	/// Adapters are started when the application starts and gracefully stopped when
	/// the application shuts down, with a configurable drain timeout for pending messages.
	/// </para>
	/// <para>
	/// Usage example:
	/// <code>
	/// services.AddTransportAdapterLifecycle(opts =>
	/// {
	///     opts.DrainTimeoutSeconds = 60;
	///     opts.ThrowOnStartupFailure = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddTransportAdapterLifecycle(
		this IServiceCollection services,
		Action<TransportAdapterHostedServiceOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure registry exists
		_ = GetOrCreateTransportRegistry(services);

		// Configure options
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}
		else
		{
			_ = services.Configure<TransportAdapterHostedServiceOptions>(static _ => { });
		}

		// Register hosted service
		_ = services.AddHostedService<TransportAdapterHostedService>();

		return services;
	}

	/// <summary>
	/// Adds transport startup validation to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action for validation options. </param>
	/// <returns> The service collection for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This registers a hosted service that validates transport configuration at startup.
	/// Validation includes checking that a default transport is configured when multiple
	/// transports are registered.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddTransportValidation(
		this IServiceCollection services,
		Action<TransportValidationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new TransportValidationOptions();
		configure?.Invoke(options);

		services.TryAddSingleton(options);
		_ = services.AddHostedService<TransportStartupValidator>();

		return services;
	}

	/// <summary>
	/// Adds multi-transport health checks to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional configuration action for health check options. </param>
	/// <param name="name"> The health check name. Default is "transports". </param>
	/// <param name="failureStatus"> The failure status to report. Default is Unhealthy. </param>
	/// <param name="tags"> Optional tags for the health check. Default includes "transport" and "ready". </param>
	/// <param name="timeout"> Optional timeout for the health check. </param>
	/// <returns> The service collection for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This health check monitors all transports registered in the <see cref="TransportRegistry"/>.
	/// It reports the aggregate health status of all transports and provides detailed status
	/// for each individual transport in the health check data.
	/// </para>
	/// <para>
	/// Usage example:
	/// <code>
	/// services.AddMultiTransportHealthChecks(opts =>
	/// {
	///     opts.RequireAtLeastOneTransport = true;
	///     opts.RequireDefaultTransportHealthy = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMultiTransportHealthChecks(
		this IServiceCollection services,
		Action<MultiTransportHealthCheckOptions>? configure = null,
		string name = "transports",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new MultiTransportHealthCheckOptions();
		configure?.Invoke(options);

		services.TryAddSingleton(options);

		// Ensure registry exists
		_ = GetOrCreateTransportRegistry(services);

		_ = services.AddHealthChecks().Add(new HealthCheckRegistration(
			name,
			sp =>
			{
				var registry = sp.GetRequiredService<TransportRegistry>();
				var healthCheckOptions = sp.GetService<MultiTransportHealthCheckOptions>()
										 ?? new MultiTransportHealthCheckOptions();
				return new MultiTransportHealthCheck(registry, healthCheckOptions);
			},
			failureStatus ?? HealthStatus.Unhealthy,
			tags ?? ["transport", "messaging", "ready"],
			timeout));

		return services;
	}

	/// <summary>
	/// Gets the existing <see cref="TransportBindingRegistry"/> from DI or creates and registers a new one.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The singleton <see cref="TransportBindingRegistry"/> instance. </returns>
	private static TransportBindingRegistry GetOrCreateBindingRegistry(IServiceCollection services)
	{
		// Check if already registered
		var existingDescriptor =
			services.FirstOrDefault(d => d.ServiceType == typeof(TransportBindingRegistry) && d.ImplementationInstance is not null);

		if (existingDescriptor?.ImplementationInstance is TransportBindingRegistry existing)
		{
			return existing;
		}

		// Create new instance and register
		// Note: CA2000 suppressed - ownership transferred to DI container as singleton
#pragma warning disable CA2000
		var registry = new TransportBindingRegistry();
#pragma warning restore CA2000
		services.TryAddSingleton(registry);
		return registry;
	}
}
