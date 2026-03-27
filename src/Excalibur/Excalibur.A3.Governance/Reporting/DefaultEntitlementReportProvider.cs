// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.NonHumanIdentity;
using Excalibur.A3.Governance.OrphanedAccess;
using Excalibur.A3.Governance.Reporting;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default implementation of <see cref="IEntitlementReportProvider"/> that aggregates
/// data from governance stores to build entitlement snapshots.
/// </summary>
/// <remarks>
/// <para>
/// All dependencies except <see cref="IGrantStore"/> are optional. When an optional
/// dependency is missing, the provider gracefully degrades: affected report types
/// return empty entries and a warning is logged.
/// </para>
/// <para>
/// Production implementations should use pagination/batching for tenants with many users.
/// This implementation loads all matching grants into memory.
/// </para>
/// </remarks>
internal sealed partial class DefaultEntitlementReportProvider(
	IGrantStore grantStore,
	IAccessReviewStore? accessReviewStore,
	ISoDEvaluator? sodEvaluator,
	IOrphanedAccessDetector? orphanedDetector,
	IPrincipalTypeProvider? principalTypeProvider,
	ILogger<DefaultEntitlementReportProvider> logger) : IEntitlementReportProvider
{
	/// <inheritdoc />
	public async Task<EntitlementSnapshot> GenerateUserSnapshotAsync(
		string userId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);

		var grants = await grantStore.GetAllGrantsAsync(userId, cancellationToken).ConfigureAwait(false);
		var entries = await BuildEntriesAsync(grants, cancellationToken).ConfigureAwait(false);

		LogEntitlementReportGenerated(logger, EntitlementReportType.UserEntitlements, entries.Count);

		return new EntitlementSnapshot(
			DateTimeOffset.UtcNow,
			EntitlementReportType.UserEntitlements,
			userId,
			entries);
	}

	/// <inheritdoc />
	public async Task<EntitlementSnapshot> GenerateTenantSnapshotAsync(
		string tenantId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tenantId);

		var queryStore = grantStore.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore;
		IReadOnlyList<EntitlementEntry> entries;

		if (queryStore is not null)
		{
			// Use query store to get all grants in the tenant
			var grants = await queryStore.GetMatchingGrantsAsync(
				null, tenantId, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);
			entries = await BuildEntriesAsync(grants, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			LogEntitlementDependencyMissing(logger, nameof(IGrantQueryStore), "TenantEntitlements");
			entries = [];
		}

		LogEntitlementReportGenerated(logger, EntitlementReportType.TenantEntitlements, entries.Count);

		return new EntitlementSnapshot(
			DateTimeOffset.UtcNow,
			EntitlementReportType.TenantEntitlements,
			tenantId,
			entries);
	}

	/// <inheritdoc />
	public async Task<EntitlementSnapshot> GenerateReportAsync(
		EntitlementReportType reportType, string? tenantId,
		CancellationToken cancellationToken)
	{
		try
		{
			var entries = reportType switch
			{
				EntitlementReportType.UserEntitlements => throw new ArgumentException(
					$"Use {nameof(GenerateUserSnapshotAsync)} for user-scoped reports.", nameof(reportType)),
				EntitlementReportType.TenantEntitlements => await GenerateTenantEntriesAsync(tenantId, cancellationToken).ConfigureAwait(false),
				EntitlementReportType.OrphanedGrants => await GenerateOrphanedEntriesAsync(tenantId, cancellationToken).ConfigureAwait(false),
				EntitlementReportType.ExpiringGrants => await GenerateExpiringEntriesAsync(tenantId, cancellationToken).ConfigureAwait(false),
				EntitlementReportType.SoDViolations => await GenerateSoDViolationEntriesAsync(tenantId, cancellationToken).ConfigureAwait(false),
				EntitlementReportType.UnreviewedGrants => await GenerateUnreviewedEntriesAsync(tenantId, cancellationToken).ConfigureAwait(false),
				_ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "Unknown report type."),
			};

			LogEntitlementReportGenerated(logger, reportType, entries.Count);

			return new EntitlementSnapshot(
				DateTimeOffset.UtcNow,
				reportType,
				tenantId,
				entries);
		}
#pragma warning disable CA1031 // Do not catch general exception types -- report provider must not crash
		catch (Exception ex) when (ex is not ArgumentException and not ArgumentOutOfRangeException)
		{
			LogEntitlementReportFailed(logger, reportType, ex);
			throw;
		}
