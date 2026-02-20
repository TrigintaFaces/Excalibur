// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
public sealed class WaitStrategiesShould : IDisposable
{
    private IDisposable? _strategy;

    // --- HybridWaitStrategy ---

    [Fact]
    public async Task HybridWaitStrategy_ReturnsTrueWhenConditionMet()
    {
        var strategy = new HybridWaitStrategy();
        _strategy = strategy;

        var result = await strategy.WaitAsync(() => true, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HybridWaitStrategy_ReturnsFalseWhenCancelled()
    {
        var strategy = new HybridWaitStrategy();
        _strategy = strategy;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await strategy.WaitAsync(() => false, cts.Token);

        result.ShouldBeFalse();
    }

    [Fact]
    public void HybridWaitStrategy_ThrowsForInvalidSpinCount()
    {
        Should.Throw<ArgumentException>(() => new HybridWaitStrategy(spinCount: 0));
        Should.Throw<ArgumentException>(() => new HybridWaitStrategy(spinCount: -1));
    }

    [Fact]
    public void HybridWaitStrategy_ThrowsForInvalidDelay()
    {
        Should.Throw<ArgumentException>(() => new HybridWaitStrategy(delayMilliseconds: 0));
        Should.Throw<ArgumentException>(() => new HybridWaitStrategy(delayMilliseconds: -1));
    }

    [Fact]
    public void HybridWaitStrategy_ResetDoesNotThrow()
    {
        var strategy = new HybridWaitStrategy();
        _strategy = strategy;

        Should.NotThrow(() => strategy.Reset());
    }

    // --- SpinWaitStrategy ---

    [Fact]
    public async Task SpinWaitStrategy_ReturnsTrueWhenConditionMet()
    {
        var strategy = new SpinWaitStrategy();
        _strategy = strategy;

        var result = await strategy.WaitAsync(() => true, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task SpinWaitStrategy_ReturnsFalseWhenCancelled()
    {
        var strategy = new SpinWaitStrategy();
        _strategy = strategy;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await strategy.WaitAsync(() => false, cts.Token);

        result.ShouldBeFalse();
    }

    // --- YieldWaitStrategy ---

    [Fact]
    public async Task YieldWaitStrategy_ReturnsTrueWhenConditionMet()
    {
        var strategy = new YieldWaitStrategy();
        _strategy = strategy;

        var result = await strategy.WaitAsync(() => true, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task YieldWaitStrategy_ReturnsFalseWhenCancelled()
    {
        var strategy = new YieldWaitStrategy();
        _strategy = strategy;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await strategy.WaitAsync(() => false, cts.Token);

        result.ShouldBeFalse();
    }

    // --- TimerWaitStrategy ---

    [Fact]
    public async Task TimerWaitStrategy_ReturnsTrueWhenConditionMet()
    {
        var strategy = new TimerWaitStrategy();
        _strategy = strategy;

        var result = await strategy.WaitAsync(() => true, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task TimerWaitStrategy_ReturnsFalseWhenCancelled()
    {
        var strategy = new TimerWaitStrategy();
        _strategy = strategy;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await strategy.WaitAsync(() => false, cts.Token);

        result.ShouldBeFalse();
    }

    // --- AdaptiveWaitStrategy ---

    [Fact]
    public async Task AdaptiveWaitStrategy_ReturnsTrueWhenConditionMet()
    {
        var strategy = new AdaptiveWaitStrategy();
        _strategy = strategy;

        var result = await strategy.WaitAsync(() => true, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AdaptiveWaitStrategy_ReturnsFalseWhenCancelled()
    {
        var strategy = new AdaptiveWaitStrategy();
        _strategy = strategy;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await strategy.WaitAsync(() => false, cts.Token);

        result.ShouldBeFalse();
    }

    public void Dispose()
    {
        _strategy?.Dispose();
    }
}
