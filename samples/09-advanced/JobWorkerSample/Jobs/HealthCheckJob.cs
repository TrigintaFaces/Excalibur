// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that performs periodic health checks on system components.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="HealthCheckJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class HealthCheckJob(ILogger<HealthCheckJob> logger) : IBackgroundJob
{
	private readonly ILogger<HealthCheckJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Starting health check job at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			var checks = new List<(string Name, Func<CancellationToken, Task<bool>> Check)>
			{
				("Database", CheckDatabaseAsync),
				("External API", CheckExternalApiAsync),
				("File System", CheckFileSystemAsync),
				("Memory Usage", CheckMemoryUsageAsync)
			};

			var results = new List<(string Name, bool IsHealthy, TimeSpan Duration)>();

			foreach (var (name, check) in checks)
			{
				var stopwatch = ValueStopwatch.StartNew();
				try
				{
					var isHealthy = await check(cancellationToken).ConfigureAwait(false);
					var elapsed = stopwatch.Elapsed;
					results.Add((name, isHealthy, elapsed));

					_logger.LogDebug("{CheckName} health check: {Status} ({Duration}ms)",
						name, isHealthy ? "Healthy" : "Unhealthy", (long)elapsed.TotalMilliseconds);
				}
				catch (Exception ex)
				{
					var elapsed = stopwatch.Elapsed;
					results.Add((name, false, elapsed));
					_logger.LogWarning(ex, "{CheckName} health check failed ({Duration}ms)",
						name, (long)elapsed.TotalMilliseconds);
				}
			}

			var healthyCount = results.Count(static r => r.IsHealthy);
			var totalCount = results.Count;
			var avgDuration = results.Average(static r => r.Duration.TotalMilliseconds);

			_logger.LogInformation(
				"Health check completed: {Healthy}/{Total} checks passed, average duration: {AvgDuration:F1}ms",
				healthyCount, totalCount, avgDuration);

			if (healthyCount < totalCount)
			{
				var unhealthyChecks = results.Where(static r => !r.IsHealthy).Select(static r => r.Name);
				_logger.LogWarning("Unhealthy components: {UnhealthyComponents}", string.Join(", ", unhealthyChecks));
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Health check job failed");
			throw;
		}
	}

	/// <summary>
	///     Simulates a database health check.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the database is healthy; otherwise, false. </returns>
	private async Task<bool> CheckDatabaseAsync(CancellationToken cancellationToken)
	{
		// Simulate database check
		await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
		return true; // Always healthy in this sample
	}

	/// <summary>
	///     Simulates an external API health check.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the external API is healthy; otherwise, false. </returns>
	private async Task<bool> CheckExternalApiAsync(CancellationToken cancellationToken)
	{
		// Simulate external API check
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		// Randomly fail sometimes to show unhealthy state
		return Random.Shared.Next(100) > 10;
	}

	/// <summary>
	///     Simulates a file system health check.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the file system is healthy; otherwise, false. </returns>
	private async Task<bool> CheckFileSystemAsync(CancellationToken cancellationToken)
	{
		// Simulate file system check
		await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
		return Directory.Exists(Path.GetTempPath());
	}

	/// <summary>
	///     Simulates a memory usage health check.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if memory usage is within acceptable limits; otherwise, false. </returns>
	private async Task<bool> CheckMemoryUsageAsync(CancellationToken cancellationToken)
	{
		// Simulate memory check
		await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
		var memoryUsage = GC.GetTotalMemory(false);
		return memoryUsage < 1024 * 1024 * 1024; // Less than 1GB
	}
}
