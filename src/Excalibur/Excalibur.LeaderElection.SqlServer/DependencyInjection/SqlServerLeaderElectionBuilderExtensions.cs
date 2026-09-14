// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
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
	/// // Populate the options from appsettings.json (your own call site)
	/// services.AddOptions&lt;SqlServerLeaderElectionOptions&gt;().BindConfiguration("LeaderElection:SqlServer");
	/// </code>
	/// </example>
	/// <remarks>
	/// To populate the options from configuration, call
	/// <c>services.AddOptions&lt;SqlServerLeaderElectionOptions&gt;().BindConfiguration("Section:Path")</c>
	/// after this registration. Configuration binding is reflective, so it is not trim- or
	/// native-AOT-safe; doing it at your own call site puts the warning where the trimmer can see it
	/// rather than on this method, which is otherwise trim-safe.
	/// </remarks>
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

		RegisterOptionsAndServices(builder, options, connectionFactory, hasBuilderConnection);

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

		// Register options with the ConnectionString() builder value, so ResolveConnectionFactory's
		// options-fallback branch (used whenever the consumer called .ConnectionString(...) rather than
		// .ConnectionFactory(...) or .ConnectionStringName(...)) has something to read. Without this,
		// IOptions<SqlServerLeaderElectionOptions> is never configured on the factory path and
		// connectionFactory(sp) resolves an empty connection string.
		_ = builder.Services.Configure<SqlServerLeaderElectionOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.LockResource = options.LockResource;
		});

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

		// 3 & 4. Connection string from options
		return sp =>
		{
			var opts = sp.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
			return () => new SqlConnection(opts.Value.ConnectionString);
		};
	}

	/// <summary>
	/// Registers options, services, and validation for the standard leader election pattern.
	/// </summary>
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
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

			// Resolve lock resource: use builder value, or fall back to options (bound by the consumer)
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
			// optional classifier-accelerated self-demotion (null when none registered → grace-only).
			var failureClassifier = sp.GetService<IMessageFailureClassifier>();
			// optional fencing-token provider (null when WithFencingTokens not enabled → no fencing).
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

		RegisterDefaultFencingTokenProvider(builder.Services, connectionFactory);
		RegisterOutboxGate(builder.Services);
	}

	/// <summary>
	/// Matches Consul/Kubernetes/InMemory/Postgres/MongoDB/Redis and the sibling
	/// <c>AddSqlServerHealthBasedLeaderElection()</c>, all of which register this. Without it a consumer
	/// wiring <see cref="UseSqlServer"/> or <see cref="UseSqlServerFactory"/> plus an outbox hits the
	/// outbox's own startup refusal for no reason a SQL Server consumer would expect versus the
	/// health-based path. Split out of the callers to keep their class coupling (CA1506) in bounds, same
	/// rationale as <see cref="RegisterDefaultFencingTokenProvider"/> below.
	/// </summary>
	private static void RegisterOutboxGate(IServiceCollection services) =>
		OutboxBuilderLeaderElectionExtensions.RegisterOutboxLeaderGate(services);

	/// <summary>
	/// Fencing is on by default: a stalled ex-leader's writes landing after a new leader is elected
	/// is silent data corruption, so the safe posture is auto-registering the store's arbitrated provider
	/// rather than requiring a second, easily-forgotten <c>AddSqlServerFencingTokenProvider()</c> +
	/// <c>WithFencingTokens()</c> call. <c>WithoutFencingTokens()</c> opts out. Split out of the callers to
	/// keep their class coupling (CA1506) in bounds.
	/// </summary>
	private static void RegisterDefaultFencingTokenProvider(
		IServiceCollection services,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory) =>
		services.TryAddDefaultFencingTokenProvider(sp =>
			new SqlServerFencingTokenProvider(ResolveConnectionString(connectionFactory, sp)));

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
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var failureClassifier = sp.GetService<IMessageFailureClassifier>();
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
				var inner = new SqlServerLeaderElectionFactory(connStr, loggerFactory, electionOptions, failureClassifier, fencingTokenProvider);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "SqlServer");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("sqlserver"));

		RegisterDefaultFencingTokenProvider(builder.Services, connectionFactory);

		// Deliberately NOT wired here, unlike UseSqlServer()/UseMongoDB()/UseRedis(): MEASURED (not
		// assumed) that the factory pattern registers ONLY a keyed ILeaderElectionFactory
		// ("sqlserver"/"default") and NEVER an unkeyed ILeaderElection -- there is no single canonical
		// election for a multi-lock factory to expose. Calling RegisterOutboxGate here would register gate
		// factories that can NEVER resolve (GetRequiredService<ILeaderElection>() always throws), which is
		// WORSE than not registering them: the outbox's own startup backstop checks
		// IServiceProviderIsService, which only asks "is a descriptor registered", not "would resolving it
		// succeed" -- so a pre-wired-but-unsatisfiable gate would make that clean, actionable startup
		// refusal disappear, replaced by a raw DI exception the first time the outbox actually drains.
		// See OutboxLeaderGateAutoRegistersByDefaultShould's negative arm.
	}

	/// <summary>
	/// Opens (and immediately disposes) a connection from <paramref name="connectionFactory"/> purely to
	/// read its resolved connection string, mirroring the pattern the election and factory registrations
	/// above already use — <see cref="SqlServerFencingTokenProvider"/>'s constructor takes a raw connection
	/// string, not a factory.
	/// </summary>
	private static string ResolveConnectionString(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory,
		IServiceProvider sp)
	{
		var createConnection = connectionFactory(sp);
		using var connection = createConnection();
		return connection.ConnectionString;
	}
}
