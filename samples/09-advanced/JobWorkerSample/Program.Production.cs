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

// Add Excalibur Job Host with production-ready configuration using the unified API
builder.Services.AddExcaliburJobHost(
	configureQuartz: q =>
	{
		// Enable persistent job store with SQL Server
		var connectionString = builder.Configuration.GetConnectionString("JobStore");
		if (!string.IsNullOrEmpty(connectionString))
		{
			q.UsePersistentStore(store =>
			{
				store.UseProperties = true;
				store.PerformSchemaValidation = true;
				// Note: This will need Quartz SQL Server package extensions to work store.UseSqlServer(connectionString);
			});
		}

		// Configure thread pool
		q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
	},
	configureJobs: null,
	typeof(Program).Assembly);

// Add health checks endpoint
builder.Services.AddHealthChecks();

// Configure health check UI (optional)
builder.Services.AddHealthChecksUI(settings =>
{
	_ = settings.AddHealthCheckEndpoint("Jobs Health", "/health");
	_ = settings.SetEvaluationTimeInSeconds(60);
	_ = settings.MaximumHistoryEntriesPerEndpoint(50);
}); // .AddInMemoryStorage(); // TODO: Fix health checks UI storage

var host = builder.Build();

// Run migrations for job persistence store if needed
using (var scope = host.Services.CreateScope())
{
	var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
	if (configuration.GetValue<bool>("Jobs:RunMigrations"))
	{
		// This would typically be handled by your migration strategy For Quartz.NET, tables are usually created manually or via scripts
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
		logger.LogInformation("Job store migrations would run here if configured");
	}
}

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
