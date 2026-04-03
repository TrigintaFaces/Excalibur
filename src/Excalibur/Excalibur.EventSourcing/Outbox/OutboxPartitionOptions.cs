// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Partitioning strategy for the outbox table.
/// </summary>
public enum OutboxPartitionStrategy
{
	/// <summary>Single outbox table (current behavior, default).</summary>
	None = 0,

	/// <summary>One outbox table per tenant shard (when vpbunk sharding is active).</summary>
	PerShard = 1,

	/// <summary>Hash(tenantId) % N partitions in same database.</summary>
	ByTenantHash = 2,
}

/// <summary>
/// Configuration options for partitioned outbox processing.
/// </summary>
public sealed class OutboxPartitionOptions
{
	/// <summary>
	/// Gets or sets the partitioning strategy.
	/// </summary>
	/// <value>The partition strategy. Default is <see cref="OutboxPartitionStrategy.None"/>.</value>
	public OutboxPartitionStrategy Strategy { get; set; } = OutboxPartitionStrategy.None;

	/// <summary>
	/// Gets or sets the number of partitions for hash-based partitioning.
	/// </summary>
	/// <value>The partition count. Default is 8. Only used when Strategy is <see cref="OutboxPartitionStrategy.ByTenantHash"/>.</value>
	[Range(1, 256)]
	public int PartitionCount { get; set; } = 8;

	/// <summary>
	/// Gets or sets the number of processor instances per partition.
	/// </summary>
	/// <value>The processor count per partition. Default is 1.</value>
	[Range(1, int.MaxValue)]
	public int ProcessorCountPerPartition { get; set; } = 1;

	/// <summary>
	/// Gets or sets the shard IDs for per-shard partitioning.
	/// Required when <see cref="Strategy"/> is <see cref="OutboxPartitionStrategy.PerShard"/>.
	/// </summary>
	/// <value>The shard IDs. Default is an empty list.</value>
	public List<string> ShardIds { get; set; } = [];

	/// <summary>
	/// Gets or sets the polling interval when no messages are available in a partition.
	/// </summary>
	/// <value>The polling interval. Default is 1 second.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the back-off interval after an error processing a partition.
	/// </summary>
	/// <value>The error back-off interval. Default is 5 seconds.</value>
	public TimeSpan ErrorBackoffInterval { get; set; } = TimeSpan.FromSeconds(5);
}
