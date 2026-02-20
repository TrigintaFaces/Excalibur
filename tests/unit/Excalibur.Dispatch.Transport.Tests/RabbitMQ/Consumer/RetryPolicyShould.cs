// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using RmqRetryPolicy = Excalibur.Dispatch.Transport.RabbitMQ.RetryPolicy;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Consumer;

/// <summary>
/// Unit tests for <see cref="RmqRetryPolicy"/> configuration and delay calculation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RetryPolicyShould : UnitTestBase
{
	[Fact]
	public void None_ReturnsZeroMaxRetries()
	{
		var policy = RmqRetryPolicy.None();

		policy.MaxRetries.ShouldBe(0);
		policy.UseExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void Fixed_SetsMaxRetriesAndConstantDelay()
	{
		var policy = RmqRetryPolicy.Fixed(5, TimeSpan.FromSeconds(3));

		policy.MaxRetries.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(3));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(3));
		policy.UseExponentialBackoff.ShouldBeFalse();
	}

	[Fact]
	public void Fixed_UsesDefaultDelayWhenNotProvided()
	{
		var policy = RmqRetryPolicy.Fixed(3);

		policy.MaxRetries.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Exponential_SetsBackoffProperties()
	{
		var policy = RmqRetryPolicy.Exponential(
			maxRetries: 5,
			initialDelay: TimeSpan.FromSeconds(2),
			maxDelay: TimeSpan.FromMinutes(1),
			multiplier: 3.0);

		policy.MaxRetries.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(2));
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		policy.BackoffMultiplier.ShouldBe(3.0);
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void Exponential_UsesDefaultsWhenNotProvided()
	{
		var policy = RmqRetryPolicy.Exponential(maxRetries: 3);

		policy.MaxRetries.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
		policy.BackoffMultiplier.ShouldBe(2.0);
		policy.UseExponentialBackoff.ShouldBeTrue();
	}

	[Fact]
	public void CalculateDelay_ReturnsZeroForZeroOrNegativeAttempt()
	{
		var policy = RmqRetryPolicy.Exponential(maxRetries: 5);

		policy.CalculateDelay(0).ShouldBe(TimeSpan.Zero);
		policy.CalculateDelay(-1).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateDelay_ReturnsConstantDelayForFixedPolicy()
	{
		var delay = TimeSpan.FromSeconds(5);
		var policy = RmqRetryPolicy.Fixed(3, delay);

		policy.CalculateDelay(1).ShouldBe(delay);
		policy.CalculateDelay(2).ShouldBe(delay);
		policy.CalculateDelay(3).ShouldBe(delay);
	}

	[Fact]
	public void CalculateDelay_AppliesExponentialBackoff()
	{
		var policy = RmqRetryPolicy.Exponential(
			maxRetries: 5,
			initialDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(10),
			multiplier: 2.0);

		// 1 * 2^0 = 1s
		policy.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(1));
		// 1 * 2^1 = 2s
		policy.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(2));
		// 1 * 2^2 = 4s
		policy.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(4));
		// 1 * 2^3 = 8s
		policy.CalculateDelay(4).ShouldBe(TimeSpan.FromSeconds(8));
	}

	[Fact]
	public void CalculateDelay_CapsAtMaxDelay()
	{
		var policy = RmqRetryPolicy.Exponential(
			maxRetries: 10,
			initialDelay: TimeSpan.FromSeconds(10),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 2.0);

		// 10 * 2^0 = 10s
		policy.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(10));
		// 10 * 2^1 = 20s
		policy.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(20));
		// 10 * 2^2 = 40s, capped to 30s
		policy.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(30));
		// 10 * 2^3 = 80s, capped to 30s
		policy.CalculateDelay(4).ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(2, 3)]
	[InlineData(3, 9)]
	[InlineData(4, 27)]
	public void CalculateDelay_WorksWithCustomMultiplier(int attempt, int expectedSeconds)
	{
		var policy = RmqRetryPolicy.Exponential(
			maxRetries: 10,
			initialDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(10),
			multiplier: 3.0);

		policy.CalculateDelay(attempt).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
	}
}
