// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Exception thrown when signature verification operations fail.
/// </summary>
public sealed class VerificationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="VerificationException" /> class.
	/// </summary>
	public VerificationException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="VerificationException" /> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public VerificationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="VerificationException" /> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The underlying cause of the failure.</param>
	public VerificationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
