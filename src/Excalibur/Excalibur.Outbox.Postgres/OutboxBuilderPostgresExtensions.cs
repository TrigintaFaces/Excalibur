// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

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
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
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
	/// }));
	///
	/// // IDb factory
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UsePostgres(postgres =>
	///     {
	///         postgres.ConnectionFactory(sp => sp.GetRequiredService&lt;IDb&gt;())
	///                 .SchemaName("messaging");
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
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

		// The fail-closed single-tenant default so a single-tenant host has a non-null ITenantContext; the
		// multi-tenancy composition replaces it with the ambient context. It is NOT what makes this outbox
		// tenant-safe and the store registration below no longer depends on it: the outbox carries the tenant
		// on the row (stamped on enqueue, returned on drain), which is a different mechanism from reading an
		// ambient discriminator, and it is attested as that mechanism rather than as ambient scoping.
		builder.Services.AddDefaultTenantContext();

		// Register services based on connection mode
		if (postgresBuilder.ConfiguredDbFactory is not null)
		{
			var dbFactory = postgresBuilder.ConfiguredDbFactory;

			// Register Postgres outbox store with IDb factory. AddTenantAwareStore emits the
			// ITenantPartitionedCapability<IOutboxStore> marker as part of THIS registration, so the marker
			// cannot exist without the store factory. It is the partitioned seam rather than the scoped one
			// because this store does not read an ambient tenant on any path: it persists tenant_id per message
			// and returns it on drain, so the owning tenant is re-established from the row. That seam takes no
			// ITenantContext, so there is no dependency here to be handed and silently discarded.
			builder.Services.AddTenantAwareStore<IOutboxStore, PostgresOutboxStore>(sp =>
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

			// Registers IDb, which is what the store below resolves. The type argument is explicit and the
			// factory takes the provider: a no-argument lambda cannot bind the factory overload, so it bound
			// the INSTANCE overload instead and registered a Func<NpgsqlConnection> under its own type. IDb was then
			// registered nowhere, and the store threw on resolve the first time a host used this path.
			builder.Services.TryAddSingleton<IDb>(_ =>
			{
				var connection = new NpgsqlConnection(connectionString);
				connection.Open();
				return new OutboxDb(connection);
			});

			// Register Postgres outbox store (DI-constructed). AddTenantAwareStore emits the
			// ITenantPartitionedCapability<IOutboxStore> marker inseparably from this registration. Isolation
			// here is the per-message tenant_id column, not an ambient discriminator, so this is the partitioned
			// seam and there is no ITenantContext to thread or to drop.
			builder.Services.AddTenantAwareStore<IOutboxStore, PostgresOutboxStore>(
				static sp => new PostgresOutboxStore(
					sp.GetRequiredService<IDb>(),
					sp.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>(),
					sp.GetRequiredService<ILogger<PostgresOutboxStore>>(),
					sp.GetService<PostgresOutboxStoreMetrics>()));
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

		// The ITenantPartitionedCapability<IOutboxStore> marker is emitted by AddTenantAwareStore
		// above, inseparably from the store registration. It attests the mechanism this store actually
		// implements: tenant_id stamped and persisted on enqueue and returned on drain for the processor's
		// per-message BeginScope. The drain itself is deliberately estate-wide.
		builder.Services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<PostgresOutboxStore>());

		return builder;
	}
}
