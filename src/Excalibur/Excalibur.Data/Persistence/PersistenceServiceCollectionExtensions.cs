// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring persistence services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
	/// <summary>
	/// Adds the shared persistence services that every provider package builds on.
	/// </summary>
	/// <remarks>
	/// Individual providers are added — and configured — by their own package extension (for example
	/// <c>AddExcaliburSqlServer</c>, <c>AddExcaliburPostgres</c>), each of which registers its
	/// <see cref="IPersistenceProvider" /> under a stable keyed-DI key and, if no provider has claimed it
	/// yet, under <c>"default"</c>. Resolve a specific provider with
	/// <c>GetRequiredKeyedService&lt;IPersistenceProvider&gt;(key)</c> or
	/// <c>[FromKeyedServices(key)]</c>; the non-keyed <see cref="IPersistenceProvider" /> registered here
	/// forwards to <c>"default"</c>.
	/// </remarks>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddPersistence(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		services.TryAddSingleton<IConnectionStringProvider, ConnectionStringProvider>();

		// Fail loud at host start if the consumer forgot to pick a persistence provider.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, PersistencePrerequisiteValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, PersistencePrerequisiteValidator>());

		// Non-keyed IPersistenceProvider convenience alias: forwards to keyed "default" so consumers
		// can inject IPersistenceProvider directly without [FromKeyedServices("default")].
		services.TryAddSingleton<IPersistenceProvider>(sp =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("default"));

		return services;
	}
}
