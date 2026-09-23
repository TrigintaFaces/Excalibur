// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.Diagnostics;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// The bound on a CDC reconnect loop: the delay grows and is capped, the count resets on progress or on a
/// stable connection, and a configured limit stops the loop on exactly the failure that reaches it.
/// </summary>
/// <remarks>
/// A failure the framework does not recognise is transient by design, so without this bound a condition
/// that never clears is retried forever at a fixed interval and nothing reports it. Every arm drives the
/// clock by hand; none waits on wall time.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcTransientFailureBackoffShould
{
	private static readonly TimeSpan Base = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

	/// <summary>
	/// SAFETY. Repeated failures back off instead of retrying at a fixed interval.
	/// </summary>
	/// <remarks>RED against a loop that always waits the base interval.</remarks>
	[Fact]
	public void DoubleTheDelay_UntilItReachesTheMaximum()
	{
		var backoff = new CdcTransientFailureBackoff(Base, Max, null, new FakeTimeProvider(), null);

		var delays = Enumerable.Range(0, 7).Select(_ => backoff.RecordTransientFailure().Delay).ToList();

		delays.ShouldBe(
		[
			TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8),
			TimeSpan.FromSeconds(16), Max, Max,
		]);
	}

	/// <summary>
	/// SAFETY. A count in the thousands returns the maximum rather than overflowing.
	/// </summary>
	/// <remarks>
	/// RED against a plain multiplication: <see cref="TimeSpan"/> arithmetic throws on overflow, and that
	/// exception would escape the catch of a loop configured to retry forever.
	/// </remarks>
	[Theory]
	// Counts whose doubling count is a multiple of 64: C# masks a long shift count to six bits, so an
	// unguarded shift by 64 is a shift by 0 and would return the base delay instead of the maximum.
	[InlineData(65)]
	[InlineData(129)]
	[InlineData(63)]
	[InlineData(64)]
	[InlineData(10_000)]
	[InlineData(int.MaxValue)]
	public void ReturnTheMaximum_ForAnyLargeCount(int consecutiveFailures)
	{
		var backoff = new CdcTransientFailureBackoff(Base, Max, null, new FakeTimeProvider(), null);

		backoff.ComputeDelay(consecutiveFailures).ShouldBe(Max);
	}

	/// <summary>
	/// SAFETY. A configured limit stops the loop on the failure that reaches it, and not before.
	/// </summary>
	[Fact]
	public void ReportExhaustion_OnExactlyTheFailureThatReachesTheLimit()
	{
		var backoff = new CdcTransientFailureBackoff(Base, Max, 3, new FakeTimeProvider(), null);

		backoff.RecordTransientFailure().Exhausted.ShouldBeFalse();
		backoff.RecordTransientFailure().Exhausted.ShouldBeFalse();

		var third = backoff.RecordTransientFailure();
		third.Exhausted.ShouldBeTrue();
		third.ConsecutiveFailures.ShouldBe(3);
	}

	/// <summary>
	/// LIVENESS. Without a limit the loop never stops on its own.
	/// </summary>
	[Fact]
	public void NeverReportExhaustion_WhenNoLimitIsSet()
	{
		var backoff = new CdcTransientFailureBackoff(Base, Max, null, new FakeTimeProvider(), null);

		for (var i = 0; i < 50; i++)
		{
			backoff.RecordTransientFailure().Exhausted.ShouldBeFalse();
		}

		backoff.ConsecutiveFailures.ShouldBe(50);
	}

	/// <summary>
	/// LIVENESS. Progress ends a run of failures: the count and the delay start again from the base.
	/// </summary>
	/// <remarks>RED against a count that only ever grows: an intermittent fault eventually stops a working loop.</remarks>
	[Fact]
	public void StartAgainFromTheBase_AfterProgress()
	{
		var backoff = new CdcTransientFailureBackoff(Base, Max, 3, new FakeTimeProvider(), null);
		_ = backoff.RecordTransientFailure();
		_ = backoff.RecordTransientFailure();

		backoff.RecordProgress();

		backoff.ConsecutiveFailures.ShouldBe(0);
		var next = backoff.RecordTransientFailure();
		next.Delay.ShouldBe(Base);
		next.Exhausted.ShouldBeFalse();
	}

	/// <summary>
	/// LIVENESS. A connection that stayed up longer than the maximum delay before failing was healthy.
	/// </summary>
	/// <remarks>
	/// A quiet source has nothing to confirm, so it never reports progress; an idle timeout that drops it every
	/// few minutes must not add up to a stop. RED against a count that only progress can reset.
	/// </remarks>
	[Fact]
	public void NotCountTowardsTheLimit_AFailureAfterAStableConnection()
	{
		var clock = new FakeTimeProvider();
		var backoff = new CdcTransientFailureBackoff(Base, Max, 2, clock, null);

		for (var i = 0; i < 10; i++)
		{
			backoff.BeginAttempt();
			clock.Advance(Max);

			var outcome = backoff.RecordTransientFailure();
			outcome.Exhausted.ShouldBeFalse($"attempt {i + 1} stayed up for the maximum delay and was healthy");
			outcome.ConsecutiveFailures.ShouldBe(1);
		}
	}

	/// <summary>
	/// SAFETY. A connection that fails quickly, every time, does count.
	/// </summary>
	/// <remarks>The paired arm for the one above: without it a stable-interval rule that reset everything would pass.</remarks>
	[Fact]
	public void CountTowardsTheLimit_FailuresThatComeQuickly()
	{
		var clock = new FakeTimeProvider();
		var backoff = new CdcTransientFailureBackoff(Base, Max, 2, clock, null);

		backoff.BeginAttempt();
		clock.Advance(TimeSpan.FromMilliseconds(100));
		backoff.RecordTransientFailure().Exhausted.ShouldBeFalse();

		backoff.BeginAttempt();
		clock.Advance(TimeSpan.FromMilliseconds(100));
		backoff.RecordTransientFailure().Exhausted.ShouldBeTrue();
	}

	/// <summary>
	/// The count reaches the health state as it changes, including the return to zero.
	/// </summary>
	[Fact]
	public void PublishTheCountToTheHealthState()
	{
		var health = new CdcHealthState();
		health.RecordConsecutiveTransientFailures(7);

		var backoff = new CdcTransientFailureBackoff(Base, Max, null, new FakeTimeProvider(), health);
		health.ConsecutiveTransientFailures.ShouldBe(0, "a new loop starts from zero, not from a previous loop's count");

		_ = backoff.RecordTransientFailure();
		_ = backoff.RecordTransientFailure();
		health.ConsecutiveTransientFailures.ShouldBe(2);

		backoff.RecordProgress();
		health.ConsecutiveTransientFailures.ShouldBe(0);
	}

	/// <summary>
	/// A maximum below the provider's own interval is raised to it, so the loop never retries faster than it
	/// was configured to wait.
	/// </summary>
	[Fact]
	public void NeverWaitLessThanTheBaseInterval()
	{
		var fiveMinutes = TimeSpan.FromMinutes(5);
		var backoff = new CdcTransientFailureBackoff(fiveMinutes, TimeSpan.FromMinutes(1), null, new FakeTimeProvider(), null);

		backoff.RecordTransientFailure().Delay.ShouldBe(fiveMinutes);
		backoff.RecordTransientFailure().Delay.ShouldBe(fiveMinutes);
	}
}
