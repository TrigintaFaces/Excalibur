// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
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

	private static Func<IServiceProvider, Func<NpgsqlConnection>> ResolveSourceFactory(
		PostgresCdcBuilder pgBuilder)
	{
		if (pgBuilder.SourceConnectionFactory is not null)
		{
			return pgBuilder.SourceConnectionFactory;
		}

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

		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();
			return () => new NpgsqlConnection(opts.Value.ConnectionString);
		};
	}

	private static Func<IServiceProvider, Func<NpgsqlConnection>>? ResolveStateFactory(
		PostgresCdcBuilder pgBuilder,
		PostgresCdcStateStoreBuilder? stateBuilder)
	{
		if (pgBuilder.StateConnectionFactoryFunc is not null)
		{
			return pgBuilder.StateConnectionFactoryFunc;
		}

		if (stateBuilder is null)
		{
			return null;
		}

		if (stateBuilder.StateConnectionString is not null)
		{
			var connStr = stateBuilder.StateConnectionString;
			return _ => () => new NpgsqlConnection(connStr);
		}

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

		return null;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	private static void RegisterOptionsAndServices(
		ICdcBuilder builder,
		PostgresCdcBuilder pgBuilder,
		PostgresCdcOptions pgOptions,
		PostgresCdcStateStoreOptions stateStoreOptions,
		Func<IServiceProvider, Func<NpgsqlConnection>> sourceFactory)
	{
		string? stateStoreBindConfigPath = null;
		PostgresCdcStateStoreBuilder? stateBuilder = null;
		if (pgBuilder.StateStoreConfigure is not null)
		{
			stateBuilder = new PostgresCdcStateStoreBuilder(stateStoreOptions);
			pgBuilder.StateStoreConfigure(stateBuilder);
			stateStoreBindConfigPath = stateBuilder.BindConfigurationPath;
		}

		stateStoreOptions.Validate();

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

		if (pgBuilder.SourceBindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresCdcOptions>()
				.BindConfiguration(pgBuilder.SourceBindConfigurationPath)
				.ValidateOnStart();

			if (!string.IsNullOrWhiteSpace(pgOptions.ConnectionString))
			{
				var explicitConnectionString = pgOptions.ConnectionString;
				_ = builder.Services.PostConfigure<PostgresCdcOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		_ = builder.Services.Configure<PostgresCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		if (stateStoreBindConfigPath is not null)
		{
			builder.Services.AddOptions<PostgresCdcStateStoreOptions>()
				.BindConfiguration(stateStoreBindConfigPath)
				.ValidateOnStart();
		}

		builder.Services.TryAddSingleton(Options.Create(new PostgresCdcRecoveryOptions()));

		var stateFactory = ResolveStateFactory(pgBuilder, stateBuilder);

		builder.Services.TryAddSingleton<IPostgresCdcStateStore>(sp =>
		{
			var effectiveFactory = stateFactory ?? sourceFactory;
			var factory = effectiveFactory(sp);
			var stateOptions = sp.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();

			using var connection = factory();
			return new PostgresCdcStateStore(connection.ConnectionString, stateOptions);
		});

		builder.Services.TryAddSingleton<IPostgresCdcProcessor>(sp =>
		{
			var sourceFactoryResult = sourceFactory(sp);
			var stateStore = sp.GetRequiredService<IPostgresCdcStateStore>();
			var logger = sp.GetRequiredService<ILogger<PostgresCdcProcessor>>();

			using var connection = sourceFactoryResult();
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();

			var optionsValue = options.Value;
			optionsValue.ConnectionString = connection.ConnectionString;

			return new PostgresCdcProcessor(Options.Create(optionsValue), stateStore, logger);
		});

		return;
	}
}
