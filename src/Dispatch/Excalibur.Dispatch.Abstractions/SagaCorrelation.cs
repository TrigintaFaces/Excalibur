// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Excalibur.Dispatch;

/// <summary>
/// Derives a saga identifier from a business correlation key, so a publisher can address a saga instance
/// without knowing the identifier the saga was created with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem this solves.</b> <see cref="ISagaEvent.SagaId"/> is the only thing that locates a saga
/// instance, so without this a publisher has to already hold the saga's identifier — which the first
/// message in a conversation cannot, and a service in another bounded context generally never will. The
/// business key is the thing every participant does share: an order number, a tenant's subscription id,
/// a shipment reference.
/// </para>
/// <para>
/// <b>Why derivation rather than a lookup table.</b> The same key always derives the same identifier, so
/// the existing load-by-id path finds the instance with no second query, no index to keep, and nothing
/// extra for a provider to implement. Every store that can load a saga by id can therefore correlate,
/// which is why this works on all of them rather than on whichever ones gained a lookup first.
/// </para>
/// <para>
/// <b>The identifier is a function of the saga TYPE as well as the key</b>, so two different sagas
/// correlating on the same order number receive independently scoped hash inputs. Pass a stable type scope:
/// a renamed saga derives different identifiers and stops finding its own in-flight instances, which is
/// a migration hazard and is the reason the type name is an explicit argument rather than
/// something read from the call site.
/// </para>
/// <para>
/// <b>Collision bound.</b> The result retains 122 SHA-256 digest bits after UUID version and variant
/// encoding. Framing distinguishes input boundaries, but truncated hashes cannot prove uniqueness.
/// A saga whose identity must be provably unique should carry an identifier assigned by a suitable
/// authoritative identity service.
/// </para>
/// <para>
/// <b>SHA-256 and RFC 9562 version 8, deliberately, and NOT the version 5 that a reader might expect.</b>
/// Name-based version 5 is specified over SHA-1, and this project does not use SHA-1. Version 8 is the
/// RFC's provision for an application-defined derivation, which is exactly what this is, so the digest
/// can be SHA-256 while the result is still a well-formed UUID with its version and variant bits set.
/// The derivation is not a security boundary: it decides which saga an event belongs to, and an attacker
/// who can choose the business key can already address that saga by sending the key.
/// </para>
/// <para>
/// <b>Versioned framing.</b> The v2 derivation hashes a domain tag and two big-endian UTF-8 byte-length
/// frames. Changing this derivation changes existing IDs: publishers and coordinators must upgrade
/// together, and retained in-flight state needs explicit migration rather than an ambiguous fallback.
/// </para>
/// </remarks>
public static class SagaCorrelation
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

	/// <summary>
	/// Derives the saga identifier for <paramref name="correlationKey"/> within <paramref name="sagaType"/>.
	/// </summary>
	/// <param name="sagaType">A stable name for the saga type, scoping the key independently for unrelated sagas.</param>
	/// <param name="correlationKey">The business key shared by every participant in the conversation.</param>
	/// <returns>The identifier to place on <see cref="ISagaEvent.SagaId"/>.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when either argument is null, empty, whitespace, or contains ill-formed UTF-16. Neither has a meaningful empty value: an
	/// empty key would route every message to one instance, and an empty type would let unrelated sagas
	/// share identifiers.
	/// </exception>
	public static Guid ToSagaId(string sagaType, string correlationKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
		ArgumentException.ThrowIfNullOrWhiteSpace(correlationKey);

        // A delimiter is valid input: encode exact UTF8 byte lengths so moving it across the
        // type/key boundary cannot preserve the hash input. No trimming or normalization occurs.
        var typeBytes = EncodedLength(sagaType, nameof(sagaType));
        var keyBytes = EncodedLength(correlationKey, nameof(correlationKey));
        var domain = "Excalibur.SagaCorrelation/v2\0"u8;
        var length = checked(domain.Length + sizeof(uint) + typeBytes + sizeof(uint) + keyBytes);
        var scoped = length <= 1024 ? stackalloc byte[length] : new byte[length];
        domain.CopyTo(scoped);
        var offset = domain.Length;
        BinaryPrimitives.WriteUInt32BigEndian(scoped[offset..], (uint)typeBytes);
        offset += sizeof(uint);
        offset += StrictUtf8.GetBytes(sagaType.AsSpan(), scoped.Slice(offset, typeBytes));
        BinaryPrimitives.WriteUInt32BigEndian(scoped[offset..], (uint)keyBytes);
        offset += sizeof(uint);
        _ = StrictUtf8.GetBytes(correlationKey.AsSpan(), scoped.Slice(offset, keyBytes));
        Span<byte> digest = stackalloc byte[32];
        var written = SHA256.HashData(scoped, digest);
		if (written != digest.Length)
		{
			throw new InvalidOperationException(
				$"SHA-256 produced {written} bytes instead of {digest.Length}, so the derivation cannot be "
				+ "completed. Nothing in this method can recover from that, and returning a partial digest "
				+ "would derive a saga identifier nobody can reproduce.");
		}

		Span<byte> id = stackalloc byte[16];
		digest[..16].CopyTo(id);

		// RFC 9562: version in the high nibble of octet 6, variant in the top two bits of octet 8. Setting
		// them is what makes the result a well-formed UUID rather than 16 arbitrary bytes, and it is why
		// only 122 of the 128 bits carry digest.
		id[6] = (byte)((id[6] & 0x0F) | 0x80);
		id[8] = (byte)((id[8] & 0x3F) | 0x80);

		// Big-endian so the identifier is byte-order stable across platforms: a saga correlated on one
		// machine must derive the same identifier on another, and Guid's own layout is little-endian for
		// its first three fields.
		return new Guid(id, bigEndian: true);
	}
    /// <summary>Derives an identifier using the same stable, fully scoped type identity as declarative saga correlation.</summary>
    /// <typeparam name="TSaga">The closed saga type, including its assembly simple name, namespace and generic arguments.</typeparam>
    /// <param name="correlationKey">The literal well-formed Unicode business key.</param>
    /// <returns>The UUIDv8 to place on <see cref="ISagaEvent.SagaId"/>.</returns>
    /// <remarks>Assembly versions, strong-name keys and load contexts are excluded intentionally. Renaming or moving the type/assembly changes identifiers. Supplying the exact generated scope through the string overload reproduces this typed result; an ordinary short type-name scope does not.</remarks>
    /// <exception cref="ArgumentException">The key is null, empty, whitespace or ill-formed UTF-16.</exception>
    public static Guid ToSagaId<TSaga>(string correlationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationKey);
        return ToSagaId(TypedScope<TSaga>.Value, correlationKey);
    }

    private static int EncodedLength(string value, string parameter)
    {
        try { return StrictUtf8.GetByteCount(value); }
        catch (EncoderFallbackException error)
        { throw new ArgumentException("Correlation inputs must be well-formed Unicode; encoder replacement cannot define identity.", parameter, error); }
    }

    private static class TypedScope<TSaga>
    {
        // Build recursion does not enter other generic caches, avoiding static initialization cycles.
        internal static readonly string Value = SagaCorrelationTypeIdentity.Create(typeof(TSaga));
    }

}
