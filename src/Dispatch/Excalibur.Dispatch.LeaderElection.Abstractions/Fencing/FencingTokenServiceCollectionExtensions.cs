// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering fencing token services.
/// </summary>
public static class FencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Adds fencing token middleware and registers the specified provider.
	/// </summary>
	/// <typeparam name="TProvider">The fencing token provider implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFencingTokenSupport<TProvider>(
		this IServiceCollection services)
		where TProvider : class, IFencingTokenProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, TProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds fencing token middleware using an existing provider instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="provider">The fencing token provider instance.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddFencingTokenSupport(
		this IServiceCollection services,
		IFencingTokenProvider provider)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(provider);

		services.TryAddSingleton(provider);
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
