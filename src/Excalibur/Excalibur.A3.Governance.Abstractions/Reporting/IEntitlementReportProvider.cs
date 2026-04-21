// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// Generates entitlement report snapshots for governance and compliance purposes.
/// </summary>
/// <remarks>
/// <para>
/// Aggregates data from grant stores, access review stores, separation-of-duties evaluators,
/// and principal type providers to build comprehensive entitlement snapshots.
/// </para>
/// <para>
/// Supports six report types: user entitlements, tenant entitlements, orphaned grants,
/// expiring grants, SoD violations, and unreviewed grants.
/// </para>
/// </remarks>
public interface IEntitlementReportProvider
{
	/// <summary>
	/// Generates an entitlement snapshot for a specific user.
	/// </summary>
	/// <param name="userId">The user identifier to report on.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A snapshot containing all entitlements for the specified user.</returns>
	Task<EntitlementSnapshot> GenerateUserSnapshotAsync(
		string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Generates an entitlement snapshot for a specific tenant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier to report on.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A snapshot containing all entitlements within the specified tenant.</returns>
	Task<EntitlementSnapshot> GenerateTenantSnapshotAsync(
		string tenantId, CancellationToken cancellationToken);

	/// <summary>
	/// Generates an entitlement report of the specified type.
	/// </summary>
	/// <param name="reportType">The type of report to generate.</param>
	/// <param name="tenantId">Optional tenant scope. Pass <see langword="null"/> for all tenants.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A snapshot containing entitlements matching the report criteria.</returns>
	Task<EntitlementSnapshot> GenerateReportAsync(
		EntitlementReportType reportType, string? tenantId,
		CancellationToken cancellationToken);
}
