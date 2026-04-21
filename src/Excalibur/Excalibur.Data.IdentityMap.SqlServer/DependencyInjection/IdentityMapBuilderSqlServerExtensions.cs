// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

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
	/// // Bind from appsettings.json
	/// services.AddIdentityMap(identity =>
	/// {
	///     identity.UseSqlServer(sql =>
	///     {
	///         sql.BindConfiguration("IdentityMap:SqlServer");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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

		// Register BindConfiguration if set
		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerIdentityMapOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerIdentityMapOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

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

		// 3 & 4. Connection string from options (direct or via BindConfiguration) —
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
			// Connection string or BindConfiguration — use the default constructor
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
