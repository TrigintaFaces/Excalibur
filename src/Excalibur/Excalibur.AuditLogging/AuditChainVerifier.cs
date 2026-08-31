// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Compliance;

namespace Excalibur.AuditLogging;

/// <summary>
/// One chain partition's contribution to a verification: the records in range, in the order they were
/// written, together with the tags that bind them to whatever preceded and followed the range.
/// </summary>
/// <param name="AnchorPriorTag">
/// The stored tag of the record immediately preceding <paramref name="Events"/> within this same partition,
/// or <see langword="null"/> to assert that the first record in range is the partition's genesis record.
/// </param>
/// <param name="Events">
/// The partition's in-range records, streamed in the order they were written. Ordering by a value an attacker
/// or a skewed clock controls — a timestamp, for instance — rather than by write order will report an intact
/// trail broken. The sequence is enumerated once.
/// </param>
/// <param name="Successor">
/// The record immediately <em>following</em> <paramref name="Events"/> within this same partition, or
/// <see langword="null"/> when the range runs to the end of the partition's chain. It pins the right edge:
/// the successor was written to follow the range's last record, so removing records from the end of the range
/// breaks its keyed MAC. Omitting it when a successor does exist forfeits that detection silently.
/// </param>
/// <remarks>
/// A partition is the unit a store chains over on write. A store that writes one chain per tenant supplies
/// one partition per tenant; a store that chains per tenant and application supplies one per pair. Records
/// drawn from two partitions must never be combined into one instance: their tags interleave, so each
/// record's prior tag names a predecessor that is not its neighbour here.
/// </remarks>
public readonly record struct AuditChainPartition(
	string? AnchorPriorTag,
	IAsyncEnumerable<AuditEvent> Events,
	AuditEvent? Successor)
{
	/// <summary>
	/// Creates a partition from records a caller already holds in memory.
	/// </summary>
	/// <param name="anchorPriorTag">The tag of the record immediately preceding the range, or <see langword="null"/> for genesis.</param>
	/// <param name="events">The partition's in-range records, in the order they were written.</param>
	/// <param name="successor">The record immediately following the range in this partition, or <see langword="null"/>.</param>
	/// <returns>A partition that streams <paramref name="events"/> in order.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="events"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// A convenience for callers that have a list, not a requirement to produce one. Verification itself needs
	/// only a forward stream, and a store able to read its records lazily should supply one rather than
	/// materialize the range to call this.
	/// </remarks>
	public static AuditChainPartition FromList(
		string? anchorPriorTag,
		IReadOnlyList<AuditEvent> events,
		AuditEvent? successor)
	{
		ArgumentNullException.ThrowIfNull(events);
		return new AuditChainPartition(anchorPriorTag, Stream(events), successor);

		static async IAsyncEnumerable<AuditEvent> Stream(IReadOnlyList<AuditEvent> source)
		{
			for (var i = 0; i < source.Count; i++)
			{
				yield return source[i];
			}

			await Task.CompletedTask.ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Verifies audit hash-chains on behalf of an audit store, establishing both halves of tamper-evidence —
/// that no record's contents were altered, and that no record was inserted, deleted, or reordered — in a
/// single pass shared by every provider.
/// </summary>
/// <remarks>
/// <para>
/// Both detections come from one walk. Each record's keyed MAC is verified against the prior tag taken
/// <b>from the record that actually precedes it</b>, rather than from the record's own stored claim about
/// its predecessor. Altering a covered field breaks the MAC; removing a record breaks the next survivor's,
/// because the tag it was written with names a predecessor that is no longer there. A store that instead
/// verifies each record against its own stored claim checks only the first of those, and will report a
/// trail with records removed from it as intact.
/// </para>
/// <para>
/// The stored claim is separately compared against the predecessor actually present, so that altering it
/// alone — which moves no MAC input — is reported rather than passing unnoticed.
/// </para>
/// <para>
/// This type exists so that a provider does not have to rediscover those distinctions. A store should read
/// its records, group them into partitions matching how it chains on write, resolve each partition's anchor
/// and successor, and call <see cref="VerifyAsync"/> — leaving no per-provider verification logic to diverge.
/// </para>
/// </remarks>
public static class AuditChainVerifier
{
	/// <summary>
	/// Verifies every supplied chain partition and renders the outcome as an
	/// <see cref="AuditIntegrityResult"/>.
	/// </summary>
	/// <param name="integrity">The keyed integrity strategy that holds the signing key.</param>
	/// <param name="partitions">
	/// The chain partitions in range. Supply one entry per partition the store chains over; supplying a
	/// single entry holding records from several partitions reports violations on an intact trail.
	/// </param>
	/// <param name="startDate">The inclusive start of the window examined, recorded on the result.</param>
	/// <param name="endDate">The inclusive end of the window examined, recorded on the result.</param>
	/// <param name="isHashChained">
	/// Whether the store's write path chained these records. Pass <see langword="false"/> from a store whose
	/// chaining is switched off: each partition is then a single record, so the reported quantity counts
	/// records rather than chains, and the result says so.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see cref="AuditIntegrityOutcome.NoEventsInScope"/> when the partitions hold no records at all;
	/// <see cref="AuditIntegrityOutcome.ViolationsDetected"/> naming the earliest failing record when any
	/// partition's chain does not hold; otherwise <see cref="AuditIntegrityOutcome.Verified"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="integrity"/> or <paramref name="partitions"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Each partition is walked until its first failure, so
	/// <see cref="AuditIntegrityResult.CompromisedChainCount"/> counts <em>compromised partitions</em>, not
	/// failing records. Once a chain breaks, every record after the break is unverifiable rather than
	/// independently sound, so counting further failures within one partition would report the consequences
	/// of a single break as though they were separate findings.
	/// </para>
	/// <para>
	/// A partition is the store's write-time chaining unit, so the unit this quantity is in depends on the
	/// store's configuration: with chaining on, a partition is a chain and two altered records in one chain
	/// count once; with chaining off, the store supplies one partition per record and the same quantity
	/// counts records. That is why <paramref name="isHashChained"/> is required rather than inferred — it
	/// travels onto the result so a reader is never handed a number whose unit they cannot see.
	/// </para>
	/// </remarks>
	public static async Task<AuditIntegrityResult> VerifyAsync(
		IAuditIntegrityStrategy integrity,
		IReadOnlyList<AuditChainPartition> partitions,
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		bool isHashChained,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(integrity);
		ArgumentNullException.ThrowIfNull(partitions);

		long eventsExamined = 0;
		var compromisedPartitions = 0;
		PartitionFailure? earliestFailure = null;

		foreach (var partition in partitions)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (partition.Events is null)
			{
				continue;
			}

			var (examined, failure) =
				await VerifyPartitionAsync(integrity, partition, cancellationToken).ConfigureAwait(false);

			eventsExamined += examined;

			if (failure is null)
			{
				continue;
			}

			compromisedPartitions++;

			// The earliest failure by write time, not by the order partitions happen to be supplied in —
			// partition ordering carries no meaning, so choosing by it would make the reported record vary
			// between runs over identical data.
			if (earliestFailure is null || failure.Value.Timestamp < earliestFailure.Value.Timestamp)
			{
				earliestFailure = failure;
			}
		}

		// Nothing was examined, so nothing was established. Reported distinctly rather than as a pass: an
		// unexpectedly empty window may itself be the evidence that records are not reaching the store.
		if (eventsExamined == 0)
		{
			return AuditIntegrityResult.NoEventsInScope(startDate, endDate);
		}

		if (compromisedPartitions > 0)
		{
			return AuditIntegrityResult.ViolationsDetected(
				eventsExamined,
				startDate,
				endDate,
				earliestFailure!.Value.EventId,
				earliestFailure.Value.Description,
				compromisedPartitions,
				isHashChained);
		}

		return AuditIntegrityResult.Verified(eventsExamined, startDate, endDate, isHashChained);
	}

	private static async Task<(long Examined, PartitionFailure? Failure)> VerifyPartitionAsync(
		IAuditIntegrityStrategy integrity,
		AuditChainPartition partition,
		CancellationToken cancellationToken)
	{
		// The walk stops at the first break, so the cursor's last two records are the broken one and its
		// predecessor at the moment the strategy returns. Retaining those two — and nothing else — is what
		// lets a violation be described by name without holding the range in memory to look it up afterwards.
		var cursor = new ChainCursor();

		var result = await integrity
			.VerifyChainAsync(
				cursor.ProjectAsync(partition.Events, cancellationToken),
				partition.AnchorPriorTag,
				ProjectSuccessor(partition.Successor),
				cancellationToken)
			.ConfigureAwait(false);

		if (result.IsValid)
		{
			return (cursor.Examined, null);
		}

		var describedAsSuccessor = result.Break == AuditChainBreak.SuccessorLinkBroken
			|| (result.Break == AuditChainBreak.UntaggedRecord && result.FirstBrokenIndex >= cursor.Examined);

		var broken = describedAsSuccessor ? partition.Successor : cursor.Current;

		// A break at a record the walk never named — an empty partition supplied with a successor, or a
		// strategy reporting an index it never enumerated. Reported rather than swallowed: a violation that
		// cannot be described is still a violation, and dropping it would turn a broken chain into a pass.
		if (broken is null)
		{
			return (cursor.Examined, new PartitionFailure(
				UnnamedRecordId,
				DateTimeOffset.MinValue,
				UndescribableBreakDescription));
		}

		return (cursor.Examined, new PartitionFailure(
			broken.EventId,
			broken.Timestamp,
			Describe(result.Break, broken, cursor)));
	}

	/// <summary>One partition's verification failure, reduced to what the reported result needs.</summary>
	private readonly record struct PartitionFailure(string EventId, DateTimeOffset Timestamp, string Description);

	private static string Describe(AuditChainBreak brokenKind, AuditEvent broken, ChainCursor cursor) =>
		brokenKind switch
		{
			AuditChainBreak.UntaggedRecord => FormattableString.Invariant(
				$"Event '{broken.EventId}' carries no integrity tag, so neither its contents nor its place in the chain can be established."),

			AuditChainBreak.ContentAltered => FormattableString.Invariant(
				$"Integrity tag mismatch for event '{broken.EventId}' at {broken.Timestamp:O}: the record's stored tag does not match its current contents."),

			AuditChainBreak.StoredLinkageAltered => FormattableString.Invariant(
				$"Stored chain linkage altered at event '{broken.EventId}' at {broken.Timestamp:O}: the record verifies against {PredecessorPhrase(cursor)}, but its own stored prior tag names a different record, so the stored linkage value was rewritten."),

			AuditChainBreak.SuccessorLinkBroken => FormattableString.Invariant(
				$"Chain link broken at event '{broken.EventId}' at {broken.Timestamp:O}, the record following the verified range: it was written to follow a record that is no longer the last record in range, so records have been removed from the end of the range (or that following record was itself altered)."),

			// PredecessorMismatch, and any break a future strategy reports that this one does not yet name.
			// Described as a broken link rather than dismissed: an unrecognised break is still a break.
			_ => FormattableString.Invariant(
				$"Chain link broken at event '{broken.EventId}' at {broken.Timestamp:O}: it was written to follow a record whose tag is not the tag of {PredecessorPhrase(cursor)}, so a record has been removed, inserted, or reordered."),
		};

	private static string PredecessorPhrase(ChainCursor cursor) =>
		cursor.Previous is null
			? "the record preceding the verified range"
			: FormattableString.Invariant($"event '{cursor.Previous.EventId}'");

	private static AuditChainLink? ProjectSuccessor(AuditEvent? successor) =>
		successor is null
			? null
			: new AuditChainLink(
				AuditEventCanonicalizer.Canonicalize(successor),
				successor.EventHash ?? string.Empty,
				successor.PreviousEventHash);

	private const string UnnamedRecordId = "(unidentified)";

	private const string UndescribableBreakDescription =
		"A chain partition failed verification at a record the verification could not name. Treat the partition as compromised.";

	/// <summary>
	/// Projects records to links while remembering only the current record and the one before it.
	/// </summary>
	/// <remarks>
	/// The two retained records are what a violation description needs — the broken record and the
	/// predecessor it was supposed to follow — and they are the only state the pass keeps beyond the fold's
	/// own accumulator, so memory does not grow with the range.
	/// </remarks>
	private sealed class ChainCursor
	{
		public AuditEvent? Previous { get; private set; }

		public AuditEvent? Current { get; private set; }

		public long Examined { get; private set; }

		public async IAsyncEnumerable<AuditChainLink> ProjectAsync(
			IAsyncEnumerable<AuditEvent> events,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			await foreach (var auditEvent in events.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				Previous = Current;
				Current = auditEvent;
				Examined++;

				// An absent tag is passed through as empty rather than filtered out: the strategy reports it as
				// an untagged record, so clearing a record's tag cannot stand in for deleting the record.
				yield return new AuditChainLink(
					AuditEventCanonicalizer.Canonicalize(auditEvent),
					auditEvent.EventHash ?? string.Empty,
					auditEvent.PreviousEventHash);
			}
		}
	}
}
