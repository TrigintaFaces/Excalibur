// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// In-memory implementation of the Elasticsearch connection-credential store, for development scenarios.
/// </summary>
/// <remarks>
/// This is not encryption-key custody: it stores opaque connection secrets (OAuth tokens, service-account
/// secrets, passwords, API keys) in memory, lost on restart. Field-level encryption keys are provisioned
/// through <c>Excalibur.Compliance</c>'s <see cref="Excalibur.Compliance.IKeyManagementProvider"/> instead.
/// </remarks>
internal sealed class LocalKeyProvider : IElasticsearchKeyProvider
{
	private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
	private readonly Dictionary<string, SecretMetadata> _metadata = new(StringComparer.Ordinal);

	/// <inheritdoc/>
	public event EventHandler<SecretAccessedEventArgs>? SecretAccessed;

	/// <inheritdoc/>
	public Task<string?> GetSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Read, DateTimeOffset.UtcNow));
		return Task.FromResult(_secrets.GetValueOrDefault(keyName));
	}

	/// <inheritdoc/>
	public Task<bool> SetSecretAsync(string keyName, string secretValue, SecretMetadata? metadata,
		CancellationToken cancellationToken)
	{
		_secrets[keyName] = secretValue;
		if (metadata != null)
		{
			_metadata[keyName] = metadata;
		}

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Write, DateTimeOffset.UtcNow));
		return Task.FromResult(true);
	}

	/// <inheritdoc/>
	public Task<bool> DeleteSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		var removed = _secrets.Remove(keyName);
		_ = _metadata.Remove(keyName);
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Delete, DateTimeOffset.UtcNow));
		return Task.FromResult(removed);
	}

	/// <inheritdoc/>
	public Task<bool> SecretExistsAsync(string keyName, CancellationToken cancellationToken) =>
		Task.FromResult(_secrets.ContainsKey(keyName));

	/// <inheritdoc/>
	public Task<SecretMetadata?> GetSecretMetadataAsync(string keyName, CancellationToken cancellationToken)
	{
		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(keyName, SecretOperation.Metadata, DateTimeOffset.UtcNow));
		return Task.FromResult(_metadata.GetValueOrDefault(keyName));
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(string? prefix, bool includeMetadata,
		CancellationToken cancellationToken)
	{
		var secrets = _secrets.Keys
			.Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix, StringComparison.Ordinal))
			.Select(k => new SecretInfo(k, includeMetadata ? _metadata.GetValueOrDefault(k) : null))
			.ToList();

		SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(prefix ?? "*", SecretOperation.List, DateTimeOffset.UtcNow));
		return Task.FromResult<IReadOnlyList<SecretInfo>>(secrets.AsReadOnly());
	}
}
