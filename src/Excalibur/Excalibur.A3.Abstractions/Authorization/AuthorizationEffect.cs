// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// The effect of an authorization evaluation.
/// </summary>
public enum AuthorizationEffect
{
	/// <summary>
	/// Access is denied.
	/// </summary>
	/// <remarks>
	/// This is the zero value, so a default-initialized <see cref="AuthorizationEffect" /> fails
	/// closed (deny) rather than accidentally permitting access.
	/// </remarks>
	Deny = 0,

	/// <summary>
	/// Access is permitted.
	/// </summary>
	Permit = 1,
}
