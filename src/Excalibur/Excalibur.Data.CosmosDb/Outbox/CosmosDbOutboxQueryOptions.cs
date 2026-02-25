// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Configuration options for Cosmos DB outbox cross-partition query optimization.
/// </summary>
/// <remarks>
/// <para>
/// Cross-partition queries in Cosmos DB can be expensive because they fan out across
/// all physical partitions. This options class controls the partition key strategy
/// and parallelism settings to optimize query performance for outbox operations.
/// </para>
/// <para>
/// The default partition key path is <c>/partitionKey</c> which uses status-based
/// partitioning (staged, sent, failed, scheduled). This groups related messages
/// and avoids cross-partition queries for common operations like
/// <c>GetUnsentMessagesAsync</c> and <c>CleanupSentMessagesAsync</c>.
/// </para>
/// </remarks>
public sealed class CosmosDbOutboxQueryOptions
{
	/// <summary>
	/// Gets or sets the partition key path used for the outbox container.
	/// </summary>
	/// <value>Defaults to "/partitionKey".</value>
	[Required]
	public string PartitionKeyPath { get; set; } = "/partitionKey";

	/// <summary>
	/// Gets or sets a value indicating whether cross-partition queries are allowed.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	/// <remarks>
	/// When disabled, queries that would require cross-partition fan-out will throw.
	/// Enable only when necessary (e.g., <c>GetStatisticsAsync</c> aggregation).
	/// </remarks>
	public bool EnableCrossPartitionQuery { get; set; }

	/// <summary>
	/// Gets or sets the maximum degree of parallelism for cross-partition queries.
	/// </summary>
	/// <value>Defaults to -1 (automatic parallelism based on partition count).</value>
	/// <remarks>
	/// Set to 1 for serial execution, or a specific number to limit parallelism.
	/// A value of -1 allows the SDK to determine the optimal parallelism.
	/// </remarks>
	[Range(-1, 256)]
	public int MaxConcurrency { get; set; } = -1;

	/// <summary>
	/// Gets or sets the maximum number of items buffered during cross-partition reads.
	/// </summary>
	/// <value>Defaults to -1 (SDK default).</value>
	/// <remarks>
	/// Controls the number of items buffered client-side during a cross-partition
	/// query. Higher values use more memory but reduce round trips.
	/// </remarks>
	[Range(-1, 100000)]
	public int MaxBufferedItemCount { get; set; } = -1;

	/// <summary>
	/// Gets or sets a value indicating whether to use continuation tokens for paginated reads.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// When enabled, large result sets are read using continuation tokens to avoid
	/// memory pressure. When disabled, all results are read in a single request.
	/// </remarks>
	public bool UseContinuationTokens { get; set; } = true;

	/// <summary>
	/// Gets or sets the preferred page size for FeedIterator reads.
	/// </summary>
	/// <value>Defaults to 100.</value>
	[Range(1, 10000)]
	public int PreferredPageSize { get; set; } = 100;
}
