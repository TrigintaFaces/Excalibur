// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions;
using Excalibur.Data.Postgres.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Extension methods for configuring Postgres provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// </para>
/// </remarks>
public static class OutboxBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the outbox to use Postgres as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="configure">Optional action to configure Postgres-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Postgres as the outbox storage provider.
	/// It registers the <see cref="PostgresOutboxStore"/> and related services.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UsePostgres(connectionString, postgres =>
	///     {
	///         postgres.SchemaName("messaging")
	///                 .TableName("outbox_messages")
	///                 .ReservationTimeout(TimeSpan.FromMinutes(10))
	///                 .MaxAttempts(5);
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UsePostgres(
		this IOutboxBuilder builder,
		string connectionString,
		Action<IPostgresOutboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Create and configure Postgres options
		var postgresOptions = new PostgresOutboxStoreOptions();

		if (configure is not null)
		{
			var postgresBuilder = new PostgresOutboxBuilder(postgresOptions);
			configure(postgresBuilder);
		}

		// Register Postgres options
		_ = builder.Services.AddOptions<PostgresOutboxStoreOptions>()
			.Configure(opt =>
			{
				opt.SchemaName = postgresOptions.SchemaName;
				opt.OutboxTableName = postgresOptions.OutboxTableName;
				opt.DeadLetterTableName = postgresOptions.DeadLetterTableName;
				opt.ReservationTimeout = postgresOptions.ReservationTimeout;
				opt.MaxAttempts = postgresOptions.MaxAttempts;
				opt.BatchProcessingTimeout = postgresOptions.BatchProcessingTimeout;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register IDb for Postgres - the store depends on this
		builder.Services.TryAddSingleton(() =>
		{
			var connection = new NpgsqlConnection(connectionString);
			connection.Open();
			return connection;
		});

		// Register Postgres outbox store
		builder.Services.TryAddSingleton<PostgresOutboxStore>();
		builder.Services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<PostgresOutboxStore>());

		return builder;
	}

	/// <summary>
	/// Configures the outbox to use Postgres with a database factory.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="dbFactory">A factory function that provides an <see cref="IDb"/> instance.</param>
	/// <param name="configure">Optional action to configure Postgres-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="dbFactory"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like multi-database setups,
	/// custom connection pooling, or IDb integration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UsePostgres(
	///         sp => sp.GetRequiredService&lt;IDb&gt;(),
	///         postgres => postgres.SchemaName("messaging"))
	///         .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UsePostgres(
		this IOutboxBuilder builder,
		Func<IServiceProvider, IDb> dbFactory,
		Action<IPostgresOutboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(dbFactory);

		// Create and configure Postgres options
		var postgresOptions = new PostgresOutboxStoreOptions();

		if (configure is not null)
		{
			var postgresBuilder = new PostgresOutboxBuilder(postgresOptions);
			configure(postgresBuilder);
		}

		// Register Postgres options
		_ = builder.Services.AddOptions<PostgresOutboxStoreOptions>()
			.Configure(opt =>
			{
				opt.SchemaName = postgresOptions.SchemaName;
				opt.OutboxTableName = postgresOptions.OutboxTableName;
				opt.DeadLetterTableName = postgresOptions.DeadLetterTableName;
				opt.ReservationTimeout = postgresOptions.ReservationTimeout;
				opt.MaxAttempts = postgresOptions.MaxAttempts;
				opt.BatchProcessingTimeout = postgresOptions.BatchProcessingTimeout;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register Postgres outbox store with IDb factory
		builder.Services.TryAddSingleton(sp =>
		{
			var db = dbFactory(sp);
			var options = sp.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresOutboxStore>>();
			var metrics = sp.GetService<PostgresOutboxStoreMetrics>();
			return new PostgresOutboxStore(db, options, logger, metrics);
		});
		builder.Services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<PostgresOutboxStore>());

		return builder;
	}
}
