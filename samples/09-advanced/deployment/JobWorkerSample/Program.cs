// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ============================================================================
// MINIMAL SETUP (uncomment for quickstart, comment out everything below)
// ============================================================================
// For a 5-minute quickstart with zero configuration:
//
// using Excalibur.Jobs;
// using Excalibur.Jobs.Quartz;
//
// var builder = Host.CreateApplicationBuilder(args);
// builder.Services.AddExcalibur(excalibur => excalibur.AddJobs(
//     configureJobs: jobs =>
//     {
//         jobs.AddRecurringJob<HelloWorldJob>(TimeSpan.FromMinutes(1), "hello-job");
//     },
//     typeof(Program).Assembly));
// var host = builder.Build();
// host.Run();
//
// public class HelloWorldJob : IBackgroundJob
// {
//     private readonly ILogger<HelloWorldJob> _logger;
//     public HelloWorldJob(ILogger<HelloWorldJob> logger) => _logger = logger;
//     public Task ExecuteAsync(CancellationToken cancellationToken = default)
//     {
//         _logger.LogInformation("Hello from Excalibur Job at {Time}", DateTimeOffset.Now);
//         return Task.CompletedTask;
//     }
// }
//
// That's it! Jobs are discovered and scheduled automatically.
// The configuration below shows the full-featured production setup.
// ============================================================================

#pragma warning disable CA1506 // Avoid excessive class coupling - acceptable for sample Program.cs

using Excalibur.Jobs.Quartz;

using JobWorkerSample.Jobs;

using Serilog;

namespace JobWorkerSample;

/// <summary>
///     Entry point for the Job Worker Service sample application demonstrating Excalibur Jobs with Quartz.NET.
/// </summary>
public class Program
{
	/// <summary>
	///     Main entry point for the application.
	/// </summary>
	/// <param name="args"> Command line arguments. </param>
	public static void Main(string[] args)
	{
		// Configure Serilog for logging
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
			.CreateLogger();

		try
		{
			Log.Information("Starting Excalibur Job Worker Service");

			var builder = Host.CreateApplicationBuilder(args);

			// Configure logging
			_ = builder.Services.AddSerilog();

			// Configure Excalibur Job Host with Quartz.NET using the unified API.
			// This sample uses Quartz's in-memory job store — no database
			// required. Swap in a persistent ADO store (SQL Server, Postgres,
			// etc.) by adding the matching Quartz provider package and
 // configuring it in place of UseInMemoryStore.
			_ = builder.Services.AddExcalibur(excalibur => excalibur.AddJobs(
				configureQuartz: q =>
				{
					q.UseInMemoryStore();

					// Configure job execution settings
					q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);

					// Configure job data map handling
					q.UseMicrosoftDependencyInjectionJobFactory();

					// NOTE: The built-in CdcJob / OutboxJob / DataProcessingJob are intentionally
					// NOT scheduled here. This sample demonstrates the job HOST and distributed
					// coordination using an in-memory store with no database. Those jobs each require
					// their backing subsystem to be registered, or they fail to activate at trigger time:
					//
					//   CdcJob             -> builder.Services.AddExcaliburSqlServices();
					//                         builder.Services.AddSqlServerCdcJob(builder.Configuration);
					//                         then: CdcJob.ConfigureJob(q, builder.Configuration);
					//   OutboxJob          -> builder.Services.AddExcalibur(x => x.AddOutbox(o => o.UseInMemory()));
					//                         then: OutboxJob.ConfigureJob(q, builder.Configuration);
					//   DataProcessingJob  -> builder.Services.AddDataProcessing(dp => dp.ConnectionFactory(...)
					//                                                                     .BindConfiguration("DataProcessing"));
					//                         then: DataProcessingJob.ConfigureJob(q, builder.Configuration);
					//
					// See samples/09-advanced/cdc/CdcJobQuartz for a complete, runnable CdcJob worker.
				},
				configureJobs: jobs =>
				{
					// Sample jobs for demonstration using the fluent API
					_ = jobs.AddRecurringJob<HealthCheckJob>(TimeSpan.FromMinutes(5), "health-monitor");
					_ = jobs.AddOneTimeJob<StartupJob>("startup-init");

					// Conditionally add development jobs
					_ = jobs.AddJobIf(builder.Environment.IsDevelopment(),
						devJobs => _ = devJobs.AddRecurringJob<DevelopmentJob>(TimeSpan.FromMinutes(1), "dev-utilities"));
				},
				typeof(Program).Assembly));

			// Optional: Add job coordination for distributed deployments
			var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
			if (!string.IsNullOrEmpty(redisConnectionString))
			{
				_ = builder.Services.AddJobCoordinationRedis(redisConnectionString);
			}

			var host = builder.Build();

			Log.Information("Excalibur Job Worker Service configured with Quartz in-memory store and ready to start");
			host.Run();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Application terminated unexpectedly");
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}
}
