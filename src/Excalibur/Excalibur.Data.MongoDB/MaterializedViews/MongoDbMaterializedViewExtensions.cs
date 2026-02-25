// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.MongoDB.MaterializedViews;

/// <summary>
/// Extension methods for registering MongoDB materialized view store.
/// </summary>
public static class MongoDbMaterializedViewExtensions
{
	/// <summary>
	/// Configures the materialized view store to use MongoDB.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="configure">Action to configure MongoDB options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseMongoDb(options =>
	///     {
	///         options.ConnectionString = "mongodb://localhost:27017";
	///         options.DatabaseName = "myapp";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseMongoDb(
		this IMaterializedViewsBuilder builder,
		Action<MongoDbMaterializedViewStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<MongoDbMaterializedViewStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder.UseStore<MongoDbMaterializedViewStore>();
	}

	/// <summary>
	/// Configures the materialized view store to use MongoDB with a connection string.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseMongoDb("mongodb://localhost:27017", "myapp");
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseMongoDb(
		this IMaterializedViewsBuilder builder,
		string connectionString,
		string databaseName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return builder.UseMongoDb(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}
}
