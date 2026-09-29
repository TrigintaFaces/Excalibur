// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure atomicity lock for <see cref="DynamoDbEventStore"/>'s append (5wo4w2, REVIEW fail-fast fix):
/// DynamoDB's <c>TransactWriteItems</c> hard-caps at 100 items with no &gt;100 atomic primitive, so an oversized
/// append cannot honor the all-or-nothing <see cref="IEventStore.AppendAsync"/> contract — it is REJECTED at the API
/// boundary before any write, making a torn event-stream prefix impossible by construction.
/// </summary>
/// <remarks>
/// A &gt;100-event append throws <see cref="ArgumentOutOfRangeException"/> before issuing any write, so nothing is
/// persisted (true all-or-nothing). A 100-event append (the boundary) commits atomically in full. <b>RED behavior:</b>
/// the prior chunked-<c>TransactWriteItems</c> path (now removed) would partially commit a &gt;100 append, leaving a
/// torn prefix. Never skipped.
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "DynamoDb")]
[Trait("Component", "EventStore")]
public sealed class DynamoDbEventStoreChunkedAppendAtomicityShould : IClassFixture<DynamoDbEventStoreContainerFixture>
{
	private const string AggregateType = "OversizedAppendAggregate";
	private const int TransactItemLimit = 100;
	private readonly DynamoDbEventStoreContainerFixture _fixture;

	public DynamoDbEventStoreChunkedAppendAtomicityShould(DynamoDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private IEventStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra atomicity is never skipped: "
			+ $"{_fixture.InitializationError}");

		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = $"{_fixture.TableName}_{Guid.NewGuid():N}",
			CreateTableIfNotExists = true,
		});
		return new DynamoDbEventStore(_fixture.Client, _fixture.StreamsClient, options, NullLogger<DynamoDbEventStore>.Instance, UntenantedContext.Instance);
	}

	private static List<TestDomainEvent> NewBatch(string aggregateId, int count) =>
		[.. Enumerable.Range(0, count).Select(_ => new TestDomainEvent
		{
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Data = $"data-{Guid.NewGuid():N}",
		})];

	[Fact]
	public async Task Reject_an_oversized_append_at_the_boundary_without_any_partial_write()
	{
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		// > 100 events cannot be appended atomically on DynamoDB → rejected before any write.
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			async () => await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 150), expectedVersion: -1, CancellationToken.None));

		// True all-or-nothing: nothing was persisted (no torn prefix).
		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(0, "a rejected oversized append must persist NOTHING (all-or-nothing, no torn prefix)");
	}

	[Fact]
	public async Task Append_a_max_size_batch_atomically_in_full()
	{
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		// Exactly the 100-item boundary commits atomically in full.
		var result = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, TransactItemLimit), expectedVersion: -1, CancellationToken.None);
		result.Success.ShouldBeTrue();

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(TransactItemLimit, "a 100-event append is within the atomic limit and must persist in full");
		loaded.Select(e => e.Version).ShouldBe(
			Enumerable.Range(0, TransactItemLimit).Select(i => (long)i),
			"the committed events must be a contiguous version prefix v0..v99");
	}

	[Fact]
	public async Task Refuse_an_oversized_append_under_every_configuration()
	{
		// SUPERSEDED ARM. This previously asserted the opposite -- that a >100 append SUCCEEDS on the
		// non-transactional opt-out path -- because that path existed and accepted it best-effort. The
		// opt-out is removed: it bought throughput with a torn prefix no later read could detect, and
		// there is now no configuration in which an oversized append is accepted.
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			async () => await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 150), expectedVersion: -1, CancellationToken.None));

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(0, "no configuration may accept an oversized append, and none may leave a partial stream");
	}

	[Fact]
	public async Task Append_a_single_event_without_a_transaction_and_still_commit_it()
	{
		// The single-event fast path is the one saving the removed flag legitimately offered: one
		// conditional PutItem is atomic by itself, so it skips TransactWriteItems and its doubled write
		// cost. It is kept, so it needs an arm -- otherwise the carve-out is untested.
		var store = CreateStore();
		var aggregateId = $"agg-{Guid.NewGuid():N}";

		var result = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, 1), expectedVersion: -1, CancellationToken.None);
		result.Success.ShouldBeTrue();

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(1, "a single-event append commits without a transaction");
	}
}
