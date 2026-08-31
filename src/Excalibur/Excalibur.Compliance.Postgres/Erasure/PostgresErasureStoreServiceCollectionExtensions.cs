// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Postgres erasure store services.
/// </summary>
public static class PostgresErasureStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Postgres erasure store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStore(
		this IServiceCollection services,
		Action<PostgresErasureStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresErasureStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresErasureStoreOptions>,
				PostgresErasureStoreOptionsValidator>());

		_ = services.AddDataSubjectHashing(); // store pseudonymizes data-subject ids (B3).
		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<IErasureStore> marker in the same act. The
		// marker is not separately registerable, so a store that was never handed the context cannot carry
		// a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantAwareStore<IErasureStore, PostgresErasureStore>(sp =>
			new PostgresErasureStore(
				sp.GetRequiredService<IOptions<PostgresErasureStoreOptions>>(),
				sp.GetRequiredService<IDataSubjectHasher>(),
				sp.GetRequiredService<ILogger<PostgresErasureStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<PostgresErasureStore>());

		// Provisioning is a configuration concern, so it is settled once at host startup rather than on the
		// path of every write. The hosted service verifies this store's schema before the host accepts
		// traffic, so a mis-provisioned deployment fails to start instead of reporting a deployment fault
		// as the failure of one data subject's erasure request.
		services.AddSingleton<IErasureSchemaValidator>(sp => sp.GetRequiredService<PostgresErasureStore>());
		_ = services.AddErasureSchemaValidation();

		return services;
	}

	/// <summary>
	/// Adds the Postgres erasure store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddPostgresErasureStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Adds the Postgres erasure store with connection string from configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionStringName">The connection string name from configuration.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresErasureStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<PostgresErasureStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<PostgresErasureStoreOptions>()
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
			ServiceDescriptor.Singleton<IValidateOptions<PostgresErasureStoreOptions>,
				PostgresErasureStoreOptionsValidator>());

		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<IErasureStore> marker in the same act. The
		// marker is not separately registerable, so a store that was never handed the context cannot carry
		// a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantAwareStore<IErasureStore, PostgresErasureStore>(sp =>
			new PostgresErasureStore(
				sp.GetRequiredService<IOptions<PostgresErasureStoreOptions>>(),
				sp.GetRequiredService<IDataSubjectHasher>(),
				sp.GetRequiredService<ILogger<PostgresErasureStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<PostgresErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<PostgresErasureStore>());

		// Provisioning is a configuration concern, so it is settled once at host startup rather than on the
		// path of every write. The hosted service verifies this store's schema before the host accepts
		// traffic, so a mis-provisioned deployment fails to start instead of reporting a deployment fault
		// as the failure of one data subject's erasure request.
		services.AddSingleton<IErasureSchemaValidator>(sp => sp.GetRequiredService<PostgresErasureStore>());
		_ = services.AddErasureSchemaValidation();

		return services;
	}
}
