// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers a distributed cache whose keyspace is partitioned per application.
/// </summary>
public static class ScopedDistributedCacheServiceCollectionExtensions
{
	/// <summary>
	/// Registers an application-scoped <see cref="IDistributedCache"/> under
	/// <see cref="DistributedCacheServiceKeys.ApplicationScoped"/>, wrapping whichever unkeyed
	/// <see cref="IDistributedCache"/> the container resolves.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configure">Configures the partition the cache writes into.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This registers a SEPARATE keyed service; it does not replace or decorate the unkeyed registration.
	/// That is what makes the guarantee checkable: a component that needs partitioning depends on the key
	/// and fails to resolve when nothing registered one, rather than resolving an unkeyed cache and being
	/// unable to tell whether it was partitioned.
	/// </para>
	/// <para>
	/// It resolves the inner cache lazily, per the service-provider factory, so registration ORDER does not
	/// decide what gets wrapped — unlike a decoration that mutates an existing descriptor in place and
	/// therefore only ever wraps what was registered before it ran.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddApplicationScopedDistributedCache(
		this IServiceCollection services,
		Action<CacheKeyScopeOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CacheKeyScopeOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CacheKeyScopeOptions>>(
				new CacheKeyScopeOptionsValidator()));

		// Keyed, and resolved through a factory rather than by mutating the unkeyed descriptor. The inner
		// cache is whatever the container holds AT RESOLUTION time, so a consumer registering their cache
		// after this call is still partitioned.
		services.TryAddKeyedSingleton<IDistributedCache>(
			DistributedCacheServiceKeys.ApplicationScoped,
			(provider, _) => new ScopedDistributedCache(
				provider.GetRequiredService<IDistributedCache>(),
				provider.GetRequiredService<IOptions<CacheKeyScopeOptions>>().Value.Scope));

		return services;
	}
}
