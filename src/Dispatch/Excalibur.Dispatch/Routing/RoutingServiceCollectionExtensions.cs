// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Options.Routing;
using Excalibur.Dispatch.Routing.Builder;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection" /> to register message routing services.
/// </summary>
public static class RoutingServiceCollectionExtensions
{
	/// <summary>
	/// Adds dispatch routing services to the service collection with default configuration.
	/// </summary>
	/// <param name="services">The service collection to add routing services to.</param>
	/// <param name="configure">Optional configuration action for routing options.</param>
	/// <returns>The same service collection instance for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers a default <see cref="IDispatchRouter"/> that routes all messages
	/// to the "local" transport. For custom routing configuration, use <c>UseRouting()</c>
	/// on the <see cref="IDispatchBuilder"/> instead.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDispatchRouting(this IServiceCollection services, Action<RoutingOptions>? configure = null)
	{
		var optionsBuilder = services.AddOptions<RoutingOptions>();
		if (configure != null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register default transport selector (routes everything to "local")
		services.TryAddSingleton<ITransportSelector>(sp =>
		{
			var builder = new RoutingBuilder();
			builder.Transport.Default("local");
			var config = new RoutingConfiguration(builder);
			return new ConfiguredTransportSelector(config);
		});

		// Register default endpoint router (no endpoint routing by default)
		services.TryAddSingleton<IEndpointRouter>(sp =>
		{
			var builder = new RoutingBuilder();
			var config = new RoutingConfiguration(builder);
			return new ConfiguredEndpointRouter(config);
		});

		// Register the unified dispatch router
		services.TryAddSingleton<IDispatchRouter>(sp =>
		{
			var transportSelector = sp.GetRequiredService<ITransportSelector>();
			var endpointRouter = sp.GetRequiredService<IEndpointRouter>();
			return new DefaultDispatchRouter(transportSelector, endpointRouter);
		});

		return services;
	}
}
