// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

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
	/// Gets the identifier of this key's current GENERATION — the material provisioned at this handle — or
	/// <see langword="null"/> when the provider cannot identify one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Minted when material is provisioned, never reused, and never derived from the handle.</b> A handle is
	/// derived from the thing it protects, so it is stable and re-occupiable: destroying a key and provisioning
	/// another at the same handle restarts <see cref="Version"/> at 1, and handle-plus-version then designates
	/// two different pieces of material at two different times. A generation identifier is what lets a reader
	/// of an older payload establish that the material it was written under is gone.
	/// </para>
	/// <para>
	/// <b>Null is a real answer and it is not benign.</b> A provider that cannot identify a generation cannot
	/// support a crypto-shred read path, because a destroyed key and a re-provisioned one are
	/// indistinguishable through it. Callers that depend on the distinction refuse rather than assume -- and
	/// the nullability is deliberate for exactly that case: a handle provisioned outside this framework has no
	/// lineage identity we can honestly state, and inventing one on read would give material destroyed
	/// elsewhere a fresh identifier and present it as live.
	/// </para>
	/// <para>
	/// <b>A <see cref="KeyGeneration"/> rather than a <see cref="string"/>, because the ledger keys on it.</b>
	/// A string-typed generation accepts an ordinal, a backend version identifier or a value derived from the
	/// subject, each of which can collide across subjects -- and a collision under a generation-keyed
	/// destruction ledger reports one subject's live data as erased by another's erasure. The type admits only
	/// a CSPRNG mint or a 32-hexadecimal-character parse, so that value cannot be constructed here at all.
	/// </para>
	/// </remarks>
	public KeyGeneration? Generation { get; init; }

	/// <summary>
	/// Gets the current lifecycle status of the key.
	/// </summary>
	public required KeyStatus Status { get; init; }

	/// <summary>
	/// Gets the encryption algorithm this key is used with.
	/// </summary>
	public required EncryptionAlgorithm Algorithm { get; init; }

	/// <summary>
	/// Gets the instant the backend reports this key version was created, or <see langword="null"/> when it
	/// reports none.
	/// </summary>
	/// <value>
	/// The creation instant of the version this metadata describes, or <see langword="null"/> when the
	/// key-management backend does not supply one.
	/// </value>
	/// <remarks>
	/// <para>
	/// <b><see langword="null"/> means NOT KNOWN, and it is never a substitute for one.</b> A provider that
	/// cannot learn the instant reports <see langword="null"/>; it must never stamp its own clock, and must
	/// never use a sentinel such as <see cref="DateTimeOffset.MinValue"/>. Both are fabrications a caller
	/// cannot tell apart from a measurement, and a fabricated "now" is worse than no value at all: it sorts
	/// the oldest material as the newest, so stale data reads as current and is never re-encrypted.
	/// </para>
	/// <para>
	/// <b>Callers treat an unknown instant as STALE, never as recent.</b> Where this field decides whether a
	/// key is due for rotation or has exceeded a maximum age, <see langword="null"/> counts as due and as
	/// exceeded. Where it orders keys, <see langword="null"/> sorts oldest. That direction costs extra work
	/// and never costs safety, which is the only acceptable way to resolve an absence on a path like this.
	/// </para>
	/// <para>
	/// <b>It describes the version this metadata is about</b>, not the first version of the handle. Two
	/// versions of one key may therefore report different instants, and a caller may use them to order those
	/// versions only when neither is <see langword="null"/>.
	/// </para>
	/// </remarks>
	public required DateTimeOffset? CreatedAt { get; init; }

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
