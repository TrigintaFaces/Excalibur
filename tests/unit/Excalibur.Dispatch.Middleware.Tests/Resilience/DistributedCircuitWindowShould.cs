// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Author≠impl regression lock for <c>zxb7fp</c> — the distributed circuit breaker's <b>windowed</b>
/// (rolling, time-decayed) failure accounting on <see cref="DistributedCircuitMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// (Implementer = PlatformDeveloper; this is the independent lock authored by TestsDeveloper, against the
/// pinned <c>RecordWindow</c>/<c>GetWindow</c> seam — deterministic, pure of wall-clock: the test supplies
/// <c>nowTicks</c> explicitly so there is no timing flakiness.)
/// </para>
/// <para>
/// <b>The invariant (the bug zxb7fp fixed):</b> the breaker must trip on a <em>rolling-window</em> failure
/// ratio, NOT a lifetime-cumulative one — old buckets that have rolled out of the sampling window must be
/// excluded from both the count and the ratio. The open decision the breaker computes is
/// <c>in-window attempts ≥ MinimumThroughput AND in-window failure-ratio &gt; FailureRatio</c>, and this lock
/// pins the two quantities that decision consumes. RED if the windowing reverts to cumulative (the eviction /
/// in-window filter is removed): the time-decay tests would then over-count rolled-out failures.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class DistributedCircuitWindowShould
{
	private const long BucketTicks = 1000;
	private const int BucketCount = 10;
	private const long WindowTicks = BucketTicks * BucketCount;

	[Fact]
	public void AggregateAttemptsAndFailureRatio_WithinTheWindow()
	{
		var metrics = new DistributedCircuitMetrics();
		const long now = 100 * BucketTicks;

		// 10 attempts in-window, 4 failures.
		for (var i = 0; i < 6; i++) metrics.RecordWindow(failure: false, now, BucketTicks, BucketCount);
		for (var i = 0; i < 4; i++) metrics.RecordWindow(failure: true, now, BucketTicks, BucketCount);

		var (attempts, ratio) = metrics.GetWindow(now, BucketTicks, BucketCount);

		attempts.ShouldBe(10);
		ratio.ShouldBe(0.4, tolerance: 1e-9);
	}

	[Fact]
	public void ExcludeFailuresThatHaveRolledOutOfTheWindow_NotLifetimeCumulative()
	{
		var metrics = new DistributedCircuitMetrics();
		const long t0 = 100 * BucketTicks;

		// 5 old failures, all in the t0 bucket.
		for (var i = 0; i < 5; i++) metrics.RecordWindow(failure: true, t0, BucketTicks, BucketCount);

		// Advance a full window past t0 so the old bucket has rolled out, then record 2 attempts (1 failure).
		var later = t0 + WindowTicks;
		metrics.RecordWindow(failure: true, later, BucketTicks, BucketCount);
		metrics.RecordWindow(failure: false, later, BucketTicks, BucketCount);

		var (attempts, ratio) = metrics.GetWindow(later, BucketTicks, BucketCount);

		// Cumulative would be (7, 6/7≈0.857); windowed must be only the 2 in-window attempts.
		attempts.ShouldBe(2);
		ratio.ShouldBe(0.5, tolerance: 1e-9);
	}

	[Fact]
	public void EvictRolledOutBucketsFromPersistedState_SoTheMetricNeverGrowsUnbounded()
	{
		var metrics = new DistributedCircuitMetrics();
		const long t0 = 100 * BucketTicks;

		metrics.RecordWindow(failure: true, t0, BucketTicks, BucketCount);

		// A record a full window later must evict the t0 bucket (RemoveAll in RecordWindow).
		var later = t0 + WindowTicks;
		metrics.RecordWindow(failure: false, later, BucketTicks, BucketCount);

		var minEpoch = (later / BucketTicks) - BucketCount + 1;
		metrics.Windows.ShouldNotContain(b => b.Epoch < minEpoch,
			"rolled-out buckets must be evicted so the persisted window is bounded (time-decay, not cumulative).");
	}

	[Fact]
	public void ReportZeroRatio_WhenNoAttemptsAreInTheWindow()
	{
		var metrics = new DistributedCircuitMetrics();

		var (attempts, ratio) = metrics.GetWindow(100 * BucketTicks, BucketTicks, BucketCount);

		attempts.ShouldBe(0);
		ratio.ShouldBe(0.0, tolerance: 1e-9);
	}

	[Theory]
	// below MinimumThroughput → never trips, even at a 100% in-window failure ratio
	[InlineData(3, 3, 5, 0.5, false)]
	// at/above MinimumThroughput AND ratio strictly above FailureRatio → trips
	[InlineData(10, 6, 5, 0.5, true)]
	// enough throughput but ratio not strictly above the threshold → does NOT trip (boundary is '>', not '>=')
	[InlineData(10, 5, 5, 0.5, false)]
	public void DriveTheDocumentedOpenCondition_FromTheInWindowQuantities(
		int attempts, int failures, int minimumThroughput, double failureRatio, bool expectedTrip)
	{
		var metrics = new DistributedCircuitMetrics();
		const long now = 100 * BucketTicks;

		for (var i = 0; i < failures; i++) metrics.RecordWindow(failure: true, now, BucketTicks, BucketCount);
		for (var i = 0; i < attempts - failures; i++) metrics.RecordWindow(failure: false, now, BucketTicks, BucketCount);

		var (windowAttempts, windowRatio) = metrics.GetWindow(now, BucketTicks, BucketCount);

		// The breaker's open decision (DistributedCircuitBreaker): windowed throughput gate AND windowed ratio.
		var trips = windowAttempts >= minimumThroughput && windowRatio > failureRatio;

		trips.ShouldBe(expectedTrip);
	}
}
