// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Restores master keys from backups and inspects backup validity for disaster recovery scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Handles the secure import of master encryption keys (Key Encryption Keys - KEKs), reconstruction
/// from split-knowledge recovery shares, and verification/status inspection of existing backups.
/// </para>
/// <para>
/// WARNING: Master key operations are highly sensitive. All operations MUST be audited
/// and require appropriate authorization (typically multi-party approval).
/// </para>
/// </remarks>
public interface IMasterKeyRestoreService
{
	/// <summary>
	/// Imports a previously exported master key backup.
	/// </summary>
	/// <param name="backup">The backup data to import.</param>
	/// <param name="options">Options controlling the import operation.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the import operation including the restored key metadata.</returns>
	/// <exception cref="ArgumentNullException">Thrown when backup is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the backup is corrupted or cannot be decrypted.</exception>
	/// <exception cref="MasterKeyBackupException">Thrown when the import fails due to validation errors.</exception>
	Task<MasterKeyImportResult> ImportMasterKeyAsync(
		MasterKeyBackup backup,
		MasterKeyImportOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reconstructs a master key from the provided shares.
	/// </summary>
	/// <param name="shares">The shares to combine (must meet the threshold).</param>
	/// <param name="options">Options controlling the reconstruction.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The result of the reconstruction including the restored key metadata.</returns>
	/// <exception cref="ArgumentException">Thrown when shares is null or empty.</exception>
	/// <exception cref="MasterKeyBackupException">Thrown when insufficient shares are provided or reconstruction fails.</exception>
	Task<MasterKeyImportResult> ReconstructFromSharesAsync(
		BackupShare[] shares,
		MasterKeyImportOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Verifies that a backup can be successfully restored without actually importing it.
	/// </summary>
	/// <param name="backup">The backup to verify.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The verification result including any warnings or issues found.</returns>
	Task<BackupVerificationResult> VerifyBackupAsync(
		MasterKeyBackup backup,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of existing backups for a master key.
	/// </summary>
	/// <param name="keyId">The unique identifier of the master key.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The backup status including when backups were created and their validity.</returns>
	Task<MasterKeyBackupStatus?> GetBackupStatusAsync(
		string keyId,
		CancellationToken cancellationToken);
}
