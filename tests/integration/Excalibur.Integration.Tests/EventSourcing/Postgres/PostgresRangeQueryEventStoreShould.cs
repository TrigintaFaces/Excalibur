// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres.ParallelCatchUp;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture; values are parameterized.

namespace Excalibur.Integration.Tests.EventSourcing.Postgres;

/// <summary>
/// Docker-Postgres engage-tests (ADR-336 Wave 2) for <see cref="PostgresRangeQueryEventStore"/>:
/// <list type="bullet">
///   <item><b>bd-gzph5a</b> — each yielded <see cref="StoredEvent.GlobalPosition"/> must equal its row's
///   <c>global_position</c> (the store built the event without the arg, so it defaulted to <c>0</c>).</item>
///   <item><b>bd-778kpz</b> (AC-2, Postgres) — gap-tolerant paging: a gap in <c>global_position</c> narrower
///   than the read range, with <c>batchSize</c> smaller than the gap, must NOT stop enumeration early.</item>
/// </list>
/// Both bugs were fixed in the same commit; these drive the real internal range store against real Postgres
/// (the only place a wrong column-map or a break-on-empty is observable). Rows are seeded with explicit
/// <c>global_position</c> values so gaps and positions are fully deterministic.
/// </summary>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "EventStore")]
public sealed class PostgresRangeQueryEventStoreShould : IClassFixture<PostgresEventStoreContainerFixture>
{
	private const string TableName = "range_query_events";
	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresRangeQueryEventStoreShould(PostgresEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// bd-gzph5a — the range store must carry each row's <c>global_position</c> onto
	/// <see cref="StoredEvent.GlobalPosition"/>. RED pre-fix (omitted ctor arg → every value is the <c>0</c>
	/// sentinel); GREEN once the row value is mapped through.
	/// </summary>
	[Fact]
	public async Task PopulateGlobalPositionFromTheRow()
	{
		await EnsureTableAsync();

		var seeded = new long[] { 1, 2, 3 };
		await SeedAsync(seeded);

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		var rangeStore = new PostgresRangeQueryEventStore(
			dataSource, "public", TableName, NullLogger<PostgresRangeQueryEventStore>.Instance);

		var collected = new List<StoredEvent>();
		await foreach (var stored in rangeStore.ReadRangeAsync(1, 3, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false))
		{
			collected.Add(stored);
		}

		var positions = collected.Select(e => e.GlobalPosition).ToList();

		// RED pre-fix: positions would be [0,0,0] (the unset sentinel) — never the row values.
		positions.SequenceEqual(seeded).ShouldBeTrue(
			$"each StoredEvent.GlobalPosition must equal its row global_position — expected [{string.Join(",", seeded)}] " +
			$"but got [{string.Join(",", positions)}] (bd-gzph5a — omitted ctor arg defaults to 0)");

		// AC-2: ascending global order, no zero-collisions.
		positions.SequenceEqual(positions.OrderBy(p => p)).ShouldBeTrue("events must be yielded in ascending global_position order");
		positions.ShouldNotContain(0L, "no event may carry the unset GlobalPosition sentinel");
	}

	/// <summary>
	/// bd-778kpz (AC-2) — a <c>global_position</c> gap wider than <c>batchSize</c> must not stop paging; every
	/// event in <c>[from,to]</c> after the gap must still be returned. RED pre-fix (break-on-empty drops them).
	/// </summary>
	[Fact]
	public async Task ReturnEventsAfterAGlobalPositionGap()
	{
		await EnsureTableAsync();

		// Gap 4..9 (7 wide) between {1,2,3} and {10,11}; batchSize 2 < gap so interior batches are empty.
		var seeded = new long[] { 1, 2, 3, 10, 11 };
		await SeedAsync(seeded);

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		var rangeStore = new PostgresRangeQueryEventStore(
			dataSource, "public", TableName, NullLogger<PostgresRangeQueryEventStore>.Instance);

		var collected = new List<StoredEvent>();
		await foreach (var stored in rangeStore.ReadRangeAsync(1, 11, batchSize: 2, CancellationToken.None)
			.ConfigureAwait(false))
		{
			collected.Add(stored);
		}

		// Assert on event IDENTITY (not GlobalPosition) so this lock isolates gap tolerance: the range store
		// filters/orders by the global_position COLUMN (correct pre-fix), so which rows are returned depends only
		// on the paging loop — independent of the separate gzph5a property-mapping fix.
		var expectedIds = seeded.Select(p => $"evt-{p}").OrderBy(id => id, StringComparer.Ordinal).ToList();
		var collectedIds = collected.Select(e => e.EventId).OrderBy(id => id, StringComparer.Ordinal).ToList();

		// RED pre-fix: break-on-empty stops at the first empty interior batch → only {evt-1,evt-2,evt-3} returned.
		collectedIds.SequenceEqual(expectedIds).ShouldBeTrue(
			$"gap-tolerant paging must return every event in [1,11] across the gap — expected [{string.Join(",", expectedIds)}] " +
			$"but got [{string.Join(",", collectedIds)}] (bd-778kpz — break-on-empty drops post-gap events)");
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		const string sql = $"""
			DROP TABLE IF EXISTS public.{TableName};
			CREATE TABLE public.{TableName} (
				event_id VARCHAR(255) PRIMARY KEY,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				event_type VARCHAR(255) NOT NULL,
				event_data BYTEA NOT NULL,
				metadata BYTEA,
				version BIGINT NOT NULL,
				timestamp TIMESTAMPTZ NOT NULL,
				global_position BIGINT NOT NULL UNIQUE
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task SeedAsync(IReadOnlyList<long> globalPositions)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		for (var i = 0; i < globalPositions.Count; i++)
		{
			const string insert = $"""
				INSERT INTO public.{TableName}
					(event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp, global_position)
				VALUES
					(@event_id, @aggregate_id, @aggregate_type, @event_type, @event_data, NULL, @version, TIMESTAMPTZ '2026-01-01 00:00:00+00', @global_position);
				""";

			await using var command = new NpgsqlCommand(insert, connection);
			_ = command.Parameters.AddWithValue("event_id", $"evt-{globalPositions[i]}");
			_ = command.Parameters.AddWithValue("aggregate_id", "agg-1");
			_ = command.Parameters.AddWithValue("aggregate_type", "TestAggregate");
			_ = command.Parameters.AddWithValue("event_type", "TestEvent");
			_ = command.Parameters.AddWithValue("event_data", new byte[] { 1, 2, 3 });
			_ = command.Parameters.AddWithValue("version", (long)i);
			_ = command.Parameters.AddWithValue("global_position", globalPositions[i]);
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}
}
