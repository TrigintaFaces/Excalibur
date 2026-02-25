// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Time;

/// <summary>
/// Utility class for common TimeProvider operations.
/// </summary>
public static class TimeProviderUtilities
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
	/// Creates a cancellation token that cancels after the specified delay.
	/// </summary>
	/// <param name="timeProvider"> The TimeProvider instance. </param>
	/// <param name="delay"> The delay before cancellation. </param>
	/// <returns> A cancellation token that cancels after the delay. </returns>
	public static CancellationToken CreateCancellationToken(this TimeProvider timeProvider, TimeSpan delay)
	{
		ArgumentNullException.ThrowIfNull(timeProvider);

		if (delay == Timeout.InfiniteTimeSpan)
		{
			return CancellationToken.None;
		}

		var cts = new CancellationTokenSource();
		var timer = timeProvider.CreateTimer(
			_ => cts.Cancel(),
			state: null,
			delay,
			Timeout.InfiniteTimeSpan);

		// Dispose timer when cancellation token is disposed (requires .NET 8+ pattern)
		_ = cts.Token.Register(timer.Dispose);

		return cts.Token;
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
		var tcs = new TaskCompletionSource<bool>();
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
