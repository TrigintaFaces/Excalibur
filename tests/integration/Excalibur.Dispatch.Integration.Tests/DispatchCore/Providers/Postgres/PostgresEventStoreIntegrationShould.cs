// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Postgres;

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
/// database using TestContainers. Tests cover append, load, and concurrency.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "EventStore")]
[Trait("Provider", "Postgres")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
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
		await InitializeEventTableAsync();
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<IDomainEvent>
		{
			CreateTestEvent(aggregateId, 1),
			CreateTestEvent(aggregateId, 2)
		};

		// Act - expectedVersion is -1 for new aggregates (no events yet)
		var appendResult = await store.AppendAsync(aggregateId, TestAggregateType, events, -1, TestCancellationToken);
		var loadedEvents = await store.LoadAsync(aggregateId, TestAggregateType, TestCancellationToken);

		// Assert
		appendResult.ErrorMessage.ShouldBeNull($"Append failed: {appendResult.ErrorMessage}");
		appendResult.Success.ShouldBeTrue();
		appendResult.NextExpectedVersion.ShouldBeGreaterThanOrEqualTo(0);
		loadedEvents.Count.ShouldBe(2);
	}

	/// <summary>
	/// Tests that concurrency conflicts are detected.
	/// </summary>
	[Fact]
	public async Task DetectConcurrencyConflict()
	{
		// Arrange
		await InitializeEventTableAsync();
		var store = CreateEventStore();
		var aggregateId = Guid.NewGuid().ToString();
		var events1 = new List<IDomainEvent> { CreateTestEvent(aggregateId, 1) };
		var events2 = new List<IDomainEvent> { CreateTestEvent(aggregateId, 1) };

		// First append should succeed (expectedVersion = -1 for new aggregate)
		var result1 = await store.AppendAsync(aggregateId, TestAggregateType, events1, -1, TestCancellationToken);
		result1.Success.ShouldBeTrue();

		// Act - Second append with wrong expected version (-1) should fail since we now have version 0+
		var result2 = await store.AppendAsync(aggregateId, TestAggregateType, events2, -1, TestCancellationToken);

		// Assert
		result2.Success.ShouldBeFalse();
		result2.IsConcurrencyConflict.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that events can be loaded from a specific version.
	/// </summary>
	[Fact]
	public async Task LoadEventsFromVersion()
	{
		// Arrange
		await InitializeEventTableAsync();
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

		_ = await store.AppendAsync(aggregateId, TestAggregateType, events, -1, TestCancellationToken);

		// Act - Load from version 2 (exclusive, meaning get versions > 2)
		var loadedEvents = await store.LoadAsync(aggregateId, TestAggregateType, 2, TestCancellationToken);

		// Assert - Should return a subset of the 5 original events
		loadedEvents.Count.ShouldBeGreaterThan(0);
		loadedEvents.Count.ShouldBeLessThan(5);
	}

	/// <summary>
	/// Tests that events are isolated across aggregates.
	/// </summary>
	[Fact]
	public async Task IsolateEventsAcrossAggregates()
	{
		// Arrange
		await InitializeEventTableAsync();
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
		_ = await store.AppendAsync(aggregateId1, TestAggregateType, events1, -1, TestCancellationToken);
		_ = await store.AppendAsync(aggregateId2, TestAggregateType, events2, -1, TestCancellationToken);

		var loaded1 = await store.LoadAsync(aggregateId1, TestAggregateType, TestCancellationToken);
		var loaded2 = await store.LoadAsync(aggregateId2, TestAggregateType, TestCancellationToken);

		// Assert
		loaded1.Count.ShouldBe(2);
		loaded2.Count.ShouldBe(3);
		loaded1.ShouldAllBe(e => e.AggregateId == aggregateId1);
		loaded2.ShouldAllBe(e => e.AggregateId == aggregateId2);
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
		var logger = NullLogger<PostgresEventStore>.Instance;
		return new PostgresEventStore(_pgFixture.ConnectionString, logger);
	}

	private async Task InitializeEventTableAsync()
	{
		const string createTableSql = """
			CREATE TABLE IF NOT EXISTS public.events (
			    position BIGSERIAL PRIMARY KEY,
			    event_id VARCHAR(255) NOT NULL UNIQUE,
			    aggregate_id VARCHAR(255) NOT NULL,
			    aggregate_type VARCHAR(255) NOT NULL,
			    event_type VARCHAR(255) NOT NULL,
			    event_data BYTEA NOT NULL,
			    metadata BYTEA,
			    version BIGINT NOT NULL,
			    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
			    CONSTRAINT uq_aggregate_version UNIQUE (aggregate_id, aggregate_type, version)
			);

			CREATE INDEX IF NOT EXISTS idx_events_aggregate ON public.events (aggregate_id, aggregate_type, version);
			""";

		await using var connection = new NpgsqlConnection(_pgFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(createTableSql);

		// Clean up any existing data for test isolation
		_ = await connection.ExecuteAsync("TRUNCATE TABLE public.events RESTART IDENTITY CASCADE;");
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
