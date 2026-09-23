// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Compliance;

/// <summary>
/// Signed record of a completed GDPR Article 17 erasure request.
/// </summary>
/// <remarks>
/// <para>
/// A certificate is a record of the request, not proof that the data was disposed of. Its signature does
/// not cover every claim it carries, and the evidence of disposal is the key-management service's record
/// of each key deletion.
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
	/// Gets everything this certificate asserts — and exactly what <see cref="Signature"/> covers.
	/// </summary>
	/// <remarks>
	/// The signature is computed over a canonical serialization of this payload in full. Nothing outside it
	/// is signed, and nothing inside it is skipped, so "what the document claims" and "what was signed"
	/// cannot diverge.
	/// </remarks>
	public required ErasureCertificatePayload Payload { get; init; }

	/// <summary>
	/// Gets the HMAC-SHA256 signature over <see cref="Payload"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The signature lives on the envelope rather than on the payload, and that placement is the fix.</b>
	/// While it sat among the claims it had to be excluded from its own input, and an exclusion is a list
	/// somebody maintains — the moment a second self-referential field appears, forgetting to exclude it is
	/// an infinite regress that fails closed only by luck. Here there is no exclusion list to forget.
	/// </para>
	/// <para>
	/// <b>Consumer obligation: a signature authenticates the payload it covers and nothing else.</b> Verify
	/// it against a canonical serialization of <see cref="Payload"/>, and read
	/// <see cref="ErasureCertificatePayload.Version"/> from INSIDE the verified payload — never from an
	/// untrusted envelope — when deciding which scheme to apply.
	/// </para>
	/// </remarks>
	public required string Signature { get; init; }
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
	/// Gets a value indicating whether the framework positively established the destruction this
	/// certificate attests.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see langword="true"/> means established by the framework itself: every identifier in
	/// <see cref="DeletedKeyIds"/> was reported irrecoverable by the key-management provider, and
	/// <see cref="Methods"/> names how.
	/// </para>
	/// <para>
	/// <see langword="false"/> means <em>not established</em> — whatever the reason. It is not a claim
	/// that the erasure failed. The ordinary case is an erasure discharged entirely by record deletion:
	/// the erasure contributors reported erasing rows, the framework recorded their reports, and it
	/// verified nothing itself. Read it together with <see cref="Methods"/>, which is
	/// <see cref="VerificationMethod.None"/> whenever this is <see langword="false"/>.
	/// </para>
	/// </remarks>
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
