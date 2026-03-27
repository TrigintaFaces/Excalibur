// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.OrphanedAccess;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Background service that periodically runs the orphaned access detector
/// and optionally executes auto-revocation of flagged grants.
/// </summary>
internal sealed partial class OrphanedAccessScanService(
	IServiceScopeFactory scopeFactory,
	IOptions<OrphanedAccessOptions> options,
	ILogger<OrphanedAccessScanService> logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = options.Value;
		var interval = TimeSpan.FromHours(opts.ScanIntervalHours);

		using var timer = new PeriodicTimer(interval);

		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
		{
			try
			{
				await RunScanAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
#pragma warning disable CA1031 // Do not catch general exception types -- BackgroundService must not crash
			catch (Exception ex)
			{
				LogScanFailed(logger, ex);
			}
#pragma warning restore CA1031
		}
	}

	private async Task RunScanAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var detector = scope.ServiceProvider.GetRequiredService<IOrphanedAccessDetector>();

		var report = await detector.DetectAsync(tenantId: null, cancellationToken)
			.ConfigureAwait(false);

		LogScanResult(logger, report.TotalUsersScanned, report.OrphanedGrants.Count);

		foreach (var grant in report.OrphanedGrants)
		{
			LogOrphanedGrant(logger, grant.UserId, grant.GrantScope,
				grant.UserStatus, grant.RecommendedAction);
		}
	}

	[LoggerMessage(EventId = 3543, Level = LogLevel.Information,
		Message = "Orphaned access background scan completed: {UserCount} users scanned, {OrphanedCount} orphaned grants.")]
	private static partial void LogScanResult(ILogger logger, int userCount, int orphanedCount);

	[LoggerMessage(EventId = 3544, Level = LogLevel.Warning,
		Message = "Orphaned grant: user '{UserId}', scope '{GrantScope}', status {UserStatus}, action {RecommendedAction}.")]
	private static partial void LogOrphanedGrant(ILogger logger, string userId, string grantScope,
		PrincipalStatus userStatus, OrphanedAccessAction recommendedAction);

	[LoggerMessage(EventId = 3545, Level = LogLevel.Error,
		Message = "Orphaned access background scan failed.")]
	private static partial void LogScanFailed(ILogger logger, Exception exception);
}
