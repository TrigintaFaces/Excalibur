// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="FixedBackoffCalculator" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FixedBackoffCalculatorShould
{
	[Fact]
	public void CreateWithValidDelay()
	{
		var sut = new FixedBackoffCalculator(TimeSpan.FromSeconds(5));

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowForNegativeDelay()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new FixedBackoffCalculator(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void AllowZeroDelay()
	{
		var sut = new FixedBackoffCalculator(TimeSpan.Zero);

		sut.CalculateDelay(1).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ReturnSameDelayForEveryAttempt()
	{
		var delay = TimeSpan.FromSeconds(3);
		var sut = new FixedBackoffCalculator(delay);

		sut.CalculateDelay(1).ShouldBe(delay);
		sut.CalculateDelay(2).ShouldBe(delay);
		sut.CalculateDelay(5).ShouldBe(delay);
		sut.CalculateDelay(100).ShouldBe(delay);
	}

	[Fact]
	public void ThrowForAttemptLessThanOne()
	{
		var sut = new FixedBackoffCalculator(TimeSpan.FromSeconds(1));

		Should.Throw<ArgumentOutOfRangeException>(() => sut.CalculateDelay(0));
		Should.Throw<ArgumentOutOfRangeException>(() => sut.CalculateDelay(-1));
	}

	[Fact]
	public void ImplementIBackoffCalculator()
	{
		var sut = new FixedBackoffCalculator(TimeSpan.FromSeconds(1));

		sut.ShouldBeAssignableTo<IBackoffCalculator>();
	}
}
