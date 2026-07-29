// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// PRODUCTION VARIANT (reference only — Program.cs is the application entry point)
// ============================================================================
// This file shows the same job host wired the way a production deployment
// usually wants it: Serilog read from configuration, plus a health-check
// endpoint and the health-check UI. It is a plain class rather than a second
// set of top-level statements, because a project can only have one entry point.
// To run this variant instead, call ProductionHostExample.RunAsync(args) from
// Program.Main.
// ============================================================================

#pragma warning disable CA1506 // Avoid excessive class coupling - acceptable for sample Program.cs

using Excalibur.Jobs;

using Serilog;

namespace JobWorkerSample;

/// <summary>
///     Production-shaped host configuration for the Job Worker sample.
/// </summary>
internal static class ProductionHostExample
{
	/// <summary>
	///     Builds and runs the job worker host with production logging and health checks.
	/// </summary>
	/// <param name="args"> Command line arguments. </param>
	/// <param name="cancellationToken"> Token used to stop the host. </param>
	public static async Task RunAsync(string[] args, CancellationToken cancellationToken)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Configure Serilog
		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(builder.Configuration)
			.Enrich.FromLogContext()
			.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
			.CreateLogger();

		_ = builder.Services.AddSerilog();

		// Add Excalibur Job Host with the unified API.
		// This sample uses Quartz's in-memory job store; swap in an ADO
		// provider (SQL Server, Postgres, etc.) by adding the matching
		// Quartz package and configuring it in place of UseInMemoryStore.
		_ = builder.Services.AddExcalibur(excalibur => excalibur.AddJobs(
			configureQuartz: q =>
			{
				q.UseInMemoryStore();
				q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
			},
			configureJobs: null,
			typeof(Program).Assembly));

		// Add health checks endpoint
		_ = builder.Services.AddHealthChecks();

		// Configure health check UI with in-memory storage (bounded history,
		// no database required).
		_ = builder.Services
			.AddHealthChecksUI(settings =>
			{
				_ = settings.AddHealthCheckEndpoint("Jobs Health", "/health");
				_ = settings.SetEvaluationTimeInSeconds(60);
				_ = settings.MaximumHistoryEntriesPerEndpoint(50);
			})
			.AddInMemoryStorage();

		var host = builder.Build();

		await host.RunAsync(cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
///     Example custom job showing the shape of a long-running maintenance task.
/// </summary>
internal sealed class MaintenanceJob(ILogger<MaintenanceJob> logger) : IBackgroundJob
{
	private readonly ILogger<MaintenanceJob> _logger = logger;

	/// <inheritdoc/>
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Running maintenance job at {Time}", DateTimeOffset.Now);

		// Perform maintenance tasks
		await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

		_logger.LogInformation("Maintenance job completed");
	}
}
