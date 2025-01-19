using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs;

/// <summary>
///     Monitors configuration changes for a job and updates its state in the scheduler accordingly.
/// </summary>
/// <typeparam name="TJob"> The type of the job being monitored. </typeparam>
/// <typeparam name="TConfig"> The type of the job configuration. </typeparam>
public sealed class JobConfigHostedWatcherService<TJob, TConfig> : IJobConfigHostedWatcherService
	where TJob : IConfigurableJob<TConfig>
	where TConfig : class, IJobConfig
{
	private readonly IScheduler? _scheduler;
	private readonly IOptionsMonitor<TConfig> _configMonitor;
	private readonly ILogger<JobConfigHostedWatcherService<TJob, TConfig>> _logger;
	private bool _disposed;
	private IDisposable? _changeListener;

	/// <summary>
	///     Initializes a new instance of the <see cref="JobConfigHostedWatcherService{TJob, TConfig}" /> class.
	/// </summary>
	/// <param name="scheduler"> The scheduler responsible for managing job execution. </param>
	/// <param name="configMonitor"> Monitors changes to the job configuration. </param>
	/// <param name="logger"> The logger for logging information and errors. </param>
	public JobConfigHostedWatcherService(
		IScheduler? scheduler,
		IOptionsMonitor<TConfig> configMonitor,
		ILogger<JobConfigHostedWatcherService<TJob, TConfig>> logger)
	{
		_scheduler = scheduler;
		_configMonitor = configMonitor;
		_logger = logger;
	}

	/// <summary>
	///     Starts monitoring the job configuration and updates the scheduler when changes occur.
	/// </summary>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			var initialConfig = _configMonitor.CurrentValue;
			var jobKey = new JobKey(initialConfig.JobName, initialConfig.JobGroup);

			_logger.LogInformation("Starting the job watcher service for {JobKey}.", jobKey);
			_logger.LogInformation("Initial configuration loaded: Disabled: {Disabled}", initialConfig.Disabled);

			await UpdateJobState(jobKey, initialConfig, cancellationToken).ConfigureAwait(false);

			_changeListener = _configMonitor.OnChange(async newConfig =>
			{
				try
				{
					_logger.LogInformation("Configuration change detected for {JobKey}. Updating job state.", jobKey);

					await UpdateJobState(jobKey, newConfig, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "An error occurred while handling configuration change for {JobKey}.", jobKey);
					throw;
				}
			});

			_logger.LogInformation("Job watcher service for {JobKey} started successfully.", jobKey);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while starting the job watcher service.");
			throw;
		}
	}

	/// <summary>
	///     Stops the service, cleaning up resources.
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
			var initialConfig = _configMonitor.CurrentValue;
			var jobKey = new JobKey(initialConfig.JobName, initialConfig.JobGroup);

			_logger.LogInformation("Stopping the job watcher service for {JobKey}.", jobKey);

			// Dispose of the configuration change listener
			_changeListener?.Dispose();

			_logger.LogInformation("Job watcher service for {JobKey} stopped successfully.", jobKey);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while stopping the job watcher service.");
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
	///     Disposes of the resources used by the policy.
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
		}

		_disposed = true;
	}

	/// <summary>
	///     Updates the state of the job in the scheduler based on the provided configuration.
	/// </summary>
	/// <param name="jobKey"> The key identifying the job in the scheduler. </param>
	/// <param name="newConfig"> The updated job configuration. </param>
	/// <param name="cancellationToken"> A token to observe while waiting for the task to complete. </param>
	private async Task UpdateJobState(JobKey? jobKey, TConfig newConfig, CancellationToken cancellationToken)
	{
		if (_scheduler == null || jobKey == null)
		{
			return;
		}

		if (newConfig.Disabled)
		{
			_logger.LogInformation("Pausing job {JobKey} due to configuration change.", jobKey);
			await _scheduler.PauseJob(jobKey, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			_logger.LogInformation("Resuming job {JobKey} due to configuration change.", jobKey);
			await _scheduler.ResumeJob(jobKey, cancellationToken).ConfigureAwait(false);
		}
	}
}
