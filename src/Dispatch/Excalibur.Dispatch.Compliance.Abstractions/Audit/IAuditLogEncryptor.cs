// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides encryption and decryption of audit log entries at rest.
/// </summary>
/// <remarks>
/// <para>
/// This service encrypts sensitive fields within audit entries before storage
/// and decrypts them on retrieval. It uses the existing <see cref="IKeyManagementProvider"/>
/// infrastructure for key access and supports field-level encryption granularity.
/// </para>
/// </remarks>
public interface IAuditLogEncryptor
{
	/// <summary>
	/// Encrypts the specified audit entry fields before storage.
	/// </summary>
	/// <param name="entry">The audit entry to encrypt.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The encrypted audit entry.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
	Task<EncryptedAuditEntry> EncryptAsync(
		AuditEvent entry,
		CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts an encrypted audit entry for reading.
	/// </summary>
	/// <param name="encryptedEntry">The encrypted audit entry to decrypt.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The decrypted audit event.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptedEntry"/> is null.</exception>
	Task<AuditEvent> DecryptAsync(
		EncryptedAuditEntry encryptedEntry,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents an audit entry with encrypted field values.
/// </summary>
public sealed record EncryptedAuditEntry
{
	/// <summary>
	/// Gets the unique identifier for this audit event.
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// Gets the encrypted field data as a dictionary of field name to encrypted value.
	/// </summary>
	public required IReadOnlyDictionary<string, byte[]> EncryptedFields { get; init; }

	/// <summary>
	/// Gets the non-encrypted fields preserved in clear text (event type, timestamp, etc.).
	/// </summary>
	public required IReadOnlyDictionary<string, string> ClearFields { get; init; }

	/// <summary>
	/// Gets the key identifier used for encryption.
	/// </summary>
	public required string KeyIdentifier { get; init; }

	/// <summary>
	/// Gets the encryption algorithm used.
	/// </summary>
	public required EncryptionAlgorithm Algorithm { get; init; }
}

/// <summary>
/// Configuration options for audit log encryption at rest.
/// </summary>
public sealed class AuditLogEncryptionOptions
{
	/// <summary>
	/// Gets or sets the encryption algorithm to use.
	/// Default: <see cref="EncryptionAlgorithm.Aes256Gcm"/>.
	/// </summary>
	public EncryptionAlgorithm EncryptionAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets the key identifier to use for encryption.
	/// </summary>
	public string? KeyIdentifier { get; set; }

	/// <summary>
	/// Gets or sets the list of field names to encrypt.
	/// Fields not in this list will be stored in clear text.
	/// Default includes ActorId, ResourceId, IpAddress, and Reason.
	/// </summary>
	public IList<string> EncryptFields { get; set; } =
	[
		nameof(AuditEvent.ActorId),
		nameof(AuditEvent.ResourceId),
		nameof(AuditEvent.IpAddress),
		nameof(AuditEvent.Reason)
	];
}
