// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerErasureStore>();
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());

		return services;
	}

	/// <summary>
	/// Adds the SQL Server erasure store to the service collection with a connection string.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The SQL Server connection string. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Uses default options with auto-schema creation enabled. For custom schema/table names, use the overload that accepts a configure delegate.
	/// </remarks>
	public static IServiceCollection AddSqlServerErasureStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerErasureStore(options =>
		{
			options.ConnectionString = connectionString;
		});
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
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<SqlServerErasureStore>();
		services.TryAddSingleton<IErasureStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureCertificateStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());
		services.TryAddSingleton<IErasureQueryStore>(sp => sp.GetRequiredService<SqlServerErasureStore>());

		return services;
	}
}
