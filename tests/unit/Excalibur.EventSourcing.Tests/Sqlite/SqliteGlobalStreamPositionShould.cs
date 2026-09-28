// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// The SQLite event store allocates gapless global positions and exposes them through a readable global
/// stream.
/// </summary>
/// <remarks>
/// <para>
/// SQLite is an embedded engine, so this IS real infrastructure -- no container, nothing mocked, and the
/// arms are inherently non-skipped. The engine's own transaction semantics are what is under test.
/// </para>
/// <para>
/// <b>What is being protected.</b> <c>GlobalPosition</c> was previously
/// <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>, assigned by SQLite. AUTOINCREMENT forbids reuse of a
/// deleted row's value, which the old schema comment called load-bearing -- but it never addressed the
/// case that actually produces holes: an ABORTED append still consumes its AUTOINCREMENT value. The
/// store now allocates the position from a counter row inside the appending transaction, where the
/// increment rolls back with the transaction, so nothing is consumed by an append that does not commit.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- a losing append consumes no position and the
/// committed stream stays contiguous. LIVENESS -- appends still succeed, the stream reads them back, and
/// the head advances, so a store that refused every append (trivially gapless) fails rather than passes.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SqliteGlobalStreamPositionShould : IDisposable
{
	private const string AggregateType = "Order";

	private readonly string _databasePath;
	private readonly string _connectionString;
	private readonly string _table = "Events";

	public SqliteGlobalStreamPositionShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-gsp-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";
	}

	public void Dispose()
	{
		// Scoped to this suite's own connection string, never ClearAllPools(): the pool is keyed by
		// connection string, and the process-global clear disposes handles belonging to every other
		// Sqlite test running in parallel -- which surfaces in THEM as ObjectDisposedException.
		using var pooled = new SqliteConnection(_connectionString);
		SqliteConnection.ClearPool(pooled);
		if (File.Exists(_databasePath))
		{
			try
			{
				File.Delete(_databasePath);
			}
			catch (IOException)
			{
				// A temp file the OS still holds is not a test failure.
			}
		}
	}

	[MessageName("Test.SqliteGlobalStreamPosition.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	private SqliteEventStore Store() =>
		new(
			_connectionString,
			NullLogger<SqliteEventStore>.Instance,
			TestTenantContext.SingleTenantDefault,
			Options.Create(new TenantContextOptions()),
			_table);

	private SqliteGlobalStreamQuery Query() => new(_connectionString, _table, requireTenant: false);

	private static async Task<long> AppendAsync(SqliteEventStore store, string aggregateId, long expectedVersion)
	{
		var result = await store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { new OrderPlaced(aggregateId) },
				expectedVersion,
				CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue("the append must succeed for its position to mean anything");

		return result.FirstEventPosition.ShouldNotBeNull(
			"a successful append must report the global position it was allocated");
	}

	[Fact]
	public async Task ReadTheGlobalStreamInPositionOrderAcrossAggregates()
	{
		var store = Store();
		var first = "agg-" + Guid.NewGuid().ToString("N");
		var second = "agg-" + Guid.NewGuid().ToString("N");

		var p1 = await AppendAsync(store, first, -1).ConfigureAwait(false);
		var p2 = await AppendAsync(store, second, -1).ConfigureAwait(false);
		var p3 = await AppendAsync(store, first, 0).ConfigureAwait(false);

		// SAFETY — the counter is global, not per stream: returning to the first aggregate must yield the
		// highest position, not a value continuing that stream's own numbering.
		p2.ShouldBe(p1 + 1);
		p3.ShouldBe(p2 + 1);

		var events = await Query().ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS — the query actually returns them. Everything above would still hold if the stream
		// were unreadable, which is the state every non-SQL-Server provider was in.
		events.Count.ShouldBe(3, "the global stream must return every appended event");
		events.Select(static e => e.GlobalPosition).ShouldBe(new[] { p1, p2, p3 }, "in position order");

		var head = await Query().GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
		head.ShouldBe(p3, "the head is the highest committed position");
	}

	[Fact]
	public async Task NotBurnAPositionWhenAnAppendLosesAConcurrencyRace()
	{
		var store = Store();
		var contended = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await AppendAsync(store, contended, -1).ConfigureAwait(false);

		// Two appends at the SAME expected version. Exactly one can win; the loser rolls back. Which one
		// wins is not deterministic, but the OUTCOME asserted below is.
		var racers = await Task.WhenAll(
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()),
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()))
			.ConfigureAwait(false);

		// LIVENESS — one of them committed. A store that failed both would satisfy the gapless assertion
		// below trivially.
		var winners = racers.Count(static r => r.Success);
		winners.ShouldBe(1, "exactly one append may win a same-version race");

		var next = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), -1).ConfigureAwait(false);

		// SAFETY — the losing append consumed no position, so the stream is still contiguous. Under
		// AUTOINCREMENT the loser's value would be burned and this would be seed + 3.
		next.ShouldBe(
			seed + 2,
			"an append that rolls back must not consume a global position — the counter increment rolls " +
			"back with its transaction");

		var events = await Query().ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None)
			.ConfigureAwait(false);
		var positions = events.Select(static e => e.GlobalPosition).ToList();
		(positions[^1] - positions[0] + 1).ShouldBe(positions.Count, "committed positions must be contiguous");
	}

	[Fact]
	public async Task ResumeAboveExistingEventsWhenTheCounterIsCreatedOnAPopulatedDatabase()
	{
		// A database written before the counter existed: events are present, the counter table is not.
		// Seeding the counter at 0 here would reissue positions 1.. and every append would fail on the
		// primary key, so the counter seeds from the table's own high-water mark instead.
		await using (var connection = new SqliteConnection(_connectionString))
		{
			await connection.OpenAsync().ConfigureAwait(false);
			await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // The table name is a constant in this test, not input
			command.CommandText = $"""
				CREATE TABLE IF NOT EXISTS [{_table}] (
					GlobalPosition INTEGER PRIMARY KEY,
					EventId TEXT NOT NULL, AggregateId TEXT NOT NULL, AggregateType TEXT NOT NULL,
					EventType TEXT NOT NULL, EventData BLOB, Metadata BLOB,
					Version INTEGER NOT NULL, Timestamp TEXT NOT NULL, TenantId TEXT NOT NULL,
					UNIQUE(AggregateId, AggregateType, Version, TenantId)
				);
				INSERT INTO [{_table}] (GlobalPosition, EventId, AggregateId, AggregateType, EventType,
					EventData, Metadata, Version, Timestamp, TenantId)
				VALUES (500, 'legacy-1', 'legacy-agg', 'Order', 'Legacy', X'01', NULL, 0, '2026-01-01T00:00:00.0000000+00:00', 'default');
				""";
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		var position = await AppendAsync(Store(), "agg-" + Guid.NewGuid().ToString("N"), -1).ConfigureAwait(false);

		// SAFETY — above every existing row. A counter seeded at 0 would have collided on the key here.
		position.ShouldBe(
			501,
			"the position counter must resume above the events already in the table, not restart at 1");
	}
}
