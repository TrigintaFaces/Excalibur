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
	/// Verifies an ordered chain of records, detecting forgery, insertion, deletion, or reordering, and
	/// reporting which of those the break was.
	/// </summary>
	/// <param name="chain">
	/// The ordered links of one chain partition, in the order they were written, streamed. All links must
	/// belong to the same partition: a chain assembled from interleaved partitions reports violations on an
	/// intact trail. The sequence is enumerated once, and only as far as the first break.
	/// </param>
	/// <param name="anchorPriorTag">
	/// The stored tag of the record immediately preceding <paramref name="chain"/>'s first link within the
	/// same partition, or <see langword="null"/> to assert that the first link is the partition's genesis
	/// record.
	/// </param>
	/// <param name="successor">
	/// The link of the record immediately <em>following</em> <paramref name="chain"/> within the same
	/// partition, or <see langword="null"/> when no record follows it. Supplying it pins the right edge of
	/// the range the way <paramref name="anchorPriorTag"/> pins the left: the successor was written to follow
	/// the range's last record, so its keyed MAC no longer verifies once records are removed from the end of
	/// the range. Passing <see langword="null"/> when a successor does exist silently forfeits that
	/// detection.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A result whose <see cref="AuditChainVerificationResult.IsValid"/> is <see langword="true"/> only if every
	/// link verifies, chains to its predecessor, and — when a successor is supplied — is followed by it;
	/// otherwise the index of the first broken link and what kind of break it was.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Each link's MAC is verified against the prior tag taken <b>from the preceding link</b>, never from the
	/// record's own stored claim about its predecessor. That is what makes deletion detectable: a record's
	/// own claim survives the removal of the record it names, whereas the preceding link does not.
	/// </para>
	/// <para>
	/// The stored claim is nonetheless checked, separately, against the predecessor actually present. That is
	/// what makes the claim a <em>verified</em> value rather than an unread one: altering it alone moves no
	/// MAC input, so without this check it passes unnoticed while auditor-facing exports may still read it.
	/// The two checks together also separate the two failures a bare MAC mismatch cannot tell apart — a
	/// record whose contents were rewritten from a record whose predecessor is missing.
	/// </para>
	/// <para>
	/// <paramref name="anchorPriorTag"/> is required rather than optional because the left edge of a verified
	/// range is a decision the caller must make. Supplying <see langword="null"/> for a range whose first link
	/// is not the genesis record reports that link broken — which is the intended detection of records deleted
	/// from the front of the range.
	/// </para>
	/// <para>
	/// The chain is an <see cref="IAsyncEnumerable{T}"/> rather than a materialized list because verification
	/// is a fold carrying exactly one accumulator — the prior tag — and so needs constant space however long
	/// the range is. A list-shaped parameter would make the space cost of a compliance pass grow without
	/// bound in the history it examines, and a compliance pass that exhausts memory on a year of records is
	/// one that gets switched off.
	/// </para>
	/// </remarks>
	ValueTask<AuditChainVerificationResult> VerifyChainAsync(
		IAsyncEnumerable<AuditChainLink> chain,
		string? anchorPriorTag,
		AuditChainLink? successor,
		CancellationToken cancellationToken);
}

/// <summary>
/// One link in an audit hash-chain: a record's canonical content, its stored integrity tag, and its stored
/// claim about which record preceded it.
/// </summary>
/// <param name="CanonicalContent">The deterministic canonical bytes of the record's integrity-covered fields.</param>
/// <param name="Tag">The record's stored integrity tag (<c>v1:{keyId}:{mac}</c>).</param>
/// <param name="StoredPriorTag">
/// The prior tag as stored <em>on the record itself</em>, or <see langword="null"/> when the record claims to
/// be its partition's genesis. This is the record's own claim, not the predecessor actually present; the two
/// are compared, and a disagreement is a violation.
/// </param>
public readonly record struct AuditChainLink(ReadOnlyMemory<byte> CanonicalContent, string Tag, string? StoredPriorTag);

/// <summary>
/// What kind of break a chain verification found, when it found one.
/// </summary>
/// <remarks>
/// The distinction is not cosmetic. A rewritten record, a removed one, and an altered linkage value call for
/// different responses from whoever reads the report, and a bare "verification failed" leaves them unable to
/// tell which happened.
/// </remarks>
public enum AuditChainBreak
{
	/// <summary>No break; the chain verified.</summary>
	None = 0,

	/// <summary>The record carries no integrity tag at all, so nothing about it can be established.</summary>
	UntaggedRecord = 1,

	/// <summary>
	/// The record's MAC does not match its live contents, while its stored claim still names the record that
	/// actually precedes it — so the record's contents were rewritten in place.
	/// </summary>
	ContentAltered = 2,

	/// <summary>
	/// The record's MAC does not match, and its stored claim names a predecessor that is not the record now
	/// preceding it — so a record has been removed, inserted, or reordered.
	/// </summary>
	PredecessorMismatch = 3,

	/// <summary>
	/// The record's MAC verifies, but its stored claim about its predecessor disagrees with the record that
	/// actually precedes it — so the stored linkage value alone was altered.
	/// </summary>
	StoredLinkageAltered = 4,

	/// <summary>
	/// Every record in range verified, but the record following the range does not chain to the range's last
	/// record. Its principal cause is removal of records from the end of the range: the survivors still chain
	/// to one another and to the anchor, so nothing inside the range mentions the removed suffix, and only the
	/// record beyond it still carries the tag that named what was there. It also reports the successor itself
	/// having been altered, which is likewise a break the reader must act on.
	/// </summary>
	SuccessorLinkBroken = 5,
}

/// <summary>
/// The outcome of verifying an audit hash-chain.
/// </summary>
/// <param name="IsValid"><see langword="true"/> when every link verifies and chains correctly.</param>
/// <param name="FirstBrokenIndex">
/// The zero-based index of the first link that failed verification, or <c>-1</c> when the chain is valid. When
/// <see cref="Break"/> is <see cref="AuditChainBreak.SuccessorLinkBroken"/> the break is at the successor, one
/// past the last in-range link, so this is the number of links examined.
/// </param>
/// <param name="Break">What kind of break was found, or <see cref="AuditChainBreak.None"/>.</param>
public readonly record struct AuditChainVerificationResult(bool IsValid, int FirstBrokenIndex, AuditChainBreak Break);
