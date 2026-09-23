// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Tracks per-partition consumption progress so a Kafka offset commit can only ever name a position that
/// every lower offset has been dealt with — never a position past work that is still owed.
/// </summary>
/// <remarks>
/// <para>
/// Kafka's committed offset is a single number per partition and it means <c>"every offset below this
/// has been dealt with"</c>. A receiver that hands out a batch and settles its members out of order
/// therefore cannot commit each message as it settles: committing <c>n+3</c> when <c>n</c> is still in a
/// handler declares <c>n</c> dealt with, and a restart resumes past it. This type exists to make that
/// declaration impossible to express.
/// </para>
/// <para>
/// <b>The invariant, and how the shape enforces it.</b> An offset is <em>owed</em> while it is in a
/// caller's hands (dispatched and not settled) or waiting to be replayed (requeued). The commit position
/// is the lowest owed offset, or one past the highest offset this tenure has fetched when nothing is owed:
/// </para>
/// <code>position = min( min(dispatched), min(awaiting redelivery), highest fetched + 1 )</code>
/// <para>
/// Every offset below that position was therefore either settled or never delivered in this tenure, by
/// construction. Kafka offsets are not contiguous — a transaction marker occupies an offset and is never
/// delivered, and compaction removes records — so the position is defined by what is owed rather than by
/// draining settled offsets one at a time, which would halt at the first offset that never arrives. The
/// memory held is proportional to the work in flight, not to the work settled.
/// </para>
/// <para>
/// A commit is emitted only when the position exceeds the one the broker last accepted, so a late settle
/// cannot rewind the broker.
/// </para>
/// <para>
/// <b>Ownership.</b> Confluent.Kafka does not expose the group generation id, so the assignment epoch is
/// taken from Kafka's own rebalance callbacks: <see cref="OnPartitionsAssigned"/> increments a
/// per-partition generation and discards the previous tenure's state. A receipt minted under an earlier
/// generation is refused by <see cref="TrySettle"/> and <see cref="TryRequeue"/> and cannot move the new
/// owner's position.
/// </para>
/// <para>
/// This type holds no Kafka client and performs no I/O; it decides positions and the caller performs the
/// commit or seek. That keeps the arithmetic testable without a broker.
/// </para>
/// </remarks>
internal sealed class KafkaPartitionProgress
{
	private readonly ConcurrentDictionary<TopicPartition, PartitionState> _partitions = new();

	/// <summary>
	/// Gets the number of offsets that have been dispatched and not yet settled, across all partitions.
	/// </summary>
	public int Outstanding
	{
		get
		{
			var total = 0;
			foreach (var state in _partitions.Values)
			{
				lock (state.Gate)
				{
					total += state.Dispatched.Count;
				}
			}

			return total;
		}
	}

	/// <summary>
	/// Records that <paramref name="offset"/> was fetched from <paramref name="partition"/> and is about to
	/// be dispatched, and reports the generation the caller must present when settling it.
	/// </summary>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset that was fetched.</param>
	/// <param name="generation">The assignment generation this delivery belongs to.</param>
	/// <returns>
	/// <see langword="true"/> when the offset should be dispatched; <see langword="false"/> when this tenure
	/// has already handed it out and it is not awaiting redelivery — the replay a requeue seek causes — in
	/// which case the caller must drop it rather than hand the same work out twice.
	/// </returns>
	public bool TryBeginDelivery(TopicPartition partition, long offset, out long generation)
	{
		var decision = BeginDelivery(partition, offset);
		generation = decision.Generation;
		return decision.Deliver;
	}

