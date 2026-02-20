using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AdaptiveWaitStrategyShould
{
	[Fact]
	public void CreateWithDefaultParameters()
	{
		var strategy = new AdaptiveWaitStrategy();

		// Should not throw — verify it exists
		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomParameters()
	{
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 200, contentionThreshold: 20);

		strategy.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnZeroMaxSpinCount()
	{
		Should.Throw<ArgumentException>(() => new AdaptiveWaitStrategy(maxSpinCount: 0));
	}

	[Fact]
	public void ThrowOnNegativeMaxSpinCount()
	{
		Should.Throw<ArgumentException>(() => new AdaptiveWaitStrategy(maxSpinCount: -1));
	}

	[Fact]
	public void ThrowOnZeroContentionThreshold()
	{
		Should.Throw<ArgumentException>(() => new AdaptiveWaitStrategy(contentionThreshold: 0));
	}

	[Fact]
	public void ThrowOnNegativeContentionThreshold()
	{
		Should.Throw<ArgumentException>(() => new AdaptiveWaitStrategy(contentionThreshold: -1));
	}

	[Fact]
	public async Task WaitAsync_ReturnsTrue_WhenConditionImmediatelyTrue()
	{
		var strategy = new AdaptiveWaitStrategy();

		var result = await strategy.WaitAsync(() => true, CancellationToken.None);

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitAsync_ReturnsFalse_WhenCancelled()
	{
		var strategy = new AdaptiveWaitStrategy();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var result = await strategy.WaitAsync(() => false, cts.Token);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task WaitAsync_ThrowsOnNullCondition()
	{
		var strategy = new AdaptiveWaitStrategy();

		await Should.ThrowAsync<ArgumentNullException>(
			() => strategy.WaitAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task WaitAsync_EventuallyReturnsTrue()
	{
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 10);
		var counter = 0;

		var result = await strategy.WaitAsync(() =>
		{
			counter++;
			return counter >= 3;
		}, CancellationToken.None);

		result.ShouldBeTrue();
		counter.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void Reset_DoesNotThrow()
	{
		var strategy = new AdaptiveWaitStrategy();

		Should.NotThrow(() => strategy.Reset());
	}
}
