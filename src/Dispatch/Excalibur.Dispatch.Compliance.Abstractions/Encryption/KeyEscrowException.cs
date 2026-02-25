// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when a key escrow operation fails.
/// </summary>
public sealed class KeyEscrowException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowException"/> class.
	/// </summary>
	public KeyEscrowException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public KeyEscrowException(string message) : base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public KeyEscrowException(string message, Exception innerException) : base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the key identifier related to this error, if applicable.
	/// </summary>
	public string? KeyId { get; init; }

	/// <summary>
	/// Gets or sets the escrow identifier related to this error, if applicable.
	/// </summary>
	public string? EscrowId { get; init; }

	/// <summary>
	/// Gets or sets the error code for programmatic handling.
	/// </summary>
	public KeyEscrowErrorCode ErrorCode { get; init; }
}

/// <summary>
/// Error codes for key escrow operations.
/// </summary>
public enum KeyEscrowErrorCode
{
	/// <summary>
	/// Unknown or unspecified error.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The specified key was not found in escrow.
	/// </summary>
	KeyNotFound = 1,

	/// <summary>
	/// An escrow already exists for the specified key.
	/// </summary>
	EscrowAlreadyExists = 2,

	/// <summary>
	/// The escrow has expired.
	/// </summary>
	EscrowExpired = 3,

	/// <summary>
	/// The escrow has been revoked.
	/// </summary>
	EscrowRevoked = 4,

	/// <summary>
	/// The recovery token is invalid or expired.
	/// </summary>
	InvalidToken = 5,

	/// <summary>
	/// Insufficient shares provided for recovery.
	/// </summary>
	InsufficientShares = 6,

	/// <summary>
	/// The secret sharing reconstruction failed.
	/// </summary>
	ReconstructionFailed = 7,

	/// <summary>
	/// The master encryption key is unavailable.
	/// </summary>
	MasterKeyUnavailable = 8,

	/// <summary>
	/// A storage or persistence error occurred.
	/// </summary>
	StorageError = 9,

	/// <summary>
	/// The key material integrity check failed.
	/// </summary>
	IntegrityCheckFailed = 10
}
