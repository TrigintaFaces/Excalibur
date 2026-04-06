// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.Configuration;
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
	/// Adds Postgres event store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <param name="schema">The schema name for the event store table. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "events".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// Using NpgsqlDataSource is the recommended pattern per Npgsql documentation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource,
		string schema = "public",
		string table = "events")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton(sp =>
			new PostgresEventStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresEventStore>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres event store implementation with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for event store options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="PostgresEventStoreOptions.ConnectionString"/> is not configured.
	/// </exception>
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		Action<PostgresEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresEventStoreOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres event store. " +
				"Set PostgresEventStoreOptions.ConnectionString.");
		}

#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		return services.AddPostgresEventStore(dataSource, options.Schema, options.Table);
	}

	/// <summary>
	/// Adds Postgres event store implementation using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPostgresEventStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new PostgresEventStoreOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres event store. " +
				"Set PostgresEventStoreOptions.ConnectionString.");
		}

#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		return services.AddPostgresEventStore(dataSource, options.Schema, options.Table);
	}

	/// <summary>
	/// Adds Postgres snapshot store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "public".</param>
	/// <param name="table">The snapshot store table name. Default: "event_store_snapshots".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource,
		string schema = "public",
		string table = "event_store_snapshots")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton(sp =>
			new PostgresSnapshotStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresSnapshotStore>>(),
				schema,
				table));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres snapshot store implementation with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for snapshot store options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="PostgresSnapshotStoreOptions.ConnectionString"/> is not configured.
	/// </exception>
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		Action<PostgresSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresSnapshotStoreOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres snapshot store. " +
				"Set PostgresSnapshotStoreOptions.ConnectionString.");
		}

#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		return services.AddPostgresSnapshotStore(dataSource, options.Schema, options.Table);
	}

	/// <summary>
	/// Adds Postgres snapshot store implementation using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPostgresSnapshotStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new PostgresSnapshotStoreOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres snapshot store. " +
				"Set PostgresSnapshotStoreOptions.ConnectionString.");
		}

#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		return services.AddPostgresSnapshotStore(dataSource, options.Schema, options.Table);
	}

	/// <summary>
	/// Adds Postgres event-sourced outbox store implementation with an NpgsqlDataSource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="dataSource">The NpgsqlDataSource for creating connections.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "public".</param>
	/// <param name="table">The outbox table name. Default: "event_sourced_outbox".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddPostgresOutboxStore(
		this IServiceCollection services,
		NpgsqlDataSource dataSource,
		string schema = "public",
		string table = "event_sourced_outbox")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(dataSource);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new PostgresEventSourcedOutboxStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresEventSourcedOutboxStore>>(),
				schema,
				table));

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
	///     options.HealthChecks.RegisterHealthChecks = true;
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

		// Register NpgsqlDataSource as singleton (DI container manages lifecycle)
#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		_ = services.AddPostgresEventStore(dataSource, options.EventStoreSchema, options.EventStoreTable);
		_ = services.AddPostgresSnapshotStore(dataSource, options.SnapshotStoreSchema, options.SnapshotStoreTable);
		_ = services.AddPostgresOutboxStore(dataSource, options.OutboxSchema, options.OutboxTable);

		// Register health checks if enabled
		if (options.HealthChecks.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.OutboxStoreHealthCheckName,
					tags: ["outbox", "Postgres", "eventsourcing"]);
		}

		return services;
	}

	/// <summary>
	/// Adds all Postgres event sourcing implementations using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddPostgresEventSourcing(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new PostgresEventSourcingOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for Postgres event sourcing. " +
				"Set PostgresEventSourcingOptions:ConnectionString in configuration or use the NpgsqlDataSource overloads.");
		}

		_ = services.AddOptions<PostgresEventSourcingOptions>()
			.Bind(configuration);

		// Register NpgsqlDataSource as singleton (DI container manages lifecycle)
#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
#pragma warning restore CA2000
		services.TryAddSingleton(dataSource);
		_ = services.AddPostgresEventStore(dataSource, options.EventStoreSchema, options.EventStoreTable);
		_ = services.AddPostgresSnapshotStore(dataSource, options.SnapshotStoreSchema, options.SnapshotStoreTable);
		_ = services.AddPostgresOutboxStore(dataSource, options.OutboxSchema, options.OutboxTable);

		// Register health checks if enabled
		if (options.HealthChecks.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "Postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.OutboxStoreHealthCheckName,
					tags: ["outbox", "Postgres", "eventsourcing"]);
		}

		return services;
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

	private static void RegisterEventStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<IEventStore>("postgres", (sp, _) =>
			new TelemetryEventStore(
				sp.GetRequiredService<PostgresEventStore>(),
				new Meter(EventSourcingMeters.EventStore),
				new ActivitySource(EventSourcingActivitySources.EventStore),
				"Postgres"));
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("postgres"));
	}

	private static void RegisterSnapshotStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<ISnapshotStore>("postgres", (sp, _) =>
			new TelemetrySnapshotStore(
				sp.GetRequiredService<PostgresSnapshotStore>(),
				new Meter(EventSourcingMeters.SnapshotStore),
				new ActivitySource(EventSourcingActivitySources.SnapshotStore),
				"Postgres"));
		services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("postgres"));
	}
}
