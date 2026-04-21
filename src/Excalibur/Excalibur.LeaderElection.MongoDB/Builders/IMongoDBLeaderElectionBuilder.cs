// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.LeaderElection.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB leader election settings.
/// </summary>
public interface IMongoDBLeaderElectionBuilder
{
	/// <summary>Sets the MongoDB connection string.</summary>
	IMongoDBLeaderElectionBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance.</summary>
	IMongoDBLeaderElectionBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBLeaderElectionBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBLeaderElectionBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name. Default: "excalibur".</summary>
	IMongoDBLeaderElectionBuilder DatabaseName(string databaseName);

	/// <summary>Sets the leader election collection name. Default: "leader_elections".</summary>
	IMongoDBLeaderElectionBuilder CollectionName(string collectionName);
}
