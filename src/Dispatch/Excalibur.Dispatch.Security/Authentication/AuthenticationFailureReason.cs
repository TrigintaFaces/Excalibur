// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines authentication failure reasons.
/// </summary>
public enum AuthenticationFailureReason
{
	/// <summary>
	/// No token was provided.
	/// </summary>
	MissingToken = 0,

	/// <summary>
	/// Token validation failed.
	/// </summary>
	InvalidToken = 1,

	/// <summary>
	/// Token has expired.
	/// </summary>
	TokenExpired = 2,

	/// <summary>
	/// Validation error occurred.
	/// </summary>
	ValidationError = 3,

	/// <summary>
	/// Unknown error occurred.
	/// </summary>
	UnknownError = 4,
}
