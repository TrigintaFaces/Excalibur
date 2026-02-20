// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Postgres.Snapshots;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres snapshot store services.
/// </summary>
public static class PostgresSnapshotStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Postgres snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="configureOptions">Optional action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		string connectionString,
		Action<PostgresSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Configure options
		var builder = services.AddOptions<PostgresSnapshotStoreOptions>();
		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register snapshot store with connection string
		services.TryAddScoped<ISnapshotStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresSnapshotStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresSnapshotStore>>();

			return new PostgresSnapshotStore(connectionString, options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the Postgres snapshot store to the service collection with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory function that creates Postgres connections.</param>
	/// <param name="configureOptions">Optional action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like multi-database setups,
	/// custom connection pooling, or integration with IDb abstraction.
	/// </remarks>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		Func<IServiceProvider, NpgsqlConnection> connectionFactory,
		Action<PostgresSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Configure options
		var builder = services.AddOptions<PostgresSnapshotStoreOptions>();
		if (configureOptions != null)
		{
			_ = builder.Configure(configureOptions);
		}

		_ = builder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register snapshot store with connection factory
		services.TryAddScoped<ISnapshotStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresSnapshotStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresSnapshotStore>>();

			return new PostgresSnapshotStore(
				() => connectionFactory(sp),
				options,
				logger);
		});

		return services;
	}
}
