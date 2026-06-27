// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the canonical
/// CDC builder pattern (see <c>CdcBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class SqlServerLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use SQL Server for leader election.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the SQL Server leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// // Connection string
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	///     le.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionString(configuration.GetConnectionString("LeaderElection")!)
	///            .LockResource("MyApp.Leader");
	///     })));
	///
	/// // Named connection string
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	///     le.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionStringName("LeaderElection")
	///            .LockResource("MyApp.Leader");
	///     })));
	///
	/// // Connection factory (Azure Managed Identity)
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	///     le.UseSqlServer(sql =&gt;
	///     {
	///         sql.ConnectionFactory(sp =&gt;
	///         {
	///             var config = sp.GetRequiredService&lt;IConfiguration&gt;();
	///             var connStr = config.GetConnectionString("LeaderElection")!;
	///             return () =&gt; new SqlConnection(connStr);
	///         })
	///         .LockResource("MyApp.Leader");
	///     })));
	///
	/// // Bind from appsettings.json
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	///     le.UseSqlServer(sql =&gt;
	///     {
	///         sql.BindConfiguration("LeaderElection:SqlServer");
	///     })));
	/// </code>
	/// </example>
	public static ILeaderElectionBuilder UseSqlServer(
		this ILeaderElectionBuilder builder,
		Action<ISqlServerLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options via builder
		var options = new SqlServerLeaderElectionOptions();
		var sqlBuilder = new SqlServerLeaderElectionBuilder(options);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		// Determine whether the builder configured a non-connection-string connection
		var hasBuilderConnection = sqlBuilder.ConnectionFactoryFunc is not null
			|| sqlBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, sqlBuilder, options, connectionFactory, hasBuilderConnection);

		return builder;
	}

	/// <summary>
	/// Configures the leader election builder to use the SQL Server factory provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the SQL Server leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// Use the factory when you need multiple leader elections with different lock resources.
	/// The <see cref="ISqlServerLeaderElectionBuilder.LockResource"/> setting is ignored
	/// for the factory pattern — each election instance specifies its own resource name.
	/// </remarks>
	public static ILeaderElectionBuilder UseSqlServerFactory(
		this ILeaderElectionBuilder builder,
		Action<ISqlServerLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure SQL Server options via builder
		var options = new SqlServerLeaderElectionOptions();
		var sqlBuilder = new SqlServerLeaderElectionBuilder(options);
		configure(sqlBuilder);

		// Determine connection factory based on builder state
		var connectionFactory = ResolveConnectionFactory(sqlBuilder);

		RegisterFactoryServices(builder, connectionFactory);

		return builder;
	}

	/// <summary>
	/// Resolves the connection factory from the builder configuration.
	/// </summary>
	private static Func<IServiceProvider, Func<SqlConnection>> ResolveConnectionFactory(
		SqlServerLeaderElectionBuilder sqlBuilder)
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

		// 3 & 4. Connection string from options (direct or via BindConfiguration)
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	/// <summary>
	/// Registers options, services, and validation for the standard leader election pattern.
	/// </summary>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		SqlServerLeaderElectionBuilder sqlBuilder,
		SqlServerLeaderElectionOptions options,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		bool hasBuilderConnection)
	{
		// Register options from builder state
		_ = builder.Services.Configure<SqlServerLeaderElectionOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.LockResource = options.LockResource;
		});

		// Register BindConfiguration if set
		if (sqlBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<SqlServerLeaderElectionOptions>()
				.BindConfiguration(sqlBuilder.BindConfigurationPath)
				.ValidateOnStart();

			// When ConnectionString() was explicitly called alongside BindConfiguration,
			// re-apply via PostConfigure so the explicit value takes precedence over config.
			if (!string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				var explicitConnectionString = options.ConnectionString;
				_ = builder.Services.PostConfigure<SqlServerLeaderElectionOptions>(opt =>
				{
					opt.ConnectionString = explicitConnectionString;
				});
			}
		}

		// Register ValidateOnStart with connection awareness
		builder.Services.AddSingleton<IValidateOptions<SqlServerLeaderElectionOptions>>(
			new SqlServerLeaderElectionOptionsValidator { HasBuilderConnection = hasBuilderConnection });
		builder.Services.AddOptions<SqlServerLeaderElectionOptions>().ValidateOnStart();

		// Resolve lock resource from options
		var lockResource = options.LockResource!;

		// Register SqlServerLeaderElection using resolved connection factory
		builder.Services.TryAddSingleton(sp =>
		{
			var createConnection = connectionFactory(sp);
			var leOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerLeaderElection>>();

			// Resolve lock resource: use builder value, or fall back to options (from BindConfiguration)
			var resolvedLockResource = !string.IsNullOrWhiteSpace(lockResource)
				? lockResource
				: sp.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>().Value.LockResource
					?? throw new InvalidOperationException(
						"No lock resource configured for LeaderElection. " +
						"Call LockResource(\"MyApp.Leader\") inside UseSqlServer().");

			// The existing SqlServerLeaderElection constructor takes a raw connection string.
			// Resolve the connection string from the factory.
			using var connection = createConnection();
			var connStr = connection.ConnectionString;
			// ot72w3: optional classifier-accelerated self-demotion (null when none registered → grace-only).
			var failureClassifier = sp.GetService<IMessageFailureClassifier>();
			// nxmjpm/ADR-339: optional fencing-token provider (null when WithFencingTokens not enabled → no fencing).
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
			return new SqlServerLeaderElection(connStr, resolvedLockResource, leOptions, logger, failureClassifier, fencingTokenProvider);
		});

		// Register keyed telemetry wrapper
		builder.Services.AddKeyedSingleton<ILeaderElection>("sqlserver", (sp, _) =>
		{
			var inner = sp.GetRequiredService<SqlServerLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "SqlServer");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("sqlserver"));
	}

	/// <summary>
	/// Registers factory services for the multi-election pattern.
	/// </summary>
	private static void RegisterFactoryServices(
		ILeaderElectionBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
	{
		builder.Services.AddKeyedSingleton<ILeaderElectionFactory>("sqlserver", (sp, _) =>
		{
			var createConnection = connectionFactory(sp);
			using var connection = createConnection();
			var connStr = connection.ConnectionString;

			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var failureClassifier = sp.GetService<IMessageFailureClassifier>();
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
				var inner = new SqlServerLeaderElectionFactory(connStr, loggerFactory, failureClassifier, fencingTokenProvider);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "SqlServer");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("sqlserver"));
	}
}
