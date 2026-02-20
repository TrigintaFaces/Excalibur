// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Exception thrown when an erasure operation fails.
/// </summary>
public sealed class ErasureOperationException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureOperationException"/> class.
	/// </summary>
	public ErasureOperationException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureOperationException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public ErasureOperationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureOperationException"/> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public ErasureOperationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets or sets the request ID associated with the failed operation.
	/// </summary>
	public Guid? RequestId { get; init; }

	/// <summary>
	/// Gets or sets the reason for the failure.
	/// </summary>
	public ErasureFailureReason Reason { get; init; }

	/// <summary>
	/// Creates an exception for a validation failure.
	/// </summary>
	public static ErasureOperationException ValidationFailed(Guid requestId, string message) =>
		new(message)
		{
			RequestId = requestId,
			Reason = ErasureFailureReason.ValidationFailed
		};

	/// <summary>
	/// Creates an exception for a key deletion failure.
	/// </summary>
	public static ErasureOperationException KeyDeletionFailed(Guid requestId, string keyId, Exception? inner = null) =>
		new($"Failed to delete key '{keyId}' for erasure request {requestId}", inner)
		{
			RequestId = requestId,
			Reason = ErasureFailureReason.KeyDeletionFailed
		};

	/// <summary>
	/// Creates an exception for a verification failure.
	/// </summary>
	public static ErasureOperationException VerificationFailed(Guid requestId, string message) =>
		new(message)
		{
			RequestId = requestId,
			Reason = ErasureFailureReason.VerificationFailed
		};

	/// <summary>
	/// Creates an exception for a legal hold block.
	/// </summary>
	public static ErasureOperationException BlockedByLegalHold(Guid requestId, Guid holdId, string caseReference) =>
		new($"Erasure request {requestId} blocked by legal hold {holdId} (case: {caseReference})")
		{
			RequestId = requestId,
			Reason = ErasureFailureReason.BlockedByLegalHold
		};
}

/// <summary>
/// Reasons for erasure operation failure.
/// </summary>
public enum ErasureFailureReason
{
	/// <summary>
	/// Unknown failure reason.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Request validation failed.
	/// </summary>
	ValidationFailed = 1,

	/// <summary>
	/// Data subject not found.
	/// </summary>
	DataSubjectNotFound = 2,

	/// <summary>
	/// Key deletion failed in KMS.
	/// </summary>
	KeyDeletionFailed = 3,

	/// <summary>
	/// Erasure verification failed.
	/// </summary>
	VerificationFailed = 4,

	/// <summary>
	/// Request blocked by legal hold.
	/// </summary>
	BlockedByLegalHold = 5,

	/// <summary>
	/// Request already processed.
	/// </summary>
	AlreadyProcessed = 6,

	/// <summary>
	/// Certificate generation failed.
	/// </summary>
	CertificateGenerationFailed = 7,

	/// <summary>
	/// Storage operation failed.
	/// </summary>
	StorageFailed = 8,

	/// <summary>
	/// Operation timed out.
	/// </summary>
	Timeout = 9
}
