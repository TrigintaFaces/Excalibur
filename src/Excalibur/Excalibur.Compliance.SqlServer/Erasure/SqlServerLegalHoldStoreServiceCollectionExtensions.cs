// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server legal hold store services.
/// </summary>
public static class SqlServerLegalHoldStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the SQL Server legal hold store to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> A delegate to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerLegalHoldStore(
		this IServiceCollection services,
		Action<SqlServerLegalHoldStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SqlServerLegalHoldStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerLegalHoldStoreOptions>,
				SqlServerLegalHoldStoreOptionsValidator>());

		_ = services.AddDataSubjectHashing(); // store pseudonymizes data-subject ids (B3).
		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantScopedStore builds the store WITH the ambient tenant context and emits the
		// ITenantScopingCapability<ILegalHoldStore> marker in the same act, so a store that was never
		// handed the context cannot carry a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantScopedStore<ILegalHoldStore, SqlServerLegalHoldStore>((sp, tenantContext) =>
			new SqlServerLegalHoldStore(
				sp.GetRequiredService<IOptions<SqlServerLegalHoldStoreOptions>>(),
				sp.GetRequiredService<ILogger<SqlServerLegalHoldStore>>(),
				tenantContext,
				sp.GetService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());

		return services;
	}

	/// <summary>
	/// Adds the SQL Server legal hold store with connection string from configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionStringName"> The connection string name from configuration. </param>
	/// <param name="configure"> Optional additional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerLegalHoldStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<SqlServerLegalHoldStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<SqlServerLegalHoldStoreOptions>()
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
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerLegalHoldStoreOptions>,
				SqlServerLegalHoldStoreOptionsValidator>());

		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantScopedStore builds the store WITH the ambient tenant context and emits the
		// ITenantScopingCapability<ILegalHoldStore> marker in the same act, so a store that was never
		// handed the context cannot carry a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantScopedStore<ILegalHoldStore, SqlServerLegalHoldStore>((sp, tenantContext) =>
			new SqlServerLegalHoldStore(
				sp.GetRequiredService<IOptions<SqlServerLegalHoldStoreOptions>>(),
				sp.GetRequiredService<ILogger<SqlServerLegalHoldStore>>(),
				tenantContext,
				sp.GetService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());

		return services;
	}
}
