// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Checks that an erasure certificate's content is the content that was signed.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no lenient mode and there will not be one.</b> A tolerance — "ignore the fields we know our
/// store drops", "accept a certificate from the older scheme" — is a verifier that certifies an alteration,
/// which is the defect this whole seam exists to close wearing the verifier's badge. The method below takes
/// a certificate and a key and nothing else, so there is no option object a leniency could be added to.
/// </para>
/// <para>
/// <b>What it establishes, exactly.</b> That the payload presented is byte-for-byte the payload over which
/// the tag was computed, by someone holding the signing key. It says nothing about whether the claims were
/// true when they were made: a certificate can be faithfully signed and still attest to a verification
/// nobody performed. Integrity and truthfulness are different properties and only the first is checkable
/// from here.
/// </para>
/// <para>
/// <b>Why it is static rather than a service.</b> Verification is a pure function of a document and a key.
/// Nothing about it needs a lifetime, a container or a seam, and giving it one would invite configuration —
/// which is where leniency gets added.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var outcome = ErasureCertificateVerifier.Verify(certificate, signingKey);
/// if (outcome != ErasureCertificateVerificationResult.Verified)
/// {
///     // Do not hand this certificate to an auditor as evidence of its own contents.
/// }
/// </code>
/// </example>
public static class ErasureCertificateVerifier
{
	/// <summary>
	/// Recomputes <paramref name="certificate"/>'s signature over its payload and compares it with the
	/// signature the certificate carries.
	/// </summary>
	/// <param name="certificate">The certificate to check, exactly as it was read back from storage.</param>
	/// <param name="signingKey">
	/// The HMAC key the certificate was signed with, supplied from a secret manager. This is the same key
	/// configured as the erasure retention signing key on the host that issued the certificate.
	/// </param>
	/// <returns>What the check established. See <see cref="ErasureCertificateVerificationResult"/>.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="certificate"/> or <paramref name="signingKey"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">Thrown when <paramref name="signingKey"/> is empty.</exception>
	public static ErasureCertificateVerificationResult Verify(ErasureCertificate certificate, byte[] signingKey)
	{
		ArgumentNullException.ThrowIfNull(certificate);
		ArgumentNullException.ThrowIfNull(signingKey);

		if (signingKey.Length == 0)
		{
			// A caller's configuration mistake is not a verdict about the document. Returning "mismatch"
			// here would report every certificate in a misconfigured host as altered, which is a security
			// finding manufactured out of a missing setting.
			throw new InvalidOperationException(
				"Cannot verify an erasure certificate signature: no HMAC signing key was supplied. Pass the same "
				+ "key the issuing host was configured with (the erasure retention signing key). Verifying with no "
				+ "key would establish nothing, so it is refused rather than reported as a failed verification.");
		}

		if (!TryParseTag(certificate.Signature, out var presentedMac))
		{
			return ErasureCertificateVerificationResult.NotVerifiable;
		}

		if (!string.Equals(
			certificate.Payload.Version,
			ErasureCertificateSigner.SchemePayloadVersion,
			StringComparison.Ordinal))
		{
			// The tag claims this scheme while the signed content names another. Whichever of the two is
			// wrong, no canonical form this framework knows how to build corresponds to the document in
			// hand, so nothing can be established. Refusing here is what stops a tag prefix steering the
			// verifier onto a weaker interpretation of the same bytes.
			return ErasureCertificateVerificationResult.NotVerifiable;
		}

		var computedMac = ErasureCertificateSigner.ComputeMac(certificate.Payload, signingKey);

		return CryptographicOperations.FixedTimeEquals(computedMac, presentedMac)
			? ErasureCertificateVerificationResult.Verified
			: ErasureCertificateVerificationResult.SignatureMismatch;
	}

	// Reads "v2:{base64 mac}". Anything else -- an older scheme's bare Base64, a truncated column, an empty
	// string -- is not a tag this framework can check, and is reported as such rather than as a mismatch.
	private static bool TryParseTag(string signature, out byte[] mac)
	{
		mac = [];

		if (string.IsNullOrEmpty(signature))
		{
			return false;
		}

		var separator = signature.IndexOf(':', StringComparison.Ordinal);
		if (separator < 0
			|| !signature.AsSpan(0, separator).SequenceEqual(ErasureCertificateSigner.SchemeTagPrefix))
		{
			return false;
		}

		Span<byte> decoded = stackalloc byte[ErasureCertificateSigner.MacLength];

		if (!Convert.TryFromBase64Chars(signature.AsSpan(separator + 1), decoded, out var written)
			|| written != ErasureCertificateSigner.MacLength)
		{
			return false;
		}

		mac = decoded[..written].ToArray();

		return true;
	}
}
