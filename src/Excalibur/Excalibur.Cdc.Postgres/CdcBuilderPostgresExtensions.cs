// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Extension methods for configuring Postgres CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the CDC processor to use Postgres.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Action to configure Postgres-specific options including connection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Postgres as the CDC provider.
	/// It registers the <see cref="PostgresCdcProcessor"/> and <see cref="PostgresCdcStateStore"/>.
	/// Connection can be provided via the builder using
	/// <see cref="IPostgresCdcBuilder.ConnectionString"/>,
	/// <see cref="IPostgresCdcBuilder.ConnectionStringName"/>,
	/// <see cref="IPostgresCdcBuilder.ConnectionFactory"/>, or
	/// <see cref="IPostgresCdcBuilder.BindConfiguration"/>.
	/// </para>
	/// <para>
	/// Postgres CDC uses logical replication with the pgoutput protocol.
	/// Server requirements:
	/// <list type="bullet">
	/// <item><description>wal_level = logical</description></item>
	/// <item><description>A publication for the tables to capture</description></item>
	/// <item><description>A replication slot (created automatically if AutoCreateSlot is true)</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionString(connectionString)
	///           .SchemaName("excalibur")
	///           .ReplicationSlotName("my_cdc_slot")
	///           .PublicationName("my_publication")
	///           .PollingInterval(TimeSpan.FromSeconds(1))
	///           .BatchSize(1000);
	///     })
	///     .TrackTable("public.orders", table =&gt;
	///     {
	///         table.MapInsert&lt;OrderCreatedEvent&gt;()
	///              .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///              .MapDelete&lt;OrderDeletedEvent&gt;();
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	///
	/// // Named connection string
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionStringName("CdcDatabase")
	///           .ReplicationSlotName("my_slot")
	///           .PublicationName("my_pub");
	///     });
	/// });
	///
	/// // Connection factory
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UsePostgres(pg =&gt;
	///     {
	///         pg.ConnectionFactory(sp =&gt;
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("CdcDatabase")!;
	///             return () =&gt; new NpgsqlConnection(connStr);
	///         })
	///         .ReplicationSlotName("my_slot")
	///         .PublicationName("my_pub");
	///     });
	/// });
	///
	/// // From appsettings.json
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UsePostgres(pg =&gt;
	///     {
	///         pg.BindConfiguration("Cdc:Postgres")
	///           .ReplicationSlotName("my_slot")
	///           .PublicationName("my_pub");
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UsePostgres(
		this ICdcBuilder builder,
		Action<IPostgresCdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure Postgres options
		var pgOptions = new PostgresCdcOptions();
		var stateStoreOptions = new PostgresCdcStateStoreOptions();

		var pgBuilder = new PostgresCdcBuilder(pgOptions, stateStoreOptions);
		configure(pgBuilder);

		// Determine source connection factory
		var sourceFactory = ResolveSourceFactory(pgBuilder);

		// Validate options (connection string not required when using factory or ConnectionStringName)
		var hasExplicitFactory = pgBuilder.SourceConnectionFactory is not null
			|| pgBuilder.SourceConnectionStringName is not null;

		if (!hasExplicitFactory)
		{
			pgOptions.Validate();
		}

		RegisterOptionsAndServices(builder, pgBuilder, pgOptions, stateStoreOptions, sourceFactory);

		return builder;
	}

	/// <summary>
	/// Resolves the source connection factory from the builder configuration.
	/// </summary>
	/// <remarks>
	/// Priority order:
	/// 1. Explicit <see cref="PostgresCdcBuilder.SourceConnectionFactory"/> (set via <c>ConnectionFactory()</c>)
	/// 2. <see cref="PostgresCdcBuilder.SourceConnectionStringName"/> (resolved from IConfiguration at DI resolution)
	/// 3. <see cref="PostgresCdcOptions.ConnectionString"/> (set via <c>ConnectionString()</c> or <c>BindConfiguration()</c>)
	/// </remarks>
	private static Func<IServiceProvider, Func<NpgsqlConnection>> ResolveSourceFactory(
		PostgresCdcBuilder pgBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (pgBuilder.SourceConnectionFactory is not null)
		{
			return pgBuilder.SourceConnectionFactory;
		}

		// 2. Named connection string resolved from IConfiguration
		if (pgBuilder.SourceConnectionStringName is not null)
		{
			var connStrName = pgBuilder.SourceConnectionStringName;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it is defined in the ConnectionStrings section of your configuration.");
				return () => new NpgsqlConnection(resolved);
			};
		}

		// 3. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();
			return () => new NpgsqlConnection(opts.Value.ConnectionString);
		};
	}

	/// <summary>
	/// Resolves the state connection factory from the builder configuration.
	/// Returns <see langword="null"/> when no separate state store is configured (falls back to source).
	/// </summary>
	private static Func<IServiceProvider, Func<NpgsqlConnection>>? ResolveStateFactory(
		PostgresCdcBuilder pgBuilder,
		PostgresCdcStateStoreBuilder? stateBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (pgBuilder.StateConnectionFactoryFunc is not null)
		{
			return pgBuilder.StateConnectionFactoryFunc;
		}

		if (stateBuilder is null)
		{
			return null;
		}

		// 2. Connection string set directly on state store builder
		if (stateBuilder.StateConnectionString is not null)
		{
			var connStr = stateBuilder.StateConnectionString;
			return _ => () => new NpgsqlConnection(connStr);
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
				return () => new NpgsqlConnection(resolved);
			};
		}

		// 4. No separate state connection -- fall back to source
		return null;
	}

	/// <summary>
	/// Registers options, services, and the CDC processor/state store.
	/// </summary>
	private static void RegisterOptionsAndServices(
		ICdcBuilder builder,
		PostgresCdcBuilder pgBuilder,
		PostgresCdcOptions pgOptions,
		PostgresCdcStateStoreOptions stateStoreOptions,
		Func<IServiceProvider, Func<NpgsqlConnection>> sourceFactory)
	{
		// Apply state store configure callback if present
		string? stateStoreBindConfigPath = null;
		PostgresCdcStateStoreBuilder? stateBuilder = null;
		if (pgBuilder.StateStoreConfigure is not null)
		{
			stateBuilder = new PostgresCdcStateStoreBuilder(stateStoreOptions);
			pgBuilder.StateStoreConfigure(stateBuilder);
			stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
		}

		stateStoreOptions.Validate();

		// Register Postgres CDC options
		_ = builder.Services.Configure<PostgresCdcOptions>(opt =>
		{
			opt.ConnectionString = pgOptions.ConnectionString;
			opt.PublicationName = pgOptions.PublicationName;
			opt.ReplicationSlotName = pgOptions.ReplicationSlotName;
			opt.ProcessorId = pgOptions.ProcessorId;
			opt.PollingInterval = pgOptions.PollingInterval;
			opt.BatchSize = pgOptions.BatchSize;
			opt.Timeout = pgOptions.Timeout;
			opt.Replication.AutoCreateSlot = pgOptions.Replication.AutoCreateSlot;
			opt.Replication.UseBinaryProtocol = pgOptions.Replication.UseBinaryProtocol;
			opt.TableNames = pgOptions.TableNames;
			opt.RecoveryOptions = pgOptions.RecoveryOptions;
		});

		// Register source BindConfiguration if set
		if (pgBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresCdcOptions>()
				.BindConfiguration(pgBuilder.SourceBindConfigurationPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(pgOptions.ConnectionString))
			{
				var explicitConnectionString = pgOptions.ConnectionString;
				_ = builder.Services.PostConfigure<PostgresCdcOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register CDC state store options
		_ = builder.Services.Configure<PostgresCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		// Register state store BindConfiguration if set
		if (stateStoreBindConfigPath is not null)
		{
			builder.Services.AddOptions<PostgresCdcStateStoreOptions>()
				.BindConfiguration(stateStoreBindConfigPath)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		// Register default recovery options if not already registered
		builder.Services.TryAddSingleton(Options.Create(new PostgresCdcRecoveryOptions()));

		// State factory: resolve from state store builder, explicit factory, or fall back to source
		var stateFactory = ResolveStateFactory(pgBuilder, stateBuilder);

		// Register Postgres CDC state store with factory
		// Uses state factory when WithStateStore was called, source factory otherwise (backward compat)
		builder.Services.TryAddSingleton<IPostgresCdcStateStore>(sp =>
		{
			var effectiveFactory = stateFactory ?? sourceFactory;
			var factory = effectiveFactory(sp);
			var stateOptions = sp.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();

			using var connection = factory();
			return new PostgresCdcStateStore(connection.ConnectionString, stateOptions);
		});

		// Register Postgres CDC processor with factory
		builder.Services.TryAddSingleton<IPostgresCdcProcessor>(sp =>
		{
			var sourceFactoryResult = sourceFactory(sp);
			var stateStore = sp.GetRequiredService<IPostgresCdcStateStore>();
			var logger = sp.GetRequiredService<ILogger<PostgresCdcProcessor>>();

			// Get connection string from source factory connection for options
			using var connection = sourceFactoryResult();
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();

			// Update connection string in options
			var optionsValue = options.Value;
			optionsValue.ConnectionString = connection.ConnectionString;

			return new PostgresCdcProcessor(Options.Create(optionsValue), stateStore, logger);
		});

		return;
	}
}
