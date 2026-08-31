// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Extension methods for configuring Oracle provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// Connection is configured via the builder using
/// <see cref="IOracleOutboxBuilder.ConnectionString"/> or
/// <see cref="IOracleOutboxBuilder.ConnectionFactory"/>.
/// </para>
/// </remarks>
public static class OutboxBuilderOracleExtensions
{
	/// <summary>
	/// Configures the outbox to use Oracle as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure Oracle-specific options including connection.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Oracle as the outbox storage provider.
	/// It registers the <see cref="OracleOutboxStore"/> and related services.
	/// Connection can be provided via the builder using
	/// <see cref="IOracleOutboxBuilder.ConnectionString"/> or
	/// <see cref="IOracleOutboxBuilder.ConnectionFactory"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseOracle(oracle =>
	///     {
	///         oracle.ConnectionString(connectionString)
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
	///     outbox.UseOracle(oracle =>
	///     {
	///         oracle.ConnectionFactory(sp => sp.GetRequiredService&lt;IDb&gt;())
	///                 .SchemaName("messaging");
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </example>
	public static IOutboxBuilder UseOracle(
		this IOutboxBuilder builder,
		Action<IOracleOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure Oracle options
		var oracleOptions = new OracleOutboxStoreOptions();
		var oracleBuilder = new OracleOutboxBuilder(oracleOptions);
		configure(oracleBuilder);

		// Register Oracle options
		_ = builder.Services.AddOptions<OracleOutboxStoreOptions>()
			.Configure(opt =>
			{
				opt.SchemaName = oracleOptions.SchemaName;
				opt.OutboxTableName = oracleOptions.OutboxTableName;
				opt.DeadLetterTableName = oracleOptions.DeadLetterTableName;
				opt.ReservationTimeout = oracleOptions.ReservationTimeout;
				opt.MaxAttempts = oracleOptions.MaxAttempts;
				opt.BatchProcessing.BatchProcessingTimeout = oracleOptions.BatchProcessing.BatchProcessingTimeout;
			})
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OracleOutboxStoreOptions>, OracleOutboxStoreOptionsValidator>());

		// The fail-closed single-tenant default so a single-tenant host has a non-null ITenantContext; the
		// multi-tenancy composition replaces it with the ambient context. It is NOT what makes this outbox
		// tenant-safe and the store registration below no longer depends on it: the outbox carries the tenant
		// on the row (stamped on enqueue, returned on drain), which is a different mechanism from reading an
		// ambient discriminator, and it is attested as that mechanism rather than as ambient scoping.
		builder.Services.AddDefaultTenantContext();

		// Register services based on connection mode
		if (oracleBuilder.ConfiguredDbFactory is not null)
		{
			var dbFactory = oracleBuilder.ConfiguredDbFactory;

			// Register Oracle outbox store with IDb factory. AddTenantAwareStore emits the
			// ITenantPartitionedCapability<IOutboxStore> marker as part of THIS registration, so the marker
			// cannot exist without the store factory. It is the partitioned seam rather than the scoped one
			// because OracleOutboxStore reads no ambient tenant on any path: it persists TENANTID per message
			// and returns it on drain, so the owning tenant is re-established from the row. That seam takes no
			// ITenantContext, so there is no dependency here to be handed and silently discarded.
			builder.Services.AddTenantAwareStore<IOutboxStore, OracleOutboxStore>(sp =>
			{
				var db = dbFactory(sp);
				var options = sp.GetRequiredService<IOptions<OracleOutboxStoreOptions>>();
				var logger = sp.GetRequiredService<ILogger<OracleOutboxStore>>();
				var metrics = sp.GetService<OracleOutboxStoreMetrics>();
				return new OracleOutboxStore(db, options, logger, metrics);
			});
		}
		else if (oracleBuilder.ConfiguredConnectionString is not null)
		{
			var connectionString = oracleBuilder.ConfiguredConnectionString;

			// Registers IDb, which is what the store below resolves. The type argument is explicit and the
			// factory takes the provider: a no-argument lambda cannot bind the factory overload, so it bound
			// the INSTANCE overload instead and registered a Func<OracleConnection> under its own type. IDb was then
			// registered nowhere, and the store threw on resolve the first time a host used this path.
			builder.Services.TryAddSingleton<IDb>(_ =>
			{
				var connection = new OracleConnection(connectionString);
				connection.Open();
				return new OutboxDb(connection);
			});

			// Register Oracle outbox store (DI-constructed). AddTenantAwareStore emits the
			// ITenantPartitionedCapability<IOutboxStore> marker inseparably from this registration. The tenant
			// is message-carried (TENANTID per row), not ambient, so this is the partitioned seam and there is
			// no ITenantContext to thread or to drop.
			builder.Services.AddTenantAwareStore<IOutboxStore, OracleOutboxStore>(
				static sp => ActivatorUtilities.CreateInstance<OracleOutboxStore>(sp));
		}
		else
		{
			throw new InvalidOperationException(
				"Oracle outbox requires a connection. " +
				"Call ConnectionString() or ConnectionFactory() on the builder. " +
				"Example: outbox.UseOracle(pg => pg.ConnectionString(\"Host=...\"))");
		}

		builder.Services.AddKeyedSingleton<IOutboxStore>("oracle", (sp, _) => sp.GetRequiredService<OracleOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("oracle"));

		// The ITenantPartitionedCapability<IOutboxStore> marker is emitted by AddTenantAwareStore
		// above, inseparably from the store registration. It attests the mechanism this store actually
		// implements: TENANTID stamped and persisted on enqueue and returned on drain for the processor's
		// per-message BeginScope. The drain itself is deliberately estate-wide.
		builder.Services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<OracleOutboxStore>());

		return builder;
	}
}
