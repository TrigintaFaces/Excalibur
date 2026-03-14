// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides HMAC-based signing and verification for the composite signing service.
/// </summary>
/// <remarks>
/// Supports <see cref="SigningAlgorithm.HMACSHA256"/> and <see cref="SigningAlgorithm.HMACSHA512"/>.
/// Uses <see cref="CryptographicOperations.FixedTimeEquals"/> for constant-time signature comparison
/// to prevent timing attacks (required for symmetric HMAC verification).
/// </remarks>
public sealed class HmacSignatureAlgorithmProvider : ISignatureAlgorithmProvider
{
	/// <inheritdoc />
	public bool SupportsAlgorithm(SigningAlgorithm algorithm)
		=> algorithm is SigningAlgorithm.HMACSHA256 or SigningAlgorithm.HMACSHA512;

	/// <inheritdoc />
	public Task<byte[]> SignAsync(
		byte[] data,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(data);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		var signature = algorithm switch
		{
			SigningAlgorithm.HMACSHA256 => ComputeHmacSha256(data, keyMaterial),
			SigningAlgorithm.HMACSHA512 => ComputeHmacSha512(data, keyMaterial),
			_ => throw new SigningException($"Algorithm {algorithm} is not supported by {nameof(HmacSignatureAlgorithmProvider)}."),
		};

		return Task.FromResult(signature);
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

		var expectedSignature = algorithm switch
		{
			SigningAlgorithm.HMACSHA256 => ComputeHmacSha256(data, keyMaterial),
			SigningAlgorithm.HMACSHA512 => ComputeHmacSha512(data, keyMaterial),
			_ => throw new VerificationException($"Algorithm {algorithm} is not supported by {nameof(HmacSignatureAlgorithmProvider)}."),
		};

		// Constant-time comparison to prevent timing attacks (required for symmetric HMAC)
		var isValid = CryptographicOperations.FixedTimeEquals(expectedSignature, signature);

		return Task.FromResult(isValid);
	}

	private static byte[] ComputeHmacSha256(byte[] data, byte[] key)
	{
		using var hmac = new HMACSHA256(key);
		return hmac.ComputeHash(data);
	}

	private static byte[] ComputeHmacSha512(byte[] data, byte[] key)
	{
		using var hmac = new HMACSHA512(key);
		return hmac.ComputeHash(data);
	}
}
