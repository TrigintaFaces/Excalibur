// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="YieldWaitStrategy"/>.
/// </summary>
/// <remarks>
/// Tests the Task.Yield-based wait strategy implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class YieldWaitStrategyShould : IDisposable
{
	private readonly YieldWaitStrategy _strategy;

	public YieldWaitStrategyShould()
	{
		_strategy = new YieldWaitStrategy();
	}

	public void Dispose()
	{
		_strategy.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesInstance()
	{
		// Arrange & Act
		using var strategy = new YieldWaitStrategy();

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	#endregion

	#region WaitAsync Tests

	[Fact]
	public async Task WaitAsync_WithTrueCondition_ReturnsImmediately()
	{
		// Arrange
		var conditionCalled = false;

		// Act
		var result = await _strategy.WaitAsync(() =>
		{
			conditionCalled = true;
			return true;
		}, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		conditionCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsync_WithNullCondition_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task WaitAsync_WhenConditionBecomesTrueAfterWaiting_ReturnsTrue()
	{
		// Arrange
		var callCount = 0;

		// Act
		var result = await _strategy.WaitAsync(() =>
		{
			callCount++;
			return callCount >= 3;
		}, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		callCount.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task WaitAsync_WhenCancelled_ReturnsFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var callCount = 0;

		// Start the wait in a task
		var waitTask = Task.Run(async () =>
			await _strategy.WaitAsync(() =>
			{
				callCount++;
				if (callCount > 10)
				{
					cts.Cancel();
				}

				return false;
			}, cts.Token));

		// Act
		var result = await waitTask;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task WaitAsync_WhenAlreadyCancelled_ReturnsFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		var result = await _strategy.WaitAsync(
			() => false,
			cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task WaitAsync_WithTrueConditionAndCancellation_ReturnsTrue()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act
		var result = await _strategy.WaitAsync(
			() => true,
			cts.Token);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsync_ConditionCalledMultipleTimes()
	{
		// Arrange
		var callCount = 0;

		// Act
		_ = await _strategy.WaitAsync(() =>
		{
			callCount++;
			return callCount >= 5;
		}, CancellationToken.None);

		// Assert
		callCount.ShouldBe(5);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_DoesNotThrow()
	{
		// Act & Assert - Should not throw
		_strategy.Reset();
	}

	[Fact]
	public void Reset_CanBeCalledMultipleTimes()
	{
		// Act & Assert - Should not throw
		_strategy.Reset();
		_strategy.Reset();
		_strategy.Reset();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var strategy = new YieldWaitStrategy();

		// Act & Assert - Should not throw
		strategy.Dispose();
		strategy.Dispose();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIWaitStrategy()
	{
		// Assert
		_ = _strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Assert
		_ = _strategy.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void InheritsFromWaitStrategyBase()
	{
		// Assert
		_ = _strategy.ShouldBeAssignableTo<WaitStrategyBase>();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public async Task SpinningWaitScenario()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();
		var completed = false;

		// Simulate a task that will set the flag
		_ = Task.Run(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50);
			completed = true;
		});

		// Act
		var result = await strategy.WaitAsync(() => completed, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		completed.ShouldBeTrue();
	}

	[Fact]
	public async Task TimeoutScenario()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act
		var result = await strategy.WaitAsync(
			() => false, // Never true
			cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion
}

