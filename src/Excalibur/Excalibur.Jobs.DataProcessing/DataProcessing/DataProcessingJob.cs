// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Data.DataProcessing;
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
public sealed partial class DataProcessingJob : IJob, IConfigurableJob<DataProcessingJobOptions>
{
	/// <summary>
	/// The configuration section name used to bind <see cref="DataProcessingJobOptions"/> from application configuration.
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
	public static void ConfigureJob(IQuartzBuilder configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		// The configuration-binding source generator does not intercept this call in this project,
		// so the reflection-based ConfigurationBinder.Get<T> is reached and the trim analyzer is
		// correct to flag it. Binding happens once at startup from a fixed section; the options type
		// is referenced directly here so the trimmer keeps it. Suppressed rather than hidden: the
		// member named below is the BCL one actually reached, not a wrapper of ours.
		#pragma warning disable IL2026, IL3050 // ConfigurationBinder.Get<T> - see note above
		var jobConfig = configuration.GetSection(JobConfigSectionName).Get<DataProcessingJobOptions>()
			?? throw new InvalidOperationException($"Job configuration not found at {JobConfigSectionName}.");
		#pragma warning restore IL2026, IL3050
		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		// A Disabled job is never registered with the scheduler, so no trigger ever fires.
		// Mirrors OutboxJob.ConfigureJob — keeps "Disabled" semantics consistent across built-in jobs.
		if (jobConfig.Disabled)
		{
			return;
		}

		_ = configurator.AddJob<DataProcessingJob>(job => job
			.WithIdentity(jobKey)
			.WithDescription("Process data tasks"));

		_ = configurator.AddTrigger(trigger => trigger
			.ForJob(jobKey)
			.WithIdentity($"{jobConfig.JobName}Trigger")
			.StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
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

		// The configuration-binding source generator does not intercept this call in this project,
		// so the reflection-based ConfigurationBinder.Get<T> is reached and the trim analyzer is
		// correct to flag it. Binding happens once at startup from a fixed section; the options type
		// is referenced directly here so the trimmer keeps it. Suppressed rather than hidden: the
		// member named below is the BCL one actually reached, not a wrapper of ours.
		#pragma warning disable IL2026, IL3050 // ConfigurationBinder.Get<T> - see note above
		var jobConfig = configuration.GetSection(JobConfigSectionName).Get<DataProcessingJobOptions>()
			?? throw new InvalidOperationException($"Job configuration not found at {JobConfigSectionName}.");
		#pragma warning restore IL2026, IL3050

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
	/// <param name="cancellationToken">The cancellation token for this firing.</param>
	/// <returns>The asynchronous execution.</returns>
	public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				LogJobStarting(jobGroup, jobName);

				await _dataOrchestrationManager.ProcessDataTasksAsync(cancellationToken).ConfigureAwait(false);

				cancellationToken.ThrowIfCancellationRequested();
				_heartbeatTracker.RecordHeartbeat(jobName);
				LogJobCompleted(jobGroup, jobName);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Graceful shutdown requested — propagate so Quartz respects the cancellation
				throw;
			}
			catch (Exception ex)
			{
				LogJobError(ex.GetType().Name, jobGroup, jobName, ex.Message, ex);
				cancellationToken.ThrowIfCancellationRequested();
				// Preserve the next scheduled firing while reporting this failure to listeners.
				throw new JobExecutionException(ex) { RefireImmediately = false };
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
	/// <summary>Executes using the cancellation token supplied by the execution context.</summary>
	/// <param name="context">The Quartz execution context.</param>
	/// <returns>The asynchronous execution.</returns>
	public Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return Execute(context, context.CancellationToken).AsTask();
	}
}
