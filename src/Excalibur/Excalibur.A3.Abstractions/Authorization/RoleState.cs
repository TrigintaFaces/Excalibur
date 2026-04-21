// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Represents the lifecycle state of a role.
/// </summary>
/// <remarks>
/// <para>
/// State transitions: <c>Active</c> -> <c>Inactive</c> (reversible) -> <c>Deprecated</c> (one-way).
/// A deprecated role cannot be assigned but remains visible for audit.
/// </para>
/// </remarks>
public enum RoleState
{
	/// <summary>
	/// The role is active and can be assigned to users.
	/// </summary>
	Active = 0,

	/// <summary>
	/// The role is temporarily inactive. Can be reactivated.
	/// </summary>
	Inactive = 1,

	/// <summary>
	/// The role is permanently deprecated. Cannot be assigned or reactivated.
	/// Exists only for historical audit purposes.
	/// </summary>
	Deprecated = 2,
}
