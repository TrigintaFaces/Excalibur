// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides key management operations including retrieval, rotation, and deletion.
/// </summary>
/// <remarks>
/// <para>
/// Key management is separated from encryption to support:
/// - Cloud KMS integration (AWS KMS, Azure Key Vault, Google Cloud KMS)
/// - HSM-backed key storage
/// - Automated key rotation policies
/// </para>
/// <para> Implementations must ensure key material is never exposed in logs or errors. </para>
/// </remarks>
public interface IKeyManagementProvider
{
	/// <summary>
	/// Retrieves metadata for a specific encryption key.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The key metadata, or null if the key does not exist. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="keyId" /> is null or empty. </exception>
	Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves metadata for a specific version of an encryption key.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key. </param>
	/// <param name="version"> The key version to retrieve. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The key metadata for the specified version, or null if not found. </returns>
	Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken);

	/// <summary>
	/// Lists all keys matching the specified filter criteria.
	/// </summary>
	/// <param name="status"> Optional filter by key status. Null returns all statuses. </param>
	/// <param name="purpose"> Optional filter by key purpose. Null returns all purposes. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A list of key metadata matching the criteria. </returns>
	Task<IReadOnlyList<KeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a new encryption key or rotates an existing key to a new version.
	/// </summary>
	/// <param name="keyId"> The unique identifier for the key. If the key exists, creates a new version. </param>
	/// <param name="algorithm"> The encryption algorithm for this key. </param>
	/// <param name="purpose"> Optional purpose or scope for the key. </param>
	/// <param name="expiresAt"> Optional expiration date for the key. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The result of the rotation/creation operation. </returns>
	Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a key for deletion after the specified retention period.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key to delete. </param>
	/// <param name="retentionDays">
	/// Number of days to retain the key before permanent deletion. Allows recovery during this period. Default is 30 days.
	/// </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> True if the key was scheduled for deletion; false if the key was not found. </returns>
	/// <remarks>
	/// For GDPR Right to Erasure, crypto-shredding can be achieved by deleting the encryption key, rendering all data encrypted
	/// with that key unrecoverable.
	/// </remarks>
	Task<bool> DeleteKeyAsync(
		string keyId,
		int retentionDays,
		CancellationToken cancellationToken);

	/// <summary>
	/// Suspends a key, preventing its use for any cryptographic operations.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key to suspend. </param>
	/// <param name="reason"> The reason for suspension (for audit purposes). </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> True if the key was suspended; false if the key was not found. </returns>
	Task<bool> SuspendKeyAsync(
		string keyId,
		string reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the currently active key for encryption operations.
	/// </summary>
	/// <param name="purpose"> Optional purpose to filter keys. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The active key metadata, or null if no active key exists. </returns>
	Task<KeyMetadata?> GetActiveKeyAsync(
		string? purpose,
		CancellationToken cancellationToken);
}
