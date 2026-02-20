// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.LeaderElection;
using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB leader election services.
/// </summary>
public static class MongoDbLeaderElectionExtensions
{
	/// <summary>
	/// Adds MongoDB leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="configure">Action to configure the MongoDB leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Requires an <see cref="IMongoClient"/> to be registered in the service collection.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddMongoDbLeaderElection("my-service:leader", options =>
	/// {
	///     options.ConnectionString = "mongodb://localhost:27017";
	///     options.DatabaseName = "myapp";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMongoDbLeaderElection(
		this IServiceCollection services,
		string resourceName,
		Action<MongoDbLeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbLeaderElectionOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			var mongoOptions = sp.GetRequiredService<IOptions<MongoDbLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbLeaderElection>>();
			return new MongoDbLeaderElection(client, resourceName, mongoOptions, electionOptions, logger);
		});
		services.TryAddSingleton<ILeaderElection>(sp => sp.GetRequiredService<MongoDbLeaderElection>());

		return services;
	}

	/// <summary>
	/// Adds MongoDB leader election to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="resourceName">The resource name for the election lock.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbLeaderElection(
		this IServiceCollection services,
		string resourceName,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddMongoDbLeaderElection(resourceName, options =>
		{
			options.ConnectionString = connectionString;
		});
	}
}
