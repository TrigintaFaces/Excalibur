// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.OrphanedAccess;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default implementation of <see cref="IOrphanedAccessDetector"/> that scans all grants
/// via <see cref="IActivityGroupGrantStore"/> and checks each user's status via
/// <see cref="IUserStatusProvider"/>.
/// </summary>
internal sealed partial class DefaultOrphanedAccessDetector(
	IGrantStore grantStore,
	IUserStatusProvider userStatusProvider,
	IOptions<OrphanedAccessOptions> options,
	ILogger<DefaultOrphanedAccessDetector> logger) : IOrphanedAccessDetector
{
	/// <inheritdoc />
	public async Task<OrphanedAccessReport> DetectAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var opts = options.Value;

		// Get all distinct user IDs across grant types
		var userIds = await GetDistinctUserIdsAsync(cancellationToken).ConfigureAwait(false);

		LogScanStarted(logger, userIds.Count, tenantId);

		var orphanedGrants = new List<OrphanedGrant>();

		var now = DateTimeOffset.UtcNow;

		foreach (var userId in userIds)
		{
			PrincipalStatusResult statusResult;
			try
			{
				statusResult = await userStatusProvider.GetStatusAsync(userId, cancellationToken)
					.ConfigureAwait(false);
			}
#pragma warning disable CA1031 // Do not catch general exception types -- never fail entire scan per R4 spec
			catch (Exception ex)
			{
				LogStatusProviderFailed(logger, userId, ex);
				statusResult = new PrincipalStatusResult(PrincipalStatus.Unknown, null);
			}
#pragma warning restore CA1031

			if (statusResult.Status == PrincipalStatus.Active)
			{
				continue;
			}

			var action = DetermineAction(statusResult, opts, now);

			// Get this user's grants
			var grants = await grantStore.GetAllGrantsAsync(userId, cancellationToken)
				.ConfigureAwait(false);

			foreach (var grant in grants)
			{
				// Filter by tenant if specified
				if (tenantId is not null && !string.Equals(grant.TenantId, tenantId, StringComparison.Ordinal))
				{
					continue;
				}

				orphanedGrants.Add(new OrphanedGrant(
					UserId: userId,
					GrantScope: grant.Qualifier,
					UserStatus: statusResult.Status,
					GrantedOn: grant.GrantedOn,
					RecommendedAction: action));
			}
		}

		LogScanCompleted(logger, userIds.Count, orphanedGrants.Count);

		return new OrphanedAccessReport(
			GeneratedAt: DateTimeOffset.UtcNow,
			TenantId: tenantId,
			OrphanedGrants: orphanedGrants,
			TotalUsersScanned: userIds.Count);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}

	private static OrphanedAccessAction DetermineAction(
		PrincipalStatusResult statusResult, OrphanedAccessOptions opts, DateTimeOffset now)
	{
		switch (statusResult.Status)
		{
			case PrincipalStatus.Departed:
				return opts.AutoRevokeDeparted
					? OrphanedAccessAction.Revoke
					: OrphanedAccessAction.Flag;

			case PrincipalStatus.Inactive:
				if (!opts.AutoRevokeAfterGracePeriod)
				{
					return OrphanedAccessAction.Flag;
				}

				// Check if inactive duration exceeds grace period
				if (statusResult.StatusChangedAt.HasValue)
				{
					var inactiveDays = (now - statusResult.StatusChangedAt.Value).TotalDays;
					return inactiveDays >= opts.InactiveGracePeriodDays
						? OrphanedAccessAction.Revoke
						: OrphanedAccessAction.Flag;
				}

				// No timestamp available -- conservative: Flag rather than Revoke
				return OrphanedAccessAction.Flag;

			case PrincipalStatus.Unknown:
				return OrphanedAccessAction.Investigate;

			default:
				return OrphanedAccessAction.Flag;
		}
	}

	private async Task<IReadOnlyList<string>> GetDistinctUserIdsAsync(
		CancellationToken cancellationToken)
	{
		if (grantStore.GetService(typeof(IActivityGroupGrantStore)) is not IActivityGroupGrantStore activityGroupGrantStore)
		{
			return [];
		}

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

	[LoggerMessage(EventId = 3540, Level = LogLevel.Information,
		Message = "Orphaned access scan started for {UserCount} users (tenant: {TenantId}).")]
	private static partial void LogScanStarted(ILogger logger, int userCount, string? tenantId);

	[LoggerMessage(EventId = 3541, Level = LogLevel.Information,
		Message = "Orphaned access scan completed: {UserCount} users scanned, {OrphanedCount} orphaned grants detected.")]
	private static partial void LogScanCompleted(ILogger logger, int userCount, int orphanedCount);

	[LoggerMessage(EventId = 3542, Level = LogLevel.Warning,
		Message = "IUserStatusProvider failed for user '{UserId}'. Treating as Unknown.")]
	private static partial void LogStatusProviderFailed(ILogger logger, string userId, Exception exception);
}
