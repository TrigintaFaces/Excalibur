// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Background service that periodically scans all users' grants against SoD policies
/// to detect existing violations.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="PeriodicTimer"/> for the scan interval (not <c>Task.Delay</c>)
/// and <see cref="IServiceScopeFactory"/> for scoped dependencies.
/// Only runs when <see cref="SoDOptions.EnableDetectiveScanning"/> is <see langword="true"/>.
/// </para>
/// </remarks>
internal sealed partial class SoDDetectiveScanService(
	IServiceScopeFactory scopeFactory,
	IOptions<SoDOptions> options,
	ILogger<SoDDetectiveScanService> logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = options.Value;
		if (!opts.EnableDetectiveScanning)
		{
			LogDetectiveScanDisabled(logger);
			return;
		}

		using var timer = new PeriodicTimer(opts.DetectiveScanInterval);

		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
		{
			try
			{
				await ScanAllUsersAsync(stoppingToken).ConfigureAwait(false);
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

	private async Task ScanAllUsersAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var evaluator = scope.ServiceProvider.GetRequiredService<ISoDEvaluator>();
		var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();

		// Get distinct user IDs from the activity group grant store (ISP extension)
		var userIds = await GetDistinctUserIdsAsync(grantStore, cancellationToken).ConfigureAwait(false);

		LogScanStarted(logger, userIds.Count);

		var totalConflicts = 0;

		foreach (var userId in userIds)
		{
			var conflicts = await evaluator.EvaluateCurrentAsync(userId, cancellationToken)
				.ConfigureAwait(false);

			if (conflicts.Count > 0)
			{
				totalConflicts += conflicts.Count;
				foreach (var conflict in conflicts)
				{
					LogConflictDetected(logger, conflict.PolicyId, userId,
						conflict.ConflictingItem1, conflict.ConflictingItem2, conflict.Severity);
				}
			}
		}

		LogScanCompleted(logger, userIds.Count, totalConflicts);
	}

	private static async Task<IReadOnlyList<string>> GetDistinctUserIdsAsync(
		IGrantStore grantStore,
		CancellationToken cancellationToken)
	{
		// Try to get user IDs via IActivityGroupGrantStore ISP
		if (grantStore.GetService(typeof(IActivityGroupGrantStore)) is not IActivityGroupGrantStore activityGroupGrantStore)
		{
			// Fallback: no way to enumerate users without an ISP extension
			return [];
		}

		// Enumerate users across all known grant types and union into a single set
		var userIds = new HashSet<string>(StringComparer.Ordinal);

		foreach (var grantType in new[] { GrantType.Activity, GrantType.ActivityGroup, GrantType.Role })
		{
			var ids = await activityGroupGrantStore.GetDistinctActivityGroupGrantUserIdsAsync(
				grantType, cancellationToken).ConfigureAwait(false);

			foreach (var id in ids)
			{
				userIds.Add(id);
			}
		}

		return [.. userIds];
	}

	[LoggerMessage(EventId = 3500, Level = LogLevel.Information, Message = "SoD detective scanning is disabled.")]
	private static partial void LogDetectiveScanDisabled(ILogger logger);

	[LoggerMessage(EventId = 3501, Level = LogLevel.Information, Message = "SoD detective scan started for {UserCount} users.")]
	private static partial void LogScanStarted(ILogger logger, int userCount);

	[LoggerMessage(EventId = 3502, Level = LogLevel.Information, Message = "SoD detective scan completed: {UserCount} users scanned, {ConflictCount} conflicts detected.")]
	private static partial void LogScanCompleted(ILogger logger, int userCount, int conflictCount);

	[LoggerMessage(EventId = 3503, Level = LogLevel.Warning, Message = "SoD conflict detected: policy '{PolicyId}', user '{UserId}', items '{Item1}' vs '{Item2}', severity {Severity}.")]
	private static partial void LogConflictDetected(ILogger logger, string policyId, string userId, string item1, string item2, SoDSeverity severity);

	[LoggerMessage(EventId = 3504, Level = LogLevel.Error, Message = "SoD detective scan failed.")]
	private static partial void LogScanFailed(ILogger logger, Exception exception);
}
