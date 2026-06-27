// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Author≠impl regression lock for S851 Lane 2 · <c>0yum52</c> — <see cref="RetryMiddleware"/>'s
/// exponential backoff must use the <b>configured</b> <see cref="RetryOptions.BackoffMultiplier"/>, not a
/// hardcoded factor of 2 (so it matches the documented option + the Outbox/Inbox backoff growth).
/// </summary>
/// <remarks>
/// Drives the <c>private static CalculateDelay(RetryOptions, int)</c> seam by reflection (same toolkit as
/// <c>InboxProcessorBackoffShould</c>). With <see cref="BackoffStrategy.Exponential"/> the delay is
/// deterministic (jitter applies only to <see cref="BackoffStrategy.ExponentialWithJitter"/>):
/// <c>delay(attempt) = BaseDelay · BackoffMultiplier^(attempt-1)</c>, clamped to <c>MaxDelay</c>.
/// <para>
/// <b>RED on the pre-fix surface:</b> with the pre-<c>0yum52</c> hardcoded <c>2</c>, a configured
/// multiplier of <c>3.0</c> at attempt 3 yields <c>100·2² = 400ms</c> instead of <c>100·3² = 900ms</c> →
/// the configured-multiplier facts go RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryMiddlewareBackoffMultiplierShould
{
	private static readonly MethodInfo CalculateDelayMethod =
		typeof(RetryMiddleware).GetMethod("CalculateDelay", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException(
			"0yum52: RetryMiddleware.CalculateDelay(RetryOptions, int) not found — the configured-backoff " +
			"seam is the thing under test; its absence/rename is the pre-fix RED.");

	private static TimeSpan CalculateDelay(RetryOptions options, int attempt) =>
		(TimeSpan)CalculateDelayMethod.Invoke(null, [options, attempt])!;

	private static RetryOptions Exponential(double multiplier) => new()
	{
		BackoffStrategy = BackoffStrategy.Exponential,
		BaseDelay = TimeSpan.FromMilliseconds(100),
		BackoffMultiplier = multiplier,
		MaxDelay = TimeSpan.FromHours(1), // large enough that the values below are never clamped
	};

	[Fact]
	public void HonorConfiguredMultiplier_AtAttemptThree()
	{
		var options = Exponential(3.0);

		// 100 · 3^(3-1) = 900ms — uses the CONFIGURED multiplier. Pre-fix hardcoded 2 ⇒ 400ms (RED).
		CalculateDelay(options, 3).ShouldBe(TimeSpan.FromMilliseconds(900));
	}

	[Fact]
	public void HonorConfiguredMultiplier_AtAttemptTwo() =>
		// 100 · 3^1 = 300ms (pre-fix hardcoded 2 ⇒ 200ms).
		CalculateDelay(Exponential(3.0), 2).ShouldBe(TimeSpan.FromMilliseconds(300));

	[Fact]
	public void FirstAttempt_IsBaseDelay() =>
		// 100 · m^0 = 100ms for any multiplier (exponent 0).
		CalculateDelay(Exponential(3.0), 1).ShouldBe(TimeSpan.FromMilliseconds(100));

	[Fact]
	public void DefaultMultiplierIsTwo_GrowthDoublesEachAttempt() =>
		// Documents the default (BackoffMultiplier = 2.0): 100 · 2^2 = 400ms at attempt 3.
		CalculateDelay(Exponential(2.0), 3).ShouldBe(TimeSpan.FromMilliseconds(400));
}
