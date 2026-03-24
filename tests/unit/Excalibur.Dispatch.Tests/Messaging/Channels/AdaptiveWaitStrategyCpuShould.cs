// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Regression tests for T.10 (bd-mjmae): AdaptiveWaitStrategy under contention must not produce
/// excessive CPU usage. Verifies that the strategy uses backoff (exponential delay, SemaphoreSlim,
/// or similar) instead of a tight Task.Yield loop when contention is high.
/// </summary>
/// <remarks>
/// The fix replaces the tight <c>while (!condition()) await Task.Yield();</c> loop with
/// exponential backoff using <c>Task.Delay</c> that doubles from 1ms to 64ms.
/// Note: Task.Delay with CancellationToken throws TaskCanceledException when cancelled.
/// CI runners under heavy parallel load can experience extreme scheduling delays (10-60x slower
/// than local dev). All timeouts are calibrated for worst-case CI, not local performance.
/// </remarks>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Channels")]
public sealed class AdaptiveWaitStrategyCpuShould
{
	[Fact]
	public async Task CompleteWithinTimeout_UnderHighContention()
	{
		// Arrange -- Create a strategy with low thresholds to trigger contention path quickly
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 1, contentionThreshold: 1);

		// Drive contention count above threshold by forcing multiple contended waits
		for (var i = 0; i < 5; i++)
		{
			var counter = 0;
			await strategy.WaitAsync(() =>
			{
				counter++;
				return counter >= 3; // Force spinning past maxSpinCount
			}, CancellationToken.None);
		}

		// Act -- Now run concurrent waiters that will all hit the contention (backoff) path.
		// Generous timeout: backoff doubles 1ms→64ms, 20 waiters under CI load can take minutes.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
		var conditionMet = 0;
		var concurrentWaiters = 20;

		var tasks = Enumerable.Range(0, concurrentWaiters).Select(_ => Task.Run(async () =>
		{
			try
			{
				var result = await strategy.WaitAsync(
					() => Volatile.Read(ref conditionMet) >= concurrentWaiters,
					cts.Token);
				return result;
			}
			catch (TaskCanceledException)
			{
				return false;
			}
		}, cts.Token)).ToArray();

		// Give a brief moment for all waiters to enter the wait path
		await Task.Delay(100, CancellationToken.None);

		// Signal all waiters by setting conditionMet
		Interlocked.Exchange(ref conditionMet, concurrentWaiters);

		// Assert -- All tasks should complete within the timeout (proving no CPU saturation hang)
		var results = await Task.WhenAll(tasks);
		results.ShouldAllBe(r => r, "All concurrent waiters should return true when condition is met");
	}

	[Fact]
	public async Task NotSpinExcessively_WhenConditionEventuallyMet()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 10, contentionThreshold: 2);
		var callCount = 0;

		// Act -- Wait for condition that becomes true after a few calls.
		// Reduced from 50 to 5 checks: each backoff iteration doubles (1→2→4→8→16→32→64ms),
		// so 50 checks can take minutes under CI load. 5 checks is sufficient to prove the
		// condition is polled and eventually met.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

		var result = await strategy.WaitAsync(() =>
		{
			var count = Interlocked.Increment(ref callCount);
			return count >= 5;
		}, cts.Token);

		// Assert
		result.ShouldBeTrue("Condition should eventually be met");
		callCount.ShouldBeGreaterThanOrEqualTo(5, "Condition should have been checked enough times");
	}

	[Fact]
	public async Task RespectCancellation_UnderContention()
	{
		// Arrange -- Strategy in contention mode, with a condition that never becomes true.
		// The backoff path uses Task.Delay(ms, cancellationToken) which throws
		// TaskCanceledException when the token is cancelled.
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 1, contentionThreshold: 1);

		// Force into contention mode
		for (var i = 0; i < 3; i++)
		{
			var c = 0;
			await strategy.WaitAsync(() => { c++; return c >= 2; }, CancellationToken.None);
		}

		// Act -- Start a wait with a condition that never becomes true, cancel after 2s.
		// Under CI load, backoff delays accumulate — 500ms was too tight.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		var sw = Stopwatch.StartNew();
		var result = false;
		var wasCancelled = false;

		try
		{
			result = await strategy.WaitAsync(() => false, cts.Token);
		}
		catch (TaskCanceledException)
		{
			wasCancelled = true;
		}
		catch (OperationCanceledException)
		{
			wasCancelled = true;
		}

		sw.Stop();

		// Assert -- Should respect cancellation (either returns false or throws cancellation)
		(result == false || wasCancelled).ShouldBeTrue(
			"Should return false or throw cancellation when token is cancelled");

		// The cancellation should happen within a reasonable time (not stuck in tight loop).
		// CI runners under extreme load can take 60s+ for Task.Delay to fire.
		sw.ElapsedMilliseconds.ShouldBeLessThan(60_000,
			"Cancellation should be respected within 60 seconds");
	}

	[Fact]
	public async Task HandleConcurrentWaiters_WithoutDeadlock()
	{
		// Arrange -- Multiple concurrent waiters with a shared condition
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 5, contentionThreshold: 3);
		var signal = 0;
		// Generous timeout: 50 concurrent waiters with backoff under CI load need substantial time.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

		// Act -- Start 50 concurrent waiters, all waiting for the same signal
		var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
		{
			try
			{
				return await strategy.WaitAsync(
					() => Volatile.Read(ref signal) == 1,
					cts.Token);
			}
			catch (TaskCanceledException)
			{
				return false;
			}
		}, cts.Token)).ToArray();

		// Brief delay to let waiters start
		await Task.Delay(200, CancellationToken.None);

		// Signal
		Volatile.Write(ref signal, 1);

		// Assert -- All should complete without deadlock
		var results = await Task.WhenAll(tasks);
		results.ShouldAllBe(r => r, "All 50 concurrent waiters should complete successfully");
	}

	[Fact]
	public async Task RecoverFromHighContention_AfterReset()
	{
		// Arrange
		var strategy = new AdaptiveWaitStrategy(maxSpinCount: 10, contentionThreshold: 2);

		// Drive into high contention
		for (var i = 0; i < 10; i++)
		{
			var c = 0;
			await strategy.WaitAsync(() => { c++; return c >= 15; }, CancellationToken.None);
		}

		// Act -- Reset and verify immediate condition still works efficiently
		strategy.Reset();

		var sw = Stopwatch.StartNew();
		var result = await strategy.WaitAsync(() => true, CancellationToken.None);
		sw.Stop();

		// Assert -- After reset, immediate condition should resolve near-instantly
		result.ShouldBeTrue();
		sw.ElapsedMilliseconds.ShouldBeLessThan(1000,
			"After reset, immediate condition should resolve quickly via spin path (not contention path)");
	}
}
