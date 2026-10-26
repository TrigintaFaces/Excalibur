// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.DataProcessing.Tests.DataProcessing;

/// <summary>
/// Binds the cancellation semantics the stall clock is built on, against the real runtime rather than
/// against documentation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these arms exist at this level.</b> The orchestration fix rests on three behaviours of
/// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>: a later call REPLACES the pending
/// deadline rather than adding to it; <see cref="Timeout.InfiniteTimeSpan"/> DISARMS a pending deadline
/// without cancelling; and a source linked to a cancelled parent cancels. Every one of those was
/// available to me only as third-party prose, and a timeout built on a misremembered API fails in
/// production rather than here. So they are pinned where they can go red.
/// </para>
/// <para>
/// Real time, not a <see cref="TimeProvider"/>: these arms are about the behaviour of the platform timer
/// itself, and substituting a fake clock would test the fake. Margins are generous for that reason —
/// the arms assert an ORDER of events, never a duration.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "DataProcessing")]
public sealed class ProcessingStallTimeoutShould
{
	private static readonly TimeSpan Stall = TimeSpan.FromMilliseconds(400);

	[Fact]
	public async Task NotCancelWorkThatKeepsMakingProgress()
	{
		// LIVENESS, and the whole point of the change: total elapsed time far exceeds the stall budget and
		// the task survives, because it never went quiet. This is exactly the case the previous total
		// deadline failed, and it is RED if RecordProgress stops re-arming.
		using var deadline = new ProcessingDeadline(Stall, totalTimeout: null, _ => { });

		for (var i = 0; i < 8; i++)
		{
			await Task.Delay(Stall / 4, TestContext.Current.CancellationToken);
			deadline.RecordProgress();
		}

		deadline.Token.IsCancellationRequested.ShouldBeFalse(
			"progress must REPLACE the pending deadline; if it accumulated or was ignored, 8 intervals "
			+ "totalling twice the budget would have cancelled");
		deadline.Classify().ShouldBe(ProcessingDeadlineOutcome.None);
	}

	[Fact]
	public async Task CancelWorkThatGoesQuiet()
	{
		// SAFETY. Without this arm, a deadline that simply never arms passes the arm above.
		using var deadline = new ProcessingDeadline(Stall, totalTimeout: null, _ => { });

		await Task.Delay(Stall * 3, TestContext.Current.CancellationToken);

		deadline.Token.IsCancellationRequested.ShouldBeTrue("a task that made no progress must cancel");
		deadline.Classify().ShouldBe(ProcessingDeadlineOutcome.Stalled);
	}

	[Fact]
	public async Task StopAPendingDeadlineWithoutCancellingWhenDisarmed()
	{
		// The post-run disarm. Without it, a task that finished just before its clock expired is reported
		// as a stall.
		using var deadline = new ProcessingDeadline(Stall, totalTimeout: null, _ => { });
		deadline.Disarm();

		await Task.Delay(Stall * 3, TestContext.Current.CancellationToken);

		deadline.Token.IsCancellationRequested.ShouldBeFalse(
			"disarming must cancel the pending deadline without cancelling the work");
		deadline.Classify().ShouldBe(ProcessingDeadlineOutcome.None);
	}

	[Fact]
	public async Task ReportTheTotalBudgetSeparatelyFromAStall()
	{
		// The cap must fire THROUGH the link and stay separately attributable -- progress throughout, so a
		// classifier that merely reported "cancelled" would name the wrong fault and the wrong fix.
		using var deadline = new ProcessingDeadline(Stall * 10, totalTimeout: Stall, _ => { });

		for (var i = 0; i < 6; i++)
		{
			await Task.Delay(Stall / 2, TestContext.Current.CancellationToken);
			deadline.RecordProgress();
		}

		deadline.Token.IsCancellationRequested.ShouldBeTrue("the cap must reach the work through the link");
		deadline.Classify().ShouldBe(
			ProcessingDeadlineOutcome.TotalBudgetExceeded,
			"a task cancelled by the absolute cap while progressing must NOT be reported as a stall");
	}

	[Fact]
	public async Task ObserveTheCallersCancellationWithoutBlamingEitherClock()
	{
		using var caller = new CancellationTokenSource();
		using var deadline = new ProcessingDeadline(Stall * 10, totalTimeout: null, _ => { }, clock: null, caller.Token);

		await caller.CancelAsync();

		deadline.Token.IsCancellationRequested.ShouldBeTrue();
		deadline.Classify().ShouldBe(
			ProcessingDeadlineOutcome.None,
			"a shutdown is not a timeout; attributing it to a clock would report a fault that did not occur");
	}

	[Fact]
	public void DefaultToAStallBudgetRatherThanATotalOne()
	{
		var options = new DataProcessingOptions();

		options.ProcessingStallTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.ProcessingTotalTimeout.ShouldBeNull(
			"an absolute cap re-introduces the failure this change removed, so it must be opt-in");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void RefuseANonPositiveStallBudget(int seconds) =>
		Should.Throw<ArgumentOutOfRangeException>(
			() => new DataProcessingOptions { ProcessingStallTimeout = TimeSpan.FromSeconds(seconds) });

	[Fact]
	public void RefuseATotalBudgetBelowTheStallBudget()
	{
		// Unsatisfiable by construction: the cap would fire before the stall clock could expire once, so
		// every task fails and the message blames the cap instead of the misconfiguration.
		var result = new DataProcessingOptionsValidator().Validate(
			null,
			new DataProcessingOptions
			{
				ProcessingStallTimeout = TimeSpan.FromSeconds(60),
				ProcessingTotalTimeout = TimeSpan.FromSeconds(30),
			});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ProcessingTotalTimeout");
	}

	[Fact]
	public async Task ObserveAConsumerCancellationCallbackThatThROWS()
	{
		// REGRESSION. The deadline cancels on a TIMER THREAD, and cancellation callbacks run on whichever
		// thread cancels -- so a consumer registration that throws escapes as an unhandled background
		// exception unless the cancel is wrapped where it is invoked. That protection existed, was deleted
		// when this policy was extracted, and was caught only by an integration test that reported its own
		// arms as passing while the RUN failed. It is bound here, where it is cheap to keep.
		Exception? observed = null;
		using var deadline = new ProcessingDeadline(Stall, totalTimeout: null, e => observed = e);
		using var registration = deadline.Token.Register(
			() => throw new InvalidOperationException("Cancellation callback failed"));

		await Task.Delay(Stall * 3, TestContext.Current.CancellationToken);

		deadline.Token.IsCancellationRequested.ShouldBeTrue("cancellation must still succeed");
		observed.ShouldNotBeNull("a throwing consumer callback must be REPORTED, never left unobserved");
		observed!.ToString().ShouldContain("Cancellation callback failed");
	}
}
