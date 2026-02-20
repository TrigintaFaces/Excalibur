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
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server event sourcing services.
/// </summary>
public static class SqlServerEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds SQL Server event store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="SqlServerEventStore"/> as the <see cref="IEventStore"/> implementation.
	/// For configuration via options, use <see cref="AddSqlServerEventSourcing(IServiceCollection, Action{SqlServerEventSourcingOptions})"/>.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton(sp =>
			new SqlServerEventStore(
				connectionString,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				sp.GetService<IInternalSerializer>(),
				sp.GetService<IPayloadSerializer>()));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server event store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton(sp =>
			new SqlServerEventStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				sp.GetService<IInternalSerializer>(),
				sp.GetService<IPayloadSerializer>()));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="SqlServerSnapshotStore"/> as the <see cref="ISnapshotStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerSnapshotStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton(sp =>
			new SqlServerSnapshotStore(
				connectionString,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>()));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerSnapshotStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton(sp =>
			new SqlServerSnapshotStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>()));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server event-sourced outbox store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="SqlServerEventSourcedOutboxStore"/> as the <see cref="IEventSourcedOutboxStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new SqlServerEventSourcedOutboxStore(
				connectionString,
				sp.GetRequiredService<ILogger<SqlServerEventSourcedOutboxStore>>()));

		return services;
	}

	/// <summary>
	/// Adds SQL Server event-sourced outbox store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new SqlServerEventSourcedOutboxStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerEventSourcedOutboxStore>>()));

		return services;
	}

	/// <summary>
	/// Adds all SQL Server event sourcing implementations with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for SQL Server event sourcing options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended method for configuring SQL Server event sourcing.
	/// It registers all stores and optionally health checks based on the provided options.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddSqlServerEventSourcing(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("EventStore");
	///     options.RegisterHealthChecks = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventSourcing(
		this IServiceCollection services,
		Action<SqlServerEventSourcingOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerEventSourcingOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server event sourcing. " +
				"Set SqlServerEventSourcingOptions.ConnectionString or use the connection factory overloads.");
		}

		_ = services.Configure(configure);

		// Register stores
		_ = services.AddSqlServerEventStore(options.ConnectionString);
		_ = services.AddSqlServerSnapshotStore(options.ConnectionString);
		_ = services.AddSqlServerOutboxStore(options.ConnectionString);

		// Register health checks if enabled
		if (options.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: options.EventStoreHealthCheckName,
					tags: ["eventstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.OutboxStoreHealthCheckName,
					tags: ["outbox", "sqlserver", "eventsourcing"]);
		}

		return services;
	}

	/// <summary>
	/// Adds all SQL Server event sourcing implementations with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="registerHealthChecks">Whether to register health checks. Default: true.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Convenience method that registers event store, snapshot store, and outbox store
	/// with a single connection string.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventSourcing(
		this IServiceCollection services,
		string connectionString,
		bool registerHealthChecks = true)
	{
		return services.AddSqlServerEventSourcing(options =>
		{
			options.ConnectionString = connectionString;
			options.RegisterHealthChecks = registerHealthChecks;
		});
	}

	/// <summary>
	/// Adds all SQL Server event sourcing implementations with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// Health checks are not registered in this overload as there is no connection string available.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventSourcing(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		_ = services.AddSqlServerEventStore(connectionFactory);
		_ = services.AddSqlServerSnapshotStore(connectionFactory);
		_ = services.AddSqlServerOutboxStore(connectionFactory);

		return services;
	}

	/// <summary>
	/// Adds SQL Server materialized view store implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="SqlServerMaterializedViewStore"/> as the <see cref="IMaterializedViewStore"/> implementation.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerMaterializedViewStore(
		this IServiceCollection services,
		string connectionString,
		string? viewTableName = null,
		string? positionTableName = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionString);

		services.TryAddSingleton<IMaterializedViewStore>(sp =>
			new SqlServerMaterializedViewStore(
				connectionString,
				sp.GetRequiredService<ILogger<SqlServerMaterializedViewStore>>(),
				viewTableName,
				positionTableName));

		return services;
	}

	/// <summary>
	/// Adds SQL Server materialized view store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="viewTableName">Optional view table name. Defaults to "MaterializedViews".</param>
	/// <param name="positionTableName">Optional position table name. Defaults to "MaterializedViewPositions".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerMaterializedViewStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		string? viewTableName = null,
		string? positionTableName = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton<IMaterializedViewStore>(sp =>
			new SqlServerMaterializedViewStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerMaterializedViewStore>>(),
				viewTableName,
				positionTableName));

		return services;
	}

	/// <summary>
	/// Adds SQL Server schema migrator implementation.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="SqlServerMigrator"/> as the <see cref="IMigrator"/> implementation.
	/// Migration scripts should be embedded resources named following the pattern: YYYYMMDDHHMMSS_MigrationName.sql
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddSqlServerMigrator(
	///     connectionString,
	///     typeof(Program).Assembly,
	///     "MyApp.Migrations");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerMigrator(
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
			new SqlServerMigrator(
				connectionString,
				migrationAssembly,
				migrationNamespace,
				sp.GetRequiredService<ILogger<SqlServerMigrator>>()));

		return services;
	}

	/// <summary>
	/// Adds SQL Server schema migrator implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerMigrator(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		Assembly migrationAssembly,
		string migrationNamespace)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(migrationAssembly);
		ArgumentNullException.ThrowIfNull(migrationNamespace);

		services.TryAddSingleton<IMigrator>(sp =>
			new SqlServerMigrator(
				connectionFactory,
				migrationAssembly,
				migrationNamespace,
				sp.GetRequiredService<ILogger<SqlServerMigrator>>()));

		return services;
	}

	/// <summary>
	/// Adds SQL Server schema migrator with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for migrator options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddSqlServerMigrator(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("EventStore");
	///     options.MigrationAssembly = typeof(Program).Assembly;
	///     options.MigrationNamespace = "MyApp.Migrations";
	///     options.AutoMigrateOnStartup = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerMigrator(
		this IServiceCollection services,
		Action<SqlServerMigratorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerMigratorOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server migrator. " +
				"Set SqlServerMigratorOptions.ConnectionString.");
		}

		if (options.MigrationAssembly == null)
		{
			throw new InvalidOperationException(
				"MigrationAssembly must be configured for SQL Server migrator. " +
				"Set SqlServerMigratorOptions.MigrationAssembly to the assembly containing migration scripts.");
		}

		if (string.IsNullOrWhiteSpace(options.MigrationNamespace))
		{
			throw new InvalidOperationException(
				"MigrationNamespace must be configured for SQL Server migrator. " +
				"Set SqlServerMigratorOptions.MigrationNamespace to the namespace prefix for migration resources.");
		}

		_ = services.Configure(configure);
		_ = services.AddSqlServerMigrator(options.ConnectionString, options.MigrationAssembly, options.MigrationNamespace);

		if (options.AutoMigrateOnStartup)
		{
			_ = services.AddHostedService<SqlServerMigrationHostedService>();
		}

		return services;
	}

	private static void RegisterEventStoreTelemetryWrapper(IServiceCollection services)
	{
		services.TryAddSingleton<IEventStore>(sp =>
			new TelemetryEventStore(
				sp.GetRequiredService<SqlServerEventStore>(),
				new Meter(EventSourcingMeters.EventStore),
				new ActivitySource(EventSourcingActivitySources.EventStore),
				"sqlserver"));
	}

	private static void RegisterSnapshotStoreTelemetryWrapper(IServiceCollection services)
	{
		services.TryAddSingleton<ISnapshotStore>(sp =>
			new TelemetrySnapshotStore(
				sp.GetRequiredService<SqlServerSnapshotStore>(),
				new Meter(EventSourcingMeters.SnapshotStore),
				new ActivitySource(EventSourcingActivitySources.SnapshotStore),
				"sqlserver"));
	}
}