	/// <summary>
	/// Records that <paramref name="offset"/> was fetched from <paramref name="partition"/> and decides what the
	/// caller must do with it, including whether the partition must first be sought back.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A lost seek is detected here.</b> Offsets arrive in order from the fetch position, so a fetched offset
	/// above the lowest offset awaiting redelivery means the fetch position has passed an owed offset without
	/// delivering it: the seek that should have replayed it threw, or a later seek overtook it. The decision asks
	/// for a seek back to that offset, and the record is not delivered, because it will be fetched again.
	/// </para>
	/// <para>
	/// <b>A requeued offset can cease to exist.</b> Compaction removes a record once a later record with the same
	/// key supersedes it, and it may do so between the requeue and the replay. The replay then starts at the next
	/// surviving offset, above the owed one, and seeking back again would repeat forever. After two consecutive
	/// seeks back to the same offset each return a first record above it, the offset is released as gone. Two,
	/// not one: releasing an offset that still exists would let the position pass a message that was never
	/// redelivered, so the conclusion must hold even if a record fetched before a seek is delivered after it.
	/// </para>
	/// <para>
	/// <b>Only a seek that happened counts.</b> A record past the owed offset is evidence that the offset is gone
	/// only if the seek back before it was actually performed, which the caller reports through
	/// <see cref="NoteSeekIssued"/> once its seek returns. A seek that threw proves nothing, so the tracker asks for
	/// it again without counting; otherwise two failed seeks would release an offset that was never re-fetched.
	/// </para>
	/// </remarks>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset that was fetched.</param>
	/// <returns>The decision the caller must act on.</returns>
	public KafkaDeliveryDecision BeginDelivery(TopicPartition partition, long offset)
	{
		ArgumentNullException.ThrowIfNull(partition);

		var state = _partitions.GetOrAdd(partition, static _ => new PartitionState());
		lock (state.Gate)
		{
			var generation = state.Generation;
			long? abandoned = null;

			while (state.AwaitingRedelivery.Count > 0 && offset > state.AwaitingRedelivery.Min)
			{
				var owed = state.AwaitingRedelivery.Min;
				if (state.ReseekTarget == owed)
				{
					// Counted only when the seek back before this record was performed; a seek that threw leaves
					// nothing to conclude, and is simply asked for again.
					if (state.ReseekIssued)
					{
						state.ReseekMisses++;
						state.ReseekIssued = false;
					}
				}
				else
				{
					state.ReseekTarget = owed;
					state.ReseekMisses = 0;
					state.ReseekIssued = false;
				}

				if (state.ReseekMisses < 2)
				{
					return new KafkaDeliveryDecision(false, generation, owed, abandoned);
				}

				// Two consecutive seeks back to this offset each returned a later record first: it is no longer in
				// the log. Release it and look again, in case another owed offset sits behind it.
				_ = state.AwaitingRedelivery.Remove(owed);
				state.ReseekTarget = null;
				state.ReseekMisses = 0;
				state.ReseekIssued = false;
				abandoned = owed;
			}

			if (state.ReseekTarget is { } target && offset <= target)
			{
				// The seek back arrived: the owed offset (or an earlier one) is being fetched again.
				state.ReseekTarget = null;
				state.ReseekMisses = 0;
				state.ReseekIssued = false;
			}

			// A seek performed for a requeue rewinds the partition, so the poll that follows replays every
			// offset from the seek point — not only the requeued one. A seek does not move the high water, so
			// every offset at or below it has been handed out already: settled, still in someone's hands, or
			// requeued. Only the requeued ones are owed a redelivery. This one predicate also covers an offset
			// the forward fetch skipped, because nothing below the high water is ever fetched afresh.
			if (state.HighWater is { } highWater
				&& offset <= highWater
				&& !state.AwaitingRedelivery.Contains(offset))
			{
				return new KafkaDeliveryDecision(false, generation, null, abandoned);
			}

			state.Floor ??= offset;
			if (state.HighWater is not { } fetched || offset > fetched)
			{
				state.HighWater = offset;
			}

			_ = state.Dispatched.Add(offset);
			_ = state.AwaitingRedelivery.Remove(offset);
			return new KafkaDeliveryDecision(true, generation, null, abandoned);
		}
	}

	/// <summary>
	/// Records that the seek back a <see cref="KafkaDeliveryDecision"/> asked for was performed.
	/// </summary>
	/// <remarks>
	/// Call only after the seek returned; never before it, and never when it threw. Only a performed seek lets a
	/// later record count as evidence that the owed offset no longer exists.
	/// </remarks>
	/// <param name="partition">The partition that was sought.</param>
	/// <param name="offset">The offset it was sought to.</param>
	public void NoteSeekIssued(TopicPartition partition, long offset)
	{
		ArgumentNullException.ThrowIfNull(partition);

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return;
		}

