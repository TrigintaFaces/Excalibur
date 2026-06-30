// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging;

/// <summary>
/// The single shared tamper-evidence contract for audit trails: a keyed message authentication code
/// (HMAC) chained across records, so neither forging a record's contents nor inserting, deleting, or
/// reordering records can go undetected by an actor without the signing key.
/// </summary>
/// <remarks>
/// <para>
/// The strategy operates on opaque, backend-produced <c>canonicalContent</c> (see
/// <see cref="AuditRecordCanonicalizer"/>) so a single implementation serves every audit sink regardless
/// of its concrete record type. Each backend canonicalizes its own record's integrity-covered fields to
/// bytes, then drives this strategy.
/// </para>
/// <para>
/// <b>Security invariants (load-bearing):</b>
/// <list type="bullet">
/// <item><description>The MAC is <b>keyed</b> (HMAC, key from the signing-key provider held outside the
/// audit store). Producing a tag without the key is impossible — there is no unkeyed fallback. When the
/// key cannot be obtained, tag computation and verification <b>fail closed</b> (throw / report invalid),
/// never emitting or accepting an unprotected tag.</description></item>
/// <item><description>The chain link's MAC covers the canonical content <em>and</em> the prior record's
/// tag, so insert/delete/reorder breaks the chain.</description></item>
/// <item><description>Verification uses a constant-time comparison; a missing, malformed, or
/// unknown-key tag is treated as a violation, never as valid.</description></item>
/// <item><description>Tags are versioned (<c>v1:{keyId}:{mac}</c>) to support key rotation and
/// algorithm agility.</description></item>
/// </list>
/// </para>
/// <para>
/// Verification re-canonicalizes the <em>live reloaded</em> fields (never a persisted canonical blob) so
/// it checks the queryable record that an attacker could tamper with.
/// </para>
/// </remarks>
public interface IAuditIntegrityStrategy
{
	/// <summary>
	/// Computes the versioned, keyed integrity tag for a record's canonical content, chained to the prior
	/// record's tag.
	/// </summary>
	/// <param name="canonicalContent">The deterministic canonical bytes of the record's integrity-covered fields.</param>
	/// <param name="priorTag">The prior record's tag, or <see langword="null"/> for the genesis record.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The versioned integrity tag in the form <c>v1:{keyId}:{mac}</c>.</returns>
	/// <exception cref="System.InvalidOperationException">Thrown (fail-closed) when no signing key is available.</exception>
	ValueTask<string> ComputeTagAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, CancellationToken cancellationToken);

	/// <summary>
	/// Verifies a single record's integrity tag against its live canonical content and prior tag.
	/// </summary>
	/// <param name="canonicalContent">The deterministic canonical bytes of the reloaded record's live fields.</param>
	/// <param name="priorTag">The prior record's tag, or <see langword="null"/> for the genesis record.</param>
	/// <param name="tag">The stored tag to verify.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> only if the tag is well-formed, its key is known, and the keyed MAC matches; otherwise <see langword="false"/>.</returns>
	ValueTask<bool> VerifyAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, string tag, CancellationToken cancellationToken);

	/// <summary>
	/// Verifies an ordered chain of records, detecting forgery, insertion, deletion, or reordering.
	/// </summary>
	/// <param name="chain">The ordered links (canonical content + stored tag) from genesis to head.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A result whose <see cref="AuditChainVerificationResult.IsValid"/> is <see langword="true"/> only if every
	/// link verifies and chains to its predecessor; otherwise the index of the first broken link.
	/// </returns>
	ValueTask<AuditChainVerificationResult> VerifyChainAsync(IReadOnlyList<AuditChainLink> chain, CancellationToken cancellationToken);
}

/// <summary>
/// One link in an audit hash-chain: a record's canonical content paired with its stored integrity tag.
/// </summary>
/// <param name="CanonicalContent">The deterministic canonical bytes of the record's integrity-covered fields.</param>
/// <param name="Tag">The record's stored integrity tag (<c>v1:{keyId}:{mac}</c>).</param>
public readonly record struct AuditChainLink(ReadOnlyMemory<byte> CanonicalContent, string Tag);

/// <summary>
/// The outcome of verifying an audit hash-chain.
/// </summary>
/// <param name="IsValid"><see langword="true"/> when every link verifies and chains correctly.</param>
/// <param name="FirstBrokenIndex">
/// The zero-based index of the first link that failed verification, or <c>-1</c> when the chain is valid.
/// </param>
public readonly record struct AuditChainVerificationResult(bool IsValid, int FirstBrokenIndex);
