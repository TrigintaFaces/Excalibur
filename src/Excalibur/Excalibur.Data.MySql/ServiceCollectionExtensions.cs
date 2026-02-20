// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MySql;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MySQL/MariaDB persistence services.
/// </summary>
public static class MySqlServiceCollectionExtensions
{
	/// <summary>
	/// Adds MySQL/MariaDB persistence services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the MySQL provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburMySql(
		this IServiceCollection services,
		Action<MySqlProviderOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		services.Configure(configure);
		services.AddOptions<MySqlProviderOptions>().ValidateDataAnnotations().ValidateOnStart();
		services.TryAddSingleton<MySqlPersistenceProvider>();
		services.TryAddSingleton<IPersistenceProvider>(sp => sp.GetRequiredService<MySqlPersistenceProvider>());

		return services;
	}

	/// <summary>
	/// Adds MySQL/MariaDB persistence services to the service collection using a configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configurationSection">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburMySql(
		this IServiceCollection services,
		string configurationSection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);

		services.AddOptions<MySqlProviderOptions>()
			.BindConfiguration(configurationSection)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<MySqlPersistenceProvider>();
		services.TryAddSingleton<IPersistenceProvider>(sp => sp.GetRequiredService<MySqlPersistenceProvider>());

		return services;
	}
}
