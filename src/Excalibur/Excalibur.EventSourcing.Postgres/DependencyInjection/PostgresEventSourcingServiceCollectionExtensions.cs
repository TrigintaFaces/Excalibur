// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres event sourcing services.
/// </summary>
/// <remarks>
/// <para>
/// <b>For event store and snapshot store registration, use the canonical builder pattern:</b>
/// <code>
/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
/// {
///     es.UsePostgres(pg =&gt; pg.ConnectionString("Host=localhost;Database=MyApp;"))
///       .AddRepository&lt;OrderAggregate, Guid&gt;();
/// }));
/// </code>
/// </para>
/// <para>
/// This class retains materialized view store and migrator registration methods
/// that are not yet covered by the builder pattern.
/// </para>
/// </remarks>
public static class PostgresEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds Postgres materialized view store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresMaterializedViewStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource,
		string? viewTableName = null,
		string? positionTableName = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton<IMaterializedViewStore>(sp =>
			new PostgresMaterializedViewStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresMaterializedViewStore>>(),
				viewTableName,
				positionTableName));

		return services;
	}

	/// <summary>
	/// Adds Postgres schema migrator implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresMigrator(
		this IServiceCollection services,
		NpgsqlDataSource dataSource,
		Assembly migrationAssembly,
		string migrationNamespace)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(migrationAssembly);
		ArgumentNullException.ThrowIfNull(migrationNamespace);

		services.TryAddSingleton<IMigrator>(sp =>
			new PostgresMigrator(
				dataSource,
				migrationAssembly,
				migrationNamespace,
				sp.GetRequiredService<ILogger<PostgresMigrator>>()));

		return services;
	}

	/// <summary>
	/// Adds Postgres schema migrator with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for migrator options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddPostgresMigrator(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("EventStore");
	///     options.MigrationAssembly = typeof(Program).Assembly;
	///     options.MigrationNamespace = "MyApp.Migrations";
	///     options.AutoMigrateOnStartup = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresMigrator(
		this IServiceCollection services,
		Action<PostgresMigratorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresMigratorOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres migrator. " +
				"Set PostgresMigratorOptions.ConnectionString.");
		}

		if (options.MigrationAssembly == null)
		{
			throw new InvalidOperationException(
				"MigrationAssembly must be configured for Postgres migrator. " +
				"Set PostgresMigratorOptions.MigrationAssembly to the assembly containing migration scripts.");
		}

		if (string.IsNullOrWhiteSpace(options.MigrationNamespace))
		{
			throw new InvalidOperationException(
				"MigrationNamespace must be configured for Postgres migrator. " +
				"Set PostgresMigratorOptions.MigrationNamespace to the namespace prefix for migration resources.");
		}

		_ = services.Configure(configure);
#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var migratorDataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(migratorDataSource);
		_ = services.AddPostgresMigrator(
			migratorDataSource,
			options.MigrationAssembly,
			options.MigrationNamespace);

		if (options.AutoMigrateOnStartup)
		{
			_ = services.AddHostedService<PostgresMigrationHostedService>();
		}

		return services;
	}

	internal static void RegisterEventStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<IEventStore>("postgres", (sp, _) =>
		{
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(EventSourcingMeters.EventStore) ?? new Meter(EventSourcingMeters.EventStore);
			return new TelemetryEventStore(
				sp.GetRequiredService<PostgresEventStore>(),
				meter,
				new ActivitySource(EventSourcingActivitySources.EventStore),
				"Postgres");
		});
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("postgres"));
	}

	internal static void RegisterSnapshotStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<ISnapshotStore>("postgres", (sp, _) =>
		{
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(EventSourcingMeters.SnapshotStore) ?? new Meter(EventSourcingMeters.SnapshotStore);
			return new TelemetrySnapshotStore(
				sp.GetRequiredService<PostgresSnapshotStore>(),
				meter,
				new ActivitySource(EventSourcingActivitySources.SnapshotStore),
				"Postgres");
		});
		services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("postgres"));
	}
}
