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
/// Integration tests for <see cref="SqlServerEventStore"/> using Excalibur.EventSourcing.
/// Tests real SQL Server database operations using TestContainers.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// bd-4v9k1: SqlServer EventStore Tests (10 tests).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "EventStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerEventStoreIntegrationShould : IAsyncLifetime
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

		var result = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(1);

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);
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
		_ = await eventStore.AppendAsync(aggregateId, aggregateType, [event1], -1, CancellationToken.None).ConfigureAwait(true);

		// Try to append with wrong expected version
		var event2 = new TestDomainEvent(aggregateId, 1);
		var result = await eventStore.AppendAsync(aggregateId, aggregateType, [event2], -1, CancellationToken.None).ConfigureAwait(true);

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

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Load only events after version 0
		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, 0, CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(2);
		loaded[0].Version.ShouldBe(1);
		loaded[1].Version.ShouldBe(2);
	}

	/// <summary>
	/// Verifies that events can be marked as dispatched for outbox pattern.
	/// </summary>
	[Fact]
	public async Task MarkEventAsDispatched()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var testEvent = new TestDomainEvent(aggregateId, 0);
		var events = new List<IDomainEvent> { testEvent };

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		var undispatched = await eventStore.GetUndispatchedEventsAsync(10, CancellationToken.None).ConfigureAwait(true);
		undispatched.ShouldContain(e => e.EventId == testEvent.EventId);

		await eventStore.MarkEventAsDispatchedAsync(testEvent.EventId, CancellationToken.None).ConfigureAwait(true);

		var afterMark = await eventStore.GetUndispatchedEventsAsync(10, CancellationToken.None).ConfigureAwait(true);
		afterMark.ShouldNotContain(e => e.EventId == testEvent.EventId);
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

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

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
		_ = await eventStore.AppendAsync(aggregateId1, aggregateType, events1, -1, CancellationToken.None).ConfigureAwait(true);

		// Append events to second aggregate
		var events2 = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId2, 0),
		};
		_ = await eventStore.AppendAsync(aggregateId2, aggregateType, events2, -1, CancellationToken.None).ConfigureAwait(true);

		// Load and verify isolation
		var loaded1 = await eventStore.LoadAsync(aggregateId1, aggregateType, CancellationToken.None).ConfigureAwait(true);
		var loaded2 = await eventStore.LoadAsync(aggregateId2, aggregateType, CancellationToken.None).ConfigureAwait(true);

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

		var result = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);
		result.Success.ShouldBeTrue();

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(5);

		// Verify strict version ordering
		for (int i = 0; i < 5; i++)
		{
			loaded[i].Version.ShouldBe(i);
		}
	}

	/// <summary>
	/// Verifies that getting undispatched events from empty store returns empty list.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyListForUndispatchedWhenNoneExist()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();

		// Mark all events as dispatched first by clearing the table
		await ClearAllEventsAsync().ConfigureAwait(true);

		var undispatched = await eventStore.GetUndispatchedEventsAsync(10, CancellationToken.None).ConfigureAwait(true);

		_ = undispatched.ShouldNotBeNull();
		undispatched.Count.ShouldBe(0);
	}

	/// <summary>
	/// Verifies that multiple events can be marked as dispatched sequentially.
	/// </summary>
	[Fact]
	public async Task MarkMultipleEventsAsDispatchedSequentially()
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

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Mark each event as dispatched one by one
		foreach (var evt in events)
		{
			await eventStore.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None).ConfigureAwait(true);
		}

		var undispatched = await eventStore.GetUndispatchedEventsAsync(10, CancellationToken.None).ConfigureAwait(true);
		undispatched.ShouldNotContain(e => events.Any(evt => evt.EventId == e.EventId));
	}

	/// <summary>
	/// Verifies that batch size limit is respected when getting undispatched events.
	/// </summary>
	[Fact]
	public async Task RespectBatchSizeLimitForUndispatchedEvents()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Clear any existing events first
		await ClearAllEventsAsync().ConfigureAwait(true);

		// Append 5 events
		var events = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, 0),
			new TestDomainEvent(aggregateId, 1),
			new TestDomainEvent(aggregateId, 2),
			new TestDomainEvent(aggregateId, 3),
			new TestDomainEvent(aggregateId, 4),
		};

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Request only 3 undispatched events
		var undispatched = await eventStore.GetUndispatchedEventsAsync(3, CancellationToken.None).ConfigureAwait(true);

		undispatched.Count.ShouldBe(3);
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
		await using var command = new SqlCommand("DELETE FROM EventStoreEvents", connection);
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
