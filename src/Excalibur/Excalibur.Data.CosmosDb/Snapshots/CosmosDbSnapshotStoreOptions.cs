// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Snapshots;

/// <summary>
/// Configuration options for the Cosmos DB snapshot store.
/// </summary>
public sealed class CosmosDbSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the shared client/connection options.
	/// </summary>
	public CosmosDbClientOptions Client { get; set; } = new();

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
	/// Gets or sets the base wait, in milliseconds, before re-attempting a snapshot write that lost a
	/// conditional write to a concurrent writer.
	/// </summary>
	/// <remarks>
	/// The wait is drawn at random from an interval that grows exponentially from this base and is capped
	/// well below a second, because a rejected conditional write has not waited on a lock — it was refused
	/// in one round trip. The randomisation, not the growth, is what lets contention drain: writers
	/// rejected at the same instant would otherwise compute the same wait and collide again on waking.
	/// Zero disables the wait, which returns the loop to spending its whole attempt budget inside the
	/// window in which it is losing.
	/// </remarks>
	/// <value>Defaults to 25 milliseconds.</value>
	public int ContendedWriteBackoffMilliseconds { get; set; } = 25;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		Client.Validate();

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
