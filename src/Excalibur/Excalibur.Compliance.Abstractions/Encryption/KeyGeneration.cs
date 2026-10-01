// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Excalibur.Compliance;

/// <summary>
/// The identifier of one provisioning LINEAGE of key material: 128 bits, minted by this framework, and the
/// value a destruction record is keyed on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a type and not a <see cref="string"/>.</b> The destruction ledger is keyed on the generation
/// alone, so a weak generation is not a local problem: two subjects whose generations collide share one
/// destruction record, and erasing the first reports the second's live personal data as lawfully erased. A
/// <see cref="string"/> parameter accepts an ordinal, a backend version identifier, a key ARN, or a value
/// derived from the subject identifier -- all of which collide or are guessable, and none of which any
/// compiler would question. The only ways to obtain a <see cref="KeyGeneration"/> are <see cref="Mint"/>,
/// which uses a CSPRNG, and <see cref="Parse"/>, which rejects anything that is not 32 hexadecimal
/// characters. A weak generation stops being discouraged and becomes unrepresentable.
/// </para>
/// <para>
/// <b>What this type CANNOT enforce, stated plainly because it is a consumer obligation.</b> It constrains
/// SHAPE, not ENTROPY. A provider may hash a subject identifier into 32 hexadecimal characters and
/// <see cref="Parse"/> will accept it, because the result is indistinguishable from a mint. Such a value
/// collides across subjects exactly as a weak one does, with the consequence above. A provider implementing
/// <see cref="IKeyManagementProvider"/> MUST obtain its generations from <see cref="Mint"/>, or from a CSPRNG
/// of its own -- never derived from the handle, the subject, a counter, or a clock.
/// </para>
/// <para>
/// <b>It deliberately stops short of the wire.</b> The envelope and the encryption context carry the
/// generation as a <see cref="string"/>, and that is not an oversight: the value is bound into the AES-GCM
/// associated data and is persisted inside serialized envelopes, so changing its serialized form would alter
/// what already-written payloads authenticate against and make them unreadable. This type lives at the
/// provider, metadata and ledger boundary; <see cref="ToString"/> produces the exact characters that travel
/// and are stored, so the round trip is byte-identical.
/// </para>
/// <para>
/// Opaque: compare for equality, never for order, and do not read the characters for meaning.
/// </para>
/// </remarks>
public readonly struct KeyGeneration : IEquatable<KeyGeneration>
{
	/// <summary>The number of hexadecimal characters a generation is: 128 bits.</summary>
	private const int HexLength = 32;

	private readonly string? _value;

	private KeyGeneration(string value) => _value = value;

	/// <summary>
	/// Gets a value indicating whether this instance carries an identifier.
	/// </summary>
	/// <remarks>
	/// A <see langword="default"/> instance carries none. It exists because a struct cannot forbid
	/// <see langword="default"/>, so this type cannot make "unset" unrepresentable -- only
	/// <i>distinguishable</i>, which is what this reports. Prefer <see cref="Nullable{T}"/> at a boundary that
	/// genuinely has no generation, so the absence sits in the signature rather than in a flag a caller may
	/// forget to read.
	/// </remarks>
	public bool HasValue => _value is not null;

	/// <summary>
	/// Mints a new generation for newly provisioned material.
	/// </summary>
	/// <returns>A generation no other provisioning will hold.</returns>
	/// <remarks>
	/// 128 bits from <see cref="RandomNumberGenerator"/>. The identifier is not secret -- it travels in
	/// cleartext on every envelope -- but it must be unguessable and unique by construction, because it is
	/// what the destruction ledger is keyed on. A <see cref="Guid"/> or <see cref="Random"/> is forbidden
	/// here: neither is a CSPRNG, and key-adjacent material does not get the convenient option.
	/// </remarks>
	public static KeyGeneration Mint() => new(RandomNumberGenerator.GetHexString(HexLength, lowercase: true));

	/// <summary>
	/// Parses a generation that was previously minted, as read back from a backend, an envelope, or a record.
	/// </summary>
	/// <param name="value">The 32-character hexadecimal identifier.</param>
	/// <returns>The parsed generation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="value"/> is not exactly 32 hexadecimal characters.
	/// </exception>
	/// <remarks>
	/// Throwing rather than substituting is the point. A value that is not a generation cannot be turned into
	/// one, and the alternatives -- accepting it, or quietly yielding an unset instance -- would both put a
	/// value nothing minted into the position the ledger keys on.
	/// </remarks>
	public static KeyGeneration Parse(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (TryParse(value, out var generation))
		{
			return generation;
		}

		throw new ArgumentException(
			"The value is not a key generation. A generation is exactly 32 hexadecimal characters, minted from "
			+ "a cryptographic random number generator. A backend version identifier, a key ARN, an ordinal, or "
			+ "any value derived from the key handle or the data subject is not a generation: the destruction "
			+ "ledger is keyed on this value alone, so one that can collide across subjects would report one "
			+ "subject's live data as erased by another subject's erasure.",
			nameof(value));
	}

	/// <summary>
	/// Attempts to parse a generation, without throwing.
	/// </summary>
	/// <param name="value">The candidate identifier; may be <see langword="null"/>.</param>
	/// <param name="generation">The parsed generation, or <see langword="default"/> when parsing failed.</param>
	/// <returns><see langword="true"/> when <paramref name="value"/> is a well-formed generation.</returns>
	/// <remarks>
	/// For a provider reading a generation back from a backend that may not carry one. A
	/// <see langword="false"/> means the material has no identity this framework can state, which a caller
	/// reports as absent -- never as a value it invented.
	/// </remarks>
	public static bool TryParse([NotNullWhen(true)] string? value, out KeyGeneration generation)
	{
		generation = default;

		if (value is null || value.Length != HexLength)
		{
			return false;
		}

		foreach (var character in value)
		{
			var isHexDigit = character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
			if (!isHexDigit)
			{
				return false;
			}
		}

		generation = new KeyGeneration(value);

		return true;
	}

	/// <summary>
	/// Returns the characters that travel on an envelope and are stored in a destruction record.
	/// </summary>
	/// <returns>
	/// The 32-character hexadecimal identifier, or an empty string for a <see langword="default"/> instance.
	/// </returns>
	/// <remarks>
	/// Byte-identical to what <see cref="Parse"/> accepted or <see cref="Mint"/> produced. The serialized form
	/// is part of the contract: it is bound into the AES-GCM associated data, so a change to it would make
	/// already-written payloads fail to authenticate.
	/// </remarks>
	public override string ToString() => _value ?? string.Empty;

	/// <inheritdoc/>
	/// <remarks>
	/// Ordinal and case-sensitive. Two generations differing only in case are different records, because the
	/// ledger compares the stored characters; collapsing case here while the database does not would report a
	/// destruction against material that was never destroyed.
	/// </remarks>
	public bool Equals(KeyGeneration other) => string.Equals(_value, other._value, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is KeyGeneration other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

	/// <summary>Reports whether two generations name the same material.</summary>
	/// <param name="left">The first generation.</param>
	/// <param name="right">The second generation.</param>
	/// <returns><see langword="true"/> when both name the same material.</returns>
	public static bool operator ==(KeyGeneration left, KeyGeneration right) => left.Equals(right);

	/// <summary>Reports whether two generations name different material.</summary>
	/// <param name="left">The first generation.</param>
	/// <param name="right">The second generation.</param>
	/// <returns><see langword="true"/> when they name different material.</returns>
	public static bool operator !=(KeyGeneration left, KeyGeneration right) => !left.Equals(right);
}
