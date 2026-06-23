// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// A lock-free, fixed-memory rolling-window error-rate counter modeled on Polly v8's
/// rolling-health window.
/// </summary>
/// <remarks>
/// <para>
/// The window is divided into <c>bucketCount</c> fixed-duration buckets. Each bucket tracks the
/// attempt and failure counts for its slice of time. As time advances, the oldest bucket is
/// transparently reused (its counters are zeroed by the first writer to roll it over), so memory
/// is bounded and a recent burst of failures is never diluted by long-past history — unlike a
/// lifetime-cumulative ratio whose denominator only grows.
/// </para>
/// <para>
/// Time is supplied by the caller as <c>nowTicks</c> (100-nanosecond ticks, e.g.
/// <see cref="DateTimeOffset.UtcTicks"/>) so the type is a pure function of its inputs and can be
/// tested deterministically with synthetic tick values — no wall-clock dependency.
/// </para>
/// <para>
/// Recording is O(1) and allocation-free on the hot path (<see cref="Interlocked"/> increments);
/// reading is O(bucketCount). The reset-then-increment sequence can transiently lose a handful of
/// counts under heavy concurrent rollover; this is acceptable for a health <em>signal</em> (not exact
/// accounting) and matches Polly-style rolling health.
/// </para>
/// </remarks>
internal sealed class RollingErrorWindow
{
	private readonly long[] _attempts;
	private readonly long[] _failures;
	private readonly long[] _bucketEpoch;
	private readonly long _bucketTicks;
	private readonly int _bucketCount;

	/// <summary>
	/// Initializes a new instance of the <see cref="RollingErrorWindow"/> class.
	/// </summary>
	/// <param name="window">The total time span the window covers. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
	/// <param name="bucketCount">The number of buckets the window is divided into. Must be at least 1.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="window"/> is not positive, or <paramref name="bucketCount"/> is less than 1.
	/// </exception>
	public RollingErrorWindow(TimeSpan window, int bucketCount)
	{
		if (window <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be greater than TimeSpan.Zero.");
		}

		if (bucketCount < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be at least 1.");
		}

		_bucketCount = bucketCount;

		// Guard against a sub-tick bucket width when window.Ticks < bucketCount.
		_bucketTicks = Math.Max(1L, window.Ticks / bucketCount);

		_attempts = new long[bucketCount];
		_failures = new long[bucketCount];
		_bucketEpoch = new long[bucketCount];

		// Sentinel epoch so the first write to any bucket is treated as a rollover and zeroed,
		// even at nowTicks == 0 (the synthetic-clock test origin).
		Array.Fill(_bucketEpoch, -1L);
	}

	/// <summary>
	/// Records a single operation attempt at the supplied time.
	/// </summary>
	/// <param name="nowTicks">The current time in 100-nanosecond ticks.</param>
	public void RecordAttempt(long nowTicks) => Record(failure: false, nowTicks);

	/// <summary>
	/// Records a single operation failure at the supplied time.
	/// </summary>
	/// <param name="nowTicks">The current time in 100-nanosecond ticks.</param>
	/// <remarks>
	/// Mirrors the existing <c>OperationStatistics.RecordFailure()</c> semantics: it increments only
	/// the failure counter, not the attempt counter, so the attempt is counted exactly once at the
	/// corresponding <see cref="RecordAttempt"/> call site.
	/// </remarks>
	public void RecordFailure(long nowTicks) => Record(failure: true, nowTicks);

	/// <summary>
	/// Computes the failure ratio across the buckets still inside the rolling window.
	/// </summary>
	/// <param name="nowTicks">The current time in 100-nanosecond ticks.</param>
	/// <returns>
	/// The ratio of failures to attempts (0.0–1.0) over the window, or 0.0 when there were no
	/// in-window attempts.
	/// </returns>
	public double GetErrorRate(long nowTicks)
	{
		var currentTick = nowTicks / _bucketTicks;
		var minTick = currentTick - _bucketCount + 1;

		long attempts = 0;
		long failures = 0;
		for (var i = 0; i < _bucketCount; i++)
		{
			if (Volatile.Read(ref _bucketEpoch[i]) >= minTick)
			{
				attempts += Volatile.Read(ref _attempts[i]);
				failures += Volatile.Read(ref _failures[i]);
			}
		}

		return attempts > 0 ? (double)failures / attempts : 0.0;
	}

	private void Record(bool failure, long nowTicks)
	{
		var tick = nowTicks / _bucketTicks;

		// Modulo guarded against (theoretical) negative ticks so the index is always in range.
		var idx = (int)(((tick % _bucketCount) + _bucketCount) % _bucketCount);

		var seen = Volatile.Read(ref _bucketEpoch[idx]);
		if (seen != tick && Interlocked.CompareExchange(ref _bucketEpoch[idx], tick, seen) == seen)
		{
			// Winner of the rollover race zeroes the stale counters before anyone increments them.
			Interlocked.Exchange(ref _attempts[idx], 0);
			Interlocked.Exchange(ref _failures[idx], 0);
		}

		// Honor the per-method contract: the attempt is counted exactly once at the RecordAttempt
		// call site; RecordFailure increments ONLY the failure counter (the caller pairs one
		// RecordAttempt with one RecordFailure for a failed op, so attempts must not be double-counted).
		if (failure)
		{
			Interlocked.Increment(ref _failures[idx]);
		}
		else
		{
			Interlocked.Increment(ref _attempts[idx]);
		}
	}
}
