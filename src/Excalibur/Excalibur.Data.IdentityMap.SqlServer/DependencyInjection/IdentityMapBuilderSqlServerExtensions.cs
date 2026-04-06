// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap;
using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the SQL Server identity map store provider.
/// </summary>
public static class IdentityMapBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the identity map store to use SQL Server as the backing store.
	/// </summary>
	/// <param name="builder">The identity map builder.</param>
	/// <param name="configure">The SQL Server configuration action.</param>
	/// <returns>The identity map builder for method chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("dbo")
	///            .TableName("IdentityMap");
	///     });
	/// });
	/// </code>
	/// </example>
	public static IIdentityMapBuilder UseSqlServer(
		this IIdentityMapBuilder builder,
		Action<ISqlServerIdentityMapBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerIdentityMapOptions();
		var sqlBuilder = new SqlServerIdentityMapBuilder(options);
		configure(sqlBuilder);

		builder.Services.AddOptions<SqlServerIdentityMapOptions>()
			.Configure(opt =>
			{
				opt.ConnectionString = options.ConnectionString;
				opt.SchemaName = options.SchemaName;
				opt.TableName = options.TableName;
				opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
				opt.MaxBatchSize = options.MaxBatchSize;
			})
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerIdentityMapOptions>,
				SqlServerIdentityMapOptionsValidator>());

		builder.Services.TryAddSingleton<IIdentityMapStore, SqlServerIdentityMapStore>();

		return builder;
	}
}
