// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)

using Xunit;

namespace Excalibur.Testing.Containers;

/// <summary>
/// Base class for TestContainers-backed fixtures providing standardized Docker container lifecycle
/// management with timeout handling, transient-failure retry, and optional graceful degradation.
/// </summary>
/// <remarks>
/// <para>
/// Derived classes implement <see cref="InitializeContainerAsync"/> and <see cref="DisposeContainerAsync"/>
/// to manage their specific container. Docker is expected to be available in CI; required infrastructure
/// fails loudly, while optional infrastructure can opt into graceful degradation via
/// <see cref="AllowGracefulDegradation"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyRedisFixture : ContainerFixtureBase
/// {
///     private RedisContainer? _container;
///     public string ConnectionString => _container?.GetConnectionString()
///         ?? throw new InvalidOperationException("Container not initialized");
///
///     protected override async Task InitializeContainerAsync(CancellationToken ct)
///     {
///         _container = new RedisBuilder().Build();
///         await _container.StartAsync(ct);
///     }
///
///     protected override async Task DisposeContainerAsync(CancellationToken ct)
///     {
///         if (_container is not null) await _container.DisposeAsync();
///     }
/// }
/// </code>
/// </example>
public abstract class ContainerFixtureBase : IAsyncLifetime
{
	/// <summary>
	/// Gets a value indicating whether Docker is available and the container started successfully.
	/// Always <see langword="true"/> after successful initialization; if Docker is unavailable,
	/// <see cref="InitializeAsync"/> throws (unless <see cref="AllowGracefulDegradation"/> is set).
	/// </summary>
	public bool DockerAvailable { get; private set; } = true;

	/// <summary>Gets the error message if container initialization failed; otherwise <see langword="null"/>.</summary>
	public string? InitializationError { get; private set; }

	/// <summary>
	/// Marks the fixture as unavailable after the container started but a dependent service failed.
	/// </summary>
	/// <param name="reason">Description of what failed.</param>
	public void MarkUnavailable(string reason)
	{
		DockerAvailable = false;
		InitializationError = reason;
	}

	/// <summary>
	/// Gets a value indicating whether the fixture should degrade gracefully (set
	/// <see cref="DockerAvailable"/> to <see langword="false"/> without throwing) when the container cannot
	/// start, instead of throwing. Default is <see langword="false"/> — required infrastructure fails loudly.
	/// </summary>
	protected virtual bool AllowGracefulDegradation => false;

	/// <summary>Gets the timeout for container startup operations.</summary>
	protected virtual TimeSpan ContainerStartTimeout => ContainerTimeouts.ContainerStart;

	/// <summary>Gets the timeout for container disposal operations.</summary>
	protected virtual TimeSpan ContainerDisposeTimeout => ContainerTimeouts.ContainerDispose;

	/// <summary>
	/// Initializes the container with timeout handling and transient-failure retry.
	/// </summary>
	/// <returns>A task representing the asynchronous initialization.</returns>
	public async ValueTask InitializeAsync()
	{
		const int maxAttempts = 3;
		Exception? lastException = null;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				using var cts = new CancellationTokenSource(ContainerStartTimeout);
				await InitializeContainerAsync(cts.Token).ConfigureAwait(false);
				DockerAvailable = true;
				InitializationError = null;
				return;
			}
			catch (Exception ex) when (attempt < maxAttempts && IsRetriableDockerException(ex))
			{
				lastException = ex;
				await Task.Delay(TimeSpan.FromSeconds(5 * attempt), CancellationToken.None).ConfigureAwait(false);
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
			throw new InvalidOperationException(
				$"Container startup failed after {maxAttempts} attempts: {InitializationError}",
				lastException);
		}
	}

	/// <summary>
	/// Disposes the container resources, best-effort, bounded by <see cref="ContainerDisposeTimeout"/>.
	/// </summary>
	/// <returns>A task representing the asynchronous disposal.</returns>
	public async ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);

		if (!DockerAvailable)
		{
			return;
		}

		try
		{
			using var cts = new CancellationTokenSource(ContainerDisposeTimeout);
			var disposeTask = DisposeContainerAsync(cts.Token);
			var completed = await Task.WhenAny(disposeTask, Task.Delay(ContainerDisposeTimeout, CancellationToken.None)).ConfigureAwait(false);
			if (completed != disposeTask)
			{
				return;
			}

			await disposeTask.ConfigureAwait(false);
		}
		catch
		{
			// Best-effort cleanup — never fail a test on container teardown.
		}
	}

	/// <summary>
	/// When implemented in a derived class, initializes the specific container.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected abstract Task InitializeContainerAsync(CancellationToken cancellationToken);

	/// <summary>
	/// When implemented in a derived class, disposes the specific container.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected abstract Task DisposeContainerAsync(CancellationToken cancellationToken);

	private static bool IsDockerException(Exception ex) =>
		ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
		|| ex.Message.Contains("container", StringComparison.OrdinalIgnoreCase);

	private static bool IsRetriableDockerException(Exception ex)
	{
		Exception? current = ex;
		while (current is not null)
		{
			if (current is TimeoutException or TaskCanceledException || IsDockerException(current))
			{
				return true;
			}

			current = current.InnerException;
		}

		return false;
	}
}
