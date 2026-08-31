// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Aws;

/// <summary>
/// Envelope key wrapping for <see cref="AwsKmsProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// AWS KMS never returns the key material of a customer managed key. It does encrypt and decrypt
/// small payloads under that key, which is all wrapping a data key requires: the 32-byte data key
/// goes to KMS, KMS returns it encrypted, and only the encrypted form is persisted.
/// </para>
/// <para>
/// KMS rotates the backing material of a key internally, under the same key identifier, and picks
/// the correct backing key on decrypt from the ciphertext itself. The version argument is therefore
/// unused here, consistent with how this provider treats AWS keys elsewhere: KMS does not expose
/// historical versions as separately addressable keys.
/// </para>
/// <para>
/// The KMS request and response types live in <see cref="AwsKmsDataKeyWrapper"/> rather than here,
/// to keep this already-large provider within its class-coupling limit.
/// </para>
/// </remarks>
public sealed partial class AwsKmsProvider : IKeyWrappingProvider
{
	/// <inheritdoc />
	public Task<WrappedDataKey> WrapDataKeyAsync(
		string keyId,
		int version,
		byte[] dataKey,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentNullException.ThrowIfNull(dataKey);

		if (dataKey.Length == 0)
		{
			throw new ArgumentException("The data key cannot be empty.", nameof(dataKey));
		}

		return AwsKmsDataKeyWrapper.WrapAsync(
			_kmsClient, _options.BuildKeyAlias(keyId), dataKey, cancellationToken);
	}

	/// <inheritdoc />
	public Task<byte[]> UnwrapDataKeyAsync(
		string keyId,
		int version,
		WrappedDataKey wrappedKey,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentNullException.ThrowIfNull(wrappedKey);

		if (wrappedKey.CiphertextBlob.Length == 0)
		{
			throw new ArgumentException("The wrapped data key cannot be empty.", nameof(wrappedKey));
		}

		// Prefer the exact key recorded at wrap time; fall back to the alias for rows written before
		// that was captured. Either way KMS verifies the ciphertext against the named key.
		var keyReference = string.IsNullOrEmpty(wrappedKey.WrappingKeyId)
			? _options.BuildKeyAlias(keyId)
			: wrappedKey.WrappingKeyId;

		return AwsKmsDataKeyWrapper.UnwrapAsync(
			_kmsClient, keyReference, wrappedKey.CiphertextBlob, cancellationToken);
	}
}
