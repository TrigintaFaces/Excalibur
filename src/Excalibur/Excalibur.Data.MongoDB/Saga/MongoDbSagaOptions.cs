// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Saga;

/// <summary>
/// Configuration options for the MongoDB saga store.
/// </summary>
/// <remarks>
/// <para>
/// The saga store uses MongoDB document storage with JSON serialization for saga state,
/// enabling flexible state management and efficient upsert operations.
/// </para>
/// <para>
/// Connection pool settings are tuned for typical saga workloads with
/// moderate concurrency and low latency requirements.
/// </para>
/// </remarks>
public sealed class MongoDbSagaOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <value>The connection string for the MongoDB server. Defaults to localhost.</value>
	[Required]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>The database name. Defaults to "excalibur".</value>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the collection name for saga documents.
	/// </summary>
	/// <value>The collection name. Defaults to "sagas".</value>
	[Required]
	public string CollectionName { get; set; } = "sagas";

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// </summary>
	/// <value>The timeout duration. Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>The timeout duration. Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int ConnectTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
	/// </summary>
	/// <value><see langword="true"/> to use SSL/TLS; otherwise, <see langword="false"/>.</value>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	/// <value>The maximum pool size. Defaults to 100 connections.</value>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="ConnectionString"/>, <see cref="DatabaseName"/>, or <see cref="CollectionName"/> is null or whitespace.
	/// </exception>
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
