// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server erasure store services.
/// </summary>
public static class SqlServerErasureStoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds the SQL Server erasure store to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> A delegate to configure the options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// This registers <see cref="SqlServerErasureStore" /> as the <see cref="IErasureStore" /> implementation for production use. The store
	/// automatically creates the required schema and tables if <see cref="SqlServerErasureStoreOptions.AutoCreateSchema" /> is true.
	/// </remarks>
	public static IServiceCollection AddSqlServerErasureStore(
		this IServiceCollection services,
		Action<SqlServerErasureStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SqlServerErasureStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerErasureStoreOptions>,
				SqlServerErasureStoreOptionsValidator>());

		_ = services.AddDataSubjectHashing(); // store pseudonymizes data-subject ids (B3).
		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<IErasureStore> marker in the same act. The
		// marker is not separately registerable, so a store that was never handed the context cannot carry
		// a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantAwareStore<IErasureStore, SqlServerErasureStore>(sp =>
			new SqlServerErasureStore(
				sp.GetRequiredService<IOptions<SqlServerErasureStoreOptions>>(),
				sp.GetRequiredService<IDataSubjectHasher>(),
				sp.GetRequiredService<ILogger<SqlServerErasureStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());

		// Provisioning is a configuration concern, so it is settled once at host startup rather than on the
		// path of every write. The hosted service verifies this store's schema before the host accepts
		// traffic, so a mis-provisioned deployment fails to start instead of reporting a deployment fault
		// as the failure of one data subject's erasure request.
		services.AddSingleton<IErasureSchemaValidator>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		_ = services.AddErasureSchemaValidation();

		return services;
	}

	/// <summary>
	/// Adds the SQL Server erasure store with connection string from configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionStringName"> The connection string name from configuration. </param>
	/// <param name="configure"> Optional additional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// This overload retrieves the connection string from IConfiguration at runtime. The connection string should be configured under "ConnectionStrings:{connectionStringName}".
	/// </para>
	/// <para> Example configuration:
	/// <code>
	///{
	///"ConnectionStrings": {
	///"Compliance": "Server=...;Database=...;..."
	///}
	///}
	/// </code>
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerErasureStoreFromConfiguration(
		this IServiceCollection services,
		string connectionStringName,
		Action<SqlServerErasureStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

		_ = services.AddOptions<SqlServerErasureStoreOptions>()
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
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerErasureStoreOptions>,
				SqlServerErasureStoreOptionsValidator>());

		// The fail-closed single-tenant default guarantees a non-null ambient context; the multi-tenancy
		// composition replaces it with the resolver-driven one.
		_ = services.AddDefaultTenantContext();

		// AddTenantAwareStore builds the store WITH the ambient tenant context (this store's constructor
		// declares one) and emits the ITenantScopingCapability<IErasureStore> marker in the same act. The
		// marker is not separately registerable, so a store that was never handed the context cannot carry
		// a truthful-looking capability and pass the multi-tenancy gate.
		_ = services.AddTenantAwareStore<IErasureStore, SqlServerErasureStore>(sp =>
			new SqlServerErasureStore(
				sp.GetRequiredService<IOptions<SqlServerErasureStoreOptions>>(),
				sp.GetRequiredService<IDataSubjectHasher>(),
				sp.GetRequiredService<ILogger<SqlServerErasureStore>>(),
				sp.GetRequiredService<ITenantContext>(),
				sp.GetRequiredService<IOptions<TenantContextOptions>>()));
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());

		// Provisioning is a configuration concern, so it is settled once at host startup rather than on the
		// path of every write. The hosted service verifies this store's schema before the host accepts
		// traffic, so a mis-provisioned deployment fails to start instead of reporting a deployment fault
		// as the failure of one data subject's erasure request.
		services.AddSingleton<IErasureSchemaValidator>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		_ = services.AddErasureSchemaValidation();

		return services;
	}
}
