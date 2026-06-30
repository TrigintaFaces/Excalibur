// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Distributed circuit metrics stored in cache.
/// </summary>
internal sealed class DistributedCircuitMetrics
{
	/// <summary>
	/// Gets or sets the total number of successful operations.
	/// </summary>
	/// <value>The total count of successful operations.</value>
	public long SuccessCount { get; set; }

	/// <summary>
	/// Gets or sets the total number of failed operations.
	/// </summary>
	/// <value>The total count of failed operations.</value>
	public long FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the current count of consecutive failures.
	/// </summary>
	/// <value>The number of consecutive failures without an intervening success.</value>
	public int ConsecutiveFailures { get; set; }

	/// <summary>
	/// Gets or sets the current count of consecutive successes.
	/// </summary>
	/// <value>The number of consecutive successes without an intervening failure.</value>
	public int ConsecutiveSuccesses { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last successful operation.
	/// </summary>
	/// <value>The timestamp when the last successful operation completed.</value>
	public DateTimeOffset LastSuccess { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last failed operation.
	/// </summary>
	/// <value>The timestamp when the last failed operation occurred.</value>
	public DateTimeOffset LastFailure { get; set; }

	/// <summary>
	/// Gets or sets the reason for the last failure.
	/// </summary>
	/// <value>A description of why the last operation failed.</value>
	public string LastFailureReason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the rolling sampling-window buckets used for the windowed open decision.
	/// </summary>
	/// <remarks>
	/// Each bucket aggregates the attempts and failures observed within one time slice of the
	/// configured <c>SamplingDuration</c>. Buckets older than the window are evicted on write so the
	/// failure ratio reflects recent traffic (Polly v8 rolling-window semantics) rather than a
	/// lifetime-cumulative ratio whose denominator only grows. The counts are persisted with the
	/// metric so cross-instance aggregation of the distributed breaker actually holds (zxb7fp).
	/// </remarks>
	/// <value>The list of active rolling-window buckets.</value>
	public List<CircuitWindowBucket> Windows { get; set; } = [];

	/// <summary>
	/// Records an attempt (and optionally a failure) into the rolling sampling window at the supplied time.
	/// </summary>
	/// <param name="failure"><see langword="true"/> when the attempt failed; otherwise <see langword="false"/>.</param>
	/// <param name="nowTicks">The current time in 100-nanosecond ticks (e.g. <see cref="DateTimeOffset.UtcTicks"/>).</param>
	/// <param name="bucketTicks">The width of a single bucket in ticks. Must be at least 1.</param>
	/// <param name="bucketCount">The number of buckets spanning the sampling window. Must be at least 1.</param>
	public void RecordWindow(bool failure, long nowTicks, long bucketTicks, int bucketCount)
	{
		var epoch = nowTicks / bucketTicks;
		var minEpoch = epoch - bucketCount + 1;

		// Evict buckets that have rolled out of the window so the persisted metric never accumulates
		// unbounded history (time-decay, not lifetime-cumulative).
		_ = Windows.RemoveAll(b => b.Epoch < minEpoch);

		var bucket = Windows.Find(b => b.Epoch == epoch);
		if (bucket is null)
		{
			bucket = new CircuitWindowBucket { Epoch = epoch };
			Windows.Add(bucket);
		}

		bucket.Attempts++;
		if (failure)
		{
			bucket.Failures++;
		}
	}

	/// <summary>
	/// Computes the in-window attempt count and failure ratio across the buckets still inside the window.
	/// </summary>
	/// <param name="nowTicks">The current time in 100-nanosecond ticks.</param>
	/// <param name="bucketTicks">The width of a single bucket in ticks. Must be at least 1.</param>
	/// <param name="bucketCount">The number of buckets spanning the sampling window. Must be at least 1.</param>
	/// <returns>
	/// A tuple of the total in-window attempts and the failure ratio (0.0–1.0), or 0.0 ratio when there
	/// were no in-window attempts.
	/// </returns>
	public (long Attempts, double FailureRatio) GetWindow(long nowTicks, long bucketTicks, int bucketCount)
	{
		var minEpoch = (nowTicks / bucketTicks) - bucketCount + 1;

		long attempts = 0;
		long failures = 0;
		foreach (var bucket in Windows)
		{
			if (bucket.Epoch >= minEpoch)
			{
				attempts += bucket.Attempts;
				failures += bucket.Failures;
			}
		}

		return (attempts, attempts > 0 ? (double)failures / attempts : 0.0);
	}
}

/// <summary>
/// A single rolling-window time-slice bucket of attempt and failure counts for the distributed
/// circuit breaker's windowed failure-ratio evaluation.
/// </summary>
internal sealed class CircuitWindowBucket
{
	/// <summary>
	/// Gets or sets the bucket epoch (the time slice index: <c>nowTicks / bucketTicks</c>).
	/// </summary>
	/// <value>The monotonically increasing time-slice index for this bucket.</value>
	public long Epoch { get; set; }

	/// <summary>
	/// Gets or sets the number of attempts recorded in this bucket's time slice.
	/// </summary>
	/// <value>The count of attempts (successes plus failures) in the slice.</value>
	public long Attempts { get; set; }

	/// <summary>
	/// Gets or sets the number of failures recorded in this bucket's time slice.
	/// </summary>
	/// <value>The count of failed attempts in the slice.</value>
	public long Failures { get; set; }
}
