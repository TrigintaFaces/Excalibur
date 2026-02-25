// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides services for migrating encrypted data between providers, algorithms, or key versions.
/// </summary>
/// <remarks>
/// <para>
/// This interface supports encryption migrations such as:
/// - Re-encrypting data with a new key version after key rotation
/// - Migrating data from one encryption algorithm to another
/// - Transferring encrypted data between encryption providers
/// </para>
/// <para>
/// Migrations are designed to be resumable and can be performed in batches for large datasets.
/// Progress tracking enables monitoring and recovery from interrupted migrations.
/// </para>
/// </remarks>
public interface IEncryptionMigrationService
{
	/// <summary>
	/// Re-encrypts a single piece of encrypted data using the target configuration.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to migrate. </param>
	/// <param name="sourceContext"> The encryption context used for the original encryption. </param>
	/// <param name="targetContext"> The encryption context to use for re-encryption. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The result of the migration operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	/// <exception cref="EncryptionMigrationException"> Thrown when migration fails. </exception>
	Task<EncryptionMigrationResult> MigrateAsync(
		EncryptedData encryptedData,
		EncryptionContext sourceContext,
		EncryptionContext targetContext,
		CancellationToken cancellationToken);

	/// <summary>
	/// Re-encrypts a batch of encrypted data items using the target configuration.
	/// </summary>
	/// <param name="items"> The encrypted data items to migrate with their source contexts. </param>
	/// <param name="targetContext"> The encryption context to use for re-encryption. </param>
	/// <param name="options"> Options controlling batch migration behavior. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The results of the batch migration operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	Task<EncryptionBatchMigrationResult> MigrateBatchAsync(
		IReadOnlyList<EncryptionMigrationItem> items,
		EncryptionContext targetContext,
		BatchMigrationOptions options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if the encrypted data requires migration based on the current policy.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data to check. </param>
	/// <param name="policy"> The migration policy to evaluate against. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> True if the data requires migration; otherwise, false. </returns>
	Task<bool> RequiresMigrationAsync(
		EncryptedData encryptedData,
		MigrationPolicy policy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current migration status for tracking purposes.
	/// </summary>
	/// <param name="migrationId"> The identifier of the migration to check. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The current status of the migration, or null if not found. </returns>
	Task<MigrationStatus?> GetMigrationStatusAsync(
		string migrationId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Estimates the scope of a migration for planning purposes.
	/// </summary>
	/// <param name="policy"> The migration policy to evaluate. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> An estimate of the migration scope. </returns>
	Task<MigrationEstimate> EstimateMigrationAsync(
		MigrationPolicy policy,
		CancellationToken cancellationToken);
}
