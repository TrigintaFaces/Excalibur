// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Certificate proving GDPR Article 17 compliance for an erasure request.
/// </summary>
/// <remarks>
/// <para>
/// Certificates provide cryptographically verifiable proof of data erasure.
/// They are retained for 7 years to support regulatory audits and legal discovery.
/// </para>
/// <para>
/// The certificate includes:
/// - Anonymized data subject reference (hash)
/// - Erasure method and summary
/// - Verification proof from KMS
/// - Digital signature for integrity
/// </para>
/// </remarks>
public sealed record ErasureCertificate
{
	/// <summary>
	/// Gets the certificate identifier.
	/// </summary>
	public required Guid CertificateId { get; init; }

	/// <summary>
	/// Gets the original erasure request ID.
	/// </summary>
	public required Guid RequestId { get; init; }

	/// <summary>
	/// Gets the anonymized data subject reference (hash of identifier).
	/// </summary>
	public required string DataSubjectReference { get; init; }

	/// <summary>
	/// Gets when the erasure request was received.
	/// </summary>
	public required DateTimeOffset RequestReceivedAt { get; init; }

	/// <summary>
	/// Gets when the erasure was completed.
	/// </summary>
	public required DateTimeOffset CompletedAt { get; init; }

	/// <summary>
	/// Gets the erasure method used.
	/// </summary>
	public required ErasureMethod Method { get; init; }

	/// <summary>
	/// Gets the summary of data erased.
	/// </summary>
	public required ErasureSummary Summary { get; init; }

	/// <summary>
	/// Gets the verification result.
	/// </summary>
	public required VerificationSummary Verification { get; init; }

	/// <summary>
	/// Gets the legal basis for the erasure.
	/// </summary>
	public required ErasureLegalBasis LegalBasis { get; init; }

	/// <summary>
	/// Gets any exceptions where data was retained per Article 17(3).
	/// </summary>
	public IReadOnlyList<ErasureException> Exceptions { get; init; } = [];

	/// <summary>
	/// Gets the cryptographic signature of the certificate.
	/// </summary>
	public required string Signature { get; init; }

	/// <summary>
	/// Gets the certificate generation timestamp.
	/// </summary>
	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets when this certificate should be retained until.
	/// </summary>
	public required DateTimeOffset RetainUntil { get; init; }

	/// <summary>
	/// Gets the certificate version for schema compatibility.
	/// </summary>
	public string Version { get; init; } = "1.0";
}

/// <summary>
/// Method used for data erasure.
/// </summary>
public enum ErasureMethod
{
	/// <summary>
	/// Keys deleted, rendering data irrecoverable.
	/// </summary>
	CryptographicErasure = 0,

	/// <summary>
	/// Data physically deleted from storage.
	/// </summary>
	PhysicalDeletion = 1,

	/// <summary>
	/// Data overwritten with random values.
	/// </summary>
	SecureOverwrite = 2,

	/// <summary>
	/// Combination of methods used.
	/// </summary>
	Hybrid = 3
}

/// <summary>
/// Summary of erased data.
/// </summary>
public sealed record ErasureSummary
{
	/// <summary>
	/// Gets the number of encryption keys deleted.
	/// </summary>
	public int KeysDeleted { get; init; }

	/// <summary>
	/// Gets the number of records affected.
	/// </summary>
	public int RecordsAffected { get; init; }

	/// <summary>
	/// Gets the data categories erased.
	/// </summary>
	public IReadOnlyList<string> DataCategories { get; init; } = [];

	/// <summary>
	/// Gets the tables/collections affected.
	/// </summary>
	public IReadOnlyList<string> TablesAffected { get; init; } = [];

	/// <summary>
	/// Gets the total data size in bytes (before erasure).
	/// </summary>
	public long DataSizeBytes { get; init; }
}

/// <summary>
/// Summary of erasure verification.
/// </summary>
public sealed record VerificationSummary
{
	/// <summary>
	/// Gets whether verification passed.
	/// </summary>
	public required bool Verified { get; init; }

	/// <summary>
	/// Gets the verification methods used.
	/// </summary>
	public required VerificationMethod Methods { get; init; }

	/// <summary>
	/// Gets the verification timestamp.
	/// </summary>
	public required DateTimeOffset VerifiedAt { get; init; }

	/// <summary>
	/// Gets the hash of the verification report.
	/// </summary>
	public string? ReportHash { get; init; }

	/// <summary>
	/// Gets the list of deleted key IDs.
	/// </summary>
	public IReadOnlyList<string> DeletedKeyIds { get; init; } = [];

	/// <summary>
	/// Gets any verification warnings.
	/// </summary>
	public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Methods used for erasure verification.
/// </summary>
[Flags]
public enum VerificationMethod
{
	/// <summary>
	/// No verification performed.
	/// </summary>
	None = 0,

	/// <summary>
	/// Verified via audit log entries.
	/// </summary>
	AuditLog = 1,

	/// <summary>
	/// Verified via KMS key deletion confirmation.
	/// </summary>
	KeyManagementSystem = 2,

	/// <summary>
	/// Verified via HSM attestation.
	/// </summary>
	HsmAttestation = 4,

	/// <summary>
	/// Verified data is unreadable (decryption fails).
	/// </summary>
	DecryptionFailure = 8
}

/// <summary>
/// Exception where data was retained per Article 17(3).
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Represents a GDPR erasure exception (legal retention), not a runtime exception.")]
public sealed record ErasureException
{
	/// <summary>
	/// Gets the legal basis for the exception.
	/// </summary>
	public required LegalHoldBasis Basis { get; init; }

	/// <summary>
	/// Gets the data category retained.
	/// </summary>
	public required string DataCategory { get; init; }

	/// <summary>
	/// Gets the reason for retention.
	/// </summary>
	public required string Reason { get; init; }

	/// <summary>
	/// Gets the expected retention period.
	/// </summary>
	public TimeSpan? RetentionPeriod { get; init; }

	/// <summary>
	/// Gets the associated legal hold ID.
	/// </summary>
	public Guid? HoldId { get; init; }
}
