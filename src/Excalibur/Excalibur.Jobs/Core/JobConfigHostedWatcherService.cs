// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Monitors configuration changes for a job and updates its state in the scheduler accordingly.
/// </summary>
/// <typeparam name="TJob"> The type of the job being monitored. </typeparam>
/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="JobConfigHostedWatcherService{TJob, TConfig}" /> class. </remarks>
/// <param name="scheduler"> The scheduler responsible for managing job execution. </param>
/// <param name="configMonitor"> Monitors changes to the job configuration. </param>
/// <param name="logger"> The logger for logging information and errors. </param>
public sealed partial class JobConfigHostedWatcherService<TJob, TConfig>(
	IScheduler? scheduler,
	IOptionsMonitor<TConfig> configMonitor,
	ILogger<JobConfigHostedWatcherService<TJob, TConfig>> logger) : IJobConfigHostedWatcherService
	where TJob : IConfigurableJob<TConfig>
	where TConfig : class, IJobConfig
{
	private readonly CancellationTokenSource _stoppingCts = new();
	private volatile bool _disposed;
	private IDisposable? _changeListener;

	/// <summary>
	/// Starts monitoring the job configuration and updates the scheduler when changes occur.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			var initialConfig = configMonitor.CurrentValue;
			var jobKey = new JobKey(initialConfig.JobName, initialConfig.JobGroup);

			LogStartingJobWatcherService(jobKey);
			LogInitialConfigurationLoaded(initialConfig.Disabled);

			await UpdateJobStateAsync(jobKey, initialConfig, cancellationToken).ConfigureAwait(false);

			_changeListener = configMonitor.OnChange(async newConfig =>
			{
				try
				{
					LogConfigurationChangeDetected(jobKey);

					await UpdateJobStateAsync(jobKey, newConfig, _stoppingCts.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogErrorHandlingConfigurationChange(jobKey, ex);
					throw;
				}
			});

			LogJobWatcherServiceStartedSuccessfully(jobKey);
		}
		catch (Exception ex)
		{
			LogErrorStartingJobWatcherService(ex);
			throw;
		}
	}

	/// <summary>
	/// Stops the service, cleaning up resources.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	/// <returns> A task that represents the asynchronous stop operation. </returns>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			var initialConfig = configMonitor.CurrentValue;
			var jobKey = new JobKey(initialConfig.JobName, initialConfig.JobGroup);

			LogStoppingJobWatcherService(jobKey);

			// Cancel any pending OnChange callbacks before disposing the listener
			await _stoppingCts.CancelAsync().ConfigureAwait(false);

			// Dispose of the configuration change listener
			_changeListener?.Dispose();

			LogJobWatcherServiceStoppedSuccessfully(jobKey);
		}
		catch (Exception ex)
		{
			LogErrorStoppingJobWatcherService(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of the resources used by the policy.
	/// </summary>
	/// <param name="disposing"> Indicates whether the method is being called from the Dispose method. </param>
	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_changeListener?.Dispose();
			_stoppingCts.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Updates the state of the job in the scheduler based on the provided configuration.
	/// </summary>
	/// <param name="jobKey"> The key identifying the job in the scheduler. </param>
	/// <param name="newConfig"> The updated job configuration. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	private async Task UpdateJobStateAsync(JobKey? jobKey, TConfig newConfig, CancellationToken cancellationToken)
	{
		if (scheduler == null || jobKey == null)
		{
			return;
		}

		if (newConfig.Disabled)
		{
			LogPausingJob(jobKey);
			await scheduler.PauseJob(jobKey, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			LogResumingJob(jobKey);
			await scheduler.ResumeJob(jobKey, cancellationToken).ConfigureAwait(false);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(JobsEventId.StartingJobWatcherService, LogLevel.Information,
		"Starting the job watcher service for {JobKey}.")]
	private partial void LogStartingJobWatcherService(JobKey jobKey);

	[LoggerMessage(JobsEventId.InitialConfigurationLoaded, LogLevel.Information,
		"Initial configuration loaded: Disabled: {Disabled}")]
	private partial void LogInitialConfigurationLoaded(bool disabled);

	[LoggerMessage(JobsEventId.ConfigurationChangeDetected, LogLevel.Information,
		"Configuration change detected for {JobKey}. Updating job state.")]
	private partial void LogConfigurationChangeDetected(JobKey jobKey);

	[LoggerMessage(JobsEventId.ErrorHandlingConfigurationChange, LogLevel.Error,
		"An error occurred while handling configuration change for {JobKey}.")]
	private partial void LogErrorHandlingConfigurationChange(JobKey jobKey, Exception ex);

	[LoggerMessage(JobsEventId.JobWatcherServiceStartedSuccessfully, LogLevel.Information,
		"Job watcher service for {JobKey} started successfully.")]
	private partial void LogJobWatcherServiceStartedSuccessfully(JobKey jobKey);

	[LoggerMessage(JobsEventId.ErrorStartingJobWatcherService, LogLevel.Error,
		"An error occurred while starting the job watcher service.")]
	private partial void LogErrorStartingJobWatcherService(Exception ex);

	[LoggerMessage(JobsEventId.StoppingJobWatcherService, LogLevel.Information,
		"Stopping the job watcher service for {JobKey}.")]
	private partial void LogStoppingJobWatcherService(JobKey jobKey);

	[LoggerMessage(JobsEventId.JobWatcherServiceStoppedSuccessfully, LogLevel.Information,
		"Job watcher service for {JobKey} stopped successfully.")]
	private partial void LogJobWatcherServiceStoppedSuccessfully(JobKey jobKey);

	[LoggerMessage(JobsEventId.ErrorStoppingJobWatcherService, LogLevel.Error,
		"An error occurred while stopping the job watcher service.")]
	private partial void LogErrorStoppingJobWatcherService(Exception ex);

	[LoggerMessage(JobsEventId.PausingJob, LogLevel.Information,
		"Pausing job {JobKey} due to configuration change.")]
	private partial void LogPausingJob(JobKey jobKey);

	[LoggerMessage(JobsEventId.ResumingJob, LogLevel.Information,
		"Resuming job {JobKey} due to configuration change.")]
	private partial void LogResumingJob(JobKey jobKey);
}
