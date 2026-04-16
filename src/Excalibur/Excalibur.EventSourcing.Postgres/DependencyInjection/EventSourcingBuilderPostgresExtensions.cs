// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Extension methods for configuring Postgres event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Postgres for event store
	/// and snapshot store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the Postgres event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionString(configuration.GetConnectionString("EventStore")!)
	///           .EventStoreSchema("public");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UsePostgres(
		this IEventSourcingBuilder builder,
		Action<IPostgresEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresEventSourcingOptions();
		var pgBuilder = new PostgresEventSourcingBuilder(options);
		configure(pgBuilder);

		var dataSourceFactory = ResolveDataSourceFactory(pgBuilder);
		var hasBuilderConnection = pgBuilder.DataSourceFactoryFunc is not null
			|| pgBuilder.DataSourceInstance is not null
			|| pgBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, pgBuilder, options, dataSourceFactory, hasBuilderConnection);

		return builder;
	}

	/// <summary>
	/// Resolves the <see cref="NpgsqlDataSource"/> factory from the builder configuration.
	/// All connection paths converge to NpgsqlDataSource for proper pooling.
	/// </summary>
	private static Func<IServiceProvider, NpgsqlDataSource> ResolveDataSourceFactory(
		PostgresEventSourcingBuilder pgBuilder)
	{
		// 1. Pre-configured DataSource instance
		if (pgBuilder.DataSourceInstance is not null)
		{
			var ds = pgBuilder.DataSourceInstance;
			return _ => ds;
		}

		// 2. Explicit DataSource factory
		if (pgBuilder.DataSourceFactoryFunc is not null)
		{
			return pgBuilder.DataSourceFactoryFunc;
		}

		// 3. Named connection string resolved from IConfiguration
		if (pgBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = pgBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return NpgsqlDataSource.Create(resolved);
			};
		}

		// 4 & 5. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<PostgresEventSourcingOptions>>();
			return NpgsqlDataSource.Create(opts.Value.ConnectionString!);
		};
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		PostgresEventSourcingBuilder pgBuilder,
		PostgresEventSourcingOptions options,
		Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<PostgresEventSourcingOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.EventStoreSchema = options.EventStoreSchema;
			opt.EventStoreTable = options.EventStoreTable;
			opt.SnapshotStoreSchema = options.SnapshotStoreSchema;
			opt.SnapshotStoreTable = options.SnapshotStoreTable;
			opt.OutboxSchema = options.OutboxSchema;
			opt.OutboxTable = options.OutboxTable;
			opt.HealthChecks = options.HealthChecks;
		});

		// Register BindConfiguration if set
		if (pgBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresEventSourcingOptions>()
				.BindConfiguration(pgBuilder.BindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<PostgresEventSourcingOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart with connection awareness
		builder.Services.AddSingleton<IValidateOptions<PostgresEventSourcingOptions>>(
			new PostgresEventSourcingOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<PostgresEventSourcingOptions>().ValidateOnStart();

		// Register DataSource as singleton for lifecycle management (shared by EventStore + SnapshotStore)
#pragma warning disable CA2000 // Dispose objects before losing scope -- managed by DI container
		builder.Services.TryAddSingleton(dataSourceFactory);
#pragma warning restore CA2000
		RegisterEventStore(builder.Services, options.EventStoreSchema, options.EventStoreTable);
		RegisterSnapshotStore(builder.Services, options.SnapshotStoreSchema, options.SnapshotStoreTable);

		// Register health checks if enabled and connection string is available
		if (options.HealthChecks.RegisterHealthChecks && !string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "postgres", "eventsourcing"])
				.AddNpgSql(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "postgres", "eventsourcing"]);
		}
	}

	private static void RegisterEventStore(
		IServiceCollection services,
		string schema,
		string table)
	{
		services.TryAddSingleton(sp =>
		{
			var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
			return new PostgresEventStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresEventStore>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table);
		});

		PostgresEventSourcingServiceCollectionExtensions.RegisterEventStoreTelemetryWrapper(services);
	}

	private static void RegisterSnapshotStore(
		IServiceCollection services,
		string schema,
		string table)
	{
		services.TryAddSingleton(sp =>
		{
			var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
			return new PostgresSnapshotStore(
				dataSource,
				sp.GetRequiredService<ILogger<PostgresSnapshotStore>>(),
				schema,
				table);
		});

		PostgresEventSourcingServiceCollectionExtensions.RegisterSnapshotStoreTelemetryWrapper(services);
	}
}
