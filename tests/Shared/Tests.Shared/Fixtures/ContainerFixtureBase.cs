// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Diagnostics;

using Tests.Shared.Infrastructure;

namespace Tests.Shared.Fixtures;

/// <summary>
/// Base class for container fixtures that provides standardized timeout and error handling.
/// </summary>
/// <remarks>
/// <para>
/// This unified base class provides consistent Docker container lifecycle management
/// with proper timeout handling, error detection, and graceful degradation when
/// Docker is unavailable.
/// </para>
/// <para>
/// Derived classes must implement <see cref="InitializeContainerAsync"/> and
/// <see cref="DisposeContainerAsync"/> to manage their specific container type.
/// </para>
/// <para>
/// Docker is expected to be available in all CI environments (GitHub Actions provides Docker
/// out of the box). Tests no longer skip when Docker is unavailable - they fail, which is
/// the correct behavior for CI/CD pipelines.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class SqlServerContainerFixture : ContainerFixtureBase
/// {
///     private MsSqlContainer? _container;
///
///     public string ConnectionString => _container?.GetConnectionString()
///         ?? throw new InvalidOperationException("Container not initialized");
///
///     protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
///     {
///         _container = new MsSqlBuilder()
///             .WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
///             .Build();
///         await _container.StartAsync(cancellationToken);
///     }
///
///     protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
///     {
///         if (_container is not null)
///         {
///             await _container.DisposeAsync();
///         }
///     }
/// }
/// </code>
/// </example>
public abstract class ContainerFixtureBase : IAsyncLifetime
{
	/// <summary>
	/// Gets a value indicating whether Docker is available and the container started successfully.
	/// </summary>
	/// <remarks>
	/// Always <c>true</c> after successful initialization. If Docker is unavailable,
	/// <see cref="InitializeAsync"/> throws instead of silently degrading.
	/// </remarks>
	public bool DockerAvailable { get; private set; } = true;

	/// <summary>
	/// Gets the error message if container initialization failed.
	/// </summary>
	public string? InitializationError { get; private set; }

	/// <summary>
	/// Marks the fixture as unavailable after the container started but a dependent
	/// service (e.g., queue creation, admin API) failed. Tests should skip.
	/// </summary>
	/// <param name="reason">Description of what failed.</param>
	public void MarkUnavailable(string reason)
	{
		DockerAvailable = false;
		InitializationError = reason;
	}

	/// <summary>
	/// Gets a value indicating whether the fixture should degrade gracefully when the
	/// container cannot start, instead of throwing.
	/// </summary>
	/// <remarks>
	/// Override and return <c>true</c> for optional infrastructure (e.g., cloud transport
	/// emulators like LocalStack or ASB emulator) that may not be available in all CI
	/// environments. When <c>true</c>, initialization failure sets <see cref="DockerAvailable"/>
	/// to <c>false</c> without throwing, allowing tests to skip gracefully.
	/// Default is <c>false</c> — required infrastructure (SQL Server, Redis) fails loudly.
	/// </remarks>
	protected virtual bool AllowGracefulDegradation => false;

	/// <summary>
	/// Gets the timeout for container startup operations.
	/// </summary>
	/// <remarks>
	/// Override this property in derived classes for containers that require longer
	/// startup times (e.g., SQL Server may need more time than Redis).
	/// Default value uses <see cref="TestTimeouts.ContainerStart"/> which is affected
	/// by the <c>TEST_TIMEOUT_MULTIPLIER</c> environment variable.
	/// </remarks>
	protected virtual TimeSpan ContainerStartTimeout => TestTimeouts.ContainerStart;

	/// <summary>
	/// Gets the timeout for container disposal operations.
	/// </summary>
	/// <remarks>
	/// Override this property in derived classes if containers need more time to shut down cleanly.
	/// Default value uses <see cref="TestTimeouts.ContainerDispose"/> which is affected
	/// by the <c>TEST_TIMEOUT_MULTIPLIER</c> environment variable.
	/// </remarks>
	protected virtual TimeSpan ContainerDisposeTimeout => TestTimeouts.ContainerDispose;

