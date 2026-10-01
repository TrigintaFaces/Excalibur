// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Binds the already-folded check for a fold targeting an <c>OverrideProjectionId</c>, which is a
/// SECOND id the same handler invocation can write to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms exist for.</b> The apply loop's already-folded filter is evaluated against
/// the PRIMARY projection id, but a fold can target two ids, and the override id is outside that
/// filter's domain entirely. The filter answers <i>"has the primary already folded this event?"</i>
/// when the load-bearing question for the override write is <i>"has the TARGET of this fold already
/// folded it?"</i> Nothing asked the second question, so an event already in the override target's
/// state could be folded into it again.
/// </para>
/// <para>
/// <b>Why the conditional write does not catch it, which is the part worth internalising.</b> The
/// positioned write enforces <c>state = fold(apply, init, {e : pos(e) &lt;= P})</c> with respect to the
/// POSITION. It has no opinion on whether an event was folded twice. On an overlapping batch the
/// highest position still advances and the expected position still matches what was read, so both
/// conjuncts hold and the store ADMITS the write. The position is truthful and the state is not, and
/// nothing downstream can tell.
/// </para>
/// <para>
/// <b>Why the primary filter does not mask it here, which is what makes these arms real.</b> On a
/// single aggregate the primary filter would skip the event before the handler ran, so the override
/// could not double-fold. The two ids come apart because the write loop writes EACH folded id
/// SEPARATELY: one id's write can be superseded by a competing writer while another's succeeds,
/// leaving the override target AHEAD of the primary. These arms seed exactly that state rather than
/// simulating the race, because the race is not what is under test — the filter's domain is.
/// </para>
/// <para>
/// <b>The projection ACCUMULATES on purpose.</b> An assigning projection passes these arms whether or
/// not the mechanism works, which is the shape that let the original defect ship.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class OverrideProjectionIdPositionedApplyShould
{
	private const string SharedId = "shared-ledger";

	/// <summary>
	/// SAFETY: an event at or below the OVERRIDE target's stored position is not folded into it again.
	/// </summary>
	/// <remarks>
	/// <b>Non-vacuity.</b> Remove the <c>overrideAlreadyFolded</c> guard in <c>ProjectionBuilder</c> and
	/// this arm goes RED: the handler runs a second time against the shared state and <c>Total</c>
	/// becomes 2. Note the guard must sit BEFORE the handler invocation — moving it to guard only the
	/// <c>folded</c> append leaves this arm RED too, because the handler has already mutated the cached
	/// state by then, which is precisely why the cheaper placement is wrong.
	/// </remarks>
	[Fact]
	public async Task Not_fold_an_event_the_override_target_already_holds()
	{
		var store = new SeededPositionedStore();

		// The override target is AHEAD of the primary. Reachable because each folded id is written
		// separately, so one write can be superseded while the other lands.
		store.Seed(SharedId, total: 1, position: 100);

		var apply = BuildApply();

		// The batch MUST also carry an event ABOVE the stored position. With only the stale event the
		// write's new position would be 95 against a stored 100, the store would refuse it as
		// non-advancing, and this arm would pass on the STORE's monotonicity rather than on the gate --
		// measuring the wrong mechanism and reporting green either way. Verified: an earlier version of
		// this arm did exactly that and survived deleting the gate.
		await apply(Batch(("agg-x", 95), ("agg-x", 105)), Context(), Provider(store), CancellationToken.None);

		store.Get(SharedId)!.Total.ShouldBe(
			2,
			"105 is new and must be folded; 95 is at or below the override target's stored position of "
			+ "100 and is already in that state. Folding 95 again gives 3 -- a double count carried into "
			+ "the store under a position (105) that is perfectly truthful, which is why the conditional "
			+ "write admits it and cannot detect the error.");
	}

	/// <summary>
	/// LIVENESS: an event beyond the override target's stored position IS folded into it.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a guard that skips EVERYTHING — the
	/// cheapest way never to double-fold and the most expensive way to be wrong. This is the arm that
	/// fails if the guard's comparison is inverted or its bound is off by one.
	/// </remarks>
	[Fact]
	public async Task Still_fold_an_event_beyond_the_override_target_position()
	{
		var store = new SeededPositionedStore();
		store.Seed(SharedId, total: 1, position: 100);

		var apply = BuildApply();
		await apply(Batch(("agg-x", 105)), Context(), Provider(store), CancellationToken.None);

		store.Get(SharedId)!.Total.ShouldBe(
			2,
			"event 105 is beyond the stored position of 100, so it has not been folded in yet and must "
			+ "be applied. A guard that skips it stalls the override target permanently.");
	}

	/// <summary>
	/// BOUNDARY: an event exactly AT the stored position is already folded, so it is skipped.
	/// </summary>
	/// <remarks>
	/// The stored position names the last event folded IN, not the next one expected. An off-by-one
	/// here re-applies the boundary event on every redelivery.
	/// </remarks>
	[Fact]
	public async Task Treat_an_event_exactly_at_the_stored_position_as_already_folded()
	{
		var store = new SeededPositionedStore();
		store.Seed(SharedId, total: 1, position: 100);

		var apply = BuildApply();

		// Paired with an advancing event for the same reason as the arm above: without one the store
		// refuses the write and the arm stops measuring the gate.
		await apply(Batch(("agg-x", 100), ("agg-x", 105)), Context(), Provider(store), CancellationToken.None);

		store.Get(SharedId)!.Total.ShouldBe(
			2,
			"the stored position names the last event folded IN, not the next one expected, so the event "
			+ "at exactly 100 is already present. Only 105 may be applied. An off-by-one here re-applies "
			+ "the boundary event on every redelivery.");
	}

	/// <summary>
	/// LIVENESS, and the half the primary filter used to swallow: an event the PRIMARY has already
	/// folded is still delivered to an override target whose own position is behind.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The defect.</b> The primary filter used to <c>continue</c> before the handler ran. The
	/// override id is outside that filter's domain, and it is only knowable by RUNNING the handler — so
	/// skipping the delivery meant the override target never learned the event existed. A later event
	/// in the same batch then advanced the override past it and nothing re-reads it: permanent
	/// omission, no upper bound, no signal. Worse than the double-fold its sibling arm binds — that is
	/// a wrong number; this is data that never arrives.
	/// </para>
	/// <para>
	/// <b>The setup is the reachable one, not a contrivance.</b> Each folded id is written separately,
	/// so the two ids come apart in the ordinary course: the primary is at 100 while the shared ledger
	/// it routes to is still at 50.
	/// </para>
	/// <para>
	/// <b>Non-vacuity.</b> Restore the <c>continue</c> and this arm goes RED at 2 — only event 105
	/// arrives, and 95 is lost to the shared ledger forever.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Deliver_an_event_the_primary_already_folded_to_a_lagging_override_target()
	{
		var store = new SeededPositionedStore();
		store.Seed("agg-x", total: 7, position: 100);
		store.Seed(SharedId, total: 1, position: 50);

		var apply = BuildApply();

		// 95 is at or below the PRIMARY's stored position (100) and above the OVERRIDE target's (50).
		// 105 is above both, and is what lets the shared write advance.
		await apply(Batch(("agg-x", 95), ("agg-x", 105)), Context(), Provider(store), CancellationToken.None);

		store.Get(SharedId)!.Total.ShouldBe(
			3,
			"the shared ledger holds 1 and is owed BOTH 95 and 105 -- 95 because its own stored position "
			+ "is 50, regardless of the primary already holding it. A filter evaluated against the "
			+ "primary answers the wrong question for this write, and skipping the delivery loses 95 "
			+ "permanently: nothing re-reads it once 105 advances the row past it");
	}

	/// <summary>
	/// SAFETY: discovering the override must not fold the event into the PRIMARY a second time.
	/// </summary>
	/// <remarks>
	/// The handler has to run for the override id to be discovered, and running it mutates whatever
	/// state it is handed. It is therefore handed a THROWAWAY instance when the primary has already
	/// folded the event, and the throwaway is dropped. Hand it the real primary state instead and this
	/// arm goes RED at 9 — and the corruption is persisted, under a position (105) that is perfectly
	/// truthful, because the later event in the same batch legitimately advances the row.
	/// </remarks>
	[Fact]
	public async Task Not_double_fold_the_primary_while_discovering_the_override()
	{
		var store = new SeededPositionedStore();
		store.Seed("agg-x", total: 7, position: 100);
		store.Seed(SharedId, total: 1, position: 50);

		var apply = BuildApply();
		await apply(Batch(("agg-x", 95), ("agg-x", 105)), Context(), Provider(store), CancellationToken.None);

		store.Get("agg-x")!.Total.ShouldBe(
			8,
			"the primary holds 7 and is owed only 105: 95 is at or below its stored position of 100 and "
			+ "is already in that state. The delivery of 95 exists solely to discover the override id "
			+ "and must land on a throwaway instance");
	}

	private static ProjectionRegistration.InlineApplyDelegate BuildApply()
	{
		var services = new ServiceCollection();
		var builder = new ProjectionBuilder<Tally>(services);
		_ = builder.WhenHandledBy<Counted, RouteToShared>();

		var registry = new InMemoryProjectionRegistry();
		builder.Build(registry);

		return registry.GetRegistration(typeof(Tally))!.InlineApply!;
	}

	private static IServiceProvider Provider(SeededPositionedStore store)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IProjectionStore<Tally>>(store);
		_ = services.AddTransient<RouteToShared>();
		return services.BuildServiceProvider();
	}

	private static EventNotificationContext Context() =>
		new("agg-x", "Agg", 1, DateTimeOffset.UnixEpoch);

	private static IReadOnlyList<ProjectionEvent> Batch(params (string Aggregate, long Position)[] items) =>
		[.. items.Select(i => new ProjectionEvent(new Counted(), i.Aggregate, i.Position))];

	private sealed class Tally
	{
		public int Total { get; set; }
	}

	/// <summary>
	/// Routes every event to one shared projection id, which is what <c>OverrideProjectionId</c> is for.
	/// </summary>
	/// <remarks>
	/// An ASYNC handler is required: the override lives on <see cref="ProjectionHandlerContext"/>, and
	/// the synchronous registration arms are handed a different context type that has no such member.
	/// A sync-registered projection therefore cannot reach this defect at all — which is why the
	/// existing generated-input suite, running the sync-only fast path, could never have found it.
	/// </remarks>
	private sealed class RouteToShared : IProjectionEventHandler<Tally, Counted>
	{
		public Task HandleAsync(
			Tally projection, Counted @event, ProjectionHandlerContext context, CancellationToken cancellationToken)
		{
			context.OverrideProjectionId = SharedId;
			projection.Total++;
			return Task.CompletedTask;
		}
	}

	[MessageName("Test.OverrideProjectionIdPositionedApply.Counted")]
	private sealed record Counted : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public string EventType { get; init; } = nameof(Counted);

		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Implements the positioned contract DIRECTLY, inheriting no first-party base that could supply
	/// the member under test, and exposes a seam to seed a starting position.
	/// </summary>
	private sealed class SeededPositionedStore : IPositionedProjectionStore<Tally>
	{
		private readonly Dictionary<string, (Tally State, long Position)> _rows = new(StringComparer.Ordinal);

		internal void Seed(string id, int total, long position) =>
			_rows[id] = (new Tally { Total = total }, position);

		internal Tally? Get(string id) => _rows.TryGetValue(id, out var r) ? r.State : null;

		// The unnumbered write: a complete fold whose prefix has no global position number.
		// Distinct from the blind UpsertAsync, which records that the state is not a fold at all.
		public Task UpsertUnnumberedAsync(
			string id, Tally projection, CancellationToken cancellationToken)
		{
			UnnumberedWrites++;
			return UpsertAsync(id, projection, cancellationToken);
		}

		/// <summary>Gets how many unnumbered writes this double received.</summary>
		public int UnnumberedWrites { get; private set; }

		public Task<(Tally? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id, CancellationToken cancellationToken) =>
			Task.FromResult(_rows.TryGetValue(id, out var r)
				? (Clone(r.State), ProjectionPosition.FromStored(r.Position))
				: ((Tally?)null, ProjectionPosition.Unnumbered));

		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id, Tally projection, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken)
		{
			var present = _rows.TryGetValue(id, out var existing);
			var storedPosition = present ? existing.Position : (long?)null;

			// Both conjuncts of the contract, enforced exactly as a real store must.
			if (storedPosition != expectedPosition || (present && newPosition <= existing.Position))
			{
				return Task.FromResult(new ProjectionAdvanceResult(
					ProjectionAdvanceOutcome.Superseded, storedPosition));
			}

			_rows[id] = (Clone(projection), newPosition);
			return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
		}

		// Rewrites the state at the position the row already holds. Never creates: an absent row was
		// deleted, and deletion is how erasure removes personal data.
		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, Tally projection, long atPosition, CancellationToken cancellationToken)
		{
			if (!_rows.TryGetValue(id, out var row))
			{
				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null));
			}

			// This double's row carries a non-nullable position, so RequiresRebuild is unreachable
			// here by construction. The providers' own conformance arms cover it.
			if (row.Position != atPosition)
			{
				return Task.FromResult(new ProjectionRefoldResult(
					ProjectionRefoldOutcome.Superseded, row.Position));
			}

			_rows[id] = (Clone(projection), atPosition);

			return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));
		}

		// Overwrites BOTH state and position of an EXISTING row, for a caller that folded the whole
		// stream from an empty seed. Unconditional on POSITION but conditional on EXISTENCE: an absent
		// row was deleted, deletion is how erasure removes personal data, and a replay must not restore it.
		public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
			string id, Tally projection, long newPosition, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

			if (!_rows.ContainsKey(id))
			{
				return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished));
			}

			_rows[id] = (Clone(projection), newPosition);

			return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
		}

		public Task<Tally?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult(Get(id));

		public Task UpsertAsync(string id, Tally projection, CancellationToken cancellationToken)
		{
			_rows[id] = (Clone(projection), UnpositionedMarker);
			return Task.CompletedTask;
		}

		internal const long UnpositionedMarker = -1;

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_ = _rows.Remove(id);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<Tally>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Tally>>([.. _rows.Values.Select(static v => v.State)]);

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult((long)_rows.Count);

		public object? GetService(Type serviceType) =>
			serviceType == typeof(IPositionedProjectionStore<Tally>) ? this : null;

		private static Tally Clone(Tally t) => new() { Total = t.Total };
	}
}
