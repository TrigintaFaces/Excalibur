// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

namespace Excalibur.Domain.Extensions;

/// <summary>
/// Provides extension methods for Task operations.
/// </summary>
public static class TaskExtensions
{
	private static readonly CompositeFormat TimeoutExceededFormat =
		CompositeFormat.Parse(Resources.TaskExtensions_TimeoutExceeded);

	/// <summary>
	/// Applies a timeout to the specified task.
	/// </summary>
	/// <param name="task"> The task to apply the timeout to. </param>
	/// <param name="timeout"> The timeout duration. </param>
	/// <returns> A task that represents the completion of the original task or a timeout exception. </returns>
	/// <exception cref="TimeoutException"> Thrown when the operation does not complete within the specified timeout. </exception>
	public static async Task TimeoutAfterAsync(this Task task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException(
				string.Format(
					CultureInfo.CurrentCulture,
					TimeoutExceededFormat,
					timeout.TotalSeconds));
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
	/// <exception cref="TimeoutException"> Thrown when the operation does not complete within the specified timeout. </exception>
	public static async Task<T> TimeoutAfterAsync<T>(this Task<T> task, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(task);

		using var cts = new CancellationTokenSource();

		var delayTask = Task.Delay(timeout, cts.Token);
		var completedTask = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

		if (completedTask == delayTask)
		{
			throw new TimeoutException(
				string.Format(
					CultureInfo.CurrentCulture,
					TimeoutExceededFormat,
					timeout.TotalSeconds));
		}

		await cts.CancelAsync().ConfigureAwait(false);
		return await task.ConfigureAwait(false);
	}
}
