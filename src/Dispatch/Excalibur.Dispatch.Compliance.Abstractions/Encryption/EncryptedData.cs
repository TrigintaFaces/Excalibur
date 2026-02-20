// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents encrypted data with associated metadata required for decryption.
/// </summary>
/// <remarks>
/// Encrypted data includes key identification and algorithm info to support key rotation and future algorithm migration.
/// </remarks>
public sealed record EncryptedData
{
	/// <summary>
	/// Magic bytes that identify data as encrypted by this framework.
	/// </summary>
	/// <remarks>
	/// Encrypted fields are identified by these magic bytes at the start of the serialized data.
	/// Format: 0x45 0x58 0x43 0x52 ("EXCR" for Excalibur Encrypted)
	/// </remarks>
	public static ReadOnlySpan<byte> MagicBytes => [0x45, 0x58, 0x43, 0x52];

	/// <summary>
	/// Checks if the provided data appears to be encrypted by this framework.
	/// </summary>
	/// <param name="data">The data to check.</param>
	/// <returns><see langword="true"/> if the data starts with the encryption magic bytes; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// <para>
	/// This method uses magic byte detection to determine if data is encrypted.
	/// This is a heuristic check - false positives are possible if unencrypted data happens to start with the same bytes.
	/// </para>
	/// <para>
	/// For performance, this method uses <see cref="Span{T}"/> to avoid allocations.
	/// </para>
	/// </remarks>
	public static bool IsFieldEncrypted(ReadOnlySpan<byte> data)
	{
		if (data.Length < MagicBytes.Length)
		{
			return false;
		}

		return data[..MagicBytes.Length].SequenceEqual(MagicBytes);
	}

	/// <summary>
	/// Checks if the provided byte array appears to be encrypted by this framework.
	/// </summary>
	/// <param name="data">The data to check.</param>
	/// <returns><see langword="true"/> if the data starts with the encryption magic bytes; otherwise, <see langword="false"/>.</returns>
	public static bool IsFieldEncrypted(byte[]? data)
	{
		if (data is null)
		{
			return false;
		}

		return IsFieldEncrypted(data.AsSpan());
	}

	/// <summary>
	/// Gets the encrypted ciphertext bytes.
	/// </summary>
	public required byte[] Ciphertext { get; init; }

	/// <summary>
	/// Gets the identifier of the key used for encryption.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the version of the key used for encryption.
	/// </summary>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets the encryption algorithm used.
	/// </summary>
	public required EncryptionAlgorithm Algorithm { get; init; }

	/// <summary>
	/// Gets the initialization vector (IV) or nonce used for encryption.
	/// </summary>
	public required byte[] Iv { get; init; }

	/// <summary>
	/// Gets the authentication tag for authenticated encryption modes (GCM, Poly1305). Null for non-authenticated modes.
	/// </summary>
	public byte[]? AuthTag { get; init; }

	/// <summary>
	/// Gets the timestamp when this data was encrypted.
	/// </summary>
	public DateTimeOffset EncryptedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the optional tenant identifier for multi-tenant isolation.
	/// </summary>
	public string? TenantId { get; init; }
}
