// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the canonical
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class OutboxBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the outbox to use SQL Server as the storage provider.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure SQL Server-specific options via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("Messaging")
	///            .TableName("OutboxMessages");
	///     })
	///     .EnableBackgroundProcessing();
	/// }));
	///
	/// // Named connection string
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionStringName("OutboxDb");
	///     });
	/// }));
	///
	/// // Connection factory (Azure Managed Identity)
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionFactory(sp =>
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("OutboxDb")!;
	///             return () => new SqlConnection(connStr);
	///         });
	///     });
	/// }));
	///
	/// // Bind from appsettings.json
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.BindConfiguration("Outbox:SqlServer");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling",
		Justification = "DI composition root wires many provider types by design; matches the store's existing suppression.")]
	public static IOutboxBuilder UseSqlServer(
		this IOutboxBuilder builder,
		Action<ISqlServerOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options via builder
		var sqlOptions = new SqlServerOutboxOptions();
		var sqlBuilder = new SqlServerOutboxBuilder(sqlOptions);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		// Determine whether the builder configured a non-connection-string connection
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		// Register options from builder state
		_ = builder.Services.Configure<SqlServerOutboxOptions>(opt => CopyBuilderOptions(sqlOptions, opt));

		// Register BindConfiguration if set
		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerOutboxOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
			{
				var explicitConnectionString = sqlOptions.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerOutboxOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart with connection awareness. The validator also enforces the cross-options
		// FailureBackoffFloorSeconds > PollingInterval invariant, so it resolves IOptions<OutboxProcessingOptions>
		// from the provider (a factory registration, since HasBuilderConnection is set here by the builder).
		builder.Services.AddSingleton<IValidateOptions<SqlServerOutboxOptions>>(sp =>
			new SqlServerOutboxOptionsValidator(
				sp.GetRequiredService<IOptions<Excalibur.Outbox.Outbox.OutboxProcessingOptions>>(),
				sp.GetRequiredService<IOptions<Excalibur.Outbox.Partitioning.OutboxPartitionOptions>>())
			{
				HasBuilderConnection = hasBuilderConnection,
			});
		builder.Services.AddOptions<SqlServerOutboxOptions>().ValidateOnStart();

		// Register SQL Server outbox store using resolved connection factory
		// Fail-closed single-tenant default so the dep-gated AddTenantScopedStore seam resolves ITenantContext.
		builder.Services.AddDefaultTenantContext();
		builder.Services.AddTenantScopedStore<IOutboxStore, SqlServerOutboxStore>((sp, _) =>
		{
			var factory = connectionFactory(sp);
			var options = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
			var payloadSerializer = sp.GetService<IPayloadSerializer>();
			var logger = sp.GetRequiredService<ILogger<SqlServerOutboxStore>>();
			return new SqlServerOutboxStore(factory, options, payloadSerializer, logger);
		});
		builder.Services.AddKeyedSingleton<IOutboxStore>("sqlserver", (sp, _) =>
		{
			var inner = sp.GetRequiredService<SqlServerOutboxStore>();

			// The decorator forwards capability resolution to the store it wraps, so wrapping cannot erase
			// a capability SqlServerOutboxStore provides. Consumers resolve capabilities with
			// GetService(typeof(...)); casting the decorated instance would see only the decorator's own type.
			return new Excalibur.Outbox.Diagnostics.TelemetryOutboxStoreDecorator(inner);
		});
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("sqlserver"));

		// The ITenantScopingCapability<IOutboxStore> marker is emitted by AddTenantScopedStore above,
		// inseparably from the store registration (S886 rw2ull — the marker cannot exist without the store
		// factory). The outbox honors the ambient tenant by stamping + persisting TenantId on enqueue and
		// returning it on drain for the processor's per-message BeginScope.
		builder.Services.TryAddSingleton<IMultiTransportOutboxStore>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		builder.Services.TryAddSingleton<IMultiTransportOutboxStoreAdmin>(sp => sp.GetRequiredService<SqlServerOutboxStore>());
		builder.Services.TryAddSingleton<ITransactionalOutboxWriter>(sp => sp.GetRequiredService<SqlServerOutboxStore>());

		// Register health checks if enabled
		if (sqlBuilder.HealthChecksEnabled && !string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddSqlServer(
					sqlOptions.ConnectionString,
					name: sqlBuilder.HealthCheckName,
					tags: ["outbox", "sqlserver"]);
		}

		return builder;
	}

	/// <summary>
	/// Configures the outbox to use SQL Server dead letter queue.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure dead letter queue options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IOutboxBuilder WithSqlServerDeadLetterQueue(
		this IOutboxBuilder builder,
		Action<SqlServerDeadLetterQueueOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.Configure(configure);

		builder.Services.TryAddSingleton<SqlServerDeadLetterQueue>();
		builder.Services.TryAddSingleton<IDeadLetterQueue>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());
		builder.Services.TryAddSingleton<IDeadLetterQueueAdmin>(sp => sp.GetRequiredService<SqlServerDeadLetterQueue>());

		return builder;
	}

	/// <summary>
	/// Copies the builder-configured options onto the registered options instance.
	/// </summary>
	private static void CopyBuilderOptions(SqlServerOutboxOptions source, SqlServerOutboxOptions target)
	{
		target.ConnectionString = source.ConnectionString;
		target.Tables.SchemaName = source.Tables.SchemaName;
		target.Tables.OutboxTableName = source.Tables.OutboxTableName;
		target.Tables.TransportsTableName = source.Tables.TransportsTableName;
		target.Tables.DeadLetterTableName = source.Tables.DeadLetterTableName;
		target.Processing.CommandTimeoutSeconds = source.Processing.CommandTimeoutSeconds;
		target.Processing.UseRowLocking = source.Processing.UseRowLocking;
		target.Processing.DefaultBatchSize = source.Processing.DefaultBatchSize;
		target.Processing.MaxRetryCount = source.Processing.MaxRetryCount;
		target.Processing.RetryDelayMinutes = source.Processing.RetryDelayMinutes;
	}

	/// <summary>
	/// Resolves the connection factory from the builder configuration.
	/// </summary>
	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerOutboxBuilder sqlBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.ConnectionFactoryFunc is not null)
		{
			return sqlBuilder.ConnectionFactoryFunc;
		}

		// 2. Named connection string resolved from IConfiguration
		if (sqlBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = sqlBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 3 & 4. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}
}
