// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB inbox store settings.
/// </summary>
public interface IMongoDBInboxBuilder
{
	/// <summary>Sets the MongoDB connection string.</summary>
	IMongoDBInboxBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance.</summary>
	IMongoDBInboxBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBInboxBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBInboxBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name. Default: "excalibur".</summary>
	IMongoDBInboxBuilder DatabaseName(string databaseName);

	/// <summary>Sets the inbox collection name. Default: "inbox_messages".</summary>
	IMongoDBInboxBuilder CollectionName(string collectionName);
}
