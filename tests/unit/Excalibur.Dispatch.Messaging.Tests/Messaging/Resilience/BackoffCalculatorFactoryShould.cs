// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="BackoffCalculatorFactory" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BackoffCalculatorFactoryShould
{
	private static RetryPolicyOptions CreateDefaultOptions() => new()
	{
		BaseDelay = TimeSpan.FromSeconds(1),
		MaxDelay = TimeSpan.FromMinutes(5),
		BackoffMultiplier = 2.0,
		JitterFactor = 0.1,
	};

	[Fact]
	public void CreateFixedCalculatorForFixedStrategy()
	{
		var result = BackoffCalculatorFactory.Create(BackoffStrategy.Fixed, CreateDefaultOptions());

		result.ShouldBeOfType<FixedBackoffCalculator>();
	}

	[Fact]
	public void CreateLinearCalculatorForLinearStrategy()
	{
		var result = BackoffCalculatorFactory.Create(BackoffStrategy.Linear, CreateDefaultOptions());

		result.ShouldBeOfType<LinearBackoffCalculator>();
	}

	[Fact]
	public void CreateExponentialCalculatorForExponentialStrategy()
	{
		var result = BackoffCalculatorFactory.Create(BackoffStrategy.Exponential, CreateDefaultOptions());

		result.ShouldBeOfType<ExponentialBackoffCalculator>();
	}

	[Fact]
	public void CreateExponentialWithJitterCalculatorForJitterStrategy()
	{
		var result = BackoffCalculatorFactory.Create(BackoffStrategy.ExponentialWithJitter, CreateDefaultOptions());

		result.ShouldBeOfType<ExponentialBackoffCalculator>();
	}

	[Fact]
	public void ThrowForUnknownBackoffStrategy()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			BackoffCalculatorFactory.Create((BackoffStrategy)999, CreateDefaultOptions()));
	}

	[Fact]
	public void ThrowForNullOptionsWithBackoffStrategy()
	{
		Should.Throw<ArgumentNullException>(() =>
			BackoffCalculatorFactory.Create(BackoffStrategy.Fixed, null!));
	}

	// RetryStrategy overload tests

	[Fact]
	public void CreateFixedCalculatorForFixedDelayRetryStrategy()
	{
		var result = BackoffCalculatorFactory.Create(RetryStrategy.FixedDelay, CreateDefaultOptions());

		result.ShouldBeOfType<FixedBackoffCalculator>();
	}

	[Fact]
	public void CreateExponentialCalculatorForExponentialBackoffRetryStrategy()
	{
		var result = BackoffCalculatorFactory.Create(RetryStrategy.ExponentialBackoff, CreateDefaultOptions());

		result.ShouldBeOfType<ExponentialBackoffCalculator>();
	}

	[Fact]
	public void CreateExponentialWithJitterWhenEnabledInOptions()
	{
		var options = CreateDefaultOptions();
		options.EnableJitter = true;

		var result = BackoffCalculatorFactory.Create(RetryStrategy.ExponentialBackoff, options);

		result.ShouldBeOfType<ExponentialBackoffCalculator>();
	}

	[Fact]
	public void ThrowForUnknownRetryStrategy()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			BackoffCalculatorFactory.Create((RetryStrategy)999, CreateDefaultOptions()));
	}

	[Fact]
	public void ThrowForNullOptionsWithRetryStrategy()
	{
		Should.Throw<ArgumentNullException>(() =>
			BackoffCalculatorFactory.Create(RetryStrategy.FixedDelay, null!));
	}
}
