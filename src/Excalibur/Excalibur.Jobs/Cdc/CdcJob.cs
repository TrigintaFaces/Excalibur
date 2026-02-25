// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using IJob = Quartz.IJob;

namespace Excalibur.Jobs.Cdc;

/// <summary>
/// Represents a CDC (Change Data Capture) job that processes changes in configured databases.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class CdcJob : IJob, IConfigurableJob<CdcJobConfig>
{
	/// <summary>
	/// The configuration section name used to bind <see cref="CdcJobConfig"/> from application configuration.
	/// </summary>
	/// <value>The string <c>"Jobs:CdcJob"</c>.</value>
	public const string JobConfigSectionName = "Jobs:CdcJob";

	private readonly IDataChangeEventProcessorFactory _factory;
	private readonly Func<string, SqlConnection> _connectionFactory;
	private readonly IOptions<CdcJobConfig> _cdcConfigOptions;
	private readonly JobHeartbeatTracker _heartbeatTracker;
	private readonly ILogger<CdcJob> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcJob" /> class.
	/// </summary>
	/// <param name="factory"> The factory for creating data change event processors. </param>
	/// <param name="connectionFactory">
	/// A factory function that creates SQL connections from connection string names.
	/// Follows the S547 <c>Func&lt;SqlConnection&gt;</c> factory pattern — connection creation
	/// is deferred to the factory, not performed via <c>new SqlConnection(connectionString)</c>.
	/// Register in DI with <c>services.AddSingleton&lt;Func&lt;string, SqlConnection&gt;&gt;(...)</c>.
	/// </param>
	/// <param name="cdcConfigOptions"> The CDC job configuration options. </param>
	/// <param name="heartbeatTracker"> The heartbeat tracker for recording job activity. </param>
	/// <param name="logger"> The logger for logging job activities. </param>
	public CdcJob(
		IDataChangeEventProcessorFactory factory,
		Func<string, SqlConnection> connectionFactory,
		IOptions<CdcJobConfig> cdcConfigOptions,
		JobHeartbeatTracker heartbeatTracker,
		ILogger<CdcJob> logger)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(cdcConfigOptions);
		ArgumentNullException.ThrowIfNull(heartbeatTracker);
		ArgumentNullException.ThrowIfNull(logger);

		_factory = factory;
		_connectionFactory = connectionFactory;
		_cdcConfigOptions = cdcConfigOptions;
		_heartbeatTracker = heartbeatTracker;
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcJob" /> class using <see cref="IConfiguration"/>
	/// to resolve connection strings by name, following the S547 DI factory pattern.
	/// </summary>
	/// <param name="factory"> The factory for creating data change event processors. </param>
	/// <param name="configuration"> The application configuration for resolving connection strings. </param>
	/// <param name="cdcConfigOptions"> The CDC job configuration options. </param>
	/// <param name="heartbeatTracker"> The heartbeat tracker for recording job activity. </param>
	/// <param name="logger"> The logger for logging job activities. </param>
	/// <remarks>
	/// This constructor resolves connection strings from <see cref="IConfiguration"/> by name,
	/// creating <see cref="SqlConnection"/> instances via a factory function instead of
	/// <c>new SqlConnection(connectionString)</c> directly. Prefer this constructor when
	/// <c>Func&lt;string, SqlConnection&gt;</c> is not explicitly registered in DI.
	/// </remarks>
	public CdcJob(
		IDataChangeEventProcessorFactory factory,
		IConfiguration configuration,
		IOptions<CdcJobConfig> cdcConfigOptions,
		JobHeartbeatTracker heartbeatTracker,
		ILogger<CdcJob> logger)
		: this(
			factory,
			CreateConnectionFactory(configuration),
			cdcConfigOptions,
			heartbeatTracker,
			logger)
	{
	}

	private static Func<string, SqlConnection> CreateConnectionFactory(IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		return connectionName =>
		{
			var connectionString = configuration.GetConnectionString(connectionName);

			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new InvalidOperationException(
					$"Connection string '{connectionName}' not found in configuration. " +
					"Ensure it is registered in the ConnectionStrings section.");
			}

			return new SqlConnection(connectionString);
		};
	}

	/// <summary>
	/// Configures the job and its associated trigger in Quartz.
	/// </summary>
	/// <param name="configurator"> The Quartz configurator. </param>
	/// <param name="configuration"> The application configuration. </param>
	public static void ConfigureJob(IServiceCollectionQuartzConfigurator configurator, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configurator);
		ArgumentNullException.ThrowIfNull(configuration);

		var jobConfig = configuration.GetJobConfiguration<CdcJobConfig>(JobConfigSectionName);
		var jobKey = new JobKey(jobConfig.JobName, jobConfig.JobGroup);

		_ = configurator.AddJob<CdcJob>(jobKey, job => job.WithIdentity(jobKey).WithDescription("CDC processing job"));

		_ = configurator.AddTrigger(trigger => trigger.ForJob(jobKey).WithIdentity($"{jobConfig.JobName}Trigger")
			.StartAt(DateBuilder.EvenSecondDate(DateTimeOffset.UtcNow.AddSeconds(15))).WithCronSchedule(jobConfig.CronSchedule)
			.WithDescription("Trigger for CDC processing job"));
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

		var jobConfig = configuration.GetJobConfiguration<CdcJobConfig>(JobConfigSectionName);

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
	/// Executes the CDC job, processing changes for each configured database.
	/// </summary>
	/// <param name="context"> The execution context provided by Quartz. </param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task Execute(IJobExecutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var jobName = context.JobDetail.Key.Name;
		var jobGroup = context.JobDetail.Key.Group;
		var jobConfig = _cdcConfigOptions.Value;

		using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobGroup"] = jobGroup, ["JobName"] = jobName }))
		{
			try
			{
				LogJobStarting(jobGroup, jobName);

				var tasks = jobConfig.DatabaseConfigs
					.Distinct()
					.Select(dbConfig => ProcessCdcChangesAsync(dbConfig, context.CancellationToken));

				var results = await Task.WhenAll(tasks).ConfigureAwait(false);

				_heartbeatTracker.RecordHeartbeat(jobName);

				LogJobCompleted(jobGroup, jobName, results.Sum());
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

	private async Task<int> ProcessCdcChangesAsync(DatabaseConfig dbConfig, CancellationToken cancellationToken)
	{
		var cdcConnection = _connectionFactory(dbConfig.DatabaseConnectionIdentifier);
		var storeConnection = _connectionFactory(dbConfig.StateConnectionIdentifier);

		var processor = _factory.Create(dbConfig, cdcConnection, storeConnection);

		try
		{
			return await processor.ProcessCdcChangesAsync(cancellationToken).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Intentional: CDC processor errors should not crash the job - log and return 0 processed
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogCdcProcessingError(ex.GetType().Name, dbConfig.DatabaseName, ex.Message, ex);
			return 0;
		}
		finally
		{
			await processor.DisposeAsync().ConfigureAwait(false);
			await cdcConnection.DisposeAsync().ConfigureAwait(false);
			await storeConnection.DisposeAsync().ConfigureAwait(false);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(JobsEventId.CdcJobStarting, LogLevel.Information,
		"Starting execution of {JobGroup}:{JobName}.")]
	private partial void LogJobStarting(string jobGroup, string jobName);

	[LoggerMessage(JobsEventId.CdcJobCompleted, LogLevel.Information,
		"Completed execution of {JobGroup}:{JobName}. Processed {TotalEvents} events.")]
	private partial void LogJobCompleted(string jobGroup, string jobName, int totalEvents);

	[LoggerMessage(JobsEventId.CdcJobError, LogLevel.Error,
		"{Error} executing {JobGroup}:{JobName}: {Message}")]
	private partial void LogJobError(string error, string jobGroup, string jobName, string message, Exception ex);

	[LoggerMessage(JobsEventId.CdcProcessingError, LogLevel.Error,
		"{Error} executing ProcessCdcChangesAsync for {DatabaseName}: {Message}")]
	private partial void LogCdcProcessingError(string error, string databaseName, string message, Exception ex);
}
