// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Azure Key Vault implementation of <see cref="IAzureRsaKeyWrapper"/> that wraps and
/// unwraps AES data encryption keys using RSA keys stored in Key Vault.
/// </summary>
/// <remarks>
/// <para>
/// All cryptographic operations are performed server-side in Azure Key Vault.
/// The RSA key material never leaves the vault, providing envelope encryption
/// with HSM-grade protection when using Premium tier.
/// </para>
/// <para>
/// This implementation integrates with the existing <see cref="AzureKeyVaultProvider"/>
/// infrastructure and shares the same Key Vault authentication mechanism.
/// </para>
/// </remarks>
public sealed partial class AzureKeyVaultRsaKeyWrapper : IAzureRsaKeyWrapper, IDisposable
{
	private readonly RsaKeyWrappingOptions _options;
	private readonly ILogger<AzureKeyVaultRsaKeyWrapper> _logger;
	private readonly SemaphoreSlim _rateLimitSemaphore = new(10, 10);
	private readonly AzureKeyVaultOptions _vaultOptions;
	private CryptographyClient? _cryptoClient;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultRsaKeyWrapper"/> class.
	/// </summary>
	/// <param name="options">The RSA key wrapping options.</param>
	/// <param name="vaultOptions">The Azure Key Vault options (provides credential).</param>
	/// <param name="logger">The logger.</param>
	public AzureKeyVaultRsaKeyWrapper(
		IOptions<RsaKeyWrappingOptions> options,
		IOptions<AzureKeyVaultOptions> vaultOptions,
		ILogger<AzureKeyVaultRsaKeyWrapper> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(vaultOptions);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_vaultOptions = vaultOptions.Value;
		_logger = logger;

		if (_options.KeyVaultUrl is null)
		{
			throw new ArgumentException(
				"KeyVaultUrl is required for RSA key wrapping.",
				nameof(options));
		}

		if (string.IsNullOrEmpty(_options.KeyName))
		{
			throw new ArgumentException(
				"KeyName is required for RSA key wrapping.",
				nameof(options));
		}

		LogRsaKeyWrapperInitialized(_options.KeyVaultUrl, _options.KeyName);
	}

	/// <inheritdoc />
	public async Task<byte[]> WrapKeyAsync(byte[] key, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(key);

		if (key.Length == 0)
		{
			throw new ArgumentException("Key data cannot be empty.", nameof(key));
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var client = await GetOrCreateCryptoClientAsync(cancellationToken).ConfigureAwait(false);
			var algorithm = MapAlgorithm(_options.Algorithm);

			var result = await client.WrapKeyAsync(algorithm, key, cancellationToken).ConfigureAwait(false);

			LogKeyWrapped(_options.KeyName!, algorithm.ToString(), key.Length);

			return result.EncryptedKey;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogKeyWrapFailed(ex, _options.KeyName!);
			throw;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> UnwrapKeyAsync(byte[] wrappedKey, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(wrappedKey);

		if (wrappedKey.Length == 0)
		{
			throw new ArgumentException("Wrapped key data cannot be empty.", nameof(wrappedKey));
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var client = await GetOrCreateCryptoClientAsync(cancellationToken).ConfigureAwait(false);
			var algorithm = MapAlgorithm(_options.Algorithm);

			var result = await client.UnwrapKeyAsync(algorithm, wrappedKey, cancellationToken).ConfigureAwait(false);

			LogKeyUnwrapped(_options.KeyName!, algorithm.ToString());

			return result.Key;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogKeyUnwrapFailed(ex, _options.KeyName!);
			throw;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_rateLimitSemaphore.Dispose();
		_disposed = true;

		LogRsaKeyWrapperDisposed();
	}

	private async Task<CryptographyClient> GetOrCreateCryptoClientAsync(CancellationToken cancellationToken)
	{
		if (_cryptoClient is not null)
		{
			return _cryptoClient;
		}

		var credential = _vaultOptions.Credential ?? new DefaultAzureCredential();
		var keyClient = new KeyClient(_options.KeyVaultUrl!, credential);

		KeyVaultKey key;
		if (!string.IsNullOrEmpty(_options.KeyVersion))
		{
			var response = await keyClient.GetKeyAsync(_options.KeyName!, _options.KeyVersion, cancellationToken)
				.ConfigureAwait(false);
			key = response.Value;
		}
		else
		{
			var response = await keyClient.GetKeyAsync(_options.KeyName!, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			key = response.Value;
		}

		_cryptoClient = new CryptographyClient(key.Id, credential);
		return _cryptoClient;
	}

	private static KeyWrapAlgorithm MapAlgorithm(RsaWrappingAlgorithm algorithm) =>
		algorithm switch
		{
			RsaWrappingAlgorithm.RsaOaep => KeyWrapAlgorithm.RsaOaep,
			RsaWrappingAlgorithm.RsaOaep256 => KeyWrapAlgorithm.RsaOaep256,
			_ => KeyWrapAlgorithm.RsaOaep256
		};

	[LoggerMessage(AzureRsaKeyWrappingEventId.RsaKeyWrapperInitialized, LogLevel.Information,
		"AzureKeyVaultRsaKeyWrapper initialized for vault {VaultUrl}, key {KeyName}")]
	private partial void LogRsaKeyWrapperInitialized(Uri vaultUrl, string keyName);

	[LoggerMessage(AzureRsaKeyWrappingEventId.KeyWrapped, LogLevel.Debug,
		"Wrapped key using {KeyName} with algorithm {Algorithm}, input size {KeySize} bytes")]
	private partial void LogKeyWrapped(string keyName, string algorithm, int keySize);

	[LoggerMessage(AzureRsaKeyWrappingEventId.KeyWrapFailed, LogLevel.Error,
		"Failed to wrap key using {KeyName}")]
	private partial void LogKeyWrapFailed(Exception exception, string keyName);

	[LoggerMessage(AzureRsaKeyWrappingEventId.KeyUnwrapped, LogLevel.Debug,
		"Unwrapped key using {KeyName} with algorithm {Algorithm}")]
	private partial void LogKeyUnwrapped(string keyName, string algorithm);

	[LoggerMessage(AzureRsaKeyWrappingEventId.KeyUnwrapFailed, LogLevel.Error,
		"Failed to unwrap key using {KeyName}")]
	private partial void LogKeyUnwrapFailed(Exception exception, string keyName);

	[LoggerMessage(AzureRsaKeyWrappingEventId.RsaKeyWrapperDisposed, LogLevel.Debug,
		"AzureKeyVaultRsaKeyWrapper disposed")]
	private partial void LogRsaKeyWrapperDisposed();
}
