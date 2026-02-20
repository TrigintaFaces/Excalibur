// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents an encrypted backup of a master encryption key.
/// </summary>
/// <remarks>
/// <para>
/// The backup contains the encrypted key material along with metadata
/// required for restoration. The actual key material is never stored in plaintext.
/// </para>
/// <para>
/// The backup format includes:
/// - Encrypted key material (wrapped with a backup key or HSM)
/// - Key metadata (algorithm, version, purpose)
/// - Integrity verification (hash/MAC)
/// - Backup timestamp and expiration
/// </para>
/// </remarks>
public sealed record MasterKeyBackup
{
	/// <summary>
	/// Gets the unique identifier for this backup.
	/// </summary>
	public required string BackupId { get; init; }

	/// <summary>
	/// Gets the identifier of the master key that was backed up.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the key that was backed up.
	/// </summary>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets the encrypted key material.
	/// </summary>
	/// <remarks>
	/// This is the wrapped/encrypted form of the master key.
	/// Never contains plaintext key material.
	/// </remarks>
	public required byte[] EncryptedKeyMaterial { get; init; }

	/// <summary>
	/// Gets the algorithm used to encrypt the backup.
	/// </summary>
	public required EncryptionAlgorithm WrappingAlgorithm { get; init; }

	/// <summary>
	/// Gets the identifier of the key used to wrap (encrypt) this backup.
	/// </summary>
	/// <remarks>
	/// This could be an HSM key, a backup encryption key, or another master key.
	/// </remarks>
	public string? WrappingKeyId { get; init; }

	/// <summary>
	/// Gets the algorithm of the backed-up master key.
	/// </summary>
	public required EncryptionAlgorithm KeyAlgorithm { get; init; }

	/// <summary>
	/// Gets the initialization vector used for wrapping.
	/// </summary>
	public byte[]? Iv { get; init; }

	/// <summary>
	/// Gets the authentication tag for integrity verification.
	/// </summary>
	public byte[]? AuthTag { get; init; }

	/// <summary>
	/// Gets the SHA-256 hash of the original plaintext key for verification.
	/// </summary>
	/// <remarks>
	/// Used to verify successful restoration without exposing key material.
	/// </remarks>
	public required string KeyHash { get; init; }

	/// <summary>
	/// Gets the timestamp when this backup was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when this backup expires and should no longer be used.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the purpose or scope of the backed-up key.
	/// </summary>
	public string? Purpose { get; init; }

	/// <summary>
	/// Gets the format version of this backup structure.
	/// </summary>
	/// <remarks>
	/// Allows for future changes to the backup format while maintaining backward compatibility.
	/// </remarks>
	public int FormatVersion { get; init; } = 1;

	/// <summary>
	/// Gets optional metadata associated with the backup.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets a value indicating whether this backup has expired.
	/// </summary>
	public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow;
}
