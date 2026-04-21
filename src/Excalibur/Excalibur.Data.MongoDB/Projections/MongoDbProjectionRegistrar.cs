// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.MongoDB.Projections;

/// <summary>
/// Registrar for adding multiple MongoDB projection stores that share
/// a common connection string and database name. Used with
/// <c>AddMongoDbProjections</c>.
/// </summary>
/// <remarks>
/// Each projection type gets its own options instance, so per-projection
/// overrides (collection name, timeouts, etc.) are fully isolated.
/// </remarks>
public sealed class MongoDbProjectionRegistrar
{
	private readonly IServiceCollection _services;
	private readonly string? _connectionString;
	private readonly string? _databaseName;
	private readonly Action<MongoDbProjectionStoreOptions>? _configureShared;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbProjectionRegistrar"/> class
	/// with explicit connection string and database name.
	/// </summary>
	internal MongoDbProjectionRegistrar(IServiceCollection services, string connectionString, string databaseName)
	{
		_services = services;
		_connectionString = connectionString;
		_databaseName = databaseName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbProjectionRegistrar"/> class
	/// with a shared options configuration action.
	/// </summary>
	internal MongoDbProjectionRegistrar(IServiceCollection services, Action<MongoDbProjectionStoreOptions> configureShared)
	{
		_services = services;
		_configureShared = configureShared;
	}

	/// <summary>
	/// Adds a projection store for the specified type using the shared configuration.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="configureOptions">Optional action to override per-projection options (e.g., collection name).</param>
	/// <returns>This registrar for fluent chaining.</returns>
	public MongoDbProjectionRegistrar Add<TProjection>(
		Action<MongoDbProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		_services.AddMongoDbProjectionStore<TProjection>(options =>
		{
			if (_configureShared != null)
			{
				_configureShared(options);
			}
			else
			{
				options.ConnectionString = _connectionString!;
				options.DatabaseName = _databaseName!;
			}

			configureOptions?.Invoke(options);
		});

		return this;
	}
}
