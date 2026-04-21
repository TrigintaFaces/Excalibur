// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.SeparationOfDuties;

/// <summary>
/// Defines what kind of items a Separation of Duties policy references.
/// </summary>
public enum SoDPolicyScope
{
	/// <summary>
	/// The policy's <see cref="SoDPolicy.ConflictingItems"/> reference role names.
	/// A conflict is detected when a user holds grants for two or more of the listed roles.
	/// </summary>
	Role = 0,

	/// <summary>
	/// The policy's <see cref="SoDPolicy.ConflictingItems"/> reference activity names.
	/// A conflict is detected when a user's effective permissions include two or more of the listed activities.
	/// </summary>
	Activity = 1,
}
