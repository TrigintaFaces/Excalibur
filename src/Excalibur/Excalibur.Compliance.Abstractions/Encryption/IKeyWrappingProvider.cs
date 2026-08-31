// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Wraps and unwraps a locally-generated data encryption key (DEK) under a key the provider holds,
/// without that key ever leaving the provider.
/// </summary>
/// <remarks>
/// <para>
/// This is the production key path for encryption providers. A key management service — a cloud KMS or
/// an HSM — deliberately does not export key bytes; that non-export property is the reason to use one.
/// An encryption provider therefore cannot obtain raw master-key material from such a service, and a
/// provider that demands raw material can only ever be satisfied by an in-process key holder whose keys
/// do not survive a restart.
/// </para>
/// <para>
/// Envelope encryption resolves this. The caller generates a single-use DEK from a cryptographic random
/// source, encrypts the payload with it locally, and asks the key service to <em>wrap</em> the DEK. Only
/// the wrapped DEK is persisted. To decrypt, the caller asks the key service to unwrap it. The service
/// performs the wrap and unwrap internally and never discloses the wrapping key, so the guarantee the
/// service exists to provide is preserved rather than defeated. This is the construction used by
/// <c>Microsoft.AspNetCore.DataProtection</c> key ring encryption, Azure Key Vault
/// <c>WrapKey</c>/<c>UnwrapKey</c>, and AWS KMS <c>GenerateDataKey</c>.
/// </para>
/// <para>
/// This is an optional capability. A provider advertises it by answering for it from
/// <see cref="IServiceProvider.GetService(Type)"/>; consumers query for it rather than casting, so the
/// capability remains discoverable through decorators. A provider may supply this capability, raw key
/// material, both, or neither.
/// </para>
/// <para>
/// Implementations MUST NOT log, cache, or otherwise retain the plaintext DEK, and MUST fail closed:
/// an unwrap that cannot be completed, or whose integrity check fails, throws rather than returning a
/// substituted or empty key.
/// </para>
/// </remarks>
public interface IKeyWrappingProvider
{
	/// <summary>
	/// Wraps a data encryption key under the specified key version.
	/// </summary>
	/// <param name="keyId"> The identifier of the wrapping key held by this provider. </param>
	/// <param name="version"> The version of the wrapping key to use. </param>
	/// <param name="dataKey">
	/// The plaintext data encryption key to wrap. The caller retains ownership and is responsible for
	/// clearing it; implementations MUST NOT retain a reference beyond the call.
	/// </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The wrapped key, suitable for persistence alongside the ciphertext it protects. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="keyId"/> or <paramref name="dataKey"/> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="keyId"/> is empty or <paramref name="dataKey"/> is empty. </exception>
	/// <exception cref="EncryptionException"> Thrown when the provider cannot complete the wrap. </exception>
	Task<WrappedDataKey> WrapDataKeyAsync(
		string keyId,
		int version,
		byte[] dataKey,
		CancellationToken cancellationToken);

	/// <summary>
	/// Unwraps a data encryption key previously produced by <see cref="WrapDataKeyAsync"/>.
	/// </summary>
	/// <param name="keyId"> The identifier of the wrapping key held by this provider. </param>
	/// <param name="version"> The version of the wrapping key that produced <paramref name="wrappedKey"/>. </param>
	/// <param name="wrappedKey"> The wrapped key to unwrap. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns>
	/// The plaintext data encryption key. The caller owns the returned array and SHOULD clear it with
	/// <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/> once finished.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="keyId"/> or <paramref name="wrappedKey"/> is null. </exception>
	/// <exception cref="EncryptionException">
	/// Thrown when the wrapped key cannot be unwrapped — the wrapping key is unavailable, destroyed, or
	/// does not match. Implementations MUST fail rather than return a key that did not round-trip.
	/// </exception>
	Task<byte[]> UnwrapDataKeyAsync(
		string keyId,
		int version,
		WrappedDataKey wrappedKey,
		CancellationToken cancellationToken);
}

/// <summary>
/// A data encryption key that has been wrapped by an <see cref="IKeyWrappingProvider"/>, together with
/// the information needed to unwrap it later.
/// </summary>
/// <remarks>
/// The wrapped key is safe to persist next to the ciphertext it protects: it is only usable by a caller
/// that can reach the key service holding the wrapping key.
/// </remarks>
public sealed record WrappedDataKey
{
	/// <summary>
	/// Gets the opaque wrapped key bytes as returned by the key service.
	/// </summary>
	/// <remarks>
	/// The internal structure is the provider's own and MUST be treated as opaque by callers. Providers
	/// commonly embed the wrapping key identity in these bytes.
	/// </remarks>
	public required byte[] CiphertextBlob { get; init; }

	/// <summary>
	/// Gets the provider-side identity of the exact key that performed the wrap, when the provider
	/// exposes one.
	/// </summary>
	/// <value>
	/// A provider-specific key identifier — for example a versioned key URI — or <see langword="null"/>
	/// when the provider embeds the identity in <see cref="CiphertextBlob"/> instead.
	/// </value>
	/// <remarks>
	/// Recorded so that an unwrap resolves the same key version that performed the wrap, rather than
	/// whichever version is current at read time. A rotated key must still be able to read data it wrote.
	/// </remarks>
	public string? WrappingKeyId { get; init; }

	/// <summary>
	/// Gets the provider-side wrapping algorithm, when the provider names one.
	/// </summary>
	/// <value> A provider-specific algorithm identifier, or <see langword="null"/> when not applicable. </value>
	public string? Algorithm { get; init; }
}
