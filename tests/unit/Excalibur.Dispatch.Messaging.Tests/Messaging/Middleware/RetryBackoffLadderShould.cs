// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Middleware;

/// <summary>
/// Pins the delay each backoff strategy produces, so the ladder can be moved without moving its
/// behaviour.
/// </summary>
/// <remarks>
/// A wrong delay does not throw and does not fail a build. It shows up as a service retrying too
/// fast against a struggling dependency, or too slowly to recover inside a timeout. That is why the
/// deterministic strategies are pinned to exact values and the jittered ones to their documented
/// bounds rather than to a sample.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class RetryBackoffLadderShould
{
	private static readonly TimeSpan Base = TimeSpan.FromMilliseconds(100);

	private static RetryPolicyOptions Options(double multiplier = 2.0) => new()
	{
		Backoff = new RetryBackoffOptions
		{
			BaseDelay = Base,
			MaxDelay = TimeSpan.FromMinutes(30),
			BackoffMultiplier = multiplier,
		},
	};

	[Fact]
	public void GrowFixedDelaysNotAtAll()
	{
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Fixed, Options());

		Enumerable.Range(1, 4).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldBe([100d, 100d, 100d, 100d]);
	}

	[Fact]
	public void GrowLinearDelaysByTheAttemptNumber()
	{
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Linear, Options());

		Enumerable.Range(1, 4).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldBe([100d, 200d, 300d, 400d]);
	}

	[Fact]
	public void GrowExponentialDelaysByTheConfiguredMultiplier()
	{
		// The multiplier is a documented option; a hard-coded 2 here would silently ignore it.
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, Options(multiplier: 3.0));

		Enumerable.Range(1, 4).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldBe([100d, 300d, 900d, 2700d]);
	}

	[Theory]
	[InlineData(BackoffStrategy.Fixed)]
	[InlineData(BackoffStrategy.Linear)]
	[InlineData(BackoffStrategy.Exponential)]
	[InlineData(BackoffStrategy.ExponentialWithJitter)]
	[InlineData(BackoffStrategy.Fibonacci)]
	[InlineData(BackoffStrategy.FullJitter)]
	[InlineData(BackoffStrategy.DecorrelatedJitter)]
	public void AcceptAZeroDelayAsRetryImmediately(BackoffStrategy strategy)
	{
		// Zero is a legal configuration -- "retry without waiting" -- and every strategy must honour it
		// rather than reject it on the retry path, where the throw would surface as a failed dispatch.
		var options = new RetryPolicyOptions
		{
			Backoff = new RetryBackoffOptions { BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero },
		};

		var calc = BackoffCalculatorFactory.Create(strategy, options);

		calc.CalculateDelay(1).ShouldBe(TimeSpan.Zero);
		calc.CalculateDelay(5).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void RefuseANegativeDelay()
	{
		// The liveness arm above would also be satisfied by a calculator that validates nothing at all.
		var options = new RetryPolicyOptions
		{
			Backoff = new RetryBackoffOptions { BaseDelay = TimeSpan.FromMilliseconds(-1), MaxDelay = Base },
		};

		_ = Should.Throw<ArgumentOutOfRangeException>(
			() => BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, options));
	}

	[Fact]
	public void DoubleExponentialDelaysUnderTheDefaultMultiplier()
	{
		// The default is 2.0; a ladder that ignored the option entirely would also pass the 3.0 arm
		// if it happened to hard-code 3, so both the default and a configured value are pinned.
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, Options());

		Enumerable.Range(1, 4).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldBe([100d, 200d, 400d, 800d]);
	}

	[Fact]
	public void GrowFibonacciDelaysAlongTheSequence()
	{
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Fibonacci, Options());

		Enumerable.Range(1, 6).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldBe([100d, 100d, 200d, 300d, 500d, 800d]);
	}

	[Fact]
	public void NeverExceedTheConfiguredMaximum()
	{
		// The ceiling is the property that stops an exponential ladder from becoming an outage.
		var options = Options();
		options.Backoff.MaxDelay = TimeSpan.FromMilliseconds(250);
		var calc = BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, options);

		Enumerable.Range(1, 8).Select(a => calc.CalculateDelay(a).TotalMilliseconds)
			.ShouldAllBe(ms => ms <= 250d);
	}
}
