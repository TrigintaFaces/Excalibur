// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

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

		var sqlBuilder = new SqlServerCdcBuilder(sqlOptions);
		configure?.Invoke(sqlBuilder);

		// Validate options
		sqlOptions.Validate();

		// Apply state store configure callback if present
		var stateStoreOptions = new SqlServerCdcStateStoreOptions
		{
			SchemaName = sqlOptions.SchemaName,
			TableName = sqlOptions.StateTableName
		};

		string? stateStoreBindConfigPath = null;
		if (sqlBuilder.StateStoreConfigure is not null)
		{
			var stateBuilder = new SqlServerCdcStateStoreBuilder(stateStoreOptions);
			sqlBuilder.StateStoreConfigure(stateBuilder);
			stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
		}

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

		// Register source BindConfiguration if set
		if (sqlBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerCdcOptions>()
				.BindConfiguration(sqlBuilder.SourceBindConfigurationPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Register CDC state store options
		_ = builder.Services.Configure<SqlServerCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		// Register state store BindConfiguration if set
		if (stateStoreBindConfigPath is not null)
		{
			builder.Services.AddOptions<SqlServerCdcStateStoreOptions>()
				.BindConfiguration(stateStoreBindConfigPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Source connection factory. If ConnectionStringName was set, resolve from IConfiguration.
		Func<IServiceProvider, Func<SqlConnection>> sourceFactory;
		if (sqlBuilder.SourceConnectionStringName is not null)
		{
			var connStrName = sqlBuilder.SourceConnectionStringName;
			sourceFactory = sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it is defined in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}
		else
		{
			sourceFactory = sp =>
			{
				var opts = sp.GetRequiredService<IOptions<SqlServerCdcOptions>>();
				return () => new SqlConnection(opts.Value.ConnectionString);
			};
		}

		// State factory: use separate factory if WithStateStore was called, else fall back to source
		var stateFactory = sqlBuilder.StateConnectionFactory;

		RegisterCdcServices(builder, sqlOptions, sourceFactory, stateFactory);

		// Register post-configure callback for auto-mapping handler registration
		RegisterAutoMappingCallback(builder);

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

		var sqlBuilder = new SqlServerCdcBuilder(sqlOptions);
		configure?.Invoke(sqlBuilder);

		// Validate options (connection string not required for factory overload)
		if (string.IsNullOrWhiteSpace(sqlOptions.SchemaName))
		{
			throw new InvalidOperationException("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(sqlOptions.StateTableName))
		{
			throw new InvalidOperationException("StateTableName is required.");
		}

		// Apply state store configure callback if present
		var stateStoreOptions = new SqlServerCdcStateStoreOptions
		{
			SchemaName = sqlOptions.SchemaName,
			TableName = sqlOptions.StateTableName
		};

		string? stateStoreBindConfigPath = null;
		if (sqlBuilder.StateStoreConfigure is not null)
		{
			var stateBuilder = new SqlServerCdcStateStoreBuilder(stateStoreOptions);
			sqlBuilder.StateStoreConfigure(stateBuilder);
			stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
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

		// Register source BindConfiguration if set
		if (sqlBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerCdcOptions>()
				.BindConfiguration(sqlBuilder.SourceBindConfigurationPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Register CDC state store options
		_ = builder.Services.Configure<SqlServerCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		// Register state store BindConfiguration if set
		if (stateStoreBindConfigPath is not null)
		{
			builder.Services.AddOptions<SqlServerCdcStateStoreOptions>()
				.BindConfiguration(stateStoreBindConfigPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// State factory: use separate factory if WithStateStore was called, else fall back to source
		var stateFactory = sqlBuilder.StateConnectionFactory;

		RegisterCdcServices(builder, sqlOptions, connectionFactory, stateFactory);

		// Register post-configure callback for auto-mapping handler registration
		RegisterAutoMappingCallback(builder);

		return builder;
	}

	private static void RegisterAutoMappingCallback(ICdcBuilder builder)
	{
		if (builder is CdcBuilder cdcBuilder)
		{
			cdcBuilder.PostConfigureCallbacks.Add(static (services, options) =>
			{
				foreach (var table in options.TrackedTables)
				{
					if (!table.HasEventMappers)
					{
						continue;
					}

					// Capture table reference for the closure.
					// Use AddSingleton (not TryAddEnumerable) because factory-delegate
					// registrations have implType=interface, which TryAddEnumerable
					// treats as indistinguishable -- only one handler would register.
					// Each table gets its own unique handler instance.
					var capturedTable = table;
					services.AddSingleton<IDataChangeHandler>(sp =>
						new AutoMappingDataChangeHandler(
							sp,
							sp.GetRequiredService<Excalibur.Dispatch.Abstractions.IDispatcher>(),
							capturedTable,
							sp.GetRequiredService<ILogger<AutoMappingDataChangeHandler>>()));
				}
			});
		}
	}

	private static void RegisterCdcServices(
		ICdcBuilder builder,
		SqlServerCdcOptions sqlOptions,
		Func<IServiceProvider, Func<SqlConnection>> sourceConnectionFactory,
		Func<IServiceProvider, Func<SqlConnection>>? stateConnectionFactory)
	{
		// Register default CdcRecoveryOptions if not already registered
		builder.Services.TryAddSingleton(Options.Create(new CdcRecoveryOptions()));

		// Auto-register IDatabaseConfig when configured via the builder.
		// TryAdd ensures manual registration still takes precedence.
		if (sqlOptions.HasDatabaseConfig)
		{
			builder.Services.TryAddSingleton<IDatabaseConfig>(new DatabaseConfig
			{
				DatabaseName = sqlOptions.DatabaseName!,
				DatabaseConnectionIdentifier = sqlOptions.DatabaseConnectionIdentifier
					?? $"cdc-{sqlOptions.DatabaseName}",
				StateConnectionIdentifier = sqlOptions.StateConnectionIdentifier
					?? $"state-{sqlOptions.DatabaseName}",
				CaptureInstances = sqlOptions.CaptureInstances ?? [],
				StopOnMissingTableHandler = sqlOptions.StopOnMissingTableHandler
			});
		}

		// Register SQL Server CDC state store with factory
		// Uses state factory when WithStateStore was called, source factory otherwise (backward compat)
		builder.Services.TryAddSingleton<ICdcStateStore>(sp =>
		{
			var effectiveFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var factory = effectiveFactory(sp);
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			return new CdcStateStore(factory(), stateStoreOptions);
		});

		// Register CDC repository with source factory (always reads from source)
		builder.Services.TryAddSingleton<ICdcRepository>(sp =>
		{
			var factory = sourceConnectionFactory(sp);
			return new CdcRepository(factory());
		});

		builder.Services.TryAddSingleton<ICdcRepositoryLsnMapping>(sp =>
		{
			var factory = sourceConnectionFactory(sp);
			return new CdcRepository(factory());
		});

		// Register SQL Server CDC processor with dual factories
		builder.Services.TryAddSingleton<ICdcProcessor>(sp =>
		{
			var sourceFactory = sourceConnectionFactory(sp);
			var effectiveStateFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var stateFactory = effectiveStateFactory(sp);

			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<CdcProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseConfig>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseConfig is required for CdcProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var cdcConnection = sourceFactory();
			var stateStoreConnection = stateFactory();

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
			var sourceFactory = sourceConnectionFactory(sp);
			var effectiveStateFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var stateFactory = effectiveStateFactory(sp);

			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<DataChangeEventProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseConfig>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseConfig is required for DataChangeEventProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var cdcConnection = sourceFactory();
			var stateStoreConnection = stateFactory();

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
