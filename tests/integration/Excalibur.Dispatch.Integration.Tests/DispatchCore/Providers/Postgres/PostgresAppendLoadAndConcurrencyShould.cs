// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Load and concurrency properties of the append path, against a REAL Postgres.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists, and why a sample does not replace it.</b> The CDC sample seeds roughly 28 rows,
/// single-threaded. Every property below needs a threshold that volume never reaches: a single append
/// larger than <c>MaxEventsPerStatement</c> (256), and genuine parallel appenders contending for the
/// position counter. A sample proves the pieces connect; it cannot reach these states at all, so a green
/// sample run says nothing about them.
/// </para>
/// <para>
/// <b>A mock cannot host any of this.</b> The object under test is the store's own allocation and
/// isolation behaviour — a counter row whose lock is held until commit, and the contiguity that follows
/// from it. A fake returns what it was told, and would certify every property here while the real store
/// violated them.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "EventStore")]
[Trait("Database", "Postgres")]
[Trait(TraitNames.Category, TestCategories.Integration)]
public sealed class PostgresAppendLoadAndConcurrencyShould : IntegrationTestBase
{
	// Deliberately ABOVE InsertEventsBatchRequest.MaxEventsPerStatement (256), so the append is written
	// in more than one statement and the later-chunk position arithmetic is actually exercised. At or
	// below the limit this arm would pass against an implementation that cannot chunk at all.
	private const int EventsPastOneStatement = 300;

	private const int ConcurrentAppenders = 16;
	private const int EventsPerAppender = 25;

	private readonly PostgresFixture _pgFixture;

	public PostgresAppendLoadAndConcurrencyShould(PostgresFixture pgFixture) => _pgFixture = pgFixture;

	[Fact]
	public async Task ReportTheLastPositionOfAnAppendLargerThanOneStatement()
	{
		// The session token is the LAST event's position. An append of 300 events is written in more than
		// one statement, so the later chunks' positions are derived from the block the first reserved —
		// arithmetic no unit test can exercise, because it needs the store to actually allocate.
		await InitializeEventTableAsync();
		var store = CreateEventStore();
		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var result = await store.AppendAsync(
			aggregateId,
			"LoadTest",
			Events(aggregateId, EventsPastOneStatement, fromVersion: 0),
			-1,
			TestCancellationToken);

		result.Success.ShouldBeTrue(result.ErrorMessage);
		result.FirstEventPosition.ShouldNotBeNull();
		result.LastEventPosition.ShouldNotBeNull();

		result.LastEventPosition!.Value.ShouldBe(
			result.FirstEventPosition!.Value + EventsPastOneStatement - 1,
			"the reported block must cover every event the append wrote");

		// And the block is real, not merely arithmetic: the rows are there and contiguous.
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var stored = (await connection.QueryAsync<long>(
			"SELECT position FROM public.events WHERE aggregate_id = @id ORDER BY position",
			new { id = aggregateId })).ToList();

		stored.Count.ShouldBe(EventsPastOneStatement);
		stored[0].ShouldBe(result.FirstEventPosition.Value);
		stored[stored.Count - 1].ShouldBe(result.LastEventPosition.Value);
		stored.ShouldBe(
			Enumerable.Range(0, EventsPastOneStatement)
				.Select(i => result.FirstEventPosition.Value + i)
				.ToList(),
			"the reserved block must be contiguous — a gap means a later chunk wrote outside it");
	}

