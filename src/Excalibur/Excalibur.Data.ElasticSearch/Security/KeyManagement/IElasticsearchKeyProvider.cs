// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for secure key and secret management providers that integrate with external key management systems for
/// Elasticsearch security operations.
/// </summary>
public interface IElasticsearchKeyProvider
{
	/// <summary>
	/// Occurs when a secret is accessed, for audit and monitoring purposes.
	/// </summary>
	event EventHandler<SecretAccessedEventArgs>? SecretAccessed;

	/// <summary>
	/// Occurs when a key rotation is completed successfully.
	/// </summary>
	event EventHandler<KeyRotatedEventArgs>? KeyRotated;

	/// <summary>
	/// Gets the provider type for this key management implementation.
	/// </summary>
	/// <value> The type of key management provider. </value>
	KeyManagementProviderType ProviderType { get; }

	/// <summary>
	/// Gets a value indicating whether this provider supports hardware security modules (HSM).
	/// </summary>
	/// <value> True if the provider supports HSM-backed key storage, false otherwise. </value>
	bool SupportsHsm { get; }

	/// <summary>
	/// Gets a value indicating whether this provider supports key rotation.
	/// </summary>
	/// <value> True if the provider supports automatic key rotation, false otherwise. </value>
	bool SupportsKeyRotation { get; }

	/// <summary>
	/// Retrieves a secret value from the secure key management system.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the secret value or null if the secret does not exist.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret retrieval fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is null, empty, or invalid. </exception>
	Task<string?> GetSecretAsync(string keyName, CancellationToken cancellationToken);

	/// <summary>
	/// Stores a secret value in the secure key management system.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret to store. </param>
	/// <param name="secretValue"> The secret value to store securely. </param>
	/// <param name="metadata"> Optional metadata associated with the secret. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the secret was stored successfully, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret storage fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name or secret value is invalid. </exception>
	Task<bool> SetSecretAsync(string keyName, string secretValue, SecretMetadata? metadata,
		CancellationToken cancellationToken);

	/// <summary>
	/// Removes a secret from the key management system.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret to remove. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the secret was removed successfully, false
	/// if the secret did not exist.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret deletion fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is null, empty, or invalid. </exception>
	Task<bool> DeleteSecretAsync(string keyName, CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether a secret exists in the key management system without retrieving its value.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret to check. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains true if the secret exists, false otherwise. </returns>
	/// <exception cref="SecurityException"> Thrown when secret existence check fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is null, empty, or invalid. </exception>
	Task<bool> SecretExistsAsync(string keyName, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves metadata about a secret without accessing the secret value itself.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the secret metadata or null if the secret does not exist.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when metadata retrieval fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is null, empty, or invalid. </exception>
	Task<SecretMetadata?> GetSecretMetadataAsync(string keyName, CancellationToken cancellationToken);

	/// <summary>
	/// Generates a new encryption key with the specified parameters and stores it securely.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the new encryption key. </param>
	/// <param name="keyType"> The type of encryption key to generate. </param>
	/// <param name="keySize"> The size of the key in bits. </param>
	/// <param name="metadata"> Optional metadata associated with the key. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the key generation result including success status
	/// and key information.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when key generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the parameters are invalid. </exception>
	Task<KeyGenerationResult> GenerateEncryptionKeyAsync(
		string keyName,
		EncryptionKeyType keyType,
		int keySize,
		SecretMetadata? metadata,
		CancellationToken cancellationToken);

	/// <summary>
	/// Rotates an existing encryption key by generating a new version while maintaining access to the old version.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the key to rotate. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the key rotation result including the new key
	/// version and rotation metadata.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when key rotation fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is invalid. </exception>
	Task<KeyRotationResult> RotateEncryptionKeyAsync(string keyName, CancellationToken cancellationToken);

	/// <summary>
	/// Lists all secrets managed by this provider, optionally filtered by prefix.
	/// </summary>
	/// <param name="prefix"> Optional prefix to filter secret names. If null, all secrets are returned. </param>
	/// <param name="includeMetadata"> Whether to include metadata in the results. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the list of secret information matching the
	/// specified criteria.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret listing fails due to security constraints. </exception>
	Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(string? prefix, bool includeMetadata,
		CancellationToken cancellationToken);
}
