// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Avoid excessive class coupling - acceptable for sample Program.cs

using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.DataProcessing;
using Excalibur.Jobs.Outbox;

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

			// Configure Excalibur Job Host with Quartz.NET using the unified API
			// This replaces the old pattern of calling AddExcaliburJobHost + AddExcaliburJobs + AddExcaliburJobsWithConfiguration separately
			var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
			_ = builder.Services.AddExcaliburJobHost(
				configureQuartz: q =>
				{
					// Configure Quartz.NET persistence - tracks ALL job executions, states, and history
					if (!string.IsNullOrEmpty(connectionString))
					{
						// Use persistent store with SQL Server for job execution tracking
						q.UsePersistentStore(store =>
						{
							store.UseProperties = true; // Store all JobDataMap values as strings (recommended)
							store.PerformSchemaValidation = true; // Validate database schema
																  // TODO: Add SQL Server and SystemTextJson extensions when packages are available
																  // store.UseSqlServer(sql => { sql.ConnectionString = connectionString; sql.TablePrefix = "QRTZ_"; });
																  // store.UseSystemTextJsonSerializer();

							// Optional: Enable clustering for distributed job execution
							// store.UseClustering(c => { c.CheckinInterval = TimeSpan.FromSeconds(10); c.CheckinMisfireThreshold = TimeSpan.FromSeconds(20); });
						});
					}
					else
					{
						// Fallback to in-memory store (no persistence)
						q.UseInMemoryStore();
					}

					// Configure job execution settings
					q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);

					// Configure job data map handling
					q.UseMicrosoftDependencyInjectionJobFactory();

					// Register actual Excalibur jobs using their built-in ConfigureJob methods
					// These jobs read their configuration from appsettings.json "Jobs:{JobName}" sections
					CdcJob.ConfigureJob(q, builder.Configuration);
					OutboxJob.ConfigureJob(q, builder.Configuration);
					DataProcessingJob.ConfigureJob(q, builder.Configuration);
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
				typeof(Program).Assembly);

			// Optional: Add job coordination for distributed deployments
			var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
			if (!string.IsNullOrEmpty(redisConnectionString))
			{
				_ = builder.Services.AddJobCoordinationRedis(redisConnectionString);
			}

			// Optional: Add workflow orchestration support
			_ = builder.Services.AddWorkflows();

			var host = builder.Build();

			Log.Information("Excalibur Job Worker Service configured with Quartz persistence and ready to start");
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