	/// <summary>
	/// Gets the TOTAL wall-clock budget for <see cref="InitializeAsync"/>, across every retry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This exists because the fixture must lose a race deliberately. The test runs pass
	/// <c>--blame-hang-timeout</c> (10m on some jobs, 5m on others); when no test reports progress
	/// for that long, the blame collector kills the test host. A killed host does not fail the run
	/// in any visible way: the tests that had already finished are reported as passed, the tests
	/// that had not started yet are simply absent, and the runner prints
	/// <c>Passed! - Failed: 0</c>.
	/// </para>
	/// <para>
	/// The per-attempt timeout alone could exceed that. <see cref="TestTimeouts.ContainerStart"/> is
	/// 120s scaled by <c>TEST_TIMEOUT_MULTIPLIER</c>, which is 3 on CI, so one attempt could run 6
	/// minutes and three attempts plus backoff could run about 18 -- comfortably past the blame
	/// timeout. A slow container therefore killed the host before this class could throw the clear
	/// "container startup failed" error it is written to throw.
	/// </para>
	/// <para>
	/// Measured, on the run that produced this: the last test finished at 13:12:08, the assembly
	/// reported success at 13:22:21 -- a 10.2 minute gap against a 10 minute blame timeout -- and 96
	/// tests that had never run were missing from the results entirely. Not failed, not skipped:
	/// absent. A healthy run of the same commit executed all 96. The only thing that noticed was the
	/// population census, which refused because the accounted total did not equal the expected one.
	/// </para>
	/// <para>
	/// So the budget is deliberately below the SHORTEST blame timeout in use, not the longest. If a
	/// container cannot start within it, this class throws while the host is still alive, the
	/// collection fails visibly, and the failure names the container instead of vanishing.
	/// Override only to make it smaller.
	/// </para>
	/// </remarks>
	protected virtual TimeSpan TotalInitializationBudget => TestTimeouts.ContainerInitBudget;

	/// <summary>
	/// Initializes the container with proper timeout and error handling.
	/// </summary>
	/// <remarks>
	/// Docker is expected to be available in all CI environments.
	/// Failures propagate as exceptions so tests fail visibly rather than silently skipping.
	/// </remarks>
	public async ValueTask InitializeAsync()
	{
		const int maxAttempts = 3;
		Exception? lastException = null;

		// The ACTUAL number of attempts made, which is not always maxAttempts: a non-retriable
		// exception breaks out of the loop immediately. Reporting the constant instead of this
		// counter tells the reader the fixture tried three times when it may have tried once, and
		// sends them looking for a flaky container when the real cause was a hard failure on the
		// first call. Measured case: a CosmosDB 503 is not classified retriable, so the loop broke
		// after attempt 1 while the message claimed 3.
		var attemptsMade = 0;

		// The whole of initialization is bounded, not just each attempt. See
		// TotalInitializationBudget: exceeding the runner's blame-hang timeout does not produce a
		// failure, it produces a KILLED HOST that still reports "Passed!" with the unrun tests
		// silently missing. Every wait below is clamped to what is left of this budget so the
		// fixture always throws first, while there is still a process alive to throw in.
		var budget = TotalInitializationBudget;
		var startedAt = Stopwatch.StartNew();
		TimeSpan Remaining() => budget - startedAt.Elapsed;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			attemptsMade = attempt;

			var attemptBudget = Remaining();
			if (attemptBudget <= TimeSpan.Zero)
			{
				lastException ??= new TimeoutException(
					$"The total container initialization budget of {budget.TotalSeconds:F0}s was exhausted before attempt {attempt}.");
				break;
			}

			// Whichever is tighter: this attempt's own timeout, or what remains of the total budget.
			var effectiveTimeout = attemptBudget < ContainerStartTimeout ? attemptBudget : ContainerStartTimeout;

			try
			{
				using var cts = new CancellationTokenSource(effectiveTimeout);
				await InitializeContainerAsync(cts.Token).ConfigureAwait(false);
				DockerAvailable = true;
				InitializationError = null;
				return;
			}
			catch (Exception ex) when (attempt < maxAttempts && IsRetriableDockerException(ex))
			{
				// Retry transient Docker startup issues (daemon hiccups, named pipe timeouts, etc.).
				lastException = ex;

				var backoff = TimeSpan.FromSeconds(5 * attempt);
				var left = Remaining();
				if (left <= TimeSpan.Zero)
				{
					break;
				}

				// Never sleep past the budget: a backoff that outlives it hands the remaining time
				// to the blame collector, which is the outcome this budget exists to prevent.
				await Task.Delay(backoff < left ? backoff : left).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				lastException = ex;
				break;
			}
		}

		DockerAvailable = false;
		InitializationError = lastException?.Message ?? "Container initialization failed after retries.";

