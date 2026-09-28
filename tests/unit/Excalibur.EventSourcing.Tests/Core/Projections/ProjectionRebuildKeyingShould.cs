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
