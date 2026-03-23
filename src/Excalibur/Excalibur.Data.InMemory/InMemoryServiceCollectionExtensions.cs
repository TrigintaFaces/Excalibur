// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring in-memory persistence services.
/// </summary>
/// <remarks>
/// The in-memory persistence provider is intended for testing and development only.
/// Data is lost when the process restarts.
/// </remarks>
public static class InMemoryServiceCollectionExtensions
{
	/// <summary>
	/// Adds in-memory persistence services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An optional delegate to configure the in-memory provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburInMemory(
		this IServiceCollection services,
		Action<InMemoryProviderOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var builder = services.AddOptions<InMemoryProviderOptions>();
		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.ValidateDataAnnotations().ValidateOnStart();

		services.TryAddSingleton<InMemoryPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("inmemory"));

		return services;
	}
}
