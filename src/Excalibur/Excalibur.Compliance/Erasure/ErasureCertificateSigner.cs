// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;


namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Issues an erasure certificate: establishes every claim that carries legal meaning, then signs what
/// survives with HMAC-SHA256 over a canonical serialization of the whole payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>The entire payload is the signed input, and that is the point.</b> The previous scheme signed
/// <c>$"{requestId}|{dataSubjectIdHash}|{completedAt:O}"</c> — three identity fields and not one claim — so
/// the counts, the verification summary, the legal basis, the exemptions and what was actually erased could
/// all be altered while the certificate still authenticated. Signing the payload whole means a claim added
/// to <see cref="ErasureCertificatePayload"/> is covered without anyone remembering to add it here.
/// </para>
/// <para>
/// <b>This is also the ONE place a claim is checked, and that placement is the design.</b> Signing is the
/// instant a value becomes legal evidence, and every source of a claim arrives here: the coverage
/// evaluator, a consumer's own erasure contributor, a payload rebuilt from persisted status, a store round
/// trip. A check placed beside each source is a check the NEXT source will not know it owes — three guards
/// in three places is how mutually-justifying guards arise, and none of them covers the fourth producer. A
/// check placed here cannot be bypassed by a producer that does not exist yet, because there is no other
/// way to obtain a signature.
/// </para>
/// <para>
/// <b>An unestablished claim is reported, never refused, and that asymmetry is deliberate.</b> The
/// ENVELOPE's shape is refused — no signing key, or a payload declaring a scheme this code did not produce,
/// throws — because such a document could not be authenticated by anyone and can be re-issued at will. A
/// CLAIM cannot be refused the same way. Signing happens AFTER the irreversible act: keys are destroyed,
/// then contributors run, then this issues the document. A refusal here would leave a consumer with the
/// destruction performed and no evidence that it was performed, which is strictly worse than the claim
/// defect it was protecting them from. So the certificate is ALWAYS issued; what varies is what it
/// asserts. An unestablished claim is stripped where the payload can express "not established", and it is
/// reported to the caller either way, which folds it into the erasure's errors — the collection that
/// already makes an attestation of completion unreachable.
/// </para>
/// <para>
/// Serialization goes through the source-generated context, so it is AOT- and trim-safe and its property
/// order is fixed at compile time. The payload carries only scalars and ordered lists — no dictionary or
/// set whose enumeration order is unspecified — so the signed bytes are stable across processes. A
/// non-deterministic signed input would fail verification intermittently, which is a worse defect than the
/// one this replaces.
/// </para>
/// </remarks>
internal static class ErasureCertificateSigner
{
	/// <summary>
	/// The prefix every signature this scheme produces carries, so a verifier can select a canonical form
	/// without first parsing a payload it has not authenticated.
	/// </summary>
	/// <remarks>
	/// The sibling audit chain already does exactly this (<c>v1:{keyId}:{mac}</c>). A bare MAC forces a
	/// verifier to guess which scheme produced it, and with certificates from an older scheme sitting in
	/// consumer databases the only way to guess is to try each one — which is a downgrade path by another
	/// name. There is no key id here because this path has a single configured signing key and no key
	/// provider to resolve one against; adding a key id would be inventing a rotation story that does not
	/// exist.
	/// </remarks>
	internal const string SchemeTagPrefix = "v2";

	/// <summary>The value <see cref="ErasureCertificatePayload.Version"/> must carry for this scheme.</summary>
	internal const string SchemePayloadVersion = "2.0";

	/// <summary>The byte length of an HMAC-SHA256 tag.</summary>
	internal const int MacLength = 32;

