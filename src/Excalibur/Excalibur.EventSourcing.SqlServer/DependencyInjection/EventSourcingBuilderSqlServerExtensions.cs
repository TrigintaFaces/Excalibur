// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the canonical
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use SQL Server for event store
	/// and snapshot store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the SQL Server event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(configuration.GetConnectionString("EventStore")!)
	///            .EventStoreSchema("es")
	///            .SnapshotStoreSchema("es");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	///
	/// // Named connection string
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionStringName("EventStore");
	///     });
	/// });
	///
	/// // Connection factory (Azure Managed Identity)
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionFactory(sp =&gt;
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("EventStore")!;
	///             return () =&gt; new SqlConnection(connStr);
	///         });
	///     });
	/// });
	///
	/// // Bind from appsettings.json
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseSqlServer(sql =&gt;
	///     {
	///         sql.BindConfiguration("EventSourcing:SqlServer");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseSqlServer(
		this IEventSourcingBuilder builder,
		Action<ISqlServerEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options via builder
		var options = new SqlServerEventSourcingOptions();
		var sqlBuilder = new SqlServerEventSourcingBuilder(options);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		// Determine whether the builder configured a non-connection-string connection
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, sqlBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	/// <summary>
	/// Resolves the connection factory from the builder configuration.
	/// </summary>
	/// <remarks>
	/// Priority order (last-wins means only one is set, but resolution handles fallback):
	/// <list type="number">
	/// <item>Explicit <see cref="SqlServerEventSourcingBuilder.ConnectionFactoryFunc"/> (set via <c>ConnectionFactory()</c>)</item>
	/// <item><see cref="SqlServerEventSourcingBuilder.ConnectionStringNameValue"/> (resolved from IConfiguration at DI resolution)</item>
	/// <item><see cref="SqlServerEventSourcingBuilder.BindConfigurationPath"/> (resolved via options binding)</item>
	/// <item><see cref="SqlServerEventSourcingOptions.ConnectionString"/> (set via <c>ConnectionString()</c>)</item>
	/// <item>None set -- ValidateOnStart will catch this at startup</item>
	/// </list>
	/// </remarks>
	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerEventSourcingBuilder sqlBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.ConnectionFactoryFunc is not null)
		{
			return sqlBuilder.ConnectionFactoryFunc;
		}

		// 2. Named connection string resolved from IConfiguration
		if (sqlBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = sqlBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 3 & 4. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerEventSourcingOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	/// <summary>
	/// Registers options, services, and validation.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		SqlServerEventSourcingBuilder sqlBuilder,
		SqlServerEventSourcingOptions options,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<SqlServerEventSourcingOptions>(opt =>
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
		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerEventSourcingOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerEventSourcingOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart with connection awareness
		builder.Services.AddSingleton<IValidateOptions<SqlServerEventSourcingOptions>>(
			new SqlServerEventSourcingOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<SqlServerEventSourcingOptions>().ValidateOnStart();

		// Register stores using resolved connection factory
		RegisterEventStore(builder.Services, connectionFactory, options.EventStoreSchema, options.EventStoreTable);
		RegisterSnapshotStore(builder.Services, connectionFactory, options.SnapshotStoreSchema, options.SnapshotStoreTable);

		// Register health checks if enabled and connection string is available
		if (options.HealthChecks.RegisterHealthChecks && !string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.EventStoreHealthCheckName,
					tags: ["eventstore", "sqlserver", "eventsourcing"])
				.AddSqlServer(
					options.ConnectionString,
					name: options.HealthChecks.SnapshotStoreHealthCheckName,
					tags: ["snapshotstore", "sqlserver", "eventsourcing"]);
		}
	}

	private static void RegisterEventStore(
		IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		string schema,
		string table)
	{
		services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			return new SqlServerEventStore(
				factory,
				sp.GetRequiredService<ILogger<SqlServerEventStore>>(),
				sp.GetService<ISerializer>(),
				sp.GetService<IPayloadSerializer>(),
				schema,
				table);
		});

		SqlServerEventSourcingServiceCollectionExtensions.RegisterEventStoreTelemetryWrapper(services);
	}

	private static void RegisterSnapshotStore(
		IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		string schema,
		string table)
	{
		services.TryAddSingleton(sp =>
		{
			var factory = connectionFactory(sp);
			return new SqlServerSnapshotStore(
				factory,
				sp.GetRequiredService<ILogger<SqlServerSnapshotStore>>(),
				schema,
				table);
		});

		SqlServerEventSourcingServiceCollectionExtensions.RegisterSnapshotStoreTelemetryWrapper(services);
	}
}
