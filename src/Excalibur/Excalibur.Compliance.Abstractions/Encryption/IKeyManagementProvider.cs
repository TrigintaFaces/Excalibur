// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides core key management operations including retrieval, rotation, and active key lookup.
/// </summary>
/// <remarks>
/// <para>
/// Key management is separated from encryption to support:
/// - Cloud KMS integration (AWS KMS, Azure Key Vault, Google Cloud KMS)
/// - HSM-backed key storage
/// - Automated key rotation policies
/// </para>
/// <para> Implementations must ensure key material is never exposed in logs or errors. </para>
/// <para>
/// Administrative operations (listing, deletion, suspension) are defined in <see cref="IKeyManagementAdmin"/>.
/// Implementations typically implement both interfaces.
/// </para>
/// <para>
/// Optional capabilities — including durability, discovered via <see cref="IDurableKeyProvider"/> —
/// are resolved through <see cref="IServiceProvider.GetService(Type)"/>, never by casting the provider.
/// A provider answers for the capabilities it supplies; decorators MUST forward <c>GetService</c> to the
/// wrapped provider.
/// </para>
/// </remarks>
public interface IKeyManagementProvider : IServiceProvider
{
	/// <summary>
	/// Resolves an optional key-management capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve, for example <see cref="IDurableKeyProvider"/>. </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this provider supplies the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// The default implementation answers for any capability this instance itself implements. Leaf providers
	/// need not override it. Decorators MUST override it to defer unknown capabilities to the provider they
	/// wrap; a decorator that does not forward silently disables the capability beneath it.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

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
	/// Gets the currently active key for encryption operations.
	/// </summary>
	/// <param name="purpose"> Optional purpose to filter keys. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The active key metadata, or null if no active key exists. </returns>
	Task<KeyMetadata?> GetActiveKeyAsync(
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
}

/// <summary>
/// Provides administrative key management operations including listing, deletion, and suspension.
/// </summary>
/// <remarks>
/// <para>
/// This interface separates administrative operations from core key management
/// (<see cref="IKeyManagementProvider"/>) following the Interface Segregation Principle.
/// Consumers that only need to retrieve or rotate keys should depend on
/// <see cref="IKeyManagementProvider"/> instead.
/// </para>
/// <para>
/// Administrative operations include:
/// - Key inventory and listing
/// - Key deletion (crypto-shredding for GDPR compliance)
/// - Key suspension for security incidents
/// </para>
/// </remarks>
public interface IKeyManagementAdmin
{
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
	/// Destroys a key, or schedules it for irreversible destruction, and reports which of the two occurred.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key to delete. </param>
	/// <param name="retentionDays">
	/// The requested number of days to retain the key before irreversible destruction. A value of <c>0</c> requests
	/// immediate destruction. Implementations MUST NOT silently substitute a longer window: where a backend imposes a
	/// mandatory minimum retention, the effective irreversibility instant MUST be surfaced through the returned
	/// <see cref="KeyDestructionOutcome.IrreversibleAt"/> rather than clamped in silence.
	/// </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns>
	/// A <see cref="KeyDestructionOutcome"/> whose <see cref="KeyDestructionOutcome.State"/> distinguishes a key that is
	/// irrecoverable on return (<see cref="KeyDestructionState.Completed"/>) from one merely scheduled and still recoverable
	/// until <see cref="KeyDestructionOutcome.IrreversibleAt"/> (<see cref="KeyDestructionState.ScheduledIrreversible"/>),
	/// or a key that did not exist (<see cref="KeyDestructionState.NotFound"/>, an idempotent no-op).
	/// </returns>
	/// <remarks>
	/// For GDPR Right to Erasure, crypto-shredding is achieved by destroying the encryption key, rendering all data
	/// encrypted with that key unrecoverable. Callers issuing a completion attestation MUST treat only
	/// <see cref="KeyDestructionState.Completed"/> as irrecoverable-now: a
	/// <see cref="KeyDestructionState.ScheduledIrreversible"/> key remains recoverable until its window elapses and MUST NOT
	/// be attested as erased. On failure of the underlying destruction primitive, implementations MUST fail closed (surface
	/// the failure), never report a false success.
	/// </remarks>
	Task<KeyDestructionOutcome> DeleteKeyAsync(
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
	/// Reactivates a previously suspended key, restoring its use for cryptographic operations.
	/// </summary>
	/// <param name="keyId"> The unique identifier of the key to reactivate. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> True if the key was reactivated; false if the key was not found. </returns>
	/// <remarks>
	/// This is the inverse of <see cref="SuspendKeyAsync"/>. A key that is not suspended is
	/// returned to (or left in) the active state.
	/// </remarks>
	Task<bool> ReactivateKeyAsync(
		string keyId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Marks an <see cref="IKeyManagementProvider"/> whose key material is durable — keys survive a process
/// restart. A provider advertises this capability by answering for it from
/// <see cref="IServiceProvider.GetService(Type)"/>; consumers query rather than cast, so the capability
/// is discoverable through decorators.
/// </summary>
public interface IDurableKeyProvider
{
}
