// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Configuration options for the MongoDB CDC processor using Change Streams.
/// </summary>
public sealed class MongoDbCdcOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <remarks>
	/// For Change Streams, the MongoDB deployment must be a replica set or sharded cluster.
	/// </remarks>
	[Required]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	/// Gets or sets the database name to watch for changes.
	/// </summary>
	/// <remarks>
	/// If null, watches changes across all databases.
	/// </remarks>
	public string? DatabaseName { get; set; }

	/// <summary>
	/// Gets or sets the collection names to watch for changes.
	/// </summary>
	/// <remarks>
	/// If empty, watches all collections in the database (or all collections in all databases
	/// if <see cref="DatabaseName"/> is also null).
	/// </remarks>
	public string[] CollectionNames { get; set; } = [];

	/// <summary>
	/// Gets or sets the unique identifier for this CDC processor instance.
	/// </summary>
	/// <remarks>
	/// Used for state tracking and distinguishing multiple processors.
	/// </remarks>
	[Required]
	public string ProcessorId { get; set; } = "default";

	/// <summary>
	/// Gets or sets the number of changes to process in a single batch.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum time to wait for new changes.
	/// </summary>
	/// <remarks>
	/// This controls the getMore behavior. If no documents are available,
	/// the server will return an empty batch after this time.
	/// </remarks>
	public TimeSpan MaxAwaitTime { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the interval between reconnection attempts after a failure.
	/// </summary>
	public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets whether to include the full document in change events.
	/// </summary>
	/// <remarks>
	/// When true, update operations include the full document after the change.
	/// When false, only the delta is included (default MongoDB behavior).
	/// </remarks>
	public bool FullDocument { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to include pre-image of the document (MongoDB 6.0+).
	/// </summary>
	/// <remarks>
	/// Requires change stream pre and post images to be enabled on the collection.
	/// </remarks>
	public bool FullDocumentBeforeChange { get; set; }

	/// <summary>
	/// Gets or sets the operation types to watch for.
	/// </summary>
	/// <remarks>
	/// If empty, watches all operation types. Valid values: insert, update, replace, delete, invalidate.
	/// </remarks>
	public string[] OperationTypes { get; set; } = [];

	/// <summary>
	/// Gets or sets the server selection timeout.
	/// </summary>
	public TimeSpan ServerSelectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the connection timeout.
	/// </summary>
	public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether to use SSL/TLS.
	/// </summary>
	public bool UseSsl { get; set; }

	/// <summary>
	/// Gets or sets the maximum connection pool size.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing or invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(ProcessorId))
		{
			throw new InvalidOperationException("ProcessorId is required.");
		}

		if (BatchSize <= 0)
		{
			throw new InvalidOperationException("BatchSize must be greater than 0.");
		}

		if (MaxAwaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException("MaxAwaitTime must be positive.");
		}
	}
}
