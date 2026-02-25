// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Extension methods for configuring SQL Server CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the CDC processor to use SQL Server.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="configure">Optional action to configure SQL Server-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="connectionString"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring SQL Server as the CDC provider.
	/// It registers the <see cref="CdcProcessor"/>, <see cref="CdcStateStore"/>,
	/// and related services.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(connectionString, sql =&gt;
	///     {
	///         sql.SchemaName("cdc")
	///            .StateTableName("CdcProcessingState")
	///            .PollingInterval(TimeSpan.FromSeconds(5))
	///            .BatchSize(100);
	///     })
	///     .TrackTable("dbo.Orders", table =&gt;
	///     {
	///         table.MapInsert&lt;OrderCreatedEvent&gt;()
	///              .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///              .MapDelete&lt;OrderDeletedEvent&gt;();
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServer(
		this ICdcBuilder builder,
		string connectionString,
		Action<ISqlServerCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionString);

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("Connection string cannot be empty or whitespace.", nameof(connectionString));
		}

		// Create and configure SQL Server options
		var sqlOptions = new SqlServerCdcOptions { ConnectionString = connectionString };

		if (configure is not null)
		{
			var sqlBuilder = new SqlServerCdcBuilder(sqlOptions);
			configure(sqlBuilder);
		}

		// Validate options
		sqlOptions.Validate();

		// Register SQL Server CDC options
		_ = builder.Services.Configure<SqlServerCdcOptions>(opt =>
		{
			opt.SchemaName = sqlOptions.SchemaName;
			opt.StateTableName = sqlOptions.StateTableName;
			opt.PollingInterval = sqlOptions.PollingInterval;
			opt.BatchSize = sqlOptions.BatchSize;
			opt.CommandTimeout = sqlOptions.CommandTimeout;
			opt.ConnectionString = sqlOptions.ConnectionString;
		});

		// Register CDC state store options (existing type for backward compatibility)
		_ = builder.Services.Configure<SqlServerCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = sqlOptions.SchemaName;
			opt.TableName = sqlOptions.StateTableName;
		});

		// Delegate to the connection factory overload — connections are created
		// at resolution time from IOptions<SqlServerCdcOptions>.ConnectionString,
		// keeping connection creation deferred and consistent with the factory overload.
		RegisterCdcServices(builder, sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerCdcOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		});

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use SQL Server with a connection factory.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionFactory">A factory function that creates SQL connections.</param>
	/// <param name="configure">Optional action to configure SQL Server-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="connectionFactory"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload when you need custom connection management, such as
	/// using dependency injection for connection pooling or custom connection strings.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sp =&gt; () =&gt; new SqlConnection(connectionString), sql =&gt;
	///     {
	///         sql.SchemaName("audit")
	///            .BatchSize(200);
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServer(
		this ICdcBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		Action<ISqlServerCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Create and configure SQL Server options
		var sqlOptions = new SqlServerCdcOptions();

		if (configure is not null)
		{
			var sqlBuilder = new SqlServerCdcBuilder(sqlOptions);
			configure(sqlBuilder);
		}

		// Validate options (connection string not required for factory overload)
		if (string.IsNullOrWhiteSpace(sqlOptions.SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(sqlOptions.StateTableName))
		{
			throw new InvalidOperationException("StateTableName is required.");
		}

		// Register SQL Server CDC options
		_ = builder.Services.Configure<SqlServerCdcOptions>(opt =>
		{
			opt.SchemaName = sqlOptions.SchemaName;
			opt.StateTableName = sqlOptions.StateTableName;
			opt.PollingInterval = sqlOptions.PollingInterval;
			opt.BatchSize = sqlOptions.BatchSize;
			opt.CommandTimeout = sqlOptions.CommandTimeout;
		});

		// Register CDC state store options
		_ = builder.Services.Configure<SqlServerCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = sqlOptions.SchemaName;
			opt.TableName = sqlOptions.StateTableName;
		});

		RegisterCdcServices(builder, connectionFactory);

		return builder;
	}

	private static void RegisterCdcServices(
		ICdcBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
	{
		// Register default CdcRecoveryOptions if not already registered
		builder.Services.TryAddSingleton(Options.Create(new CdcRecoveryOptions()));

		// Register SQL Server CDC state store with factory
		builder.Services.TryAddSingleton<ICdcStateStore>(sp =>
		{
			var factory = connectionFactory(sp);
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			return new CdcStateStore(factory(), stateStoreOptions);
		});

		// Register CDC repository with factory (both core and LSN mapping interfaces)
		builder.Services.TryAddSingleton<ICdcRepository>(sp =>
		{
			var factory = connectionFactory(sp);
			return new CdcRepository(factory());
		});

		builder.Services.TryAddSingleton<ICdcRepositoryLsnMapping>(sp =>
		{
			var factory = connectionFactory(sp);
			return new CdcRepository(factory());
		});

		// Register SQL Server CDC processor with factory
		builder.Services.TryAddSingleton<ICdcProcessor>(sp =>
		{
			var factory = connectionFactory(sp);
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<CdcProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseConfig>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseConfig is required for CdcProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var cdcConnection = factory();
			var stateStoreConnection = factory();

			return new CdcProcessor(
				appLifetime,
				databaseConfig,
				cdcConnection,
				stateStoreConnection,
				stateStoreOptions,
				policyFactory,
				logger);
		});

		// Register DataChangeEventProcessor for background processing adapter
		builder.Services.TryAddSingleton<IDataChangeEventProcessor>(sp =>
		{
			var factory = connectionFactory(sp);
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<DataChangeEventProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseConfig>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseConfig is required for DataChangeEventProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var cdcConnection = factory();
			var stateStoreConnection = factory();

			return new DataChangeEventProcessor(
				appLifetime,
				databaseConfig,
				cdcConnection,
				stateStoreConnection,
				stateStoreOptions,
				sp,
				policyFactory,
				logger);
		});

		// Register ICdcBackgroundProcessor adapter for the hosted service
		builder.Services.TryAddSingleton<ICdcBackgroundProcessor>(sp =>
			new SqlServerCdcBackgroundProcessorAdapter(
				sp.GetRequiredService<IDataChangeEventProcessor>()));
	}
}
