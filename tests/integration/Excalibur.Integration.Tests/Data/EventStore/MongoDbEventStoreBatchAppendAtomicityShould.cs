// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure atomicity lock for <see cref="MongoDbEventStore"/>'s batch append (ccn3qt): all-or-nothing
/// persistence, contiguous version order, and a contiguous global-sequence block reserved by a single
/// <c>Inc(count)</c> (replacing the per-event FindOneAndUpdate).
/// </summary>
[Collection(MongoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "MongoDb")]
[Trait("Component", "EventStore")]
public sealed class MongoDbEventStoreBatchAppendAtomicityShould : IClassFixture<MongoDbEventStoreContainerFixture>
{
	private const string AggregateType = "BatchAppendAtomicityAggregate";
	private readonly MongoDbEventStoreContainerFixture _fixture;

	public MongoDbEventStoreBatchAppendAtomicityShould(MongoDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private MongoDbEventStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB EventStore batch-append atomicity runs against real infrastructure and is never skipped.");
		var options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});
		return new MongoDbEventStore(options, NullLogger<MongoDbEventStore>.Instance);
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
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var first = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 5), expectedVersion: -1, CancellationToken.None);
		first.Success.ShouldBeTrue();

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(5, "a multi-event batch append must be all-or-nothing");
		loaded.Select(e => e.Version).ShouldBe(Enumerable.Range(0, 5).Select(i => (long)i),
			"the batch must load in contiguous ascending version order");

		var aggregateB = $"agg-{Guid.NewGuid():N}";
		var second = await store.AppendAsync(aggregateB, AggregateType, NewBatch(aggregateB, 3), expectedVersion: -1, CancellationToken.None);
		second.Success.ShouldBeTrue();
		second.FirstEventPosition.ShouldBe(first.FirstEventPosition + 5,
			"the global sequence must advance by exactly the prior batch's event count (single Inc block reservation)");
	}

	[Fact]
	public async Task Append_all_or_nothing_under_concurrent_contention()
	{
		var store = CreateStore();
		const int aggregates = 8;
		const int perBatch = 4;

		var ids = Enumerable.Range(0, aggregates).Select(_ => $"agg-{Guid.NewGuid():N}").ToArray();

		var results = await Task.WhenAll(ids
			.Select(id => Task.Run(() =>
				store.AppendAsync(id, AggregateType, NewBatch(id, perBatch), expectedVersion: -1, CancellationToken.None).AsTask())))
			.ConfigureAwait(false);

		for (var i = 0; i < ids.Length; i++)
		{
			var loaded = await store.LoadAsync(ids[i], AggregateType, CancellationToken.None);
			if (results[i].Success)
			{
				loaded.Count.ShouldBe(perBatch, "a successful concurrent append must persist its WHOLE batch (no torn prefix)");
			}
			else
			{
				loaded.Count.ShouldBe(0, "a failed concurrent append must persist NOTHING (all-or-nothing)");
			}
		}

		var committedFirstPositions = results.Where(r => r.Success).Select(r => r.FirstEventPosition).ToList();
		committedFirstPositions.Distinct().Count().ShouldBe(
			committedFirstPositions.Count,
			"committed concurrent batches must each reserve a distinct global sequence block");
	}

	[Fact]
	public async Task Reject_a_second_concurrent_append_to_the_same_aggregate_version()
	{
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";
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
