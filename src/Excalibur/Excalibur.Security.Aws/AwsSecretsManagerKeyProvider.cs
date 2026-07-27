// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Security.Aws;

/// <summary>
/// An <see cref="IKeyProvider"/> backed by AWS Secrets Manager. Key material is stored as the
/// secret's binary payload (never a plaintext string), retrieved keys are optionally cached with a
/// bounded TTL, and an unknown key fails closed by throwing a <see cref="SigningException"/> — the
/// retrieval path never mints a substitute key.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container")]
internal sealed partial class AwsSecretsManagerKeyProvider : IKeyProvider, IDisposable
{
	private readonly ILogger<AwsSecretsManagerKeyProvider> _logger;
	private readonly IAmazonSecretsManager _client;
	private readonly AwsSecretsManagerKeyProviderOptions _options;
	private readonly TimeProvider _timeProvider;
	private readonly bool _ownsClient;
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSecretsManagerKeyProvider"/> class that
	/// talks to the real AWS Secrets Manager service, building the client from the configured region.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The provider options.</param>
	/// <param name="timeProvider">The time source used for cache expiry.</param>
	public AwsSecretsManagerKeyProvider(
		ILogger<AwsSecretsManagerKeyProvider> logger,
		IOptions<AwsSecretsManagerKeyProviderOptions> options,
		TimeProvider timeProvider)
		: this(logger, BuildClient(options?.Value), options is not null ? options.Value : null!, timeProvider, ownsClient: true)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSecretsManagerKeyProvider"/> class with an
	/// explicit <see cref="IAmazonSecretsManager"/> seam. Used by tests (via <c>InternalsVisibleTo</c>)
	/// to drive a fake client; the supplied client is not disposed by this provider.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="client">The AWS Secrets Manager client seam.</param>
	/// <param name="options">The provider options.</param>
	/// <param name="timeProvider">The time source used for cache expiry.</param>
	internal AwsSecretsManagerKeyProvider(
		ILogger<AwsSecretsManagerKeyProvider> logger,
		IAmazonSecretsManager client,
		AwsSecretsManagerKeyProviderOptions options,
		TimeProvider timeProvider)
		: this(logger, client, options, timeProvider, ownsClient: false)
	{
	}

	private AwsSecretsManagerKeyProvider(
		ILogger<AwsSecretsManagerKeyProvider> logger,
		IAmazonSecretsManager client,
		AwsSecretsManagerKeyProviderOptions options,
		TimeProvider timeProvider,
		bool ownsClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_ownsClient = ownsClient;
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

		var secretId = ResolveSecretName(keyId);

		GetSecretValueResponse response;
		try
		{
			response = await _client.GetSecretValueAsync(
				new GetSecretValueRequest { SecretId = secretId }, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException ex)
		{
			// Fail closed: an unknown key is never substituted with minted material.
			LogKeyNotFound(keyId);
			throw new SigningException($"Signing key '{keyId}' was not found in AWS Secrets Manager.", ex);
		}
		catch (Exception ex) when (ex is not SigningException and not ArgumentException and not OperationCanceledException)
		{
			LogKeyOperationFailed(ex, keyId);
			throw new SigningException($"Failed to retrieve signing key '{keyId}' from AWS Secrets Manager.", ex);
		}

		var key = ExtractKeyMaterial(response);
		if (key is null || key.Length == 0)
		{
			LogKeyNotFound(keyId);
			throw new SigningException($"Signing key '{keyId}' in AWS Secrets Manager contained no key material.");
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
		var secretId = ResolveSecretName(keyId);

		try
		{
			using var putStream = new MemoryStream(key, writable: false);
			try
			{
				_ = await _client.PutSecretValueAsync(
					new PutSecretValueRequest { SecretId = secretId, SecretBinary = putStream },
					cancellationToken).ConfigureAwait(false);
			}
			catch (ResourceNotFoundException)
			{
				// The secret does not exist yet — create it (idempotent store-or-update).
				using var createStream = new MemoryStream(key, writable: false);
				_ = await _client.CreateSecretAsync(
					new CreateSecretRequest { Name = secretId, SecretBinary = createStream },
					cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException)
		{
			LogKeyOperationFailed(ex, keyId);
			throw new SigningException($"Failed to store signing key '{keyId}' in AWS Secrets Manager.", ex);
		}

		// Refresh the cache so a subsequent read observes the new material immediately.
		Cache(keyId, (byte[])key.Clone());
	}

	private static byte[]? ExtractKeyMaterial(GetSecretValueResponse response)
	{
		if (response.SecretBinary is { } binary)
		{
			return binary.ToArray();
		}

		// A secret stored as a string is interpreted as base64-encoded key material.
		if (!string.IsNullOrEmpty(response.SecretString))
		{
			try
			{
				return Convert.FromBase64String(response.SecretString);
			}
			catch (FormatException)
			{
				return null;
			}
		}

		return null;
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

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsClient)
		{
			_client.Dispose();
		}
	}

	private static IAmazonSecretsManager BuildClient(AwsSecretsManagerKeyProviderOptions? options)
	{
		var region = options?.Region;
		return string.IsNullOrWhiteSpace(region)
			? new AmazonSecretsManagerClient()
			: new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
	}

	private readonly record struct CacheEntry(byte[] Key, DateTimeOffset ExpiresAt);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyRetrieved, LogLevel.Debug,
		"Retrieved signing key {KeyId} from AWS Secrets Manager")]
	private partial void LogKeyRetrieved(string keyId);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyCacheHit, LogLevel.Trace,
		"Served signing key {KeyId} from local cache")]
	private partial void LogKeyCacheHit(string keyId);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyNotFound, LogLevel.Warning,
		"Signing key {KeyId} not found in AWS Secrets Manager (fail-closed)")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyStored, LogLevel.Information,
		"Stored signing key {KeyId} in AWS Secrets Manager")]
	private partial void LogKeyStored(string keyId);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyRotated, LogLevel.Information,
		"Rotated signing key {KeyId} in AWS Secrets Manager")]
	private partial void LogKeyRotated(string keyId);

	[LoggerMessage(AwsSecurityEventId.AwsSecretsManagerKeyOperationFailed, LogLevel.Error,
		"AWS Secrets Manager key operation failed for {KeyId}")]
	private partial void LogKeyOperationFailed(Exception ex, string keyId);
}
