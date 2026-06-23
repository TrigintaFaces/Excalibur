// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Independent regression lock for <see cref="RollingErrorWindow"/> (bd-zj9f12, author≠implementer).
/// </summary>
/// <remarks>
/// The pre-fix GracefulDegradation error rate was computed from lifetime-cumulative
/// <c>OperationStatistics</c> counters whose denominator only grows, so a recent error burst could
/// never move the ratio once a long-running service had warmed up — error-rate auto-degradation was
/// effectively dead. The fix replaces that with this Polly-style bucketed sliding window.
///
/// <para>
/// These tests bind the windowing CONTRACT the fix exists to provide: in-window aggregation plus
/// aging-out of past history. They are deterministic — <see cref="RollingErrorWindow"/> is a pure
/// function of the supplied <c>nowTicks</c>, so synthetic tick values are used and there is no
/// wall-clock dependency. They are non-vacuous against the replaced lifetime-cumulative semantics:
/// a cumulative counter would NOT age failures out (<see cref="AgesOutFailures_OnceWindowElapses"/>)
/// and WOULD dilute a recent all-failure burst behind a large historical success denominator
/// (<see cref="RecentFailureBurst_IsNotDilutedByAgedOutHistory"/>).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class RollingErrorWindowShould : UnitTestBase
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private const int BucketCount = 6; // 10s per bucket

    [Fact]
    public void GetErrorRate_WithNoAttempts_ReturnsZero()
    {
        // Arrange
        var window = new RollingErrorWindow(Window, BucketCount);

        // Act
        var rate = window.GetErrorRate(nowTicks: 0);

        // Assert -- empty window has no attempts; ratio is defined as 0, never NaN (0/0 guard).
        rate.ShouldBe(0.0);
    }

    [Fact]
    public void GetErrorRate_AggregatesAttemptsAndFailuresWithinWindow()
    {
        // Arrange
        var window = new RollingErrorWindow(Window, BucketCount);
        var bucketTicks = Window.Ticks / BucketCount;

        // Two ops in bucket 0 (1 failure), two ops one bucket later (1 failure) -- both in-window.
        RecordOperation(window, nowTicks: 0, failed: true);
        RecordOperation(window, nowTicks: 0, failed: false);
        RecordOperation(window, nowTicks: bucketTicks, failed: true);
        RecordOperation(window, nowTicks: bucketTicks, failed: false);

        // Act -- read at the later bucket; both buckets are still inside the 60s window.
        var rate = window.GetErrorRate(nowTicks: bucketTicks);

        // Assert -- 2 failures / 4 attempts across the in-window buckets.
        rate.ShouldBe(0.5);
    }

    [Fact]
    public void AgesOutFailures_OnceWindowElapses()
    {
        // Arrange -- four consecutive failures at the window origin.
        var window = new RollingErrorWindow(Window, BucketCount);
        for (var i = 0; i < 4; i++)
        {
            RecordOperation(window, nowTicks: 0, failed: true);
        }

        window.GetErrorRate(nowTicks: 0).ShouldBe(1.0); // sanity: 4/4 at t0

        // Act -- advance a FULL window past the origin so the origin bucket falls out of range.
        var rate = window.GetErrorRate(nowTicks: Window.Ticks);

        // Assert (bd-zj9f12 lock): the aged-out failures no longer count -> rate drops to 0.
        // A lifetime-cumulative ratio (the pre-fix behavior) would still report 1.0 here -> RED.
        rate.ShouldBe(0.0);
    }

    [Fact]
    public void RecentFailureBurst_IsNotDilutedByAgedOutHistory()
    {
        // Arrange -- a large history of successes at the origin...
        var window = new RollingErrorWindow(Window, BucketCount);
        for (var i = 0; i < 1000; i++)
        {
            RecordOperation(window, nowTicks: 0, failed: false);
        }

        window.GetErrorRate(nowTicks: 0).ShouldBe(0.0); // sanity: 0/1000 at t0

        // ...then a small all-failure burst a FULL window later (old successes have aged out).
        var burstTicks = Window.Ticks;
        for (var i = 0; i < 5; i++)
        {
            RecordOperation(window, nowTicks: burstTicks, failed: true);
        }

        // Act
        var rate = window.GetErrorRate(nowTicks: burstTicks);

        // Assert (bd-zj9f12 lock): the recent burst reads 5/5 = 1.0, NOT diluted by the 1000 aged-out
        // successes. A cumulative counter would report 5/1005 ~= 0.005 and never trip a degradation
        // threshold -> this assertion is RED against the pre-fix lifetime-cumulative semantics.
        rate.ShouldBe(1.0);
    }

    [Fact]
    public void RecordFailure_DoesNotItselfCountAsAnAttempt()
    {
        // Arrange -- mirror OperationStatistics semantics: an attempt is counted once via
        // RecordAttempt; RecordFailure increments ONLY the failure counter.
        var window = new RollingErrorWindow(Window, BucketCount);

        window.RecordAttempt(nowTicks: 0);
        window.RecordFailure(nowTicks: 0);

        // Act
        var rate = window.GetErrorRate(nowTicks: 0);

        // Assert -- exactly one attempt, one failure -> 1.0 (not 1/2 from double-counting).
        rate.ShouldBe(1.0);
    }

    [Theory]
    [InlineData(0)]      // zero window
    [InlineData(-1)]     // negative window (ticks)
    public void Constructor_Throws_WhenWindowIsNotPositive(long windowTicks)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RollingErrorWindow(TimeSpan.FromTicks(windowTicks), BucketCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_WhenBucketCountIsLessThanOne(int bucketCount)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RollingErrorWindow(Window, bucketCount));
    }

    private static void RecordOperation(RollingErrorWindow window, long nowTicks, bool failed)
    {
        window.RecordAttempt(nowTicks);
        if (failed)
        {
            window.RecordFailure(nowTicks);
        }
    }
}
