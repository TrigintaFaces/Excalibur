// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres legal hold store services.
/// </summary>
public static class PostgresLegalHoldStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Postgres legal hold store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresLegalHoldStore(
		this IServiceCollection services,
		Action<PostgresLegalHoldStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresLegalHoldStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresLegalHoldStoreOptions>,
				PostgresLegalHoldStoreOptionsValidator>());

		_ = services.AddDataSubjectHashing(); // store pseudonymizes data-subject ids (B3).
		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<ILegalHoldStore> marker in the same act, so
		// a store that was never handed the context cannot carry a truthful-looking capability and pass
		// the multi-tenancy gate.
		_ = services.AddTenantAwareStore<ILegalHoldStore, PostgresLegalHoldStore>(sp =>
			new PostgresLegalHoldStore(
				sp.GetRequiredService<IOptions<PostgresLegalHoldStoreOptions>>(),
				sp.GetRequiredService<ILogger<PostgresLegalHoldStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<PostgresLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<PostgresLegalHoldStore>());

		return services;
	}

	/// <summary>
	/// Adds the Postgres legal hold store with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresLegalHoldStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresLegalHoldStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Adds the Postgres legal hold store with connection string from configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionStringName">The connection string name from configuration.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresLegalHoldStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<PostgresLegalHoldStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<PostgresLegalHoldStoreOptions>()
			.Configure<IConfiguration>((options, config) =>
			{
				var connectionString = config.GetConnectionString(connectionStringName);
				if (!string.IsNullOrEmpty(connectionString))
				{
					options.ConnectionString = connectionString;
				}
			})
			.PostConfigure(options =>
			{
				configure?.Invoke(options);
				options.Validate();
			})
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresLegalHoldStoreOptions>,
				PostgresLegalHoldStoreOptionsValidator>());

		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<ILegalHoldStore> marker in the same act, so
		// a store that was never handed the context cannot carry a truthful-looking capability and pass
		// the multi-tenancy gate.
		_ = services.AddTenantAwareStore<ILegalHoldStore, PostgresLegalHoldStore>(sp =>
			new PostgresLegalHoldStore(
				sp.GetRequiredService<IOptions<PostgresLegalHoldStoreOptions>>(),
				sp.GetRequiredService<ILogger<PostgresLegalHoldStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<PostgresLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<PostgresLegalHoldStore>());

		return services;
	}
}
