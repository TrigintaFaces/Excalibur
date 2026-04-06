// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class MongoDbLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the MongoDB provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="configure">Action to configure the MongoDB leader election options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IMongoClient"/> to be registered in the service collection.
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburLeaderElection(le =&gt;
	/// {
	///     le.UseMongoDB("my-service:leader", options =&gt;
	///     {
	///         options.ConnectionString = "mongodb://localhost:27017";
	///         options.DatabaseName = "myapp";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ILeaderElectionBuilder UseMongoDB(
		this ILeaderElectionBuilder builder,
		string resourceName,
		Action<MongoDbLeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<MongoDbLeaderElectionOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbLeaderElectionOptions>, MongoDbLeaderElectionOptionsValidator>());

		return builder.UseMongoDBCore(resourceName);
	}

	/// <summary>
	/// Configures the leader election builder to use the MongoDB provider with an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="MongoDbLeaderElectionOptions"/>.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UseMongoDB(
		this ILeaderElectionBuilder builder,
		string resourceName,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<MongoDbLeaderElectionOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbLeaderElectionOptions>, MongoDbLeaderElectionOptionsValidator>());

		return builder.UseMongoDBCore(resourceName);
	}

	/// <summary>
	/// Configures the leader election builder to use the MongoDB provider with a connection string.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburLeaderElection(le =&gt;
	/// {
	///     le.UseMongoDB("my-service:leader", "mongodb://localhost:27017");
	/// });
	/// </code>
	/// </example>
	public static ILeaderElectionBuilder UseMongoDB(
		this ILeaderElectionBuilder builder,
		string resourceName,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return builder.UseMongoDB(resourceName, options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	private static ILeaderElectionBuilder UseMongoDBCore(
		this ILeaderElectionBuilder builder,
		string resourceName)
	{
		builder.Services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			var mongoOptions = sp.GetRequiredService<IOptions<MongoDbLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbLeaderElection>>();
			return new MongoDbLeaderElection(client, resourceName, mongoOptions, electionOptions, logger);
		});
		builder.Services.AddKeyedSingleton<ILeaderElection>("mongodb", (sp, _) =>
		{
			var inner = sp.GetRequiredService<MongoDbLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "MongoDB");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("mongodb"));

		return builder;
	}
}
