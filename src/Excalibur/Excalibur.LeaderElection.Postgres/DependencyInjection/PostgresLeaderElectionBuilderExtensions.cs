// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class PostgresLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the Postgres advisory lock provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the Postgres leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur =&gt;
	/// {
	///     excalibur.AddLeaderElection(le =&gt;
	///     {
	///         le.UsePostgres(pg =&gt;
	///         {
	///             pg.ConnectionString("Host=localhost;Database=MyApp;")
	///               .LockKey(42);
	///         });
	///     });
	/// });
	/// </code>
	/// </example>
	public static ILeaderElectionBuilder UsePostgres(
		this ILeaderElectionBuilder builder,
		Action<IPostgresLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new PostgresLeaderElectionOptions();
		var pgBuilder = new PostgresLeaderElectionBuilder(options);
		configure(pgBuilder);

		var hasBuilderConnection = pgBuilder.DataSourceFactoryFunc is not null
			|| pgBuilder.DataSourceInstance is not null
			|| pgBuilder.ConnectionStringNameValue is not null;

		RegisterOptionsAndServices(builder, pgBuilder, options, hasBuilderConnection);

		return builder.UsePostgresCore();
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		PostgresLeaderElectionBuilder pgBuilder,
		PostgresLeaderElectionOptions options,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<PostgresLeaderElectionOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.LockKey = options.LockKey;
			opt.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
		});

		if (pgBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<PostgresLeaderElectionOptions>()
				.BindConfiguration(pgBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresLeaderElectionOptions>>(
				new PostgresLeaderElectionOptionsValidator { HasBuilderConnection = hasBuilderConnection }));
		builder.Services.AddOptions<PostgresLeaderElectionOptions>().ValidateOnStart();

		// For DataSource/DataSourceFactory/ConnectionStringName, resolve the connection string
		// and set it on the options so that PostgresLeaderElection can use it
		if (pgBuilder.DataSourceInstance is not null)
		{
			var ds = pgBuilder.DataSourceInstance;
			_ = builder.Services.PostConfigure<PostgresLeaderElectionOptions>(opt =>
				opt.ConnectionString = ds.ConnectionString);
		}
		else if (pgBuilder.DataSourceFactoryFunc is not null)
		{
			var factory = pgBuilder.DataSourceFactoryFunc;
			builder.Services.AddSingleton<IPostConfigureOptions<PostgresLeaderElectionOptions>>(sp =>
			{
				var ds = factory(sp);
				return new ConnectionStringNamePostConfigure(ds.ConnectionString);
			});
		}
		else if (pgBuilder.ConnectionStringNameValue is not null)
		{
			var connStrName = pgBuilder.ConnectionStringNameValue;
			builder.Services.AddSingleton<IPostConfigureOptions<PostgresLeaderElectionOptions>>(sp =>
			{
				var config = sp.GetRequiredService<IConfiguration>();
				var resolved = config.GetConnectionString(connStrName)
					?? throw new InvalidOperationException(
						$"Connection string '{connStrName}' not found in IConfiguration.");
				return new ConnectionStringNamePostConfigure(resolved);
			});
		}
	}

	private sealed class ConnectionStringNamePostConfigure : IPostConfigureOptions<PostgresLeaderElectionOptions>
	{
		private readonly string _connectionString;

		internal ConnectionStringNamePostConfigure(string connectionString)
		{
			_connectionString = connectionString;
		}

		public void PostConfigure(string? name, PostgresLeaderElectionOptions options)
		{
			if (string.IsNullOrWhiteSpace(options.ConnectionString))
			{
				options.ConnectionString = _connectionString;
			}
		}
	}

	private static ILeaderElectionBuilder UsePostgresCore(this ILeaderElectionBuilder builder)
	{
		builder.Services.TryAddSingleton(sp =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresLeaderElection>>();
			return new PostgresLeaderElection(pgOptions, electionOptions, logger);
		});
		builder.Services.AddKeyedSingleton<ILeaderElection>("postgres", (sp, _) =>
		{
			var inner = sp.GetRequiredService<PostgresLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Postgres");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("postgres"));

		return builder;
	}
}
