// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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
	/// Reads a stored envelope back into an <see cref="EncryptedData"/>, without decrypting it.
	/// </summary>
	/// <param name="framed">
	/// The stored envelope bytes: the <see cref="MagicBytes"/>-prefixed form that
	/// <see cref="EncryptedFieldBinding.TryReadEnvelope"/> yields for an annotated property.
	/// </param>
	/// <param name="envelope">The parsed envelope when this method returns <see langword="true"/>.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="framed"/> is a readable envelope; otherwise
	/// <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>This is what makes the crypto-shredding guarantee checkable from outside the framework.</b> That
	/// guarantee is stated in terms of <see cref="KeyGeneration"/> -- a destruction ledger holds a row for the
	/// generation a field's envelope names -- so a reader that cannot recover the generation from the stored
	/// bytes cannot verify, audit or report an erasure except by attempting a decrypt and inferring the answer
	/// from whether it succeeded. Parsing needs no key, no tenant and no configuration, so the question can be
	/// asked of stored data alone, which is the form an audit takes.
	/// </para>
	/// <para>
	/// <b>No exception on any input</b>, following <see cref="int.TryParse(string?, out int)"/>. Bytes that do
	/// not carry <see cref="MagicBytes"/>, that are truncated, or whose body is not a complete envelope all
	/// return <see langword="false"/> and leave <paramref name="envelope"/> <see langword="null"/>. A stored
	/// value is read from a column the caller does not necessarily control, so malformed data is an expected
	/// condition rather than a fault.
	/// </para>
	/// <para>
	/// <b>Parsing is not validation of the ciphertext.</b> A <see langword="true"/> result says the envelope's
	/// metadata was readable. Whether the payload still decrypts depends on whether the generation it names
	/// survives, which is the question a destruction ledger answers -- and answering it is the reason to parse.
	/// </para>
	/// </remarks>
	public static bool TryParse(ReadOnlySpan<byte> framed, [NotNullWhen(true)] out EncryptedData? envelope)
	{
		envelope = null;
		if (!IsFieldEncrypted(framed))
		{
			return false;
		}

		try
		{
			envelope = JsonSerializer.Deserialize(
				framed[MagicBytes.Length..],
				EncryptedDataJsonContext.Default.EncryptedData);
		}
		catch (JsonException)
		{
			// Truncated, corrupt, or missing a member this record declares as required. Reported rather than
			// thrown: the caller is inspecting bytes it did not write, and a parse that threw would force
			// every auditing caller to wrap it in the try/catch this method already owns.
			return false;
		}

		return envelope is not null;
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
