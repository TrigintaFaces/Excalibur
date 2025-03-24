using Excalibur.Core.Extensions;
using Excalibur.Data.Outbox;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Quartz;

using IJob = Quartz.IJob;

namespace Excalibur.Jobs.Quartz.Outbox;

/// <summary>
///     Represents a Quartz job responsible for dispatching outbox messages.
/// </summary>
/// <remarks>
///     This job reads messages from the outbox, deserializes them, and dispatches them using the provided dispatcher. The job configuration
///     is defined in the application configuration under "Jobs:OutboxJob".
/// </remarks>
[DisallowConcurrentExecution]
public class OutboxJob : IJob, IConfigurableJob<OutboxJobConfig>
{
	public const string JobConfigSectionName = $"Jobs:{nameof(OutboxJob)}";

	private static readonly string DispatcherId = Uuid7Extensions.GenerateString();

	private readonly IOutboxManager _outboxManager;

	private readonly ILogger<OutboxJob> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="OutboxJob" /> class.
	/// </summary>
	/// <param name="outboxManager"> The manager responsible for handling outbox operations. </param>
	/// <param name="logger"> The logger for logging job activities. </param>
	public OutboxJob(IOutboxManager outboxManager, ILogger<OutboxJob> logger)
	{
		ArgumentNullException.ThrowIfNull(outboxManager, nameof(outboxManager));
		ArgumentNullException.ThrowIfNull(logger, nameof(logger));

		_outboxManager = outboxManager;
		_logger = logger;
	}

	/// <summary>
	///     Configures the Quartz job and its associated trigger.
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
			(IJobConfigurator job) => job.WithIdentity(jobKey).WithDescription("Dispatch outbox messages job"));

		_ = configurator.AddTrigger(
			(ITriggerConfigurator trigger) => trigger.ForJob(jobKey).WithIdentity($"{jobConfig.JobName}Trigger")
				.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(15))).WithCronSchedule(jobConfig.CronSchedule)
				.WithDescription("A cron based trigger for the dispatch of outbox messages"));
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

		var jobConfig = configuration.GetJobConfiguration<OutboxJobConfig>(JobConfigSectionName);
		var logger = loggerFactory.CreateLogger<JobHealthCheck>();

		_ = healthChecks.AddCheck($"{jobConfig.JobName}HealthCheck", new JobHealthCheck(jobConfig.JobName, jobConfig, logger));
	}

	/// <summary>
	///     Executes the job, processing outbox messages.
	/// </summary>
	/// <param name="context"> The execution context provided by Quartz. </param>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object> { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		await using (_outboxManager.ConfigureAwait(false))
		{
			try
			{
				_logger.LogInformation("Starting execution of {JobGroup}:{JobName}.", jobGroup, jobName);

				_ = await _outboxManager.RunOutboxDispatchAsync(DispatcherId).ConfigureAwait(false);

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
