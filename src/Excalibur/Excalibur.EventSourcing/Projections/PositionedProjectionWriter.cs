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
	/// Resolves the mandatory positioned capability for a persisted asynchronous page.
	/// </summary>
	/// <remarks>
	/// Validate the entire page before invoking any handler or loading a projection. A later missing
	/// position must not leave an earlier event's external effect performed under an invalid contract.
	/// Rechecked per invocation because a scoped store factory can differ from the startup probe.
	/// </remarks>
	internal static IPositionedProjectionStore<TProjection> ResolveRequired(
		IProjectionStore<TProjection> store, IReadOnlyList<ProjectionEvent> events)
	{
		ArgumentNullException.ThrowIfNull(events);
		var positioned = Resolve(store)
			?? throw new InvalidOperationException(
				$"Asynchronous projection '{typeof(TProjection).FullName}' requires a store that exposes "
				+ "positioned compare-and-set writes through its decorator chain. Unconditional writes "
				+ "cannot safely participate in cooperative projection processing.");
		if (events.Any(static e => e.GlobalPosition is not > 0))
		{
			throw new InvalidOperationException(
				$"Asynchronous projection '{typeof(TProjection).FullName}' received an event without a "
				+ "positive committed global position. Persisted asynchronous pages must come from the global stream.");
		}

		return positioned;
	}

	/// <summary>
	/// Whether one event is already folded into the stored projection, and must therefore be skipped.
	/// </summary>
	/// <param name="storedPosition">The position read alongside the state, or <see langword="null"/>.</param>
	/// <param name="eventPosition">The event's global position, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when the event is at or below the stored position.</returns>
	/// <remarks>
	/// <para>
	/// A missing event or stored position returns false: this predicate cannot establish that the
	/// event is already folded. False does not establish that a write is safe; an existing row without
	/// a usable position can still require rebuilding before it accepts a positioned advance.
	/// </para>
	/// <para>
	/// Persisted asynchronous factories call ResolveRequired before invoking handlers and reject every
	/// event without a positive committed position. Their events come from the global stream, not from
	/// SaveAsync. The nullable case is part of this internal helper's general contract, not an
	/// unpositioned fallback available to persisted asynchronous processing.
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
	/// This internal helper retains a generic unconditional branch when either a capability or a target
	/// position is absent. That branch supplies no positioned concurrency guarantee. Persisted asynchronous
	/// factories first call ResolveRequired, which requires the capability and validates every event's
	/// committed position before any fold; those factories cannot select the unconditional branch.
	/// SaveAsync does not invoke projection factories or persist read models.
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
				+ "the state and its position together. Direct unconditional or unnumbered writes can leave "
				+ "a row without a usable coordinate. Persisted asynchronous processing rejects missing event "
				+ "positions before handlers run; SaveAsync does not write read models. "
				+ "Read the projection's position to see which state the row is in -- it "
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
