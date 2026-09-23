// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.Diagnostics;

namespace Excalibur.Cdc;

/// <summary>
/// Bounds a CDC reconnect loop: capped exponential backoff between attempts, a count of consecutive
/// attempts that ended in a transient fault without making progress, and an optional limit on that count.
/// </summary>
/// <remarks>
/// <para>
/// An unrecognised fault is classified transient by design, so a condition that never clears would
/// otherwise be retried forever at a fixed interval with nothing surfacing it. This type is what makes the
/// retry visible and, when the consumer asks for it, finite.
/// </para>
/// <para>
/// <b>What counts.</b> An attempt ends a run of consecutive failures when it made progress — the caller
/// reports that through <see cref="RecordProgress"/> — or when it stayed up for at least the maximum
/// reconnect delay before failing. The second rule exists for quiet sources: a stream with nothing to
/// confirm, disconnected by an idle timeout every few minutes, is healthy, and must not be counted towards
/// a stop.
/// </para>
/// <para>
/// One instance serves one consume loop for the lifetime of one start call, on that loop's thread. It is
/// not thread-safe, and it does not need to be.
/// </para>
/// </remarks>
internal sealed class CdcTransientFailureBackoff
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly int? _maxConsecutiveFailures;
	private readonly TimeProvider _timeProvider;
	private readonly CdcHealthState? _healthState;
	private long _attemptStarted;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcTransientFailureBackoff"/> class.
	/// </summary>
	/// <param name="baseDelay">The delay after the first failure; doubled for each consecutive one.</param>
	/// <param name="maxDelay">
	/// The largest delay between attempts, and also how long an attempt must stay up to count as stable. A
	/// value below <paramref name="baseDelay"/> is raised to it, so a loop never retries faster than the
	/// interval it was configured to wait.
	/// </param>
	/// <param name="maxConsecutiveFailures">
	/// The number of consecutive transient failures after which the loop must stop, or
	/// <see langword="null"/> to retry for as long as the process runs.
	/// </param>
	/// <param name="timeProvider">The clock the stable interval is measured on.</param>
	/// <param name="healthState">Where the consecutive-failure count is published, when health checks are registered.</param>
	public CdcTransientFailureBackoff(
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		int? maxConsecutiveFailures,
		TimeProvider timeProvider,
		CdcHealthState? healthState)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(baseDelay, TimeSpan.Zero);
		if (maxConsecutiveFailures is { } limit)
		{
			ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1, nameof(maxConsecutiveFailures));
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay < baseDelay ? baseDelay : maxDelay;
		_maxConsecutiveFailures = maxConsecutiveFailures;
		_timeProvider = timeProvider;
		_healthState = healthState;
		_attemptStarted = timeProvider.GetTimestamp();
		_healthState?.RecordConsecutiveTransientFailures(0);
	}

	/// <summary>
	/// Gets the number of consecutive attempts that ended in a transient fault without progress.
	/// </summary>
	/// <value>Zero after progress; incremented by each counted failure.</value>
	public int ConsecutiveFailures { get; private set; }

	/// <summary>
	/// Marks the start of a connection attempt, from which the stable interval is measured.
	/// </summary>
	public void BeginAttempt() => _attemptStarted = _timeProvider.GetTimestamp();

	/// <summary>
	/// Reports that the current attempt made progress, which ends any run of consecutive failures.
	/// </summary>
	/// <remarks>
	/// For a streaming source, call this only for a position strictly newer than the one confirmed when the
	/// attempt began. Re-confirming a position the source delivered again is not progress, and treating it
	/// as progress lets a fault that recurs after it retry forever.
	/// </remarks>
	public void RecordProgress()
	{
		if (ConsecutiveFailures == 0)
		{
			return;
		}

		ConsecutiveFailures = 0;
		_healthState?.RecordConsecutiveTransientFailures(0);
	}

	/// <summary>
	/// Counts a transient failure of the current attempt and reports how long to wait and whether to stop.
	/// </summary>
	/// <returns>The delay before the next attempt, and whether the configured limit has been reached.</returns>
	public CdcTransientFailureOutcome RecordTransientFailure()
	{
		if (_timeProvider.GetElapsedTime(_attemptStarted) >= _maxDelay)
		{
			// The attempt stayed up longer than the longest backoff: it was healthy, and this failure
			// starts a new run rather than extending the previous one.
			ConsecutiveFailures = 0;
		}

		ConsecutiveFailures++;
		_healthState?.RecordConsecutiveTransientFailures(ConsecutiveFailures);

		var exhausted = _maxConsecutiveFailures is { } limit && ConsecutiveFailures >= limit;
		return new CdcTransientFailureOutcome(ComputeDelay(ConsecutiveFailures), ConsecutiveFailures, exhausted);
	}

	/// <summary>
	/// Computes <c>min(base × 2^(failures − 1), max)</c> without overflowing <see cref="TimeSpan"/>.
	/// </summary>
	/// <param name="consecutiveFailures">The number of consecutive failures, at least one.</param>
	/// <returns>The delay before the next attempt.</returns>
	internal TimeSpan ComputeDelay(int consecutiveFailures)
	{
		var doublings = consecutiveFailures - 1;

		// Doubling the base 'doublings' times stays within the maximum exactly when the base is no larger than
		// the maximum shifted right the same number of times. Comparing that way never multiplies, so a count
		// in the thousands cannot overflow into an exception that would escape a retry-forever loop.
		if (doublings >= 62 || _baseDelay.Ticks > _maxDelay.Ticks >> doublings)
		{
			return _maxDelay;
		}

		return TimeSpan.FromTicks(_baseDelay.Ticks << doublings);
	}
}
