// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Cdc;

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
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="configure">Optional action to configure Postgres-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="connectionString"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Postgres as the CDC provider.
	/// It registers the <see cref="PostgresCdcProcessor"/> and <see cref="PostgresCdcStateStore"/>.
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
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UsePostgres(connectionString, pg =&gt;
	///     {
	///         pg.SchemaName("excalibur")
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
	/// </code>
	/// </example>
	public static ICdcBuilder UsePostgres(
		this ICdcBuilder builder,
		string connectionString,
		Action<IPostgresCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionString);

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new ArgumentException("Connection string cannot be empty or whitespace.", nameof(connectionString));
		}

		// Create and configure Postgres options
		var pgOptions = new PostgresCdcOptions { ConnectionString = connectionString };

		var stateStoreOptions = new PostgresCdcStateStoreOptions();

		if (configure is not null)
		{
			var pgBuilder = new PostgresCdcBuilder(pgOptions, stateStoreOptions);
			configure(pgBuilder);
		}

		// Validate options
		pgOptions.Validate();
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
			opt.AutoCreateSlot = pgOptions.AutoCreateSlot;
			opt.UseBinaryProtocol = pgOptions.UseBinaryProtocol;
			opt.TableNames = pgOptions.TableNames;
			opt.RecoveryOptions = pgOptions.RecoveryOptions;
		});

		// Register CDC state store options
		_ = builder.Services.Configure<PostgresCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		// Register default recovery options if not already registered
		builder.Services.TryAddSingleton(Options.Create(new PostgresCdcRecoveryOptions()));

		// Register Postgres CDC state store
		builder.Services.TryAddSingleton<IPostgresCdcStateStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();
			var stateOptions = sp.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
			return new PostgresCdcStateStore(options.Value.ConnectionString, stateOptions);
		});

		// Register Postgres CDC processor
		builder.Services.TryAddSingleton<IPostgresCdcProcessor>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();
			var stateStore = sp.GetRequiredService<IPostgresCdcStateStore>();
			var logger = sp.GetRequiredService<ILogger<PostgresCdcProcessor>>();
			return new PostgresCdcProcessor(options, stateStore, logger);
		});

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use Postgres with a connection factory.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="connectionFactory">A factory function that creates Postgres connections.</param>
	/// <param name="configure">Optional action to configure Postgres-specific options.</param>
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
	///     cdc.UsePostgres(sp =&gt; () =&gt; new NpgsqlConnection(connectionString), pg =&gt;
	///     {
	///         pg.SchemaName("audit")
	///           .BatchSize(200);
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UsePostgres(
		this ICdcBuilder builder,
		Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactory,
		Action<IPostgresCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Create and configure Postgres options
		var pgOptions = new PostgresCdcOptions();
		var stateStoreOptions = new PostgresCdcStateStoreOptions();

		if (configure is not null)
		{
			var pgBuilder = new PostgresCdcBuilder(pgOptions, stateStoreOptions);
			configure(pgBuilder);
		}

		// Validate state store options
		stateStoreOptions.Validate();

		// Register Postgres CDC options
		_ = builder.Services.Configure<PostgresCdcOptions>(opt =>
		{
			opt.PublicationName = pgOptions.PublicationName;
			opt.ReplicationSlotName = pgOptions.ReplicationSlotName;
			opt.ProcessorId = pgOptions.ProcessorId;
			opt.PollingInterval = pgOptions.PollingInterval;
			opt.BatchSize = pgOptions.BatchSize;
			opt.Timeout = pgOptions.Timeout;
			opt.AutoCreateSlot = pgOptions.AutoCreateSlot;
			opt.UseBinaryProtocol = pgOptions.UseBinaryProtocol;
			opt.TableNames = pgOptions.TableNames;
			opt.RecoveryOptions = pgOptions.RecoveryOptions;
		});

		// Register CDC state store options
		_ = builder.Services.Configure<PostgresCdcStateStoreOptions>(opt =>
		{
			opt.SchemaName = stateStoreOptions.SchemaName;
			opt.TableName = stateStoreOptions.TableName;
		});

		// Register default recovery options if not already registered
		builder.Services.TryAddSingleton(Options.Create(new PostgresCdcRecoveryOptions()));

		// Register Postgres CDC state store with factory
		builder.Services.TryAddSingleton<IPostgresCdcStateStore>(sp =>
		{
			var factory = connectionFactory(sp);
			var stateOptions = sp.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();

			// Get connection string from factory connection
			using var connection = factory();
			return new PostgresCdcStateStore(connection.ConnectionString, stateOptions);
		});

		// Register Postgres CDC processor with factory
		builder.Services.TryAddSingleton<IPostgresCdcProcessor>(sp =>
		{
			var factory = connectionFactory(sp);
			var stateStore = sp.GetRequiredService<IPostgresCdcStateStore>();
			var logger = sp.GetRequiredService<ILogger<PostgresCdcProcessor>>();

			// Get connection string from factory connection for options
			using var connection = factory();
			var options = sp.GetRequiredService<IOptions<PostgresCdcOptions>>();

			// Update connection string in options
			var optionsValue = options.Value;
			optionsValue.ConnectionString = connection.ConnectionString;

			return new PostgresCdcProcessor(Options.Create(optionsValue), stateStore, logger);
		});

		return builder;
	}
}
