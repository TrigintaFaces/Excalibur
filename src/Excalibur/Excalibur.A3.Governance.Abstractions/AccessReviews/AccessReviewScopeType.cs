// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Defines the type of scope filter for an access review campaign.
/// </summary>
public enum AccessReviewScopeType
{
	/// <summary>
	/// Review every grant in the campaign's tenant.
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
}
