// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Excalibur.A3.Governance.NonHumanIdentity;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// In-memory API key manager for development and testing. Stores keys as SHA-256 hashes.
/// </summary>
internal sealed partial class InMemoryApiKeyManager(
	IOptions<ApiKeyOptions> options,
	ILogger<InMemoryApiKeyManager> logger) : IApiKeyManager
{
	private readonly ConcurrentDictionary<string, StoredApiKey> _keysById = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, StoredApiKey> _keysByHash = new(StringComparer.Ordinal);
#pragma warning disable IDE0330 // Use 'System.Threading.Lock' -- net8.0 does not have Lock type
	private readonly object _rotationLock = new();
#pragma warning restore IDE0330

	/// <inheritdoc />
	public Task<ApiKeyCreationResult> CreateKeyAsync(ApiKeyRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrEmpty(request.PrincipalId);

		var opts = options.Value;

		// Enforce MaxKeysPerPrincipal
		var activeCount = CountActiveKeys(request.PrincipalId);
		if (activeCount >= opts.MaxKeysPerPrincipal)
		{
			throw new InvalidOperationException(
				$"Principal '{request.PrincipalId}' already has {activeCount} active key(s) (max: {opts.MaxKeysPerPrincipal}).");
		}

		// Generate key
		var keyBytes = RandomNumberGenerator.GetBytes(opts.KeyLengthBytes);
		var plaintextKey = Convert.ToBase64String(keyBytes);
		var hash = ComputeHash(plaintextKey);
		var keyId = Guid.NewGuid().ToString("N");
		var expiresAt = request.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(opts.DefaultExpirationDays);

		var stored = new StoredApiKey(
			KeyId: keyId,
			PrincipalId: request.PrincipalId,
			PrincipalType: request.PrincipalType,
			HashedValue: hash,
			CreatedAt: DateTimeOffset.UtcNow,
			ExpiresAt: expiresAt,
			RevokedAt: null,
			Scopes: request.Scopes,
			Description: request.Description);

		_keysById[keyId] = stored;
		_keysByHash[hash] = stored;

		LogApiKeyCreated(logger, keyId, request.PrincipalId);

		return Task.FromResult(new ApiKeyCreationResult(keyId, plaintextKey, expiresAt));
	}

	/// <inheritdoc />
	public Task RevokeKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		if (_keysById.TryGetValue(keyId, out var stored) && stored.RevokedAt is null)
		{
			var revoked = stored with { RevokedAt = DateTimeOffset.UtcNow };
			_keysById[keyId] = revoked;
			_keysByHash[stored.HashedValue] = revoked;

			LogApiKeyRevoked(logger, keyId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ApiKeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(apiKey);

		var hash = ComputeHash(apiKey);

		if (!_keysByHash.TryGetValue(hash, out var stored))
		{
			LogApiKeyValidationFailed(logger, "Key not found.");
			return Task.FromResult(new ApiKeyValidationResult(false, null, null, null, null, "Key not found."));
		}

		if (stored.RevokedAt is not null)
		{
			LogApiKeyValidationFailed(logger, "Key has been revoked.");
			return Task.FromResult(new ApiKeyValidationResult(false, stored.KeyId, stored.PrincipalId, stored.PrincipalType, null, "Key has been revoked."));
		}

		if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
		{
			LogApiKeyExpired(logger, stored.KeyId);
			return Task.FromResult(new ApiKeyValidationResult(false, stored.KeyId, stored.PrincipalId, stored.PrincipalType, null, "Key has expired."));
		}

		return Task.FromResult(new ApiKeyValidationResult(
			true, stored.KeyId, stored.PrincipalId, stored.PrincipalType, stored.Scopes, null));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ApiKeyMetadata>> GetKeysByPrincipalAsync(string principalId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(principalId);

		var keys = _keysById.Values
			.Where(k => string.Equals(k.PrincipalId, principalId, StringComparison.Ordinal) && k.RevokedAt is null)
			.Select(k => new ApiKeyMetadata(
				k.KeyId, k.PrincipalId, k.PrincipalType, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.Scopes, k.Description))
			.ToList();

		return Task.FromResult<IReadOnlyList<ApiKeyMetadata>>(keys);
	}

	/// <inheritdoc />
	public Task<ApiKeyCreationResult> RotateKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		lock (_rotationLock)
		{
			if (!_keysById.TryGetValue(keyId, out var existing))
			{
				throw new InvalidOperationException($"API key '{keyId}' not found.");
			}

			if (existing.RevokedAt is not null)
			{
				throw new InvalidOperationException($"API key '{keyId}' has already been revoked.");
			}

			// Revoke old key
			var revoked = existing with { RevokedAt = DateTimeOffset.UtcNow };
			_keysById[keyId] = revoked;
			_keysByHash[existing.HashedValue] = revoked;

			// Create new key with same principal, scopes, description
			var opts = options.Value;
			var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(opts.KeyLengthBytes);
			var plaintextKey = Convert.ToBase64String(keyBytes);
			var hash = ComputeHash(plaintextKey);
			var newKeyId = Guid.NewGuid().ToString("N");
			var expiresAt = DateTimeOffset.UtcNow.AddDays(opts.DefaultExpirationDays);

			var newStored = new StoredApiKey(
				KeyId: newKeyId,
				PrincipalId: existing.PrincipalId,
				PrincipalType: existing.PrincipalType,
				HashedValue: hash,
				CreatedAt: DateTimeOffset.UtcNow,
				ExpiresAt: expiresAt,
				RevokedAt: null,
				Scopes: existing.Scopes,
				Description: existing.Description);

			_keysById[newKeyId] = newStored;
			_keysByHash[hash] = newStored;

			LogApiKeyRotated(logger, keyId, newKeyId);

			return Task.FromResult(new ApiKeyCreationResult(newKeyId, plaintextKey, expiresAt));
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType) => null;

	private int CountActiveKeys(string principalId)
	{
		return _keysById.Values.Count(k =>
			string.Equals(k.PrincipalId, principalId, StringComparison.Ordinal)
			&& k.RevokedAt is null
			&& k.ExpiresAt > DateTimeOffset.UtcNow);
	}

	private static string ComputeHash(string plaintextKey)
	{
		var keyBytes = System.Text.Encoding.UTF8.GetBytes(plaintextKey);
		var hashBytes = SHA256.HashData(keyBytes);
		return Convert.ToHexString(hashBytes);
	}

	private sealed record StoredApiKey(
		string KeyId,
		string PrincipalId,
		PrincipalType PrincipalType,
		string HashedValue,
		DateTimeOffset CreatedAt,
		DateTimeOffset ExpiresAt,
		DateTimeOffset? RevokedAt,
		IReadOnlyList<string> Scopes,
		string? Description);

	[LoggerMessage(EventId = 3570, Level = LogLevel.Information, Message = "API key '{KeyId}' created for principal '{PrincipalId}'.")]
	private static partial void LogApiKeyCreated(ILogger logger, string keyId, string principalId);

	[LoggerMessage(EventId = 3571, Level = LogLevel.Information, Message = "API key '{KeyId}' revoked.")]
	private static partial void LogApiKeyRevoked(ILogger logger, string keyId);

	[LoggerMessage(EventId = 3572, Level = LogLevel.Information, Message = "API key '{OldKeyId}' rotated to '{NewKeyId}'.")]
	private static partial void LogApiKeyRotated(ILogger logger, string oldKeyId, string newKeyId);

	[LoggerMessage(EventId = 3573, Level = LogLevel.Debug, Message = "API key validation failed: {Reason}")]
	private static partial void LogApiKeyValidationFailed(ILogger logger, string reason);

	[LoggerMessage(EventId = 3574, Level = LogLevel.Debug, Message = "API key '{KeyId}' has expired.")]
	private static partial void LogApiKeyExpired(ILogger logger, string keyId);
}
