// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that performs one-time initialization tasks when the application starts.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="StartupJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class StartupJob(ILogger<StartupJob> logger) : IBackgroundJob
{
	private readonly ILogger<StartupJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Starting application initialization job at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			await InitializeCacheAsync(cancellationToken).ConfigureAwait(false);
			await ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
			await WarmupServicesAsync(cancellationToken).ConfigureAwait(false);
			await LogSystemInfoAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Application initialization completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Application initialization job failed");
			throw;
		}
	}

	/// <summary>
	///     Simulates cache initialization.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task InitializeCacheAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Initializing application cache");

		// Simulate cache warm-up
		await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Application cache initialized");
	}

	/// <summary>
	///     Simulates configuration validation.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ValidateConfigurationAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Validating application configuration");

		// Simulate configuration validation
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Application configuration validated");
	}

	/// <summary>
	///     Simulates service warm-up.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task WarmupServicesAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Warming up application services");

		// Simulate service warm-up
		await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Application services warmed up");
	}

	/// <summary>
	///     Logs system information for diagnostic purposes.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task LogSystemInfoAsync(CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);

		var systemInfo = new
		{
			Environment.MachineName,
			OSVersion = Environment.OSVersion.ToString(),
			Environment.ProcessorCount,
			WorkingSet = Environment.WorkingSet / 1024 / 1024, // MB
			DotNetVersion = Environment.Version.ToString(),
			Environment.CurrentDirectory
		};

		_logger.LogInformation("System Information: {@SystemInfo}", systemInfo);
	}
}
