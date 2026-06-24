// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Security.Cryptography;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Placeholder implementation of local key provider for development scenarios.
/// </summary>
/// <remarks>
/// Retains every key version produced by generation and rotation so that ciphertext encrypted under an earlier version
/// remains decryptable after rotation. Rotation only ever ADDS a new version; it never overwrites or discards a prior
/// version's secret material.
/// </remarks>
internal sealed class LocalKeyProvider : IElasticsearchKeyProvider
{
	private const string InitialVersion = "1";

	// keyName -> (version -> secret). Every version ever produced is retained.
	private readonly Dictionary<string, Dictionary<string, string>> _versionedSecrets = new(StringComparer.Ordinal);

	// keyName -> current version identifier.
	private readonly Dictionary<string, string> _currentVersions = new(StringComparer.Ordinal);

	private readonly Dictionary<string, SecretMetadata> _localMetadata = new(StringComparer.Ordinal);

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
		return Task.FromResult(GetCurrentSecret(keyName));
	}

	/// <inheritdoc/>
	public Task<string?> GetSecretVersionAsync(string keyName, string version, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		if (string.IsNullOrEmpty(version))
		{
			throw new ArgumentException("Version cannot be null or empty", nameof(version));
		}

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Read, DateTimeOffset.UtcNow));

		var secret = _versionedSecrets.TryGetValue(keyName, out var versions)
			? versions.GetValueOrDefault(version)
			: null;
		return Task.FromResult(secret);
	}

	/// <inheritdoc/>
	public Task<string?> GetCurrentVersionAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		return Task.FromResult(_currentVersions.GetValueOrDefault(keyName));
	}

	/// <inheritdoc/>
	public Task<bool> SetSecretAsync(string keyName, string secretValue, SecretMetadata? metadata,
		CancellationToken cancellationToken)
	{
		// Setting a secret directly establishes (or replaces) the current version while retaining prior versions.
		StoreNewVersion(keyName, secretValue);
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
		var removed = _versionedSecrets.Remove(keyName);
		_ = _currentVersions.Remove(keyName);
		_ = _localMetadata.Remove(keyName);
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Delete, DateTimeOffset.UtcNow));
		return Task.FromResult(removed);
	}

	/// <inheritdoc/>
	public Task<bool> SecretExistsAsync(string keyName, CancellationToken cancellationToken) =>
		Task.FromResult(_versionedSecrets.ContainsKey(keyName));

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

		var version = StoreNewVersion(keyName, keyData);
		if (metadata != null)
		{
			_localMetadata[keyName] = metadata;
		}

		return Task.FromResult(KeyGenerationResult.CreateSuccess(keyName, keyType, keySize, version));
	}

	/// <inheritdoc/>
	public Task<KeyRotationResult> RotateEncryptionKeyAsync(string keyName, CancellationToken cancellationToken)
	{
		if (!_versionedSecrets.ContainsKey(keyName) || !_currentVersions.TryGetValue(keyName, out var previousVersion))
		{
			return Task.FromResult(KeyRotationResult.CreateFailure(keyName, "Key not found"));
		}

		// Generate new key material and ADD it as a new version. The prior version's secret is retained so pre-rotation
		// ciphertext stays decryptable.
		var key = new byte[32]; // Default to 256-bit key
		RandomNumberGenerator.Fill(key);
		var newKeyData = Convert.ToBase64String(key);

		var newVersion = StoreNewVersion(keyName, newKeyData);

		KeyRotated?.Invoke(this, new KeyRotatedEventArgs(keyName, newVersion, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90)));

		return Task.FromResult(KeyRotationResult.CreateSuccess(keyName, newVersion, previousVersion, DateTimeOffset.UtcNow.AddDays(90)));
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(string? prefix, bool includeMetadata,
		CancellationToken cancellationToken)
	{
		var secrets = _versionedSecrets.Keys
			.Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix, StringComparison.Ordinal))
			.Select(k => new SecretInfo(k, includeMetadata ? _localMetadata.GetValueOrDefault(k) : null))
			.ToList();

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(prefix ?? "*", SecretOperation.List, DateTimeOffset.UtcNow));
		return Task.FromResult<IReadOnlyList<SecretInfo>>(secrets.AsReadOnly());
	}

	/// <summary>
	/// Returns the secret for the current version of a key, or null if the key does not exist.
	/// </summary>
	private string? GetCurrentSecret(string keyName)
	{
		if (!_currentVersions.TryGetValue(keyName, out var version) ||
			!_versionedSecrets.TryGetValue(keyName, out var versions))
		{
			return null;
		}

		return versions.GetValueOrDefault(version);
	}

	/// <summary>
	/// Stores a new secret value as the next version of a key and makes it current, retaining all prior versions.
	/// </summary>
	/// <returns> The version identifier assigned to the stored secret. </returns>
	private string StoreNewVersion(string keyName, string secretValue)
	{
		if (!_versionedSecrets.TryGetValue(keyName, out var versions))
		{
			versions = new Dictionary<string, string>(StringComparer.Ordinal);
			_versionedSecrets[keyName] = versions;
		}

		var nextVersion = ComputeNextVersion(keyName);
		versions[nextVersion] = secretValue;
		_currentVersions[keyName] = nextVersion;
		return nextVersion;
	}

	/// <summary>
	/// Computes the next monotonically increasing numeric version identifier for a key.
	/// </summary>
	private string ComputeNextVersion(string keyName)
	{
		if (!_currentVersions.TryGetValue(keyName, out var current) ||
			!int.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentNumber))
		{
			return InitialVersion;
		}

		return (currentNumber + 1).ToString(CultureInfo.InvariantCulture);
	}
}
