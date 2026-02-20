// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for processing GDPR Article 17 erasure requests (Right to be Forgotten).
/// </summary>
/// <remarks>
/// <para>
/// This service implements cryptographic erasure by deleting encryption keys,
/// rendering all data encrypted with those keys irrecoverable. This approach provides:
/// - Mathematically provable data destruction
/// - Verifiable compliance evidence
/// - Integration with existing key hierarchy
/// </para>
/// <para>
/// The service supports a configurable grace period (default 72 hours) before key deletion
/// to allow for cancellation of mistaken requests and legal hold integration.
/// </para>
/// </remarks>
public interface IErasureService
{
	/// <summary>
	/// Submits an erasure request for a data subject.
	/// </summary>
	/// <param name="request">The erasure request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Erasure request result with tracking ID and status.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
	/// <exception cref="ErasureException">Thrown when the request cannot be processed.</exception>
	Task<ErasureResult> RequestErasureAsync(
		ErasureRequest request,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current status of an erasure request.
	/// </summary>
	/// <param name="requestId">The erasure request tracking ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Current status of the erasure request, or null if not found.</returns>
	Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a pending erasure request (only during grace period).
	/// </summary>
	/// <param name="requestId">The erasure request tracking ID.</param>
	/// <param name="reason">Cancellation reason for audit trail.</param>
	/// <param name="cancelledBy">Identity of the person/system cancelling the request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if cancelled successfully, false if request not found or already executed.</returns>
	/// <exception cref="InvalidOperationException">Thrown when request has already been executed.</exception>
	Task<bool> CancelErasureAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a compliance certificate for a completed erasure request.
	/// </summary>
	/// <param name="requestId">The erasure request tracking ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Compliance certificate with cryptographic proof of erasure.</returns>
	/// <exception cref="InvalidOperationException">Thrown when request is not completed.</exception>
	/// <exception cref="KeyNotFoundException">Thrown when request is not found.</exception>
	Task<ErasureCertificate> GenerateCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a scheduled erasure request by deleting encryption keys.
	/// </summary>
	/// <param name="requestId">The erasure request tracking ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Execution result with details of keys deleted.</returns>
	/// <remarks>
	/// <para>
	/// This method is typically called by the scheduler background service when
	/// the grace period has expired. It performs:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Validation that the request is in Scheduled status</description></item>
	/// <item><description>Re-verification of legal holds</description></item>
	/// <item><description>Cryptographic key deletion via KMS</description></item>
	/// <item><description>Recording of completion status</description></item>
	/// </list>
	/// </remarks>
	Task<ErasureExecutionResult> ExecuteAsync(
		Guid requestId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of an erasure execution operation.
/// </summary>
public sealed record ErasureExecutionResult
{
	/// <summary>
	/// Gets whether the erasure executed successfully.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the error message if execution failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the number of encryption keys deleted.
	/// </summary>
	public int KeysDeleted { get; init; }

	/// <summary>
	/// Gets the number of data records affected.
	/// </summary>
	public int RecordsAffected { get; init; }

	/// <summary>
	/// Creates a successful execution result.
	/// </summary>
	public static ErasureExecutionResult Succeeded(int keysDeleted, int recordsAffected) => new()
	{
		Success = true,
		KeysDeleted = keysDeleted,
		RecordsAffected = recordsAffected
	};

	/// <summary>
	/// Creates a failed execution result.
	/// </summary>
	public static ErasureExecutionResult Failed(string errorMessage) => new()
	{
		Success = false,
		ErrorMessage = errorMessage
	};
}
