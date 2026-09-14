// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;
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
	/// <remarks>
	/// To populate the options from configuration, call
	/// <c>services.AddOptions&lt;PostgresLeaderElectionOptions&gt;().BindConfiguration("Section:Path")</c>
	/// after this registration. Configuration binding is reflective, so it is not trim- or
	/// native-AOT-safe; doing it at your own call site puts the warning where the trimmer can see it
	/// rather than on this method, which is otherwise trim-safe.
	/// </remarks>
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
			// optional fencing-token provider (fencing is on by default; null only when the consumer
			// called WithoutFencingTokens() → no fencing).
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
			return new PostgresLeaderElection(pgOptions, electionOptions, logger, fencingTokenProvider);
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

		// Fencing is on by default: a stalled ex-leader's writes landing after a new leader is
		// elected is silent data corruption, so the safe posture is auto-registering the store's arbitrated
		// provider rather than requiring a second, easily-forgotten AddPostgresFencingTokenProvider() +
		// WithFencingTokens() call. WithoutFencingTokens() opts out. This is the UsePostgres() builder path;
		// the standalone Add*PostgresLeaderElection() entry points are wired separately. The connection
		// string is resolved lazily from options (same as PostgresLeaderElection above) because
		// DataSource/DataSourceFactory/ConnectionStringName only finalize it via PostConfigure.
		builder.Services.TryAddDefaultFencingTokenProvider(sp =>
			new PostgresFencingTokenProvider(sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>().Value.ConnectionString));

		return builder;
	}
}
