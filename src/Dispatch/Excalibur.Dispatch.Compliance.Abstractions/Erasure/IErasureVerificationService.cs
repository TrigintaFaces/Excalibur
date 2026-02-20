// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Severity of verification failures.
/// </summary>
public enum VerificationSeverity
{
	/// <summary>
	/// Warning - erasure likely complete but cannot fully verify.
	/// </summary>
	Warning = 0,

	/// <summary>
	/// Error - verification failed, erasure may be incomplete.
	/// </summary>
	Error = 1,

	/// <summary>
	/// Critical - strong evidence erasure did not complete.
	/// </summary>
	Critical = 2
}

/// <summary>
/// Service for verifying GDPR erasure completeness.
/// </summary>
/// <remarks>
/// <para>
/// Verification uses a defense-in-depth approach combining:
/// </para>
/// <list type="bullet">
/// <item><description>Cryptographic verification - Confirms keys are deleted in KMS</description></item>
/// <item><description>Audit log verification - Reviews audit trail for deletion events</description></item>
/// <item><description>Decryption failure verification - Confirms encrypted data is irrecoverable</description></item>
/// </list>
/// </remarks>
public interface IErasureVerificationService
{
	/// <summary>
	/// Verifies that an erasure request was successfully completed.
	/// </summary>
	/// <param name="requestId">The erasure request ID to verify.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Verification result with details.</returns>
	Task<VerificationResult> VerifyErasureAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates a verification report for audit purposes.
	/// </summary>
	/// <param name="requestId">The erasure request ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The verification report.</returns>
	Task<VerificationReport> GenerateReportAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies that a specific encryption key has been deleted.
	/// </summary>
	/// <param name="keyId">The key ID to verify.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the key is confirmed deleted.</returns>
	Task<bool> VerifyKeyDeletionAsync(
		string keyId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Detailed result of erasure verification.
/// </summary>
public sealed record VerificationResult
{
	/// <summary>
	/// Gets whether verification passed overall.
	/// </summary>
	public required bool Verified { get; init; }

	/// <summary>
	/// Gets the verification methods used.
	/// </summary>
	public required VerificationMethod Methods { get; init; }

	/// <summary>
	/// Gets the keys confirmed deleted.
	/// </summary>
	public IReadOnlyList<string> DeletedKeyIds { get; init; } = [];

	/// <summary>
	/// Gets any verification failures.
	/// </summary>
	public IReadOnlyList<VerificationFailure> Failures { get; init; } = [];

	/// <summary>
	/// Gets any warnings (non-blocking issues).
	/// </summary>
	public IReadOnlyList<string> Warnings { get; init; } = [];

	/// <summary>
	/// Gets the verification timestamp.
	/// </summary>
	public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the duration of the verification process.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets a hash of this verification result for integrity.
	/// </summary>
	public string? ResultHash { get; init; }

	/// <summary>
	/// Converts to a summary for certificate inclusion.
	/// </summary>
	public VerificationSummary ToSummary() => new()
	{
		Verified = Verified,
		Methods = Methods,
		VerifiedAt = VerifiedAt,
		ReportHash = ResultHash,
		DeletedKeyIds = DeletedKeyIds,
		Warnings = Warnings
	};

	/// <summary>
	/// Creates a failed verification result.
	/// </summary>
	public static VerificationResult Failed(VerificationFailure failure, TimeSpan duration) => new()
	{
		Verified = false,
		Methods = VerificationMethod.None,
		Failures = [failure],
		VerifiedAt = DateTimeOffset.UtcNow,
		Duration = duration
	};

	/// <summary>
	/// Creates a successful verification result.
	/// </summary>
	public static VerificationResult Success(
		VerificationMethod methods,
		IReadOnlyList<string> deletedKeyIds,
		TimeSpan duration,
		IReadOnlyList<string>? warnings = null) => new()
		{
			Verified = true,
			Methods = methods,
			DeletedKeyIds = deletedKeyIds,
			Warnings = warnings ?? [],
			VerifiedAt = DateTimeOffset.UtcNow,
			Duration = duration
		};
}

/// <summary>
/// A verification failure detail.
/// </summary>
public sealed record VerificationFailure
{
	/// <summary>
	/// Gets what failed verification.
	/// </summary>
	public required string Subject { get; init; }

	/// <summary>
	/// Gets the failure reason.
	/// </summary>
	public required string Reason { get; init; }

	/// <summary>
	/// Gets the severity of the failure.
	/// </summary>
	public required VerificationSeverity Severity { get; init; }

	/// <summary>
	/// Gets additional details about the failure.
	/// </summary>
	public string? Details { get; init; }

	/// <summary>
	/// Gets the verification method that failed.
	/// </summary>
	public VerificationMethod? FailedMethod { get; init; }
}

/// <summary>
/// Detailed verification report for audit purposes.
/// </summary>
public sealed record VerificationReport
{
	/// <summary>
	/// Gets the report identifier.
	/// </summary>
	public required Guid ReportId { get; init; }

	/// <summary>
	/// Gets the erasure request ID.
	/// </summary>
	public required Guid RequestId { get; init; }

	/// <summary>
	/// Gets the verification result.
	/// </summary>
	public required VerificationResult Result { get; init; }

	/// <summary>
	/// Gets detailed verification steps performed.
	/// </summary>
	public IReadOnlyList<VerificationStep> Steps { get; init; } = [];

	/// <summary>
	/// Gets when the report was generated.
	/// </summary>
	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the hash of the report for integrity verification.
	/// </summary>
	public string? ReportHash { get; init; }
}

/// <summary>
/// Individual verification step in a report.
/// </summary>
public sealed record VerificationStep
{
	/// <summary>
	/// Gets the step name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the verification method used.
	/// </summary>
	public required VerificationMethod Method { get; init; }

	/// <summary>
	/// Gets whether this step passed.
	/// </summary>
	public required bool Passed { get; init; }

	/// <summary>
	/// Gets details about the step result.
	/// </summary>
	public string? Details { get; init; }

	/// <summary>
	/// Gets the duration of this step.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets when this step was performed.
	/// </summary>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
