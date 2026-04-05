// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines CRUD operations for secure secret storage and retrieval in key management systems.
/// </summary>
public interface IElasticsearchKeyStorage
{
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
}
