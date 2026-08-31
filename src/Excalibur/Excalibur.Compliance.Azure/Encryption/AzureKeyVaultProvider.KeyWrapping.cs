// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Excalibur.Compliance.Azure;

/// <summary>
/// Envelope key wrapping for <see cref="AzureKeyVaultProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Key Vault does not store AES keys, and it does not export key material at all. This provider therefore
/// creates RSA keys and uses them to wrap and unwrap the symmetric data keys that actually encrypt
/// payloads. Every wrap and unwrap executes inside the vault; the RSA private key never leaves it, and is
/// HSM-protected on the Premium tier.
/// </para>
/// <para>
/// This is what makes Key Vault usable as a production key source: the encryption provider never needs
/// the vault to hand over key bytes, which it would refuse to do.
/// </para>
/// </remarks>
public sealed partial class AzureKeyVaultProvider : IKeyWrappingProvider
{
	private const string WrapAlgorithmName = "RSA-OAEP-256";

	/// <inheritdoc />
	public async Task<WrappedDataKey> WrapDataKeyAsync(
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

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			// The versioned key URI, captured at wrap time. Recording it is what lets a later unwrap reach
			// the exact key version that performed this wrap rather than whichever version is current then,
			// so rotating the key does not strand data the previous version protected.
			var versionedKeyId = response.Value.Id;

			var client = new CryptographyClient(versionedKeyId, _options.Credential ?? new DefaultAzureCredential());

			var result = await client
				.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dataKey, cancellationToken)
				.ConfigureAwait(false);

			return new WrappedDataKey
			{
				CiphertextBlob = result.EncryptedKey,
				WrappingKeyId = versionedKeyId.ToString(),
				Algorithm = WrapAlgorithmName
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not EncryptionException)
		{
			throw new EncryptionException($"Azure Key Vault could not wrap the data key for '{keyId}'.", ex)
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> UnwrapDataKeyAsync(
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

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var credential = _options.Credential ?? new DefaultAzureCredential();

			// Prefer the exact key version recorded at wrap time. Falling back to the current version is
			// only correct for data wrapped before rotation, and it is better to attempt it than to fail
			// outright -- but an unwrap under the wrong version fails in the vault rather than silently
			// producing a wrong key, so this cannot return a substituted key either way.
			CryptographyClient client;
			if (!string.IsNullOrEmpty(wrappedKey.WrappingKeyId))
			{
				client = new CryptographyClient(new Uri(wrappedKey.WrappingKeyId), credential);
			}
			else
			{
				var keyName = GetKeyName(keyId);
				var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
				client = new CryptographyClient(response.Value.Id, credential);
			}

			var result = await client
				.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrappedKey.CiphertextBlob, cancellationToken)
				.ConfigureAwait(false);

			return result.Key;
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not EncryptionException)
		{
			throw new EncryptionException($"Azure Key Vault could not unwrap the data key for '{keyId}'.", ex)
			{
				ErrorCode = EncryptionErrorCode.ServiceUnavailable
			};
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}
}
