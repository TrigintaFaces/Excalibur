// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helper for the ambient tenant store bridge.
/// </summary>
public static class AmbientTenantStoreResolverServiceCollectionExtensions
{
	/// <summary>
	/// Registers the open-generic ambient tenant store resolver, which routes to the current
	/// ambient tenant's store via the registered <see cref="ITenantStoreResolver{TStore}"/> and
	/// <see cref="Excalibur.Dispatch.ITenantContext"/>. Consumers must also register an
	/// <see cref="ITenantShardMap"/> and provider-specific <see cref="ITenantStoreResolver{TStore}"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	public static IServiceCollection AddAmbientTenantStoreResolver(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAdd(ServiceDescriptor.Transient(
			typeof(IAmbientTenantStoreResolver<>),
			typeof(AmbientTenantStoreResolver<>)));

		return services;
	}
}