		if (!AllowGracefulDegradation)
		{
			// Throw so the collection/class fixture fails clearly instead of producing
			// hundreds of cryptic "Could not find resource" errors from individual tests.
			throw new InvalidOperationException(
				$"Container startup failed after {attemptsMade} attempt(s) (max {maxAttempts}) within a total budget of "
				+ $"{TotalInitializationBudget.TotalSeconds:F0}s, having spent {startedAt.Elapsed.TotalSeconds:F0}s: {InitializationError}. "
				+ "This is thrown deliberately BEFORE the runner's blame-hang timeout so the failure is visible: a hang "
				+ "past that timeout kills the test host, and the tests that never ran then go MISSING from the results "
				+ "rather than failing, while the assembly still reports Passed.",
				lastException);
		}

		// Graceful degradation: container is optional (e.g., cloud transport emulators).
		// Tests should check DockerAvailable and skip when false.
	}

	/// <summary>
	/// Disposes the container resources.
	/// </summary>
	/// <remarks>
	/// Disposal is skipped if the container never started successfully (i.e., <see cref="DockerAvailable"/> is <c>false</c>).
	/// Errors during disposal are swallowed to prevent tests from failing due to cleanup issues.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		if (DockerAvailable)
		{
			try
			{
				using var cts = new CancellationTokenSource(ContainerDisposeTimeout);
				// Use Task.WhenAny because IContainer.DisposeAsync() does not accept
				// a CancellationToken, so the CTS alone cannot interrupt a hung dispose.
				var disposeTask = DisposeContainerAsync(cts.Token);
				var completed = await Task.WhenAny(disposeTask, Task.Delay(ContainerDisposeTimeout)).ConfigureAwait(false);
				if (completed != disposeTask)
				{
					// Timed out -- abandon the dispose rather than hanging the test host
					return;
				}

				await disposeTask.ConfigureAwait(false);
			}
			catch
			{
				// Best effort cleanup - don't fail tests due to cleanup issues
			}
		}
	}

	/// <summary>
	/// When implemented in a derived class, initializes the specific container.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected abstract Task InitializeContainerAsync(CancellationToken cancellationToken);

	/// <summary>
	/// When implemented in a derived class, disposes the specific container.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected abstract Task DisposeContainerAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether an exception is related to Docker not being available.
	/// </summary>
	/// <param name="ex">The exception to check.</param>
	/// <returns><c>true</c> if the exception indicates Docker is unavailable; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This method checks for common Docker-related error patterns including:
	/// <list type="bullet">
	/// <item><description>"Docker" in the message (Docker daemon not running)</description></item>
	/// <item><description>"container" in the message (container startup failures)</description></item>
	/// </list>
	/// The check is case-insensitive to catch various error message formats.
	/// </remarks>
	private static bool IsDockerException(Exception ex)
	{
		return ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
				 ex.Message.Contains("container", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsRetriableDockerException(Exception ex)
	{
		Exception? current = ex;
		while (current is not null)
		{
			if (current is TimeoutException || current is TaskCanceledException || IsDockerException(current)
				|| IsServiceStillStartingException(current))
			{
				return true;
			}

			current = current.InnerException;
		}

		return false;
	}

	/// <summary>
	/// True when the container is up but the service inside it has not finished starting, which the
	/// backend reports as a 503 and explicitly invites the caller to retry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the same condition as a data plane that lags its control plane: the daemon started the
	/// container, the endpoint answers, and the engine behind it is not ready yet. It is emphatically
	/// retriable -- the Cosmos emulator says so in the response body, <c>"pgcosmos extension is still
	/// starting; retry request shortly"</c> -- but it arrives as a provider exception rather than a
	/// Docker or timeout one, so the classifier above rejected it and the loop broke on the first
	/// attempt while advertising three.
	/// </para>
	/// <para>
	/// Matched on the message rather than the provider's exception type so that Tests.Shared does not
	/// take a dependency on every backend SDK it may host a container for. The tokens are the status
	/// itself and the backend's own retry invitation, both narrow enough not to catch a genuine
	/// service fault: a backend that is broken rather than starting does not ask to be retried.
	/// </para>
	/// </remarks>
	private static bool IsServiceStillStartingException(Exception ex)
	{
		var message = ex.Message;

		return message.Contains("still starting", StringComparison.OrdinalIgnoreCase)
			|| message.Contains("retry request shortly", StringComparison.OrdinalIgnoreCase)
			|| (message.Contains("ServiceUnavailable", StringComparison.OrdinalIgnoreCase)
				&& message.Contains("503", StringComparison.Ordinal));
	}
}
