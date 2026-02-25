// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Background service that periodically enforces retention policies by scanning for
/// and cleaning up expired personal data.
/// </summary>
/// <remarks>
/// <para>
/// This service polls at the configured interval and delegates enforcement to
/// <see cref="IRetentionEnforcementService"/>. It follows the same pattern as
/// <see cref="ErasureSchedulerBackgroundService"/>.
/// </para>
/// </remarks>
public sealed partial class RetentionEnforcementBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<RetentionEnforcementOptions> _options;
	private readonly ILogger<RetentionEnforcementBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetentionEnforcementBackgroundService"/> class.
	/// </summary>
	/// <param name="scopeFactory">The service scope factory.</param>
	/// <param name="options">The retention enforcement options.</param>
	/// <param name="logger">The logger.</param>
	public RetentionEnforcementBackgroundService(
		IServiceScopeFactory scopeFactory,
		IOptions<RetentionEnforcementOptions> options,
		ILogger<RetentionEnforcementBackgroundService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Value.Enabled)
		{
			LogRetentionEnforcementDisabled();
			return;
		}

		LogRetentionEnforcementServiceStarting(_options.Value.ScanInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await using var scope = _scopeFactory.CreateAsyncScope();
				var service = scope.ServiceProvider.GetRequiredService<IRetentionEnforcementService>();
				await service.EnforceRetentionAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogRetentionEnforcementProcessingError(ex);
			}

			try
			{
				await Task.Delay(_options.Value.ScanInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogRetentionEnforcementServiceStopped();
	}

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementDisabled,
		LogLevel.Information,
		"Retention enforcement background service is disabled")]
	private partial void LogRetentionEnforcementDisabled();

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementServiceStarting,
		LogLevel.Information,
		"Retention enforcement background service starting with scan interval {ScanInterval}")]
	private partial void LogRetentionEnforcementServiceStarting(TimeSpan scanInterval);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementProcessingError,
		LogLevel.Error,
		"Error in retention enforcement processing cycle")]
	private partial void LogRetentionEnforcementProcessingError(Exception exception);

	[LoggerMessage(
		ComplianceEventId.RetentionEnforcementServiceStopped,
		LogLevel.Information,
		"Retention enforcement background service stopped")]
	private partial void LogRetentionEnforcementServiceStopped();
}
