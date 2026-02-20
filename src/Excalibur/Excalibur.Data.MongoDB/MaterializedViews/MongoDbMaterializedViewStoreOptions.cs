// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.MaterializedViews;

/// <summary>
/// Configuration options for <see cref="MongoDbMaterializedViewStore"/>.
/// </summary>
public sealed class MongoDbMaterializedViewStoreOptions
{
	/// <summary>
	/// Default views collection name.
	/// </summary>
	public const string DefaultViewsCollectionName = "materialized_views";

	/// <summary>
	/// Default positions collection name.
	/// </summary>
	public const string DefaultPositionsCollectionName = "materialized_view_positions";

	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <value>The connection string. Required.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>The database name. Required.</value>
	[Required]
	public string DatabaseName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the collection name for materialized views.
	/// </summary>
	/// <value>The views collection name. Defaults to "materialized_views".</value>
	[Required]
	public string ViewsCollectionName { get; set; } = DefaultViewsCollectionName;

	/// <summary>
	/// Gets or sets the collection name for position tracking.
	/// </summary>
	/// <value>The positions collection name. Defaults to "materialized_view_positions".</value>
	[Required]
	public string PositionsCollectionName { get; set; } = DefaultPositionsCollectionName;

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// </summary>
	/// <value>The timeout in seconds. Defaults to 30.</value>
	[Range(1, int.MaxValue)]
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>The timeout in seconds. Defaults to 10.</value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeoutSeconds { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>The max pool size. Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value><see langword="true"/> to use TLS; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Validates that required options are set.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required for MongoDbMaterializedViewStoreOptions.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required for MongoDbMaterializedViewStoreOptions.");
		}
	}
}
