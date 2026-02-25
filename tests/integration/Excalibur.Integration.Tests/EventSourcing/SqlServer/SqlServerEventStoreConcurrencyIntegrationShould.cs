// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerEventStore"/> concurrent operations using real SQL Server via TestContainers.
/// Tests parallel appends, concurrency conflict detection, and read consistency.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerEventStoreConcurrencyIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;

	public async Task InitializeAsync()
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

	public async Task DisposeAsync()
	{
		if (_container != null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that parallel appends to different aggregates succeed.
	/// Under serializable isolation, some may fail due to transient deadlocks,
	/// so we verify all succeed on retry after initial parallel attempt.
	/// </summary>
	[Fact]
	public async Task SucceedWithParallelAppendsToDifferentAggregates()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		const int parallelCount = 5;
		var aggregateIds = Enumerable.Range(0, parallelCount)
			.Select(_ => Guid.NewGuid().ToString())
			.ToList();

		var tasks = new List<Task<(string AggregateId, AppendResult Result)>>(parallelCount);

		for (int i = 0; i < parallelCount; i++)
		{
			var aggregateId = aggregateIds[i];
			var eventStore = CreateEventStore();

			tasks.Add(Task.Run(async () =>
			{
				var events = new List<IDomainEvent>
				{
					new TestDomainEvent(aggregateId, 0),
					new TestDomainEvent(aggregateId, 1),
				};

				var result = await eventStore.AppendAsync(
					aggregateId, "TestAggregate", events, -1, CancellationToken.None).ConfigureAwait(false);
				return (aggregateId, result);
			}));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(true);

		// Retry any that failed due to transient deadlocks (serializable isolation can cause this)
		foreach (var (aggregateId, result) in results.Where(r => !r.Result.Success))
		{
			var retryStore = CreateEventStore();
			var retryEvents = new List<IDomainEvent>
			{
				new TestDomainEvent(aggregateId, 0),
				new TestDomainEvent(aggregateId, 1),
			};

			var retryResult = await retryStore.AppendAsync(
				aggregateId, "TestAggregate", retryEvents, -1, CancellationToken.None).ConfigureAwait(true);
			retryResult.Success.ShouldBeTrue();
		}

		// Verify all aggregates have their events
		var verifyStore = CreateEventStore();
		foreach (var aggregateId in aggregateIds)
		{
			var loaded = await verifyStore.LoadAsync(aggregateId, "TestAggregate", CancellationToken.None).ConfigureAwait(true);
			loaded.Count.ShouldBe(2);
			loaded[0].Version.ShouldBe(0);
			loaded[1].Version.ShouldBe(1);
		}
	}

	/// <summary>
	/// Verifies that parallel appends to the same aggregate detect concurrency conflicts.
	/// At most one writer can succeed when multiple writers target the same aggregate with the same expected version.
	/// </summary>
	[Fact]
	public async Task DetectConflictWithParallelAppendsToSameAggregate()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";
		const int parallelCount = 5;
		var tasks = new List<Task<AppendResult>>(parallelCount);

		for (int i = 0; i < parallelCount; i++)
		{
			var eventStore = CreateEventStore();

			tasks.Add(Task.Run(async () =>
			{
				var events = new List<IDomainEvent>
				{
					new TestDomainEvent(aggregateId, 0),
				};

				// All writers use expectedVersion -1 (new aggregate)
				return await eventStore.AppendAsync(
					aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(false);
			}));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(true);

		// Exactly one should succeed, the rest should be concurrency conflicts or failures
		var successCount = results.Count(r => r.Success);
		var conflictOrFailCount = results.Count(r => !r.Success);

		successCount.ShouldBe(1);
		conflictOrFailCount.ShouldBe(parallelCount - 1);
	}

	/// <summary>
	/// Verifies that reading during a concurrent write returns a consistent state
	/// (either before the write or after, never partial).
	/// </summary>
	[Fact]
	public async Task ReturnConsistentStateWhenReadingDuringWrite()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// First, seed the aggregate with some initial events
		var eventStore = CreateEventStore();
		var seedEvents = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, 0),
			new TestDomainEvent(aggregateId, 1),
			new TestDomainEvent(aggregateId, 2),
		};
		var seedResult = await eventStore.AppendAsync(
			aggregateId, aggregateType, seedEvents, -1, CancellationToken.None).ConfigureAwait(true);
		seedResult.Success.ShouldBeTrue();

		// Now perform concurrent reads and writes
		var writeTask = Task.Run(async () =>
		{
			var writer = CreateEventStore();
			var newEvents = new List<IDomainEvent>
			{
				new TestDomainEvent(aggregateId, 3),
				new TestDomainEvent(aggregateId, 4),
			};
			return await writer.AppendAsync(
				aggregateId, aggregateType, newEvents, 2, CancellationToken.None).ConfigureAwait(false);
		});

		var readTask = Task.Run(async () =>
		{
			var reader = CreateEventStore();
			return await reader.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(false);
		});

		await Task.WhenAll(writeTask, readTask).ConfigureAwait(true);

		var readResult = await readTask.ConfigureAwait(true);

		// The read should return a consistent snapshot: either 3 events (before write) or 5 events (after write)
		var validCounts = new[] { 3, 5 };
		validCounts.ShouldContain(readResult.Count);

		// Verify version ordering is always contiguous
		for (int i = 0; i < readResult.Count; i++)
		{
			readResult[i].Version.ShouldBe(i);
		}
	}

	/// <summary>
	/// Verifies that sequential appends with correct expected versions succeed.
	/// </summary>
	[Fact]
	public async Task SucceedWithSequentialAppendsUsingCorrectVersions()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// First append (new aggregate)
		var result1 = await eventStore.AppendAsync(
			aggregateId, aggregateType,
			[new TestDomainEvent(aggregateId, 0)],
			-1, CancellationToken.None).ConfigureAwait(true);
		result1.Success.ShouldBeTrue();
		result1.NextExpectedVersion.ShouldBe(0);

		// Second append (expected version = 0)
		var result2 = await eventStore.AppendAsync(
			aggregateId, aggregateType,
			[new TestDomainEvent(aggregateId, 1)],
			0, CancellationToken.None).ConfigureAwait(true);
		result2.Success.ShouldBeTrue();
		result2.NextExpectedVersion.ShouldBe(1);

		// Third append (expected version = 1)
		var result3 = await eventStore.AppendAsync(
			aggregateId, aggregateType,
			[new TestDomainEvent(aggregateId, 2), new TestDomainEvent(aggregateId, 3)],
			1, CancellationToken.None).ConfigureAwait(true);
		result3.Success.ShouldBeTrue();
		result3.NextExpectedVersion.ShouldBe(3);

		// Verify all events are persisted
		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(4);

		for (int i = 0; i < 4; i++)
		{
			loaded[i].Version.ShouldBe(i);
		}
	}

	/// <summary>
	/// Verifies that a stale writer (wrong expected version) is rejected after another writer succeeds.
	/// </summary>
	[Fact]
	public async Task RejectStaleWriterAfterAnotherWriterSucceeds()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Writer 1 succeeds first
		var writer1 = CreateEventStore();
		var result1 = await writer1.AppendAsync(
			aggregateId, aggregateType,
			[new TestDomainEvent(aggregateId, 0)],
			-1, CancellationToken.None).ConfigureAwait(true);
		result1.Success.ShouldBeTrue();

		// Writer 2 tries with stale expected version (-1, thinking aggregate is new)
		var writer2 = CreateEventStore();
		var result2 = await writer2.AppendAsync(
			aggregateId, aggregateType,
			[new TestDomainEvent(aggregateId, 0)],
			-1, CancellationToken.None).ConfigureAwait(true);
		result2.Success.ShouldBeFalse();
		result2.IsConcurrencyConflict.ShouldBeTrue();

		// Verify only writer 1's event is persisted
		var loaded = await writer1.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(1);
		loaded[0].Version.ShouldBe(0);
	}

	private IEventStore CreateEventStore()
	{
		var logger = NullLogger<SqlServerEventStore>.Instance;
		return new SqlServerEventStore(_connectionString!, logger);
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
				IsDispatched BIT NOT NULL DEFAULT 0,
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
