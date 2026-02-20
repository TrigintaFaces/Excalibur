// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="AdaptiveWaitStrategy"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Channels")]
public sealed class AdaptiveWaitStrategyShould
{
	[Fact]
	public void ImplementIWaitStrategy()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy();

		// Assert
		strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(AdaptiveWaitStrategy).IsPublic.ShouldBeTrue();
		typeof(AdaptiveWaitStrategy).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExtendWaitStrategyBase()
	{
		// Assert
		typeof(AdaptiveWaitStrategy).BaseType.ShouldBe(typeof(WaitStrategyBase));
	}

	[Fact]
	public void AcceptDefaultParameters()
	{
		// Act
		var strategy = new AdaptiveWaitStrategy();

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomMaxSpinCount()
	{
		// Act
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 50);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomContentionThreshold()
	{
		// Act
		var strategy = new AdaptiveWaitStrategy(contentionThreshold: 5);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptBothCustomParameters()
	{
		// Act
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 200, contentionThreshold: 20);

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenMaxSpinCountIsZero()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new AdaptiveWaitStrategy(maxSpinCount: 0));
	}

	[Fact]
	public void ThrowWhenMaxSpinCountIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new AdaptiveWaitStrategy(maxSpinCount: -1));
	}

	[Fact]
	public void ThrowWhenContentionThresholdIsZero()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new AdaptiveWaitStrategy(contentionThreshold: 0));
	}

	[Fact]
	public void ThrowWhenContentionThresholdIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new AdaptiveWaitStrategy(contentionThreshold: -1));
	}

	[Fact]
	public async Task WaitAsyncReturnsTrueWhenConditionIsMet()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsyncReturnsFalseWhenCancelled()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy();
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
		var strategy = new AdaptiveWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task WaitAsyncWaitsUntilConditionIsMet()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy();
		var counter = 0;
		var maxIterations = 5;

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
		var strategy = new AdaptiveWaitStrategy();

		// Act & Assert - Should not throw
		Should.NotThrow(() => strategy.Reset());
	}

	[Fact]
	public async Task ResetAllowsReuse()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy();

		// First wait
		await strategy.WaitAsync(() => true, CancellationToken.None);

		// Reset
		strategy.Reset();

		// Second wait should work
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}
}
