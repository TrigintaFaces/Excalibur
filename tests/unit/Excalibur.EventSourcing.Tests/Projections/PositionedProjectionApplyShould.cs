// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Binds what a projection store that records positions buys: a redelivered batch is not folded twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this closes.</b> Applying an event to a projection is load-modify-write, and the stored
/// row recorded nothing about which events were already in it, so nothing could detect a second
/// application. An assigning handler survives a replay; an accumulating one double-counts on every
/// restart, silently and without bound — and the framework cannot tell the two apart.
/// </para>
/// <para>
/// The projection here ACCUMULATES on purpose. An assigning projection would pass these arms whether or
/// not the mechanism works, which is exactly the shape that let the defect ship.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class PositionedProjectionApplyShould
{
	/// <summary>
	/// SAFETY: re-delivering a batch that is already folded in changes nothing.
	/// </summary>
	/// <remarks>
	/// <b>This property is held by the STORE, not by the caller.</b> Measured: severing the caller's
	/// already-folded filter leaves this arm GREEN, because the recomputed write does not advance the
	/// position and the store refuses it. The filter's job is the liveness arm below — it converts that
	/// refusal into forward progress instead of a stall. Two mechanisms, two properties; do not read
	/// either arm as covering both.
	/// </remarks>
	[Fact]
	public async Task Not_fold_a_redelivered_batch_a_second_time()
	{
		var store = new RecordingPositionedStore();
		var apply = BuildApply(store);

		var batch = Batch(("agg-1", 1), ("agg-1", 2), ("agg-1", 3));

		await apply(batch, Context(), Provider(store), CancellationToken.None);
		var afterFirst = store.Get("agg-1")!.Total;

		// The same events arrive again -- a crash between applying and checkpointing, or a reader that
		// restarted from an older mark.
		await apply(batch, Context(), Provider(store), CancellationToken.None);

		afterFirst.ShouldBe(3, "the first delivery folds all three events");
		store.Get("agg-1")!.Total.ShouldBe(
			3,
			"a redelivered batch must not be folded twice -- an accumulating projection would otherwise "
			+ "double-count silently on every restart.");
	}

	/// <summary>
	/// LIVENESS: new events beyond the stored position are still folded.
	/// </summary>
	/// <remarks>
	/// <b>This is the arm the caller's filter actually holds up</b>, and the one that reddens when it is
	/// removed: without the filter an overlapping batch recomputes a state the store then refuses as
	/// non-advancing, so the reader never progresses past the overlap. It also stops the safety arm
	/// above being satisfied by an apply path that simply stopped writing — the cheapest way never to
	/// double-apply, and the most expensive way to be wrong.
	/// </remarks>
	[Fact]
	public async Task Still_fold_events_beyond_the_stored_position()
	{
		var store = new RecordingPositionedStore();
		var apply = BuildApply(store);

		await apply(Batch(("agg-1", 1), ("agg-1", 2)), Context(), Provider(store), CancellationToken.None);

		// A batch that overlaps the applied prefix and extends past it.
		await apply(
			Batch(("agg-1", 1), ("agg-1", 2), ("agg-1", 3), ("agg-1", 4)),
			Context(), Provider(store), CancellationToken.None);

		store.Get("agg-1")!.Total.ShouldBe(
			4,
			"the two new events must be folded while the two already applied are skipped.");
		store.LastWrittenPosition.ShouldBe(4);
	}

	/// <summary>
	/// The write carries the position the state was READ at, not a value invented at write time.
	/// </summary>
	[Fact]
	public async Task Write_the_position_its_state_was_read_at()
	{
		var store = new RecordingPositionedStore();
		var apply = BuildApply(store);

		await apply(Batch(("agg-1", 5)), Context(), Provider(store), CancellationToken.None);
		store.LastExpectedPosition.ShouldBeNull("nothing was stored, so the caller claims absence");

		await apply(Batch(("agg-1", 9)), Context(), Provider(store), CancellationToken.None);
		store.LastExpectedPosition.ShouldBe(5, "the second write expects what the first one stored");
	}

	/// <summary>
	/// LIVENESS, and the one that matters most: a fold carrying NO position is still persisted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The defect this arm exists for.</b> On the save path every event is dispatched before the
	/// store has assigned it a global position, so the batch carries none. An earlier shape read that as
	/// "this apply cannot participate in a position-conditional write" — true of the CONDITION — and
	/// concluded the write should be skipped, which does not follow. The result was that on every store
	/// that had the positioned capability, inline projections were computed, handlers ran, search text
	/// was built, and the object was dropped. No exception, no log line, no test.
	/// </para>
	/// <para>
	/// It survived because the safety arms above are all satisfied by a store that writes nothing, and
	/// this fixture's unconditional path used to THROW rather than record — encoding the same wrong
	/// belief in the test that the production code held.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Still_persist_a_fold_that_carries_no_position()
	{
		var store = new RecordingPositionedStore();
		var apply = BuildApply(store);

		// The save path: real events, no global position yet.
		var batch = (IReadOnlyList<ProjectionEvent>)
		[
			new ProjectionEvent(new Counted(), "agg-1", GlobalPosition: null),
			new ProjectionEvent(new Counted(), "agg-1", GlobalPosition: null),
		];

		await apply(batch, Context(), Provider(store), CancellationToken.None);

		store.Get("agg-1").ShouldNotBeNull(
			"a fold with no position must still be written -- a missing position withdraws the "
			+ "CONDITION, never the WRITE. Dropping it loses the projection silently.");
		store.Get("agg-1")!.Total.ShouldBe(2, "both events must be folded into the persisted state");
		store.UnconditionalWrites.ShouldBe(1, "exactly one unconditional write, not one per event");
	}

	private static ProjectionRegistration.InlineApplyDelegate BuildApply(RecordingPositionedStore store)
	{
		var builder = new ProjectionBuilder<Counter>(new ServiceCollection());
		builder.When<Counted>(static (p, _) => p.Total++);

		var registry = new InMemoryProjectionRegistry();
		builder.Build(registry);

		return registry.GetRegistration(typeof(Counter))!.InlineApply!;
	}

	private static IServiceProvider Provider(RecordingPositionedStore store)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IProjectionStore<Counter>>(store);
		return services.BuildServiceProvider();
	}

	private static EventNotificationContext Context() =>
		new("agg-1", "Agg", 1, DateTimeOffset.UnixEpoch);

	private static IReadOnlyList<ProjectionEvent> Batch(params (string Aggregate, long Position)[] items) =>
		[.. items.Select(i => new ProjectionEvent(new Counted(), i.Aggregate, i.Position))];

	private sealed class Counter
	{
		public int Total { get; set; }
	}

	// Declares a message name because a startup validator requires every domain event to have one:
	// an event is stored under the name it declares.
	[MessageName("Test.PositionedProjectionApply.Counted")]
	private sealed record Counted : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public string EventType { get; init; } = nameof(Counted);

		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Implements the positioned contract DIRECTLY, inheriting nothing that could supply the member
	/// under test.
	/// </summary>
	/// <remarks>
	/// A fixture deriving from a first-party base would re-test the base rather than the contract, and
	/// would pass even for an implementation that gets the condition wrong.
	/// </remarks>
	private sealed class RecordingPositionedStore : IPositionedProjectionStore<Counter>
	{
		private readonly Dictionary<string, (Counter State, long Position)> _rows = new(StringComparer.Ordinal);

		internal long? LastWrittenPosition { get; private set; }

		internal long? LastExpectedPosition { get; private set; }

		internal Counter? Get(string id) => _rows.TryGetValue(id, out var r) ? r.State : null;

		// The unnumbered write: a complete fold whose prefix has no global position number.
		// Distinct from the blind UpsertAsync, which records that the state is not a fold at all.
		public Task UpsertUnnumberedAsync(
			string id, Counter projection, CancellationToken cancellationToken)
		{
			UnnumberedWrites++;
			return UpsertAsync(id, projection, cancellationToken);
		}

		/// <summary>Gets how many unnumbered writes this double received.</summary>
		public int UnnumberedWrites { get; private set; }

		public Task<(Counter? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id, CancellationToken cancellationToken) =>
			Task.FromResult(_rows.TryGetValue(id, out var r)
				? (Clone(r.State), ProjectionPosition.FromStored(r.Position))
				: ((Counter?)null, ProjectionPosition.Unnumbered));

		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id, Counter projection, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken)
		{
			LastExpectedPosition = expectedPosition;

			var present = _rows.TryGetValue(id, out var existing);
			var storedPosition = present ? existing.Position : (long?)null;

			// The contract's two conjuncts, enforced exactly as a real store must.
			if (storedPosition != expectedPosition
				|| (present && newPosition <= existing.Position))
			{
				return Task.FromResult(new ProjectionAdvanceResult(
					ProjectionAdvanceOutcome.Superseded, storedPosition));
			}

			_rows[id] = (Clone(projection), newPosition);
			LastWrittenPosition = newPosition;
			return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
		}

		// Rewrites the state at the position the row already holds. Never creates: an absent row was
		// deleted, and deletion is how erasure removes personal data.
		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, Counter projection, long atPosition, CancellationToken cancellationToken)
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
			string id, Counter projection, long newPosition, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

			if (!_rows.ContainsKey(id))
			{
				return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished));
			}

			_rows[id] = (Clone(projection), newPosition);
			LastWrittenPosition = newPosition;

			return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
		}

		public Task<Counter?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult(Get(id));

		/// <summary>
		/// Records an unconditional write, which is the CORRECT path for an unpositioned fold.
		/// </summary>
		/// <remarks>
		/// This member used to throw, on the belief that reaching the unconditional write while the
		/// positioned capability exists was always a defect. That belief is what produced the worst bug
		/// in this seam: the save path carries no global position -- the events are being committed right
		/// now and the store assigns positions inside that transaction -- so the write there MUST be
		/// unconditional, and code written to avoid it simply dropped the projection. A missing position
		/// withdraws the CONDITION, never the WRITE.
		/// </remarks>
		public Task UpsertAsync(string id, Counter projection, CancellationToken cancellationToken)
		{
			_rows[id] = (Clone(projection), UnpositionedMarker);
			UnconditionalWrites++;
			return Task.CompletedTask;
		}

		/// <summary>The sentinel a row carries when it was written without a position.</summary>
		internal const long UnpositionedMarker = -1;

		/// <summary>How many times the unconditional path was taken.</summary>
		internal int UnconditionalWrites { get; private set; }

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_ = _rows.Remove(id);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<Counter>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Counter>>([.. _rows.Values.Select(static v => v.State)]);

		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult((long)_rows.Count);

		private static Counter Clone(Counter c) => new() { Total = c.Total };
	}
}
