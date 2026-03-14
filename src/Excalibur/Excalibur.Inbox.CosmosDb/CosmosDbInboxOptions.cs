// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Configuration options for the Cosmos DB inbox store.
/// </summary>
/// <remarks>
/// <para>
/// Client/connection properties are delegated to <see cref="Client"/>.
/// This follows the <c>CosmosClientOptions</c> pattern of reusing shared client configuration.
/// </para>
/// </remarks>
public sealed class CosmosDbInboxOptions
{
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
	/// Gets or sets the default time to live for documents in seconds.
	/// </summary>
	/// <remarks>
	/// Set to -1 for no expiration. Defaults to 7 days (604800 seconds).
	/// </remarks>
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the shared client/connection options.
	/// </summary>
	/// <value> The Cosmos DB client options. </value>
	public CosmosDbClientOptions Client { get; set; } = new();

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		Client.Validate();

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException(
				"Database name is required.");
		}

		if (string.IsNullOrWhiteSpace(ContainerName))
		{
			throw new InvalidOperationException(
				"Container name is required.");
		}
	}
}
