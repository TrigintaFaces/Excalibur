// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Represents the current status of a principal (user/service account) in the identity system.
/// </summary>
/// <remarks>
/// Used by <see cref="IUserStatusProvider"/> to determine whether a principal's grants
/// should be flagged, revoked, or investigated for orphaned access.
/// </remarks>
public enum PrincipalStatus
{
	/// <summary>
	/// The principal is active and in good standing.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The principal is inactive (e.g., on leave, disabled account).
	/// Grants may be flagged or revoked depending on grace period configuration.
	/// </summary>
	Inactive = 1,

	/// <summary>
	/// The principal has departed the organization (terminated, resigned).
	/// Grants should typically be revoked.
	/// </summary>
	Departed = 2,

	/// <summary>
	/// The principal's status could not be determined.
	/// Grants should be investigated manually.
	/// </summary>
	Unknown = 3,
}
