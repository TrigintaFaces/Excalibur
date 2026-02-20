// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="LinearBackoffCalculator" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LinearBackoffCalculatorShould
{
	[Fact]
	public void CreateWithValidBaseDelay()
	{
		var sut = new LinearBackoffCalculator(TimeSpan.FromSeconds(1));

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowForZeroBaseDelay()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new LinearBackoffCalculator(TimeSpan.Zero));
	}

	[Fact]
	public void ThrowForNegativeBaseDelay()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new LinearBackoffCalculator(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void ThrowForNegativeMaxDelay()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new LinearBackoffCalculator(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void ThrowForZeroMaxDelay()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new LinearBackoffCalculator(TimeSpan.FromSeconds(1), TimeSpan.Zero));
	}

	[Fact]
	public void CalculateLinearDelay()
	{
		var sut = new LinearBackoffCalculator(TimeSpan.FromSeconds(2));

		sut.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(2));  // 2 * 1
		sut.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(4));  // 2 * 2
		sut.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(6));  // 2 * 3
		sut.CalculateDelay(5).ShouldBe(TimeSpan.FromSeconds(10)); // 2 * 5
	}

	[Fact]
	public void ClampToMaxDelay()
	{
		var sut = new LinearBackoffCalculator(
			TimeSpan.FromSeconds(5),
			TimeSpan.FromSeconds(15));

		sut.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(5));  // 5 * 1 = 5 (under cap)
		sut.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(15)); // 5 * 3 = 15 (at cap)
		sut.CalculateDelay(10).ShouldBe(TimeSpan.FromSeconds(15)); // 5 * 10 = 50 (clamped to 15)
	}

	[Fact]
	public void DefaultMaxDelayToThirtyMinutes()
	{
		var sut = new LinearBackoffCalculator(TimeSpan.FromMinutes(10));

		// 10 * 4 = 40 minutes, but should be clamped to 30 minutes
		sut.CalculateDelay(4).ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void ThrowForAttemptLessThanOne()
	{
		var sut = new LinearBackoffCalculator(TimeSpan.FromSeconds(1));

		Should.Throw<ArgumentOutOfRangeException>(() => sut.CalculateDelay(0));
		Should.Throw<ArgumentOutOfRangeException>(() => sut.CalculateDelay(-1));
	}

	[Fact]
	public void ImplementIBackoffCalculator()
	{
		var sut = new LinearBackoffCalculator(TimeSpan.FromSeconds(1));

		sut.ShouldBeAssignableTo<IBackoffCalculator>();
	}
}
