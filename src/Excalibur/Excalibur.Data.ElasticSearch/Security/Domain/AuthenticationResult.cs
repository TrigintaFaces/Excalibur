// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of an authentication attempt.
/// </summary>
/// <remarks>
/// The numbering is not contiguous. Values are never reassigned, because an authentication outcome
/// that was stored or logged as a number would silently come to mean something else.
/// </remarks>
public enum AuthenticationResult
{
	/// <summary>
	/// Authentication succeeded.
	/// </summary>
	Success = 0,

	/// <summary>
	/// Authentication failed due to invalid credentials.
	/// </summary>
	InvalidCredentials = 1,

	/// <summary>
	/// Authentication failed due to system error.
	/// </summary>
	SystemError = 5,
}
