// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Postgres.EventSourcing;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Integration tests for <see cref="PostgresEventStore"/> using TestContainers.
/// Tests real Postgres database operations for event sourcing.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 177 - Provider Testing Epic Phase 3.
/// bd-pdikd: Postgres EventStore Tests (5 tests).
/// </para>
/// <para>
/// These tests verify the PostgresEventStore implementation against a real Postgres
/// database using TestContainers. Tests cover append, load, concurrency, and dispatch marking.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "EventStore")]
[Trait("Provider", "Postgres")]
public sealed class PostgresEventStoreIntegrationShould : IntegrationTestBase
{
	private const string TestAggregateType = "TestAggregate";
	private readonly PostgresFixture _pgFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreIntegrationShould"/> class.
	/// </summary>
	/// <param name="pgFixture">The Postgres container fixture.</param>
	public PostgresEventStoreIntegrationShould(PostgresFixture pgFixture)
	{
		_pgFixture = pgFixture;
	}

	/// <summary>
	/// Tests that events can be appended and loaded.
	/// </summary>
	[Fact]
	public async Task AppendAndLoadEvents()
	{
		// Arrange
		await InitializeEventTableAsync().ConfigureAwait(true);
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId, 1),
			CreateTestEvent(aggregateId, 2)
		};

		// Act - expectedVersion is -1 for new aggregates (no events yet)
		var appendResult = await store.AppendAsync(aggregateId, TestAggregateType, events, -1, TestCancellationToken).ConfigureAwait(true);
		var loadedEvents = await store.LoadAsync(aggregateId, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		appendResult.ErrorMessage.ShouldBeNull($"Append failed: {appendResult.ErrorMessage}");
		appendResult.Success.ShouldBeTrue();
		// Store uses 0-based versioning: -1 -> increment to 0, then to 1; returns last version written
		appendResult.NextExpectedVersion.ShouldBe(1);
		loadedEvents.Count.ShouldBe(2);
		// Stored versions are 0-indexed: 0, 1
		loadedEvents[0].Version.ShouldBe(0);
		loadedEvents[1].Version.ShouldBe(1);
	}

	/// <summary>
	/// Tests that concurrency conflicts are detected.
	/// </summary>
	[Fact]
	public async Task DetectConcurrencyConflict()
	{
		// Arrange
		await InitializeEventTableAsync().ConfigureAwait(true);
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events1 = new List<IDomainEvent> { CreateTestEvent(aggregateId, 1) };
		var events2 = new List<IDomainEvent> { CreateTestEvent(aggregateId, 1) };

		// First append should succeed (expectedVersion = -1 for new aggregate)
		var result1 = await store.AppendAsync(aggregateId, TestAggregateType, events1, -1, TestCancellationToken).ConfigureAwait(true);
		result1.Success.ShouldBeTrue();

		// Act - Second append with wrong expected version (-1) should fail since we now have version 1
		var result2 = await store.AppendAsync(aggregateId, TestAggregateType, events2, -1, TestCancellationToken).ConfigureAwait(true);

		// Assert
		result2.Success.ShouldBeFalse();
		result2.IsConcurrencyConflict.ShouldBeTrue();
		// After first append, aggregate version is 0 (0-indexed). On conflict, actual version (0) is returned.
		result2.NextExpectedVersion.ShouldBe(0);
	}

	/// <summary>
	/// Tests that events can be loaded from a specific version.
	/// </summary>
	[Fact]
	public async Task LoadEventsFromVersion()
	{
		// Arrange
		await InitializeEventTableAsync().ConfigureAwait(true);
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId, 1),
			CreateTestEvent(aggregateId, 2),
			CreateTestEvent(aggregateId, 3),
			CreateTestEvent(aggregateId, 4),
			CreateTestEvent(aggregateId, 5)
		};

		_ = await store.AppendAsync(aggregateId, TestAggregateType, events, -1, TestCancellationToken).ConfigureAwait(true);

		// Act - Load from version 2 (exclusive, meaning get versions > 2)
		// Events are stored at versions 0, 1, 2, 3, 4
		var loadedEvents = await store.LoadAsync(aggregateId, TestAggregateType, 2, TestCancellationToken).ConfigureAwait(true);

		// Assert - Should only have events at versions 3 and 4 (fromVersion is exclusive)
		loadedEvents.Count.ShouldBe(2);
		loadedEvents[0].Version.ShouldBe(3);
		loadedEvents[1].Version.ShouldBe(4);
	}

	/// <summary>
	/// Tests that events can be marked as dispatched.
	/// </summary>
	[Fact]
	public async Task MarkEventAsDispatched()
	{
		// Arrange
		await InitializeEventTableAsync().ConfigureAwait(true);
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var testEvent = CreateTestEvent(aggregateId, 1);
		var events = new List<IDomainEvent> { testEvent };

		_ = await store.AppendAsync(aggregateId, TestAggregateType, events, -1, TestCancellationToken).ConfigureAwait(true);

		// Act
		await store.MarkEventAsDispatchedAsync(testEvent.EventId, TestCancellationToken).ConfigureAwait(true);

		// Assert - Undispatched query should return empty
		var undispatched = await store.GetUndispatchedEventsAsync(10, TestCancellationToken).ConfigureAwait(true);
		undispatched.ShouldBeEmpty();
	}

	/// <summary>
	/// Tests that events are isolated across aggregates.
	/// </summary>
	[Fact]
	public async Task IsolateEventsAcrossAggregates()
	{
		// Arrange
		await InitializeEventTableAsync().ConfigureAwait(true);
		var store = CreateEventStore();
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();

		var events1 = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId1, 1),
			CreateTestEvent(aggregateId1, 2)
		};
		var events2 = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId2, 1),
			CreateTestEvent(aggregateId2, 2),
			CreateTestEvent(aggregateId2, 3)
		};

		// Act
		_ = await store.AppendAsync(aggregateId1, TestAggregateType, events1, -1, TestCancellationToken).ConfigureAwait(true);
		_ = await store.AppendAsync(aggregateId2, TestAggregateType, events2, -1, TestCancellationToken).ConfigureAwait(true);

		var loaded1 = await store.LoadAsync(aggregateId1, TestAggregateType, TestCancellationToken).ConfigureAwait(true);
		var loaded2 = await store.LoadAsync(aggregateId2, TestAggregateType, TestCancellationToken).ConfigureAwait(true);

		// Assert
		loaded1.Count.ShouldBe(2);
		loaded2.Count.ShouldBe(3);
		loaded1.All(e => e.AggregateId == aggregateId1).ShouldBeTrue();
		loaded2.All(e => e.AggregateId == aggregateId2).ShouldBeTrue();
	}

	private static TestDomainEvent CreateTestEvent(string aggregateId, int sequence)
	{
		return new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			EventType = "TestEvent",
			OccurredAt = DateTimeOffset.UtcNow,
			Version = sequence,
			Sequence = sequence,
			Data = $"test-data-{sequence}"
		};
	}

	private IEventStore CreateEventStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresEventStoreOptions
		{
			SchemaName = "public",
			EventsTableName = "event_store_events"
		});
		var logger = NullLogger<PostgresEventStore>.Instance;
		return new PostgresEventStore(_pgFixture.ConnectionString, options, logger);
	}

	private async Task InitializeEventTableAsync()
	{
		const string createTableSql = """
			CREATE TABLE IF NOT EXISTS public.event_store_events (
			    global_sequence BIGSERIAL PRIMARY KEY,
			    event_id VARCHAR(100) NOT NULL UNIQUE,
			    aggregate_id VARCHAR(100) NOT NULL,
			    aggregate_type VARCHAR(500) NOT NULL,
			    event_type VARCHAR(500) NOT NULL,
			    event_data BYTEA NOT NULL,
			    metadata BYTEA,
			    version BIGINT NOT NULL,
			    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
			    is_dispatched BOOLEAN NOT NULL DEFAULT FALSE,
			    CONSTRAINT uq_aggregate_version UNIQUE (aggregate_id, aggregate_type, version)
			);

			CREATE INDEX IF NOT EXISTS idx_events_aggregate ON public.event_store_events (aggregate_id, aggregate_type, version);
			CREATE INDEX IF NOT EXISTS idx_events_undispatched ON public.event_store_events (is_dispatched) WHERE is_dispatched = FALSE;
			""";

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTableSql).ConfigureAwait(true);

		// Clean up any existing data for test isolation
		_ = await connection.ExecuteAsync("TRUNCATE TABLE public.event_store_events RESTART IDENTITY CASCADE;").ConfigureAwait(true);
	}

	/// <summary>
	/// Test domain event for integration testing.
	/// </summary>
	private sealed class TestDomainEvent : IDomainEvent
	{
		public required string EventId { get; init; }
		public required string AggregateId { get; init; }
		public required string EventType { get; init; }
		public required DateTimeOffset OccurredAt { get; init; }
		public required long Version { get; init; }
		public required int Sequence { get; init; }
		public required string Data { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
