// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Tests.Shared.Fixtures;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
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
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerEventStoreIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private readonly RequiredContainer _requiredContainer = new("SQL Server (Docker)");

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_requiredContainer.MarkStarted();

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw _requiredContainer.Failed(ex);
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
		_requiredContainer.Require();

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
		_requiredContainer.Require();

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
		_requiredContainer.Require();

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
		_requiredContainer.Require();

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
		_requiredContainer.Require();

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
		_requiredContainer.Require();

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

	private IEventStore CreateEventStore()
	{
		var logger = NullLogger<SqlServerEventStore>.Instance;
		return new SqlServerEventStore(_connectionString, logger, SingleTenantTestContext.Instance);
	}

	private async Task ClearAllEventsAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await ShippedEventStoreSchema.ResetAsync(_connectionString, CancellationToken.None).ConfigureAwait(false);
	}

	private Task InitializeDatabaseAsync() =>
		ShippedEventStoreSchema.EnsureCreatedAsync(_connectionString, CancellationToken.None);

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
