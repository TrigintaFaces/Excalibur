// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when decryption operations fail.
/// </summary>
/// <remarks>
/// This exception is thrown when decryption fails due to invalid ciphertext,
/// authentication tag verification failure, or key-related issues.
/// Error messages intentionally avoid exposing sensitive details to prevent information leakage.
/// </remarks>
public sealed class DecryptionException : EncryptionException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DecryptionException" /> class.
	/// </summary>
	public DecryptionException()
			: base(Resources.DecryptionException_DefaultMessage)
	{
		ErrorCode = EncryptionErrorCode.InvalidCiphertext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecryptionException" /> class with a message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public DecryptionException(string message)
		: base(message)
	{
		ErrorCode = EncryptionErrorCode.InvalidCiphertext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecryptionException" /> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The underlying cause of the failure.</param>
	public DecryptionException(string message, Exception innerException)
		: base(message, innerException)
	{
		ErrorCode = EncryptionErrorCode.InvalidCiphertext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecryptionException" /> class with a message and error code.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="errorCode">The specific error code for this decryption failure.</param>
	public DecryptionException(string message, EncryptionErrorCode errorCode)
		: base(message)
	{
		ErrorCode = errorCode;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecryptionException" /> class with a message, inner exception, and error code.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The underlying cause of the failure.</param>
	/// <param name="errorCode">The specific error code for this decryption failure.</param>
	public DecryptionException(string message, Exception innerException, EncryptionErrorCode errorCode)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
	}
}
