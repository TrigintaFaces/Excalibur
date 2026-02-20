// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Snapshots;

/// <summary>
/// Configuration options for the Cosmos DB snapshot store.
/// </summary>
public sealed class CosmosDbSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the Cosmos DB account endpoint URI.
	/// </summary>
	public string? AccountEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the Cosmos DB account key.
	/// </summary>
	public string? AccountKey { get; set; }

	/// <summary>
	/// Gets or sets the connection string (alternative to AccountEndpoint + AccountKey).
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the container name for snapshots.
	/// </summary>
	/// <value>Defaults to "snapshots".</value>
	[Required]
	public string ContainerName { get; set; } = "snapshots";

	/// <summary>
	/// Gets or sets the partition key path.
	/// </summary>
	/// <remarks>
	/// Uses aggregateType as partition key for efficient queries within aggregate type boundaries.
	/// </remarks>
	/// <value>Defaults to "/aggregateType".</value>
	[Required]
	public string PartitionKeyPath { get; set; } = "/aggregateType";

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets the throughput for the container when created.
	/// </summary>
	/// <value>Defaults to 400 RU/s.</value>
	[Range(1, int.MaxValue)]
	public int ContainerThroughput { get; set; } = 400;

	/// <summary>
	/// Gets or sets the default time to live for snapshots in seconds.
	/// </summary>
	/// <remarks>
	/// Set to -1 for no expiration (default). Set to a positive value to enable automatic cleanup
	/// of old snapshots. Per-document TTL can be set via the Ttl property on individual documents.
	/// </remarks>
	/// <value>Defaults to -1 (no expiration).</value>
	public int DefaultTtlSeconds { get; set; } = -1;

	/// <summary>
	/// Gets or sets the consistency level for operations.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (use account default).</value>
	public ConsistencyLevel? ConsistencyLevel { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry attempts for transient failures.
	/// </summary>
	/// <value>Defaults to 9.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 9;

	/// <summary>
	/// Gets or sets the maximum retry wait time in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryWaitTimeInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int RequestTimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to use direct connection mode.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseDirectMode { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable content response on write operations.
	/// </summary>
	/// <remarks>
	/// Setting this to false reduces RU consumption for write operations.
	/// </remarks>
	/// <value>Defaults to <see langword="false"/> for performance.</value>
	public bool EnableContentResponseOnWrite { get; set; }

	/// <summary>
	/// Gets or sets the preferred regions for geo-redundant operations.
	/// </summary>
	public IReadOnlyList<string>? PreferredRegions { get; set; }

	/// <summary>
	/// Gets or sets a factory function for creating the HttpClient used by the Cosmos DB client.
	/// </summary>
	/// <remarks>
	/// This is primarily used for testing with the Cosmos DB emulator, which uses self-signed certificates.
	/// The TestContainers CosmosDb container provides an HttpClient that bypasses certificate validation.
	/// </remarks>
	public Func<HttpClient>? HttpClientFactory { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
		var hasEndpointAndKey = !string.IsNullOrWhiteSpace(AccountEndpoint) && !string.IsNullOrWhiteSpace(AccountKey);

		if (!hasConnectionString && !hasEndpointAndKey)
		{
			throw new InvalidOperationException(
				ErrorMessages.EitherConnectionStringOrAccountEndpointRequired);
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException(ErrorMessages.DatabaseNameRequired);
		}

		if (string.IsNullOrWhiteSpace(ContainerName))
		{
			throw new InvalidOperationException(ErrorMessages.ContainerNameRequired);
		}
	}
}
