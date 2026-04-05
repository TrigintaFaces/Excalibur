// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines key generation, rotation, and provider capability operations for encryption key management.
/// </summary>
public interface IElasticsearchKeyManagement
{
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
}
