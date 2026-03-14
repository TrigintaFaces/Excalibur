// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.DataProcessing;
using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.DataProcessing;

/// <summary>
/// Represents a Quartz job for processing data tasks orchestrated by a <see cref="IDataOrchestrationManager" />.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class DataProcessingJob : IJob, IConfigurableJob<DataProcessingJobConfig>
{
	/// <summary>
	/// The configuration section name used to bind <see cref="DataProcessingJobConfig"/> from application configuration.
	/// </summary>
	/// <value>The string <c>"Jobs:DataProcessingJob"</c>.</value>
	public const string JobConfigSectionName = $"Jobs:{nameof(DataProcessingJob)}";

	private readonly IDataOrchestrationManager _dataOrchestrationManager;
	private readonly JobHeartbeatTracker _heartbeatTracker;
	private readonly ILogger<DataProcessingJob> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProcessingJob" /> class.
	/// </summary>
	/// <param name="dataOrchestrationManager"> The data orchestration manager responsible for processing tasks. </param>
	/// <param name="heartbeatTracker"> The heartbeat tracker for recording job activity. </param>
	/// <param name="logger"> The logger for logging job execution details. </param>
	public DataProcessingJob(
		IDataOrchestrationManager dataOrchestrationManager,
		JobHeartbeatTracker heartbeatTracker,
		ILogger<DataProcessingJob> logger)
	{
		ArgumentNullException.ThrowIfNull(dataOrchestrationManager);
		ArgumentNullException.ThrowIfNull(heartbeatTracker);
		ArgumentNullException.ThrowIfNull(logger);

		_dataOrchestrationManager = dataOrchestrationManager;
		_heartbeatTracker = heartbeatTracker;
		_logger = logger;
	}

	/// <summary>
	/// Configures the job and its trigger in Quartz using the specified configuration.
	/// </summary>
	/// <param name="configurator"> The Quartz configurator. </param>
	/// <param name="configuration"> The application configuration. </param>
	public static void ConfigureJob(IServiceCollectionQuartzConfigurator configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetJobConfiguration<DataProcessingJobConfig>(JobConfigSectionName);
		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		_ = configurator.AddJob<DataProcessingJob>(jobKey, job => job
			.WithIdentity(jobKey)
			.WithDescription("Process data tasks"));

		_ = configurator.AddTrigger(trigger => trigger
			.ForJob(jobKey)
			.WithIdentity($"{jobConfig.JobName}Trigger")
			.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(15)))
			.WithCronSchedule(jobConfig.CronSchedule)
			.WithDescription("A cron based trigger for data processing."));
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

		var jobConfig = configuration.GetJobConfiguration<DataProcessingJobConfig>(JobConfigSectionName);

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
	/// Executes the job, orchestrating data processing tasks.
	/// </summary>
	/// <param name="context"> The Quartz job execution context. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		{
			try
			{
				LogJobStarting(jobGroup, jobName);

				await _dataOrchestrationManager.ProcessDataTasksAsync(context.CancellationToken).ConfigureAwait(false);

				_heartbeatTracker.RecordHeartbeat(jobName);
				LogJobCompleted(jobGroup, jobName);
			}
#pragma warning disable CA1031 // Intentional: Quartz jobs must catch all exceptions to prevent immediate re-execution
			catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
			{
				// Graceful shutdown requested — propagate so Quartz respects the cancellation
				throw;
			}
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// Quartz best practices state that exceptions in jobs should not be rethrown as the job will subsequently process again
				// immediately and likely encounter the same exception. So swallow the exception and log the error to be investigated. If
				// this is an issue that does not resolve with time then the heartbeat will also never recover and alerts should be sent.
				LogJobError(ex.GetType().Name, jobGroup, jobName, ex.Message, ex);
			}
		}
	}

	// Source-generated logging methods
	[LoggerMessage(JobsEventId.DataProcessingJobStarting, LogLevel.Information,
		"Starting execution of {JobGroup}:{JobName}.")]
	private partial void LogJobStarting(string jobGroup, string jobName);

	[LoggerMessage(JobsEventId.DataProcessingJobCompleted, LogLevel.Information,
		"Completed execution of {JobGroup}:{JobName}.")]
	private partial void LogJobCompleted(string jobGroup, string jobName);

	[LoggerMessage(JobsEventId.DataProcessingJobError, LogLevel.Error,
		"{Error} executing {JobGroup}:{JobName}: {Message}")]
	private partial void LogJobError(string error, string jobGroup, string jobName, string message, Exception ex);
}
