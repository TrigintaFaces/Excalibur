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
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server event sourcing services.
/// </summary>
public static class SqlServerEventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds SQL Server event store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups or custom connection pooling.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton(sp =>
			new SqlServerEventStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server event store implementation with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for event store options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="SqlServerEventStoreOptions.ConnectionString"/> is not configured.
	/// </exception>
	public static IServiceCollection AddSqlServerEventStore(
		this IServiceCollection services,
		Action<SqlServerEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerEventStoreOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server event store. " +
				"Set SqlServerEventStoreOptions.ConnectionString.");
		}

		return services.AddSqlServerEventStore(
			() => new SqlConnection(options.ConnectionString),
			options.Schema,
			options.Table);
	}

	/// <summary>
	/// Adds SQL Server event store implementation using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerEventStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new SqlServerEventStoreOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server event store. " +
				"Set SqlServerEventStoreOptions:ConnectionString in configuration.");
		}

		return services.AddSqlServerEventStore(
			() => new SqlConnection(options.ConnectionString),
			options.Schema,
			options.Table);
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerSnapshotStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton(sp =>
			new SqlServerSnapshotStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>(),
				schema,
				table));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation with options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for snapshot store options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="SqlServerSnapshotStoreOptions.ConnectionString"/> is not configured.
	/// </exception>
	public static IServiceCollection AddSqlServerSnapshotStore(
		this IServiceCollection services,
		Action<SqlServerSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerSnapshotStoreOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server snapshot store. " +
				"Set SqlServerSnapshotStoreOptions.ConnectionString.");
		}

		return services.AddSqlServerSnapshotStore(
			() => new SqlConnection(options.ConnectionString),
			options.Schema,
			options.Table);
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerSnapshotStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new SqlServerSnapshotStoreOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server snapshot store. " +
				"Set SqlServerSnapshotStoreOptions:ConnectionString in configuration.");
		}

		return services.AddSqlServerSnapshotStore(
			() => new SqlConnection(options.ConnectionString),
			options.Schema,
			options.Table);
	}

	/// <summary>
	/// Adds SQL Server event-sourced outbox store implementation with a connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating SQL connections.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "dbo".</param>
	/// <param name="table">The outbox table name. Default: "EventSourcedOutbox".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerOutboxStore(
		this IServiceCollection services,
		Func<SqlConnection> connectionFactory,
		string schema = "dbo",
		string table = "EventSourcedOutbox")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new SqlServerEventSourcedOutboxStore(
				connectionFactory,
				sp.GetRequiredService<ILogger<SqlServerEventSourcedOutboxStore>>(),
				schema,
				table));

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
	///     options.HealthChecks.RegisterHealthChecks = true;
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

		// Register subscription polling options validator for SQL injection prevention
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SubscriptionPollingOptions>, SubscriptionPollingOptionsValidator>());

		// Register stores using connection factory from resolved connection string
		Func<SqlConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);
		_ = services.AddSqlServerEventStore(connectionFactory, options.EventStoreSchema, options.EventStoreTable);
		_ = services.AddSqlServerSnapshotStore(connectionFactory, options.SnapshotStoreSchema, options.SnapshotStoreTable);
		_ = services.AddSqlServerOutboxStore(connectionFactory, options.OutboxSchema, options.OutboxTable);

		// Register health checks if enabled
		if (options.HealthChecks.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.OutboxStoreHealthCheckName,
					tags: ["outbox", "sqlserver", "eventsourcing"]);
		}

		return services;
	}

	/// <summary>
	/// Adds all SQL Server event sourcing implementations using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for method chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerEventSourcing(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new SqlServerEventSourcingOptions();
		configuration.Bind(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new InvalidOperationException(
				"ConnectionString must be configured for SQL Server event sourcing. " +
				"Set SqlServerEventSourcingOptions:ConnectionString in configuration or use the connection factory overloads.");
		}

		_ = services.AddOptions<SqlServerEventSourcingOptions>()
			.Bind(configuration);

		// Register subscription polling options validator for SQL injection prevention
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SubscriptionPollingOptions>, SubscriptionPollingOptionsValidator>());

		// Register stores using connection factory from resolved connection string
		Func<SqlConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);
		_ = services.AddSqlServerEventStore(connectionFactory, options.EventStoreSchema, options.EventStoreTable);
		_ = services.AddSqlServerSnapshotStore(connectionFactory, options.SnapshotStoreSchema, options.SnapshotStoreTable);
		_ = services.AddSqlServerOutboxStore(connectionFactory, options.OutboxSchema, options.OutboxTable);

		// Register health checks if enabled
		if (options.HealthChecks.RegisterHealthChecks)
		{
			_ = services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.OutboxStoreHealthCheckName,
					tags: ["outbox", "sqlserver", "eventsourcing"]);
		}

		return services;
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
		_ = services.AddSqlServerMigrator(
			() => new SqlConnection(options.ConnectionString),
			options.MigrationAssembly,
			options.MigrationNamespace);

		if (options.AutoMigrateOnStartup)
		{
			_ = services.AddHostedService<SqlServerMigrationHostedService>();
		}

		return services;
	}

	/// <summary>
	/// Adds SQL Server event store implementation using a typed <see cref="Excalibur.Data.Abstractions.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.Abstractions.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Resolves <typeparamref name="TDb"/> from DI and extracts its <see cref="System.Data.IDbConnection"/>
	/// as a <see cref="SqlConnection"/>. Eliminates the bridging ceremony:
	/// <c>sp =&gt; () =&gt; (SqlConnection)sp.GetRequiredService&lt;TDb&gt;().Connection</c>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventStore<TDb>(
		this IServiceCollection services,
		string schema = "dbo",
		string table = "EventStoreEvents")
		where TDb : class, Excalibur.Data.Abstractions.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(sp =>
			new SqlServerEventStore(
				() => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table));

		RegisterEventStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server snapshot store implementation using a typed <see cref="Excalibur.Data.Abstractions.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.Abstractions.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerSnapshotStore<TDb>(
		this IServiceCollection services,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
		where TDb : class, Excalibur.Data.Abstractions.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(sp =>
			new SqlServerSnapshotStore(
				() => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>(),
				schema,
				table));

		RegisterSnapshotStoreTelemetryWrapper(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server event-sourced outbox store implementation using a typed <see cref="Excalibur.Data.Abstractions.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.Abstractions.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "dbo".</param>
	/// <param name="table">The outbox table name. Default: "EventSourcedOutbox".</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddSqlServerOutboxStore<TDb>(
		this IServiceCollection services,
		string schema = "dbo",
		string table = "EventSourcedOutbox")
		where TDb : class, Excalibur.Data.Abstractions.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IEventSourcedOutboxStore>(sp =>
			new SqlServerEventSourcedOutboxStore(
				() => (SqlConnection)sp.GetRequiredService<TDb>().Connection,
				sp.GetRequiredService<ILogger<SqlServerEventSourcedOutboxStore>>(),
				schema,
				table));

		return services;
	}

	/// <summary>
	/// Adds all SQL Server event sourcing implementations using a typed <see cref="Excalibur.Data.Abstractions.IDb"/> marker for connection resolution.
	/// </summary>
	/// <typeparam name="TDb">The typed database marker that implements <see cref="Excalibur.Data.Abstractions.IDb"/>.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="eventStoreSchema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="eventStoreTable">The event store table name. Default: "EventStoreEvents".</param>
	/// <param name="snapshotStoreSchema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="snapshotStoreTable">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	/// <param name="outboxSchema">The schema name for the outbox table. Default: "dbo".</param>
	/// <param name="outboxTable">The outbox table name. Default: "EventSourcedOutbox".</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Convenience method that registers event store, snapshot store, and outbox store
	/// using the typed <typeparamref name="TDb"/> marker. Health checks are not registered
	/// as there is no connection string available.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerEventSourcing<TDb>(
		this IServiceCollection services,
		string eventStoreSchema = "dbo",
		string eventStoreTable = "EventStoreEvents",
		string snapshotStoreSchema = "dbo",
		string snapshotStoreTable = "EventStoreSnapshots",
		string outboxSchema = "dbo",
		string outboxTable = "EventSourcedOutbox")
		where TDb : class, Excalibur.Data.Abstractions.IDb
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSqlServerEventStore<TDb>(eventStoreSchema, eventStoreTable);
		_ = services.AddSqlServerSnapshotStore<TDb>(snapshotStoreSchema, snapshotStoreTable);
		_ = services.AddSqlServerOutboxStore<TDb>(outboxSchema, outboxTable);

		return services;
	}

	private static void RegisterEventStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<IEventStore>("sqlserver", (sp, _) =>
		{
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(EventSourcingMeters.EventStore) ?? new Meter(EventSourcingMeters.EventStore);
			return new TelemetryEventStore(
				sp.GetRequiredService<SqlServerEventStore>(),
				meter,
				new ActivitySource(EventSourcingActivitySources.EventStore),
				"sqlserver");
		});
		services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("sqlserver"));
	}

	private static void RegisterSnapshotStoreTelemetryWrapper(IServiceCollection services)
	{
		services.AddKeyedSingleton<ISnapshotStore>("sqlserver", (sp, _) =>
		{
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(EventSourcingMeters.SnapshotStore) ?? new Meter(EventSourcingMeters.SnapshotStore);
			return new TelemetrySnapshotStore(
				sp.GetRequiredService<SqlServerSnapshotStore>(),
				meter,
				new ActivitySource(EventSourcingActivitySources.SnapshotStore),
				"sqlserver");
		});
		services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("sqlserver"));
	}
}
