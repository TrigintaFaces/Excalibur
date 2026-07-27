// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Produces master key backups for disaster recovery scenarios.
/// </summary>
/// <remarks>
/// <para>
/// Handles the secure export of master encryption keys (Key Encryption Keys - KEKs) and the
/// generation of split-knowledge recovery shares via Shamir's Secret Sharing.
/// </para>
/// <para>
/// WARNING: Master key operations are highly sensitive. All operations MUST be audited
/// and require appropriate authorization (typically multi-party approval).
/// </para>
/// </remarks>
public interface IMasterKeyBackupExporter
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
}
