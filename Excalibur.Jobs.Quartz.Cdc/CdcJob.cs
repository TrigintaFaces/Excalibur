using Excalibur.DataAccess.SqlServer;
using Excalibur.DataAccess.SqlServer.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using IJob = Quartz.IJob;

namespace Excalibur.Jobs.Quartz.Cdc;

/// <summary>
///     Represents a CDC (Change Data Capture) job that processes changes in configured databases.
/// </summary>
[DisallowConcurrentExecution]
public class CdcJob : IJob, IConfigurableJob<CdcJobConfig>
{
	private const string JobConfigSectionName = "Jobs:CdcJob";

	private readonly IConfiguration _configuration;

	private readonly IDataChangeEventProcessorFactory _factory;

	private readonly IOptions<CdcJobConfig> _cdcConfigOptions;

	private readonly ILogger<CdcJob> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="CdcJob" /> class.
	/// </summary>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="factory"> The factory for creating data change event processors. </param>
	/// <param name="cdcConfigOptions"> The CDC job configuration options. </param>
	/// <param name="logger"> The logger for logging job activities. </param>
	public CdcJob(
		IConfiguration configuration,
		IDataChangeEventProcessorFactory factory,
		IOptions<CdcJobConfig> cdcConfigOptions,
		ILogger<CdcJob> logger)
	{
		ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
		ArgumentNullException.ThrowIfNull(factory, nameof(factory));
		ArgumentNullException.ThrowIfNull(cdcConfigOptions, nameof(cdcConfigOptions));
		ArgumentNullException.ThrowIfNull(logger, nameof(logger));

		_configuration = configuration;
		_factory = factory;
		_cdcConfigOptions = cdcConfigOptions;
		_logger = logger;
	}

	/// <summary>
	///     Configures the job and its associated trigger in Quartz.
	/// </summary>
	/// <param name="configurator"> The Quartz configurator. </param>
	/// <param name="configuration"> The application configuration. </param>
	public static void ConfigureJob(IServiceCollectionQuartzConfigurator configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetJobConfiguration<CdcJobConfig>(JobConfigSectionName);
		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		_ = configurator.AddJob<CdcJob>(jobKey, (IJobConfigurator job) => job.WithIdentity(jobKey).WithDescription("CDC processing job"));

		_ = configurator.AddTrigger(
			(ITriggerConfigurator trigger) => trigger.ForJob(jobKey).WithIdentity($"{jobConfig.JobName}Trigger")
				.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(15))).WithCronSchedule(jobConfig.CronSchedule)
				.WithDescription("Trigger for CDC processing job"));
	}

	/// <summary>
	///     Configures health checks for the job.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder. </param>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="loggerFactory"> The logger factory for creating loggers. </param>
	public static void ConfigureHealthChecks(IHealthChecksBuilder healthChecks, IConfiguration configuration, ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(healthChecks);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		var jobConfig = configuration.GetJobConfiguration<CdcJobConfig>(JobConfigSectionName);
		var logger = loggerFactory.CreateLogger<JobHealthCheck>();

		_ = healthChecks.AddCheck($"{jobConfig.JobName}HealthCheck", new JobHealthCheck(jobConfig.JobName, jobConfig, logger));
	}

	/// <summary>
	///     Executes the CDC job, processing changes for each configured database.
	/// </summary>
	/// <param name="context"> The execution context provided by Quartz. </param>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;
		var jobConfig = _cdcConfigOptions.Value;

		using (_logger.BeginScope(new Dictionary<string, object> { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		{
			try
			{
				_logger.LogInformation("Starting execution of {JobGroup}:{JobName}.", jobGroup, jobName);

				var tasks = jobConfig.DatabaseConfigs.Select(
					(DatabaseConfig dbConfig) => ProcessCdcChangesAsync(dbConfig, context.CancellationToken));

				var results = await Task.WhenAll(tasks).ConfigureAwait(false);

				JobHealthCheck.Heartbeat(jobName);

				_logger.LogInformation(
					"Completed execution of {JobGroup}:{JobName}. Processed {TotalEvents} events.",
					jobGroup,
					jobName,
					results.Sum());
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

	private async Task<int> ProcessCdcChangesAsync(DatabaseConfig dbConfig, CancellationToken cancellationToken)
	{
		var cdcDb = _configuration.GetSqlDb(dbConfig.DatabaseConnectionIdentifier)();
		var stateStoreDb = _configuration.GetSqlDb(dbConfig.StateConnectionIdentifier)();

		var processor = _factory.Create(dbConfig, cdcDb, stateStoreDb);

		try
		{
			return await processor.ProcessCdcChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"{Error} executing ProcessCdcChangesAsync for {DatabaseName}: {Message}",
				ex.GetType().Name,
				dbConfig.DatabaseName,
				ex.Message);

			return 0;
		}
		finally
		{
			processor.Dispose();
		}
	}
}
