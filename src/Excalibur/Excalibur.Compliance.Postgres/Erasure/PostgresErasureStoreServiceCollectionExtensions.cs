// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres erasure store services.
/// </summary>
public static class PostgresErasureStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Postgres erasure store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStore(
		this IServiceCollection services,
		Action<PostgresErasureStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresErasureStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<PostgresErasureStore>();
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<PostgresErasureStore>());

		return services;
	}

	/// <summary>
	/// Adds the Postgres erasure store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresErasureStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Adds the Postgres erasure store with connection string from configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionStringName">The connection string name from configuration.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<PostgresErasureStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<PostgresErasureStoreOptions>()
			.Configure<IConfiguration>((options, config) =>
			{
				var connectionString = config.GetConnectionString(connectionStringName);
				if (!string.IsNullOrEmpty(connectionString))
				{
					options.ConnectionString = connectionString;
				}
			})
			.PostConfigure(options =>
			{
				configure?.Invoke(options);
				options.Validate();
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<PostgresErasureStore>();
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<PostgresErasureStore>());

		return services;
	}
}
