// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing.Policies;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering routing policy file loading services.
/// </summary>
public static class RoutingPolicyServiceCollectionExtensions
{
	/// <summary>
	/// Adds file-based routing policy loading with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for routing policy options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRoutingPolicyFile(
		this IServiceCollection services,
		Action<RoutingPolicyOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.Configure(configure);
		services.TryAddSingleton<RoutingPolicyFileLoader>();

		return services;
	}

	/// <summary>
	/// Adds file-based routing policy loading with the specified file path.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="policyFilePath">The path to the routing policy JSON file.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRoutingPolicyFile(
		this IServiceCollection services,
		string policyFilePath)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(policyFilePath);

		services.Configure<RoutingPolicyOptions>(options => options.PolicyFilePath = policyFilePath);
		services.TryAddSingleton<RoutingPolicyFileLoader>();

		return services;
	}
}
