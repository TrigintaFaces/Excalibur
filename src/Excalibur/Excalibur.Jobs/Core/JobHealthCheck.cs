// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Health check implementation for jobs.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="JobHealthCheck" /> class. </remarks>
/// <param name="jobName"> The job name. </param>
/// <param name="config"> The job configuration. </param>
/// <param name="heartbeatTracker"> The heartbeat tracker. </param>
public sealed class JobHealthCheck(string jobName, JobConfig config, JobHeartbeatTracker heartbeatTracker) : IHealthCheck
{
	private readonly string _jobName = jobName;
	private readonly JobConfig _config = config ?? throw new ArgumentNullException(nameof(config));
	private readonly JobHeartbeatTracker _heartbeatTracker = heartbeatTracker ?? throw new ArgumentNullException(nameof(heartbeatTracker));

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
	{
		var lastHeartbeat = _heartbeatTracker.GetLastHeartbeat(_jobName);

		if (lastHeartbeat.HasValue)
		{
			var timeSinceHeartbeat = DateTimeOffset.UtcNow - lastHeartbeat.Value;
			if (timeSinceHeartbeat < _config.DegradedThreshold)
			{
				return Task.FromResult(HealthCheckResult.Healthy($"Job {_jobName} is healthy. Last heartbeat: {lastHeartbeat.Value}"));
			}

			if (timeSinceHeartbeat < _config.UnhealthyThreshold)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"Job {_jobName} is degraded. Last heartbeat: {lastHeartbeat.Value} ({timeSinceHeartbeat.TotalMinutes:F1} minutes ago)"));
			}
		}

		return Task.FromResult(HealthCheckResult.Unhealthy($"Job {_jobName} has not reported a heartbeat recently."));
	}
}
