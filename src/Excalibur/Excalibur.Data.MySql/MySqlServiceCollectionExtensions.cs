// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MySql;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
		_ = services.AddOptions<MySqlProviderOptions>().ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MySqlProviderOptions>, MySqlProviderOptionsValidator>());

		services.TryAddSingleton<MySqlPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mysql",
			(sp, _) => sp.GetRequiredService<MySqlPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mysql"));

		return services;
	}

	/// <summary>
	/// Adds MySQL/MariaDB persistence services to the service collection using a configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configurationSection">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	public static IServiceCollection AddExcaliburMySql(
		this IServiceCollection services,
		string configurationSection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);

		_ = services.AddOptions<MySqlProviderOptions>()
			.BindConfiguration(configurationSection)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MySqlProviderOptions>, MySqlProviderOptionsValidator>());

		services.TryAddSingleton<MySqlPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mysql",
			(sp, _) => sp.GetRequiredService<MySqlPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mysql"));

		return services;
	}

	/// <summary>
	/// Adds MySQL/MariaDB persistence services to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	public static IServiceCollection AddExcaliburMySql(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<MySqlProviderOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MySqlProviderOptions>, MySqlProviderOptionsValidator>());

		services.TryAddSingleton<MySqlPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("mysql",
			(sp, _) => sp.GetRequiredService<MySqlPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("mysql"));

		return services;
	}
}
