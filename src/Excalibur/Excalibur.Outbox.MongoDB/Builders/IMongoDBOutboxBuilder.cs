// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Outbox.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB outbox store settings.
/// </summary>
public interface IMongoDBOutboxBuilder
{
	/// <summary>Sets the MongoDB connection string.</summary>
	IMongoDBOutboxBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance.</summary>
	IMongoDBOutboxBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBOutboxBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBOutboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name. Default: "excalibur".</summary>
	IMongoDBOutboxBuilder DatabaseName(string databaseName);

	/// <summary>Sets the outbox collection name. Default: "outbox_messages".</summary>
	IMongoDBOutboxBuilder CollectionName(string collectionName);
}
