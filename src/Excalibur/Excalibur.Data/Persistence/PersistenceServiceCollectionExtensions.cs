// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring persistence services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
	/// <summary>
	/// Adds persistence services to the service collection with default configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddPersistence(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.AddPersistence(static _ => { });
	}

	/// <summary>
	/// Adds persistence services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An action to configure persistence options. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddPersistence(
		this IServiceCollection services,
		Action<PersistenceConfiguration> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Add memory cache if not already registered
		_ = services.AddMemoryCache();

		// Create and configure the configuration
		var configuration = new PersistenceConfiguration();
		configure(configuration);

		// Register core services
		services.TryAddSingleton<IPersistenceConfiguration>(configuration);
		services.TryAddSingleton(configuration);
		services.TryAddSingleton<IPersistenceProviderFactory, PersistenceProviderFactory>();
		services.TryAddSingleton<IConnectionStringProvider, ConnectionStringProvider>();

		// Validate configuration
		_ = services.AddHostedService<PersistenceConfigurationValidator>();

		return services;
	}

	/// <summary>
	/// Adds persistence services with configuration from appsettings.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
	[RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
	public static IServiceCollection AddPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		return services.AddPersistence(config => ConfigureFromConfiguration(config, configuration));
	}

	/// <summary>
	/// Configures persistence from IConfiguration.
	/// </summary>
	[RequiresUnreferencedCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
	[RequiresDynamicCode("Calls Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Object)")]
	private static void ConfigureFromConfiguration(PersistenceConfiguration config, IConfiguration configuration)
	{
		// Load global options
		var globalOptions = configuration.GetSection("Persistence:GlobalOptions");
		if (globalOptions.Exists())
		{
			globalOptions.Bind(config.GlobalOptions);
		}

		// Load providers
		var providersSection = configuration.GetSection("Persistence:Providers");
		foreach (var providerSection in providersSection.GetChildren())
		{
			var providerConfig = new ProviderConfiguration
			{
				Name = providerSection.Key,
				Type = Enum.Parse<PersistenceProviderType>(
					providerSection["Type"] ?? "Custom", ignoreCase: true),
				ConnectionString = providerSection["ConnectionString"] ?? string.Empty,
			};

			// Bind additional options
			providerSection.Bind(providerConfig);

			config.Providers[providerSection.Key] = providerConfig;
		}

		// Set default provider
		var defaultProvider = configuration["Persistence:DefaultProvider"];
		if (!string.IsNullOrWhiteSpace(defaultProvider))
		{
			config.DefaultProvider = defaultProvider;
		}
	}
}
