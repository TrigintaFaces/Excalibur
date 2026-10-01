// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A rebuild writes the keys a reader loads, seeds fresh, and refuses rather than reporting a
/// rebuild it did not finish.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms bind.</b> The rebuild service folded EVERY event of the global stream
/// into one projection instance and wrote it under <c>typeof(TProjection).Name</c>. Every apply path
/// keys by the registered key selector falling back to the aggregate id, so the two key spaces were
/// disjoint: a rebuild wrote one document under a key no read path queries, every aggregate merged on
/// top of one another, and left every row a reader loads exactly as it was — while reporting
/// Completed. Nothing failed, nothing logged, and the operator's remedy for a wrong read model
/// quietly did nothing.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The mutation that reddens these is the old behaviour itself: key the write by
/// the projection type name instead of the derived id and
/// <see cref="Write_the_key_each_aggregate_is_read_back_under"/> goes RED while the liveness arms
/// stay GREEN. The mutation that reddens <see cref="Fold_every_event_onto_a_fresh_seed"/> is copying
/// the live path's already-folded filter, which is the specific reuse that would destroy the
/// projection; the mutation that reddens <see cref="Refuse_a_rebuild_another_writer_superseded"/> is
/// reusing the live settle predicate, under which a superseded write reports success.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionRebuildKeyingShould
{
	/// <summary>SAFETY: a rebuild writes the key a reader loads, per aggregate.</summary>
	[Fact]
	public async Task Write_the_key_each_aggregate_is_read_back_under()
	{
		var store = new RecordingProjectionStore();
		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((p, _) => p.Count++);

		var sut = BuildService(
			store,
			projection,
			Stored("agg-1", 1),
			Stored("agg-2", 2),
			Stored("agg-1", 3));

		await sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken);

		store.Written.Keys.ShouldBe(["agg-1", "agg-2"], ignoreOrder: true);
		store.Written["agg-1"].Count.ShouldBe(
			2,
			"each aggregate folds only its own events; merging them is what the single-state rebuild did");
		store.Written["agg-2"].Count.ShouldBe(1);
		store.Written.ShouldNotContainKey(
			nameof(CountProjection),
			"the projection TYPE NAME is not a key any read path queries, so a document written there "
			+ "is invisible to every reader while the rebuild reports success");
	}

	/// <summary>LIVENESS: a keyed projection still rebuilds, to the selector's key.</summary>
	/// <remarks>
	/// Without this the arm above is satisfied by a rebuild that writes nothing at all. It also pins
	/// the singleton shape: a projection that folds the whole stream into one document is a key
	/// selector returning a constant, not a separate shape the service detects.
	/// </remarks>
	[Fact]
	public async Task Rebuild_a_keyed_projection_to_the_selectors_key()
	{
		var store = new RecordingProjectionStore();
		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((p, _) => p.Count++);
		projection.AddKeySelector<Counted>(e => e.Category);

		var sut = BuildService(
			store,
			projection,
			Stored("agg-1", 1, category: "books"),
			Stored("agg-2", 2, category: "books"),
			Stored("agg-3", 3, category: "music"));

		await sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken);

		store.Written.Keys.ShouldBe(["books", "music"], ignoreOrder: true);
		store.Written["books"].Count.ShouldBe(
			2,
			"a keyed projection deliberately merges many aggregates into one key -- that is the shape, "
			+ "and a singleton projection is this with a constant selector");
		store.Written["music"].Count.ShouldBe(1);
	}

	/// <summary>A rebuild seeds FRESH and folds every event, including ones below the stored position.</summary>
	/// <remarks>
	/// The live apply path skips an event at or below the stored position, which is sound there because
	/// it has just loaded the stored state. A rebuild starts from a new instance, so that premise is
	/// false by construction: copying the filter would fold only the tail onto empty state and write it
	/// under a truthful-looking position — deterministic total destruction on the first run, with no
	/// concurrency required.
	/// </remarks>
	[Fact]
	public async Task Fold_every_event_onto_a_fresh_seed()
	{
		var store = new RecordingProjectionStore();
		store.Seed("agg-1", new CountProjection { Count = 999 }, position: 2);

		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((p, _) => p.Count++);

		var sut = BuildService(
			store,
			projection,
			Stored("agg-1", 1),
			Stored("agg-1", 2),
			Stored("agg-1", 3));

		await sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken);

		store.Written["agg-1"].Count.ShouldBe(
			3,
			"all three events fold onto a fresh instance. Carrying the stored state forward would give "
			+ "1002; applying the live path's already-folded filter would give 1 -- only the tail, on "
			+ "top of an empty seed, under a position claiming a complete fold");
		store.ExpectedPositions["agg-1"].ShouldBe(
			2,
			"the stored POSITION is still read at first touch, so the conditional write can refuse a row "
			+ "another writer moved. It is the stored STATE that a rebuild discards, not the position");
	}

	/// <summary>A superseded write is a CONFLICT for a rebuild, never a completed one.</summary>
	/// <remarks>
	/// The live settle predicate treats "the row is already at or beyond the position I attempted" as
	/// done, on the claim that such a row already holds everything this fold would have written — which
	/// is the invariant a rebuild exists because nobody believes. Under contention the live writer wins
	/// every key, so the busiest keys would be exactly the ones a rebuild silently skipped.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_rebuild_another_writer_superseded()
	{
		var store = new RecordingProjectionStore { SupersedeAt = 500 };
		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((p, _) => p.Count++);

		var sut = BuildService(store, projection, Stored("agg-1", 1));

		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken));

		refusal.Message.Contains("agg-1", StringComparison.Ordinal).ShouldBeTrue(
			"the refusal names the id it stopped on, which is what tells an operator how far the "
			+ "rebuild got");

		var status = await sut.GetStatusAsync<CountProjection>(TestContext.Current.CancellationToken);
		status.State.ShouldBe(
			ProjectionRebuildState.Failed,
			"a live writer ahead of the rebuild means the rebuilt fold never landed on that key. "
			+ "Reporting Completed would tell an operator the read model was repaired when it was not");
		store.Written.ShouldBeEmpty("a superseded write persists nothing");

		refusal.Message.ShouldContain(
			"another writer",
			customMessage: "a SUPERSEDED refusal really is a race, and the message must keep saying so -- "
				+ "this is the half that stops the unplaceable arm below passing by making every refusal "
				+ "terminal");
	}

	// LIVENESS, and it is the arm that proves an unplaceable row is now REPAIRED rather than reported.
	//
	// A row holding the unplaceable sentinel has no number for a write to advance from, so the advancing
	// write cannot express the rebuild at all -- it would have to be told to accept "expected nothing"
	// against a row that is present, which is the adopt licence a rebuild must not borrow. But a rebuild
	// folded from an EMPTY seed over the whole stream needs no prior prefix to be conditional on, which is
	// exactly what RebuildAtPositionAsync is for. So the row is overwritten and the rebuild completes.
	//
	// RefuseUnplaceable is set deliberately: it refuses every ADVANCING write, so this arm is RED the
	// moment the routing sends a numberless row back down UpsertAtPositionAsync. That is what makes it a
	// test of the route rather than of the outcome.
	[Fact]
	public async Task Rebuild_a_row_that_holds_no_placeable_position_instead_of_refusing_it()
	{
		var store = new RecordingProjectionStore { RefuseUnplaceable = true };
		store.Seed("agg-1", new CountProjection { Count = 99 }, ProjectionPosition.UnplaceableSentinel);

		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((pr, _) => pr.Count++);

		var sut = BuildService(store, projection, Stored("agg-1", 1));

		await sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken);

		var status = await sut.GetStatusAsync<CountProjection>(TestContext.Current.CancellationToken);
		status.State.ShouldBe(
			ProjectionRebuildState.Completed,
			"a row carrying no placeable position is precisely what a rebuild exists to clear, so "
			+ "reporting Failed would tell an operator to go and do by hand what just happened");

		store.Written.ShouldContainKey(
			"agg-1",
			customMessage: "the rebuilt fold must land; a rebuild that reported success without writing "
				+ "is the silent shape this seam exists to eliminate");

		store.Written["agg-1"].Count.ShouldBe(
			1,
			"the state must be the fold from an EMPTY seed over the replayed stream -- one event, so one "
			+ "-- never the 99 the discarded row held");
	}

	// SAFETY, and it is about the DIAGNOSIS rather than the behaviour: both refusals throw, and only one
	// of them can be acted on. A superseded row clears when the processor is stopped.
	//
	// An UNPLACEABLE refusal now means something narrower than it used to, because a row that ALREADY held
	// an unplaceable position when the replay read it is routed to RebuildAtPositionAsync and repaired --
	// see the arm above. Reaching the refusal means the row was ABSENT or POSITIONED at read time and holds
	// the unplaceable sentinel by write time, so a concurrent writer called the unconditional UpsertAsync
	// during the replay. It IS a race, but not the same race as a supersede, and the remedy names a
	// different culprit: stopping the processor does not stop a component that writes the projection
	// blindly on its own schedule. RED if the two refusals are folded back into one message.
	[Fact]
	public async Task Diagnose_a_blind_write_during_the_replay_as_its_own_race()
	{
		// No seeded row, so the replay reads ABSENT and attempts the insert-if-absent write. The store
		// refuses it as unplaceable, which is what a blind write landing in the gap looks like.
		var store = new RecordingProjectionStore { RefuseUnplaceable = true };
		var projection = new MultiStreamProjection<CountProjection>();
		projection.AddHandler<Counted>((pr, _) => pr.Count++);

		var sut = BuildService(store, projection, Stored("agg-1", 1));

		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.RebuildAsync<CountProjection>(TestContext.Current.CancellationToken));

		refusal.Message.ShouldContain(
			"UpsertAsync",
			customMessage: "the operator has to be pointed at the blind write that caused this, because "
				+ "stopping the processor will not stop it");

		refusal.Message.ShouldNotContain(
			"another writer advanced it",
			customMessage: "this is not a supersede: nothing advanced the row, something erased its "
				+ "position, and the two have different remedies");

		refusal.Message.ShouldNotContain(
			"unknown",
			customMessage: "an unplaceable row has no current position, so a message promising one prints "
				+ "a placeholder instead of saying what is actually wrong");

		store.Written.ShouldBeEmpty("a refused write persists nothing");
	}

	// The payload IS the event's data, so the serializer fake reconstructs each event from what it was
	// handed rather than from call order. A sequence-based fake would pass even if the rebuild paired
	// payloads with the wrong stored envelopes, which is one of the things these arms exist to see.
	private static StoredEvent Stored(string aggregateId, long position, string category = "default") =>
		new(
			EventId: $"e{position}",
			AggregateId: aggregateId,
			AggregateType: "Agg",
			EventType: "Counted",
			EventData: System.Text.Encoding.UTF8.GetBytes(category),
			Metadata: null,
			Version: position,
			Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(position))
		{
			GlobalPosition = position,
		};

	private static ProjectionRebuildService BuildService(
		RecordingProjectionStore store,
		MultiStreamProjection<CountProjection> projection,
		params StoredEvent[] events)
	{
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var serializer = A.Fake<IEventSerializer>();
		var serviceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<CountProjection>))).Returns(projection);
		A.CallTo(() => serviceProvider.GetService(typeof(IProjectionStore<CountProjection>))).Returns(store);

		A.CallTo(() => serializer.ResolveType("Counted")).Returns(typeof(Counted));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(Counted)))
			.ReturnsLazily(call =>
			{
				var payload = call.Arguments.Get<byte[]>(0)!;

				return (IDomainEvent)new Counted { Category = System.Text.Encoding.UTF8.GetString(payload) };
			});

		// One batch, then empty. The head matches the last position so the empty read is read as
		// exhausted rather than as a withheld gap.
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(events.ToList(), []);
		A.CallTo(() => globalQuery.GetHeadPositionAsync(A<CancellationToken>._))
			.Returns(events.Length == 0 ? 0 : events[^1].GlobalPosition);

		return new ProjectionRebuildService(
			serviceProvider,
			serializer,
			Options.Create(new ProjectionRebuildOptions { BatchSize = 100 }),
			NullLogger<ProjectionRebuildService>.Instance);
	}

	private sealed class CountProjection
	{
		public int Count { get; set; }
	}

	[MessageName("Test.Counted")]
	private sealed record Counted : DomainEvent
	{
		public string Category { get; init; } = "default";
	}

	/// <summary>
	/// A positioned projection store that records what a caller wrote, and under which key.
	/// </summary>
	/// <remarks>
	/// Implements the interface DIRECTLY rather than deriving from a framework base: the property under
	/// test is which KEY the rebuild writes, and a base that supplies the write would answer that
	/// question for the fixture instead of letting the rebuild answer it.
	/// </remarks>
	private sealed class RecordingProjectionStore : IPositionedProjectionStore<CountProjection>
	{
		private readonly Dictionary<string, (CountProjection State, long? Position)> _rows =
			new(StringComparer.Ordinal);

		public Dictionary<string, CountProjection> Written { get; } = new(StringComparer.Ordinal);

		public Dictionary<string, long?> ExpectedPositions { get; } = new(StringComparer.Ordinal);

		/// <summary>A position a competing writer has already reached, refusing every conditional write.</summary>
		public long? SupersedeAt { get; init; }

		/// <summary>Refuse every conditional write as unplaceable: the row holds no position to advance from.</summary>
		public bool RefuseUnplaceable { get; init; }

		public void Seed(string id, CountProjection state, long? position) => _rows[id] = (state, position);

		public object? GetService(Type serviceType) =>
			serviceType == typeof(IPositionedProjectionStore<CountProjection>) ? this : null;

		// The unnumbered write: a complete fold whose prefix has no global position number.
		// Distinct from the blind UpsertAsync, which records that the state is not a fold at all.
		public Task UpsertUnnumberedAsync(
			string id, CountProjection projection, CancellationToken cancellationToken)
		{
			UnnumberedWrites++;
			return UpsertAsync(id, projection, cancellationToken);
		}

		/// <summary>Gets how many unnumbered writes this double received.</summary>
		public int UnnumberedWrites { get; private set; }

		public Task<(CountProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string projectionId,
			CancellationToken cancellationToken) =>
			Task.FromResult(_rows.TryGetValue(projectionId, out var row)
				? (row.State, ProjectionPosition.FromStored(row.Position))
				: ((CountProjection?)null, ProjectionPosition.Unnumbered));

		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string projectionId,
			CountProjection projection,
			long? expectedPosition,
			long newPosition,
			CancellationToken cancellationToken)
		{
			ExpectedPositions[projectionId] = expectedPosition;

			if (RefuseUnplaceable)
			{
				// No CurrentPosition, deliberately: an unplaceable row has none, which is what makes the
				// race-shaped message render the word "unknown" where it promises a position.
				return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null));
			}

			if (SupersedeAt is { } ahead)
			{
				return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, ahead));
			}

			Written[projectionId] = projection;
			_rows[projectionId] = (projection, newPosition);

			return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
		}

		// Rewrites the state at the position the row already holds. Never creates: an absent row was
		// deleted, and deletion is how erasure removes personal data.
		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, CountProjection projection, long atPosition, CancellationToken cancellationToken)
		{
			if (!_rows.TryGetValue(id, out var row))
			{
				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null));
			}

			if (row.Position is not { } stored)
			{
				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null));
			}

			if (stored != atPosition)
			{
				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, stored));
			}

			_rows[id] = (projection, atPosition);

			return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));
		}

		// Overwrites BOTH state and position of an EXISTING row, for a caller that folded the whole
		// stream from an empty seed. Unconditional on POSITION but conditional on EXISTENCE: an absent
		// row was deleted, deletion is how erasure removes personal data, and a replay must not restore it.
		public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
			string id, CountProjection projection, long newPosition, CancellationToken cancellationToken)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

			if (!_rows.ContainsKey(id))
			{
				return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished));
			}

			Written[id] = projection;
			_rows[id] = (projection, newPosition);

			return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
		}

		public Task<CountProjection?> GetByIdAsync(string projectionId, CancellationToken cancellationToken) =>
			Task.FromResult(_rows.TryGetValue(projectionId, out var row) ? row.State : null);

		public Task UpsertAsync(string projectionId, CountProjection projection, CancellationToken cancellationToken)
		{
			Written[projectionId] = projection;
			_rows[projectionId] = (projection, null);

			return Task.CompletedTask;
		}

		public Task DeleteAsync(string projectionId, CancellationToken cancellationToken)
		{
			_ = _rows.Remove(projectionId);

			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<CountProjection>> QueryAsync(
			IDictionary<string, object>? filters,
			QueryOptions? options,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<CountProjection>>([]);

		public Task<long> CountAsync(
			IDictionary<string, object>? filters,
			CancellationToken cancellationToken) =>
			Task.FromResult(0L);
	}
}
