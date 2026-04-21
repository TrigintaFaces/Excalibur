// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that runs only in development environment for testing purposes.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="DevelopmentJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class DevelopmentJob(ILogger<DevelopmentJob> logger) : IBackgroundJob
{
	private readonly ILogger<DevelopmentJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("🔧 Development job executing at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			await PerformDevelopmentTasksAsync(cancellationToken).ConfigureAwait(false);
			await CollectDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
			await SimulateTestDataAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("🔧 Development job completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "🔧 Development job failed");
			throw;
		}
	}

	/// <summary>
	///     Performs development-specific tasks.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task PerformDevelopmentTasksAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("🔧 Performing development tasks");

		// Simulate development work
		var tasks = new[] { "Clearing development cache", "Refreshing test data", "Validating configuration", "Running diagnostic checks" };

		foreach (var task in tasks)
		{
			_logger.LogDebug("🔧 {Task}", task);
			await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500)), cancellationToken).ConfigureAwait(false);
		}

		_logger.LogDebug("🔧 Development tasks completed");
	}

	/// <summary>
	///     Collects diagnostic information for development purposes.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task CollectDiagnosticsAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("🔧 Collecting diagnostic information");

		await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);

		var diagnostics = new
		{
			Timestamp = DateTimeOffset.UtcNow,
			MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024, // MB
			ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
			UpTime = DateTimeOffset.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
			GCCollections = new { Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2) }
		};

		_logger.LogInformation("🔧 Diagnostics: {@Diagnostics}", diagnostics);
	}

	/// <summary>
	///     Simulates test data generation for development.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task SimulateTestDataAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("🔧 Simulating test data generation");

		// Simulate test data creation
		var testDataTypes = new[] { "Users", "Orders", "Products", "Categories" };

		foreach (var dataType in testDataTypes)
		{
			var recordCount = Random.Shared.Next(10, 100);
			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("🔧 Generated {RecordCount} test {DataType} records", recordCount, dataType);
		}

		_logger.LogDebug("🔧 Test data simulation completed");
	}
}
