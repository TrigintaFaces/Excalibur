// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Retention;

/// <summary>
/// Background service that periodically enforces audit event retention policies.
/// </summary>
/// <remarks>
/// <para>
/// Runs on the configured <see cref="AuditRetentionOptions.CleanupInterval"/>
/// and delegates to <see cref="IAuditRetentionService"/> for the actual enforcement.
/// </para>
/// </remarks>
public sealed partial class AuditRetentionBackgroundService : BackgroundService
{
	private readonly IAuditRetentionService _retentionService;
	private readonly AuditRetentionOptions _options;
	private readonly ILogger<AuditRetentionBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditRetentionBackgroundService"/> class.
	/// </summary>
	/// <param name="retentionService">The retention service.</param>
	/// <param name="options">The retention options.</param>
	/// <param name="logger">The logger.</param>
	public AuditRetentionBackgroundService(
		IAuditRetentionService retentionService,
		IOptions<AuditRetentionOptions> options,
		ILogger<AuditRetentionBackgroundService> logger)
	{
		ArgumentNullException.ThrowIfNull(retentionService);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_retentionService = retentionService;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogRetentionServiceStarted(_options.CleanupInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _retentionService.EnforceRetentionAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogRetentionEnforcementError(ex);
			}

			try
			{
				await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogRetentionServiceStopped();
	}

	[LoggerMessage(LogLevel.Information,
		"Audit retention background service started. Cleanup interval: {CleanupInterval}")]
	private partial void LogRetentionServiceStarted(TimeSpan cleanupInterval);

	[LoggerMessage(LogLevel.Error,
		"Error during retention enforcement")]
	private partial void LogRetentionEnforcementError(Exception exception);

	[LoggerMessage(LogLevel.Information,
		"Audit retention background service stopped")]
	private partial void LogRetentionServiceStopped();
}