	/// <summary>Every bit <see cref="VerificationMethod"/> declares, so a value carrying any other is undefined.</summary>
	/// <remarks>
	/// <see cref="Enum.IsDefined{T}(T)"/> is the wrong instrument for a <see cref="FlagsAttribute"/> enum: a
	/// legitimate combination of two declared flags is not itself a declared member, so it would be reported
	/// undefined and a truthful certificate would be called a failure. The mask is the correct test — it
	/// accepts any combination of declared bits and rejects only a value carrying a bit nobody declared.
	/// </remarks>
	private const VerificationMethod AllVerificationMethods =
		VerificationMethod.AuditLog
		| VerificationMethod.KeyManagementSystem
		| VerificationMethod.HsmAttestation
		| VerificationMethod.DecryptionFailure;

	/// <summary>
	/// Establishes every claim on <paramref name="payload"/> that carries legal meaning, signs what survives,
	/// and returns the finished certificate.
	/// </summary>
	/// <param name="payload">The claims to attest. Every one of them is covered by the signature.</param>
	/// <param name="signingKey">The HMAC key, supplied from a secret manager.</param>
	/// <param name="unestablishedClaims">
	/// The claims this payload asserted and did not establish, each phrased for an auditor rather than a
	/// developer. Empty when every claim held. A caller MUST treat a non-empty list as an erasure that did
	/// NOT complete: an exemption whose legal basis was never established is an unmet obligation, and
	/// presenting an unmet obligation as a lawful ground turns a failure into a defensible retention.
	/// </param>
	/// <returns>
	/// The certificate. Always issued — see this type's remarks for why a claim defect must not refuse one.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no signing key is configured, or when the payload declares a version this scheme did not
	/// produce. Both describe the envelope rather than any claim, and both describe a document nobody could
	/// authenticate.
	/// </exception>
	public static ErasureCertificate Issue(
		ErasureCertificatePayload payload,
		byte[] signingKey,
		out IReadOnlyList<string> unestablishedClaims)
	{
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(signingKey);

		if (signingKey.Length == 0)
		{
			// Fail closed: a keyless SHA-256 hash is not a signature — anyone can recompute it, so it is
			// forgeable and provides no tamper evidence. Refuse to emit an unsigned "signature" rather than
			// produce an erasure certificate that cannot be authenticated.
			throw new InvalidOperationException(
				"Cannot generate a tamper-evident erasure certificate signature: no HMAC signing key is configured. " +
				"Configure the erasure retention signing key (supplied from a secret manager) so certificates are " +
				"signed with HMAC-SHA256.");
		}

		if (!string.Equals(payload.Version, SchemePayloadVersion, StringComparison.Ordinal))
		{
			// Fail closed for the same reason the key guard does. The version is INSIDE the signature, so a
			// tag saying v2 over a payload declaring some other version is a document that describes a scheme
			// nobody ran. Refusing here makes the disagreement impossible to issue rather than merely
			// detectable later.
			throw new InvalidOperationException(
				$"Cannot sign an erasure certificate payload declaring version '{payload.Version}': this signing "
				+ $"scheme produces version '{SchemePayloadVersion}' certificates only. The version is part of the "
				+ "signed content, so signing a payload that names a different scheme would attest to a format "
				+ "that was not used.");
		}

		var attested = Establish(payload, out unestablishedClaims);

		return new ErasureCertificate
		{
			Payload = attested,
			Signature = $"{SchemeTagPrefix}:{Convert.ToBase64String(ComputeMac(attested, signingKey))}"
		};
	}

