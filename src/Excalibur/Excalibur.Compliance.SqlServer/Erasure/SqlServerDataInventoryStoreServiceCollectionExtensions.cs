// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server data inventory store services.
/// </summary>
public static class SqlServerDataInventoryStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the SQL Server data inventory store to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> A delegate to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerDataInventoryStore(
		this IServiceCollection services,
		Action<SqlServerDataInventoryStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SqlServerDataInventoryStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerDataInventoryStoreOptions>,
				SqlServerDataInventoryStoreOptionsValidator>());

		_ = services.AddDataSubjectHashing(); // store pseudonymizes data-subject ids (B3).
		// The store's constructor REQUIRES both the ambient context and the tenant-context options, and it is
		// registered here by type, so both must resolve. AddDefaultTenantContext registers the single-tenant
		// default context and the TenantContextOptions binding; TryAdd keeps a host's own context, which the
		// multi-tenancy composition replaces with the resolver-driven one.
		_ = services.AddDefaultTenantContext();
		// Through the tenant-aware seam rather than a bare TryAdd. The store requires an ambient
		// ITenantContext and binds its term on every statement it builds, and IDataInventoryStore is a
		// tenant-owned contract -- so registered plainly it attested nothing and a multi-tenant host was
		// refused for a store that is correct. The seam resolves the context (fail-closed), constructs the
		// store with it, and emits the tenant-scoping capability in the same act, so the attestation cannot
		// exist apart from the wiring it describes.
		_ = services.AddTenantAwareStore<IDataInventoryStore, SqlServerDataInventoryStore>();
		services.TryAddSingleton<IDataInventoryStore>(sp => sp.GetRequiredService<SqlServerDataInventoryStore>());
		services.TryAddSingleton<IDataInventoryQueryStore>(sp => sp.GetRequiredService<SqlServerDataInventoryStore>());

		return services;
	}

	/// <summary>
	/// Adds the SQL Server data inventory store with connection string from configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionStringName"> The connection string name from configuration. </param>
	/// <param name="configure"> Optional additional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerDataInventoryStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<SqlServerDataInventoryStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<SqlServerDataInventoryStoreOptions>()
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
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerDataInventoryStoreOptions>,
				SqlServerDataInventoryStoreOptionsValidator>());

		// The store's constructor REQUIRES both the ambient context and the tenant-context options, and it is
		// registered here by type, so both must resolve. AddDefaultTenantContext registers the single-tenant
		// default context and the TenantContextOptions binding; TryAdd keeps a host's own context, which the
		// multi-tenancy composition replaces with the resolver-driven one.
		_ = services.AddDefaultTenantContext();
		// Through the tenant-aware seam rather than a bare TryAdd. The store requires an ambient
		// ITenantContext and binds its term on every statement it builds, and IDataInventoryStore is a
		// tenant-owned contract -- so registered plainly it attested nothing and a multi-tenant host was
		// refused for a store that is correct. The seam resolves the context (fail-closed), constructs the
		// store with it, and emits the tenant-scoping capability in the same act, so the attestation cannot
		// exist apart from the wiring it describes.
		_ = services.AddTenantAwareStore<IDataInventoryStore, SqlServerDataInventoryStore>();
		services.TryAddSingleton<IDataInventoryStore>(sp => sp.GetRequiredService<SqlServerDataInventoryStore>());
		services.TryAddSingleton<IDataInventoryQueryStore>(sp => sp.GetRequiredService<SqlServerDataInventoryStore>());

		return services;
	}
}
