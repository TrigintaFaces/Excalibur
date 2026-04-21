// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.EventSourcing.MongoDB;

/// <summary>
/// Fluent builder for configuring MongoDB event sourcing settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides 4 canonical connection overloads plus feature-specific configuration.
/// Connection overloads are mutually exclusive (last-wins).
/// <see cref="IMongoClient"/> is registered as a singleton (thread-safe, expensive to create).
/// </para>
/// </remarks>
public interface IMongoDBEventSourcingBuilder
{
	/// <summary>Sets the MongoDB connection string. Creates an internal <see cref="IMongoClient"/> singleton.</summary>
	IMongoDBEventSourcingBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IMongoClient"/> instance directly.</summary>
	IMongoDBEventSourcingBuilder Client(IMongoClient client);

	/// <summary>Sets a factory that resolves an <see cref="IMongoClient"/> from DI.</summary>
	IMongoDBEventSourcingBuilder ClientFactory(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IMongoDBEventSourcingBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the database name. Default: "excalibur".</summary>
	IMongoDBEventSourcingBuilder DatabaseName(string databaseName);

	/// <summary>Sets the event store collection name. Default: "event_store_events".</summary>
	IMongoDBEventSourcingBuilder CollectionName(string collectionName);

	/// <summary>Sets the counter collection name. Default: "event_store_counters".</summary>
	IMongoDBEventSourcingBuilder CounterCollectionName(string counterCollectionName);
}
