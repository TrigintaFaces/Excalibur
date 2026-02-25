// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.Dispatch.Abstractions.Pipeline;
using Excalibur.Dispatch.Messaging.Abstractions.Delivery.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace examples.Excalibur.Core.Messaging.Delivery.Scheduling;

/// <summary>
/// Example demonstrating how to use the enhanced cron scheduling features.
/// </summary>
public static class CronSchedulingExample
{
	/// <summary>
	/// Configures services with enhanced cron scheduling.
	/// </summary>
	public static void ConfigureServices(IServiceCollection services)
	{
		// Basic cron scheduling with UTC
		_ = services.AddCronScheduling();

		// Or with custom configuration
		_ = services.AddCronScheduling(options =>
		{
			// Set default timezone to Eastern Time
			options.DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

			// Enable seconds in cron expressions (6-field format)
			options.IncludeSeconds = true;

			// Configure how to handle missed executions
			options.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteLatestMissed;
			options.MaxMissedExecutions = 5;

			// Enable daylight saving time adjustments
			options.AutoAdjustForDaylightSaving = true;

			// Restrict supported timezones
			_ = options.SupportedTimeZoneIds.Add("UTC");
			_ = options.SupportedTimeZoneIds.Add("Eastern Standard Time");
			_ = options.SupportedTimeZoneIds.Add("Pacific Standard Time");
			_ = options.SupportedTimeZoneIds.Add("Central European Standard Time");
		});

		// Or use convenience methods
		_ = services.AddCronSchedulingWithTimezones(
			"UTC",
			"Eastern Standard Time",
			"Pacific Standard Time",
			"Central European Standard Time"
		);

		// Add extended cron syntax support
		_ = services.AddExtendedCronScheduling(includeSeconds: true);
	}

	/// <summary>
	/// Example of scheduling messages with cron expressions.
	/// </summary>
	public static async Task ScheduleMessagesAsync(IHost host)
	{
		var scheduler = host.Services.GetRequiredService<IDispatchScheduler>();

		// Schedule a daily report at 9 AM UTC
		var dailyReport = new GenerateReportCommand
		{
			ReportType = "Daily Sales",
			Recipients = "sales-team@company.com"
		};

		await scheduler.ScheduleRecurringAsync("0 9 * * *", dailyReport);

		// Schedule with enhanced scheduler for timezone support
		if (scheduler is EnhancedRecurringDispatchScheduler enhancedScheduler)
		{
			// Schedule a weekly report every Monday at 8 AM Eastern Time
			var weeklyReport = new GenerateReportCommand
			{
				ReportType = "Weekly Summary",
				Recipients = "management@company.com"
			};

			var easternTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			await enhancedScheduler.ScheduleRecurringAsync("0 8 * * MON", easternTime, weeklyReport);

			// Schedule reminders for different timezones
			var timezones = new[]
			{
				("UTC", "0 9 * * *"), // 9 AM UTC
				("Eastern Standard Time", "0 9 * * *"), // 9 AM EST
				("Pacific Standard Time", "0 9 * * *"), // 9 AM PST
			};

			foreach (var (tzId, cron) in timezones)
			{
				var reminder = new SendReminderEvent
				{
					UserId = $"user-{tzId}",
					Message = "Daily standup meeting"
				};

				var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
				await enhancedScheduler.ScheduleRecurringAsync(cron, tz, reminder);
			}
		}
	}

	/// <summary>
	/// Example of managing cron jobs.
	/// </summary>
	public static async Task ManageCronJobsAsync(IHost host)
	{
		var jobStore = host.Services.GetRequiredService<ICronJobStore>();

		// Create a new cron job
		var job = new RecurringCronJob
		{
			Name = "Nightly Data Cleanup",
			Description = "Removes old data from temporary tables",
			CronExpression = "0 2 * * *", // 2 AM daily
			TimeZoneId = "UTC",
			MessageTypeName = typeof(GenerateReportCommand).AssemblyQualifiedName!,
			MessagePayload = """{"ReportType":"Cleanup","Recipients":"ops@company.com"}""",
			Tags = { "maintenance", "nightly" },
			Priority = 1,
			MaxRuntime = TimeSpan.FromHours(1),
			RetryOnFailure = true,
			MaxRetryAttempts = 3
		};

		await jobStore.AddJobAsync(job);

		// Query jobs by tag
		var maintenanceJobs = await jobStore.GetJobsByTagAsync("maintenance");
		foreach (var maintenanceJob in maintenanceJobs)
		{
			Console.WriteLine($"Job: {maintenanceJob.Name} - Next run: {maintenanceJob.NextRunUtc}");
		}

		// Check for due jobs
		var dueJobs = await jobStore.GetDueJobsAsync(DateTimeOffset.UtcNow);
		foreach (var dueJob in dueJobs)
		{
			Console.WriteLine($"Due job: {dueJob.Name}");

			// Process the job...

			// Record execution result
			await jobStore.RecordExecutionAsync(dueJob.Id, success: true);
		}

		// View job history
		var history = await jobStore.GetJobHistoryAsync(job.Id, limit: 10);
		foreach (var entry in history)
		{
			Console.WriteLine($"Execution at {entry.StartedUtc}: {(entry.Success ? "Success" : "Failed")}");
		}

		// Disable a job temporarily
		_ = await jobStore.SetJobEnabledAsync(job.Id, enabled: false);

		// Remove a job
		_ = await jobStore.RemoveJobAsync(job.Id);
	}

	/// <summary>
	/// Example of using the cron scheduler directly.
	/// </summary>
	public static void UseCronScheduler(IServiceProvider services)
	{
		var cronScheduler = services.GetRequiredService<ICronScheduler>();

		// Parse and validate cron expressions
		if (cronScheduler.TryParse("0 0 * * *", out var dailyMidnight))
		{
			Console.WriteLine($"Valid cron: {dailyMidnight!.GetDescription()}");

			// Get next 5 occurrences
			var now = DateTimeOffset.Now;
			for (int i = 0; i < 5; i++)
			{
				var next = dailyMidnight.GetNextOccurrence(now);
				if (next.HasValue)
				{
					Console.WriteLine($"Next run: {next.Value}");
					now = next.Value.AddMinutes(1);
				}
			}
		}

		// Work with specific timezones
		var pacificTime = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
		var pacificCron = cronScheduler.Parse("0 17 * * FRI", pacificTime); // 5 PM PST every Friday

		// Check if a specific time would trigger
		var friday5pm = new DateTimeOffset(2025, 1, 3, 17, 0, 0,
			pacificTime.GetUtcOffset(new DateTime(2025, 1, 3)));

		if (cronScheduler.WouldTriggerAt(pacificCron, friday5pm))
		{
			Console.WriteLine("This time matches the cron schedule!");
		}

		// Handle missed executions
		if (cronScheduler is CronScheduler scheduler)
		{
			var lastRun = DateTimeOffset.UtcNow.AddDays(-3);
			var missed = scheduler.GetMissedExecutions(pacificCron, lastRun, DateTimeOffset.UtcNow);

			foreach (var missedExecution in missed)
			{
				Console.WriteLine($"Missed execution at: {missedExecution}");
			}
		}
	}

	/// <summary>
	/// Example message to schedule.
	/// </summary>
	public class GenerateReportCommand : IDispatchAction
	{
		public string ReportType { get; set; } = string.Empty;
		public string Recipients { get; set; } = string.Empty;
	}

	/// <summary>
	/// Example message for timezone-specific scheduling.
	/// </summary>
	public class SendReminderEvent : IDispatchEvent
	{
		public string UserId { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
	}
}