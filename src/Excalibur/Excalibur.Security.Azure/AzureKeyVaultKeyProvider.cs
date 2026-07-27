// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using Excalibur.Security.Azure.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Security.Azure;

/// <summary>
/// An <see cref="IKeyProvider"/> backed by Azure Key Vault. Key material is stored as a Key Vault secret
/// (the raw key bytes, base64-encoded), retrieved keys are optionally cached with a bounded TTL, and an
/// unknown key fails closed by throwing a <see cref="SigningException"/> — the retrieval path never mints
/// a substitute key.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class AzureKeyVaultKeyProvider : IKeyProvider
{
	private readonly ILogger<AzureKeyVaultKeyProvider> _logger;
	private readonly ISecretClient _secretClient;
	private readonly AzureKeyVaultKeyProviderOptions _options;
	private readonly TimeProvider _timeProvider;
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultKeyProvider"/> class that talks to the
	/// real Azure Key Vault service, building a <see cref="SecretClient"/> authenticated via
	/// <see cref="DefaultAzureCredential"/> from the configured vault URI.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The provider options.</param>
	/// <param name="timeProvider">The time source used for cache expiry.</param>
	public AzureKeyVaultKeyProvider(
		ILogger<AzureKeyVaultKeyProvider> logger,
		IOptions<AzureKeyVaultKeyProviderOptions> options,
		TimeProvider timeProvider)
		: this(logger, BuildClient(options?.Value), options is not null ? options.Value : null!, timeProvider)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultKeyProvider"/> class with an explicit
	/// <see cref="ISecretClient"/> seam. Used by tests (via <c>InternalsVisibleTo</c>) to drive a fake
	/// client without reflecting on the SDK.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="secretClient">The Key Vault secret client seam.</param>
	/// <param name="options">The provider options.</param>
	/// <param name="timeProvider">The time source used for cache expiry.</param>
	internal AzureKeyVaultKeyProvider(
		ILogger<AzureKeyVaultKeyProvider> logger,
		ISecretClient secretClient,
		AzureKeyVaultKeyProviderOptions options,
		TimeProvider timeProvider)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	/// <inheritdoc />
	public async Task<byte[]> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyId))
		{
			throw new ArgumentException("Key id cannot be null or empty.", nameof(keyId));
		}

		if (TryGetCached(keyId, out var cached))
		{
			LogKeyCacheHit(keyId);
			return cached;
		}

		var secretName = ResolveSecretName(keyId);

		Response<KeyVaultSecret> response;
		try
		{
			response = await _secretClient.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			// Fail closed: an unknown key is never substituted with minted material.
			LogKeyNotFound(keyId);
			throw new SigningException($"Signing key '{keyId}' was not found in Azure Key Vault.", ex);
		}
		catch (Exception ex) when (ex is not SigningException and not ArgumentException and not OperationCanceledException)
		{
			LogKeyOperationFailed(ex, keyId);
			throw new SigningException($"Failed to retrieve signing key '{keyId}' from Azure Key Vault.", ex);
		}

		var key = ExtractKeyMaterial(response?.Value);
		if (key is null || key.Length == 0)
		{
			LogKeyNotFound(keyId);
			throw new SigningException($"Signing key '{keyId}' in Azure Key Vault contained no key material.");
		}

		Cache(keyId, key);
		LogKeyRetrieved(keyId);
		return key;
	}

	/// <inheritdoc />
	public async Task StoreKeyAsync(string keyId, byte[] key, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyId))
		{
			throw new ArgumentException("Key id cannot be null or empty.", nameof(keyId));
		}

		ArgumentNullException.ThrowIfNull(key);
		if (key.Length == 0)
		{
			throw new ArgumentException("Key material cannot be empty.", nameof(key));
		}

		await StoreInternalAsync(keyId, key, cancellationToken).ConfigureAwait(false);
		LogKeyStored(keyId);
	}

	/// <inheritdoc />
	public async Task<byte[]> RotateKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyId))
		{
			throw new ArgumentException("Key id cannot be null or empty.", nameof(keyId));
		}

		var newKey = RandomNumberGenerator.GetBytes(_options.RotatedKeySizeBytes);
		await StoreInternalAsync(keyId, newKey, cancellationToken).ConfigureAwait(false);
		LogKeyRotated(keyId);
		return newKey;
	}

	private async Task StoreInternalAsync(string keyId, byte[] key, CancellationToken cancellationToken)
	{
		var secretName = ResolveSecretName(keyId);

		try
		{
			var secret = new KeyVaultSecret(secretName, Convert.ToBase64String(key))
			{
				Properties =
				{
					ContentType = "application/octet-stream;base64",
					Tags = { ["ManagedBy"] = "Excalibur.Dispatch", ["Purpose"] = "SigningKey" },
				},
			};

			_ = await _secretClient.SetSecretAsync(secret, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
		{
			LogKeyOperationFailed(ex, keyId);
			throw new SigningException($"Failed to store signing key '{keyId}' in Azure Key Vault.", ex);
		}

		// Refresh the cache so a subsequent read observes the new material immediately.
		Cache(keyId, (byte[])key.Clone());
	}

	private static byte[]? ExtractKeyMaterial(KeyVaultSecret? secret)
	{
		if (secret is null || string.IsNullOrEmpty(secret.Value))
		{
			return null;
		}

		// Key material is stored base64-encoded (Key Vault secret values are strings).
		try
		{
			return Convert.FromBase64String(secret.Value);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private string ResolveSecretName(string keyId)
		=> string.IsNullOrEmpty(_options.SecretNamePrefix) ? keyId : _options.SecretNamePrefix + keyId;

	private bool TryGetCached(string keyId, [NotNullWhen(true)] out byte[]? key)
	{
		key = null;
		if (!_options.EnableCache)
		{
			return false;
		}

		if (_cache.TryGetValue(keyId, out var entry))
		{
			if (entry.ExpiresAt > _timeProvider.GetUtcNow())
			{
				key = (byte[])entry.Key.Clone();
				return true;
			}

			_ = _cache.TryRemove(keyId, out _);
		}

		return false;
	}

	private void Cache(string keyId, byte[] key)
	{
		if (!_options.EnableCache)
		{
			return;
		}

		// Bounded cache: skip new entries when full (existing entries still refresh).
		if (!_cache.ContainsKey(keyId) && _cache.Count >= _options.CacheMaxEntries)
		{
			return;
		}

		var expiresAt = _timeProvider.GetUtcNow().AddSeconds(_options.CacheTtlSeconds);
		_cache[keyId] = new CacheEntry(key, expiresAt);
	}

	private static ISecretClient BuildClient(AzureKeyVaultKeyProviderOptions? options)
	{
		if (options is null || string.IsNullOrWhiteSpace(options.VaultUri))
		{
			throw new InvalidOperationException(
				"Azure Key Vault URI is not configured. Set AzureKeyVaultKeyProviderOptions.VaultUri.");
		}

		return new SecretClientAdapter(new SecretClient(new Uri(options.VaultUri), new DefaultAzureCredential()));
	}

	private readonly record struct CacheEntry(byte[] Key, DateTimeOffset ExpiresAt);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyRetrieved, LogLevel.Debug,
		"Retrieved signing key {KeyId} from Azure Key Vault")]
	private partial void LogKeyRetrieved(string keyId);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyCacheHit, LogLevel.Trace,
		"Served signing key {KeyId} from local cache")]
	private partial void LogKeyCacheHit(string keyId);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyNotFound, LogLevel.Warning,
		"Signing key {KeyId} not found in Azure Key Vault (fail-closed)")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyStored, LogLevel.Information,
		"Stored signing key {KeyId} in Azure Key Vault")]
	private partial void LogKeyStored(string keyId);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyRotated, LogLevel.Information,
		"Rotated signing key {KeyId} in Azure Key Vault")]
	private partial void LogKeyRotated(string keyId);

	[LoggerMessage(AzureSecurityEventId.AzureKeyVaultKeyOperationFailed, LogLevel.Error,
		"Azure Key Vault key operation failed for {KeyId}")]
	private partial void LogKeyOperationFailed(Exception ex, string keyId);
}
