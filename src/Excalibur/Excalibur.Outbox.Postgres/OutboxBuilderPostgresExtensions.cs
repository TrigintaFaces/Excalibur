// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Extension methods for configuring Postgres provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// Connection is configured via the builder using
/// <see cref="IPostgresOutboxBuilder.ConnectionString"/> or
/// <see cref="IPostgresOutboxBuilder.ConnectionFactory"/>.
/// </para>
/// </remarks>
public static class OutboxBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the outbox to use Postgres as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure Postgres-specific options including connection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Postgres as the outbox storage provider.
	/// It registers the <see cref="PostgresOutboxStore"/> and related services.
	/// Connection can be provided via the builder using
	/// <see cref="IPostgresOutboxBuilder.ConnectionString"/> or
	/// <see cref="IPostgresOutboxBuilder.ConnectionFactory"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UsePostgres(postgres =>
	///     {
	///         postgres.ConnectionString(connectionString)
	///                 .SchemaName("messaging")
	///                 .TableName("outbox_messages")
	///                 .ReservationTimeout(TimeSpan.FromMinutes(10))
	///                 .MaxAttempts(5);
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	///
	/// // IDb factory
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UsePostgres(postgres =>
	///     {
	///         postgres.ConnectionFactory(sp => sp.GetRequiredService&lt;IDb&gt;())
	///                 .SchemaName("messaging");
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UsePostgres(
		this IOutboxBuilder builder,
		Action<IPostgresOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure Postgres options
		var postgresOptions = new PostgresOutboxStoreOptions();
		var postgresBuilder = new PostgresOutboxBuilder(postgresOptions);
		configure(postgresBuilder);

		// Register Postgres options
		_ = builder.Services.AddOptions<PostgresOutboxStoreOptions>()
			.Configure(opt =>
			{
				opt.SchemaName = postgresOptions.SchemaName;
				opt.OutboxTableName = postgresOptions.OutboxTableName;
				opt.DeadLetterTableName = postgresOptions.DeadLetterTableName;
				opt.ReservationTimeout = postgresOptions.ReservationTimeout;
				opt.MaxAttempts = postgresOptions.MaxAttempts;
				opt.BatchProcessing.BatchProcessingTimeout = postgresOptions.BatchProcessing.BatchProcessingTimeout;
			})
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresOutboxStoreOptions>, PostgresOutboxStoreOptionsValidator>());

		// Register services based on connection mode
		if (postgresBuilder.ConfiguredDbFactory is not null)
		{
			var dbFactory = postgresBuilder.ConfiguredDbFactory;

			// Register Postgres outbox store with IDb factory
			builder.Services.TryAddSingleton(sp =>
			{
				var db = dbFactory(sp);
				var options = sp.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
				var logger = sp.GetRequiredService<ILogger<PostgresOutboxStore>>();
				var metrics = sp.GetService<PostgresOutboxStoreMetrics>();
				return new PostgresOutboxStore(db, options, logger, metrics);
			});
		}
		else if (postgresBuilder.ConfiguredConnectionString is not null)
		{
			var connectionString = postgresBuilder.ConfiguredConnectionString;

			// Register IDb for Postgres - the store depends on this
			builder.Services.TryAddSingleton(() =>
			{
				var connection = new NpgsqlConnection(connectionString);
				connection.Open();
				return connection;
			});

			// Register Postgres outbox store
			builder.Services.TryAddSingleton<PostgresOutboxStore>();
		}
		else
		{
			throw new InvalidOperationException(
				"Postgres outbox requires a connection. " +
				"Call ConnectionString() or ConnectionFactory() on the builder. " +
				"Example: outbox.UsePostgres(pg => pg.ConnectionString(\"Host=...\"))");
		}

		builder.Services.AddKeyedSingleton<IOutboxStore>("postgres", (sp, _) => sp.GetRequiredService<PostgresOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("postgres"));

		return builder;
	}
}
