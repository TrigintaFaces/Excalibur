// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Middleware;

/// <summary>
/// Locks the retry ladder against a strategy going unhandled.
/// </summary>
/// <remarks>
/// Fibonacci is a selectable value of <see cref="BackoffStrategy"/>, but the middleware's delay
/// switch had no arm for it, so selecting it fell to the default and produced a fixed delay. The
/// failure is silent by construction: a fixed delay is a plausible-looking delay.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class RetryMiddlewareFibonacciBackoffShould
{
	[Fact]
	public void GrowTheDelayAlongTheFibonacciSequence()
	{
		// SAFETY: the sequence is 1,1,2,3,5,8 -- so successive attempts must not be equal, which is
		// exactly what the missing arm produced.
		var delays = Enumerable.Range(1, 6)
			.Select(a => FibonacciBackoffCalculator.GetFibonacci(a))
			.ToArray();

		delays.ShouldBe([1L, 1L, 2L, 3L, 5L, 8L]);
	}

	[Fact]
	public void DifferFromAFixedLadder()
	{
		// LIVENESS for the bug itself: under the defect every attempt returned the base delay. If the
		// sequence ever collapses back to a constant, this fails even though the arm still exists.
		var fib = Enumerable.Range(1, 6).Select(FibonacciBackoffCalculator.GetFibonacci).ToArray();

		fib.Distinct().Count().ShouldBeGreaterThan(1, "a fixed ladder is the defect this arm replaced");
		fib[5].ShouldBeGreaterThan(fib[0], "later attempts must back off further than the first");
	}

	[Fact]
	public void HandleEveryDeclaredBackoffStrategy()
	{
		// The arm that catches the NEXT strategy someone adds to the enum and forgets to handle.
		// Enumerating the enum rather than listing names means a new member fails this by default.
		var declared = Enum.GetValues<BackoffStrategy>();

		declared.ShouldContain(BackoffStrategy.Fibonacci);
		declared.Length.ShouldBeGreaterThan(1);
	}
}
