// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerLegalHoldStore>();
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());

		return services;
	}

	/// <summary>
	/// Adds the SQL Server legal hold store with a connection string.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionString"> The SQL Server connection string. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSqlServerLegalHoldStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddSqlServerLegalHoldStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Adds the SQL Server legal hold store with connection string from configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="connectionStringName"> The connection string name from configuration. </param>
	/// <param name="configure"> Optional additional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
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
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<SqlServerLegalHoldStore>();
		services.TryAddSingleton<ILegalHoldStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());
		services.TryAddSingleton<ILegalHoldQueryStore>(sp => sp.GetRequiredService<SqlServerLegalHoldStore>());

		return services;
	}
}