	/// <summary>
	/// Names the claims a caller is about to assert and has not established, before it decides the erasure's
	/// outcome.
	/// </summary>
	/// <param name="legalBasis">The Article 17(1) ground the erasure was performed under.</param>
	/// <param name="retentions">The retentions the certificate will record, each with the ground it claims.</param>
	/// <returns>One entry per unestablished claim, phrased for an auditor. Empty when every claim holds.</returns>
	/// <remarks>
	/// <para>
	/// <b>This exists because the outcome is decided BEFORE the certificate is signed, and the finding must
	/// reach the outcome.</b> An exemption whose ground was never established is an unmet obligation, so the
	/// erasure is not complete — and "not complete" is settled several steps earlier than signing, by the
	/// caller's errors collection. A finding discovered only at signing time would arrive after the
	/// attestation it needed to prevent.
	/// </para>
	/// <para>
	/// <b>It is not a second copy of the rule.</b> This and <see cref="Issue"/> reach the same per-retention
	/// predicate, so a claim cannot be established here and unestablished there. Asking early is what lets
	/// the outcome be honest; <see cref="Issue"/> remains the only path to a signature, which is what stops a
	/// producer who never asks from getting one anyway.
	/// </para>
	/// </remarks>
	internal static IReadOnlyList<string> UnestablishedClaims(
		ErasureLegalBasis legalBasis,
		IReadOnlyList<ErasureException> retentions)
	{
		ArgumentNullException.ThrowIfNull(retentions);

		List<string>? findings = null;

		EstablishLegalBasis(legalBasis, ref findings);

		for (var i = 0; i < retentions.Count; i++)
		{
			_ = Establish(retentions[i], i, ref findings);
		}

		return findings ?? [];
	}

	/// <summary>
	/// Computes the raw HMAC over the payload's canonical form.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Signing and verification share this one method deliberately. Two implementations of "the bytes we
	/// sign" is the drift that turns an honest certificate into a reported forgery, so there is exactly one.
	/// </para>
	/// <para>
	/// <b>A verifier recomputes over the payload EXACTLY as the document carries it, and never re-runs
	/// <see cref="Issue"/>'s claim validation.</b> Normalising on the way in would strip an unestablished
	/// claim out of a document that was signed WITH it, quietly bringing an altered certificate back into
	/// agreement with its own signature. What was signed is what must be checked.
	/// </para>
	/// </remarks>
	internal static byte[] ComputeMac(ErasureCertificatePayload payload, byte[] signingKey)
	{
		var json = ErasureCertificateCanonicalizer.ToCanonicalJson(payload);

		using var hmac = new HMACSHA256(signingKey);

		return hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
	}

	/// <summary>
	/// Reduces <paramref name="payload"/> to the claims it actually established, and names the ones it did not.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The payload is rebuilt only where something was stripped, so an erasure whose every claim held is
	/// signed over the identical instance it arrived as. That is not a micro-optimization: it means the
	/// ordinary path has no rebuild that could perturb the signed bytes.
	/// </para>
	/// <para>
	/// A claim is STRIPPED where the payload can say "not established" and only REPORTED where it cannot.
	/// Both add to <paramref name="unestablished"/>, so neither is silent, and the caller's errors
	/// collection is what keeps an attestation of completion out of reach either way.
	/// </para>
	/// </remarks>
	private static ErasureCertificatePayload Establish(
		ErasureCertificatePayload payload,
		out IReadOnlyList<string> unestablished)
	{
		List<string>? findings = null;

		// Enum-typed whole-erasure claims. NEITHER has a "not established" value to be reduced to -- both are
		// `required` and non-nullable on the payload -- so an undefined one is reported and left exactly as it
		// arrived. The document then carries a value no reader can interpret AND the caller's errors
		// collection says the erasure did not complete, which together are honest. Substituting a defined
		// member would be the worse outcome by far: it would INVENT a ground, which is the defect this
		// boundary exists to prevent, committed by the boundary itself.
		if (!Enum.IsDefined(payload.Method))
		{
			(findings ??= []).Add(
				$"the erasure method is recorded as {(int)payload.Method}, which names no method this "
				+ "framework defines, so how the data was erased is not established.");
		}

		EstablishLegalBasis(payload.LegalBasis, ref findings);

		if ((payload.Verification.Methods & ~AllVerificationMethods) != 0)
		{
			(findings ??= []).Add(
				$"the verification summary names methods {(int)payload.Verification.Methods}, which carries a "
				+ "flag this framework does not define, so what verified the erasure is not established.");
		}

		List<ErasureException>? exceptions = null;

		for (var i = 0; i < payload.Exceptions.Count; i++)
		{
			var claimed = payload.Exceptions[i];
			var established = Establish(claimed, i, ref findings);

			if (!ReferenceEquals(established, claimed))
			{
				exceptions ??= [.. payload.Exceptions];
				exceptions[i] = established;
			}
		}

		unestablished = findings ?? [];

		return exceptions is null ? payload : payload with { Exceptions = exceptions };
	}

