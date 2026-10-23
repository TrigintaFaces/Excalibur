// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Load and concurrency properties of the SQL Server append path, against a REAL SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is NOT a duplicate of the Postgres harness, and the reason is the design.</b> SQL Server has
/// its own <c>AllocateAndInsertEventsRequest</c> and its own counter row; contiguity is a property of a
/// provider's allocation strategy, not of the append contract. That is exactly why each provider reports
/// <c>LastEventPosition</c> itself rather than letting shared code derive <c>first + count - 1</c> — a
/// derivation would be correct for one provider and silently wrong for the next. A harness that proves
/// the property on Postgres therefore proves nothing here.
/// </para>
/// <para>
/// <b>Why these numbers.</b> Each arm is deliberately past a threshold ordinary usage never reaches: a
/// single append above the per-statement limit, so later chunks derive positions from the reserved
/// block; and genuine parallel appenders, so the counter row is actually contended. A sample seeding a
/// few dozen rows single-threaded cannot reach any of these states.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerAppendLoadAndConcurrencyShould : IClassFixture<SqlServerEventStoreContainerFixture>
{
	// Above the per-statement limit, so the append is written in more than one statement and the
	// later-chunk position arithmetic is exercised. At or below it, this arm would pass against an
	// implementation that cannot chunk at all.
	private const int EventsPastOneStatement = 300;

	private const int ConcurrentAppenders = 16;
	private const int EventsPerAppender = 25;

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerAppendLoadAndConcurrencyShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task ReportTheLastPositionOfAnAppendLargerThanOneStatement()
	{
		await _fixture.EnsureInitializedAsync();
		await _fixture.CleanupTableAsync();

		var store = CreateEventStore();
		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");

		var result = await store.AppendAsync(
			aggregateId,
			"LoadTest",
			Events(aggregateId, EventsPastOneStatement),
			-1,
			TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue(result.ErrorMessage);
		result.FirstEventPosition.ShouldNotBeNull();
		result.LastEventPosition.ShouldNotBeNull();

		result.LastEventPosition!.Value.ShouldBe(
			result.FirstEventPosition!.Value + EventsPastOneStatement - 1,
			"the reported block must cover every event the append wrote");

		var stored = await PositionsAsync("WHERE [AggregateId] = @id", ("@id", aggregateId));

		stored.Count.ShouldBe(EventsPastOneStatement);
		stored[0].ShouldBe(result.FirstEventPosition.Value);
		stored[stored.Count - 1].ShouldBe(result.LastEventPosition.Value);
		(stored[stored.Count - 1] - stored[0] + 1).ShouldBe(
			stored.Count, "the reserved block must be contiguous — a gap means a chunk wrote outside it");
	}

	[Fact]
	public async Task GiveEveryConcurrentAppenderAnExclusiveContiguousBlock()
	{
		// The property the counter row exists to provide. Each appender must receive a range that is its
		// own: no position issued twice, no block overlapping another. Overlap would mean two streams
		// interleaved in one block and a subscriber reading them out of order.
		await _fixture.EnsureInitializedAsync();
		await _fixture.CleanupTableAsync();

		var store = CreateEventStore();
		var ranges = new ConcurrentBag<(string Aggregate, long First, long Last)>();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, ConcurrentAppenders),
			TestContext.Current.CancellationToken,
			async (i, ct) =>
			{
				var aggregateId = "agg-" + i.ToString("D3") + "-" + Guid.NewGuid().ToString("N");
				var result = await store.AppendAsync(
					aggregateId, "LoadTest", Events(aggregateId, EventsPerAppender), -1, ct);

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
				ordered[i - 1].Last, "two concurrent appends were issued overlapping position blocks");
		}

		// LIVENESS: and every event landed. A store that refused every concurrent append would satisfy
		// the non-overlap assertion perfectly, so inaction must not be able to pass this arm.
		var all = await PositionsAsync(string.Empty);
		all.Count.ShouldBe(ConcurrentAppenders * EventsPerAppender);
		all.Distinct().Count().ShouldBe(all.Count, "a position was issued to more than one event");
	}

	[Fact]
	public async Task KeepTheGlobalStreamGaplessUnderConcurrentLoad()
	{
		// A catch-up subscriber reads in position order and stops at the first hole it cannot explain,
		// so the contiguous-prefix claim is what makes a subscription sound.
		await _fixture.EnsureInitializedAsync();
		await _fixture.CleanupTableAsync();

		var store = CreateEventStore();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, ConcurrentAppenders),
			TestContext.Current.CancellationToken,
			async (i, ct) =>
			{
				var aggregateId = "gapless-" + i.ToString("D3") + "-" + Guid.NewGuid().ToString("N");
				_ = await store.AppendAsync(
					aggregateId, "LoadTest", Events(aggregateId, EventsPerAppender), -1, ct);
			});

		var positions = await PositionsAsync(string.Empty);

		positions.Count.ShouldBe(ConcurrentAppenders * EventsPerAppender);
		(positions[positions.Count - 1] - positions[0] + 1).ShouldBe(
			positions.Count, "the committed global stream has a hole in it");
	}

	// The interpolated parts are the fixture's own schema and table names and a literal WHERE clause from
	// this file's call sites; every VALUE is a parameter. Matches the fixture's own suppression, which
	// builds its TRUNCATE the same way for the same reason.
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Identifiers come from the test fixture; all values are parameterised.")]
	private async Task<List<long>> PositionsAsync(string where, params (string Name, object Value)[] parameters)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken);

		var sql = $"SELECT [Position] FROM [{_fixture.SchemaName}].[{_fixture.TableName}] {where} ORDER BY [Position]";
		await using var command = new SqlCommand(sql, connection);
		foreach (var (name, value) in parameters)
		{
			_ = command.Parameters.AddWithValue(name, value);
		}

		var positions = new List<long>();
		await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
		while (await reader.ReadAsync(TestContext.Current.CancellationToken))
		{
			positions.Add(reader.GetInt64(0));
		}

		return positions;
	}

	private static IEnumerable<IDomainEvent> Events(string aggregateId, int count) =>
		Enumerable.Range(0, count).Select(i => (IDomainEvent)new LoadTestEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			Version = i,
		});

	private IEventStore CreateEventStore() =>
		new SqlServerEventStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerEventStore>.Instance,
			SingleTenantTestContext.Instance);

	[MessageName("Test.SqlServerAppendLoadAndConcurrency.LoadTestEvent")]
	private sealed class LoadTestEvent : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required DateTimeOffset OccurredAt { get; init; }

		public required long Version { get; init; }

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
