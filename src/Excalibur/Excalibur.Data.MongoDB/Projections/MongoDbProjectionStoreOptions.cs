// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Projections;

/// <summary>
/// Configuration options for the MongoDB projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports connection string configuration with standard MongoDB driver settings.
/// The projection store uses <c>projectionType</c> for efficient queries within projection type boundaries.
/// </para>
/// </remarks>
public sealed class MongoDbProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <value>Defaults to "mongodb://localhost:27017".</value>
	[Required]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>Defaults to "excalibur".</value>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the collection name for projection documents.
	/// </summary>
	/// <value>Defaults to "projections".</value>
	[Required]
	public string CollectionName { get; set; } = "projections";

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to create indexes on initialization.
	/// </summary>
	/// <remarks>
	/// When enabled, the store creates indexes on <c>projectionType</c> and a compound
	/// index on <c>projectionType</c> + <c>projectionId</c> for efficient queries.
	/// </remarks>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateIndexesOnInitialize { get; set; } = true;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required.");
		}

		if (string.IsNullOrWhiteSpace(CollectionName))
		{
			throw new InvalidOperationException("CollectionName is required.");
		}
	}
}
