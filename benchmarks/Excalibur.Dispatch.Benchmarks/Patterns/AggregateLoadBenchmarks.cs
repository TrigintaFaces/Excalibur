// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for aggregate loading operations from event store.
/// Measures performance of loading aggregates with varying event counts.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Load aggregate (10 events): &lt; 20ms (P50), &lt; 40ms (P95)
/// - Load aggregate (100 events): &lt; 100ms (P50), &lt; 200ms (P95)
/// - Load aggregate (1000 events): &lt; 500ms (P50), &lt; 1000ms (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AggregateLoadBenchmarks
{
	private MsSqlContainer? _sqlContainer;
	private SqlServerEventStore? _eventStore;
	private string? _connectionString;
	private string? _aggregateWith10Events;
	private string? _aggregateWith100Events;
	private string? _aggregateWith1000Events;

	/// <summary>
	/// Initialize SQL Server container and pre-populate aggregates with events.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		// Start SQL Server container
		_sqlContainer = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.Build();

		await _sqlContainer.StartAsync();
		_connectionString = _sqlContainer.GetConnectionString();

		// Create EventStore table
		await CreateEventStoreTableAsync();

		// Initialize event store
		_eventStore = new SqlServerEventStore(_connectionString, NullLogger<SqlServerEventStore>.Instance);

		// Pre-populate aggregates with different event counts
		_aggregateWith10Events = await CreateAggregateWithEventsAsync(10);
		_aggregateWith100Events = await CreateAggregateWithEventsAsync(100);
		_aggregateWith1000Events = await CreateAggregateWithEventsAsync(1000);
	}

	/// <summary>
	/// Cleanup SQL Server container after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_sqlContainer != null)
		{
			await _sqlContainer.DisposeAsync();
		}
	}

	/// <summary>
	/// Benchmark: Load aggregate with 10 events (small aggregate).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate10Events()
	{
		return await _eventStore.LoadAsync(
			_aggregateWith10Events,
			"TestAggregate",
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 100 events (medium aggregate).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate100Events()
	{
		return await _eventStore.LoadAsync(
			_aggregateWith100Events,
			"TestAggregate",
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 1000 events (large aggregate).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate1000Events()
	{
		return await _eventStore.LoadAsync(
			_aggregateWith1000Events,
			"TestAggregate",
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate from specific version (partial load, last 50 events of 1000).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregateFromVersion950()
	{
		return await _eventStore.LoadAsync(
			_aggregateWith1000Events,
			"TestAggregate",
			fromVersion: 950,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Concurrent load of 10 different aggregates (simulates high read load).
	/// </summary>
	[Benchmark]
	public async Task ConcurrentLoad10Aggregates()
	{
		// ValueTask requires .AsTask() for Task.WhenAll (Sprint 250 ValueTask migration)
		var tasks = new List<Task<IReadOnlyList<StoredEvent>>>();

		for (int i = 0; i < 10; i++)
		{
			tasks.Add(_eventStore.LoadAsync(
				_aggregateWith100Events,
				"TestAggregate",
				CancellationToken.None).AsTask());
		}

		_ = await Task.WhenAll(tasks);
	}

	private static TestDomainEvent CreateTestEvent(string aggregateId, long version)
	{
		return new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "TestDomainEvent",
			Metadata = new Dictionary<string, object>
			{
				["UserId"] = "benchmark-user",
				["TenantId"] = "benchmark-tenant",
			},
			Data = $"Test event data for version {version}",
		};
	}

	private async Task<string> CreateAggregateWithEventsAsync(int eventCount)
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = new List<TestDomainEvent>();

		for (int i = 1; i <= eventCount; i++)
		{
			events.Add(CreateTestEvent(aggregateId, i));
		}

		_ = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);

		return aggregateId;
	}

	private async Task CreateEventStoreTableAsync()
	{
		const string createTableSql = """
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EventStoreEvents')
            BEGIN
                CREATE TABLE EventStoreEvents (
                    Position BIGINT IDENTITY(1,1) PRIMARY KEY,
                    EventId NVARCHAR(50) NOT NULL UNIQUE,
                    AggregateId NVARCHAR(50) NOT NULL,
                    AggregateType NVARCHAR(100) NOT NULL,
                    EventType NVARCHAR(200) NOT NULL,
                    EventData VARBINARY(MAX) NOT NULL,
                    Metadata VARBINARY(MAX) NULL,
                    Version BIGINT NOT NULL,
                    Timestamp DATETIMEOFFSET NOT NULL,
                    IsDispatched BIT NOT NULL DEFAULT 0,
                    INDEX IX_AggregateId_Version (AggregateId, Version),
                    INDEX IX_IsDispatched (IsDispatched)
                );
            END
            """;

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync();

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync();
	}
}
