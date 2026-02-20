// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides master key backup and recovery operations for disaster recovery scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the secure backup and restoration of master encryption keys,
/// which are the keys used to encrypt all other keys in the system (Key Encryption Keys - KEKs).
/// </para>
/// <para>
/// Features include:
/// </para>
/// <list type="bullet">
///   <item>HSM-backed export and import of master keys</item>
///   <item>Shamir's Secret Sharing for split-knowledge recovery (e.g., 3-of-5 custodians)</item>
///   <item>Full audit logging of all backup and recovery operations</item>
///   <item>Support for multiple backup formats (encrypted blob, HSM-wrapped, etc.)</item>
/// </list>
/// <para>
/// WARNING: Master key operations are highly sensitive. All operations MUST be audited
/// and require appropriate authorization (typically multi-party approval).
/// </para>
/// </remarks>
public interface IMasterKeyBackupService
{
	/// <summary>
	/// Exports a master key for backup purposes.
	/// </summary>
	/// <param name="keyId">The unique identifier of the master key to export.</param>
	/// <param name="options">Options controlling the export operation.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The backup containing the encrypted key material.</returns>
	/// <exception cref="ArgumentException">Thrown when keyId is null or empty.</exception>
	/// <exception cref="KeyNotFoundException">Thrown when the specified key does not exist.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the key cannot be exported (e.g., non-exportable HSM key).</exception>
	Task<MasterKeyBackup> ExportMasterKeyAsync(
		string keyId,
		MasterKeyExportOptions? options,
		CancellationToken cancellationToken);

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
	/// Generates recovery shares for a master key using Shamir's Secret Sharing.
	/// </summary>
	/// <param name="keyId">The unique identifier of the master key to split.</param>
	/// <param name="totalShares">Total number of shares to generate (custodians).</param>
	/// <param name="threshold">Minimum number of shares required for reconstruction.</param>
	/// <param name="options">Options controlling the share generation.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>An array of backup shares, one for each custodian.</returns>
	/// <exception cref="ArgumentException">Thrown when keyId is null or empty.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when threshold &lt; 2, totalShares &lt; 2, or threshold &gt; totalShares.</exception>
	/// <exception cref="KeyNotFoundException">Thrown when the specified key does not exist.</exception>
	/// <remarks>
	/// <para>
	/// The default configuration is 3-of-5, meaning any 3 custodians can reconstruct the key.
	/// Each share should be securely distributed to a different custodian.
	/// </para>
	/// <para>
	/// Split-knowledge key recovery ensures no single person can access the master key.
	/// </para>
	/// </remarks>
	Task<BackupShare[]> GenerateRecoverySplitAsync(
		string keyId,
		int totalShares,
		int threshold,
		BackupShareOptions? options,
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
