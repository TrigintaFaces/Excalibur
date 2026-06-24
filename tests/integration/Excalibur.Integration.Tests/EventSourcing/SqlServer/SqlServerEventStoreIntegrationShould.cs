// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.SqlServer.ParallelCatchUp;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerEventStore"/> using Excalibur.EventSourcing.
/// Tests real SQL Server database operations using TestContainers.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-4v9k1: SqlServer EventStore Tests (10 tests).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerEventStoreIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_dockerAvailable = true;

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
			Console.WriteLine(ex.ToString());
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_container != null)
		{
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Container cleanup failed: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Verifies that events can be appended and loaded for an aggregate.
	/// </summary>
	[Fact]
	public async Task AppendAndLoadEventsForAggregate()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var events = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, 0),
			new TestDomainEvent(aggregateId, 1),
		};

		var result = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(1);

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(2);
		loaded[0].Version.ShouldBe(0);
		loaded[1].Version.ShouldBe(1);
	}

	/// <summary>
	/// Verifies that optimistic concurrency control detects version conflicts.
	/// </summary>
	[Fact]
	public async Task DetectConcurrencyConflict()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var event1 = new TestDomainEvent(aggregateId, 0);
		_ = await eventStore.AppendAsync(aggregateId, aggregateType, [event1], -1, CancellationToken.None);

		// Try to append with wrong expected version
		var event2 = new TestDomainEvent(aggregateId, 1);
		var result = await eventStore.AppendAsync(aggregateId, aggregateType, [event2], -1, CancellationToken.None);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
	}

	/// <summary>
	/// Verifies that events can be loaded from a specific version.
	/// </summary>
	[Fact]
	public async Task LoadEventsFromVersion()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var events = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, 0),
			new TestDomainEvent(aggregateId, 1),
			new TestDomainEvent(aggregateId, 2),
		};

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);

		// Load only events after version 0
		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, 0, CancellationToken.None);
		loaded.Count.ShouldBe(2);
		loaded[0].Version.ShouldBe(1);
		loaded[1].Version.ShouldBe(2);
	}

	/// <summary>
	/// Verifies that loading events for a non-existent aggregate returns empty list.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyListForNonExistentAggregate()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "NonExistentAggregate";

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None);

		_ = loaded.ShouldNotBeNull();
		loaded.Count.ShouldBe(0);
	}

	/// <summary>
	/// Verifies that events from different aggregates are isolated.
	/// </summary>
	[Fact]
	public async Task IsolateEventsAcrossMultipleAggregates()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Append events to first aggregate
		var events1 = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId1, 0),
			new TestDomainEvent(aggregateId1, 1),
		};
		_ = await eventStore.AppendAsync(aggregateId1, aggregateType, events1, -1, CancellationToken.None);

		// Append events to second aggregate
		var events2 = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId2, 0),
		};
		_ = await eventStore.AppendAsync(aggregateId2, aggregateType, events2, -1, CancellationToken.None);

		// Load and verify isolation
		var loaded1 = await eventStore.LoadAsync(aggregateId1, aggregateType, CancellationToken.None);
		var loaded2 = await eventStore.LoadAsync(aggregateId2, aggregateType, CancellationToken.None);

		loaded1.Count.ShouldBe(2);
		loaded2.Count.ShouldBe(1);
		loaded1.All(e => e.AggregateId == aggregateId1).ShouldBeTrue();
		loaded2.All(e => e.AggregateId == aggregateId2).ShouldBeTrue();
	}

	/// <summary>
	/// Verifies that batch append preserves event ordering within the batch.
	/// </summary>
	[Fact]
	public async Task PreserveEventOrderInBatchAppend()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Append a batch of 5 events
		var events = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, 0),
			new TestDomainEvent(aggregateId, 1),
			new TestDomainEvent(aggregateId, 2),
			new TestDomainEvent(aggregateId, 3),
			new TestDomainEvent(aggregateId, 4),
		};

		var result = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None);
		result.Success.ShouldBeTrue();

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(5);

		// Verify strict version ordering
		for (int i = 0; i < 5; i++)
		{
			loaded[i].Version.ShouldBe(i);
		}
	}

	/// <summary>
	/// bd-ao97rb (S841, ADR-336 / FR-7) — AC-9 on the real SQL schema. The parallel-catch-up range query
	/// referenced a non-existent <c>GlobalPosition</c> column (the actual global-ordinal column is
	/// <c>Position</c>), throwing at runtime on parallel catch-up — masked by an in-memory-only test that
	/// cannot catch a wrong SQL column name. This is the faithful Docker-SQL lock: drive the real
	/// <see cref="SqlServerRangeQueryEventStore.ReadRangeAsync"/> against a populated table and assert it
	/// executes without a missing-column error and returns the events in [from, to] ordered by the global
	/// <c>Position</c> ordinal. RED on the pre-fix <c>GlobalPosition</c> column (SQL throws); GREEN on the fix.
	/// </summary>
	[Fact]
	public async Task RangeQueryReturnsEventsByPosition_OnTheRealSqlSchema()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllEventsAsync();

		var eventStore = CreateEventStore();

		// Append across two aggregates; the Position IDENTITY column assigns the global ordinal 1..5.
		var aggregateA = Guid.NewGuid().ToString();
		_ = await eventStore.AppendAsync(
			aggregateA, "TestAggregate",
			[new TestDomainEvent(aggregateA, 0), new TestDomainEvent(aggregateA, 1)],
			-1, CancellationToken.None);

		var aggregateB = Guid.NewGuid().ToString();
		_ = await eventStore.AppendAsync(
			aggregateB, "TestAggregate",
			[new TestDomainEvent(aggregateB, 0), new TestDomainEvent(aggregateB, 1), new TestDomainEvent(aggregateB, 2)],
			-1, CancellationToken.None);

		var rangeStore = new SqlServerRangeQueryEventStore(
			() => new SqlConnection(_connectionString),
			"dbo",
			"EventStoreEvents",
			NullLogger<SqlServerRangeQueryEventStore>.Instance);

		// Act — read the middle of the global stream [2, 4].
		var collected = new List<StoredEvent>();
		await foreach (var stored in rangeStore.ReadRangeAsync(2, 4, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false))
		{
			collected.Add(stored);
		}

		// Assert — executed against the real Position column (no missing-column SqlException) and returned the
		// three events in [2, 4] ordered by the global ordinal, carried onto StoredEvent.GlobalPosition.
		collected.Count.ShouldBe(3);
		collected.Select(e => e.GlobalPosition).ShouldBe(new long[] { 2, 3, 4 });

		// EC-8 — an empty range (well beyond any assigned ordinal) returns nothing without error.
		var empty = new List<StoredEvent>();
		await foreach (var stored in rangeStore.ReadRangeAsync(1_000_000, 1_000_100, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false))
		{
			empty.Add(stored);
		}

		empty.ShouldBeEmpty();
	}

	/// <summary>
	/// bd-778kpz (S842, ADR-336 Wave 2 / FR-1, FR-1a) — KEYSTONE gap-tolerant range paging on the real SQL schema.
	/// Both range stores used to <c>break</c> on the first empty batch. A gap in the <c>Position</c> IDENTITY
	/// sequence (reseed / identity-cache jump / sequence skip) narrower than <c>[from,to]</c> would stop paging
	/// early, so a parallel catch-up poller silently stalls and misses every event after the gap. This lock drives
	/// the real <see cref="SqlServerRangeQueryEventStore.ReadRangeAsync"/> with a <c>batchSize</c> smaller than a
	/// manufactured gap and asserts that ALL events in <c>[from,to]</c> — including those after the gap — are
	/// returned. RED on break-on-empty (events after the gap are dropped); GREEN on the gap-tolerant fix.
	/// Multiple gap shapes (mid-range, at-tail, batch-aligned, multiple gaps) are exercised against one container.
	/// </summary>
	[Fact]
	public async Task GapTolerantPaging_ReturnsEventsAfterAPositionGap_OnTheRealSqlSchema()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		// Shape 1 — single mid-range gap. Append 3 events, then a reseed jump, then 2 more. The exact IDENTITY
		// start is provider-dependent (fresh-table RESEED quirk), so the read range and expectations are derived
		// from the ACTUAL seeded positions; what matters is the manufactured gap (here ~7 wide) exceeds batchSize.
		await AssertGapTolerantAsync(
			seededReseeds: [(count: 3, nextPositionAfter: 9)],
			tailCount: 2,
			batchSize: 2,
			because: "a mid-range Position gap must not stop paging — events after the gap must still be returned");

		// Shape 2 — a large gap before a single far tail event.
		await AssertGapTolerantAsync(
			seededReseeds: [(count: 3, nextPositionAfter: 19)],
			tailCount: 1,
			batchSize: 3,
			because: "a large gap before the final event must not truncate the range at the gap");

		// Shape 3 — multiple separate gaps, each wider than batchSize.
		await AssertGapTolerantAsync(
			seededReseeds: [(count: 2, nextPositionAfter: 6), (count: 1, nextPositionAfter: 14)],
			tailCount: 2,
			batchSize: 2,
			because: "two separate gaps must each be skipped, not break enumeration at the first one");
	}

	/// <summary>
	/// Seeds a deterministic <c>Position</c>-gapped event table, then reads the full <c>[min,max]</c> span via the
	/// real range store and asserts EVERY seeded event is returned. Gaps are created by reseeding the IDENTITY
	/// between appends so the global ordinal jumps, leaving holes wider than <c>batchSize</c> (so an interior batch
	/// is empty — the exact condition that break-on-empty paging would stop on). Read bounds and expectations are
	/// derived from the actual seeded positions so the lock is robust to the provider's IDENTITY seed offset while
	/// still proving gap tolerance.
	/// </summary>
	private async Task AssertGapTolerantAsync(
		(int count, long nextPositionAfter)[] seededReseeds,
		int tailCount,
		int batchSize,
		string because)
	{
		await ClearAllEventsAsync();

		var eventStore = CreateEventStore();

		// Each segment: append `count` events (consuming the next IDENTITY values), then reseed so the NEXT event
		// jumps to `nextPositionAfter + 1`, manufacturing a gap.
		foreach (var (count, nextPositionAfter) in seededReseeds)
		{
			await AppendEventsAsync(eventStore, count);
			await ReseedIdentityAsync(nextPositionAfter);
		}

		// Trailing segment after the last gap.
		await AppendEventsAsync(eventStore, tailCount);

		var seededPositions = new List<long>();
		await using (var diag = new SqlConnection(_connectionString))
		{
			await diag.OpenAsync().ConfigureAwait(false);
			await using var diagCmd = new SqlCommand("SELECT Position FROM EventStoreEvents ORDER BY Position", diag);
			await using var reader = await diagCmd.ExecuteReaderAsync().ConfigureAwait(false);
			while (await reader.ReadAsync().ConfigureAwait(false))
			{
				seededPositions.Add(reader.GetInt64(0));
			}
		}

		var seededStr = string.Join(",", seededPositions);

		// Vacuity guard: the seeding MUST contain a gap wider than batchSize, otherwise no interior batch is empty
		// and break-on-empty would never trigger — the lock would be vacuously green on the broken code.
		var maxGap = 0L;
		for (var i = 1; i < seededPositions.Count; i++)
		{
			maxGap = Math.Max(maxGap, seededPositions[i] - seededPositions[i - 1] - 1);
		}

		maxGap.ShouldBeGreaterThan(
			batchSize,
			$"non-vacuity: seeded positions [{seededStr}] must contain a gap wider than batchSize {batchSize} so an interior batch is empty");

		var from = seededPositions[0];
		var to = seededPositions[^1];

		var rangeStore = new SqlServerRangeQueryEventStore(
			() => new SqlConnection(_connectionString),
			"dbo",
			"EventStoreEvents",
			NullLogger<SqlServerRangeQueryEventStore>.Instance);

		var collected = new List<StoredEvent>();
		await foreach (var stored in rangeStore.ReadRangeAsync(from, to, batchSize, CancellationToken.None)
			.ConfigureAwait(false))
		{
			collected.Add(stored);
		}

		// The gap-tolerant store must return EVERY seeded event in [min,max]; break-on-empty drops everything after
		// the first interior gap.
		var collectedStr = string.Join(",", collected.Select(e => e.GlobalPosition));
		collected.Select(e => e.GlobalPosition).SequenceEqual(seededPositions).ShouldBeTrue(
			$"{because} | seeded=[{seededStr}] collected=[{collectedStr}]");
	}

	private static async Task AppendEventsAsync(IEventStore eventStore, int count)
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent>(count);
		for (var version = 0; version < count; version++)
		{
			events.Add(new TestDomainEvent(aggregateId, version));
		}

		_ = await eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None);
	}

	[SuppressMessage("Security", "CA2100",
		Justification = "nextPositionAfter is a long literal supplied by the test, not user input; DBCC CHECKIDENT RESEED takes no parameter.")]
	private async Task ReseedIdentityAsync(long nextPositionAfter)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		// RESEED to `nextPositionAfter` so the next inserted row gets Position = nextPositionAfter + 1,
		// leaving a deterministic hole in the global ordinal.
		await using var command = new SqlCommand(
			$"DBCC CHECKIDENT ('EventStoreEvents', RESEED, {nextPositionAfter});", connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private IEventStore CreateEventStore()
	{
		var logger = NullLogger<SqlServerEventStore>.Instance;
		return new SqlServerEventStore(_connectionString, logger);
	}

	private async Task ClearAllEventsAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		// Reseed the Position IDENTITY so the global ordinal restarts at 1 — the range-query locks assert
		// absolute Position values, which must be deterministic regardless of test execution order (the
		// container is shared per-class and DELETE alone does not reset IDENTITY).
		await using var command = new SqlCommand(
			"DELETE FROM EventStoreEvents; DBCC CHECKIDENT ('EventStoreEvents', RESEED, 0);", connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task InitializeDatabaseAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventStoreEvents' AND xtype='U')
			CREATE TABLE EventStoreEvents (
				Position BIGINT IDENTITY(1,1) PRIMARY KEY,
				EventId NVARCHAR(255) NOT NULL UNIQUE,
				AggregateId NVARCHAR(255) NOT NULL,
				AggregateType NVARCHAR(255) NOT NULL,
				EventType NVARCHAR(500) NOT NULL,
				EventData VARBINARY(MAX) NOT NULL,
				Metadata VARBINARY(MAX) NULL,
				Version BIGINT NOT NULL,
				Timestamp DATETIMEOFFSET NOT NULL,
				INDEX IX_EventStoreEvents_Aggregate (AggregateId, AggregateType, Version)
			)
			""";

		Console.WriteLine($"Connection string: {_connectionString}");

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		Console.WriteLine("Database connection opened successfully");

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		Console.WriteLine("EventStoreEvents table created successfully");
	}

	private sealed record TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, long version)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = version;
			OccurredAt = DateTimeOffset.UtcNow;
			EventType = nameof(TestDomainEvent);
		}

		public string EventId { get; init; }
		public string AggregateId { get; init; }
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public string EventType { get; init; }
		public IDictionary<string, object>? Metadata => null;
	}
}
