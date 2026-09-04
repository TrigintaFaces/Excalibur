// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Locks the recovery half of the circuit breaker, which had no coverage at all.
/// </summary>
/// <remarks>
/// <para>
/// The open-duration deadline was read from the ambient clock, so reaching half-open in a test meant
/// sleeping for it. That is why the transition nobody could test is the one nobody did, and why both
/// implementations were wrong there in different ways.
/// </para>
/// <para>
/// Two properties matter and neither is implied by the other: a failed probe must reopen at once
/// rather than admitting more traffic, and half-open must admit ONE probe rather than everything
/// that happens to arrive while it is open.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CircuitBreakerRecoveryShould
{
	private static readonly TimeSpan OpenFor = TimeSpan.FromSeconds(30);

	private static async Task<(CircuitBreakerPolicy Policy, FakeTimeProvider Clock)> TrippedAsync()
	{
		var clock = new FakeTimeProvider();
		var policy = new CircuitBreakerPolicy(
			new CircuitBreakerOptions { FailureThreshold = 1, OpenDuration = OpenFor },
			"recovery",
			logger: null,
			shouldHandle: null,
			timeProvider: clock);

		await policy.FailAsync(new InvalidOperationException("down")).ConfigureAwait(false);
		policy.State.ShouldBe(CircuitState.Open, "one failure at threshold 1 opens the circuit");

		return (policy, clock);
	}

	[Fact]
	public async Task StayOpenUntilTheDeadlinePasses()
	{
		var (policy, clock) = await TrippedAsync().ConfigureAwait(false);

		clock.Advance(OpenFor - TimeSpan.FromSeconds(1));
		policy.State.ShouldBe(CircuitState.Open, "the circuit must not probe early");

		clock.Advance(TimeSpan.FromSeconds(2));
		policy.State.ShouldBe(CircuitState.HalfOpen, "past the deadline the circuit offers a probe");
	}

	[Fact]
	public async Task ReopenImmediatelyWhenTheProbeFails()
	{
		// SAFETY. The in-house state machine set its failure count to 1 here and needed the full
		// threshold again, so a failed probe left the circuit admitting traffic.
		var (policy, clock) = await TrippedAsync().ConfigureAwait(false);
		clock.Advance(OpenFor + TimeSpan.FromSeconds(1));
		policy.State.ShouldBe(CircuitState.HalfOpen);

		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(
				_ => throw new InvalidOperationException("still down"),
				TestContext.Current.CancellationToken));

		policy.State.ShouldBe(CircuitState.Open, "a probe that failed means the dependency is still down");
	}

	[Fact]
	public async Task CloseWhenTheProbeSucceeds()
	{
		// LIVENESS for the arm above: a breaker that reopened on everything would satisfy it and
		// never recover.
		var (policy, clock) = await TrippedAsync().ConfigureAwait(false);
		clock.Advance(OpenFor + TimeSpan.FromSeconds(1));

		var value = await policy.ExecuteAsync(
			_ => Task.FromResult(7),
			TestContext.Current.CancellationToken);

		value.ShouldBe(7);
		policy.State.ShouldBe(CircuitState.Closed, "a successful probe means the dependency recovered");
	}

	[Fact]
	public async Task AdmitOnlyOneProbeWhileHalfOpen()
	{
        // SAFETY. Every request arriving during half-open used to see HalfOpen, pass the Open check
        // and proceed, so a recovering dependency met a stampede at its weakest moment.
		var (policy, clock) = await TrippedAsync().ConfigureAwait(false);
		clock.Advance(OpenFor + TimeSpan.FromSeconds(1));

		var probeEntered = new TaskCompletionSource();
		var releaseProbe = new TaskCompletionSource();

		var probe = policy.ExecuteAsync(
			async _ =>
			{
				probeEntered.SetResult();
				await releaseProbe.Task.ConfigureAwait(false);
				return 1;
			},
			TestContext.Current.CancellationToken);

		await probeEntered.Task;

		_ = await Should.ThrowAsync<CircuitBreakerOpenException>(async () =>
			await policy.ExecuteAsync(
				_ => Task.FromResult(2),
				TestContext.Current.CancellationToken));

		releaseProbe.SetResult();
		(await probe).ShouldBe(1);
	}
}
