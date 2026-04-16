// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB data provider settings.
/// </summary>
public interface IMongoDBDataBuilder
{
	/// <summary>Sets the MongoDB connection string.</summary>
	IMongoDBDataBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance.</summary>
	IMongoDBDataBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBDataBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name.</summary>
	IMongoDBDataBuilder DatabaseName(string databaseName);
}
