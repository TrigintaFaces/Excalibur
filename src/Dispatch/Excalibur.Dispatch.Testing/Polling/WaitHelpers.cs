// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Testing.Polling;

/// <summary>
/// Provides async polling utilities for tests that need to wait for asynchronous conditions.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WaitHelpers"/> provides a clean way to poll for conditions in tests without busy-waiting.
/// Unlike simple <see cref="Task.Delay(TimeSpan, CancellationToken)"/> loops, these methods properly support cancellation and
/// distinguish between timeout and external cancellation.
/// </para>
/// <para>
/// The default poll interval is 100ms, which provides a good balance between
/// responsiveness and CPU usage. Adjust based on your use case.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Wait for a background service to process a message
/// var processed = await WaitHelpers.WaitUntilAsync(
///     () => messageLog.GetAll().Any(m => m.Type == "OrderCreated"),
///     TimeSpan.FromSeconds(5));
///
/// Assert.True(processed, "Message was not processed within timeout");
/// </code>
/// </example>
public static class WaitHelpers
{
	/// <summary>
	/// Default poll interval between condition checks (100ms).
	/// </summary>
	public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Waits until the specified condition returns <see langword="true"/>, or until timeout/cancellation.
	/// </summary>
	/// <param name="condition">The condition to poll. Should return <see langword="true"/> when the wait should stop.</param>
	/// <param name="timeout">Maximum time to wait for the condition.</param>
	/// <param name="pollInterval">Time between condition checks. Defaults to 100ms.</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns><see langword="true"/> if the condition was met; <see langword="false"/> if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// // Wait for a service to become healthy
	/// var isHealthy = await WaitHelpers.WaitUntilAsync(
	///     () => service.IsHealthy,
	///     TimeSpan.FromSeconds(30));
	///
	/// if (!isHealthy)
	/// {
	///     Assert.Fail("Service did not become healthy in time");
	/// }
	/// </code>
	/// </example>
	public static async Task<bool> WaitUntilAsync(
		Func<bool> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var interval = pollInterval ?? DefaultPollInterval;
		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
			timeoutCts.Token,
			cancellationToken);

