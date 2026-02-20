// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Configuration options for the Cosmos DB outbox store.
/// </summary>
public sealed class CosmosDbOutboxOptions
{
	/// <summary>
	/// Gets or sets the Azure Cosmos DB connection string.
	/// </summary>
	/// <remarks>
	/// Either <see cref="ConnectionString"/> or both <see cref="AccountEndpoint"/> and <see cref="AccountKey"/> must be provided.
	/// </remarks>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the Azure Cosmos DB account endpoint.
	/// </summary>
	public string? AccountEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the Azure Cosmos DB account key.
	/// </summary>
	public string? AccountKey { get; set; }

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	public string? DatabaseName { get; set; }

	/// <summary>
	/// Gets or sets the outbox container name.
	/// </summary>
	/// <value>Defaults to "outbox".</value>
	[Required]
	public string ContainerName { get; set; } = "outbox";

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
	/// Gets or sets the TTL in seconds for sent messages.
	/// </summary>
	/// <value>Defaults to 7 days (604800 seconds). Set to -1 to disable TTL.</value>
	public int SentMessageTtlSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for rate-limited requests.
	/// </summary>
	/// <value>Defaults to 9.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 9;

	/// <summary>
	/// Gets or sets the maximum wait time for retry in seconds.
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
	/// <value>Defaults to <see langword="false"/> for performance.</value>
	public bool EnableContentResponseOnWrite { get; set; }

	/// <summary>
	/// Gets or sets the consistency level for operations.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (use account default).</value>
	public ConsistencyLevel? ConsistencyLevel { get; set; }

	/// <summary>
	/// Gets or sets the preferred regions for operations.
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
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString) &&
			(string.IsNullOrWhiteSpace(AccountEndpoint) || string.IsNullOrWhiteSpace(AccountKey)))
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
