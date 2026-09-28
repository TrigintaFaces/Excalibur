// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Delivers only the CONTIGUOUS run of global-stream events starting immediately after the caller's
/// position, stopping at the first gap.
/// </summary>
/// <remarks>
/// <para>
/// <b>Without this, a subscriber can silently and permanently skip a committed event.</b> The store
/// guarantees that at every INSTANT the set of committed global positions is a contiguous prefix.
/// That is a predicate on a STATE, and a scan SPANS states: a
/// <c>SELECT ... WHERE Position &gt; @cp ORDER BY Position</c> examines each slot at a different
/// moment and never returns to one it has passed. One writer is enough — the scan passes slot
/// <c>N</c> while <c>N</c> is uncommitted, <c>N</c> commits, <c>N+1</c> commits, the scan reaches
/// <c>N+1</c> and returns it. The caller advances its high-water mark past <c>N</c>, and no later
/// scan from that checkpoint revisits it.
/// </para>
/// <para>
/// <b>The counter row does not prevent it.</b> It totally orders COMMITS, so position 2 cannot commit
/// before position 1 — true, and irrelevant, because the scan's READS are interleaved with those
/// commits rather than ordered against them.
/// </para>
/// <para>
/// <b>The deciding property is whether the scan is ATOMIC with respect to concurrent commits</b>, not
/// any particular vendor default. Under statement-level snapshot semantics (SQL Server with
/// <c>READ_COMMITTED_SNAPSHOT</c> on; PostgreSQL and Oracle READ COMMITTED, which are MVCC) the scan
/// is atomic and the skip cannot occur. Under SQL Server's locking READ COMMITTED — the default for a
/// self-hosted instance — it is not atomic and the skip is reachable. This decorator defends
/// regardless, which is why it is expressed as a property of the READ rather than a configuration
/// requirement placed on the consumer.
/// </para>
/// <para>
/// <b>WHY THIS IS A DECORATOR AND NOT A LINE IN EACH PROVIDER.</b> It is one correctness rule over an
/// interface with several implementations. Copied into each provider it becomes five copies, and the
/// next provider is written by copying whichever sibling its author happened to open — so the
/// guarantee holds or not depending on that choice. Here there is one implementation and one arm.
/// </para>
/// <para>
/// <b>THE PRECONDITION, which bounds where it may be applied.</b> Treating a missing position as
/// in-flight is sound ONLY for a store whose positions are gapless by construction — one that
/// allocates inside the appending transaction, so an aborted append returns its position rather than
/// burning it. Wrapped around a store that allocates from an identity column or a sequence this would
/// wait forever on a hole that never fills. It is therefore registered BY the providers that make the
/// gapless guarantee, and that registration is where the guarantee is asserted.
/// </para>
/// <para>
/// <b>THE TRADE, stated.</b> If a position is ever permanently absent this stalls the subscriber
/// rather than skipping past it. That is the correct direction — a stall is loud and observable, a
/// skipped event is silent and unrecoverable — but it is not hypothetical. Archival used to DELETE
/// event rows and now tombstones them in place, so a database archived by an earlier version carries
/// permanent holes and a subscriber whose checkpoint sits below one will not advance past it. That
/// needs a documented remedy before this reaches such a deployment.
/// </para>
/// </remarks>
/// <param name="inner">The provider query being decorated.</param>
/// <param name="logger">Records where a read stopped, so a gap is never silent.</param>
internal sealed partial class ContiguousGlobalStreamQuery(
	IGlobalStreamQuery inner,
	ILogger<ContiguousGlobalStreamQuery> logger) : IGlobalStreamQuery
{
	private readonly IGlobalStreamQuery _inner = inner ?? throw new ArgumentNullException(nameof(inner));
	private readonly ILogger<ContiguousGlobalStreamQuery> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		// A negative checkpoint makes expectedNext <= 0, which no real position can equal, so every
		// read would truncate to empty and the caller would stall permanently and silently. The store
		// column is BIGINT with no CHECK, and Start is 0, so this is reachable through a corrupted or
		// hand-edited checkpoint rather than through the API.
		ArgumentOutOfRangeException.ThrowIfNegative(position.Position);

		var events = await _inner.ReadAllAsync(position, maxCount, cancellationToken).ConfigureAwait(false);
		if (events.Count == 0)
		{
			return events;
		}

		var expectedNext = position.Position + 1;
		for (var i = 0; i < events.Count; i++)
		{
			var actual = events[i].GlobalPosition;
			if (actual == expectedNext)
			{
				expectedNext++;
				continue;
			}

			if (actual < expectedNext)
			{
				// A position BELOW the one we expect is not a gap -- it is the provider breaking the
				// ascending-order contract this decorator reads by, or returning a duplicate. Left
				// unhandled it presents as a gap, and a gap stalls the caller FOREVER at a position that
				// will never fill, quietly. Those are different faults with different remedies, and the
				// cost of conflating them is that a provider bug becomes an eternal silent stall.
				throw new InvalidOperationException(
					$"Global-stream provider {_inner.GetType().Name} returned position {actual} at index {i} "
					+ $"when {expectedNext} or higher was required. ReadAllAsync must return positions in "
					+ "strictly ascending order with no duplicates; contiguity cannot be established over an "
					+ "unordered read, and treating this as a gap would stall every subscriber permanently.");
			}

			// A gap. Everything beyond it sits above a position that has not committed, so returning
			// any of it would let the caller advance its mark past the missing one.
			//
			// THIS MUST NOT BE SILENT. A short read is indistinguishable from "no new events" to every
			// caller, and an unexplained stall is the same class of failure as the silent skip this
			// exists to prevent -- one level up.
			LogStoppedAtGap(expectedNext, actual, i);
			return i == 0 ? [] : [.. events.Take(i)];
		}

		return events;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// NOT gap-filtered, and it cannot be: filtering by event type returns a legitimately SPARSE set of
	/// positions, so a gap here carries no information about whether a transaction is in flight. A
	/// caller that advances a high-water mark from this read is exposed to the skip that
	/// <see cref="ReadAllAsync"/> defends against. Tracked; do not assume parity between the two.
	/// </remarks>
	public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken) =>
		_inner.ReadByEventTypeAsync(eventType, position, maxCount, cancellationToken);

	/// <inheritdoc/>
	public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
		_inner.GetHeadPositionAsync(cancellationToken);

	/// <summary>Records that a global-stream read stopped short because the next position was absent.</summary>
	/// <remarks>
	/// Information, and the level was chosen twice. It is not Warning, because a single gap is the
	/// expected, healthy signature of a concurrent append that has not committed yet and warning on it
	/// would train an operator to ignore the one that matters. It is not Debug either, which is what
	/// this was: the default minimum level in a stock host is Information, so a Debug line is invisible
	/// unless someone already suspected the problem -- and the remark below says this MUST NOT be
	/// silent. A level under the default filter is silent. The reasoning for not using Warning: one gap is the expected, healthy signature of
	/// a concurrent append that has not committed yet, and logging that at Warning would train an
	/// operator to ignore it. A gap that PERSISTS is the actionable condition, and detecting
	/// persistence needs state this type does not hold — it belongs on the subscription health state
	/// beside the contested-checkpoint condition. Recorded here so the event exists at all.
	/// </remarks>
	[LoggerMessage(
		EventId = 114650,
		Level = LogLevel.Information,
		Message = "Global stream read stopped at a gap: expected position {ExpectedPosition}, next "
			+ "committed position is {ActualPosition}. Returning {DeliveredCount} event(s); the missing "
			+ "position belongs to an append that has not committed. A gap that persists across polls "
			+ "is not in-flight and needs investigation.")]
	private partial void LogStoppedAtGap(long expectedPosition, long actualPosition, int deliveredCount);
}
