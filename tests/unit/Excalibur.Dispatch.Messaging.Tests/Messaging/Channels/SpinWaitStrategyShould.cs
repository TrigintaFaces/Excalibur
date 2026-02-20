// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="SpinWaitStrategy"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Channels")]
public sealed class SpinWaitStrategyShould
{
	[Fact]
	public void ImplementIWaitStrategy()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();

		// Assert
		strategy.ShouldBeAssignableTo<IWaitStrategy>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(SpinWaitStrategy).IsPublic.ShouldBeTrue();
		typeof(SpinWaitStrategy).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExtendWaitStrategyBase()
	{
		// Assert
		typeof(SpinWaitStrategy).BaseType.ShouldBe(typeof(WaitStrategyBase));
	}

	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Act
		var strategy = new SpinWaitStrategy();

		// Assert
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public async Task WaitAsyncReturnsTrueWhenConditionIsMet()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();

		// Act
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsyncReturnsFalseWhenCancelled()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();
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
		var strategy = new SpinWaitStrategy();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await strategy.WaitAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task WaitAsyncSpinsUntilConditionIsMet()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();
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
		var strategy = new SpinWaitStrategy();

		// Act & Assert - Should not throw
		Should.NotThrow(() => strategy.Reset());
	}

	[Fact]
	public async Task ResetAllowsReuse()
	{
		// Arrange
		var strategy = new SpinWaitStrategy();

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
