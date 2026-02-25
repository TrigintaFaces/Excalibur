// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres event sourcing services.
/// </summary>
public static class PostgresEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds Postgres event store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PostgresEventStore"/> as the <see cref="IEventStore"/> implementation.
	/// For configuration via options, use <see cref="AddPostgresEventSourcing(IServiceCollection, Action{PostgresEventSourcingOptions})"/>.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton(sp =>
			new PostgresEventStore(
				connectionString,
				sp.GetRequiredService<ILogger<PostgresEventStore>>(),
				sp.GetService<IInternalSerializer>(),
				sp.GetService<IPayloadSerializer>()));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres event store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// Using NpgsqlDataSource is the recommended pattern per Npgsql documentation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton(sp =>
			new PostgresEventStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresEventStore>>(),
				sp.GetService<IInternalSerializer>(),
				sp.GetService<IPayloadSerializer>()));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres snapshot store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PostgresSnapshotStore"/> as the <see cref="ISnapshotStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton(sp =>
			new PostgresSnapshotStore(
				connectionString,
				sp.GetRequiredService<ILogger<PostgresSnapshotStore>>()));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres snapshot store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton(sp =>
			new PostgresSnapshotStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresSnapshotStore>>()));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres event-sourced outbox store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PostgresEventSourcedOutboxStore"/> as the <see cref="IEventSourcedOutboxStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresOutboxStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new PostgresEventSourcedOutboxStore(
				connectionString,
				sp.GetRequiredService<ILogger<PostgresEventSourcedOutboxStore>>()));

		return services;
	}

	/// <summary>
	/// Adds Postgres event-sourced outbox store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresOutboxStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new PostgresEventSourcedOutboxStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresEventSourcedOutboxStore>>()));

		return services;
	}

	/// <summary>
	/// Adds all Postgres event sourcing implementations with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for Postgres event sourcing options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended method for configuring Postgres event sourcing.
	/// It registers all stores and optionally health checks based on the provided options.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddPostgresEventSourcing(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("EventStore");
	///     options.RegisterHealthChecks = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventSourcing(
		this IServiceCollection services,
		Action<PostgresEventSourcingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresEventSourcingOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres event sourcing. " +
				"Set PostgresEventSourcingOptions.ConnectionString or use the NpgsqlDataSource overloads.");
		}

		_ = services.Configure(configure);

		// Register stores
		_ = services.AddPostgresEventStore(options.ConnectionString);
		_ = services.AddPostgresSnapshotStore(options.ConnectionString);
		_ = services.AddPostgresOutboxStore(options.ConnectionString);

		// Register health checks if enabled
		if (options.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddNpgSql(
					options.ConnectionString,
					name: options.EventStoreHealthCheckName,
					tags: ["eventstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.OutboxStoreHealthCheckName,
					tags: ["outbox", "Postgres", "eventsourcing"]);
		}

		return services;
	}

	/// <summary>
	/// Adds all Postgres event sourcing implementations with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="registerHealthChecks">Whether to register health checks. Default: true.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Convenience method that registers event store, snapshot store, and outbox store
	/// with a single connection string.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventSourcing(
		this IServiceCollection services,
		string connectionString,
		bool registerHealthChecks = true)
	{
		return services.AddPostgresEventSourcing(options =>
		{
			options.ConnectionString = connectionString;
			options.RegisterHealthChecks = registerHealthChecks;
		});
	}

	/// <summary>
	/// Adds all Postgres event sourcing implementations with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// Health checks are not registered in this overload as there is no connection string available.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventSourcing(
		this IServiceCollection services,
		NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		_ = services.AddPostgresEventStore(dataSource);
		_ = services.AddPostgresSnapshotStore(dataSource);
		_ = services.AddPostgresOutboxStore(dataSource);

		return services;
	}

	/// <summary>
	/// Alias for <see cref="AddPostgresEventSourcing(IServiceCollection, string, bool)"/> to match template usage.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method exists for template compatibility where <c>es.UsePostgres(connectionString)</c> is expected.
	/// </para>
	/// </remarks>
	public static IServiceCollection UsePostgres(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddPostgresEventSourcing(connectionString);
	}

	/// <summary>
	/// Adds Postgres materialized view store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "materialized_views".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "materialized_view_positions".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PostgresMaterializedViewStore"/> as the <see cref="IMaterializedViewStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresMaterializedViewStore(
		this IServiceCollection services,
		string connectionString,
		string? viewTableName = null,
		string? positionTableName = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton<IMaterializedViewStore>(sp =>
			new PostgresMaterializedViewStore(
				connectionString,
				sp.GetRequiredService<ILogger<PostgresMaterializedViewStore>>(),
				viewTableName,
				positionTableName));

		return services;
	}

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
	/// Adds Postgres schema migrator implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="PostgresMigrator"/> as the <see cref="IMigrator"/> implementation.
	/// Migration scripts should be embedded resources named following the pattern: YYYYMMDDHHMMSS_MigrationName.sql
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddPostgresMigrator(
	///     connectionString,
	///     typeof(Program).Assembly,
	///     "MyApp.Migrations");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresMigrator(
		this IServiceCollection services,
		string connectionString,
		Assembly migrationAssembly,
		string migrationNamespace)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);
		ArgumentNullException.ThrowIfNull(migrationAssembly);
		ArgumentNullException.ThrowIfNull(migrationNamespace);

		services.TryAddSingleton<IMigrator>(sp =>
			new PostgresMigrator(
				connectionString,
				migrationAssembly,
				migrationNamespace,
				sp.GetRequiredService<ILogger<PostgresMigrator>>()));

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
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// </para>
	/// </remarks>
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
		_ = services.AddPostgresMigrator(options.ConnectionString, options.MigrationAssembly, options.MigrationNamespace);

		if (options.AutoMigrateOnStartup)
		{
			_ = services.AddHostedService<PostgresMigrationHostedService>();
		}

		return services;
	}

	private static void RegisterEventStoreTelemetryWrapper(IServiceCollection services)
	{
		services.TryAddSingleton<IEventStore>(sp =>
			new TelemetryEventStore(
				sp.GetRequiredService<PostgresEventStore>(),
				new Meter(EventSourcingMeters.EventStore),
				new ActivitySource(EventSourcingActivitySources.EventStore),
				"Postgres"));
	}

	private static void RegisterSnapshotStoreTelemetryWrapper(IServiceCollection services)
	{
		services.TryAddSingleton<ISnapshotStore>(sp =>
			new TelemetrySnapshotStore(
				sp.GetRequiredService<PostgresSnapshotStore>(),
				new Meter(EventSourcingMeters.SnapshotStore),
				new ActivitySource(EventSourcingActivitySources.SnapshotStore),
				"Postgres"));
	}
}
