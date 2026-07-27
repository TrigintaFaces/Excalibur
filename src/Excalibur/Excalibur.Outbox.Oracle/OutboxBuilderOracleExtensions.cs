// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

		// The fail-closed single-tenant default guarantees a non-null ITenantContext so the dep-gated
		// AddTenantScopedStore seam resolves (GetRequiredService) rather than throwing; the multi-tenancy
		// composition replaces it with the ambient context. The outbox honors the tenant by stamping +
		// persisting TENANTID per message and returning it on drain.
		builder.Services.AddDefaultTenantContext();

		// Register services based on connection mode
		if (oracleBuilder.ConfiguredDbFactory is not null)
		{
			var dbFactory = oracleBuilder.ConfiguredDbFactory;

			// Register Oracle outbox store with IDb factory. AddTenantScopedStore emits the
			// ITenantScopingCapability<IOutboxStore> marker as part of THIS registration so the marker cannot
			// exist without the store factory (S886 rw2ull — no lying marker). The outbox honors the ambient
			// tenant by persisting TENANTID per message and returning it on drain.
			// The dep-gated seam still resolves ITenantContext (fail-closed) before invoking this factory;
			// OracleOutboxStore honors the tenant purely by persisting TENANTID per message (its ctor takes no
			// ITenantContext), so the resolved context is discarded here — the registration is still gated on
			// the context being resolvable, which keeps the marker inseparable from a gated registration.
			builder.Services.AddTenantScopedStore<IOutboxStore, OracleOutboxStore>((sp, _) =>
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

			// Register IDb for Oracle - the store depends on this
			builder.Services.TryAddSingleton(() =>
			{
				var connection = new OracleConnection(connectionString);
				connection.Open();
				return connection;
			});

			// Register Oracle outbox store (DI-constructed). The dep-gated seam resolves ITenantContext
			// (fail-closed) before this factory runs; OracleOutboxStore takes no ITenantContext (tenant is
			// message-carried), so the resolved context is discarded — registration stays gated on it.
			builder.Services.AddTenantScopedStore<IOutboxStore, OracleOutboxStore>(
				static (sp, _) => ActivatorUtilities.CreateInstance<OracleOutboxStore>(sp));
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

		// The ITenantScopingCapability<IOutboxStore> marker is emitted by AddTenantScopedStore above,
		// inseparably from the store registration (S886 rw2ull — the marker cannot exist without the store
		// factory). The outbox honors the ambient tenant by stamping + persisting TENANTID on enqueue and
		// returning it on drain for the processor's per-message BeginScope.
		builder.Services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<OracleOutboxStore>());

		return builder;
	}
}
