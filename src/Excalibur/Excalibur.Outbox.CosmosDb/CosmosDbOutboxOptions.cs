// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Configuration options for the Cosmos DB outbox store.
/// </summary>
public sealed class CosmosDbOutboxOptions
{
	/// <summary>
	/// Gets or sets the Azure Cosmos DB connection string.
	/// </summary>
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
	/// Gets or sets the default time-to-live for published messages in seconds.
	/// </summary>
	/// <value>Defaults to 7 days (604800 seconds). Set to -1 to disable TTL.</value>
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

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
	/// Gets or sets a value indicating whether to use direct connection mode.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseDirectMode { get; set; } = true;

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
				"Either ConnectionString or both AccountEndpoint and AccountKey must be provided.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required.");
		}
	}
}
