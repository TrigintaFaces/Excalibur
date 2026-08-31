// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using VaultSharp.V1.SecretsEngines.Transit;

namespace Excalibur.Compliance.Vault;

/// <summary>
/// Envelope key wrapping for <see cref="VaultKeyProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The Transit secrets engine performs encryption server-side and does not return key material. Wrapping
/// a data key is therefore a Transit encrypt of 32 bytes, and unwrapping is a Transit decrypt; the Vault
/// key itself never leaves the server.
/// </para>
/// <para>
/// Transit ciphertext is a string of the form <c>vault:v1:...</c> whose prefix names the key version that
/// produced it, and Transit selects that version automatically on decrypt. A rotated key therefore still
/// reads data written under earlier versions. The ciphertext is stored here as its UTF-8 bytes so it fits
/// the provider-agnostic wrapped-key shape.
/// </para>
/// </remarks>
public sealed partial class VaultKeyProvider : IKeyWrappingProvider
{
	private const string TransitWrapAlgorithmName = "vault-transit";

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

			var response = await _vaultClient.V1.Secrets.Transit.EncryptAsync(
				keyName,
				new EncryptRequestOptions { Base64EncodedPlainText = Convert.ToBase64String(dataKey) },
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			var ciphertext = response?.Data?.CipherText;

			if (string.IsNullOrEmpty(ciphertext))
			{
				throw new EncryptionException(
					$"Vault Transit returned no ciphertext when wrapping the data key for '{keyId}'.")
				{
					ErrorCode = EncryptionErrorCode.ServiceUnavailable
				};
			}

			return new WrappedDataKey
			{
				CiphertextBlob = Encoding.UTF8.GetBytes(ciphertext),

				// The Transit ciphertext already carries its key version in the "vault:vN:" prefix, so the
				// key name alone is enough to reach the right key on unwrap.
				WrappingKeyId = keyName,
				Algorithm = TransitWrapAlgorithmName
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not EncryptionException)
		{
			throw new EncryptionException($"Vault Transit could not wrap the data key for '{keyId}'.", ex)
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
			var keyName = string.IsNullOrEmpty(wrappedKey.WrappingKeyId)
				? GetKeyName(keyId)
				: wrappedKey.WrappingKeyId;

			var response = await _vaultClient.V1.Secrets.Transit.DecryptAsync(
				keyName,
				new DecryptRequestOptions { CipherText = Encoding.UTF8.GetString(wrappedKey.CiphertextBlob) },
				_options.Keys.TransitMountPath).ConfigureAwait(false);

			var base64 = response?.Data?.Base64EncodedPlainText;

			if (string.IsNullOrEmpty(base64))
			{
				throw new EncryptionException(
					$"Vault Transit returned no plaintext when unwrapping the data key for '{keyId}'.")
				{
					ErrorCode = EncryptionErrorCode.ServiceUnavailable
				};
			}

			return Convert.FromBase64String(base64);
		}
		catch (Exception ex) when (ex is not OperationCanceledException and not EncryptionException)
		{
			throw new EncryptionException($"Vault Transit could not unwrap the data key for '{keyId}'.", ex)
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
