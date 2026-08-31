// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging;
using Excalibur.AuditLogging.Retention;
using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server audit logging services.
/// </summary>
public static class SqlServerAuditServiceCollectionExtensions
{
	/// <summary>
	/// Adds SQL Server audit logging services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the SQL Server audit options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddSqlServerAuditStore(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("AuditDb");
	///     options.SchemaName = "audit";
	///     options.Retention.RetentionPeriod = TimeSpan.FromDays(7 * 365); // 7 years for SOC2
	///     options.EnableHashChain = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerAuditStore(
		this IServiceCollection services,
		Action<SqlServerAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerAuditOptions>, SqlServerAuditOptionsValidator>());

		RegisterSqlServerAuditStoreCore(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server audit logging services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="SqlServerAuditOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerAuditStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqlServerAuditOptions>().Bind(configuration).ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerAuditOptions>, SqlServerAuditOptionsValidator>());

		RegisterSqlServerAuditStoreCore(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server audit logging services with a pre-configured options instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-configured options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or options is null.</exception>
	public static IServiceCollection AddSqlServerAuditStore(
		this IServiceCollection services,
		SqlServerAuditOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		return services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = options.ConnectionString;
			o.SchemaName = options.SchemaName;
			o.TableName = options.TableName;
			o.BatchInsertSize = options.BatchInsertSize;
			o.Retention.RetentionPeriod = options.Retention.RetentionPeriod;
			o.Retention.EnableRetentionEnforcement = options.Retention.EnableRetentionEnforcement;
			o.Retention.CleanupInterval = options.Retention.CleanupInterval;
			o.Retention.CleanupBatchSize = options.Retention.CleanupBatchSize;
			o.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
			o.UsePartitioning = options.UsePartitioning;
			o.EnableHashChain = options.EnableHashChain;
			o.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
		});
	}

	/// <summary>
	/// Adds SQL Server audit annotation store services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the SQL Server audit annotation store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddSqlServerAuditAnnotationStore(
		this IServiceCollection services,
		Action<SqlServerAuditAnnotationStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerAuditAnnotationStoreOptions>,
				SqlServerAuditAnnotationStoreOptionsValidator>());

		RegisterSqlServerAuditAnnotationStoreCore(services);

		return services;
	}

	/// <summary>
	/// Adds SQL Server audit annotation store services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddSqlServerAuditAnnotationStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqlServerAuditAnnotationStoreOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerAuditAnnotationStoreOptions>,
				SqlServerAuditAnnotationStoreOptionsValidator>());

		RegisterSqlServerAuditAnnotationStoreCore(services);

		return services;
	}

	private static void RegisterSqlServerAuditStoreCore(IServiceCollection services)
	{
		// Shared keyed-MAC + hash-chain integrity strategy + default signing-key provider —
		// SqlServerAuditStore depends on IAuditIntegrityStrategy to tag/verify records.
		_ = services.AddAuditIntegrity();

		// The retention opt-out a consumer actually sets lives on SqlServerAuditRetentionOptions, but the
		// service that enforces it reads AuditRetentionOptions. The two types are unrelated — both sealed,
		// no inheritance — so without this projection the provider-facing switch is wired to nothing: a
		// consumer sets EnableRetentionEnforcement = false, the enforcing service still reads its own
		// default of true, and their audit data is deleted anyway.
		//
		// Placed in the shared core rather than in one overload deliberately: all three AddSqlServerAuditStore
		// overloads funnel through here, so the projection cannot be present on one registration path and
		// missing from another.
		_ = services.AddOptions<AuditRetentionOptions>()
			.Configure<IOptions<SqlServerAuditOptions>>(static (core, sqlServer) =>
				core.EnableRetentionEnforcement = sqlServer.Value.Retention.EnableRetentionEnforcement);

		// Idempotent single-tenant default: SqlServerAuditStore takes ITenantContext positionally, so without
		// a registration the store cannot be constructed at all — it would throw at resolve while every unit
		// test that news it up directly still passed. A single-tenant host resolves the default tenant here;
		// a multi-tenant host has already registered its own and TryAdd leaves that untouched.
		_ = services.AddDefaultTenantContext();

		// Registered through the capability seam rather than a bare TryAddSingleton. SqlServerAuditStore takes
		// ITenantContext, and every read it builds binds the ambient tenant term (the query filter is a
		// scope, not a caller-supplied filter), so the seam derives the ambient-scoping mechanism from the
		// constructor and emits ITenantScopingCapability<IAuditStore> as part of the same act. Without that
		// marker a host wiring this store alongside the row discriminator is refused at startup, because
		// IAuditStore carries [TenantOwned] and nothing attested the store honours it.
		//
		// The estate-wide scope recorded for this provider in ARCHITECTURE.md is chain VERIFICATION only,
		// enumerated per partition. It is not the store's tenancy mechanism, and the partitioned marker
		// would be the wrong attestation here: that one states the tenant is re-established from the row
		// and never inferred from ambient state, which is the opposite of what this store does.
		_ = services.AddTenantAwareStore<IAuditStore, SqlServerAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<SqlServerAuditStore>());
	}

	private static void RegisterSqlServerAuditAnnotationStoreCore(IServiceCollection services)
	{
		// Self-sufficient rather than order-dependent: this method resolves ITenantContext as a REQUIRED
		// service, so it wires the default itself instead of relying on a sibling registration having run
		// first. TryAdd makes it idempotent, and a consumer's own context still wins.
		_ = services.AddDefaultTenantContext();

		// Built by an explicit factory rather than by type activation, because the store's ITenantContext
		// is OPTIONAL: a single-tenant host registers none, and type activation would demand every
		// constructor parameter be resolvable and fail at resolve time for that supported shape. GetService
		// yields null there, which the store reads as the untenanted partition — a concrete tenant term,
		// not an absent one — so a host that never opted into multi-tenancy still emits a scoped predicate.
		// The actor provider is NOT resolved here. This is a singleton and a consumer's actor provider is
		// per-caller state -- resolving it at this point binds one caller's identity for the life of the
		// container, and that identity is written to each row as the annotation's author. The store opens
		// a scope per operation instead.
		services.TryAddSingleton(sp => new SqlServerAuditAnnotationStore(
			sp.GetRequiredService<IOptions<SqlServerAuditAnnotationStoreOptions>>(),
			sp.GetRequiredService<IServiceScopeFactory>(),
			sp.GetRequiredService<TimeProvider>(),
			sp.GetRequiredService<ITenantContext>(),
			sp.GetRequiredService<ILogger<SqlServerAuditAnnotationStore>>()));

		// Registered under the core package's inner-store KEY, and deliberately NOT as IAuditAnnotationStore.
		// That interface is bound by the core package and always yields the access-checking decorator; a
		// provider that bound it directly would hand consumers a store with no role or authorship checks on
		// it — which is exactly what this package used to do, and no call order could recover from it.
		//
		// AddKeyedSingleton rather than TryAdd is load-bearing: the core package registers the in-memory
		// store under this key with TryAdd, so a plain TryAdd here would lose whenever AddAuditAnnotations
		// ran first and this package would go silently unused. A later keyed registration supersedes an
		// earlier one, so this store wins in BOTH call orders.
		services.AddKeyedSingleton<IAuditAnnotationStore>(
			AuditLoggingServiceCollectionExtensions.InnerAuditAnnotationStoreKey,
			(sp, _) => sp.GetRequiredService<SqlServerAuditAnnotationStore>());
	}
}
