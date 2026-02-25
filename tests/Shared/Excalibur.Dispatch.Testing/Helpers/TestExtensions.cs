// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Testing.Helpers;

/// <summary>
/// Common extension methods for test assertions and utilities.
/// </summary>
public static class TestExtensions
{
	/// <summary>
	/// Asserts that an async operation completes within the specified timeout.
	/// </summary>
	public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, string? message = null)
	{
		var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
		if (completedTask != task)
		{
			throw new TimeoutException(message ?? $"Operation did not complete within {timeout.TotalSeconds} seconds");
		}
		return await task;
	}

	/// <summary>
	/// Asserts that an async operation completes within the specified timeout.
	/// </summary>
	public static async Task WithTimeout(this Task task, TimeSpan timeout, string? message = null)
	{
		var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
		if (completedTask != task)
		{
			throw new TimeoutException(message ?? $"Operation did not complete within {timeout.TotalSeconds} seconds");
		}
		await task;
	}

	/// <summary>
	/// Creates a cancellation token that will cancel after the specified delay.
	/// </summary>
	public static CancellationToken CreateTimeoutToken(TimeSpan timeout)
	{
		return new CancellationTokenSource(timeout).Token;
	}

	/// <summary>
	/// Waits for a condition to become true with polling.
	/// Delegates to <see cref="WaitHelpers.WaitUntilAsync(Func{bool}, TimeSpan, TimeSpan?, CancellationToken)"/>.
	/// </summary>
	public static async Task WaitForConditionAsync(
		Func<bool> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		string? message = null)
	{
		var met = await WaitHelpers.WaitUntilAsync(condition, timeout, pollInterval).ConfigureAwait(false);
		if (!met)
		{
			throw new TimeoutException(message ?? $"Condition not met within {timeout.TotalSeconds} seconds");
		}
	}

	/// <summary>
	/// Waits for an async condition to become true with polling.
	/// Delegates to <see cref="WaitHelpers.WaitUntilAsync(Func{Task{bool}}, TimeSpan, TimeSpan?, CancellationToken)"/>.
	/// </summary>
	public static async Task WaitForConditionAsync(
		Func<Task<bool>> condition,
		TimeSpan timeout,
		TimeSpan? pollInterval = null,
		string? message = null)
	{
		var met = await WaitHelpers.WaitUntilAsync(condition, timeout, pollInterval).ConfigureAwait(false);
		if (!met)
		{
			throw new TimeoutException(message ?? $"Condition not met within {timeout.TotalSeconds} seconds");
		}
	}
}
