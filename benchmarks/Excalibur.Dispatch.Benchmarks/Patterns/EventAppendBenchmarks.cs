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
/// Benchmarks for event append operations in SqlServerEventStore.
/// Measures performance of single, batch, and concurrent event append scenarios.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single event append: &lt; 10ms (P50), &lt; 20ms (P95)
/// - Batch append (100 events): &lt; 100ms (P50), &lt; 200ms (P95)
/// - Concurrent append: no degradation with up to 10 concurrent aggregates
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class EventAppendBenchmarks
{
	private readonly List<TestDomainEvent> _singleEventBatch = new();
	private readonly List<TestDomainEvent> _tenEventBatch = new();
	private readonly List<TestDomainEvent> _hundredEventBatch = new();
	private readonly List<TestDomainEvent> _thousandEventBatch = new();
	private MsSqlContainer? _sqlContainer;
	private SqlServerEventStore? _eventStore;
	private string? _connectionString;

	/// <summary>
	/// Initialize SQL Server container and event store before benchmarks.
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

		// Pre-generate event batches for benchmarks
		_singleEventBatch.Add(CreateTestEvent(Guid.NewGuid().ToString(), 1));

		for (int i = 0; i < 10; i++)
		{
			_tenEventBatch.Add(CreateTestEvent(Guid.NewGuid().ToString(), i + 1));
		}

		for (int i = 0; i < 100; i++)
		{
			_hundredEventBatch.Add(CreateTestEvent(Guid.NewGuid().ToString(), i + 1));
		}

		for (int i = 0; i < 1000; i++)
		{
			_thousandEventBatch.Add(CreateTestEvent(Guid.NewGuid().ToString(), i + 1));
		}
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
	/// Benchmark: Single event append to new aggregate.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<AppendResult> AppendSingleEvent()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var @event = CreateTestEvent(aggregateId, 1);

		return await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { @event },
			expectedVersion: -1,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Batch append of 10 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendTenEvents()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = _tenEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		return await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Batch append of 100 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendHundredEvents()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = _hundredEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		return await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Batch append of 1000 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendThousandEvents()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = _thousandEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		return await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Concurrent append to 10 different aggregates (simulates high load).
	/// </summary>
	[Benchmark]
	public async Task ConcurrentAppendTenAggregates()
	{
		// ValueTask requires .AsTask() for Task.WhenAll (Sprint 250 ValueTask migration)
		var tasks = new List<Task<AppendResult>>();

		for (int i = 0; i < 10; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			var @event = CreateTestEvent(aggregateId, 1);

			tasks.Add(_eventStore.AppendAsync(
				aggregateId,
				"TestAggregate",
				new[] { @event },
				expectedVersion: -1,
				CancellationToken.None).AsTask());
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmark: Append event to existing aggregate (optimistic concurrency check).
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendToExistingAggregate()
	{
		// Pre-create aggregate with 5 events
		var aggregateId = Guid.NewGuid().ToString();
		_ = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			Enumerable.Range(1, 5).Select(v => CreateTestEvent(aggregateId, v)),
			expectedVersion: -1,
			CancellationToken.None);

		// Benchmark: append 6th event
		var @event = CreateTestEvent(aggregateId, 6);
		return await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { @event },
			expectedVersion: 5,
			CancellationToken.None);
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
				["TenantId"] = "benchmark-tenant"
			},
			Data = $"Test event data for version {version}"
		};
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
