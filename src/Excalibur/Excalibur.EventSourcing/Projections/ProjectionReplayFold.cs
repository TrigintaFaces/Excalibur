// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Folds a replay of the event stream into projection state, keyed the same way the live apply path
/// keys it, and writes each key back conditionally.
/// </summary>
/// <typeparam name="TProjection">The projection type being rebuilt.</typeparam>
/// <remarks>
/// <para>
/// <b>Why this is a separate seam rather than a loop inside the rebuild service.</b> The key
/// derivation used to be written out four times — the live asynchronous path, the live
/// synchronous-only path, recovery, and rebuild — and three of them were wrong for some projection
/// shape. A rebuild keyed by the projection TYPE NAME, which no read path queries, so it folded every
/// aggregate together into one meaningless document and left every row a reader loads untouched,
/// while reporting success. A key that disagrees with the read path is invisible: every call
/// succeeds and the reader sees stale data.
/// </para>
/// <para>
/// <b>A replay is NOT the live path with a different seed, and three of the differences are
/// load-bearing.</b> It seeds fresh, it must not apply the already-folded filter, and a superseded
/// write is a conflict rather than a completed one. Each is documented at the line that implements
/// it; copying the live path verbatim breaks all three.
/// </para>
/// <para>
/// <b>Consumer obligation: the projection's processor must not be running.</b> A replay reads the
/// whole stream and writes every key it touched; a live writer advancing the same rows in the
/// meantime makes the write conflict and leaves the replay incomplete. Stop the processor, replay,
/// then start it again. The read model is unavailable for the duration. This obligation is not
/// enforceable from here — nothing in the framework can observe whether the consumer's processor is
/// running — so it is stated rather than checked.
/// </para>
/// <para>
/// <b>Memory: everything folded is held until the flush.</b> The bound is the number of DISTINCT keys
/// the whole stream produces multiplied by the size of one <typeparamref name="TProjection"/>, and
/// the framework controls neither factor. Flushing mid-replay is possible and sound, but it trades
/// this bound for unbounded write amplification and reader-visible intermediate states, so it is not
/// what ships. A tenant large enough to exhaust memory here needs the incremental path instead.
/// </para>
/// <para>
/// <b>Completeness is ADDITIVE.</b> Every key the replayed stream produces is re-folded; a key that
/// no longer occurs in the stream keeps its existing row. Removing those is not expressible today —
/// the store contract offers no key enumeration. The consequence that matters is stated at the
/// erasure guarantee: a fully-erased aggregate's events are tombstones, tombstones are skipped before
/// key derivation, so no key is produced for that subject and a replay cannot clear its row.
/// </para>
/// </remarks>
internal sealed class ProjectionReplayFold<TProjection>
	where TProjection : class, new()
{
	private readonly Dictionary<string, TProjection> _states = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ReadObservation> _readPositions = new(StringComparer.Ordinal);
	private readonly Dictionary<string, long> _foldedTo = new(StringComparer.Ordinal);

	private readonly MultiStreamProjection<TProjection> _projection;
	private readonly IProjectionStore<TProjection>? _store;
	private readonly IPositionedProjectionStore<TProjection>? _positioned;
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionReplayFold{TProjection}"/> class.
	/// </summary>
	/// <param name="projection">The registered projection, which owns the key derivation and the handler table.</param>
	/// <param name="store">The projection store, or <see langword="null"/> when none is registered.</param>
	/// <param name="positioned">The store's positioned capability, or <see langword="null"/> when it has none.</param>
	/// <param name="serviceProvider">Resolves an asynchronous handler's dependencies.</param>
	internal ProjectionReplayFold(
		MultiStreamProjection<TProjection> projection,
		IProjectionStore<TProjection>? store,
		IPositionedProjectionStore<TProjection>? positioned,
		IServiceProvider serviceProvider)
	{
		_projection = projection;
		_store = store;
		_positioned = positioned;
		_serviceProvider = serviceProvider;
	}

	/// <summary>
	/// Gets the number of distinct projection identifiers folded so far.
	/// </summary>
	internal int KeyCount => _states.Count;

	/// <summary>
	/// Folds one replayed event into the projection identifier it belongs to.
	/// </summary>
	/// <param name="domainEvent">The deserialized, upcast domain event.</param>
	/// <param name="aggregateId">The aggregate the event was appended to.</param>
	/// <param name="aggregateType">The aggregate's type name.</param>
	/// <param name="version">The aggregate version this event carries.</param>
	/// <param name="timestamp">The event's timestamp.</param>
	/// <param name="globalPosition">The event's position in the global stream.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> when a handler folded the event; <see langword="false"/> when this
	/// projection registers no handler for the type, which is normal and produces no key.
	/// </returns>
	[RequiresUnreferencedCode("Folds through IProjectionStore, which serializes the projection type reflectively.")]
	[RequiresDynamicCode("Folds through IProjectionStore, which serializes the projection type reflectively.")]
	internal async Task<bool> FoldAsync(
		IDomainEvent domainEvent,
		string aggregateId,
		string aggregateType,
		long version,
		DateTimeOffset timestamp,
		long? globalPosition,
		CancellationToken cancellationToken)
	{
		// An event this projection does not handle produces NO KEY. Deriving one anyway would create an
		// empty row for every aggregate that ever appeared in the global stream.
		if (_projection.GetHandler(domainEvent.GetType()) is null)
		{
			return false;
		}

		var projectionEvent = new ProjectionEvent(domainEvent, aggregateId, globalPosition);

		// THE SAME derivation the live apply path uses. That is the whole repair.
		var projectionId = _projection.DeriveProjectionId(projectionEvent);
		var state = await TouchAsync(projectionId, cancellationToken).ConfigureAwait(false);

		// Built PER EVENT: the aggregate identity describes the event being applied, and a replay
		// spanning aggregates has no single answer for it. Constructing it here also resets
		// OverrideProjectionId, which a reused instance would carry between events.
		//
		// isReplay: true on BOTH contexts. A handler that skips notifications during replay depends on
		// it, and an asynchronous handler with side effects outside the projection — mail, a payment
		// call, a publish — can tell a replay from a first delivery only through the handler context.
		var handlerContext = new ProjectionHandlerContext(
			aggregateId, aggregateType, version, timestamp, isReplay: true);

		var context = new ProjectionContext(isReplay: true, globalPosition, aggregateId);

		// ApplyAsync, not Apply. The synchronous overload dispatches only the two SYNCHRONOUS handler
		// shapes and returns false for an asynchronous one, so dispatching through it folds NOTHING for
		// a projection registered with WhenHandledBy and reports success.
		_ = await _projection
			.ApplyAsync(state, domainEvent, context, handlerContext, _serviceProvider, cancellationToken)
			.ConfigureAwait(false);

		RecordFold(projectionId, globalPosition);

		// The OverrideProjectionId escape hatch, reproduced. Without it a replay does not reproduce
		// those writes at all, so every row a handler routed to a second key keeps whatever the live
		// path last left there.
		//
		// No already-folded gate here, unlike the live path, and for the same reason the primary fold
		// has none: the seed is fresh, so "already in what I hold" is false.
		if (handlerContext.OverrideProjectionId is { } overrideId
			&& !string.Equals(overrideId, projectionId, StringComparison.Ordinal))
		{
			var overrideState = await TouchAsync(overrideId, cancellationToken).ConfigureAwait(false);

			_ = await _projection
				.ApplyAsync(overrideState, domainEvent, context, handlerContext, _serviceProvider, cancellationToken)
				.ConfigureAwait(false);

			RecordFold(overrideId, globalPosition);
		}

		return true;
	}

	/// <summary>
	/// Writes every folded projection identifier back to the store.
	/// </summary>
	/// <param name="computeSearchText">The registered search-text computer, or <see langword="null"/>.</param>
	/// <param name="setSearchText">The registered search-text setter, or <see langword="null"/>.</param>
	/// <param name="projectionName">The projection's name, for the refusal message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The store refused a write because another writer advanced that identifier during the replay.
	/// </exception>
	[RequiresUnreferencedCode("Projection persistence goes through IProjectionStore, which serializes the projection type reflectively.")]
	[RequiresDynamicCode("Projection persistence goes through IProjectionStore, which serializes the projection type reflectively.")]
	internal async Task FlushAsync(
		Func<object, string>? computeSearchText,
		Action<object, string>? setSearchText,
		string projectionName,
		CancellationToken cancellationToken)
	{
		if (_store is null)
		{
			return;
		}

		// Search text is computed once per folded instance, exactly as the live apply path does it.
		// Without it a rebuilt projection loses its search text and stops matching the queries a reader
		// uses to find it — a divergence between the live and replayed shapes of the same row.
		if (computeSearchText is not null && setSearchText is not null)
		{
			foreach (var (_, folded) in _states)
			{
				setSearchText(folded, computeSearchText(folded));
			}
		}

		foreach (var (projectionId, folded) in _states)
		{
			var newPosition = _foldedTo.TryGetValue(projectionId, out var highest) ? (long?)highest : null;

			if (newPosition is not { } advanceTo)
			{
				// UNREACHABLE FROM THE ONLY CALLER TODAY, and that is stated first because the rest of this
				// comment reads as a description of live behaviour and is not one. FoldAsync has exactly one
				// caller, the rebuild service, which passes StoredEvent.GlobalPosition -- a NON-NULLABLE long.
				// Every TouchAsync is paired with a RecordFold carrying that value, including the
				// OverrideProjectionId branch, so every id present in _states has an entry in _foldedTo and
				// newPosition is never null on a rebuild. This branch therefore does not execute, and it is
				// NOT a live producer of any row shape.
				//
				// It is kept because the parameter is nullable and a future caller could pass none, so the
				// behaviour below is what SHOULD happen then: the state is a COMPLETE FOLD -- a rebuild
				// replays the whole stream from an empty seed by construction, whatever positions the events
				// carried -- so it is recorded as a fold with no number rather than through the blind surface,
				// which would record "not a fold over any prefix" and be false. The recovery path states the
				// same thing for the same reason, and THERE it is reachable.
				//
				// NOTE what that row can and cannot do: a positioned write refuses every row carrying no
				// number, so writing one here leaves the projection unadvanceable until a rebuild numbers it.
				// That is the honest end state for a fold whose coordinate is genuinely unknown -- the
				// alternative is to guess a number, which is what adoption used to do.
				//
				// Superseded wording, quoted so an inheritor recognises it: "saying so is what stops this
				// branch being a producer of unplaceable rows ... would leave behind exactly the terminal row
				// a rebuild is what you run to clear." That described a hazard this branch cannot create,
				// because it cannot run. The reachability was asserted and never measured.
				if (_positioned is not null)
				{
					await _positioned.UpsertUnnumberedAsync(projectionId, folded, cancellationToken)
						.ConfigureAwait(false);
				}
				else
				{
					// This store records no position at all, so there is no third state to state.
					await _store.UpsertAsync(projectionId, folded, cancellationToken).ConfigureAwait(false);
				}

				continue;
			}

			if (_positioned is null)
			{
				// The store records no position, so nothing can be conditional on one.
				await _store.UpsertAsync(projectionId, folded, cancellationToken).ConfigureAwait(false);

				continue;
			}

			var observed = _readPositions[projectionId];

			// THREE ROUTES, from the three observations the read distinguishes. The old single call
			// passed the collapsed number, which sent all three down the advancing write and relied on
			// its null arm ADOPTING an existing numberless row -- stamping a position onto state whose
			// prefix nobody had established.
			ProjectionAdvanceResult result;
			if (!observed.RowExisted)
			{
				// INSERT-IF-ABSENT. No row was there to fold onto, so claiming absence is a true claim.
				// A writer that created the row in the meantime wins, and the refusal is reported as
				// Superseded -- which the predicate below treats as the conflict it is.
				result = await _positioned
					.UpsertAtPositionAsync(projectionId, folded, null, advanceTo, cancellationToken)
					.ConfigureAwait(false);
			}
			else if (observed.Position.Kind == ProjectionPositionKind.Positioned)
			{
				// THE ORDINARY ADVANCE, from the number the row actually held. Its refusal is the only
				// thing that surfaces a live writer racing the rebuild, which is why positioned rows are
				// NOT sent down the unconditional route below: doing so would trade contention detection
				// for the whole rebuild to solve a problem only numberless rows have.
				result = await _positioned
					.UpsertAtPositionAsync(
						projectionId, folded, observed.Position.Value, advanceTo, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// UNNUMBERED or UNPLACEABLE, and the row EXISTS. There is no number to advance from, so
				// the advancing write cannot express this at all: it would have to be told to accept
				// "expected nothing" against a row that is present, which is the adopt licence this
				// routing exists to stop borrowing. A rebuild folded from an EMPTY seed over the whole
				// stream, so it needs no prior prefix to be conditional on -- and that is precisely the
				// caller RebuildAtPositionAsync exists for. It is UPDATE-only.
				//
				// The result is DISCARDED because both of its outcomes are settled, and that is a claim
				// about the enum rather than a shortcut. Applied is the write landing. Vanished is
				// settled for the same reason it is settled on the advancing path below: the row is gone
				// because it was deleted, deletion is how erasure removes personal data, and a
				// whole-stream replay is exactly the write that could put it back. There is no third
				// outcome -- the member is unconditional on position so it cannot be superseded, and it
				// IS the rebuild so it cannot ask for one. A third member added to
				// ProjectionRebuildOutcome would need a branch here.
				_ = await _positioned
					.RebuildAtPositionAsync(projectionId, folded, advanceTo, cancellationToken)
					.ConfigureAwait(false);

				continue;
			}

			// SUPERSEDED IS A CONFLICT FOR A REPLAY, never a settled write, and this is the one place a
			// replay must NOT reuse the live settle predicate.
			//
			// That predicate treats "the row is already at or beyond the position I attempted" as done.
			// The claim licensing it is that a row at that position already holds everything this fold
			// would have written — which is exactly the invariant a rebuild exists BECAUSE NOBODY
			// BELIEVES. Interleaved: the replay reads a key at 100 and folds; a live writer advances it
			// to 150; the replay writes expecting 100, is superseded at 150, and the live predicate
			// calls that settled. No event is folded twice and none zero times, and the replay's own
			// postcondition is false. Under contention the live writer wins every key, so the BUSIEST
			// keys — the ones most likely to be wrong — would be exactly the ones a replay silently
			// skips.
			//
			// Vanished IS settled. The row is gone because it was deleted, and deletion is how erasure
			// removes personal data; re-creating it here would undo an erasure.
			if (result.Outcome is not (ProjectionAdvanceOutcome.Applied or ProjectionAdvanceOutcome.Vanished))
			{
				// A NUMBERLESS REFUSAL HERE MEANS SOMETHING NARROWER THAN IT LOOKS, and the remedy changed
				// with it. A row that ALREADY carried no number when the replay read it never reaches this
				// write at all -- it is routed to RebuildAtPositionAsync above, which overwrites it, because
				// a whole-stream fold needs no prefix to be conditional on. That was the terminal case, and
				// it is now repaired rather than reported.
				//
				// So reaching here means the row was ABSENT or POSITIONED at read time and carries no number
				// by write time: a concurrent writer called IProjectionStore.UpsertAsync or
				// UpsertUnnumberedAsync during the replay. Both arrive here with the same outcome and get the
				// same message, because the remedy is the same one -- find the writer. It IS a race, and it is
				// reported separately
				// from the one below only because the remedy names a different culprit: stopping the
				// projection's processor is not sufficient if a component of the application writes this
				// projection on its own schedule. It also carries no CurrentPosition, so the message below
				// would render "unknown" where it promises a position.
				if (result.Outcome is ProjectionAdvanceOutcome.Unplaceable)
				{
					throw new InvalidOperationException(
						$"Rebuild of projection '{projectionName}' was refused at id '{projectionId}': the row "
						+ "lost its position while the rebuild was replaying, so there is no prefix for the "
						+ "rebuilt state to be written against. Something wrote this projection during the "
						+ "rebuild -- through IProjectionStore.UpsertAsync, which leaves no placeable position, "
						+ "or through UpsertUnnumberedAsync, which leaves a fold with no number. Stopping the "
						+ "projection's processor is NOT sufficient on its own -- find the component in your "
						+ "application that writes this projection outside the apply path, since it will do this "
						+ "again. Nothing was lost, but the rebuild is INCOMPLETE: ids written before this one "
						+ "hold rebuilt state and the rest do not. Re-run it once the other writer is stopped.");
				}

				// Deliberately NOT retried. A replay reads the whole stream; retrying against a row that
				// keeps moving would re-read it each time. Reported as a failure instead — nothing was
				// lost, and the caller can re-run once the processor is stopped, which a replay requires.
				throw new InvalidOperationException(
					$"Rebuild of projection '{projectionName}' was refused at id '{projectionId}': another "
					+ "writer advanced it to position "
					+ $"{result.CurrentPosition?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} while the "
					+ $"rebuild was replaying to {advanceTo.ToString(CultureInfo.InvariantCulture)}. The "
					+ "projection is not corrupt and no data was lost, but the rebuild is INCOMPLETE: ids "
					+ "written before this one hold rebuilt state and the rest do not. A rebuild requires "
					+ "the projection's processor to be stopped. Stop it and re-run the rebuild.");
			}
		}
	}

	/// <summary>
	/// Reads a projection identifier's stored POSITION on first touch and seeds its state fresh.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The position is read at first touch, not up front and not at write time.</b> Up front is not
	/// expressible: <c>IProjectionStore</c> offers no key enumeration, and with the override hatch the
	/// key set is not a function of the stream at all. At write time it is a read-modify-write race
	/// whose failure mode is a silent no-op.
	/// </para>
	/// <para>
	/// <b>The stored STATE is discarded, and that is not an oversight.</b> A replay seeds fresh by
	/// definition. The live path skips an event at or below the stored position, which is sound THERE
	/// only because it has just loaded the stored state, so "already in what I hold" is true. Here the
	/// premise is false by construction: copying that filter would skip every event at or below the
	/// stored position, fold only the tail onto empty state, and write it under a truthful-looking
	/// position. That is deterministic total destruction of the projection on the first run, with no
	/// concurrency required — the one part of the live path that must NOT be reused.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Projection loading goes through IProjectionStore, which serializes the projection type reflectively.")]
	[RequiresDynamicCode("Projection loading goes through IProjectionStore, which serializes the projection type reflectively.")]
	private async Task<TProjection> TouchAsync(string projectionId, CancellationToken cancellationToken)
	{
		if (_states.TryGetValue(projectionId, out var existing))
		{
			return existing;
		}

		// The PAIR, not the collapsed number. ExpectedPositionOrNull maps THREE distinguishable
		// observations onto null -- no row at all, a row holding Unnumbered, and a row holding
		// Unplaceable -- and FlushAsync routes those three to three different store members. A store
		// with no row returns (null, Unnumbered), so the Kind alone cannot separate absence from a
		// present unnumbered row either; only the projection value answers existence.
		var observed = new ReadObservation(RowExisted: false, ProjectionPosition.Unnumbered);
		if (_positioned is not null)
		{
			var (stored, readAtPos) = await _positioned
				.GetWithPositionAsync(projectionId, cancellationToken)
				.ConfigureAwait(false);

			observed = new ReadObservation(stored is not null, readAtPos);
		}

		var seeded = new TProjection();
		_states[projectionId] = seeded;
		_readPositions[projectionId] = observed;

		return seeded;
	}

	/// <summary>
	/// Records the highest global position folded into one projection identifier.
	/// </summary>
	/// <remarks>
	/// That key's OWN highest position, never the global horizon. Both are true claims about the
	/// replay, but the horizon makes the conditional write's refusal the normal outcome on every key
	/// the stream touched after this one.
	/// </remarks>
	private void RecordFold(string projectionId, long? globalPosition)
	{
		if (globalPosition is { } p
			&& (!_foldedTo.TryGetValue(projectionId, out var already) || p > already))
		{
			_foldedTo[projectionId] = p;
		}
	}

	/// <summary>
	/// What one first-touch read of a projection id actually observed.
	/// </summary>
	/// <param name="RowExisted">Whether a row was there at all.</param>
	/// <param name="Position">The position that row held, undisturbed by any collapsing.</param>
	/// <remarks>
	/// <para>
	/// <b>Both members are required, and neither is derivable from the other.</b> A store with no row
	/// answers <c>(null, Unnumbered)</c> -- absence is not given a kind of its own, because a caller
	/// writing to an absent row does not need one -- so <see cref="Position"/> alone cannot separate "no
	/// row" from "a row holding a complete fold with no number". Those two take DIFFERENT store members:
	/// the first is an insert-if-absent, the second an update-only rebuild.
	/// </para>
	/// <para>
	/// <b>Why the position is kept as the type rather than as a <c>long?</c>.</b>
	/// <c>ProjectionPosition.ExpectedPositionOrNull</c> maps all three kinds onto two values, and the
	/// three cases here need three routes. Collapsing at the read and re-deciding at the write is not
	/// possible: the information is gone by then.
	/// </para>
	/// </remarks>
	private readonly record struct ReadObservation(bool RowExisted, ProjectionPosition Position);
}
