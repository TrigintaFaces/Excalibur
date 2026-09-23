// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Tracks consecutive change-feed checkpoint-save failures for one subscription and decides when the
/// subscription should report itself degraded.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every Cosmos DB change-feed subscription (<c>Excalibur.Data.CosmosDb</c>,
/// <c>Excalibur.Outbox.CosmosDb</c>, <c>Excalibur.EventSourcing.CosmosDb</c>) — the three subscriptions
/// otherwise duplicate this exact decision. Public, not <c>InternalsVisibleTo</c>-shared, matching this
/// package's existing convention for the sibling Cosmos packages (see
/// <c>AddCosmosDbChangeFeedDurabilityDefaults</c>): a deliberate, versioned cross-package surface rather
/// than an internals leak. A checkpoint-save failure never stops event delivery (the feed's
/// at-least-once guarantee already requires idempotent handlers); it is tracked only so that a run of
/// consecutive failures — which widens the redelivery window on a future restart — becomes an
/// operator-visible signal instead of growing silently forever.
/// </para>
/// <para>
/// Deliberately holds no I/O and does no logging itself: the decision is separated from its effects so
/// it stays directly and non-vacuously unit-testable, and each subscription's own caller decides what to
/// log (its own event ID range) when this tracker's methods report a state change.
/// </para>
/// </remarks>
public sealed class ChangeFeedCheckpointFailureTracker
{
	private readonly int _maxConsecutiveFailures;
	private readonly TimeProvider _timeProvider;
	private int _consecutiveFailures;
	private int _degraded;
	private long _degradedSinceTimestamp;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChangeFeedCheckpointFailureTracker"/> class.
	/// </summary>
	/// <param name="maxConsecutiveFailures">
	/// The number of consecutive checkpoint-save failures after which the subscription reports itself
	/// degraded. Must be positive.
	/// </param>
	/// <param name="timeProvider">The time source used to measure how long the subscription has been degraded.</param>
	public ChangeFeedCheckpointFailureTracker(int maxConsecutiveFailures, TimeProvider? timeProvider = null)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxConsecutiveFailures, 0);
		_maxConsecutiveFailures = maxConsecutiveFailures;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Gets a value indicating whether the subscription has reached the consecutive-failure bound and is
	/// currently reporting itself degraded.
	/// </summary>
	public bool IsDegraded => Volatile.Read(ref _degraded) != 0;

	/// <summary>
	/// Gets the current run of consecutive checkpoint-save failures.
	/// </summary>
	public int ConsecutiveFailureCount => Volatile.Read(ref _consecutiveFailures);

	/// <summary>
	/// Gets how long the subscription has been degraded, or <see langword="null"/> when it is not.
	/// </summary>
	public TimeSpan? CheckpointLag
	{
		get
		{
			var since = Volatile.Read(ref _degradedSinceTimestamp);
			return since == 0 ? null : _timeProvider.GetElapsedTime(since);
		}
	}

	/// <summary>
	/// Records a checkpoint-save failure.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> exactly once per degradation — on the call whose failure count reaches the
	/// configured bound — so a caller logging the escalation does so once, not on every failure after it.
	/// </returns>
	public bool RecordFailure()
	{
		var count = Interlocked.Increment(ref _consecutiveFailures);
		if (count < _maxConsecutiveFailures)
		{
			return false;
		}

		if (Interlocked.Exchange(ref _degraded, 1) != 0)
		{
			return false; // already degraded from an earlier failure in this same run
		}

		Interlocked.Exchange(ref _degradedSinceTimestamp, _timeProvider.GetTimestamp());
		return true;
	}

	/// <summary>
	/// Records a successful checkpoint save, resetting the failure count and clearing any degraded state.
	/// </summary>
	public void RecordSuccess()
	{
		Interlocked.Exchange(ref _consecutiveFailures, 0);
		Interlocked.Exchange(ref _degraded, 0);
		Interlocked.Exchange(ref _degradedSinceTimestamp, 0);
	}
}
