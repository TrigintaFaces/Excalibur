using System.Collections.Concurrent;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs;

/// <summary>
///     Represents a health check for a job, monitoring its heartbeat and evaluating its health state based on configured thresholds.
/// </summary>
public class JobHealthCheck : IHealthCheck
{
	private static readonly ConcurrentDictionary<string, long> LastHeartbeats = new();
	private readonly string _jobName;
	private readonly JobConfig _jobConfig;
	private readonly ILogger<JobHealthCheck> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="JobHealthCheck" /> class.
	/// </summary>
	/// <param name="jobName"> The name of the job being monitored. </param>
	/// <param name="jobConfig"> The configuration for the job, including thresholds for health evaluation. </param>
	/// <param name="logger"> Logger for logging health check details and issues. </param>
	public JobHealthCheck(string jobName, JobConfig jobConfig, ILogger<JobHealthCheck> logger)
	{
		_jobName = jobName;
		_jobConfig = jobConfig;
		_logger = logger;
	}

	/// <summary>
	///     Records a heartbeat for a specific job.
	/// </summary>
	/// <param name="jobName"> The name of the job for which the heartbeat is recorded. </param>
	public static void Heartbeat(string jobName) => LastHeartbeats[jobName] = DateTime.UtcNow.ToBinary();

	/// <summary>
	///     Retrieves the last recorded heartbeat timestamp for a specific job.
	/// </summary>
	/// <param name="jobName"> The name of the job. </param>
	/// <returns> The <see cref="DateTime" /> of the last heartbeat, or <see cref="DateTime.MinValue" /> if no heartbeat exists. </returns>
	public static DateTime GetLastHeartbeat(string jobName) => LastHeartbeats.TryGetValue(jobName, out var binaryDate)
		? DateTime.FromBinary(binaryDate)
		: DateTime.MinValue;

	/// <summary>
	///     Performs the health check for the job, evaluating its health state based on its heartbeat and configured thresholds.
	/// </summary>
	/// <param name="context"> The context for the health check. </param>
	/// <param name="cancellationToken"> A token to observe while performing the check. </param>
	/// <returns>
	///     A task that completes with a <see cref="HealthCheckResult" /> indicating the health state of the job:
	///     - Healthy if within the configured thresholds.
	///     - Degraded if it exceeds the degraded threshold but not the unhealthy threshold.
	///     - Unhealthy if it exceeds the unhealthy threshold.
	/// </returns>
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		if (_jobConfig.Disabled)
		{
			_logger.LogInformation("Health check for job {jobName} is disabled.", _jobName);

			return Task.FromResult(HealthCheckResult.Healthy($"Job {_jobName} is disabled."));
		}

		var lastHeartbeat = GetLastHeartbeat(_jobName);
		var timeSinceLastHeartbeat = DateTime.UtcNow - lastHeartbeat;

		if (timeSinceLastHeartbeat > _jobConfig.UnhealthyThreshold)
		{
			_logger.LogError("Job {jobName} is unhealthy. Last heartbeat was {timeSinceLastHeartbeat} ago.", _jobName,
				timeSinceLastHeartbeat);
			return Task.FromResult(
				HealthCheckResult.Unhealthy($"Job {_jobName} is unhealthy. Last heartbeat was {timeSinceLastHeartbeat} ago."));
		}

		if (timeSinceLastHeartbeat > _jobConfig.DegradedThreshold)
		{
			_logger.LogWarning("Job {jobName} is degraded. Last heartbeat was {timeSinceLastHeartbeat} ago.", _jobName,
				timeSinceLastHeartbeat);
			return Task.FromResult(
				HealthCheckResult.Degraded($"Job {_jobName} is degraded. Last heartbeat was {timeSinceLastHeartbeat} ago."));
		}

		_logger.LogInformation("Job {jobName} is healthy. Last heartbeat was {timeSinceLastHeartbeat} ago.", _jobName,
			timeSinceLastHeartbeat);
		return Task.FromResult(
			HealthCheckResult.Healthy($"Job {_jobName} is healthy. Last heartbeat was {timeSinceLastHeartbeat} ago."));
	}
}
