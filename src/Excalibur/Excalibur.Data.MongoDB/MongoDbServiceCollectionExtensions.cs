// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MongoDB;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB persistence services.
/// </summary>
public static class MongoDbServiceCollectionExtensions
{
	/// <summary>
	/// Adds MongoDB persistence services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the MongoDB provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburMongoDb(
		this IServiceCollection services,
		Action<MongoDbProviderOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbProviderOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbProviderOptions>, MongoDbProviderOptionsValidator>());

		services.TryAddSingleton<MongoDbPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mongodb",
			(sp, _) => sp.GetRequiredService<MongoDbPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mongodb"));

		return services;
	}

	/// <summary>
	/// Adds MongoDB persistence services to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddExcaliburMongoDb(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MongoDbProviderOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbProviderOptions>, MongoDbProviderOptionsValidator>());

		services.TryAddSingleton<MongoDbPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mongodb",
			(sp, _) => sp.GetRequiredService<MongoDbPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mongodb"));

		return services;
	}
}
