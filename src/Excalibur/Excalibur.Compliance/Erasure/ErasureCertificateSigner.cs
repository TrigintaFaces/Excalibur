// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;


namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Signs an erasure certificate payload: HMAC-SHA256 over a canonical serialization of the whole payload.
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
/// <b>It lives in its own type rather than on the service</b> so that the thing which decides what a
/// signature covers is one small readable unit, and so the verification side has an obvious home to be
/// written against rather than re-deriving the rule.
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

	/// <summary>Signs <paramref name="payload"/> in full with <paramref name="signingKey"/>.</summary>
	/// <param name="payload">The payload to sign. Every claim on it is covered.</param>
	/// <param name="signingKey">The HMAC key, supplied from a secret manager.</param>
	/// <returns>The signature, as <c>v2:{base64 HMAC-SHA256}</c>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no signing key is configured, or when the payload declares a version this scheme did not
	/// produce.
	/// </exception>
	public static string Sign(ErasureCertificatePayload payload, byte[] signingKey)
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

		return $"{SchemeTagPrefix}:{Convert.ToBase64String(ComputeMac(payload, signingKey))}";
	}

	/// <summary>
	/// Computes the raw HMAC over the payload's canonical form.
	/// </summary>
	/// <remarks>
	/// Signing and verification share this one method deliberately. Two implementations of "the bytes we
	/// sign" is the drift that turns an honest certificate into a reported forgery, so there is exactly one.
	/// </remarks>
	internal static byte[] ComputeMac(ErasureCertificatePayload payload, byte[] signingKey)
	{
		var json = ErasureCertificateCanonicalizer.ToCanonicalJson(payload);

		using var hmac = new HMACSHA256(signingKey);

		return hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
	}
}
