// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.MongoDB;

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
	private const string BuilderManagedConnectionSentinel = "mongodb://builder-managed-client";

	/// <summary>
	/// Configures the leader election builder to use the MongoDB provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="configure">Action to configure MongoDB leader election settings via the fluent builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="resourceName"/> is null or whitespace.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburLeaderElection(le =&gt;
	/// {
	///     le.UseMongoDB("my-service:leader", mongo =&gt;
	///     {
	///         mongo.ConnectionString("mongodb://localhost:27017")
	///              .DatabaseName("myapp");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ILeaderElectionBuilder UseMongoDB(
		this ILeaderElectionBuilder builder,
		string resourceName,
		Action<IMongoDBLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new MongoDbLeaderElectionOptions();
		var mongoBuilder = new MongoDBLeaderElectionBuilder(options);
		configure(mongoBuilder);

		var hasBuilderConnection = mongoBuilder.ClientInstance is not null
			|| mongoBuilder.ClientFactoryFunc is not null;

		if (hasBuilderConnection)
		{
			options.ConnectionString = BuilderManagedConnectionSentinel;
		}

		RegisterOptionsAndServices(builder, mongoBuilder, options, resourceName, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		MongoDBLeaderElectionBuilder mongoBuilder,
		MongoDbLeaderElectionOptions options,
		string resourceName,
		bool hasBuilderConnection)
	{
		_ = builder.Services.Configure<MongoDbLeaderElectionOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.DatabaseName = options.DatabaseName;
			opt.CollectionName = options.CollectionName;
		});

		if (mongoBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<MongoDbLeaderElectionOptions>()
				.BindConfiguration(mongoBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbLeaderElectionOptions>, MongoDbLeaderElectionOptionsValidator>());
		builder.Services.AddOptions<MongoDbLeaderElectionOptions>().ValidateOnStart();

		if (hasBuilderConnection)
		{
			if (mongoBuilder.ClientInstance is not null)
			{
				var client = mongoBuilder.ClientInstance;
				builder.Services.TryAddSingleton<IMongoClient>(client);
			}
			else if (mongoBuilder.ClientFactoryFunc is not null)
			{
				var factory = mongoBuilder.ClientFactoryFunc;
				builder.Services.TryAddSingleton<IMongoClient>(factory);
			}
		}

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
	}
}
