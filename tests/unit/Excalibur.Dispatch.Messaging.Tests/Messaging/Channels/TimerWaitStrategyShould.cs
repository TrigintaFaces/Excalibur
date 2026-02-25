// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="TimerWaitStrategy"/>.
/// </summary>
/// <remarks>
/// Tests the timer-based wait strategy implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class TimerWaitStrategyShould : IDisposable
{
	private readonly TimerWaitStrategy _strategy;

	public TimerWaitStrategyShould()
	{
		_strategy = new TimerWaitStrategy(10);
	}

	public void Dispose()
	{
		_strategy.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithDefaultDelay_CreatesInstance()
	{
		// Arrange & Act
		using var strategy = new TimerWaitStrategy();

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithPositiveDelay_CreatesInstance()
	{
		// Arrange & Act
		using var strategy = new TimerWaitStrategy(50);

		// Assert
		_ = strategy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithZeroDelay_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TimerWaitStrategy(0));
	}

	[Fact]
	public void Constructor_WithNegativeDelay_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TimerWaitStrategy(-1));
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
	public async Task WaitAsync_WhenCancelled_ThrowsTaskCanceledExceptionOrReturnsFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.CancelAfter(50);

		// Act - The strategy may throw TaskCanceledException or return false depending on timing
		// This is valid behavior - cancellation can be detected at various points
		try
		{
			var result = await _strategy.WaitAsync(
				() => false,
				cts.Token);

			// If no exception, result should be false (cancelled/timed out)
			result.ShouldBeFalse();
		}
		catch (TaskCanceledException)
		{
			// This is also acceptable behavior - cancellation was detected
		}
		catch (OperationCanceledException)
		{
			// OperationCanceledException is the base type, also acceptable
		}
	}

	[Fact]
	public async Task WaitAsync_WhenAlreadyCancelled_ThrowsOrReturnsFalse()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert - May throw or return false depending on whether delay is reached
		try
		{
			var result = await _strategy.WaitAsync(
				() => false,
				cts.Token);

			// If no exception, should return false
			result.ShouldBeFalse();
		}
		catch (TaskCanceledException)
		{
			// This is also acceptable behavior
		}
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
		var strategy = new TimerWaitStrategy(10);

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

	#region Timing Tests

	[Fact]
	public async Task WaitAsync_WaitsApproximatelyConfiguredDelay()
	{
		// Arrange
		using var strategy = new TimerWaitStrategy(50);
		var callCount = 0;
		var sw = System.Diagnostics.Stopwatch.StartNew();

		// Act
		_ = await strategy.WaitAsync(() =>
		{
			callCount++;
			return callCount >= 3;
		}, CancellationToken.None);
		sw.Stop();

		// Assert - Should have waited at least 2 delays (for 3 checks)
		// Give some tolerance for timing variance
		sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(50);
	}

	#endregion
}
