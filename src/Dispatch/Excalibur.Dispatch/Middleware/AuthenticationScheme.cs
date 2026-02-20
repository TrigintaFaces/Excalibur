// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Authentication schemes supported by the authentication middleware.
/// </summary>
internal enum AuthenticationScheme
{
	/// <summary>
	/// Bearer token authentication (JWT).
	/// </summary>
	Bearer = 0,

	/// <summary>
	/// API key authentication.
	/// </summary>
	ApiKey = 1,

	/// <summary>
	/// Client certificate authentication.
	/// </summary>
	Certificate = 2,
}
