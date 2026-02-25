// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents errors that occur during encryption or decryption operations.
/// </summary>
/// <remarks>
/// Error messages intentionally avoid exposing sensitive details like key IDs or ciphertext to prevent information leakage in logs.
/// </remarks>
public class EncryptionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionException" /> class.
	/// </summary>
	public EncryptionException()
			: base(Resources.EncryptionException_DefaultMessage)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionException" /> class with a specified error message.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	public EncryptionException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionException" /> class with a specified error message and a reference to the
	/// inner exception that is the cause of this exception.
	/// </summary>
	/// <param name="message"> The message that describes the error. </param>
	/// <param name="innerException"> The exception that is the cause of the current exception. </param>
	public EncryptionException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the error code for categorizing encryption failures.
	/// </summary>
	public EncryptionErrorCode ErrorCode { get; init; } = EncryptionErrorCode.Unknown;
}

/// <summary>
/// Categorizes encryption failure types.
/// </summary>
public enum EncryptionErrorCode
{
	/// <summary>
	/// Unknown or unspecified error.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The specified key was not found.
	/// </summary>
	KeyNotFound = 1,

	/// <summary>
	/// The key has expired and cannot be used for encryption.
	/// </summary>
	KeyExpired = 2,

	/// <summary>
	/// The key is suspended and cannot be used.
	/// </summary>
	KeySuspended = 3,

	/// <summary>
	/// The ciphertext is invalid or corrupted.
	/// </summary>
	InvalidCiphertext = 4,

	/// <summary>
	/// Authentication tag verification failed (tampered data).
	/// </summary>
	AuthenticationFailed = 5,

	/// <summary>
	/// The operation is not FIPS 140-2 compliant as required.
	/// </summary>
	FipsComplianceViolation = 6,

	/// <summary>
	/// Access to the key is denied.
	/// </summary>
	AccessDenied = 7,

	/// <summary>
	/// The key management service is unavailable.
	/// </summary>
	ServiceUnavailable = 8,

	/// <summary>
	/// The algorithm is not supported.
	/// </summary>
	UnsupportedAlgorithm = 9
}
