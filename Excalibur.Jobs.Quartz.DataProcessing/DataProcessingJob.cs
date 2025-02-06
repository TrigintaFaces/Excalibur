using Excalibur.DataAccess.DataProcessing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Excalibur.Jobs.Quartz.DataProcessing;

/// <summary>
///     Represents a Quartz job for processing data tasks orchestrated by a <see cref="IDataOrchestrationManager" />.
/// </summary>
[DisallowConcurrentExecution]
public class DataProcessingJob : IJob, IConfigurableJob<DataProcessingJobConfig>
{
	private const string JobConfigSectionName = $"Jobs:{nameof(DataProcessingJob)}";
	private readonly IDataOrchestrationManager _dataOrchestrationManager;
	private readonly ILogger<DataProcessingJob> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataProcessingJob" /> class.
	/// </summary>
	/// <param name="dataOrchestrationManager"> The data orchestration manager responsible for processing tasks. </param>
	/// <param name="logger"> The logger for logging job execution details. </param>
	public DataProcessingJob(
		IDataOrchestrationManager dataOrchestrationManager,
		ILogger<DataProcessingJob> logger)
	{
		ArgumentNullException.ThrowIfNull(dataOrchestrationManager, nameof(dataOrchestrationManager));
		ArgumentNullException.ThrowIfNull(logger, nameof(logger));

		_dataOrchestrationManager = dataOrchestrationManager;
		_logger = logger;
	}

	/// <summary>
	///     Configures the job and its trigger in Quartz using the specified configuration.
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
	///     Configures health checks for the job.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder. </param>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="loggerFactory"> The logger factory for creating loggers. </param>
	public static void ConfigureHealthChecks(IHealthChecksBuilder healthChecks, IConfiguration configuration,
		ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		var jobConfig = configuration.GetJobConfiguration<DataProcessingJobConfig>(JobConfigSectionName);
		var logger = loggerFactory.CreateLogger<JobHealthCheck>();

		_ = healthChecks.AddCheck($"{jobConfig.JobName}HealthCheck", new JobHealthCheck(jobConfig.JobName, jobConfig, logger));
	}

	/// <summary>
	///     Executes the job, orchestrating data processing tasks.
	/// </summary>
	/// <param name="context"> The Quartz job execution context. </param>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object> { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		{
			try
			{
				_logger.LogInformation("Starting execution of {JobGroup}:{JobName}.", jobGroup, jobName);

				await _dataOrchestrationManager.ProcessDataTasks(context.CancellationToken).ConfigureAwait(false);

				JobHealthCheck.Heartbeat(jobName);

				_logger.LogInformation("Completed execution of {JobGroup}:{JobName}.", jobGroup, jobName);
			}
			catch (Exception ex)
			{
				// Quartz best practices state that exceptions in jobs should not be rethrown as the job will subsequently process again
				// immediately and likely encounter the same exception. So swallow the exception and log the error to be investigated. If
				// this is an issue that does not resolve with time then the heartbeat will also never recover and alerts should be sent.
				_logger.LogError(ex, "{Error} executing {JobGroup}:{JobName}: {Message}", ex.GetType().Name, jobGroup, jobName, ex.Message);
			}
		}
	}
}