	[Fact]
	public async Task GiveEveryConcurrentAppenderAnExclusiveContiguousBlock()
	{
		// THE PROPERTY THE COUNTER ROW EXISTS TO PROVIDE, and the one no amount of reading settles.
		// Each appender must receive a range of positions that is its own: no position issued twice, none
		// lost, and no other appender's events inside anyone's range. Overlap would mean two streams
		// interleaved in one block and a subscriber reading them out of order.
		await InitializeEventTableAsync();
		var store = CreateEventStore();

		var ranges = new ConcurrentBag<(string Aggregate, long First, long Last)>();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, ConcurrentAppenders),
			TestCancellationToken,
			async (i, ct) =>
			{
				var aggregateId = "agg-" + i.ToString("D3") + "-" + Guid.NewGuid().ToString("N");
				var result = await store.AppendAsync(
					aggregateId, "LoadTest", Events(aggregateId, EventsPerAppender, fromVersion: 0), -1, ct);

				result.Success.ShouldBeTrue(result.ErrorMessage);
				ranges.Add((aggregateId, result.FirstEventPosition!.Value, result.LastEventPosition!.Value));
			});

		var ordered = ranges.OrderBy(r => r.First).ToList();
		ordered.Count.ShouldBe(ConcurrentAppenders);

		foreach (var range in ordered)
		{
			(range.Last - range.First + 1).ShouldBe(
				EventsPerAppender, range.Aggregate + " was issued a block of the wrong size");
		}

		// SAFETY: no two blocks overlap.
		for (var i = 1; i < ordered.Count; i++)
		{
			ordered[i].First.ShouldBeGreaterThan(
				ordered[i - 1].Last,
				"two concurrent appends were issued overlapping position blocks");
		}

		// LIVENESS: and every event actually landed. Without this, an implementation that refused every
		// concurrent append would satisfy the non-overlap assertion perfectly.
		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var total = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM public.events");
		total.ShouldBe(ConcurrentAppenders * EventsPerAppender);

		var distinct = await connection.ExecuteScalarAsync<long>(
			"SELECT COUNT(DISTINCT position) FROM public.events");
		distinct.ShouldBe(total, "a position was issued to more than one event");
	}

	[Fact]
	public async Task KeepTheGlobalStreamGaplessUnderConcurrentLoad()
	{
		// A subscriber reads the global stream in position order and stops at the first hole it cannot
		// explain. The counter is allocated INSIDE the appending transaction precisely so an aborted
		// append burns no value — this asserts the committed set really is a contiguous prefix after
		// concurrent writers, which is the claim that makes a catch-up subscription sound.
		await InitializeEventTableAsync();
		var store = CreateEventStore();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, ConcurrentAppenders),
			TestCancellationToken,
			async (i, ct) =>
			{
				var aggregateId = "gapless-" + i.ToString("D3") + "-" + Guid.NewGuid().ToString("N");
				_ = await store.AppendAsync(
					aggregateId, "LoadTest", Events(aggregateId, EventsPerAppender, fromVersion: 0), -1, ct);
			});

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var positions = (await connection.QueryAsync<long>(
			"SELECT position FROM public.events ORDER BY position")).ToList();

		positions.Count.ShouldBe(ConcurrentAppenders * EventsPerAppender);
		(positions[positions.Count - 1] - positions[0] + 1).ShouldBe(
			positions.Count, "the committed global stream has a hole in it");
	}

	private static IEnumerable<IDomainEvent> Events(string aggregateId, int count, long fromVersion) =>
		Enumerable.Range(0, count).Select(i => (IDomainEvent)new LoadTestEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Version = fromVersion + i,
			Sequence = i,
			Data = "payload-" + i.ToString(),
		});

	private IEventStore CreateEventStore() =>
		new PostgresEventStore(
			_pgFixture.ConnectionString,
			NullLogger<PostgresEventStore>.Instance,
			tenantContext: new TestTenantContext());

	private async Task InitializeEventTableAsync()
	{
		const string CreateTableSql =
			"CREATE TABLE IF NOT EXISTS public.events (" +
			"    position BIGINT PRIMARY KEY," +
			"    event_id VARCHAR(255) NOT NULL UNIQUE," +
			"    aggregate_id VARCHAR(255) NOT NULL," +
			"    aggregate_type VARCHAR(255) NOT NULL," +
			"    event_type VARCHAR(255) NOT NULL," +
			"    event_data BYTEA NOT NULL," +
			"    metadata BYTEA," +
			"    version BIGINT NOT NULL," +
			"    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()," +
			"    archived_at TIMESTAMPTZ NULL," +
			"    tenant_id VARCHAR(450) NOT NULL DEFAULT '__untenanted__'," +
			"    CONSTRAINT uq_aggregate_version UNIQUE (tenant_id, aggregate_id, aggregate_type, version)" +
			");" +
			"CREATE TABLE IF NOT EXISTS events_position (" +
			"    id    SMALLINT PRIMARY KEY CHECK (id = 1)," +
			"    value BIGINT NOT NULL" +
			");" +
			"INSERT INTO events_position (id, value)" +
			" SELECT 1, COALESCE((SELECT MAX(position) FROM events), 0)" +
			" ON CONFLICT (id) DO NOTHING;" +
			"CREATE INDEX IF NOT EXISTS idx_events_aggregate" +
			" ON public.events (aggregate_id, aggregate_type, version);";

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(CreateTableSql);

		// Each arm asserts over the WHOLE table, so it starts from an empty one — and the counter is
		// reset with it, or the first append continues from a previous arm's block and the gapless
		// assertion measures the wrong range.
		_ = await connection.ExecuteAsync("TRUNCATE TABLE public.events RESTART IDENTITY CASCADE;");
		_ = await connection.ExecuteAsync("UPDATE events_position SET value = 0 WHERE id = 1;");
	}

	[MessageName("Test.PostgresAppendLoadAndConcurrency.LoadTestEvent")]
	private sealed class LoadTestEvent : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required DateTimeOffset OccurredAt { get; init; }

		public required long Version { get; init; }

		public required int Sequence { get; init; }

		public required string Data { get; init; }

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
