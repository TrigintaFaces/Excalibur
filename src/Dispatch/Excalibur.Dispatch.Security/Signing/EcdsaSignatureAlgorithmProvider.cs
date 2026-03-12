// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;

namespace Excalibur.Dispatch.Security;

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
/// <item>Signing (private key): PKCS#8 DER via <see cref="ECDsa.ImportPkcs8PrivateKey"/>.</item>
/// <item>Verification (public key): SubjectPublicKeyInfo DER via <see cref="ECDsa.ImportSubjectPublicKeyInfo"/>.</item>
/// </list>
/// </para>
/// <para>
/// Signature format: <see cref="DSASignatureFormat.Rfc3279DerSequence"/> (standard X.509/TLS format)
/// for maximum interoperability.
/// </para>
/// </remarks>
public sealed class EcdsaSignatureAlgorithmProvider : ISignatureAlgorithmProvider
{
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
			var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256,
				DSASignatureFormat.Rfc3279DerSequence);
			return Task.FromResult(signature);
		}
		catch (CryptographicException ex)
		{
			throw new SigningException("ECDSA signing failed. Verify that the key material is a valid PKCS#8 private key.", ex);
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
			var result = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256,
				DSASignatureFormat.Rfc3279DerSequence);
			return Task.FromResult(result);
		}
		catch (CryptographicException ex)
		{
			throw new VerificationException("ECDSA verification failed. Verify that the key material is a valid SubjectPublicKeyInfo.", ex);
		}
	}
}
