// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Postgres.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres persistence in the dependency injection container.
/// </summary>
public static class PostgresPersistenceServiceCollectionExtensions
{
	/// <summary>
	/// Adds Postgres persistence provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The Postgres connection string. </param>
	/// <param name="configureOptions"> Optional action to configure persistence options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPostgresPersistence(
		this IServiceCollection services,
		string connectionString,
		Action<PostgresPersistenceOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrEmpty(connectionString);

		// Configure options
		_ = services.AddOptions<PostgresPersistenceOptions>()
			.Configure(options =>
			{
				options.ConnectionString = connectionString;
				configureOptions?.Invoke(options);
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return AddPostgresPersistenceCore(services);
	}

	/// <summary>
	/// Adds Postgres persistence provider to the service collection using configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section containing Postgres settings. </param>
	/// <param name="configureOptions"> Optional action to configure persistence options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding for Postgres persistence may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode("Configuration binding for Postgres persistence settings requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddPostgresPersistence(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<PostgresPersistenceOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Bind configuration and apply additional configuration if provided
		var builder = services.AddOptions<PostgresPersistenceOptions>()
			.Bind(configuration);

		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder.ValidateDataAnnotations()
			.ValidateOnStart();

		return AddPostgresPersistenceCore(services);
	}

	/// <summary>
	/// Adds Postgres persistence provider to the service collection using a configuration section name.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configurationSectionName"> The name of the configuration section. </param>
	/// <param name="configureOptions"> Optional action to configure persistence options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static IServiceCollection AddPostgresPersistenceFromSection(
		this IServiceCollection services,
		string configurationSectionName = "Postgres",
		Action<PostgresPersistenceOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrEmpty(configurationSectionName);

		// Bind configuration section at DI resolution time (avoids BuildServiceProvider anti-pattern)
		var builder = services.AddOptions<PostgresPersistenceOptions>()
			.BindConfiguration(configurationSectionName);

		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return AddPostgresPersistenceCore(services);
	}

	/// <summary>
	/// Configures Postgres persistence options using a builder pattern.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="builderAction"> Action to configure the persistence builder. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddPostgresPersistence(
		this IServiceCollection services,
		Action<IPostgresPersistenceBuilder> builderAction)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(builderAction);

		var builder = new PostgresPersistenceBuilder(services);
		builderAction(builder);

		return services;
	}

	private static IServiceCollection AddPostgresPersistenceCore(IServiceCollection services)
	{
		// Add options validator
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PostgresPersistenceOptions>, PostgresPersistenceOptionsValidator>());

		// Register metrics as singleton
		services.TryAddSingleton<PostgresPersistenceMetrics>();

		// Register the persistence provider
		services.TryAddSingleton<PostgresPersistenceProvider>();
		services.TryAddSingleton<ISqlPersistenceProvider>(static provider => provider.GetRequiredService<PostgresPersistenceProvider>());
		services.TryAddSingleton<IPersistenceProvider>(static provider => provider.GetRequiredService<PostgresPersistenceProvider>());

		// Register transaction scope factory
		services.TryAddTransient<ITransactionScope>(static provider =>
		{
			var logger = provider.GetRequiredService<ILogger<PostgresTransactionScope>>();
			return new PostgresTransactionScope(IsolationLevel.ReadCommitted, logger);
		});

		// Add health check
		_ = services.Configure<HealthCheckServiceOptions>(static options => options.Registrations.Add(new HealthCheckRegistration(
			"Postgres_persistence",
			static provider => new PostgresPersistenceHealthCheck(
				provider.GetRequiredService<IOptions<PostgresPersistenceOptions>>(),
				provider.GetRequiredService<ILogger<PostgresPersistenceHealthCheck>>(),
				provider.GetService<PostgresPersistenceMetrics>()),
			HealthStatus.Unhealthy,
			["database", "Postgres", "persistence"])));

		// Initialize provider on startup
		_ = services.AddHostedService<PostgresPersistenceInitializer>();

		return services;
	}
}
