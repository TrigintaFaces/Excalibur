// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Shared client/connection options for Azure Cosmos DB.
/// </summary>
/// <remarks>
/// <para>
/// This class contains the common connection and client configuration properties
/// shared across all Cosmos DB store options (projections, sagas, snapshots, persistence).
/// </para>
/// <para>
/// Either <see cref="ConnectionString"/> or both <see cref="AccountEndpoint"/> and
/// <see cref="AccountKey"/> must be provided.
/// </para>
/// </remarks>
public sealed class CosmosDbClientOptions
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
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the consistency level for operations.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (use account default).</value>
	public ConsistencyLevel? ConsistencyLevel { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use direct connection mode.
	/// </summary>
	/// <remarks>
	/// Direct mode provides lower latency than gateway mode.
	/// </remarks>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseDirectMode { get; set; } = true;

	/// <summary>
	/// Gets or sets the preferred regions for geo-redundant operations.
	/// </summary>
	public IReadOnlyList<string>? PreferredRegions { get; set; }

	/// <summary>
	/// Gets or sets the resilience options (retry, timeout, write response).
	/// </summary>
	public CosmosDbClientResilienceOptions Resilience { get; set; } = new();

	/// <summary>
	/// Gets or sets a factory function for creating the HttpClient used by the Cosmos DB client.
	/// </summary>
	/// <remarks>
	/// This is primarily used for testing with the Cosmos DB emulator, which uses self-signed certificates.
	/// The TestContainers CosmosDb container provides an HttpClient that bypasses certificate validation.
	/// </remarks>
	public Func<HttpClient>? HttpClientFactory { get; set; }

	/// <summary>
	/// Gets or sets the application name for diagnostics.
	/// </summary>
	public string? ApplicationName { get; set; }

	/// <summary>
	/// Validates the client connection options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when neither a connection string nor an account endpoint and key pair are provided.
	/// </exception>
	public void Validate()
	{
		var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
		var hasEndpointAndKey = !string.IsNullOrWhiteSpace(AccountEndpoint) && !string.IsNullOrWhiteSpace(AccountKey);

		if (!hasConnectionString && !hasEndpointAndKey)
		{
			throw new InvalidOperationException(
				ErrorMessages.EitherConnectionStringOrAccountEndpointRequired);
		}
	}
}
