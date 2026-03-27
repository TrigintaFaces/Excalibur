// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Defines the policy to apply when an access review campaign expires with unreviewed items.
/// </summary>
public enum AccessReviewExpiryPolicy
{
	/// <summary>
	/// Take no action on unreviewed items. The campaign is marked expired for audit purposes.
	/// </summary>
	DoNothing = 0,

	/// <summary>
	/// Automatically revoke access for all unreviewed items.
	/// </summary>
	RevokeUnreviewed = 1,

	/// <summary>
	/// Notify reviewers and extend the campaign deadline.
	/// </summary>
	NotifyAndExtend = 2,
}
