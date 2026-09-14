// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authentication;

/// <summary>
/// Represents the possible states of user authentication within an application.
/// </summary>
public enum AuthenticationState
{
	/// <summary>
	/// Indicates that the user is not authenticated and is browsing anonymously.
	/// </summary>
	Anonymous = 0,

	/// <summary>
	/// Indicates that the user has been authenticated, but their identity has not been fully verified.
	/// </summary>
	Authenticated = 1,

	/// <summary>
	/// Indicates that the user has been authenticated and their identity has been fully verified.
	/// </summary>
	Identified = 2,
}
