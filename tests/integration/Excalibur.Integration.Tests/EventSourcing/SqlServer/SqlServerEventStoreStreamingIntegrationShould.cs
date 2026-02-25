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
/// Integration tests for <see cref="SqlServerEventStore"/> streaming and pagination operations
/// using real SQL Server via TestContainers.
/// Tests event loading with version offsets, batch pagination, and undispatched event streaming.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerEventStoreStreamingIntegrationShould : IAsyncLifetime
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
	/// Verifies that events can be loaded from a specific version, enabling incremental reads.
	/// </summary>
	[Fact]
	public async Task LoadEventsFromSpecificVersion()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var events = Enumerable.Range(0, 10)
			.Select(i => new TestDomainEvent(aggregateId, i))
			.Cast<IDomainEvent>()
			.ToList();

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Load from version 5 (exclusive) - should get versions 6, 7, 8, 9
		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, 5, CancellationToken.None).ConfigureAwait(true);

		loaded.Count.ShouldBe(4);
		loaded[0].Version.ShouldBe(6);
		loaded[3].Version.ShouldBe(9);
	}

	/// <summary>
	/// Verifies that loading from the last version returns an empty list.
	/// </summary>
	[Fact]
	public async Task ReturnEmptyWhenLoadingFromLastVersion()
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

		// Load from version 2 (the last one) - should be empty
		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, 2, CancellationToken.None).ConfigureAwait(true);

		loaded.Count.ShouldBe(0);
	}

	/// <summary>
	/// Verifies that simulated cursor-based reading works by iterating through events
	/// using fromVersion as a cursor.
	/// </summary>
	[Fact]
	public async Task SupportCursorBasedPagination()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Append 20 events
		var events = Enumerable.Range(0, 20)
			.Select(i => new TestDomainEvent(aggregateId, i))
			.Cast<IDomainEvent>()
			.ToList();

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Read all events in "pages" using fromVersion as cursor
		var allLoaded = new List<StoredEvent>();
		long cursor = -1;

		while (true)
		{
			var page = await eventStore.LoadAsync(aggregateId, aggregateType, cursor, CancellationToken.None).ConfigureAwait(true);
			if (page.Count == 0)
			{
				break;
			}

			allLoaded.AddRange(page);
			cursor = page[^1].Version;
		}

		allLoaded.Count.ShouldBe(20);

		// Verify version continuity
		for (int i = 0; i < 20; i++)
		{
			allLoaded[i].Version.ShouldBe(i);
		}
	}

	/// <summary>
	/// Verifies that undispatched events can be retrieved in batches (pagination).
	/// </summary>
	[Fact]
	public async Task PaginateThroughUndispatchedEvents()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllEventsAsync().ConfigureAwait(true);

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Append 8 events (all undispatched)
		var events = Enumerable.Range(0, 8)
			.Select(i => new TestDomainEvent(aggregateId, i))
			.Cast<IDomainEvent>()
			.ToList();

		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, CancellationToken.None).ConfigureAwait(true);

		// Retrieve in batches of 3
		var batch1 = await eventStore.GetUndispatchedEventsAsync(3, CancellationToken.None).ConfigureAwait(true);
		batch1.Count.ShouldBe(3);

		// Mark first batch as dispatched
		foreach (var evt in batch1)
		{
			await eventStore.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None).ConfigureAwait(true);
		}

		// Next batch should return next 3
		var batch2 = await eventStore.GetUndispatchedEventsAsync(3, CancellationToken.None).ConfigureAwait(true);
		batch2.Count.ShouldBe(3);

		// Mark second batch as dispatched
		foreach (var evt in batch2)
		{
			await eventStore.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None).ConfigureAwait(true);
		}

		// Final batch should return remaining 2
		var batch3 = await eventStore.GetUndispatchedEventsAsync(3, CancellationToken.None).ConfigureAwait(true);
		batch3.Count.ShouldBe(2);

		// Mark remaining as dispatched
		foreach (var evt in batch3)
		{
			await eventStore.MarkEventAsDispatchedAsync(evt.EventId, CancellationToken.None).ConfigureAwait(true);
		}

		// Should be empty now
		var batch4 = await eventStore.GetUndispatchedEventsAsync(3, CancellationToken.None).ConfigureAwait(true);
		batch4.Count.ShouldBe(0);
	}

	/// <summary>
	/// Verifies that events maintain their aggregate association through load-from-version reads.
	/// </summary>
	[Fact]
	public async Task MaintainAggregateAssociationInVersionedLoads()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// Append events to two different aggregates
		var events1 = Enumerable.Range(0, 5)
			.Select(i => new TestDomainEvent(aggregateId1, i))
			.Cast<IDomainEvent>()
			.ToList();
		var events2 = Enumerable.Range(0, 5)
			.Select(i => new TestDomainEvent(aggregateId2, i))
			.Cast<IDomainEvent>()
			.ToList();

		_ = await eventStore.AppendAsync(aggregateId1, aggregateType, events1, -1, CancellationToken.None).ConfigureAwait(true);
		_ = await eventStore.AppendAsync(aggregateId2, aggregateType, events2, -1, CancellationToken.None).ConfigureAwait(true);

		// Load from version 2 for aggregate 1 only
		var loaded = await eventStore.LoadAsync(aggregateId1, aggregateType, 2, CancellationToken.None).ConfigureAwait(true);

		loaded.Count.ShouldBe(2); // versions 3, 4
		loaded.ShouldAllBe(e => e.AggregateId == aggregateId1);
		loaded[0].Version.ShouldBe(3);
		loaded[1].Version.ShouldBe(4);
	}

	/// <summary>
	/// Verifies that incremental append followed by incremental load returns only new events.
	/// </summary>
	[Fact]
	public async Task LoadOnlyNewEventsAfterIncrementalAppend()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		// First batch: 3 events
		var batch1 = Enumerable.Range(0, 3)
			.Select(i => new TestDomainEvent(aggregateId, i))
			.Cast<IDomainEvent>()
			.ToList();
		var result1 = await eventStore.AppendAsync(aggregateId, aggregateType, batch1, -1, CancellationToken.None).ConfigureAwait(true);
		result1.Success.ShouldBeTrue();

		// Record the last version
		var lastVersion = result1.NextExpectedVersion;

		// Second batch: 2 more events
		var batch2 = Enumerable.Range(3, 2)
			.Select(i => new TestDomainEvent(aggregateId, i))
			.Cast<IDomainEvent>()
			.ToList();
		var result2 = await eventStore.AppendAsync(aggregateId, aggregateType, batch2, lastVersion, CancellationToken.None).ConfigureAwait(true);
		result2.Success.ShouldBeTrue();

		// Load only events after the first batch
		var newEvents = await eventStore.LoadAsync(aggregateId, aggregateType, lastVersion, CancellationToken.None).ConfigureAwait(true);

		newEvents.Count.ShouldBe(2);
		newEvents[0].Version.ShouldBe(3);
		newEvents[1].Version.ShouldBe(4);
	}

	/// <summary>
	/// Verifies that stored events preserve all metadata fields through storage round-trip.
	/// </summary>
	[Fact]
	public async Task PreserveStoredEventFieldsThroughRoundTrip()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var eventStore = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var originalEvent = new TestDomainEvent(aggregateId, 0);
		_ = await eventStore.AppendAsync(aggregateId, aggregateType, [originalEvent], -1, CancellationToken.None).ConfigureAwait(true);

		var loaded = await eventStore.LoadAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

		loaded.Count.ShouldBe(1);
		var storedEvent = loaded[0];

		storedEvent.EventId.ShouldBe(originalEvent.EventId);
		storedEvent.AggregateId.ShouldBe(aggregateId);
		storedEvent.AggregateType.ShouldBe(aggregateType);
		storedEvent.Version.ShouldBe(0);
		storedEvent.IsDispatched.ShouldBeFalse();
		storedEvent.EventData.ShouldNotBeNull();
		storedEvent.EventData.Length.ShouldBeGreaterThan(0);
	}

	private IEventStore CreateEventStore()
	{
		var logger = NullLogger<SqlServerEventStore>.Instance;
		return new SqlServerEventStore(_connectionString!, logger);
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
