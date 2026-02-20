// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Inbox;

/// <summary>
/// Configuration options for the Cosmos DB inbox store.
/// </summary>
public sealed class CosmosDbInboxOptions
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
	/// Gets or sets the container name for inbox messages.
	/// </summary>
	[Required]
	public string ContainerName { get; set; } = "inbox-messages";

	/// <summary>
	/// Gets or sets the partition key path.
	/// </summary>
	/// <remarks>
	/// Uses handler_type as partition key for optimal query patterns where
	/// messages are typically queried by handler type.
	/// </remarks>
	[Required]
	public string PartitionKeyPath { get; set; } = "/handler_type";

	/// <summary>
	/// Gets or sets the consistency level for operations.
	/// </summary>
	public ConsistencyLevel? ConsistencyLevel { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry attempts for transient failures.
	/// </summary>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 9;

	/// <summary>
	/// Gets or sets the maximum retry wait time in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxRetryWaitTimeInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int RequestTimeoutInSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets the default time to live for documents in seconds.
	/// </summary>
	/// <remarks>
	/// Set to -1 for no expiration. Defaults to 7 days (604800 seconds).
	/// </remarks>
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets a value indicating whether to use direct connection mode.
	/// </summary>
	public bool UseDirectMode { get; set; } = true;

	/// <summary>
	/// Gets or sets the preferred regions for geo-redundant operations.
	/// </summary>
	public IReadOnlyList<string>? PreferredRegions { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content response on write operations.
	/// </summary>
	/// <remarks>
	/// Setting this to false reduces RU consumption for write operations.
	/// </remarks>
	public bool EnableContentResponseOnWrite { get; set; }

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
