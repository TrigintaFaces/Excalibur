// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.IdentityMap;
using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the SQL Server identity map store provider.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the canonical
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
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
	/// // Connection string
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("dbo")
	///            .TableName("IdentityMap");
	///     });
	/// });
	///
	/// // Named connection string
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionStringName("IdentityMapDb");
	///     });
	/// });
	///
	/// // Connection factory (Azure Managed Identity)
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionFactory(sp =>
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("IdentityMapDb")!;
	///             return () => new SqlConnection(connStr);
	///         });
	///     });
	/// });
	///
	/// // Populate the options from appsettings.json (your own call site)
	/// services.AddIdentityMap(identity => identity.UseSqlServer(sql => sql.SchemaName("dbo")));
	/// services.AddOptions&lt;SqlServerIdentityMapOptions&gt;().BindConfiguration("IdentityMap:SqlServer");
	/// </code>
	/// </example>
	/// <remarks>
	/// To populate the options from configuration, call
	/// <c>services.AddOptions&lt;SqlServerIdentityMapOptions&gt;().BindConfiguration("Section:Path")</c>
	/// after this registration. Configuration binding is reflective, so it is not trim- or
	/// native-AOT-safe; doing it at your own call site puts the warning where the trimmer can see it
	/// rather than on this method, which is otherwise trim-safe.
	/// </remarks>
	public static IIdentityMapBuilder UseSqlServer(
		this IIdentityMapBuilder builder,
		Action<ISqlServerIdentityMapBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SqlServerIdentityMapOptions();
		var sqlBuilder = new SqlServerIdentityMapBuilder(options);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		// Determine whether the builder configured a non-connection-string connection
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		// Register options from builder state
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

		// Register ValidateOnStart with connection awareness
		builder.Services.AddSingleton<IValidateOptions<SqlServerIdentityMapOptions>>(
			new SqlServerIdentityMapOptionsValidator { HasBuilderConnection = hasBuilderConnection });

		// Register store using resolved connection factory
		RegisterStore(builder.Services, connectionFactory, hasBuilderConnection);

		// Register health checks if enabled
		if (sqlBuilder.HealthChecksEnabled && !string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			_ = builder.Services.AddHealthChecks()
				.AddSqlServer(
					options.ConnectionString,
					name: sqlBuilder.HealthCheckName,
					tags: ["identitymap", "sqlserver"]);
		}

		return builder;
	}

	/// <summary>
	/// Resolves the connection factory from the builder configuration.
	/// </summary>
	private static Func<IServiceProvider, Func<SqlConnection>>? ResolveConnectionFactory(
		SqlServerIdentityMapBuilder sqlBuilder)
	{
		// 1. Explicit factory takes highest precedence
		if (sqlBuilder.ConnectionFactoryFunc is not null)
		{
			return sqlBuilder.ConnectionFactoryFunc;
		}

		// 2. Named connection string resolved from IConfiguration
		if (sqlBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = sqlBuilder.ConnectionStringNameValue;
			return sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration. " +
						$"Ensure it exists in the ConnectionStrings section of your configuration.");
				return () => new SqlConnection(resolved);
			};
		}

		// 3 & 4. Connection string from options —
		// handled by the default store constructor that reads from IOptions
		return null;
	}

	private static void RegisterStore(
		IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>>? connectionFactory,
		bool hasBuilderConnection)
	{
		if (connectionFactory is not null)
		{
			// Factory or named connection string — use the factory-aware constructor
			services.TryAddSingleton(sp =>
			{
				var factory = connectionFactory(sp);
				var opts = sp.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
				var logger = sp.GetRequiredService<ILogger<SqlServerIdentityMapStore>>();
				return new SqlServerIdentityMapStore(factory, opts, logger);
			});
			services.TryAddSingleton<IIdentityMapStore>(sp =>
			{
				var inner = sp.GetRequiredService<SqlServerIdentityMapStore>();
				var meterFactory = sp.GetService<System.Diagnostics.Metrics.IMeterFactory>();
				return new Excalibur.Data.IdentityMap.Diagnostics.TelemetryIdentityMapStoreDecorator(inner, meterFactory);
			});
		}
		else
		{
			// Connection string from options — use the default constructor
			services.TryAddSingleton<SqlServerIdentityMapStore>();
			services.TryAddSingleton<IIdentityMapStore>(sp =>
			{
				var inner = sp.GetRequiredService<SqlServerIdentityMapStore>();
				var meterFactory = sp.GetService<System.Diagnostics.Metrics.IMeterFactory>();
				return new Excalibur.Data.IdentityMap.Diagnostics.TelemetryIdentityMapStoreDecorator(inner, meterFactory);
			});
		}
	}
}
