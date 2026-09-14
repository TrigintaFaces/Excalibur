// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Postgres;
using Excalibur.AuditLogging.Retention;
using Excalibur.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres audit logging services.
/// </summary>
public static class PostgresAuditServiceCollectionExtensions
{
	/// <summary>
	/// Adds Postgres audit logging services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the Postgres audit options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddPostgresAuditStore(
		this IServiceCollection services,
		Action<PostgresAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresAuditOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresAuditOptions>, PostgresAuditOptionsValidator>());

		RegisterPostgresAuditStoreCore(services);

		return services;
	}

	/// <summary>
	/// Adds Postgres audit logging services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="PostgresAuditOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[RequiresUnreferencedCode("Binding configuration to the options type reflects over its members, which trimming may remove. Configure the options in code instead of binding IConfiguration.")]
	[RequiresDynamicCode("Binding configuration to the options type can require runtime code generation, which native AOT does not support. Configure the options in code instead of binding IConfiguration.")]
	public static IServiceCollection AddPostgresAuditStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<PostgresAuditOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresAuditOptions>, PostgresAuditOptionsValidator>());

		RegisterPostgresAuditStoreCore(services);

		return services;
	}

	private static void RegisterPostgresAuditStoreCore(IServiceCollection services)
	{
		// Shared keyed-MAC + hash-chain integrity strategy + default signing-key provider —
		// PostgresAuditStore depends on IAuditIntegrityStrategy to tag/verify records.
		_ = services.AddAuditIntegrity();

		// The retention settings a consumer actually sets live on PostgresAuditRetentionOptions, but the
		// service that enforces them reads AuditRetentionOptions. The two types are unrelated — both sealed,
		// no inheritance — so without this projection the provider-facing block is wired to nothing: a
		// consumer sets EnableRetentionEnforcement = false, the enforcing service still reads its own
		// default of true, and their audit data is deleted anyway; a consumer sets RetentionPeriod = 90 days
		// and every event is kept for the core default of seven years. All three properties are projected,
		// because a block where one knob works and the rest are inert is worse than one that is wholly
		// inert — the working knob is the evidence a consumer uses to conclude the others work too. Mirrors
		// the identical projection in SqlServerAuditServiceCollectionExtensions.RegisterSqlServerAuditStoreCore.
		// CleanupBatchSize is deliberately NOT among them: there is no store-agnostic batch size in the core
		// options to project it onto (see PostgresAuditRetentionOptions.CleanupBatchSize).
		//
		// A DEFAULT IS NOT A CHOICE. Each property is projected only when it differs from this type's own
		// shipped default, so registering the store never overwrites a window the host already set on the
		// core options via a separate AddAuditRetention call.
		_ = services.AddOptions<AuditRetentionOptions>()
			.Configure<IOptions<PostgresAuditOptions>>(static (core, postgres) =>
			{
				var provider = postgres.Value.Retention;

				if (provider.EnableRetentionEnforcement != PostgresAuditRetentionOptions.DefaultEnableRetentionEnforcement)
				{
					core.EnableRetentionEnforcement = provider.EnableRetentionEnforcement;
				}

				if (provider.RetentionPeriod != PostgresAuditRetentionOptions.DefaultRetentionPeriod)
				{
					core.RetentionPeriod = provider.RetentionPeriod;
				}

				if (provider.CleanupInterval != PostgresAuditRetentionOptions.DefaultCleanupInterval)
				{
					core.CleanupInterval = provider.CleanupInterval;
				}
			});

		// Idempotent single-tenant default: the store takes ITenantContext positionally, so without a
		// registration it cannot be constructed — it would throw at resolve while every unit test that news
		// it up directly still passed. TryAdd leaves a multi-tenant host's own registration untouched.
		_ = services.AddDefaultTenantContext();

		// Registered through the capability seam rather than a bare TryAddSingleton. PostgresAuditStore takes
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
		_ = services.AddTenantAwareStore<IAuditStore, PostgresAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<PostgresAuditStore>());
	}
}
