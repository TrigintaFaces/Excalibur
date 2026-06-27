// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Middleware.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Regression lock for S851 Lane 7 · <c>0armjn</c> — <see cref="RetryMiddleware"/>'s private
/// <c>ClampMs</c> delay seam, in particular the <b>non-finite branch</b> (pairs with MS-2 <c>0yum52</c>,
/// where exponential growth via <c>Math.Pow(BackoffMultiplier, n)</c> can overflow to
/// <c>PositiveInfinity</c>/<c>NaN</c> before any cap is applied).
/// </summary>
/// <remarks>
/// <para>
/// <c>ClampMs</c> is the single seam every backoff strategy funnels its raw millisecond delay through
/// before constructing a <see cref="TimeSpan"/>. A non-finite input must collapse to <c>maxDelay</c> —
/// otherwise <see cref="TimeSpan.FromMilliseconds(double)"/> throws on <c>NaN</c>/∞. Driven by reflection
/// (the same toolkit as <c>InboxProcessorBackoffShould</c>) since the method is <c>private static</c>.
/// </para>
/// <para>
/// <b>RED on the pre-fix surface:</b> remove the <c>if (!double.IsFinite(milliseconds)) return maxDelay;</c>
/// guard and the non-finite cases throw (instead of returning <c>maxDelay</c>) → the Theory rows go RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryMiddlewareClampMsShould
{
	private static readonly MethodInfo ClampMsMethod =
		typeof(RetryMiddleware).GetMethod("ClampMs", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException(
			"0armjn: RetryMiddleware.ClampMs(double, TimeSpan) not found — the bounded-delay seam is the " +
			"thing under test; its absence/rename is the pre-fix RED.");

	private static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

	private static TimeSpan ClampMs(double milliseconds, TimeSpan maxDelay) =>
		(TimeSpan)ClampMsMethod.Invoke(null, [milliseconds, maxDelay])!;

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void CollapseNonFiniteDelayToMaxDelay(double nonFinite) =>
		// The branch under test: a non-finite delay (overflowed exponential growth) must NOT reach
		// TimeSpan.FromMilliseconds (which throws on NaN/∞) — it collapses to the cap.
		ClampMs(nonFinite, Max).ShouldBe(Max);

	[Fact]
	public void CapFiniteOverMaxToMaxDelay() =>
		ClampMs(Max.TotalMilliseconds + 10_000d, Max).ShouldBe(Max);

	[Fact]
	public void FloorNegativeDelayToZero() =>
		ClampMs(-5d, Max).ShouldBe(TimeSpan.Zero);

	[Fact]
	public void PassThroughInRangeDelay() =>
		ClampMs(1_000d, Max).ShouldBe(TimeSpan.FromMilliseconds(1_000d));
}
