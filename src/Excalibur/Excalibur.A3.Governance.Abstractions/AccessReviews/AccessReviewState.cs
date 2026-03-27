// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// The lifecycle state of an access review campaign.
/// </summary>
public enum AccessReviewState
{
	/// <summary>
	/// The campaign has been created but not yet started.
	/// </summary>
	Created = 0,

	/// <summary>
	/// The campaign is actively collecting review decisions.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// All items have been reviewed and the campaign is complete.
	/// </summary>
	Completed = 2,

	/// <summary>
	/// The campaign deadline passed before all items were reviewed.
	/// </summary>
	Expired = 3,
}
