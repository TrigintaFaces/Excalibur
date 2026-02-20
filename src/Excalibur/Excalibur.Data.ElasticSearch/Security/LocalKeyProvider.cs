// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Placeholder implementation of local key provider for development scenarios.
/// </summary>
internal sealed class LocalKeyProvider : IElasticsearchKeyProvider
{
	private readonly Dictionary<string, string> _localSecrets = [];
	private readonly Dictionary<string, SecretMetadata> _localMetadata = [];

	/// <inheritdoc/>
	public event EventHandler<SecretAccessedEventArgs>? SecretAccessed;

	/// <inheritdoc/>
	public event EventHandler<KeyRotatedEventArgs>? KeyRotated;

	/// <inheritdoc/>
	public KeyManagementProviderType ProviderType => KeyManagementProviderType.Local;

	/// <inheritdoc/>
	public bool SupportsHsm => false;

	/// <inheritdoc/>
	public bool SupportsKeyRotation => true;

	/// <inheritdoc/>
	public Task<string?> GetSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Read, DateTimeOffset.UtcNow));
		return Task.FromResult(_localSecrets.GetValueOrDefault(keyName));
	}

	/// <inheritdoc/>
	public Task<bool> SetSecretAsync(string keyName, string secretValue, SecretMetadata? metadata,
		CancellationToken cancellationToken)
	{
		_localSecrets[keyName] = secretValue;
		if (metadata != null)
		{
			_localMetadata[keyName] = metadata;
		}

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Write, DateTimeOffset.UtcNow));
		return Task.FromResult(true);
	}

	/// <inheritdoc/>
	public Task<bool> DeleteSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		var removed = _localSecrets.Remove(keyName);
		_ = _localMetadata.Remove(keyName);
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Delete, DateTimeOffset.UtcNow));
		return Task.FromResult(removed);
	}

	/// <inheritdoc/>
	public Task<bool> SecretExistsAsync(string keyName, CancellationToken cancellationToken) =>
		Task.FromResult(_localSecrets.ContainsKey(keyName));

	/// <inheritdoc/>
	public Task<SecretMetadata?> GetSecretMetadataAsync(string keyName, CancellationToken cancellationToken)
	{
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Metadata, DateTimeOffset.UtcNow));
		return Task.FromResult(_localMetadata.GetValueOrDefault(keyName));
	}

	/// <inheritdoc/>
	public Task<KeyGenerationResult> GenerateEncryptionKeyAsync(string keyName, EncryptionKeyType keyType, int keySize,
		SecretMetadata? metadata, CancellationToken cancellationToken)
	{
		var key = new byte[keySize / 8];
		RandomNumberGenerator.Fill(key);
		var keyData = Convert.ToBase64String(key);

		_localSecrets[keyName] = keyData;
		if (metadata != null)
		{
			_localMetadata[keyName] = metadata;
		}

		return Task.FromResult(KeyGenerationResult.CreateSuccess(keyName, keyType, keySize, "1.0"));
	}

	/// <inheritdoc/>
	public Task<KeyRotationResult> RotateEncryptionKeyAsync(string keyName, CancellationToken cancellationToken)
	{
		if (!_localSecrets.ContainsKey(keyName))
		{
			return Task.FromResult(KeyRotationResult.CreateFailure(keyName, "Key not found"));
		}

		// Generate new key
		var key = new byte[32]; // Default to 256-bit key
		RandomNumberGenerator.Fill(key);
		var newKeyData = Convert.ToBase64String(key);

		const string previousVersion = "1.0";
		const string newVersion = "2.0";

		_localSecrets[keyName] = newKeyData;

		KeyRotated?.Invoke(this, new KeyRotatedEventArgs(keyName, newVersion, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90)));

		return Task.FromResult(KeyRotationResult.CreateSuccess(keyName, newVersion, previousVersion, DateTimeOffset.UtcNow.AddDays(90)));
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(string? prefix, bool includeMetadata,
		CancellationToken cancellationToken)
	{
		var secrets = _localSecrets.Keys
			.Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix, StringComparison.Ordinal))
			.Select(k => new SecretInfo(k, includeMetadata ? _localMetadata.GetValueOrDefault(k) : null))
			.ToList();

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(prefix ?? "*", SecretOperation.List, DateTimeOffset.UtcNow));
		return Task.FromResult<IReadOnlyList<SecretInfo>>(secrets.AsReadOnly());
	}
}
