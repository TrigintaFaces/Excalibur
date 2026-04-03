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
	/// <param name="configure">Action to configure SQL Server-specific options including connection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring SQL Server as the CDC provider.
	/// It registers the <see cref="CdcProcessor"/>, <see cref="CdcStateStore"/>,
	/// and related services. Connection can be provided via the builder using
	/// <see cref="ISqlServerCdcBuilder.ConnectionString"/>,
	/// <see cref="ISqlServerCdcBuilder.ConnectionStringName"/>,
	/// <see cref="ISqlServerCdcBuilder.ConnectionFactory"/>, or
	/// <see cref="ISqlServerCdcBuilder.BindConfiguration"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(connectionString)
	///            .SchemaName("cdc")
	///            .DatabaseName("MyDb")
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
	///
	/// // From appsettings.json
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt;
	///     {
	///         sql.BindConfiguration("Cdc:SqlServer")
	///            .DatabaseName("MyDb");
	///     });
	/// });
	///
	/// // Named connection string
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionStringName("CdcDatabase")
	///            .DatabaseName("MyDb");
	///     });
	/// });
	///
	/// // Connection factory
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionFactory(sp =&gt;
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("CdcDatabase")!;
	///             return () =&gt; new SqlConnection(connStr);
	///         })
	///         .DatabaseName("MyDb");
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseSqlServer(
		this ICdcBuilder builder,
		Action<ISqlServerCdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options
		var sqlOptions = new SqlServerCdcOptions();
		var sqlBuilder = new SqlServerCdcBuilder(sqlOptions);
		configure(sqlBuilder);

		// Determine source connection factory
		var sourceFactory = ResolveSourceFactory(sqlBuilder);

		// Validate options (connection string not required when using factory or ConnectionStringName)
		var hasExplicitFactory = sqlBuilder.SourceConnectionFactory is not null
			|| sqlBuilder.SourceConnectionStringName is not null;

		if (!hasExplicitFactory)
		{
			sqlOptions.Validate();
		}
		else
		{
			// Still validate non-connection-string options
			if (string.IsNullOrWhiteSpace(sqlOptions.SchemaName))
			{
				throw new InvalidOperationException("SchemaName is required.");
			}

			if (string.IsNullOrWhiteSpace(sqlOptions.StateTableName))
			{
				throw new InvalidOperationException("StateTableName is required.");
			}
		}

		RegisterOptionsAndServices(builder, sqlBuilder, sqlOptions, sourceFactory);

		return builder;
	}

	/// <summary>
	/// Resolves the source connection factory from the builder configuration.
	/// </summary>
	/// <remarks>
	/// Priority order:
	/// 1. Explicit <see cref="SqlServerCdcBuilder.SourceConnectionFactory"/> (set via <c>ConnectionFactory()</c>)
	/// 2. <see cref="SqlServerCdcBuilder.SourceConnectionStringName"/> (resolved from IConfiguration at DI resolution)
	/// 3. <see cref="SqlServerCdcOptions.ConnectionString"/> (set via <c>ConnectionString()</c> or <c>BindConfiguration()</c>)
	/// </remarks>
	private static Func<IServiceProvider, Func<SqlConnection>> ResolveSourceFactory(
		SqlServerCdcBuilder sqlBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.SourceConnectionFactory is not null)
		{
			return sqlBuilder.SourceConnectionFactory;
		}

		// 2. Named connection string resolved from IConfiguration
		if (sqlBuilder.SourceConnectionStringName is not null)
		{
			var connStrName = sqlBuilder.SourceConnectionStringName;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it is defined in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 3. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerCdcOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	/// <summary>
	/// Resolves the state connection factory from the builder configuration.
	/// Returns <see langword="null"/> when no separate state store is configured (falls back to source).
	/// </summary>
	/// <remarks>
	/// Priority order:
	/// 1. Explicit <see cref="SqlServerCdcBuilder.StateConnectionFactoryFunc"/> (set via <c>StateConnectionFactory()</c>)
	/// 2. <see cref="SqlServerCdcStateStoreBuilder.StateConnectionString"/> (set via <c>state.ConnectionString()</c>)
	/// 3. <see cref="SqlServerCdcStateStoreBuilder.StateConnectionStringName"/> (resolved from IConfiguration at DI resolution)
	/// 4. <see langword="null"/> -- fall back to source connection
	/// </remarks>
	private static Func<IServiceProvider, Func<SqlConnection>>? ResolveStateFactory(
		SqlServerCdcBuilder sqlBuilder,
		SqlServerCdcStateStoreBuilder? stateBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.StateConnectionFactoryFunc is not null)
		{
			return sqlBuilder.StateConnectionFactoryFunc;
		}

		if (stateBuilder is null)
		{
			return null;
		}

		// 2. Connection string set directly on state store builder
		if (stateBuilder.StateConnectionString is not null)
		{
			var connStr = stateBuilder.StateConnectionString;
			return _ => () => new SqlConnection(connStr);
		}

		// 3. Named connection string resolved from IConfiguration
		if (stateBuilder.StateConnectionStringName is not null)
		{
			var connStrName = stateBuilder.StateConnectionStringName;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"State store connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it is defined in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 4. No separate state connection -- fall back to source
		return null;
	}

	/// <summary>
	/// Registers options, services, and auto-mapping callbacks.
	/// </summary>
	private static void RegisterOptionsAndServices(
		ICdcBuilder builder,
		SqlServerCdcBuilder sqlBuilder,
		SqlServerCdcOptions sqlOptions,
		Func<IServiceProvider, Func<SqlConnection>> sourceFactory)
	{
		// Apply state store configure callback if present
		var stateStoreOptions = new SqlServerCdcStateStoreOptions
		{
			SchemaName = sqlOptions.SchemaName,
			TableName = sqlOptions.StateTableName
		};

		string? stateStoreBindConfigPath = null;
		SqlServerCdcStateStoreBuilder? stateBuilder = null;
		if (sqlBuilder.StateStoreConfigure is not null)
		{
			stateBuilder = new SqlServerCdcStateStoreBuilder(stateStoreOptions);
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

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
			{
				var explicitConnectionString = sqlOptions.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerCdcOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
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

		// State factory: resolve from state store builder, explicit factory, or fall back to source
		var stateFactory = ResolveStateFactory(sqlBuilder, stateBuilder);

		RegisterCdcServices(builder, sqlOptions, sourceFactory, stateFactory);

		// Register post-configure callback for auto-mapping handler registration
		RegisterAutoMappingCallback(builder);
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

		// Auto-register IDatabaseOptions when configured via the builder.
		// TryAdd ensures manual registration still takes precedence.
		if (sqlOptions.HasDatabaseConfig)
		{
			builder.Services.TryAddSingleton<IDatabaseOptions>(new DatabaseOptions
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
		builder.Services.TryAddSingleton<ISqlServerCdcStateStore>(sp =>
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

			var databaseConfig = sp.GetService<IDatabaseOptions>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseOptions is required for CdcProcessor. Register an implementation or use the " +
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

			var databaseConfig = sp.GetService<IDatabaseOptions>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseOptions is required for DataChangeEventProcessor. Register an implementation or use the " +
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
