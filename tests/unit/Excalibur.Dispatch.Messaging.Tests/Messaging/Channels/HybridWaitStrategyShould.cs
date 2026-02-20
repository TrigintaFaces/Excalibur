// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="HybridWaitStrategy"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Channels")]
public sealed class HybridWaitStrategyShould
{
	[Fact]
	public void ImplementIWaitStrategy()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();

		// Assert
		strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(HybridWaitStrategy).IsPublic.ShouldBeTrue();
		typeof(HybridWaitStrategy).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExtendWaitStrategyBase()
	{
		// Assert
		typeof(HybridWaitStrategy).BaseType.ShouldBe(typeof(WaitStrategyBase));
	}

	[Fact]
	public void AcceptDefaultParameters()
	{
		// Act
		var strategy = new HybridWaitStrategy();

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomSpinCount()
	{
		// Act
		var strategy = new HybridWaitStrategy(spinCount: 20);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomDelayMilliseconds()
	{
		// Act
		var strategy = new HybridWaitStrategy(delayMilliseconds: 5);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptBothCustomParameters()
	{
		// Act
		var strategy = new HybridWaitStrategy(spinCount: 50, delayMilliseconds: 10);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenSpinCountIsZero()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HybridWaitStrategy(spinCount: 0));
	}

	[Fact]
	public void ThrowWhenSpinCountIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HybridWaitStrategy(spinCount: -1));
	}

	[Fact]
	public void ThrowWhenDelayMillisecondsIsZero()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HybridWaitStrategy(delayMilliseconds: 0));
	}

	[Fact]
	public void ThrowWhenDelayMillisecondsIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new HybridWaitStrategy(delayMilliseconds: -1));
	}

	[Fact]
	public async Task WaitAsyncReturnsTrueWhenConditionIsMet()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsyncReturnsFalseWhenCancelled()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var result = await strategy.WaitAsync(() => false, cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task WaitAsyncThrowsWhenConditionIsNull()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task WaitAsyncWaitsUntilConditionIsMet()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();
		var counter = 0;
		var maxIterations = 3;

		// Act
		var result = await strategy.WaitAsync(() =>
		{
			counter++;
			return counter >= maxIterations;
		}, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		counter.ShouldBeGreaterThanOrEqualTo(maxIterations);
	}

	[Fact]
	public void ResetClearsState()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();

		// Act & Assert - Should not throw
		Should.NotThrow(() => strategy.Reset());
	}

	[Fact]
	public async Task ResetAllowsReuse()
	{
		// Arrange
		var strategy = new HybridWaitStrategy();

		// First wait
		await strategy.WaitAsync(() => true, CancellationToken.None);

		// Reset
		strategy.Reset();

		// Second wait should work
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task UseTimerWhenSpinningDoesNotSucceed()
	{
		// Arrange
		var strategy = new HybridWaitStrategy(spinCount: 1, delayMilliseconds: 1);
		var callCount = 0;

		// Act - Condition will take more than spin count to succeed
		var result = await strategy.WaitAsync(() =>
		{
			callCount++;
			return callCount >= 15; // More than spin count
		}, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}
}
