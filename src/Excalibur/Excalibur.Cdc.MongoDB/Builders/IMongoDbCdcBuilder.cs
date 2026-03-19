// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Driver;

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Fluent builder interface for configuring MongoDB CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures MongoDB-specific CDC options such as connection string,
/// database name, collection names, change stream settings, and state store connections.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
public interface IMongoDbCdcBuilder
{
	/// <summary>
	/// Sets the MongoDB connection string for the CDC source.
	/// </summary>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the database name for CDC processing.
	/// </summary>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder DatabaseName(string databaseName);

	/// <summary>
	/// Sets the collection names to watch for changes.
	/// </summary>
	/// <param name="collectionNames">The collection names to watch.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder CollectionNames(params string[] collectionNames);

	/// <summary>
	/// Sets the unique processor identifier for this CDC instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder ProcessorId(string processorId);

	/// <summary>
	/// Sets the number of changes to process in a single batch.
	/// </summary>
	/// <param name="batchSize">The batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder BatchSize(int batchSize);

	/// <summary>
	/// Sets the interval between reconnection attempts after a failure.
	/// </summary>
	/// <param name="interval">The reconnect interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder ReconnectInterval(TimeSpan interval);

	/// <summary>
	/// Configures a separate connection for CDC state persistence using a connection string.
	/// </summary>
	/// <param name="connectionString">The state store MongoDB connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When omitted, the source connection is used for state persistence (backward compatible).
	/// </para>
	/// </remarks>
	IMongoDbCdcBuilder WithStateStore(string connectionString);

	/// <summary>
	/// Configures a separate connection for CDC state persistence with state store configuration.
	/// </summary>
	/// <param name="connectionString">The state store MongoDB connection string.</param>
	/// <param name="configure">An action to configure state store database/collection settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Configures a separate MongoDB client factory for CDC state persistence.
	/// </summary>
	/// <param name="clientFactory">A factory function that creates a MongoDB client for state storage.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder WithStateStore(Func<IServiceProvider, IMongoClient> clientFactory);

	/// <summary>
	/// Configures a separate MongoDB client factory for CDC state persistence with state store configuration.
	/// </summary>
	/// <param name="clientFactory">A factory function that creates a MongoDB client for state storage.</param>
	/// <param name="configure">An action to configure state store database/collection settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder WithStateStore(
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Binds MongoDB CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:MongoDB").</param>
	/// <returns>The builder for fluent chaining.</returns>
	IMongoDbCdcBuilder BindConfiguration(string sectionPath);
}
