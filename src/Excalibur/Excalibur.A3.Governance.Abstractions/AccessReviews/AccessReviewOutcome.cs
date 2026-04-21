// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// The outcome of an individual access review decision.
/// </summary>
public enum AccessReviewOutcome
{
	/// <summary>
	/// The reviewer approved continued access.
	/// </summary>
	Approved = 0,

	/// <summary>
	/// The reviewer revoked the access.
	/// </summary>
	Revoked = 1,

	/// <summary>
	/// The reviewer delegated the decision to another reviewer.
	/// </summary>
	Delegated = 2,
}
