// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Extension methods for <see cref="IKeyProvider"/> to support asymmetric key pair storage.
/// </summary>
public static class KeyProviderExtensions
{
	/// <summary>
	/// Stores both the private and public key for an asymmetric key pair.
	/// </summary>
	/// <remarks>
	/// The private key is stored under <paramref name="keyId"/> and the public key is stored
	/// under <c>{keyId}:pub</c>, following the asymmetric key convention used by
	/// <see cref="CompositeMessageSigningService"/>.
	/// </remarks>
	/// <param name="provider">The key provider.</param>
	/// <param name="keyId">The key identifier for the key pair.</param>
	/// <param name="privateKey">The private key material (PKCS#8 DER for ECDSA).</param>
	/// <param name="publicKey">The public key material (SubjectPublicKeyInfo DER for ECDSA).</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when both keys are stored.</returns>
	public static async Task StoreKeyPairAsync(
		this IKeyProvider provider,
		string keyId,
		byte[] privateKey,
		byte[] publicKey,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(privateKey);
		ArgumentNullException.ThrowIfNull(publicKey);

		await provider.StoreKeyAsync(keyId, privateKey, cancellationToken).ConfigureAwait(false);
		await provider.StoreKeyAsync($"{keyId}:pub", publicKey, cancellationToken).ConfigureAwait(false);
	}
}
