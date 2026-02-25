// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that performs daily data cleanup operations.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="DataCleanupJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class DataCleanupJob(ILogger<DataCleanupJob> logger) : IBackgroundJob
{
	private readonly ILogger<DataCleanupJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Starting data cleanup job at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			// Simulate cleanup operations
			await CleanupTempFilesAsync(cancellationToken).ConfigureAwait(false);
			await CleanupOldLogsAsync(cancellationToken).ConfigureAwait(false);
			await CleanupExpiredCacheAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Data cleanup job completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Data cleanup job failed");
			throw;
		}
	}

	/// <summary>
	///     Simulates cleaning up temporary files.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task CleanupTempFilesAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Cleaning up temporary files");

		// Simulate some work
		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Temporary files cleaned up");
	}

	/// <summary>
	///     Simulates cleaning up old log files.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task CleanupOldLogsAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Cleaning up old log files");

		// Simulate some work
		await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Old log files cleaned up");
	}

	/// <summary>
	///     Simulates cleaning up expired cache entries.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task CleanupExpiredCacheAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("Cleaning up expired cache entries");

		// Simulate some work
		await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);

		_logger.LogDebug("Expired cache entries cleaned up");
	}
}
