// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Saga.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB saga store settings.
/// </summary>
public interface IMongoDBSagaBuilder
{
	/// <summary>Sets the MongoDB connection string.</summary>
	IMongoDBSagaBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance.</summary>
	IMongoDBSagaBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBSagaBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBSagaBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name. Default: "excalibur".</summary>
	IMongoDBSagaBuilder DatabaseName(string databaseName);

	/// <summary>Sets the saga collection name. Default: "sagas".</summary>
	IMongoDBSagaBuilder CollectionName(string collectionName);
}
