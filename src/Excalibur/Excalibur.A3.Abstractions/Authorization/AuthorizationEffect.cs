// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// The effect of an authorization evaluation.
/// </summary>
public enum AuthorizationEffect
{
	/// <summary>
	/// Access is permitted.
	/// </summary>
	Permit = 0,

	/// <summary>
	/// Access is denied.
	/// </summary>
	Deny = 1,

	/// <summary>
	/// Insufficient information to decide.
	/// </summary>
	Indeterminate = 2,
}
