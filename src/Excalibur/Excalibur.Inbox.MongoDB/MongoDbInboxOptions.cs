// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// Configuration options for the MongoDB inbox store.
/// </summary>
public sealed class MongoDbInboxOptions
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
	/// Gets or sets the collection name for inbox entries.
	/// </summary>
	[Required]
	public string CollectionName { get; set; } = "inbox_messages";

	/// <summary>
	/// Gets or sets the default time to live for inbox entries in seconds.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 7 days (604800 seconds).
	/// Uses MongoDB TTL index on ProcessedAt field.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int DefaultTtlSeconds { get; set; } = 604800;

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
	/// Gets or sets a value indicating whether to advertise transactional (exactly-once) inbox processing.
	/// </summary>
	/// <remarks>
	/// MongoDB multi-document transactions require the server to be deployed as a replica set (or a sharded
	/// cluster). A standalone <c>mongod</c> cannot start a transaction. This flag is opt-in and defaults to
	/// <see langword="false"/> so the store never falsely advertises the atomic handler-plus-mark capability on
	/// a deployment that cannot honour it; set it to <see langword="true"/> only when the target deployment is a
	/// replica set. When <see langword="false"/>, the store reports no transactional capability and callers fall
	/// back to the at-least-once idempotent claim protocol. Even when <see langword="true"/>, starting a
	/// transaction against a non-replica-set server still fails loudly at runtime.
	/// </remarks>
	public bool EnableTransactions { get; set; }

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
