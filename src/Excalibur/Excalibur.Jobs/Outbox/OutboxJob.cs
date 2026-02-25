// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Quartz;

using IJob = Quartz.IJob;

namespace Excalibur.Jobs.Outbox;

/// <summary>
/// Represents a Quartz job responsible for dispatching outbox messages.
/// </summary>
/// <remarks>
/// This job reads messages from the outbox, deserializes them, and dispatches them using the provided dispatcher. The job configuration is
/// defined in the application configuration under "Jobs:OutboxJob".
/// </remarks>
[DisallowConcurrentExecution]
public sealed class OutboxJob : IJob, IConfigurableJob<OutboxJobConfig>
{
	/// <summary>
	/// The configuration section name used to bind <see cref="OutboxJobConfig"/> from application configuration.
	/// </summary>
	public const string JobConfigSectionName = $"Jobs:{nameof(OutboxJob)}";

	private static readonly string DispatcherId = Uuid7Extensions.GenerateString();

	private readonly IOutboxDispatcher _outbox;

	private readonly JobHeartbeatTracker _heartbeatTracker;
	private readonly ILogger<OutboxJob> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxJob" /> class.
	/// </summary>
	/// <param name="outbox"> The manager responsible for handling outbox operations. </param>
	/// <param name="heartbeatTracker"> The heartbeat tracker for recording job activity. </param>
	/// <param name="logger"> The logger for logging job activities. </param>
	public OutboxJob(IOutboxDispatcher outbox, JobHeartbeatTracker heartbeatTracker, ILogger<OutboxJob> logger)
	{
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(heartbeatTracker);
		ArgumentNullException.ThrowIfNull(logger);

		_outbox = outbox;
		_heartbeatTracker = heartbeatTracker;
		_logger = logger;
	}

	/// <summary>
	/// Configures the Quartz job and its associated trigger.
	/// </summary>
	/// <param name="configurator"> The Quartz configurator for registering the job and trigger. </param>
	/// <param name="configuration"> The application configuration. </param>
	public static void ConfigureJob(IServiceCollectionQuartzConfigurator configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetJobConfiguration<OutboxJobConfig>(JobConfigSectionName);
		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		if (jobConfig.Disabled)
		{
			return;
		}

		_ = configurator.AddJob<OutboxJob>(
			jobKey,
			job => job.WithIdentity(jobKey).WithDescription("Dispatch outbox messages job"));

		_ = configurator.AddTrigger(trigger => trigger.ForJob(jobKey).WithIdentity($"{jobConfig.JobName}Trigger")
			.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(15))).WithCronSchedule(jobConfig.CronSchedule)
			.WithDescription("A cron based trigger for the dispatch of outbox messages"));
	}

	/// <summary>
	/// Configures health checks for the job.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder. </param>
	/// <param name="configuration"> The application configuration. </param>
	public static void ConfigureHealthChecks(IHealthChecksBuilder healthChecks, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetJobConfiguration<OutboxJobConfig>(JobConfigSectionName);

		_ = healthChecks.Add(new HealthCheckRegistration(
			$"{jobConfig.JobName}HealthCheck",
			sp => new JobHealthCheck(
				jobConfig.JobName,
				jobConfig,
				sp.GetRequiredService<JobHeartbeatTracker>()),
			failureStatus: null,
			tags: null));
	}

	/// <summary>
	/// Executes the job, processing outbox messages.
	/// </summary>
	/// <param name="context"> The execution context provided by Quartz. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		await using (_outbox.ConfigureAwait(false))
		{
			try
			{
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Starting execution of {JobGroup}:{JobName}.", jobGroup, jobName);
				}

				_ = await _outbox.RunOutboxDispatchAsync(DispatcherId, context.CancellationToken).ConfigureAwait(false);

				_heartbeatTracker.RecordHeartbeat(jobName);

				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Completed execution of {JobGroup}:{JobName}.", jobGroup, jobName);
				}
			}
#pragma warning disable CA1031 // Intentional: Quartz jobs must catch all exceptions to prevent immediate re-execution
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// Quartz best practices state that exceptions in jobs should not be rethrown as the job will subsequently process again
				// immediately and likely encounter the same exception. So swallow the exception and log the error to be investigated. If
				// this is an issue that does not resolve with time then the heartbeat will also never recover and alerts should be sent.
				if (_logger.IsEnabled(LogLevel.Error))
				{
					_logger.LogError(ex, "{Error} executing {JobGroup}:{JobName}: {Message}", ex.GetType().Name, jobGroup, jobName,
						ex.Message);
				}
			}
		}
	}
}
