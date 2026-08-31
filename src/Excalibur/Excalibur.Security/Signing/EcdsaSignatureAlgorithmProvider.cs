// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;

namespace Excalibur.Security;

/// <summary>
/// Provides ECDSA P-256 signing and verification for the composite signing service.
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

	// Some platforms (macOS/CoreCrypto) refuse to import a sub-minimum curve at all, raising
	// PlatformNotSupportedException before the floor below is ever reached. Both catch blocks funnel it
	// into the same signing/verification failure so a weak key is refused identically everywhere.
	private static void RejectCurvesBelowTheMinimum(ECDsa ecdsa)
	{
		if (ecdsa.KeySize < MinimumCurveSizeInBits)
		{
			throw new CryptographicException(
				$"The supplied ECDSA key uses a {ecdsa.KeySize}-bit curve. This provider requires at " +
				$"least {MinimumCurveSizeInBits} bits (P-256 or stronger), because it pairs the signature " +
				"with SHA-256 and a smaller curve would be the weakest link.");
		}
	}

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
