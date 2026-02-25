// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when key rotation operations fail.
/// </summary>
/// <remarks>
/// This exception is thrown when key rotation fails due to key management service errors,
/// permission issues, or configuration problems.
/// </remarks>
public sealed class KeyRotationException : EncryptionException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationException" /> class.
	/// </summary>
	public KeyRotationException()
			: base(Resources.KeyRotationException_DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationException" /> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public KeyRotationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationException" /> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The underlying cause of the failure.</param>
	public KeyRotationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationException" /> class with a message and error code.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="errorCode">The specific error code for this key rotation failure.</param>
	public KeyRotationException(string message, EncryptionErrorCode errorCode)
		: base(message)
	{
		ErrorCode = errorCode;
	}
}
