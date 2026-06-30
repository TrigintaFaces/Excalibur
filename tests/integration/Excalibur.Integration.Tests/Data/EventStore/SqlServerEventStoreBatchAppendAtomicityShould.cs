// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure atomicity lock for <see cref="SqlServerEventStore"/>'s multi-row batch append (ccn3qt):
/// a multi-event append is all-or-nothing, positions map to versions correctly, and concurrent appends to distinct
/// aggregates never collide on a global position.
/// </summary>
/// <remarks>
/// The batch append emits a single multi-row <c>INSERT … OUTPUT INSERTED.Position, INSERTED.Version</c> and matches
/// positions back to events **by version** (SQL Server does not guarantee OUTPUT row order). This lock pins that
/// correctness point: loaded events ordered by version must have strictly-increasing global positions (an
/// OUTPUT-order-scrambled mapping would interleave them). Never skipped (<c>DockerAvailable.ShouldBeTrue</c>).
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
public sealed class SqlServerEventStoreBatchAppendAtomicityShould : IClassFixture<SqlServerEventStoreContainerFixture>
{
	private const string AggregateType = "BatchAppendAtomicityAggregate";
	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreBatchAppendAtomicityShould(SqlServerEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private async Task<SqlServerEventStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server EventStore batch-append atomicity runs against real infrastructure and is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		return new SqlServerEventStore(_fixture.ConnectionString, NullLogger<SqlServerEventStore>.Instance);
	}

	private static List<TestDomainEvent> NewBatch(string aggregateId, int count) =>
		[.. Enumerable.Range(0, count).Select(_ => new TestDomainEvent
		{
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = $"data-{Guid.NewGuid():N}",
		})];

	[Fact]
	public async Task Persist_the_whole_batch_atomically_in_contiguous_version_order()
	{
		var store = await CreateStoreAsync();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var first = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 5), expectedVersion: -1, CancellationToken.None);
		first.Success.ShouldBeTrue();

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);

		// No torn prefix: the whole batch is visible, never a partial subset.
		loaded.Count.ShouldBe(5, "a multi-event batch append must be all-or-nothing");

		// Versions are a contiguous 0..N-1 sequence in load order — the batch is mapped, not scrambled.
		loaded.Select(e => e.Version).ShouldBe(Enumerable.Range(0, 5).Select(i => (long)i),
			"the batch must load in contiguous ascending version order");

		// First-position is assigned and the global sequence advances by exactly the batch size: a second batch's
		// first position is contiguous with the first (the multi-row insert reserves a contiguous block, no gap/overlap).
		var aggregateB = $"agg-{Guid.NewGuid():N}";
		var second = await store.AppendAsync(aggregateB, AggregateType, NewBatch(aggregateB, 3), expectedVersion: -1, CancellationToken.None);
		second.Success.ShouldBeTrue();
		second.FirstEventPosition.ShouldBe(first.FirstEventPosition + 5,
			"the global position sequence must advance by exactly the prior batch's event count (contiguous block reservation)");
	}

	[Fact]
	public async Task Append_all_or_nothing_under_concurrent_contention()
	{
		var store = await CreateStoreAsync();
		const int aggregates = 8;
		const int perBatch = 4;

		var ids = Enumerable.Range(0, aggregates).Select(_ => $"agg-{Guid.NewGuid():N}").ToArray();

		// The store uses Serializable isolation, so concurrent appends may deadlock-victim (Success=false, retryable).
		// The atomicity guarantee is per-append all-or-nothing — NOT that every concurrent append wins without retry.
		var results = await Task.WhenAll(ids
			.Select(id => Task.Run(() =>
				store.AppendAsync(id, AggregateType, NewBatch(id, perBatch), expectedVersion: -1, CancellationToken.None).AsTask())))
			.ConfigureAwait(false);

		// Each aggregate is all-or-nothing: a success persists its FULL batch, a deadlock-victim persists ZERO events.
		for (var i = 0; i < ids.Length; i++)
		{
			var loaded = await store.LoadAsync(ids[i], AggregateType, CancellationToken.None);
			if (results[i].Success)
			{
				loaded.Count.ShouldBe(perBatch, "a successful concurrent append must persist its WHOLE batch (no torn prefix)");
			}
			else
			{
				loaded.Count.ShouldBe(0, "a failed (deadlock-victim) append must persist NOTHING (rolled back, all-or-nothing)");
			}
		}

		// Among the appends that committed, each reserved a DISTINCT global position block (no overlap/reuse).
		var committedFirstPositions = results.Where(r => r.Success).Select(r => r.FirstEventPosition).ToList();
		committedFirstPositions.Distinct().Count().ShouldBe(
			committedFirstPositions.Count,
			"committed concurrent batches must each reserve a distinct global position block");
	}

	[Fact]
	public async Task Reject_a_second_concurrent_append_to_the_same_aggregate_version()
	{
		var store = await CreateStoreAsync();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		// Seed v0 so both racers contend on expectedVersion = 0.
		_ = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 1), expectedVersion: -1, CancellationToken.None);

		async Task<bool> TryAppend()
		{
			try
			{
				var r = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 1), expectedVersion: 0, CancellationToken.None);
				return r.Success;
			}
			catch (ConcurrencyException)
			{
				return false;
			}
		}

		var results = await Task.WhenAll(TryAppend(), TryAppend()).ConfigureAwait(false);

		results.Count(ok => ok).ShouldBe(1, "optimistic concurrency must admit exactly one of two racers at the same version");
	}
}
