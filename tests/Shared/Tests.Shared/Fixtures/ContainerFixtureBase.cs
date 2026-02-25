// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

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
///             .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
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
	/// Initializes the container with proper timeout and error handling.
	/// </summary>
	/// <remarks>
	/// Docker is expected to be available in all CI environments.
	/// Failures propagate as exceptions so tests fail visibly rather than silently skipping.
	/// </remarks>
	public async Task InitializeAsync()
	{
		try
		{
			using var cts = new CancellationTokenSource(ContainerStartTimeout);
			await InitializeContainerAsync(cts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			DockerAvailable = false;
			InitializationError = ex.Message;
			throw;
		}
	}

	/// <summary>
	/// Disposes the container resources.
	/// </summary>
	/// <remarks>
	/// Disposal is skipped if the container never started successfully (i.e., <see cref="DockerAvailable"/> is <c>false</c>).
	/// Errors during disposal are swallowed to prevent tests from failing due to cleanup issues.
	/// </remarks>
	public async Task DisposeAsync()
	{
		if (DockerAvailable)
		{
			try
			{
				using var cts = new CancellationTokenSource(ContainerDisposeTimeout);
				await DisposeContainerAsync(cts.Token).ConfigureAwait(false);
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
}
