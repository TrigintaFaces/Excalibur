// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Cdc.Processing;
using Excalibur.Data.SqlServer;

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
	/// <see cref="ISqlServerCdcConnectionBuilder.ConnectionString(string)"/>,
	/// <see cref="ISqlServerCdcConnectionBuilder.ConnectionStringName(string)"/>,
	/// <see cref="ISqlServerCdcConnectionBuilder.ConnectionFactory(Func{IServiceProvider, Func{SqlConnection}})"/>, or
	/// <see cref="ISqlServerCdcConnectionBuilder.BindConfiguration(string)"/>.
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
		Justification = "CDC DI registration method requires coordination across many types; this is the single composition root for SQL Server CDC.")]
	private static void RegisterCdcServices(
		ICdcBuilder builder,
		SqlServerCdcOptions sqlOptions,
		Func<IServiceProvider, Func<SqlConnection>> sourceConnectionFactory,
		Func<IServiceProvider, Func<SqlConnection>>? stateConnectionFactory)
	{
		// Register default CdcRecoveryOptions if not already registered
		builder.Services.TryAddSingleton(Options.Create(new CdcRecoveryOptions()));

		// TryAdd the SQL Server IDataAccessPolicyFactory. The CdcProcessor and
		// DataChangeEventProcessor both require it for Polly-wrapped SQL calls.
		// Consumers who register their own policy factory retain precedence via
		// TryAdd semantics. [bd-20ft0e FIX 1]
		builder.Services.TryAddSingleton<IDataAccessPolicyFactory, SqlDataAccessPolicyFactory>();

		// Auto-register IDatabaseOptions from SqlServerCdcOptions at resolution time.
		// Uses a factory so CaptureInstances derives from CdcOptions.TrackedTables
		// at resolution time — after IPostConfigureOptions (BindTrackedTables, etc.)
		// have merged config-driven tables. This makes TrackedTables the single source
		// of truth for which capture instances are polled.
		//
		// DatabaseName may come from either the fluent builder (.DatabaseName("X"))
		// or from BindConfiguration — both populate SqlServerCdcOptions.DatabaseName.
		// We resolve it at DI resolution time so config-bound values are available.
		// TryAdd ensures manual registration still takes precedence.
		{
			// Capture builder-time overrides (may be null if using BindConfiguration).
			var capturedBuilderDbName = sqlOptions.DatabaseName;
			var capturedBuilderDbConnId = sqlOptions.DatabaseConnectionIdentifier;
			var capturedBuilderStateConnId = sqlOptions.StateConnectionIdentifier;
			var capturedStopOnMissing = sqlOptions.StopOnMissingTableHandler;
			var capturedBuilderInstances = sqlOptions.CaptureInstances;
			var capturedBatchSize = sqlOptions.BatchSize;

			builder.Services.TryAddSingleton<IDatabaseOptions>(sp =>
			{
				var resolvedOptions = sp.GetRequiredService<IOptions<SqlServerCdcOptions>>().Value;

				// Prefer builder-set value, fall back to config-bound value.
				var dbName = capturedBuilderDbName ?? resolvedOptions.DatabaseName
					?? throw new InvalidOperationException(
						"DatabaseName is required for IDatabaseOptions. Set it via .DatabaseName() on the builder " +
						"or include it in the configuration section bound via .BindConfiguration().");

				var dbConnId = capturedBuilderDbConnId ?? $"cdc-{dbName}";
				var stateConnId = capturedBuilderStateConnId ?? $"state-{dbName}";

				var cdcOptions = sp.GetRequiredService<IOptions<CdcOptions>>().Value;
				var (captureInstances, captureInstanceToTableNameMap) = DeriveCaptureInstances(
					cdcOptions.TrackedTables, capturedBuilderInstances);

				var recoveryOptions = CdcRecoveryOptions.FromCdcOptions(cdcOptions);

				return new DatabaseOptions
				{
					DatabaseName = dbName,
					DatabaseConnectionIdentifier = dbConnId,
					StateConnectionIdentifier = stateConnId,
					CaptureInstances = captureInstances,
					CaptureInstanceToTableNameMap = captureInstanceToTableNameMap,
					StopOnMissingTableHandler = capturedStopOnMissing,
					ProducerBatchSize = capturedBatchSize,
					RecoveryOptions = recoveryOptions,
				};
			});
		}

		// Bridge SqlServerCdcOptions.PollingInterval → CdcProcessingOptions.PollingInterval
		// so the hosted service uses the interval configured via .PollingInterval() on the builder.
		var capturedCommandTimeoutSeconds = (int)sqlOptions.CommandTimeout.TotalSeconds;
		var capturedPollingInterval = sqlOptions.PollingInterval;
		_ = builder.Services.PostConfigure<Excalibur.Cdc.Processing.CdcProcessingOptions>(opt =>
		{
			opt.PollingInterval = capturedPollingInterval;
		});

		// Register SQL Server CDC state store with factory
		// Uses state factory when WithStateStore was called, source factory otherwise (backward compat)
		builder.Services.TryAddSingleton<ISqlServerCdcStateStore>(sp =>
		{
			var effectiveFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var factory = effectiveFactory(sp);
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			return new CdcStateStore(factory(), stateStoreOptions);
		});

		// Register a single CdcRepository instance shared by both ICdcRepository and ICdcRepositoryLsnMapping.
		// Previously each interface got its own instance, wasting a SQL connection.
		builder.Services.TryAddSingleton(sp =>
		{
			var factory = sourceConnectionFactory(sp);
			return new CdcRepository(factory(), capturedCommandTimeoutSeconds);
		});

		builder.Services.TryAddSingleton<ICdcRepository>(sp => sp.GetRequiredService<CdcRepository>());
		builder.Services.TryAddSingleton<ICdcRepositoryLsnMapping>(sp => sp.GetRequiredService<CdcRepository>());

		// Register SQL Server CDC processor with dual factories
		builder.Services.TryAddSingleton<ISqlServerCdcProcessor>(sp =>
		{
			var effectiveStateFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var stateFactory = effectiveStateFactory(sp);

			var cdcRepository = sp.GetRequiredService<CdcRepository>();
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<CdcProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseOptions>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseOptions is required for CdcProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var stateStoreConnection = stateFactory();

			var fatalErrorOptions = sp.GetService<IOptions<CdcFatalErrorOptions>>();

			return new CdcProcessor(
				appLifetime,
				databaseConfig,
				cdcRepository,
				stateStoreConnection,
				stateStoreOptions,
				policyFactory,
				logger,
				fatalErrorOptions);
		});

		// Register DataChangeEventProcessor for background processing adapter
		builder.Services.TryAddSingleton<IDataChangeEventProcessor>(sp =>
		{
			var effectiveStateFactory = stateConnectionFactory ?? sourceConnectionFactory;
			var stateFactory = effectiveStateFactory(sp);

			var cdcRepository = sp.GetRequiredService<CdcRepository>();
			var stateStoreOptions = sp.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();
			var appLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
			var policyFactory = sp.GetRequiredService<IDataAccessPolicyFactory>();
			var logger = sp.GetRequiredService<ILogger<DataChangeEventProcessor>>();

			var databaseConfig = sp.GetService<IDatabaseOptions>()
								 ?? throw new InvalidOperationException(
									 "IDatabaseOptions is required for DataChangeEventProcessor. Register an implementation or use the " +
									 "overload that provides database configuration.");

			var stateStoreConnection = stateFactory();

			var fatalErrorOptions = sp.GetService<IOptions<CdcFatalErrorOptions>>();

			return new DataChangeEventProcessor(
				appLifetime,
				databaseConfig,
				cdcRepository,
				stateStoreConnection,
				stateStoreOptions,
				sp,
				policyFactory,
				logger,
				fatalErrorOptions);
		});

		// Register ICdcBackgroundProcessor adapter for the hosted service
		builder.Services.TryAddSingleton<ICdcBackgroundProcessor>(sp =>
			new SqlServerCdcBackgroundProcessorAdapter(
				sp.GetRequiredService<IDataChangeEventProcessor>()));
	}

	/// <summary>
	/// Derives the SQL Server capture instance names from <see cref="CdcOptions.TrackedTables"/>,
	/// merging any legacy builder-configured instances.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="CdcOptions.TrackedTables"/> is the single source of truth for which tables
	/// are polled. This method converts each entry to a capture instance name:
	/// <list type="bullet">
	/// <item>If <see cref="CdcTableTrackingOptions.CaptureInstance"/> is set, use it (explicit override).</item>
	/// <item>Otherwise, use <see cref="CdcTableTrackingOptions.TableName"/> (the repository normalizes
	/// <c>dbo.Orders</c> → <c>dbo_Orders</c> via <c>NormalizeCaptureInstanceForSql</c>).</item>
	/// </list>
	/// </para>
	/// <para>
	/// Builder-configured <c>CaptureInstances</c> (from the fluent <c>.CaptureInstances("x")</c> API)
	/// are merged as a fallback for backward compatibility. Duplicates are skipped (case-insensitive).
	/// </para>
	/// </remarks>
	private static (string[] CaptureInstances, IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap) DeriveCaptureInstances(
		List<CdcTableTrackingOptions> trackedTables,
		string[]? builderInstances)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// TrackedTables is the primary source of truth.
		foreach (var table in trackedTables)
		{
			var instance = table.CaptureInstance ?? table.TableName;
			if (!string.IsNullOrEmpty(instance))
			{
				if (set.Add(instance))
				{
					// Map capture instance → logical table name.
					// When CaptureInstance is explicitly set, use the TableName as the logical name.
					// When CaptureInstance is null, the instance IS the table name (identity mapping).
					map[instance] = table.TableName;
				}
			}
		}

		// Builder-configured CaptureInstances as fallback (backward compat).
		if (builderInstances is { Length: > 0 })
		{
			foreach (var instance in builderInstances)
			{
				if (!string.IsNullOrEmpty(instance))
				{
					if (set.Add(instance))
					{
						// Legacy builder instances have no separate table name — identity mapping.
						map[instance] = instance;
					}
				}
			}
		}

		return ([.. set], map.AsReadOnly());
	}
}
