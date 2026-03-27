// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Defines the type of scope filter for an access review campaign.
/// </summary>
public enum AccessReviewScopeType
{
	/// <summary>
	/// Review all grants in the system.
	/// </summary>
	AllGrants = 0,

	/// <summary>
	/// Review grants associated with a specific role.
	/// </summary>
	ByRole = 1,

	/// <summary>
	/// Review grants for a specific user.
	/// </summary>
	ByUser = 2,

	/// <summary>
	/// Review grants within a specific tenant.
	/// </summary>
	ByTenant = 3,
}
