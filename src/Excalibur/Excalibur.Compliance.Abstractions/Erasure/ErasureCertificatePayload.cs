// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// Every claim an erasure certificate makes, and the whole of what its signature covers.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type exists so that "what the certificate asserts" and "the signature over it" cannot drift
/// apart.</b> The signature is computed over a canonical serialization of this payload <i>in its entirety</i>
/// — no field list, no exclusions. A claim added here is therefore covered by construction, because there is
/// nowhere else for a claim to live.
/// </para>
/// <para>
/// <b>Why it is a separate type rather than a projection of the certificate.</b> A projection has to be
/// written by hand, so a claim added to the certificate is silently absent from the signed input until
/// someone remembers to extend the projection — the certificate still verifies while asserting something
/// nobody signed. Splitting the payload from the envelope removes the thing that has to be remembered.
/// </para>
/// <para>
/// <b><see cref="Version"/> is INSIDE the payload, deliberately.</b> An unsigned version marker is worse
/// than no version marker: it lets whoever holds the document choose which verification scheme a verifier
/// applies to it, which is a downgrade oracle. Signing it binds the document to its own format.
/// </para>
/// <para>
/// <b>Determinism is part of the contract.</b> Every member below is either a scalar or an ordered
/// <c>IReadOnlyList</c> whose order is part of the value — no dictionary or set, whose enumeration order is
/// not guaranteed. A signature over a non-deterministically serialized payload would fail verification
/// intermittently, which is a worse defect than the one this type fixes.
/// </para>
/// </remarks>
public sealed record ErasureCertificatePayload
{
	/// <summary>Gets the certificate identifier.</summary>
	public required Guid CertificateId { get; init; }

	/// <summary>Gets the identifier of the erasure request this certificate attests.</summary>
	public required Guid RequestId { get; init; }

	/// <summary>Gets the anonymized (hashed) reference to the data subject.</summary>
	public required string DataSubjectReference { get; init; }

	/// <summary>Gets the instant the erasure request was received.</summary>
	public required DateTimeOffset RequestReceivedAt { get; init; }

	/// <summary>Gets the instant the erasure completed.</summary>
	public required DateTimeOffset CompletedAt { get; init; }

	/// <summary>Gets the method by which the data was erased.</summary>
	public required ErasureMethod Method { get; init; }

	/// <summary>Gets what was erased.</summary>
	public required ErasureSummary Summary { get; init; }

	/// <summary>Gets the verification results recorded for the erasure.</summary>
	public required VerificationSummary Verification { get; init; }

	/// <summary>Gets the legal basis under which the erasure was performed.</summary>
	public required ErasureLegalBasis LegalBasis { get; init; }

	/// <summary>Gets the data that was lawfully retained rather than erased, with the basis for each.</summary>
	public IReadOnlyList<ErasureException> Exceptions { get; init; } = [];

	/// <summary>Gets the instant the certificate was generated.</summary>
	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>Gets the instant until which the certificate must be retained.</summary>
	public required DateTimeOffset RetainUntil { get; init; }

	/// <summary>
	/// Gets the certificate format version.
	/// </summary>
	/// <value>The format version. Defaults to <c>"2.0"</c>.</value>
	/// <remarks>
	/// <b>2.0 is the first version whose signature covers the certificate's claims.</b> A 1.0 certificate
	/// carried a signature over three identity fields only — the request id, the subject hash and the
	/// completion instant — so every claim on it could be altered while the signature still authenticated.
	/// The version lives in the signed payload precisely so a verifier cannot be steered to the weaker
	/// scheme by a document that claims to be older than it is.
	/// </remarks>
	public string Version { get; init; } = "2.0";
}
