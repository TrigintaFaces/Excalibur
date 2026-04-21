// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.LeaderElection.MongoDB;

internal sealed class MongoDBLeaderElectionBuilder : IMongoDBLeaderElectionBuilder
{
	private readonly MongoDbLeaderElectionOptions _options;

	internal MongoDBLeaderElectionBuilder(MongoDbLeaderElectionOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal IMongoClient? ClientInstance { get; private set; }
	internal Func<IServiceProvider, IMongoClient>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IMongoDBLeaderElectionBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		ClientInstance = null;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IMongoDBLeaderElectionBuilder Client(IMongoClient client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		_options.ConnectionString = null!;
		ClientFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IMongoDBLeaderElectionBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		_options.ConnectionString = null!;
		ClientInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IMongoDBLeaderElectionBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null!;
		ClientInstance = null;
		ClientFactoryFunc = null;
		return this;
	}

	public IMongoDBLeaderElectionBuilder DatabaseName(string databaseName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		_options.DatabaseName = databaseName;
		return this;
	}

	public IMongoDBLeaderElectionBuilder CollectionName(string collectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
		_options.CollectionName = collectionName;
		return this;
	}
}
