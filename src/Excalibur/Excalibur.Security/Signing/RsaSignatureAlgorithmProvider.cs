// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;

namespace Excalibur.Security;

/// <summary>
/// Provides RSA signing and verification (RSASSA-PKCS1-v1_5 and RSASSA-PSS, SHA-256) for the composite
/// signing service.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="RSA"/> from <c>System.Security.Cryptography</c> (BCL). A fresh <see cref="RSA"/> instance
/// is created per operation to avoid pinning key material in a long-lived object (matching
/// <see cref="EcdsaSignatureAlgorithmProvider"/>).
/// </para>
/// <para>
/// Key formats:
/// <list type="bullet">
/// <item>Signing (private key): PKCS#8 DER via <c>RSA.ImportPkcs8PrivateKey</c>.</item>
/// <item>Verification (public key): SubjectPublicKeyInfo DER via <c>RSA.ImportSubjectPublicKeyInfo</c>.</item>
/// </list>
/// </para>
/// <para>
/// Padding: <see cref="SigningAlgorithm.RSASHA256"/> uses <see cref="RSASignaturePadding.Pkcs1"/>;
/// <see cref="SigningAlgorithm.RSAPSSSHA256"/> uses <see cref="RSASignaturePadding.Pss"/>.
/// </para>
/// </remarks>
public sealed class RsaSignatureAlgorithmProvider : ISignatureAlgorithmProvider
{
	/// <inheritdoc />
	public bool SupportsAlgorithm(SigningAlgorithm algorithm)
		=> algorithm is SigningAlgorithm.RSASHA256 or SigningAlgorithm.RSAPSSSHA256;

	/// <inheritdoc />
	public Task<byte[]> SignAsync(
		byte[] data,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		var padding = PaddingFor(algorithm);

		try
		{
			using var rsa = RSA.Create();
			rsa.ImportPkcs8PrivateKey(keyMaterial, out _);
			var signature = rsa.SignData(data, HashAlgorithmName.SHA256, padding);
			return Task.FromResult(signature);
		}
		catch (CryptographicException ex)
		{
			throw new SigningException("RSA signing failed. Verify that the key material is a valid PKCS#8 private key.", ex);
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

		var padding = PaddingFor(algorithm);

		try
		{
			using var rsa = RSA.Create();
			rsa.ImportSubjectPublicKeyInfo(keyMaterial, out _);
			var result = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, padding);
			return Task.FromResult(result);
		}
		catch (CryptographicException ex)
		{
			throw new VerificationException("RSA verification failed. Verify that the key material is a valid SubjectPublicKeyInfo.", ex);
		}
	}

	private static RSASignaturePadding PaddingFor(SigningAlgorithm algorithm)
		=> algorithm switch
		{
			SigningAlgorithm.RSASHA256 => RSASignaturePadding.Pkcs1,
			SigningAlgorithm.RSAPSSSHA256 => RSASignaturePadding.Pss,
			_ => throw new SigningException($"RSA provider does not support algorithm '{algorithm}'."),
		};
}
