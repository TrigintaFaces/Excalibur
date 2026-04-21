// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Catch-up processing strategy.
/// </summary>
public enum CatchUpStrategy
{
	/// <summary>Sequential single-worker processing (default, existing behavior).</summary>
	Sequential = 0,

	/// <summary>Range-partitioned parallel processing for catch-up mode.</summary>
	RangePartitioned = 1,

	/// <summary>Per-shard parallel processing when tenant sharding is enabled.</summary>
	PerShard = 2,
}

/// <summary>
/// Configuration options for parallel global stream catch-up.
/// </summary>
public sealed class ParallelCatchUpOptions
{
	/// <summary>
	/// Gets or sets the catch-up processing strategy.
	/// </summary>
	/// <value>The strategy. Default is <see cref="CatchUpStrategy.Sequential"/>.</value>
	public CatchUpStrategy Strategy { get; set; } = CatchUpStrategy.Sequential;

	/// <summary>
	/// Gets or sets the number of parallel workers for range-partitioned catch-up.
	/// </summary>
	/// <value>The worker count. Default is <see cref="Environment.ProcessorCount"/>.</value>
	[Range(1, int.MaxValue)]
	public int WorkerCount { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the batch size for reading events from the store.
	/// </summary>
	/// <value>The batch size. Default is 1000.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the number of events between checkpoints.
	/// </summary>
	/// <value>The checkpoint interval. Default is 5000.</value>
	[Range(1, int.MaxValue)]
	public int CheckpointInterval { get; set; } = 5000;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for a failed worker.
	/// </summary>
	/// <value>The max retry count. Default is 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the heartbeat timeout for detecting hung workers.
	/// </summary>
	/// <value>The heartbeat timeout. Default is 60 seconds.</value>
	public TimeSpan WorkerHeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
