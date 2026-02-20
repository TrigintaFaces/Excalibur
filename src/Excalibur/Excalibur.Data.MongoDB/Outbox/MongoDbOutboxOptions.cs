// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Outbox;

/// <summary>
/// Configuration options for the MongoDB outbox store.
/// </summary>
public sealed class MongoDbOutboxOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = "mongodb://localhost:27017";

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the collection name for outbox messages.
	/// </summary>
	[Required]
	public string CollectionName { get; set; } = "outbox_messages";

	/// <summary>
	/// Gets or sets the default time to live for sent messages in seconds.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 7 days (604800 seconds).
	/// Uses MongoDB TTL index on SentAt field.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int SentMessageTtlSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the server selection timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int ServerSelectionTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int ConnectTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use SSL/TLS.
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
