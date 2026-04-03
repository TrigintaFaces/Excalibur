// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Time;

/// <summary>
/// Utility class for common TimeProvider operations.
/// </summary>
internal static class TimeProviderUtilities
{
	/// <summary>
	/// Gets the current UTC time as DateTime for compatibility with legacy code.
	/// </summary>
	/// <param name="timeProvider"> The TimeProvider instance. </param>
	/// <returns> Current UTC time as DateTime. </returns>
	public static DateTime GetUtcNowAsDateTime(this TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);
		return timeProvider.GetUtcNow().DateTime;
	}

	/// <summary>
	/// Creates a <see cref="CancellationTokenSource"/> that cancels after the specified delay.
	/// The caller is responsible for disposing the returned <see cref="CancellationTokenSource"/>.
	/// </summary>
	/// <param name="timeProvider"> The TimeProvider instance. </param>
	/// <param name="delay"> The delay before cancellation. </param>
	/// <returns> A <see cref="CancellationTokenSource"/> that cancels after the delay, or <see langword="null"/> if <paramref name="delay"/> is <see cref="Timeout.InfiniteTimeSpan"/>. </returns>
	public static CancellationTokenSource? CreateTimeoutCancellationTokenSource(this TimeProvider timeProvider, TimeSpan delay)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);

		if (delay == Timeout.InfiniteTimeSpan)
		{
			return null;
		}

		var cts = new CancellationTokenSource();
		var timer = timeProvider.CreateTimer(
			_ =>
			{
				try
				{
					cts.Cancel();
				}
				catch (ObjectDisposedException)
				{
					// CTS was disposed before the timer fired -- safe to ignore.
				}
			},
			state: null,
			delay,
			Timeout.InfiniteTimeSpan);

		// Dispose timer when CTS is cancelled
		_ = cts.Token.Register(timer.Dispose);

		return cts;
	}

	/// <summary>
	/// Executes a delay using the TimeProvider. This is testable unlike Task.Delay().
	/// </summary>
	/// <param name="timeProvider"> The TimeProvider instance. </param>
	/// <param name="delay"> The delay duration. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the delay operation. </returns>
	public static Task DelayAsync(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);

		if (delay <= TimeSpan.Zero)
		{
			return Task.CompletedTask;
		}

		// Use TaskCompletionSource with timer for TimeProvider-based delay
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var timer = timeProvider.CreateTimer(
			_ => tcs.TrySetResult(true),
			state: null,
			delay,
			Timeout.InfiniteTimeSpan);

		// Handle cancellation
		_ = cancellationToken.Register(() =>
		{
			timer.Dispose();
			_ = tcs.TrySetCanceled(cancellationToken);
		});

		// Dispose timer when task completes
		_ = tcs.Task.ContinueWith(_ => timer.Dispose(), TaskScheduler.Default);

		return tcs.Task;
	}
}