#pragma warning restore CA1031
	}

	private async Task<IReadOnlyList<EntitlementEntry>> GenerateTenantEntriesAsync(
		string? tenantId, CancellationToken cancellationToken)
	{
		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogEntitlementDependencyMissing(logger, nameof(IGrantQueryStore), "TenantEntitlements");
			return [];
		}

		var grants = await queryStore.GetMatchingGrantsAsync(
			null, tenantId ?? string.Empty, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);

		return await BuildEntriesAsync(grants, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<EntitlementEntry>> GenerateOrphanedEntriesAsync(
		string? tenantId, CancellationToken cancellationToken)
	{
		if (orphanedDetector is null)
		{
			LogEntitlementDependencyMissing(logger, nameof(IOrphanedAccessDetector), "OrphanedGrants");
			return [];
		}

		var report = await orphanedDetector.DetectAsync(tenantId, cancellationToken).ConfigureAwait(false);
		var entries = new List<EntitlementEntry>(report.OrphanedGrants.Count);

		foreach (var orphan in report.OrphanedGrants)
		{
			var principalType = principalTypeProvider is not null
				? await principalTypeProvider.GetPrincipalTypeAsync(orphan.UserId, cancellationToken).ConfigureAwait(false)
				: PrincipalType.Human;

			entries.Add(new EntitlementEntry(
				orphan.UserId,
				principalType,
				orphan.GrantScope,
				orphan.GrantedOn,
				GrantedBy: string.Empty,
				ExpiresOn: null,
				IsActive: true,
				ReviewStatus: null));
		}

		return entries;
	}

	private async Task<IReadOnlyList<EntitlementEntry>> GenerateExpiringEntriesAsync(
		string? tenantId, CancellationToken cancellationToken)
	{
		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogEntitlementDependencyMissing(logger, nameof(IGrantQueryStore), "ExpiringGrants");
			return [];
		}

		var grants = await queryStore.GetMatchingGrantsAsync(
			null, tenantId ?? string.Empty, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var expiringWindow = TimeSpan.FromDays(30);

		var expiringGrants = grants
			.Where(g => g.ExpiresOn is not null && g.ExpiresOn > now && g.ExpiresOn <= now + expiringWindow)
			.ToList();

		return await BuildEntriesAsync(expiringGrants, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<EntitlementEntry>> GenerateSoDViolationEntriesAsync(
		string? tenantId, CancellationToken cancellationToken)
	{
		if (sodEvaluator is null)
		{
			LogEntitlementDependencyMissing(logger, nameof(ISoDEvaluator), "SoDViolations");
			return [];
		}

		// Get all grants to find distinct users, then evaluate SoD per user
		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogEntitlementDependencyMissing(logger, nameof(IGrantQueryStore), "SoDViolations");
			return [];
		}

		var grants = await queryStore.GetMatchingGrantsAsync(
			null, tenantId ?? string.Empty, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);

		var userIds = grants.Select(g => g.UserId).Distinct(StringComparer.Ordinal).ToList();
		var entries = new List<EntitlementEntry>();

		foreach (var userId in userIds)
		{
			var conflicts = await sodEvaluator.EvaluateCurrentAsync(userId, cancellationToken).ConfigureAwait(false);
			if (conflicts.Count == 0)
			{
				continue;
			}

			var userGrants = grants.Where(g => string.Equals(g.UserId, userId, StringComparison.Ordinal)).ToList();
			var conflictPolicyIds = conflicts.Select(c => c.PolicyId).Distinct(StringComparer.Ordinal).ToList();

			var principalType = principalTypeProvider is not null
				? await principalTypeProvider.GetPrincipalTypeAsync(userId, cancellationToken).ConfigureAwait(false)
				: PrincipalType.Human;

			foreach (var grant in userGrants)
			{
				entries.Add(new EntitlementEntry(
					grant.UserId,
					principalType,
					grant.Qualifier,
					grant.GrantedOn,
					grant.GrantedBy,
					grant.ExpiresOn,
					IsActive: true,
					new EntitlementReviewStatus(
						HasBeenReviewed: false,
						LastReviewedOn: null,
						SoDConflictPolicyIds: conflictPolicyIds)));
			}
		}

		return entries;
	}

	private async Task<IReadOnlyList<EntitlementEntry>> GenerateUnreviewedEntriesAsync(
		string? tenantId, CancellationToken cancellationToken)
	{
		if (accessReviewStore is null)
		{
			LogEntitlementDependencyMissing(logger, nameof(IAccessReviewStore), "UnreviewedGrants");
			return [];
		}

		if (grantStore.GetService(typeof(IGrantQueryStore)) is not IGrantQueryStore queryStore)
		{
			LogEntitlementDependencyMissing(logger, nameof(IGrantQueryStore), "UnreviewedGrants");
			return [];
		}

		var grants = await queryStore.GetMatchingGrantsAsync(
			null, tenantId ?? string.Empty, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);

		// Get completed campaigns to check which grants have been reviewed
		var completedCampaigns = await accessReviewStore.GetCampaignsByStateAsync(
			AccessReviewState.Completed, cancellationToken).ConfigureAwait(false);

		var reviewedScopes = new HashSet<string>(
			completedCampaigns.Select(c => c.Scope.FilterValue).Where(f => f is not null)!,
			StringComparer.Ordinal);

		var unreviewedGrants = grants
			.Where(g => !reviewedScopes.Contains(g.Qualifier))
			.ToList();

		return await BuildEntriesAsync(unreviewedGrants, cancellationToken).ConfigureAwait(false);
	}

	private async Task<List<EntitlementEntry>> BuildEntriesAsync(
		IReadOnlyList<Grant> grants, CancellationToken cancellationToken)
	{
		var entries = new List<EntitlementEntry>(grants.Count);

		foreach (var grant in grants)
		{
			var principalType = principalTypeProvider is not null
				? await principalTypeProvider.GetPrincipalTypeAsync(grant.UserId, cancellationToken).ConfigureAwait(false)
				: PrincipalType.Human;

			var reviewStatus = await GetReviewStatusAsync(grant, cancellationToken).ConfigureAwait(false);

			entries.Add(new EntitlementEntry(
				grant.UserId,
				principalType,
				grant.Qualifier,
				grant.GrantedOn,
				grant.GrantedBy,
				grant.ExpiresOn,
				IsActive: true,
				reviewStatus));
		}

		return entries;
	}

	private async Task<EntitlementReviewStatus?> GetReviewStatusAsync(
		Grant grant, CancellationToken cancellationToken)
	{
		var hasReviewData = accessReviewStore is not null;
		var hasSoDData = sodEvaluator is not null;

		if (!hasReviewData && !hasSoDData)
		{
			return null;
		}

		var hasBeenReviewed = false;
		var lastReviewedOn = (DateTimeOffset?)null;
		var conflictPolicyIds = (IReadOnlyList<string>?)null;

		if (accessReviewStore is not null)
		{
			var completedCampaigns = await accessReviewStore.GetCampaignsByStateAsync(
				AccessReviewState.Completed, cancellationToken).ConfigureAwait(false);

			var matchingCampaign = completedCampaigns
				.Where(c => string.Equals(c.Scope.FilterValue, grant.Qualifier, StringComparison.Ordinal))
				.OrderByDescending(c => c.ExpiresAt)
				.FirstOrDefault();

			if (matchingCampaign is not null)
			{
				hasBeenReviewed = true;
				lastReviewedOn = matchingCampaign.ExpiresAt;
			}
		}

		if (sodEvaluator is not null)
		{
			var conflicts = await sodEvaluator.EvaluateCurrentAsync(
				grant.UserId, cancellationToken).ConfigureAwait(false);

			if (conflicts.Count > 0)
			{
				conflictPolicyIds = conflicts
					.Select(c => c.PolicyId)
					.Distinct(StringComparer.Ordinal)
					.ToList();
			}
		}

		return new EntitlementReviewStatus(hasBeenReviewed, lastReviewedOn, conflictPolicyIds);
	}

	[LoggerMessage(EventId = 3590, Level = LogLevel.Information,
		Message = "Entitlement report generated: type={ReportType}, entries={EntryCount}.")]
	private static partial void LogEntitlementReportGenerated(
		ILogger logger, EntitlementReportType reportType, int entryCount);

	[LoggerMessage(EventId = 3591, Level = LogLevel.Error,
		Message = "Entitlement report generation failed for type={ReportType}.")]
	private static partial void LogEntitlementReportFailed(
		ILogger logger, EntitlementReportType reportType, Exception exception);

	[LoggerMessage(EventId = 3592, Level = LogLevel.Warning,
		Message = "Optional dependency '{DependencyName}' is not registered; report type '{ReportType}' will return empty entries.")]
	private static partial void LogEntitlementDependencyMissing(
		ILogger logger, string dependencyName, string reportType);
}
