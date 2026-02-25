// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when a master key backup operation fails.
/// </summary>
public sealed class MasterKeyBackupException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MasterKeyBackupException"/> class.
	/// </summary>
	public MasterKeyBackupException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MasterKeyBackupException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public MasterKeyBackupException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MasterKeyBackupException"/> class
	/// with a specified error message and inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public MasterKeyBackupException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the identifier of the key involved in the failed operation.
	/// </summary>
	public string? KeyId { get; init; }

	/// <summary>
	/// Gets or sets the identifier of the backup involved in the failed operation.
	/// </summary>
	public string? BackupId { get; init; }

	/// <summary>
	/// Gets or sets the error code indicating the type of failure.
	/// </summary>
	public MasterKeyBackupErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Error codes for master key backup operations.
/// </summary>
public enum MasterKeyBackupErrorCode
{
	/// <summary>
	/// An unknown error occurred.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The specified key was not found.
	/// </summary>
	KeyNotFound = 1,

	/// <summary>
	/// The specified backup was not found.
	/// </summary>
	BackupNotFound = 2,

	/// <summary>
	/// The backup has expired.
	/// </summary>
	BackupExpired = 3,

	/// <summary>
	/// The backup format is not supported.
	/// </summary>
	UnsupportedFormat = 4,

	/// <summary>
	/// The backup integrity check failed.
	/// </summary>
	IntegrityCheckFailed = 5,

	/// <summary>
	/// The key hash verification failed after reconstruction.
	/// </summary>
	HashVerificationFailed = 6,

	/// <summary>
	/// Insufficient shares were provided for reconstruction.
	/// </summary>
	InsufficientShares = 7,

	/// <summary>
	/// The shares provided are from different keys or versions.
	/// </summary>
	ShareMismatch = 8,

	/// <summary>
	/// The key cannot be exported (e.g., non-exportable HSM key).
	/// </summary>
	KeyNotExportable = 9,

	/// <summary>
	/// A key with the same ID already exists and overwrite was not allowed.
	/// </summary>
	KeyAlreadyExists = 10,

	/// <summary>
	/// The wrapping key for encryption/decryption was not found.
	/// </summary>
	WrappingKeyNotFound = 11,

	/// <summary>
	/// An error occurred during cryptographic operations.
	/// </summary>
	CryptographicError = 12
}