		lock (state.Gate)
		{
			if (state.ReseekTarget == offset)
			{
				state.ReseekIssued = true;
			}
		}
	}

	/// <summary>
	/// Reports whether <paramref name="generation"/> is the partition's current assignment epoch — that is,
	/// whether a receipt issued under it still belongs to the tenure that owns the partition now.
	/// </summary>
	/// <remarks>
	/// <see cref="TrySettle"/> enforces this itself; this exists so a caller can tell a refusal apart from
	/// a settlement that simply did not move the position. Both return "nothing to commit", and reporting a
	/// refusal as a successful acknowledgment would hide a rebalance from the operator.
	/// </remarks>
	/// <param name="partition">The partition the receipt names.</param>
	/// <param name="generation">The generation carried by the receipt.</param>
	/// <returns><see langword="true"/> when the generation is current for a tracked partition.</returns>
	public bool IsCurrentGeneration(TopicPartition partition, long generation)
	{
		ArgumentNullException.ThrowIfNull(partition);

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return false;
		}

		lock (state.Gate)
		{
			return generation == state.Generation;
		}
	}

	/// <summary>
	/// Settles <paramref name="offset"/> as terminal and reports the offset to commit, if the position
	/// advanced past what the broker was last told.
	/// </summary>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset that reached a terminal state.</param>
	/// <param name="generation">The generation the delivery was made under.</param>
	/// <param name="commitOffset">
	/// When this returns <see langword="true"/>, the Kafka commit position — the next offset to read, so
	/// the lowest offset still owed, or one past the highest fetched offset when nothing is.
	/// </param>
	/// <returns>
	/// <see langword="true"/> when the caller should commit <paramref name="commitOffset"/>.
	/// <see langword="false"/> when there is nothing to commit: earlier work is still owed, the position is
	/// already committed, or the generation is stale.
	/// </returns>
	public bool TrySettle(TopicPartition partition, long offset, long generation, out long commitOffset)
	{
		ArgumentNullException.ThrowIfNull(partition);
		commitOffset = 0;

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return false;
		}

		lock (state.Gate)
		{
			// A settle carrying a previous tenure's generation belongs to a partition we no longer own (or
			// own afresh). Honouring it would move the current owner's position on the strength of work
			// performed before the handover.
			if (generation != state.Generation)
			{
				return false;
			}

			_ = state.Dispatched.Remove(offset);
			_ = state.AwaitingRedelivery.Remove(offset);

			if (state.Position() is not { } position)
			{
				return false;
			}

			// Emit a commit only for a position strictly ahead of what the broker already holds. Before the
			// first successful commit that reference is the tenure's floor — the first offset ever fetched —
			// NOT zero and NOT the current position: comparing against the position itself would emit a
			// pointless commit for a settle that moved nothing, and treating "never committed" as "commit
			// anything" would do the same. Falling back to the floor also re-emits a position whose commit
			// threw, because that failure left Committed unset while the position stayed ahead of the floor.
			var reference = state.Committed ?? state.Floor;
			if (reference is { } lastKnown && position <= lastKnown)
			{
				return false;
			}

			commitOffset = position;
			return true;
		}
	}

	/// <summary>
	/// Records that the broker accepted a commit at <paramref name="commitOffset"/> for
	/// <paramref name="partition"/>.
	/// </summary>
	/// <remarks>
	/// A commit that throws must not be recorded. Leaving <see cref="PartitionState.Committed"/> behind
	/// makes the next settle re-emit a position that subsumes the failed one, because a Kafka commit names
	/// an absolute position rather than a delta — so a transient commit failure heals on the next settle
	/// instead of stranding the position.
	/// </remarks>
	/// <param name="partition">The partition that was committed.</param>
	/// <param name="commitOffset">The position the broker accepted.</param>
	/// <param name="generation">
	/// The generation the commit was computed under. A commit from an earlier tenure is ignored: recording it
	/// would make the current tenure believe the broker already holds a position it never computed.
	/// </param>
	public void OnCommitted(TopicPartition partition, long commitOffset, long generation)
	{
		ArgumentNullException.ThrowIfNull(partition);

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return;
		}

		lock (state.Gate)
		{
			if (generation != state.Generation)
			{
				return;
			}

			if (state.Committed is not { } committed || commitOffset > committed)
			{
				state.Committed = commitOffset;
			}
		}
	}

	/// <summary>
	/// Marks <paramref name="offset"/> for redelivery and reports the offset to seek to so a healthy,
	/// still-running consumer replays it without waiting for a rebalance or a session timeout.
	/// </summary>
	/// <remarks>
	/// Idempotent: requeuing an offset that is already awaiting redelivery reports the same seek target
	/// again, so a caller whose seek threw can retry the whole call. Refusing it instead would leave the
	/// offset owed with no seek pending and the partition stalled on it.
	/// </remarks>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset to redeliver.</param>
	/// <param name="generation">The generation the delivery was made under.</param>
	/// <param name="seekOffset">
	/// When this returns <see cref="KafkaRequeueOutcome.Requeued"/>, the offset to seek the partition to —
	/// the lowest offset awaiting redelivery, so that several requeues do not seek forward past each other.
	/// </param>
	/// <returns>
	/// <see cref="KafkaRequeueOutcome.Requeued"/> when the caller should seek;
	/// <see cref="KafkaRequeueOutcome.StaleGeneration"/> when the partition has been revoked or reassigned since
	/// the delivery and is no longer this caller's to rewind; <see cref="KafkaRequeueOutcome.NotOutstanding"/>
	/// when the offset was delivered in this tenure and is already settled;
	/// <see cref="KafkaRequeueOutcome.NeverDelivered"/> when this tenure never delivered it.
	/// </returns>
	public KafkaRequeueOutcome TryRequeue(TopicPartition partition, long offset, long generation, out long seekOffset)
	{
		ArgumentNullException.ThrowIfNull(partition);
		seekOffset = 0;

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return KafkaRequeueOutcome.StaleGeneration;
		}

		lock (state.Gate)
		{
			if (generation != state.Generation)
			{
				return KafkaRequeueOutcome.StaleGeneration;
			}

			// Only an offset that is still owed can be requeued. Accepting a settled one would pin the seek
			// target below the position forever: its replay is refused as already handed out, so it would
			// never leave the redelivery set, and every later requeue would rewind to it.
			if (!state.Dispatched.Remove(offset) && !state.AwaitingRedelivery.Contains(offset))
			{
				// At or below the high water the offset was handed out in this tenure, so it has been settled;
				// above it (or before anything was fetched) it never was, and the two must not be confused.
				return state.HighWater is { } highWater && offset <= highWater
					? KafkaRequeueOutcome.NotOutstanding
					: KafkaRequeueOutcome.NeverDelivered;
			}

			// The offset stays owed: it leaves the dispatched set only because it is no longer in anyone's
			// hands, so the position still cannot pass it.
			_ = state.AwaitingRedelivery.Add(offset);
			seekOffset = state.AwaitingRedelivery.Min;
			return KafkaRequeueOutcome.Requeued;
		}
	}

	/// <summary>
	/// Discards the tracked state for revoked partitions and ends their tenure.
	/// </summary>
	/// <remarks>
	/// The generation advances here as well as on assignment, so a receipt issued before the revoke is stale
	/// from the moment the partition leaves this consumer, not only once it is assigned again. Without that, a
	/// late settle or requeue in the gap would find an empty tenure under its own generation and be told the
	/// offset was settled, or never delivered, when the truth is that it is no longer this consumer's.
	/// </remarks>
	/// <param name="partitions">The partitions being revoked.</param>
	public void OnPartitionsRevoked(IEnumerable<TopicPartition> partitions)
	{
		ArgumentNullException.ThrowIfNull(partitions);

		foreach (var partition in partitions)
		{
			if (_partitions.TryGetValue(partition, out var state))
			{
				lock (state.Gate)
				{
					state.Reset();
					state.Generation++;
				}
			}
		}
	}

	/// <summary>
	/// Begins a new assignment generation for the supplied partitions, invalidating every receipt issued
	/// under the previous tenure.
	/// </summary>
	/// <param name="partitions">The partitions being assigned.</param>
	public void OnPartitionsAssigned(IEnumerable<TopicPartition> partitions)
	{
		ArgumentNullException.ThrowIfNull(partitions);

		foreach (var partition in partitions)
		{
			var state = _partitions.GetOrAdd(partition, static _ => new PartitionState());
			lock (state.Gate)
			{
				state.Reset();
				state.Generation++;
			}
		}
	}

	/// <summary>
	/// Gets the current committable position for a partition, for assertions and diagnostics.
	/// </summary>
	/// <param name="partition">The partition to inspect.</param>
	/// <param name="position">
	/// The lowest offset still owed, or one past the highest fetched offset when nothing is; or
	/// <see langword="null"/> when nothing has been fetched from the partition yet — a state deliberately
	/// distinct from offset 0.
	/// </param>
	/// <returns><see langword="true"/> when the partition is tracked and has been fetched from.</returns>
	public bool TryGetPosition(TopicPartition partition, [NotNullWhen(true)] out long? position)
	{
		ArgumentNullException.ThrowIfNull(partition);
		position = null;

		if (!_partitions.TryGetValue(partition, out var state))
		{
			return false;
		}

		lock (state.Gate)
		{
			position = state.Position();
			return position is not null;
		}
	}

	// ponytail: one lock per partition. Contention is bounded by a single receiver's assigned partitions,
	// and every critical section is a handful of set operations. Partition into striped locks only if a
	// profile ever shows this seam hot.
	private sealed class PartitionState
	{
		public Lock Gate { get; } = new();

		/// <summary>Gets or sets the assignment epoch; receipts carry the value current when they were issued.</summary>
		public long Generation { get; set; }

		/// <summary>
		/// Gets or sets the first offset this tenure ever fetched, or null before the first fetch. It is
		/// the position the broker is already known to hold, so a commit at or below it says nothing new.
		/// </summary>
		public long? Floor { get; set; }

		/// <summary>Gets or sets the position the broker last accepted, or null before the first commit.</summary>
		public long? Committed { get; set; }

		/// <summary>Gets or sets the highest offset this tenure has fetched, or null before the first fetch.</summary>
		public long? HighWater { get; set; }

		/// <summary>Gets the offsets handed to the caller and not yet settled.</summary>
		public SortedSet<long> Dispatched { get; } = [];

		/// <summary>Gets the offsets requeued and awaiting replay; the minimum is the seek target.</summary>
		public SortedSet<long> AwaitingRedelivery { get; } = [];

		/// <summary>Gets or sets the owed offset the last requested seek back targeted, or null when none is pending.</summary>
		public long? ReseekTarget { get; set; }

		/// <summary>Gets or sets how many consecutive seeks back to <see cref="ReseekTarget"/> returned a later record first.</summary>
		public int ReseekMisses { get; set; }

		/// <summary>Gets or sets a value indicating whether the pending seek back was reported as performed.</summary>
		public bool ReseekIssued { get; set; }

		/// <summary>Computes the commit position: the lowest owed offset, else one past the high water.</summary>
		/// <returns>The position, or <see langword="null"/> before the first fetch.</returns>
		public long? Position()
		{
			if (HighWater is not { } highWater)
			{
				return null;
			}

			var position = highWater + 1;
			if (Dispatched.Count > 0)
			{
				position = Math.Min(position, Dispatched.Min);
			}

			if (AwaitingRedelivery.Count > 0)
			{
				position = Math.Min(position, AwaitingRedelivery.Min);
			}

			return position;
		}

		public void Reset()
		{
			Floor = null;
			Committed = null;
			HighWater = null;
			Dispatched.Clear();
			AwaitingRedelivery.Clear();
			ReseekTarget = null;
			ReseekMisses = 0;
			ReseekIssued = false;
		}
	}
}
