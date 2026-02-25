// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Jobs.Jobs;

/// <summary>
/// Background job that performs periodic health checks.
/// </summary>
public sealed class HealthCheckJob(
	HealthCheckService healthCheckService,
	ILogger<HealthCheckJob> logger)
	: IBackgroundJob
{
	private readonly HealthCheckService _healthCheckService =
		healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));

	private readonly ILogger<HealthCheckJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		HealthCheckJobLog.JobStarting(_logger);

		try
		{
			var report = await _healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

			HealthCheckJobLog.JobCompleted(_logger, report.Status, report.TotalDuration.TotalMilliseconds);

			// Log details for unhealthy checks
			if (report.Status != HealthStatus.Healthy)
			{
				var unhealthyChecks = report.Entries
					.Where(static e => e.Value.Status != HealthStatus.Healthy)
					.ToList();

				foreach (var entry in unhealthyChecks)
				{
					HealthCheckJobLog.HealthCheckWarning(_logger, entry.Key, entry.Value.Status,
						entry.Value.Description ?? "No description");

					if (entry.Value.Exception != null)
					{
						HealthCheckJobLog.HealthCheckError(_logger, entry.Value.Exception, entry.Key);
					}
				}
			}

			// Log metrics for all checks
			foreach (var entry in report.Entries)
			{
				if (entry.Value.Data?.Any() == true)
				{
					foreach (var data in entry.Value.Data)
					{
						HealthCheckJobLog.HealthCheckData(_logger, entry.Key, data.Key, data.Value);
					}
				}
			}
		}
		catch (Exception ex)
		{
			HealthCheckJobLog.JobFailed(_logger, ex);
			throw;
		}
	}
}
