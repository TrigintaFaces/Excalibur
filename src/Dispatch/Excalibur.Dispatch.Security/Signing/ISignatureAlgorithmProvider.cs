// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides algorithm-specific cryptographic signing and verification operations.
/// </summary>
/// <remarks>
/// <para>
/// Implementations handle the raw cryptographic operations for a specific algorithm family.
/// The <see cref="CompositeMessageSigningService"/> delegates to the appropriate provider
/// based on the <see cref="SigningAlgorithm"/> specified in the <see cref="SigningContext"/>.
/// </para>
/// <para>
/// This follows the pattern from <c>Microsoft.IdentityModel.Tokens.CryptoProviderFactory</c>
/// combined with <c>SignatureProvider</c>, where a factory resolves algorithm-specific providers.
/// Quality gate: 3 methods (limit: 5).
/// </para>
/// </remarks>
public interface ISignatureAlgorithmProvider
{
	/// <summary>
	/// Returns whether this provider supports the specified algorithm.
	/// </summary>
	/// <param name="algorithm">The signing algorithm to check.</param>
	/// <returns><see langword="true"/> if this provider can handle the algorithm; otherwise, <see langword="false"/>.</returns>
	bool SupportsAlgorithm(SigningAlgorithm algorithm);

	/// <summary>
	/// Signs data using the specified algorithm and key material.
	/// </summary>
	/// <param name="data">The data to sign.</param>
	/// <param name="keyMaterial">The key material for signing.</param>
	/// <param name="algorithm">The signing algorithm to use.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the signature bytes.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="keyMaterial"/> is <see langword="null"/>.</exception>
	/// <exception cref="SigningException">Thrown when the signing operation fails.</exception>
	Task<byte[]> SignAsync(
		byte[] data,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies a signature against data using the specified algorithm and key material.
	/// </summary>
	/// <param name="data">The data that was signed.</param>
	/// <param name="signature">The signature to verify.</param>
	/// <param name="keyMaterial">The key material for verification.</param>
	/// <param name="algorithm">The signing algorithm that was used.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with <see langword="true"/> if the signature is valid; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/>, <paramref name="signature"/>, or <paramref name="keyMaterial"/> is <see langword="null"/>.</exception>
	/// <exception cref="VerificationException">Thrown when the verification operation fails.</exception>
	Task<bool> VerifyAsync(
		byte[] data,
		byte[] signature,
		byte[] keyMaterial,
		SigningAlgorithm algorithm,
		CancellationToken cancellationToken);
}