		try
		{
			while (!linkedCts.Token.IsCancellationRequested)
			{
				if (condition())
				{
					return true;
				}

				await Task.Delay(interval, linkedCts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Timeout occurred - return false instead of throwing
			return false;
		}

		// If we get here, external cancellation was requested
		cancellationToken.ThrowIfCancellationRequested();
		return false;
	}

	/// <summary>
	/// Waits until the specified async condition returns <see langword="true"/>, or until timeout/cancellation.
	/// </summary>
	/// <param name="condition">The async condition to poll. Should return <see langword="true"/> when the wait should stop.</param>
	/// <param name="timeout">Maximum time to wait for the condition.</param>
	/// <param name="pollInterval">Time between condition checks. Defaults to 100ms.</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns><see langword="true"/> if the condition was met; <see langword="false"/> if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// // Wait for database record to appear
	/// var found = await WaitHelpers.WaitUntilAsync(
	///     async () => await repository.ExistsAsync(id),
	///     TimeSpan.FromSeconds(10));
	/// </code>
	/// </example>
	public static async Task<bool> WaitUntilAsync(
		Func<Task<bool>> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var interval = pollInterval ?? DefaultPollInterval;
		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
			timeoutCts.Token,
			cancellationToken);

		try
		{
			while (!linkedCts.Token.IsCancellationRequested)
			{
				if (await condition().ConfigureAwait(false))
				{
					return true;
				}

				await Task.Delay(interval, linkedCts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Timeout occurred - return false instead of throwing
			return false;
		}

		// If we get here, external cancellation was requested
		cancellationToken.ThrowIfCancellationRequested();
		return false;
	}

	/// <summary>
	/// Waits until the specified async condition (with cancellation support) returns <see langword="true"/>, or until timeout/cancellation.
	/// </summary>
	/// <param name="condition">The async condition to poll with cancellation support.</param>
	/// <param name="timeout">Maximum time to wait for the condition.</param>
	/// <param name="pollInterval">Time between condition checks. Defaults to 100ms.</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns><see langword="true"/> if the condition was met; <see langword="false"/> if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// // Wait for async operation with cancellation
	/// var success = await WaitHelpers.WaitUntilAsync(
	///     async ct => await service.CheckStatusAsync(ct),
	///     TimeSpan.FromSeconds(30));
	/// </code>
	/// </example>
	public static async Task<bool> WaitUntilAsync(
		Func<CancellationToken, Task<bool>> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(condition);

		var interval = pollInterval ?? DefaultPollInterval;
		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
			timeoutCts.Token,
			cancellationToken);

		try
		{
			while (!linkedCts.Token.IsCancellationRequested)
			{
				if (await condition(linkedCts.Token).ConfigureAwait(false))
				{
					return true;
				}

				await Task.Delay(interval, linkedCts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Timeout occurred - return false instead of throwing
			return false;
		}

		// If we get here, external cancellation was requested
		cancellationToken.ThrowIfCancellationRequested();
		return false;
	}

	/// <summary>
	/// Waits for a value to be produced by polling the specified producer function.
	/// </summary>
	/// <typeparam name="T">The type of value to wait for. Must be a reference type.</typeparam>
	/// <param name="producer">Function that produces the value. Returns <see langword="null"/> until the value is ready.</param>
	/// <param name="timeout">Maximum time to wait for the value.</param>
	/// <param name="pollInterval">Time between producer calls. Defaults to 100ms.</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns>The produced value, or <see langword="default"/> if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="producer"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// // Wait for a message to arrive
	/// var message = await WaitHelpers.WaitForValueAsync(
	///     () => queue.TryDequeue(out var msg) ? msg : null,
	///     TimeSpan.FromSeconds(5));
	/// </code>
	/// </example>
	public static async Task<T?> WaitForValueAsync<T>(
		Func<T?> producer,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(producer);

		T? result = null;
		var found = await WaitUntilAsync(
			() =>
			{
				result = producer();
				return result is not null;
			},
			timeout,
			pollInterval,
			cancellationToken).ConfigureAwait(false);

		return found ? result : default;
	}

	/// <summary>
	/// Waits for a value to be produced by polling the specified async producer function.
	/// </summary>
	/// <typeparam name="T">The type of value to wait for. Must be a reference type.</typeparam>
	/// <param name="producer">Async function that produces the value. Returns <see langword="null"/> until the value is ready.</param>
	/// <param name="timeout">Maximum time to wait for the value.</param>
	/// <param name="pollInterval">Time between producer calls. Defaults to 100ms.</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns>The produced value, or <see langword="default"/> if timeout occurred.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="producer"/> is <see langword="null"/>.</exception>
	public static async Task<T?> WaitForValueAsync<T>(
		Func<Task<T?>> producer,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(producer);

		T? result = null;
		var found = await WaitUntilAsync(
			async () =>
			{
				result = await producer().ConfigureAwait(false);
				return result is not null;
			},
			timeout,
			pollInterval,
			cancellationToken).ConfigureAwait(false);

		return found ? result : default;
	}

	/// <summary>
	/// Retries an async action until it succeeds (no exception thrown) or timeout/max retries is reached.
	/// </summary>
	/// <param name="action">The action to retry. Success is indicated by returning without exception.</param>
	/// <param name="timeout">Maximum total time for all retries.</param>
	/// <param name="retryDelay">Delay between retries. Defaults to 100ms.</param>
	/// <param name="maxRetries">Maximum number of retries. Defaults to <see cref="int.MaxValue"/> (timeout-based).</param>
	/// <param name="cancellationToken">Cancellation token for external cancellation.</param>
	/// <returns><see langword="true"/> if the action succeeded; <see langword="false"/> if timeout or max retries reached.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is <see langword="null"/>.</exception>
	public static async Task<bool> RetryUntilSuccessAsync(
		Func<Task> action,
		TimeSpan timeout,
		TimeSpan? retryDelay = null,
		int maxRetries = int.MaxValue,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(action);

		var delay = retryDelay ?? DefaultPollInterval;
		using var timeoutCts = new CancellationTokenSource(timeout);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
			timeoutCts.Token,
			cancellationToken);

		var attempts = 0;

		try
		{
			while (!linkedCts.Token.IsCancellationRequested && attempts < maxRetries)
			{
				attempts++;
				try
				{
					await action().ConfigureAwait(false);
					return true; // Success
				}
				catch (Exception) when (attempts < maxRetries && !linkedCts.Token.IsCancellationRequested)
				{
					// Action failed, wait and retry
					await Task.Delay(delay, linkedCts.Token).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			// Timeout occurred
			return false;
		}

		// If we get here, external cancellation was requested
		cancellationToken.ThrowIfCancellationRequested();
		return false;
	}
}
