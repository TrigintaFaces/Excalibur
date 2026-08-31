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
	/// Gets or sets how long a message is retained after it has been published, in seconds.
	/// </summary>
	/// <value>Defaults to 7 days (604800 seconds). Set to -1 to retain published messages indefinitely.</value>
	/// <remarks>
	/// This governs <b>published</b> messages only, and is applied to a message when it is marked published.
	/// A message that has not been delivered yet never expires, whatever this is set to — the container is
	/// provisioned so that nothing expires unless a message opts in.
	/// </remarks>
	[Range(-1, int.MaxValue)]
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets how long a claim lease is held before it expires, in seconds.
	/// </summary>
	/// <value>Defaults to 120 seconds, matching the relational outbox providers.</value>
	/// <remarks>
	/// Only consulted by the atomic claim (<c>ICloudNativeOutboxStoreClaim</c>). A claimant that dies
	/// mid-delivery releases its messages by letting this window elapse, so nothing has to detect the death
	/// — but a claimant that is merely <i>slow</i> can also have a message taken from under it. Set this
	/// above the maximum time a publish can take, and remember it bounds the duplicate window only up to
	/// the clock skew between claimants.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int LeaseTimeoutSeconds { get; set; } = 120;

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
	/// Gets or sets a factory function for creating the HttpClient used by the Cosmos DB client.
	/// </summary>
	/// <remarks>
	/// This is primarily used for testing against the Cosmos DB emulator, which the SDK cannot reach with a
	/// default client. Every other Cosmos store in this framework exposes the same hook; without it this
	/// store cannot be exercised against a real emulator at all.
	/// </remarks>
	public Func<HttpClient>? HttpClientFactory { get; set; }

	/// <summary>
	/// Gets or sets the connection options for Cosmos DB.
	/// </summary>
	/// <value>The connection options.</value>
	public CosmosDbOutboxConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Connection.ConnectionString) &&
			(string.IsNullOrWhiteSpace(Connection.AccountEndpoint) || string.IsNullOrWhiteSpace(Connection.AccountKey)))
		{
			throw new InvalidOperationException(
				"Either ConnectionString or both AccountEndpoint and AccountKey must be provided.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required.");
		}

		if (DefaultTimeToLiveSeconds == 0)
		{
			throw new InvalidOperationException(
				"DefaultTimeToLiveSeconds must be -1 (retain indefinitely) or a positive number of seconds. "
				+ "Cosmos rejects a time-to-live of zero.");
		}
	}
}
