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
	/// Gets or sets a value indicating whether to create the database and container if they do not exist.
	/// </summary>
	/// <remarks>
	/// Matches the option exposed by the other Cosmos-backed stores. Unlike those, this one provisions the
	/// <em>database</em> as well as the container, because the inbox's first run commonly targets an empty
	/// account where the database itself is absent. Set to <see langword="false"/> where provisioning is
	/// owned by deployment tooling and the application principal has no create rights.
	/// </remarks>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;

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
	/// Gets or sets the partition-key value shared by the inbox processed-mark and the handler's own writes,
	/// enabling transactional (exactly-once) processing.
	/// </summary>
	/// <remarks>
	/// A Cosmos DB <c>TransactionalBatch</c> is single-partition: every operation in the batch — including the
	/// handler's enlisted writes and the inbox processed-mark — must target one partition key. Transactional
	/// processing is therefore only honoured when the handler writes to the same logical partition as the inbox
	/// mark. Set this to that shared partition-key value to opt in; when left <see langword="null"/> (the
	/// default) the store advertises no transactional capability and callers fall back to the at-least-once
	/// idempotent claim protocol rather than falsely advertising atomicity. On the transactional path the
	/// processed-mark is written under this shared partition (not the per-handler partition used by the
	/// non-transactional operations), so the handler's batch and the mark commit atomically together.
	/// </remarks>
	public string? SharedPartitionKey { get; set; }

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
