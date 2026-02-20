// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Options for exporting a master key backup.
/// </summary>
public sealed class MasterKeyExportOptions
{
	/// <summary>
	/// Gets or sets the identifier of the key to use for wrapping the backup.
	/// If null, the system default backup encryption key is used.
	/// </summary>
	public string? WrappingKeyId { get; set; }

	/// <summary>
	/// Gets or sets the algorithm to use for wrapping the backup.
	/// Defaults to AES-256-GCM.
	/// </summary>
	public EncryptionAlgorithm WrappingAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets when the backup should expire.
	/// If null, the backup does not expire (not recommended for production).
	/// </summary>
	public TimeSpan? ExpiresIn { get; set; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets or sets optional metadata to include with the backup.
	/// </summary>
	public Dictionary<string, string>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the reason for creating this backup (for audit purposes).
	/// </summary>
	public string? Reason { get; set; }
}

/// <summary>
/// Options for importing a master key backup.
/// </summary>
public sealed class MasterKeyImportOptions
{
	/// <summary>
	/// Gets or sets whether to overwrite an existing key with the same ID.
	/// Defaults to false.
	/// </summary>
	public bool AllowOverwrite { get; set; }

	/// <summary>
	/// Gets or sets whether to activate the key immediately after import.
	/// Defaults to true.
	/// </summary>
	public bool ActivateImmediately { get; set; } = true;

	/// <summary>
	/// Gets or sets the new key ID to use (if different from the backup).
	/// If null, uses the original key ID from the backup.
	/// </summary>
	public string? NewKeyId { get; set; }

	/// <summary>
	/// Gets or sets the reason for importing this backup (for audit purposes).
	/// </summary>
	public string? Reason { get; set; }

	/// <summary>
	/// Gets or sets whether to verify the key hash after import.
	/// Defaults to true.
	/// </summary>
	public bool VerifyKeyHash { get; set; } = true;
}

/// <summary>
/// Options for generating backup shares.
/// </summary>
public sealed class BackupShareOptions
{
	/// <summary>
	/// Gets or sets when the shares should expire.
	/// If null, shares do not expire.
	/// </summary>
	public TimeSpan? ExpiresIn { get; set; } = TimeSpan.FromDays(365);

	/// <summary>
	/// Gets or sets the list of custodian identifiers.
	/// If provided, must have exactly the same count as totalShares.
	/// </summary>
	public IReadOnlyList<string>? CustodianIds { get; set; }

	/// <summary>
	/// Gets or sets the reason for generating shares (for audit purposes).
	/// </summary>
	public string? Reason { get; set; }
}
