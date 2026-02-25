// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

using Quartz;

namespace CdcEventStoreElasticsearch.Infrastructure;

/// <summary>
/// Quartz job for processing CDC (Change Data Capture) events.
/// This is an alternative to <see cref="CdcPollingBackgroundService"/> for scheduled execution.
/// </summary>
/// <remarks>
/// <para>
/// Key differences from the background service:
/// </para>
/// <list type="bullet">
/// <item><see cref="DisallowConcurrentExecutionAttribute"/> prevents overlapping executions</item>
/// <item>Quartz handles scheduling, retry, and persistence of job state</item>
/// <item>Better suited for serverless or shared-host environments</item>
/// <item>Integrates with Quartz's monitoring and management features</item>
/// </list>
/// </remarks>
[DisallowConcurrentExecution]
public sealed partial class CdcSampleJob : IJob
{
	private readonly IDataChangeEventProcessorFactory _processorFactory;
	private readonly IDatabaseConfig _dbConfig;
	private readonly CdcSampleJobConfig _options;
	private readonly ILogger<CdcSampleJob> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcSampleJob"/> class.
	/// </summary>
	public CdcSampleJob(
		IDataChangeEventProcessorFactory processorFactory,
		IDatabaseConfig dbConfig,
		IOptions<CdcSampleJobConfig> options,
		ILogger<CdcSampleJob> logger)
	{
		_processorFactory = processorFactory;
		_dbConfig = dbConfig;
		_options = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Configures the job and its associated trigger in Quartz.
	/// </summary>
	/// <param name="configurator">The Quartz configurator.</param>
	/// <param name="configuration">The application configuration.</param>
	public static void ConfigureJob(IServiceCollectionQuartzConfigurator configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetSection(CdcSampleJobConfig.SectionName).Get<CdcSampleJobConfig>()
						?? new CdcSampleJobConfig();

		if (!jobConfig.Enabled)
		{
			return;
		}

		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		_ = configurator.AddJob<CdcSampleJob>(jobKey, job => job
			.WithIdentity(jobKey)
			.WithDescription("CDC sample processing job - processes LegacyCustomers, LegacyOrders, and LegacyOrderItems"));

		_ = configurator.AddTrigger(trigger => trigger
			.ForJob(jobKey)
			.WithIdentity($"{jobConfig.JobName}Trigger")
			.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(5)))
			.WithCronSchedule(jobConfig.CronSchedule)
			.WithDescription("Trigger for CDC sample processing job"));
	}

	/// <summary>
	/// Executes the CDC job, processing changes from the legacy database.
	/// </summary>
	/// <param name="context">The execution context provided by Quartz.</param>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;

		using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["JobGroup"] = jobGroup,
			["JobName"] = jobName
		}))
		{
			try
			{
				LogJobStarting(jobGroup, jobName);

				var processedCount = await ProcessCdcChangesAsync(context.CancellationToken)
					.ConfigureAwait(false);

				LogJobCompleted(jobGroup, jobName, processedCount);
			}
			catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
			{
				LogJobCancelled(jobGroup, jobName);
			}
			catch (CdcStalePositionException ex)
			{
				// Stale position detected - the recovery strategy configured in
				// CdcRecoveryOptions will be applied automatically
				LogStalePositionWarning(jobGroup, jobName, ex.Message);
			}
			catch (CdcMissingTableHandlerException ex)
			{
				// No handler registered for a table
				LogMissingHandlerWarning(jobGroup, jobName, ex.Message);
			}
			catch (Exception ex)
			{
				// Quartz best practices: don't rethrow exceptions
				// The job will retry on next schedule
				LogJobError(jobGroup, jobName, ex.GetType().Name, ex.Message, ex);
			}
		}
	}

	private async Task<int> ProcessCdcChangesAsync(CancellationToken cancellationToken)
	{
		await using var cdcConnection = new SqlConnection(_options.CdcSourceConnectionString);
		await using var stateConnection = new SqlConnection(_options.StateStoreConnectionString);

		await cdcConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await stateConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var processor = _processorFactory.Create(_dbConfig, cdcConnection, stateConnection);

		return await processor.ProcessCdcChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(1001, LogLevel.Information, "Starting execution of {JobGroup}:{JobName}")]
	private partial void LogJobStarting(string jobGroup, string jobName);

	[LoggerMessage(1002, LogLevel.Information, "Completed execution of {JobGroup}:{JobName}. Processed {Count} CDC events.")]
	private partial void LogJobCompleted(string jobGroup, string jobName, int count);

	[LoggerMessage(1003, LogLevel.Information, "Execution of {JobGroup}:{JobName} was cancelled")]
	private partial void LogJobCancelled(string jobGroup, string jobName);

	[LoggerMessage(1004, LogLevel.Warning, "CDC stale position detected in {JobGroup}:{JobName}: {Message}")]
	private partial void LogStalePositionWarning(string jobGroup, string jobName, string message);

	[LoggerMessage(1005, LogLevel.Warning, "Missing table handler in {JobGroup}:{JobName}: {Message}")]
	private partial void LogMissingHandlerWarning(string jobGroup, string jobName, string message);

	[LoggerMessage(1006, LogLevel.Error, "Error in {JobGroup}:{JobName} - {ErrorType}: {Message}")]
	private partial void LogJobError(string jobGroup, string jobName, string errorType, string message, Exception ex);
}
