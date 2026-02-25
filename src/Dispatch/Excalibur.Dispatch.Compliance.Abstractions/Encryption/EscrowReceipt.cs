// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents a receipt for a successful key escrow operation.
/// </summary>
public sealed record EscrowReceipt
{
	/// <summary>
	/// Gets the unique identifier of the escrowed key.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the unique identifier of this escrow record.
	/// </summary>
	public required string EscrowId { get; init; }

	/// <summary>
	/// Gets the timestamp when the key was escrowed.
	/// </summary>
	public required DateTimeOffset EscrowedAt { get; init; }

	/// <summary>
	/// Gets the timestamp when the escrow expires, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the hash of the encrypted key material for verification.
	/// </summary>
	public required string KeyHash { get; init; }

	/// <summary>
	/// Gets the encryption algorithm used to protect the escrowed key.
	/// </summary>
	public required EncryptionAlgorithm Algorithm { get; init; }

	/// <summary>
	/// Gets the version of the master key used to encrypt the escrowed key.
	/// </summary>
	public int MasterKeyVersion { get; init; }

	/// <summary>
	/// Gets optional metadata stored with the escrow.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
