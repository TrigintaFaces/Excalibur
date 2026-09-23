// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Excalibur.Security;

/// <summary>
/// Provides ECDSA signing and verification for the composite signing service. The curve comes from
/// the supplied key; P-256 is the enforced minimum and anything weaker is rejected at sign and verify.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="ECDsa"/> from <c>System.Security.Cryptography</c> (BCL).
/// A fresh <see cref="ECDsa"/> instance is created per operation to avoid pinning key material
/// in a long-lived object. This matches <c>CryptoProviderFactory</c> behavior.
/// </para>
/// <para>
/// Key formats:
/// <list type="bullet">
/// <item>Signing (private key): PKCS#8 DER via <c>ECDsa.ImportPkcs8PrivateKey</c>.</item>
/// <item>Verification (public key): SubjectPublicKeyInfo DER via <c>ECDsa.ImportSubjectPublicKeyInfo</c>.</item>
/// </list>
/// </para>
/// <para>
/// Signature format: <see cref="DSASignatureFormat.Rfc3279DerSequence"/> (standard X.509/TLS format)
/// for maximum interoperability.
/// </para>
/// </remarks>
public sealed class EcdsaSignatureAlgorithmProvider : ISignatureAlgorithmProvider
{
	/// <summary>
	/// The smallest curve this provider will sign or verify with, in bits.
	/// </summary>
	/// <remarks>
	/// The curve is carried by the consumer's key, not chosen here, so without this floor a P-224 key
	/// would be accepted silently and produce a signature weaker than the documented strength. A
	/// stronger curve than the minimum is accepted: it can only improve on the guarantee.
	/// </remarks>
	private const int MinimumCurveSizeInBits = 256;

	// RFC 5480 §2.1.1.1 named-curve OIDs mapped to field size in bits. Verified against the DER a
	// real `openssl ecparam -genkey` emits for each curve, not transcribed from memory. Only the
	// curves .NET can construct at all are listed; an OID this table doesn't recognize (an unlisted
	// named curve, or explicit curve parameters) falls through to the post-import KeySize check below.
	private static readonly Dictionary<string, int> NamedCurveBitLengths = new(StringComparer.Ordinal)
	{
		["1.2.840.10045.3.1.1"] = 192, // secp192r1 / P-192
		["1.3.132.0.33"] = 224, // secp224r1 / P-224
		["1.2.840.10045.3.1.7"] = 256, // secp256r1 / P-256
		["1.3.132.0.34"] = 384, // secp384r1 / P-384
		["1.3.132.0.35"] = 521, // secp521r1 / P-521
	};

	// Reads the namedCurve OID straight out of the PKCS#8/SubjectPublicKeyInfo DER via the BCL's ASN.1
	// reader -- no platform crypto call is made, so this runs identically on every OS. That matters
	// because macOS/CoreCrypto refuses to even construct an ECDsa over a sub-P-256 curve: without this
	// pre-check, the floor below would never fire there and a weak-curve rejection test would pass for
	// the wrong reason (the platform's refusal, not our floor). Byte parsing has no such platform gap.
	private static void RejectNamedCurveBelowTheMinimum(ReadOnlyMemory<byte> keyMaterial, bool isPkcs8PrivateKey)
	{
		try
		{
			var reader = new AsnReader(keyMaterial, AsnEncodingRules.DER);
			var top = reader.ReadSequence();
			if (isPkcs8PrivateKey)
			{
				_ = top.ReadInteger(); // PrivateKeyInfo.version
			}

			var algorithmIdentifier = top.ReadSequence();
			_ = algorithmIdentifier.ReadObjectIdentifier(); // id-ecPublicKey -- irrelevant to non-EC keys, which never reach this path

			if (!algorithmIdentifier.HasData)
			{
				return; // no curve parameters present -- let the platform importer decide
			}

			var curveOid = algorithmIdentifier.ReadObjectIdentifier();
			if (NamedCurveBitLengths.TryGetValue(curveOid, out var bits) && bits < MinimumCurveSizeInBits)
			{
				throw new CryptographicException(FormatRejectionMessage(bits));
			}
		}
		catch (AsnContentException)
		{
			// Not the DER shape we know how to sniff (e.g. explicit curve parameters instead of a named
			// curve). Fall through to the platform importer and the post-import KeySize check.
		}
	}

	// Second line of defense: catches a weak curve the DER sniff above didn't recognize (explicit
	// parameters, or a named curve absent from the table) but that the platform still imported.
	private static void RejectCurvesBelowTheMinimum(ECDsa ecdsa)
	{
		if (ecdsa.KeySize < MinimumCurveSizeInBits)
		{
			throw new CryptographicException(FormatRejectionMessage(ecdsa.KeySize));
		}
	}

	private static string FormatRejectionMessage(int actualBits) =>
		$"The supplied ECDSA key uses a {actualBits}-bit curve. This provider requires at " +
		$"least {MinimumCurveSizeInBits} bits (P-256 or stronger), because it pairs the signature " +
		"with SHA-256 and a smaller curve would be the weakest link.";

	/// <inheritdoc />
	public bool SupportsAlgorithm(SigningAlgorithm algorithm)
		=> algorithm == SigningAlgorithm.ECDSASHA256;

	/// <inheritdoc />
	public Task<byte[]> SignAsync(
		byte[] data,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		try
		{
			RejectNamedCurveBelowTheMinimum(keyMaterial, isPkcs8PrivateKey: true);
			using var ecdsa = ECDsa.Create();
			ecdsa.ImportPkcs8PrivateKey(keyMaterial, out _);
			RejectCurvesBelowTheMinimum(ecdsa);
			var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256,
				DSASignatureFormat.Rfc3279DerSequence);
			return Task.FromResult(signature);
		}
		catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
		{
			throw new SigningException(
				"ECDSA signing failed. Verify that the key material is a valid PKCS#8 private key on a "
				+ "P-256 or stronger curve.", ex);
		}
	}

	/// <inheritdoc />
	public Task<bool> VerifyAsync(
		byte[] data,
		byte[] signature,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(signature);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		try
		{
			RejectNamedCurveBelowTheMinimum(keyMaterial, isPkcs8PrivateKey: false);
			using var ecdsa = ECDsa.Create();
			ecdsa.ImportSubjectPublicKeyInfo(keyMaterial, out _);
			RejectCurvesBelowTheMinimum(ecdsa);
			var result = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256,
				DSASignatureFormat.Rfc3279DerSequence);
			return Task.FromResult(result);
		}
		catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException)
		{
			throw new VerificationException(
				"ECDSA verification failed. Verify that the key material is a valid SubjectPublicKeyInfo on a "
				+ "P-256 or stronger curve.", ex);
		}
	}
}
