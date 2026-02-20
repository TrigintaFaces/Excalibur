// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Performance;

/// <summary>
/// Options for configuring the sharded executor.
/// </summary>
public sealed class ShardedExecutorOptions
{
	/// <summary>
	/// Gets or sets number of shards (defaults to processor count).
	/// </summary>
	/// <value>The current <see cref="ShardCount"/> value.</value>
	[Range(0, int.MaxValue)]
	public int ShardCount { get; set; }

	/// <summary>
	/// Gets or sets maximum queue depth per shard for backpressure.
	/// </summary>
	/// <value>The current <see cref="MaxQueueDepth"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxQueueDepth { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable CPU affinity for NUMA optimization.
	/// </summary>
	/// <value>The current <see cref="EnableCpuAffinity"/> value.</value>
	public bool EnableCpuAffinity { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;
}
