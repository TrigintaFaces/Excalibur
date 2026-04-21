// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Avoid excessive class coupling - acceptable for sample Program.cs

using Excalibur.Jobs.Abstractions;

using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
	.CreateLogger();

builder.Services.AddSerilog();

// Add Excalibur Job Host with the unified API.
// This sample uses Quartz's in-memory job store; swap in an ADO
// provider (SQL Server, Postgres, etc.) by adding the matching
// Quartz package and configuring it in place of UseInMemoryStore(). [bd-7rqllt]
builder.Services.AddExcalibur(excalibur => excalibur.AddJobs(
	configureQuartz: q =>
	{
		q.UseInMemoryStore();
		q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
	},
	configureJobs: null,
	typeof(Program).Assembly));

// Add health checks endpoint
builder.Services.AddHealthChecks();

// Configure health check UI with in-memory storage (bounded history,
// no database required).
builder.Services
	.AddHealthChecksUI(settings =>
	{
		_ = settings.AddHealthCheckEndpoint("Jobs Health", "/health");
		_ = settings.SetEvaluationTimeInSeconds(60);
		_ = settings.MaximumHistoryEntriesPerEndpoint(50);
	})
	.AddInMemoryStorage();

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);

// Example custom job
namespace JobWorkerSample
{
	public class MaintenanceJob(ILogger<MaintenanceJob> logger) : IBackgroundJob
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
}