	/// <summary>Records a finding when the Article 17(1) ground for erasing was not established.</summary>
	/// <remarks>
	/// Both halves are closed here, on the same terms as a retention's Article 17(3) basis and for the same
	/// reason: zero is <see cref="ErasureLegalBasis.NotEstablished"/> rather than a lawful ground, so a value
	/// nobody assigned says so instead of claiming Article 17(1)(a). An out-of-range value is a separate case
	/// and is still tested separately — it is not NotEstablished.
	/// </remarks>
	private static void EstablishLegalBasis(ErasureLegalBasis legalBasis, ref List<string>? findings)
	{
		if (legalBasis == ErasureLegalBasis.NotEstablished)
		{
			(findings ??= []).Add(
				"the erasure states no Article 17(1) legal basis, so the ground for erasing this data "
				+ "subject's data is not established.");

			return;
		}

		if (!Enum.IsDefined(legalBasis))
		{
			(findings ??= []).Add(
				$"the erasure's legal basis is recorded as {(int)legalBasis}, which names no Article 17(1) "
				+ "ground this framework defines, so the ground for erasing is not established.");
		}
	}

	/// <summary>Reduces one retention entry to the claims it established.</summary>
	private static ErasureException Establish(ErasureException retention, int index, ref List<string>? findings)
	{
		// TWO TESTS, AND NEITHER IS REDUNDANT. Enum.IsDefined alone catches an out-of-range cast, which is the
		// LOUD half; it cannot catch a value nobody assigned, because whatever sits at zero is defined by
		// construction. That is why zero is NotEstablished rather than a lawful ground: it makes the silent
		// half expressible, so a reflection binder, a deserializer or a store round trip that never set this
		// arrives here saying so instead of claiming Article 17(3)(a). Test both, because an out-of-range
		// value is still not NotEstablished.
		var basisEstablished =
			retention.Basis != LegalHoldBasis.NotEstablished && Enum.IsDefined(retention.Basis);

		if (!basisEstablished)
		{
			(findings ??= []).Add(
				retention.Basis == LegalHoldBasis.NotEstablished
					? $"retention {index} ('{retention.DataCategory}') states no Article 17(3) legal basis, so "
					+ "the ground for keeping this data subject's data is not established."
					: $"retention {index} ('{retention.DataCategory}') states legal basis "
					+ $"{(int)retention.Basis}, which names no Article 17(3) ground this framework "
					+ "defines, so the ground for keeping this data subject's data is not established.");
		}

		// A DURATION WITH NO ANCHOR IS NOT A RETENTION END, AND IT READS AS ONE. "Six years" on a signed
		// document invites the reader to supply the missing instant, and the only instant in front of them is
		// the certificate's own date -- which restarts a statutory clock at the moment the subject asked to be
		// erased, extending every retention past its lawful end. Until this scheme can state the end itself,
		// the honest form of the claim is its ABSENCE: the entry states what it established (a basis, a
		// category, a reason, the handle that releases it) and no more.
		//
		// Stripped HERE rather than trusted from the producer, for the reason this whole boundary exists: the
		// producer that emits one today is not the last producer there will be. Not reported as a finding,
		// because an unresolved end is the ordinary state of every retention this framework can currently
		// record -- reporting it would make every lawful retention an incomplete erasure, which is a fix
		// worse than the defect.
		return basisEstablished && retention.RetentionPeriod is null
			? retention
			: retention with
			{
				Basis = basisEstablished ? retention.Basis : LegalHoldBasis.NotEstablished,
				RetentionPeriod = null
			};
	}
}
