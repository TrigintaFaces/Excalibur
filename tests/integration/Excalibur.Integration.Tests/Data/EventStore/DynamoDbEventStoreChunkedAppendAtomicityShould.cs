// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

	private IEventStore CreateStore(bool transactional = true)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra atomicity is never skipped: "
			+ $"{_fixture.InitializationError}");

		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = $"{_fixture.TableName}_{Guid.NewGuid():N}",
			CreateTableIfNotExists = true,
			UseTransactionalWrite = transactional,
		});
		return new DynamoDbEventStore(_fixture.Client, _fixture.StreamsClient, options, NullLogger<DynamoDbEventStore>.Instance);
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
		var store = CreateStore(transactional: true);
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
		var store = CreateStore(transactional: true);
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
	public async Task Allow_an_oversized_append_on_the_non_transactional_opt_out_path()
	{
		// Scope guard (SA over-rejection finding): the 100-item cap is TransactWriteItems-specific. The opt-out
		// per-item PutItem path (UseTransactionalWrite=false) may legitimately append >100 (best-effort), so the
		// reject must NOT fire here — a regression that rejected on this path would fail this test.
		var store = CreateStore(transactional: false);
		var aggregateId = $"agg-{Guid.NewGuid():N}";
		const int oversized = 150;

		var result = await store.AppendAsync(aggregateId, AggregateType, NewBatch(aggregateId, oversized), expectedVersion: -1, CancellationToken.None);
		result.Success.ShouldBeTrue("the non-transactional opt-out path must accept a >100-event append (no TransactWriteItems cap)");

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(oversized, "the opt-out path persists the whole >100 append");
	}
}
