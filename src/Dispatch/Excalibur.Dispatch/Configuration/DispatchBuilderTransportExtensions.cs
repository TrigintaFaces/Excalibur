// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using IBindingConfigBuilder = Excalibur.Dispatch.Abstractions.Configuration.ITransportBindingBuilder;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for configuring transport bindings with IDispatchBuilder.
/// </summary>
public static class DispatchBuilderTransportExtensions
{
	/// <summary>
	/// Adds event binding configuration to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for bindings. </param>
	/// <returns> The dispatch builder for fluent configuration. </returns>
	public static IDispatchBuilder AddEventBindings(
		this IDispatchBuilder builder,
		Action<IBindingConfigBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure single registry instances across all calls
		var transportRegistry = GetOrCreateTransportRegistry(builder.Services);
#pragma warning disable CA2000 // Dispose objects before losing scope - ownership transferred to DI container
		var bindingRegistry = GetOrCreateBindingRegistry(builder.Services);
#pragma warning restore CA2000

		// Create and configure binding builder
		var bindingBuilder = new Transport.BindingConfigurationBuilder(transportRegistry, bindingRegistry);
		configure(bindingBuilder);

		return builder;
	}

	/// <summary>
	/// Adds transport startup validation to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Optional configuration action for validation options. </param>
	/// <returns> The dispatch builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// This registers a hosted service that validates transport configuration at startup.
	/// Validation includes checking that a default transport is configured when multiple
	/// transports are registered.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder AddTransportValidation(
		this IDispatchBuilder builder,
		Action<TransportValidationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddTransportValidation(configure);

		return builder;
	}

	/// <summary>
	/// Gets the existing <see cref="TransportRegistry"/> from DI or creates and registers a new one.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The singleton <see cref="TransportRegistry"/> instance. </returns>
	private static TransportRegistry GetOrCreateTransportRegistry(IServiceCollection services)
		=> ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);

	/// <summary>
	/// Gets the existing <see cref="TransportBindingRegistry"/> from DI or creates and registers a new one.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The singleton <see cref="TransportBindingRegistry"/> instance. </returns>
	private static TransportBindingRegistry GetOrCreateBindingRegistry(IServiceCollection services)
	{
		// Check if already registered
		var existingDescriptor = services.FirstOrDefault(
			d => d.ServiceType == typeof(TransportBindingRegistry) && d.ImplementationInstance is not null);

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
