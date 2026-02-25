// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents metadata about an encryption key without exposing key material.
/// </summary>
/// <remarks>Key metadata includes version, status, and rotation information for audit and compliance purposes.</remarks>
public sealed record KeyMetadata
{
	/// <summary>
	/// Gets the unique identifier for this key.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the key (incremented on rotation).
	/// </summary>
	public required int Version { get; init; }

	/// <summary>
	/// Gets the current lifecycle status of the key.
	/// </summary>
	public required KeyStatus Status { get; init; }

	/// <summary>
	/// Gets the encryption algorithm this key is used with.
	/// </summary>
	public required EncryptionAlgorithm Algorithm { get; init; }

	/// <summary>
	/// Gets the timestamp when this key was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when this key expires and should no longer be used for encryption. Null if the key does not expire.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the timestamp when this key was last rotated. Null if the key has never been rotated.
	/// </summary>
	public DateTimeOffset? LastRotatedAt { get; init; }

	/// <summary>
	/// Gets the purpose or scope of this key (e.g., "field-encryption", "backup").
	/// </summary>
	public string? Purpose { get; init; }

	/// <summary>
	/// Gets a value indicating whether this key can be used for FIPS 140-2 compliant operations.
	/// </summary>
	public bool IsFipsCompliant { get; init; }
}
