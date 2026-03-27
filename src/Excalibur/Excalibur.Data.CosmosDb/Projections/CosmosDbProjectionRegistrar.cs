// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.CosmosDb.Projections;

/// <summary>
/// Registrar for adding multiple Cosmos DB projection stores that share
/// a common connection string and database name. Used with
/// <see cref="CosmosDbProjectionStoreExtensions.AddCosmosDbProjections"/>.
/// </summary>
/// <remarks>
/// Each projection type gets its own options instance, so per-projection
/// overrides (container name, partition key, etc.) are fully isolated.
/// </remarks>
public sealed class CosmosDbProjectionRegistrar
{
	private readonly IServiceCollection _services;
	private readonly string _connectionString;
	private readonly string _databaseName;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbProjectionRegistrar"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The shared Cosmos DB connection string.</param>
	/// <param name="databaseName">The shared Cosmos DB database name.</param>
	internal CosmosDbProjectionRegistrar(IServiceCollection services, string connectionString, string databaseName)
	{
		_services = services;
		_connectionString = connectionString;
		_databaseName = databaseName;
	}

	/// <summary>
	/// Adds a projection store for the specified type using the shared connection string and database.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="configureOptions">Optional action to override per-projection options (e.g., container name).</param>
	/// <returns>This registrar for fluent chaining.</returns>
	public CosmosDbProjectionRegistrar Add<TProjection>(
		Action<CosmosDbProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		_services.AddCosmosDbProjectionStore<TProjection>(options =>
		{
			options.Client.ConnectionString = _connectionString;
			options.DatabaseName = _databaseName;
			configureOptions?.Invoke(options);
		});

		return this;
	}
}
