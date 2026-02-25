// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Bulkhead metrics for monitoring.
/// </summary>
public sealed class BulkheadMetrics
{
	/// <summary>
	/// Gets the name of the bulkhead.
	/// </summary>
	/// <value>The identifier associated with the bulkhead instance.</value>
	public string Name { get; init; } = string.Empty;

	/// <summary>
	/// Gets the maximum allowed concurrent operations.
	/// </summary>
	/// <value>The highest number of operations that may execute simultaneously.</value>
	public int MaxConcurrency { get; init; }

	/// <summary>
	/// Gets the maximum queue length.
	/// </summary>
	/// <value>The maximum number of queued operations permitted when concurrency is saturated.</value>
	public int MaxQueueLength { get; init; }

	/// <summary>
	/// Gets the number of currently active operations.
	/// </summary>
	/// <value>The number of in-flight operations presently executing.</value>
	public int ActiveExecutions { get; init; }

	/// <summary>
	/// Gets the current queue length.
	/// </summary>
	/// <value>The number of operations waiting in the queue.</value>
	public int QueueLength { get; init; }

	/// <summary>
	/// Gets the total operations executed.
	/// </summary>
	/// <value>The cumulative number of operations processed (successful or otherwise).</value>
	public long TotalExecutions { get; init; }

	/// <summary>
	/// Gets the total operations rejected.
	/// </summary>
	/// <value>The number of operations denied due to the bulkhead being full.</value>
	public long RejectedExecutions { get; init; }

	/// <summary>
	/// Gets the total operations queued.
	/// </summary>
	/// <value>The number of operations that waited in the queue before execution.</value>
	public long QueuedExecutions { get; init; }

	/// <summary>
	/// Gets the available capacity for new operations.
	/// </summary>
	/// <value>The number of remaining execution slots before reaching the concurrency limit.</value>
	public int AvailableCapacity { get; init; }

	/// <summary>
	/// Gets the utilization percentage.
	/// </summary>
	/// <value>The bulkhead utilization expressed as a percentage in the range 0-100.</value>
	public double UtilizationPercentage => MaxConcurrency > 0
		? (double)ActiveExecutions / MaxConcurrency * 100
		: 0;
}
