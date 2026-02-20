// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Specifies access permissions for a destination.
/// </summary>
[Flags]
public enum AccessPermissions
{
	/// <summary>
	/// No permissions.
	/// </summary>
	None = 0,

	/// <summary>
	/// Permission to send messages.
	/// </summary>
	Send = 1,

	/// <summary>
	/// Permission to receive messages.
	/// </summary>
	Receive = 1 << 1,

	/// <summary>
	/// Permission to manage the destination.
	/// </summary>
	Manage = 1 << 2,

	/// <summary>
	/// All permissions.
	/// </summary>
	All = Send | Receive | Manage,
}
