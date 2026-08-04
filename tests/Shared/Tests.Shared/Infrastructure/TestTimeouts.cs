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
	/// Total wall-clock budget for a container fixture's initialization, across every retry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Deliberately NOT scaled by the multiplier</b>, unlike every other value here. The others
	/// are bounded by how slow the machine is, so scaling them is right. This one is bounded by
	/// something outside the process: the test runner is invoked with <c>--blame-hang-timeout</c>,
	/// and when no test reports progress for that long the blame collector kills the test host.
	/// Scaling this value would push it past that ceiling on exactly the slow CI agents where the
	/// ceiling matters most.
	/// </para>
	/// <para>
	/// A killed host is the worst available failure mode, because it does not look like one. The
	/// tests that finished are reported as passed, the tests that never started are absent rather
	/// than failed, and the runner prints <c>Passed! - Failed: 0</c>. Observed: a 10.2 minute gap
	/// after the last test against a 10 minute blame timeout, 96 tests missing from the results
	/// entirely, and a green-looking assembly. Only the population census caught it.
	/// </para>
	/// <para>
	/// So this is set below the <b>shortest</b> blame timeout in use (jobs pass 5m and 10m), leaving
	/// room for the fixture to throw a diagnosable error while the host is still alive. If a blame
	/// timeout is ever lowered below this, lower this with it -- the invariant is
	/// <c>budget &lt; blame-hang-timeout</c>, and it is what keeps a container failure loud.
	/// </para>
	/// </remarks>
	public static TimeSpan ContainerInitBudget { get; } = TimeSpan.FromSeconds(240);

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
	/// Scales an arbitrary timeout by the configured test timeout multiplier.
	/// </summary>
	/// <param name="timeout">The base timeout value.</param>
	/// <returns>The scaled timeout value.</returns>
	public static TimeSpan Scale(TimeSpan timeout)
	{
		if (timeout == Timeout.InfiniteTimeSpan)
		{
			return timeout;
		}

		ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

		if (timeout == TimeSpan.Zero)
		{
			return timeout;
		}

		return TimeSpan.FromTicks((long)(timeout.Ticks * Multiplier));
	}

	/// <summary>
	/// The multiplier applied on a CI agent when nothing sets one explicitly.
	/// </summary>
	/// <remarks>
	/// Chosen against measurement rather than taste. The keyed-lock churn test drives 16 workers through
	/// 4,000 fully serialized acquisitions of one key; it finishes in well under a second locally and
	/// overran a 30-second deadline on an agent running a dozen assemblies in parallel. A timeout only
	/// costs wall-clock once something has already gone wrong, so a wider margin trades nothing on the
	/// happy path.
	/// </remarks>
	private const double DefaultCiMultiplier = 3.0;

	/// <summary>
	/// The resolved multiplier: an explicit <c>TEST_TIMEOUT_MULTIPLIER</c> if set and usable, otherwise
	/// <see cref="DefaultCiMultiplier"/> on a CI agent and 1.0 on a developer machine.
	/// </summary>
	/// <remarks>
	/// Resolved once. Nothing sets the variable at runtime, and a multiplier that could change mid-run
	/// would make two deadlines in the same test disagree.
	/// </remarks>
	private static readonly double ResolvedMultiplier = ResolveMultiplier();

	/// <summary>
	/// Gets the timeout multiplier applied to every scaled deadline.
	/// </summary>
	private static double Multiplier => ResolvedMultiplier;

	/// <summary>
	/// Resolves the multiplier, defaulting to <see cref="DefaultCiMultiplier"/> when running on CI.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The CI default exists because the environment variable is the wrong place to hold this alone. It
	/// previously had to be set per workflow, and was set in exactly one of the eight that run tests --
	/// so every <c>Scale</c> call was the identity function everywhere else, including in the jobs whose
	/// deadlines exist specifically to absorb CI load. Making the default correct means a workflow
	/// cannot forget it, and a new workflow inherits the right behaviour without knowing this exists.
	/// </para>
	/// <para>
	/// A developer machine stays at 1.0, so local runs keep tight deadlines and a genuine hang surfaces
	/// quickly rather than after a tripled wait.
	/// </para>
	/// </remarks>
	private static double ResolveMultiplier() =>
		ResolveMultiplier(
			Environment.GetEnvironmentVariable("TEST_TIMEOUT_MULTIPLIER"),
			IsContinuousIntegration);

	/// <summary>
	/// Resolves the multiplier from its two inputs.
	/// </summary>
	/// <param name="rawOverride">The raw <c>TEST_TIMEOUT_MULTIPLIER</c> value, or <see langword="null"/>.</param>
	/// <param name="isContinuousIntegration">Whether the run is on a CI agent.</param>
	/// <returns>The multiplier to apply.</returns>
	/// <remarks>
	/// Kept as a pure function of its inputs, rather than reading the environment inline, so both
	/// branches are reachable from a test. The version of this that read the environment directly could
	/// not be tested at all, which is a large part of why it went a long time returning 1.0 everywhere
	/// while appearing to do something.
	/// </remarks>
	internal static double ResolveMultiplier(string? rawOverride, bool isContinuousIntegration)
	{
		// Invariant culture: a machine with a comma decimal separator would otherwise fail to parse
		// "1.5" and silently fall back, which is the same class of silent no-op this method exists to
		// end. A non-positive value is rejected for the same reason -- 0 would collapse every deadline
		// to zero and fail every timed test instantly, which reads as a code defect rather than config.
		if (double.TryParse(rawOverride, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out var explicitValue)
			&& explicitValue > 0)
		{
			return explicitValue;
		}

		return isContinuousIntegration ? DefaultCiMultiplier : 1.0;
	}

	/// <summary>
	/// Gets a value indicating whether the tests are running on a CI agent.
	/// </summary>
	/// <remarks>
	/// <c>CI</c> is set by essentially every hosted CI provider; <c>GITHUB_ACTIONS</c> is checked as well
	/// so detection does not rest on a single variable.
	/// </remarks>
	private static bool IsContinuousIntegration =>
		IsTruthy(Environment.GetEnvironmentVariable("CI"))
		|| IsTruthy(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

	private static bool IsTruthy(string? value) =>
		!string.IsNullOrWhiteSpace(value)
		&& (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.Ordinal));

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