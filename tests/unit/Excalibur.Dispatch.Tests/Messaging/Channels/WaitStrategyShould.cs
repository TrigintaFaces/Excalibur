// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class WaitStrategyShould
{
	// --- SpinWaitStrategy ---

	[Fact]
	public async Task SpinWaitStrategy_ReturnTrue_WhenConditionMetImmediately()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task SpinWaitStrategy_ReturnFalse_WhenCancelled()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var result = await strategy.WaitAsync(() => false, cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task SpinWaitStrategy_ThrowOnNullCondition()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SpinWaitStrategy_ReturnTrue_WhenConditionBecomesTrueAfterSpinning()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();
		var counter = 0;

		// Act
		var result = await strategy.WaitAsync(() => ++counter >= 5, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		counter.ShouldBeGreaterThanOrEqualTo(5);
	}

	[Fact]
	public void SpinWaitStrategy_Reset_DoesNotThrow()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();

		// Act & Assert
		Should.NotThrow(() => strategy.Reset());
	}

	[Fact]
	public void SpinWaitStrategy_Dispose_DoesNotThrow()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();

		// Act & Assert
		Should.NotThrow(() => strategy.Dispose());
	}

	[Fact]
	public void SpinWaitStrategy_ImplementsIWaitStrategy()
	{
		// Arrange
		using var strategy = new SpinWaitStrategy();

		// Assert
		strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	// --- YieldWaitStrategy ---

	[Fact]
	public async Task YieldWaitStrategy_ReturnTrue_WhenConditionMetImmediately()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task YieldWaitStrategy_ReturnFalse_WhenCancelled()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var result = await strategy.WaitAsync(() => false, cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task YieldWaitStrategy_ThrowOnNullCondition()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task YieldWaitStrategy_ReturnTrue_WhenConditionBecomesTrueAfterYielding()
	{
		// Arrange
		using var strategy = new YieldWaitStrategy();
		var counter = 0;

		// Act
		var result = await strategy.WaitAsync(() => ++counter >= 3, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		counter.ShouldBeGreaterThanOrEqualTo(3);
	}

	// --- TimerWaitStrategy ---

	[Fact]
	public async Task TimerWaitStrategy_ReturnTrue_WhenConditionMetImmediately()
	{
		// Arrange
		using var strategy = new TimerWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task TimerWaitStrategy_ThrowOnNullCondition()
	{
		// Arrange
		using var strategy = new TimerWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void TimerWaitStrategy_ThrowOnZeroDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new TimerWaitStrategy(0));
	}

	[Fact]
	public void TimerWaitStrategy_ThrowOnNegativeDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new TimerWaitStrategy(-1));
	}

	[Fact]
	public void TimerWaitStrategy_AcceptPositiveDelay()
	{
		// Act & Assert
		using var strategy = new TimerWaitStrategy(50);
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void TimerWaitStrategy_DefaultConstructor_DoesNotThrow()
	{
		// Act & Assert
		using var strategy = new TimerWaitStrategy();
		strategy.ShouldNotBeNull();
	}

	// --- HybridWaitStrategy ---

	[Fact]
	public async Task HybridWaitStrategy_ReturnTrue_WhenConditionMetDuringSpin()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy(spinCount: 20, delayMilliseconds: 5);
		var counter = 0;

		// Act
		var result = await strategy.WaitAsync(() => ++counter >= 3, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		counter.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task HybridWaitStrategy_ReturnTrue_WhenConditionMetImmediately()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task HybridWaitStrategy_ReturnFalse_WhenCancelled()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var result = await strategy.WaitAsync(() => false, cts.Token);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task HybridWaitStrategy_ThrowOnNullCondition()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void HybridWaitStrategy_ThrowOnZeroSpinCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new HybridWaitStrategy(spinCount: 0));
	}

	[Fact]
	public void HybridWaitStrategy_ThrowOnNegativeSpinCount()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new HybridWaitStrategy(spinCount: -1));
	}

	[Fact]
	public void HybridWaitStrategy_ThrowOnZeroDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new HybridWaitStrategy(delayMilliseconds: 0));
	}

	[Fact]
	public void HybridWaitStrategy_ThrowOnNegativeDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new HybridWaitStrategy(delayMilliseconds: -1));
	}

	[Fact]
	public void HybridWaitStrategy_DefaultConstructor_DoesNotThrow()
	{
		// Act
		using var strategy = new HybridWaitStrategy();

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void HybridWaitStrategy_Reset_DoesNotThrow()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy();

		// Act & Assert
		Should.NotThrow(() => strategy.Reset());
	}

	[Fact]
	public void HybridWaitStrategy_ImplementsIWaitStrategy()
	{
		// Arrange
		using var strategy = new HybridWaitStrategy();

		// Assert
		strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	// --- WaitStrategyBase ---

	[Fact]
	public void WaitStrategyBase_DoubleDispose_DoesNotThrow()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();

		// Act & Assert
		Should.NotThrow(() =>
		{
			strategy.Dispose();
			strategy.Dispose();
		});
	}
}
