// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.Reporting;

/// <summary>
/// Identifies the type of entitlement report to generate.
/// </summary>
public enum EntitlementReportType
{
	/// <summary>
	/// All entitlements for a specific user.
	/// </summary>
	UserEntitlements = 0,

	/// <summary>
	/// All entitlements within a specific tenant.
	/// </summary>
	TenantEntitlements = 1,

	/// <summary>
	/// Grants whose owning principal is inactive, departed, or unknown.
	/// </summary>
	OrphanedGrants = 2,

	/// <summary>
	/// Grants that expire within a configurable window.
	/// </summary>
	ExpiringGrants = 3,

	/// <summary>
	/// Grants that violate separation-of-duties policies.
	/// </summary>
	SoDViolations = 4,

	/// <summary>
	/// Grants that have never been reviewed in an access review campaign.
	/// </summary>
	UnreviewedGrants = 5,
}
