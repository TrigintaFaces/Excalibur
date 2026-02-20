// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Infrastructure;

/// <summary>
/// Provides timeout utilities for tests with CI-configurable multiplier support.
/// </summary>
/// <remarks>
/// <para>
/// Set the <c>TEST_TIMEOUT_MULTIPLIER</c> environment variable to scale all timeouts
/// for slower CI environments. For example, <c>TEST_TIMEOUT_MULTIPLIER=2.0</c> will
/// double all timeout values.
/// </para>
/// </remarks>
public static class TestTimeouts
{
	/// <summary>
	/// Default timeout for unit tests (5 seconds * multiplier).
	/// </summary>
	public static TimeSpan Unit => TimeSpan.FromSeconds(5 * Multiplier);

	/// <summary>
	/// Default timeout for integration tests (30 seconds * multiplier).
	/// </summary>
	public static TimeSpan Integration => TimeSpan.FromSeconds(30 * Multiplier);

	/// <summary>
	/// Default timeout for functional tests (60 seconds * multiplier).
	/// </summary>
	public static TimeSpan Functional => TimeSpan.FromSeconds(60 * Multiplier);

	/// <summary>
	/// Default timeout for container startup operations (120 seconds * multiplier).
	/// </summary>
	public static TimeSpan ContainerStart => TimeSpan.FromSeconds(120 * Multiplier);

	/// <summary>
	/// Default timeout for container health checks (10 seconds * multiplier).
	/// </summary>
	public static TimeSpan HealthCheck => TimeSpan.FromSeconds(10 * Multiplier);

	/// <summary>
	/// Default timeout for database operations (5 seconds * multiplier).
	/// </summary>
	public static TimeSpan DatabaseOperation => TimeSpan.FromSeconds(5 * Multiplier);

	/// <summary>
	/// Default timeout for container disposal (30 seconds * multiplier).
	/// </summary>
	public static TimeSpan ContainerDispose => TimeSpan.FromSeconds(30 * Multiplier);

	/// <summary>
	/// Gets the timeout multiplier from the TEST_TIMEOUT_MULTIPLIER environment variable.
	/// </summary>
	private static double Multiplier =>
		double.TryParse(Environment.GetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER"), out var m) ? m : 1.0;

	/// <summary>
	/// Creates a cancellation token source with the specified timeout.
	/// </summary>
	/// <param name="timeout">The timeout duration.</param>
	/// <returns>A cancellation token source configured with the timeout.</returns>
	public static CancellationTokenSource CreateCancellationTokenSource(TimeSpan timeout)
	{
		return new CancellationTokenSource(timeout);
	}

	/// <summary>
	/// Executes a task with a timeout.
	/// </summary>
	/// <typeparam name="T">The type of the task result.</typeparam>
	/// <param name="task">The task to execute.</param>
	/// <param name="timeout">The timeout duration.</param>
	/// <param name="operationName">The name of the operation for error messages.</param>
	/// <returns>The task result if completed within timeout.</returns>
	/// <exception cref="TimeoutException">Thrown when the task exceeds the timeout.</exception>
	public static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operationName = "Operation")
	{
		ArgumentNullException.ThrowIfNull(task);
		using var cts = new CancellationTokenSource(timeout);
		var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);

		if (completedTask == task)
		{
			return await task.ConfigureAwait(false);
		}

		throw new TimeoutException($"{operationName} timed out after {timeout.TotalSeconds} seconds");
	}

	/// <summary>
	/// Executes a task with a timeout.
	/// </summary>
	/// <param name="task">The task to execute.</param>
	/// <param name="timeout">The timeout duration.</param>
	/// <param name="operationName">The name of the operation for error messages.</param>
	/// <exception cref="TimeoutException">Thrown when the task exceeds the timeout.</exception>
	public static async Task WithTimeout(Task task, TimeSpan timeout, string operationName = "Operation")
	{
		ArgumentNullException.ThrowIfNull(task);
		using var cts = new CancellationTokenSource(timeout);
		var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);

		if (completedTask == task)
		{
			await task.ConfigureAwait(false);
			return;
		}

		throw new TimeoutException($"{operationName} timed out after {timeout.TotalSeconds} seconds");
	}
}
