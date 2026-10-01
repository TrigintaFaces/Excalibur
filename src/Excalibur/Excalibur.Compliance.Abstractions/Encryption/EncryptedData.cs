// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

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
	/// <remarks>
	/// <b>Ordering and display, never identity.</b> A version number orders the generations of a key and names
	/// one for a human; it does not designate key material. <see cref="KeyGeneration"/> does that, because an
	/// ordinal restarts at 1 when a handle is re-provisioned and therefore designates two different pieces of
	/// material at different times.
	/// </remarks>
	public required int KeyVersion { get; init; }

	/// <summary>
	/// Gets the identifier of the key GENERATION that produced this payload.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the durable identity of the material, and it is what makes an erased subject stay erased.</b>
	/// A key handle is derived from the data subject, so it is stable and therefore RE-OCCUPIABLE: destroy a
	/// subject's key and the next ordinary write for that subject provisions a new one at the same handle, with
	/// the version ordinal starting again at 1. The handle and the ordinal together then designate both the
	/// destroyed material and the live material, so a reader of an older payload cannot tell that the key it
	/// names is gone — it resolves the live generation instead, and the decryption fails its authentication tag
	/// as though the payload were corrupt.
	/// </para>
	/// <para>
	/// A generation identifier is minted when the material is provisioned and never reused, so the question
	/// "is the material behind this payload destroyed?" has one answer that cannot be changed by anything
	/// provisioned afterwards. It is bound into the authenticated associated data, so a payload cannot be
	/// re-attributed to a different generation without failing to authenticate.
	/// </para>
	/// <para>
	/// Not secret, and not an ordering. It is an opaque identity: compare it for equality and nothing else.
	/// </para>
	/// </remarks>
	public string? KeyGeneration { get; init; }

	/// <summary>
	/// Gets the envelope layout this payload was written with.
	/// </summary>
	/// <remarks>
	/// Declared rather than inferred, so a payload written by an unsupported layout is REFUSED by name instead
	/// of being attempted and failing its authentication tag — which a reader would reasonably mistake for
	/// corrupted data. A reader that does not recognise this value must refuse the payload rather than guess at
	/// its shape.
	/// </remarks>
	public int FormatVersion { get; init; } = CurrentFormatVersion;

	/// <summary>
	/// The envelope layout this version of the framework writes.
	/// </summary>
	/// <remarks>
	/// Raised to 2 when the key generation became part of the envelope and of the authenticated associated
	/// data. A payload written before that carries no generation, so the material behind it cannot be
	/// identified and a destroyed subject cannot be distinguished from a live one — which is why such a payload
	/// is refused rather than read.
	/// </remarks>
	public const int CurrentFormatVersion = 2;

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
	/// Gets the data encryption key for this payload, wrapped by the key service identified by
	/// <see cref="KeyId"/> and <see cref="KeyVersion"/>. Null when the payload was encrypted directly
	/// with key material rather than under an envelope.
	/// </summary>
	/// <value>
	/// The wrapped data encryption key, or <see langword="null"/> when <see cref="Ciphertext"/> was
	/// encrypted directly under the identified key.
	/// </value>
	/// <remarks>
	/// <para>
	/// Envelope encryption is the path a cloud KMS or HSM can actually serve, because it never requires
	/// the key service to export key bytes. The payload is encrypted locally with a single-use data
	/// encryption key, and only that key is handed to the key service to be wrapped.
	/// </para>
	/// <para>
	/// This property records which of the two schemes produced the payload, so a reader selects the
	/// correct one from the data itself rather than from how it happens to be configured at read time.
	/// Data written under either scheme therefore remains readable after the configuration changes.
	/// </para>
	/// </remarks>
	public WrappedDataKey? WrappedKey { get; init; }

	/// <summary>
	/// Gets the timestamp when this data was encrypted.
	/// </summary>
	public DateTimeOffset EncryptedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the optional tenant identifier for multi-tenant isolation.
	/// </summary>
	public string? TenantId { get; init; }
}
