// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Data.CosmosDb.Tests.ChangeFeed;

/// <summary>
/// Regression lock for the shared, pure <see cref="ChangeFeedCheckpointFailureTracker"/> decision every
/// Cosmos change-feed subscription's checkpoint-save fail-open path gates on (bead <c>evttu3</c>): a
/// checkpoint-save failure never stops event delivery, but a run of consecutive failures crossing the
/// configured bound must become an operator-visible signal exactly once per degradation, and a
/// subsequent success must clear it.
/// </summary>
/// <remarks>
/// Deterministic and real-infra-free by design (the tracker holds no I/O), matching the same pattern as
/// <c>CdcFatalGuardShould</c> in <c>Excalibur.Cdc.Tests</c>: the pure decision is bound directly here; the
/// real-Cosmos-emulator lock proving the SUBSCRIPTION actually delivers events while its checkpoint saves
/// are forced to fail is a separate, independently-authored test (per the evttu3 ruling).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class ChangeFeedCheckpointFailureTrackerShould
{
	[Fact]
	public void NotBeDegraded_BeforeAnyFailure()
	{
		var tracker = new ChangeFeedCheckpointFailureTracker(3);

		tracker.IsDegraded.ShouldBeFalse();
		tracker.ConsecutiveFailureCount.ShouldBe(0);
		tracker.CheckpointLag.ShouldBeNull();
	}

	[Fact]
	public void NotDegrade_WhileFailuresStayBelowTheBound()
	{
		var tracker = new ChangeFeedCheckpointFailureTracker(3);

		var crossedAtFirst = tracker.RecordFailure();
		var crossedAtSecond = tracker.RecordFailure();

		crossedAtFirst.ShouldBeFalse("liveness: a single failure below the bound must not degrade the subscription");
		crossedAtSecond.ShouldBeFalse();
		tracker.IsDegraded.ShouldBeFalse();
		tracker.ConsecutiveFailureCount.ShouldBe(2);
	}

	[Fact]
	public void Degrade_ExactlyOnce_WhenConsecutiveFailuresReachTheBound()
	{
		var tracker = new ChangeFeedCheckpointFailureTracker(3);

		tracker.RecordFailure();
		tracker.RecordFailure();
		var crossedAtBound = tracker.RecordFailure();
		var crossedAfterBound = tracker.RecordFailure();

		crossedAtBound.ShouldBeTrue("safety: the Nth consecutive failure crossing the bound must report the escalation");
		crossedAfterBound.ShouldBeFalse("the escalation must be reported exactly once per degradation, not on every failure after it");
		tracker.IsDegraded.ShouldBeTrue();
		tracker.ConsecutiveFailureCount.ShouldBe(4);
	}

	[Fact]
	public void ClearDegradedStateAndResetCount_OnSuccess()
	{
		var tracker = new ChangeFeedCheckpointFailureTracker(2);
		tracker.RecordFailure();
		tracker.RecordFailure();
		tracker.IsDegraded.ShouldBeTrue();

		tracker.RecordSuccess();

		tracker.IsDegraded.ShouldBeFalse("liveness: a successful checkpoint save must clear a degraded state, since the redelivery window stopped growing");
		tracker.ConsecutiveFailureCount.ShouldBe(0);
		tracker.CheckpointLag.ShouldBeNull();
	}

	[Fact]
	public void DegradeAgain_AfterARecoveryAndANewRunOfFailures()
	{
		var tracker = new ChangeFeedCheckpointFailureTracker(2);
		tracker.RecordFailure();
		tracker.RecordFailure();
		tracker.RecordSuccess();

		tracker.RecordFailure();
		var crossedAgain = tracker.RecordFailure();

		crossedAgain.ShouldBeTrue("a fresh run of consecutive failures after a recovery must be able to degrade the subscription again");
		tracker.IsDegraded.ShouldBeTrue();
	}

	[Fact]
	public void ReportGrowingCheckpointLag_WhileDegraded()
	{
		var timeProvider = new FakeTimeProvider();
		var tracker = new ChangeFeedCheckpointFailureTracker(1, timeProvider);

		tracker.RecordFailure();
		tracker.IsDegraded.ShouldBeTrue();
		tracker.CheckpointLag.ShouldBe(TimeSpan.Zero);

		timeProvider.Advance(TimeSpan.FromMinutes(5));

		tracker.CheckpointLag.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void RejectANonPositiveBound()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new ChangeFeedCheckpointFailureTracker(0));
		Should.Throw<ArgumentOutOfRangeException>(() => new ChangeFeedCheckpointFailureTracker(-1));
	}
}
