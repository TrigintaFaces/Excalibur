// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// The read-filter-write protocol for a projection whose store records how far it has been folded.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>One implementation, deliberately.</b> Four apply factories need this protocol and copying it into
/// each would give four chances to get a concurrency contract subtly wrong — and a defect in it
/// replicates silently, because every copy passes its own tests. The factories call this; they do not
/// re-implement it.
/// </para>
/// <para>
/// <b>That claim was true of the WRITE and false of the FILTER, and this note records the correction
/// rather than quietly absorbing it.</b> The already-folded filter used to live here as a list-shaped
/// helper that NOTHING CALLED, while its predicate was written out four times in
/// <c>ProjectionBuilder</c> — so the file promising one implementation contained four copies and an
/// uncalled fifth. The helper's shape was the reason: its callers decide per event inside loops that also
/// dispatch handlers, and none of them wants a filtered list. It is now
/// <see cref="AlreadyFolded(long?, long?)"/>, a predicate over one event, and all four sites call it.
/// </para>
/// <para>
/// <b>The invariant being maintained.</b> For a projection id <c>x</c> with stored position
/// <c>P(x)</c>: <c>state(x) = fold(apply, init, { e : pos(e) &lt;= P(x) })</c>. The position is not a
/// number the writer picks — it asserts which prefix of the stream is folded into the state, and every
/// write must make that true at the instant it commits.
/// </para>
/// <para>
/// <b>Why the filter is not optional.</b> The store refuses a write that does not advance the position,
/// which is what stops a redelivered batch being folded twice. Without the caller first discarding the
/// events at or below the stored position, a redelivery would recompute the same state and be refused
/// wholesale, and the reader would make no progress at all. The filter is what turns a refusal into a
/// skip.
/// </para>
/// </remarks>
internal static class PositionedProjectionWriter<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Resolves the positioned capability from a store, or <see langword="null"/> when it has none.
	/// </summary>
	/// <remarks>
	/// Resolved through <see cref="IServiceProvider.GetService"/> on the store itself, which is how
	/// every projection capability is discovered — and which routes through whatever decorators wrap it,
	/// so a decorator that declines to mediate the capability correctly hides it rather than exposing an
	/// unmediated inner store.
	/// </remarks>
	internal static IPositionedProjectionStore<TProjection>? Resolve(IProjectionStore<TProjection> store)
	{
		ArgumentNullException.ThrowIfNull(store);
		return store.GetService(typeof(IPositionedProjectionStore<TProjection>))
			as IPositionedProjectionStore<TProjection>;
	}

	/// <summary>
	/// Whether one event is already folded into the stored projection, and must therefore be skipped.
	/// </summary>
	/// <param name="storedPosition">The position read alongside the state, or <see langword="null"/>.</param>
	/// <param name="eventPosition">The event's global position, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when the event is at or below the stored position.</returns>
	/// <remarks>
	/// <para>
	/// <b>AN EVENT WITH NO POSITION IS KEPT, and that is the half most likely to be "fixed" into a
	/// defect.</b> Such an event cannot be placed relative to the stored one, and it is the save path --
	/// the event is being committed right now and no global position exists yet. Returning
	/// <see langword="true"/> for it would drop it, and on the save path EVERY event is unpositioned, so
	/// the whole batch would vanish. That has already shipped once: see the note on
	/// <see cref="WriteAsync"/> about a shape that "silently discarded every projection folded on the SAVE
	/// PATH ... with nothing logged".
	/// </para>
	/// <para>
	/// <b>A row with no stored position keeps everything too</b>, for the same reason: there is no
	/// coordinate to compare against, so nothing can be shown to be already folded.
	/// </para>
	/// <para>
	/// <b>Why this is a predicate over ONE event rather than a filter over a batch.</b> Its four callers
	/// decide per event inside loops that also dispatch handlers and route override ids; none of them
	/// materialises a filtered list. A list-shaped helper stood here uncalled for exactly that reason --
	/// its shape did not fit the work -- while the predicate was written out four times. This is the shape
	/// the callers actually need.
	/// </para>
	/// </remarks>
	internal static bool AlreadyFolded(long? storedPosition, long? eventPosition) =>
		eventPosition is { } position && storedPosition is { } stored && position <= stored;

	/// <summary>
	/// The highest position among a set of events, or <see langword="null"/> when none carries one.
	/// </summary>
	internal static long? HighestPosition(IEnumerable<ProjectionEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);

		long? highest = null;
		foreach (var e in events)
		{
			if (e.GlobalPosition is { } pos && (highest is null || pos > highest))
			{
				highest = pos;
			}
		}

		return highest;
	}

	/// <summary>
	/// Persists a folded projection — conditionally when a position is available, unconditionally when
	/// one is not.
	/// </summary>
	/// <param name="store">The store every caller resolved; the write always goes through it or its view.</param>
	/// <param name="positioned">
	/// The positioned capability, or <see langword="null"/> when this store does not record positions.
	/// </param>
	/// <param name="id">The projection identifier.</param>
	/// <param name="state">The folded state.</param>
	/// <param name="expectedPosition">The position read alongside the state this fold started from.</param>
	/// <param name="newPosition">
	/// The highest position folded in, or <see langword="null"/> when no event in the batch carried one.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The store's decision, or <see langword="null"/> when the write was unconditional.</returns>
	/// <remarks>
	/// <para>
	/// <b>A missing position withdraws the CONDITION, never the WRITE.</b> This distinction is the whole
	/// reason both stores are passed in together rather than the caller choosing. An earlier shape took
	/// only the positioned store and returned without writing when it had no position to be conditional
	/// on — which silently discarded every projection folded on the SAVE PATH, where events are being
	/// committed now and no global position exists yet. The state was computed, the handlers ran, and the
	/// object was dropped, on every store that had the capability, with nothing logged.
	/// </para>
	/// <para>
	/// So the branch lives HERE, in the one place every apply path already routes through, and the
	/// callers no longer get to decide. "Forgot to write" is not expressible at a call site any more.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Projection serialization is reflective.")]
	[RequiresDynamicCode("Projection serialization is reflective.")]
	internal static async Task<ProjectionAdvanceResult?> WriteAsync(
		IProjectionStore<TProjection> store,
		IPositionedProjectionStore<TProjection>? positioned,
		string id,
		TProjection state,
		long? expectedPosition,
		long? newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);

		if (positioned is null || newPosition is not { } position)
		{
			await store.UpsertAsync(id, state, cancellationToken).ConfigureAwait(false);
			return null;
		}

		var result = await positioned
			.UpsertAtPositionAsync(id, state, expectedPosition, position, cancellationToken)
			.ConfigureAwait(false);

		// TERMINAL, and separated from the retry path on purpose. The row carries no position this write
		// can advance from, so re-reading yields the same value and the same refusal -- the generic message
		// below would tell an operator to expect a redelivery that can never succeed. Only a rebuild can
		// make it writable again.
		//
		// ONE terminal outcome, and the message says only what the write established. It does NOT name which
		// of the two no-number states the row is in: the outcome does not carry that, because the store
		// classified its refusal against a read taken at a different instant and the row may have changed
		// since. An operator who needs it reads the projection's position, which reports it as a fact.
		if (result.Outcome is ProjectionAdvanceOutcome.Unplaceable)
		{
			throw new InvalidOperationException(
				$"Projection '{id}' of type '{typeof(TProjection).Name}' cannot be advanced to position "
				+ $"{position.ToString(CultureInfo.InvariantCulture)} because the stored row carries no "
				+ "position to advance from. A positioned write names which prefix of the stream is folded "
				+ "into the state it stores, so folding this batch onto a state whose prefix nobody "
				+ "established would make the row assert a prefix it does not hold -- and every event below "
				+ "that position would then be missing from the read model while the position claimed "
				+ "otherwise. THIS WILL NOT RESOLVE ON RETRY. "
				+ "Replay the stream and rebuild this projection through RebuildAtPositionAsync, which writes "
				+ "the state and its position together. THREE things leave a row in this state, and one of "
				+ "them is this framework rather than your code: a batch in which no event carried a global "
				+ "position is written unconditionally by the apply path itself, which happens on the save "
				+ "path where the events are being committed now. The other two are direct calls -- "
				+ "IProjectionStore.UpsertAsync, which stores a state related to no prefix at all, and "
				+ "IPositionedProjectionStore.UpsertUnnumberedAsync, which stores a complete fold it has no "
				+ "global position for. Read the projection's position to see which state the row is in -- it "
				+ "distinguishes them, and this outcome deliberately does not, because it is the result of a "
				+ "write rather than an observation of the row.");
		}

		if (!IsSettled(result, position))
		{
			// UNSETTLED means the store refused AND is BEHIND where this fold tried to reach, so these
			// events are in neither the projection nor the store's position. Returning quietly here is
			// the worst available outcome: the apply path would report success, the host would advance
			// its checkpoint past this batch, and the events would be gone from the projection with
			// nothing left that knows to re-read them.
			//
			// Throwing is how the caller is told, and it is not a panic -- the async host catches it,
			// declines to advance the checkpoint, and the batch is redelivered, which is precisely the
			// retry this needs. A refusal the store is AHEAD of is settled and returns normally.
			throw new InvalidOperationException(
				$"The projection store refused to advance projection '{id}' of type "
				+ $"'{typeof(TProjection).Name}' to position "
				+ $"{position.ToString(CultureInfo.InvariantCulture)}, and reports it is still at "
				+ $"{result.CurrentPosition?.ToString(CultureInfo.InvariantCulture) ?? "no position"}. "
				+ "Another writer changed the projection between the read and the write, so this fold "
				+ "was computed from a state that no longer exists. The events are not lost: the "
				+ "checkpoint is not advanced and the batch is redelivered, at which point the fold is "
				+ "recomputed from the current state.");
		}

		return result;
	}

	/// <summary>
	/// Whether a refused write means the work is already done, rather than that the caller is behind.
	/// </summary>
	/// <param name="result">The store's decision.</param>
	/// <param name="attemptedPosition">The position the caller tried to write.</param>
	/// <returns>
	/// <see langword="true"/> when the caller should move on; <see langword="false"/> when it should
	/// re-read and fold again.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This is what terminates the retry. A refusal alone does not say whether the store is ahead of the
	/// caller or behind it, and treating every refusal as "retry" loops forever the moment the batch has
	/// already been applied by someone else.
	/// </para>
	/// <para>
	/// <b>A vanished projection is settled, not retried.</b> The row is gone because it was deleted, and
	/// deletion is how erasure removes personal data. Re-folding the stream would put it back.
	/// </para>
	/// <para>
	/// <b>Every member is named, and the catch-all THROWS rather than choosing.</b> A catch-all that
	/// returned a value previously sent any unrecognised outcome down the "store is behind, retry" path,
	/// which for a terminal refusal is an unbounded redelivery loop -- invisible in the type system and
	/// arriving only in production. The arm is a throw instead, so a new outcome added to
	/// <see cref="ProjectionAdvanceOutcome"/> without a decision here fails loudly on first contact. Note
	/// what that does NOT give you: it is a runtime failure, not a compile error, so adding a member
	/// obliges you to come here.
	/// </para>
	/// </remarks>
	internal static bool IsSettled(ProjectionAdvanceResult result, long attemptedPosition) =>
		result.Outcome switch
		{
			ProjectionAdvanceOutcome.Applied => true,
			ProjectionAdvanceOutcome.Vanished => true,

			// NEITHER settled NOR retryable -- handled before this method is consulted. It is false here
			// because the write did not happen; the caller must not proceed as though it had. Named as its
			// own arm rather than folded into a pattern, so a future outcome cannot be absorbed into one that
			// happens to match it and silently inherit this answer.
			ProjectionAdvanceOutcome.Unplaceable => false,

			ProjectionAdvanceOutcome.Superseded =>
				result.CurrentPosition is { } current && current >= attemptedPosition,

			_ => throw new ArgumentOutOfRangeException(
				nameof(result),
				result.Outcome,
				"Unknown projection advance outcome. Every member must be handled explicitly: a "
				+ "fall-through here decides, silently, whether a refused write is retried forever or "
				+ "treated as done."),
		};
}
