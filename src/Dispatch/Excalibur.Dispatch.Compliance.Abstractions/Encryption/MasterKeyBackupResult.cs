// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Result of importing a master key backup.
/// </summary>
public sealed record MasterKeyImportResult
{
	/// <summary>
	/// Gets a value indicating whether the import was successful.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the identifier of the imported key.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the imported key.
	/// </summary>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets the metadata of the imported key.
	/// </summary>
	public KeyMetadata? KeyMetadata { get; init; }

	/// <summary>
	/// Gets the timestamp when the import was completed.
	/// </summary>
	public required DateTimeOffset ImportedAt { get; init; }

	/// <summary>
	/// Gets a value indicating whether an existing key was overwritten.
	/// </summary>
	public bool WasOverwritten { get; init; }

	/// <summary>
	/// Gets any warnings generated during import.
	/// </summary>
	public IReadOnlyList<string>? Warnings { get; init; }

	/// <summary>
	/// Gets the error message if the import failed.
	/// </summary>
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of verifying a master key backup.
/// </summary>
public sealed record BackupVerificationResult
{
	/// <summary>
	/// Gets a value indicating whether the backup is valid and can be restored.
	/// </summary>
	public required bool IsValid { get; init; }

	/// <summary>
	/// Gets the identifier of the key in the backup.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the key in the backup.
	/// </summary>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets a value indicating whether the backup has expired.
	/// </summary>
	public bool IsExpired { get; init; }

	/// <summary>
	/// Gets a value indicating whether the backup format is supported.
	/// </summary>
	public bool FormatSupported { get; init; }

	/// <summary>
	/// Gets a value indicating whether the integrity check passed.
	/// </summary>
	public bool IntegrityCheckPassed { get; init; }

	/// <summary>
	/// Gets any warnings about the backup.
	/// </summary>
	public IReadOnlyList<string>? Warnings { get; init; }

	/// <summary>
	/// Gets error messages if validation failed.
	/// </summary>
	public IReadOnlyList<string>? Errors { get; init; }

	/// <summary>
	/// Gets the timestamp when the backup was created.
	/// </summary>
	public DateTimeOffset? BackupCreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the backup expires.
	/// </summary>
	public DateTimeOffset? BackupExpiresAt { get; init; }
}

/// <summary>
/// Status of master key backups for a specific key.
/// </summary>
public sealed record MasterKeyBackupStatus
{
	/// <summary>
	/// Gets the identifier of the master key.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the current version of the key.
	/// </summary>
	public required int CurrentVersion { get; init; }

	/// <summary>
	/// Gets a value indicating whether the key has been backed up.
	/// </summary>
	public required bool HasBackup { get; init; }

	/// <summary>
	/// Gets the timestamp of the most recent backup.
	/// </summary>
	public DateTimeOffset? LastBackupAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the most recent backup expires.
	/// </summary>
	public DateTimeOffset? BackupExpiresAt { get; init; }

	/// <summary>
	/// Gets the number of active backup shares (if split-knowledge backup was used).
	/// </summary>
	public int ActiveShareCount { get; init; }

	/// <summary>
	/// Gets the threshold required for share reconstruction.
	/// </summary>
	public int? ShareThreshold { get; init; }

	/// <summary>
	/// Gets a value indicating whether the backup is at risk (expired or about to expire).
	/// </summary>
	public bool IsAtRisk { get; init; }

	/// <summary>
	/// Gets warnings about the backup status.
	/// </summary>
	public IReadOnlyList<string>? Warnings { get; init; }
}
