// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Background service for security monitoring operations.
/// </summary>
internal sealed partial class SecurityMonitoringBackgroundService(
	IElasticsearchSecurityMonitor securityMonitor,
	IOptions<SecurityMonitoringOptions> options,
	ILogger<SecurityMonitoringBackgroundService> logger)
	: BackgroundService
{
	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var settings = options.Value;

		if (!settings.Enabled)
		{
			LogDisabled();
			return;
		}

		LogStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				_ = await securityMonitor.ProcessSecurityAlertsAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(settings.MonitoringInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogStopped();
	}
}
