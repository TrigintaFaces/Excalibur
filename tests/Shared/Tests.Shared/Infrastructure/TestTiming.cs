// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading;

namespace Tests.Shared.Infrastructure;

/// <summary>
/// Centralized timing primitives for tests.
/// Keeping timing calls behind this helper makes flakiness burn-down and
/// progressive hardening (polling/event probes over fixed waits) easier.
/// </summary>
public static class TestTiming
{
	private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var registration = cancellationToken.Register(static state =>
		{
			var tcs = (TaskCompletionSource)state!;
			tcs.TrySetResult();
		}, completion);
		await completion.Task.ConfigureAwait(false);
		await registration.DisposeAsync().ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
	}

	/// <summary>
	/// Preferred async wait primitive for tests. Keeps waits centralized so they can be hardened over time.
	/// </summary>
	public static async Task PauseAsync(int millisecondsDelay, CancellationToken cancellationToken = default)
	{
		if (millisecondsDelay == Timeout.Infinite)
		{
			await WaitUntilCancelledAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		ArgumentOutOfRangeException.ThrowIfNegative(millisecondsDelay);

		if (millisecondsDelay <= 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return;
		}

		await Task.Delay(millisecondsDelay, cancellationToken);
	}

	/// <summary>
	/// Preferred async wait primitive for tests. Keeps waits centralized so they can be hardened over time.
	/// </summary>
	public static async Task PauseAsync(TimeSpan delay, CancellationToken cancellationToken = default)
	{
		if (delay == Timeout.InfiniteTimeSpan)
		{
			await WaitUntilCancelledAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

		if (delay <= TimeSpan.Zero)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return;
		}

		await Task.Delay(delay, cancellationToken);
	}

	public static Task DelayAsync(int millisecondsDelay, CancellationToken cancellationToken = default) =>
		Task.Delay(millisecondsDelay, cancellationToken);

	public static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
		Task.Delay(delay, cancellationToken);

	public static void Sleep(int millisecondsTimeout) =>
		Thread.Sleep(millisecondsTimeout);

	public static void Sleep(TimeSpan timeout) =>
		Thread.Sleep(timeout);

	/// <summary>
	/// Preferred blocking wait primitive for tests that must use synchronous APIs.
	/// </summary>
	public static void BlockingPause(int millisecondsTimeout)
	{
		if (millisecondsTimeout > 0)
		{
			Thread.Sleep(millisecondsTimeout);
		}
	}

	/// <summary>
	/// Preferred blocking wait primitive for tests that must use synchronous APIs.
	/// </summary>
	public static void BlockingPause(TimeSpan timeout)
	{
		if (timeout > TimeSpan.Zero)
		{
			Thread.Sleep(timeout);
		}
	}
}
