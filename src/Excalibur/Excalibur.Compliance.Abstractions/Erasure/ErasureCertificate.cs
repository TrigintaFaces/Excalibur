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
	/// Gets the Article 17(3) ground under which this data was kept.
	/// </summary>
	/// <value>
	/// The legal basis; or <see cref="LegalHoldBasis.NotEstablished"/> meaning nobody stated a ground.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>The unknown lives in the enum rather than in a nullable, and that placement is the fix.</b> A
	/// nullable here would protect only this property: <c>default(LegalHoldBasis)</c> would still be a
	/// lawful-looking ground at every other carrier, including every carrier added later.
	/// <see cref="LegalHoldBasis.NotEstablished"/> at zero fixes the type once, for every site that has one
	/// of these and every site that will.
	/// </para>
	/// <para>
	/// <b>It is not a neutral omission — it makes the erasure incomplete.</b> An exemption whose ground was
	/// never established is an unmet obligation, and presenting an unmet obligation as a lawful basis turns a
	/// failure into a defensible retention. The certificate is still issued and records that the erasure did
	/// not complete, rather than attesting a lawful outcome over a retention nobody justified.
	/// </para>
	/// </remarks>
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
	/// Gets the length of the retention, stated only alongside the instant it is measured from.
	/// </summary>
	/// <value>
	/// Always <see langword="null"/> in this scheme, meaning <em>the end of this retention is not
	/// established</em>. A period is emitted only where the end instant it is measured from is also
	/// recorded, and nothing in this framework can yet establish that instant.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>A duration with no anchor is not a retention end, and it reads as one.</b> "Six years" on a signed
	/// document leaves the reader to supply the missing instant, and the only instant in front of them is the
	/// certificate's own date — which restarts a statutory clock at the moment the data subject asked to be
	/// erased, and so extends every retention past the end the law actually gives it. The certificate would
	/// then be evidence for a longer retention than the obligation supports, in a document produced to prove
	/// the opposite.
	/// </para>
	/// <para>
	/// <b>Why the end cannot be established here.</b> The instant a particular record's obligation lapses
	/// depends on facts held by the deployment, not by the framework: a warranty term runs from delivery
	/// rather than from the order that created the record, and a limitation period for claims arising from it
	/// runs from something else again. A period declared once for an aggregate type cannot express that, and
	/// the erasure's own completion instant is the one anchor in reach and the one anchor that is wrong.
	/// Until the end can be supplied per record, the honest form of the claim is its absence.
	/// </para>
	/// <para>
	/// <b>Reading this property.</b> <see langword="null"/> is a statement, not a gap: it says the end was not
	/// established and MUST NOT be read as "no retention applies" or "the retention has no end". What the
	/// entry does establish is <see cref="Basis"/>, <see cref="DataCategory"/>, <see cref="Reason"/> and
	/// <see cref="RetainedKeyHandle"/> — the ground, the data, the justification, and the key whose
	/// destruction ends the retention.
	/// </para>
	/// </remarks>
	public TimeSpan? RetentionPeriod { get; init; }

	/// <summary>
	/// Gets the associated legal hold ID.
	/// </summary>
	public Guid? HoldId { get; init; }

	/// <summary>
	/// Gets the key handle still protecting this data subject's fields in the retained aggregate type, so
	/// the retention can be released when the obligation lapses.
	/// </summary>
	/// <value>
	/// The handle to destroy, in the form <see cref="IKeyManagementAdmin.DeleteKeyAsync"/> accepts; or
	/// <see langword="null"/> for an exemption that is not an aggregate retention, and for one whose key
	/// this erasure did not decide about.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b>Without this, the declared period is decorative.</b> A retained aggregate type keeps a key of its
	/// own so the surviving record stays readable, and that key is NOT the data subject's own handle — so
	/// nothing in the erasure path ever queues it again. When the statutory period ends, destroying it is
	/// the only act that completes the erasure, and it can only be done BY NAME. A retention whose exit
	/// cannot be found is a retention with no exit, which is the Article 17 breach the mandatory period
	/// exists to prevent.
	/// </para>
	/// <para>
	/// <b>It discloses nothing the record does not already carry.</b> The handle is composed from the data
	/// subject hash, which the payload already states, and a digest of the aggregate type, which this entry
	/// already names. Recording it adds an identifier, not a fact.
	/// </para>
	/// <para>
	/// <b>Releasing it is yours to time, and the framework does not attempt it.</b> The instant a particular
	/// record's obligation lapses depends on the transaction date, the jurisdiction and whether the period
	/// was extended — facts the framework does not hold. What it guarantees is that the handle is named on
	/// the record, so the act is possible at all.
	/// </para>
	/// <para>
	/// <b>NULL, not empty, and omitted from the signed form when null — this is load-bearing, and it is
	/// not a style choice.</b> The canonical form is what gets signed, and verification recomputes it from
	/// the DESERIALIZED payload. The serializer context emits defaulted properties, so a property that
	/// always serialized would change the canonical bytes of every certificate issued before it existed
	/// and make each of them verify as a FORGERY — and these documents are retained for seven years.
	/// Being null-by-default and ignored when null, this property is absent from the canonical form of
	/// exactly those certificates, so their signatures still verify.
	/// </para>
	/// </remarks>
	[System.Text.Json.Serialization.JsonIgnore(
		Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
	public string? RetainedKeyHandle { get; init; }
}
