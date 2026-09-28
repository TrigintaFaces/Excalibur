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
	private readonly Dictionary<string, long?> _readPositions = new(StringComparer.Ordinal);
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
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Projection persistence goes through IProjectionStore, which consumers configure with preserved types.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Projection persistence goes through IProjectionStore, which consumers configure.")]
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

			if (_positioned is null || newPosition is not { } advanceTo)
			{
				// Either the store records no position, or nothing folded into this key carried one.
				// There is nothing to be conditional on.
				await _store.UpsertAsync(projectionId, folded, cancellationToken).ConfigureAwait(false);

				continue;
			}

			var result = await _positioned
				.UpsertAtPositionAsync(projectionId, folded, _readPositions[projectionId], advanceTo, cancellationToken)
				.ConfigureAwait(false);

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
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Projection loading goes through IProjectionStore, which consumers configure with preserved types.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Projection loading goes through IProjectionStore, which consumers configure.")]
	private async Task<TProjection> TouchAsync(string projectionId, CancellationToken cancellationToken)
	{
		if (_states.TryGetValue(projectionId, out var existing))
		{
			return existing;
		}

		long? readAt = null;
		if (_positioned is not null)
		{
			(_, var readAtPos) = await _positioned.GetWithPositionAsync(projectionId, cancellationToken)
				.ConfigureAwait(false);
			readAt = readAtPos.ExpectedPositionOrNull;
		}

		var seeded = new TProjection();
		_states[projectionId] = seeded;
		_readPositions[projectionId] = readAt;

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
}
