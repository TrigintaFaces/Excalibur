// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for Task operations with timeout and cancellation support.
/// </summary>
public static class TaskExtensions
{
	/// <summary>
	/// Applies a timeout to the specified task.
	/// </summary>
	/// <param name="task"> The task to apply the timeout to. </param>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> A task that represents the completion of the original task or a timeout exception. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when task is null. </exception>
	/// <exception cref="TimeoutException"> Thrown when the operation does not complete within the specified timeout. </exception>
	public static async Task TimeoutAfterAsync(this Task task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException($"The operation did not complete within the timeout of {timeout.TotalSeconds} seconds.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Applies a timeout to the specified task that returns a value.
	/// </summary>
	/// <typeparam name="T"> The type of the task result. </typeparam>
	/// <param name="task"> The task to apply the timeout to. </param>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> A task that represents the completion of the original task with its result or a timeout exception. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when task is null. </exception>
	/// <exception cref="TimeoutException"> Thrown when the operation does not complete within the specified timeout. </exception>
	public static async Task<T> TimeoutAfterAsync<T>(this Task<T> task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException($"The operation did not complete within the timeout of {timeout.TotalSeconds} seconds.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		return await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Executes a task with cancellation support, returning default if cancelled.
	/// </summary>
	/// <typeparam name="T"> The type of the task result. </typeparam>
	/// <param name="task"> The task to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The task result, or default if cancelled. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when task is null. </exception>
	public static async Task<T?> WithCancellationAsync<T>(this Task<T> task, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(task);

		var tcs = new TaskCompletionSource<bool>();
		await using (cancellationToken.Register(() => tcs.TrySetResult(true)))
		{
			var completedTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
			if (completedTask == tcs.Task)
			{
				return default;
			}

			return await task.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Safely awaits a task and ignores any exceptions.
	/// </summary>
	/// <param name="task"> The task to await. </param>
	/// <returns> A task that completes when the original task completes, regardless of success or failure. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task SafeAwaitAsync(this Task task)
	{
		try
		{
			await task.ConfigureAwait(false);
		}
		catch
		{
			// Intentionally swallow all exceptions
		}
	}

	/// <summary>
	/// Safely awaits a task and returns default if it fails.
	/// </summary>
	/// <typeparam name="T"> The type of the task result. </typeparam>
	/// <param name="task"> The task to await. </param>
	/// <returns> The task result, or default if the task fails. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task<T?> SafeAwaitAsync<T>(this Task<T> task)
	{
		try
		{
			return await task.ConfigureAwait(false);
		}
		catch
		{
			return default;
		}
	}
}
