// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public sealed class AuthenticationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationException" /> class.
	/// </summary>
	public AuthenticationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationException" /> class.
	/// </summary>
	public AuthenticationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationException" /> class.
	/// </summary>
	public AuthenticationException() : base()
	{
	}
}
