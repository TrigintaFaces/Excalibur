// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Recommended action for an orphaned grant.
/// </summary>
public enum OrphanedAccessAction
{
	/// <summary>
	/// Flag the grant for manual review. Used for inactive users within grace period.
	/// </summary>
	Flag = 0,

	/// <summary>
	/// Revoke the grant. Used for departed users or inactive users past the grace period.
	/// </summary>
	Revoke = 1,

	/// <summary>
	/// Investigate the grant. Used when the user's status could not be determined.
	/// </summary>
	Investigate = 2,
}
