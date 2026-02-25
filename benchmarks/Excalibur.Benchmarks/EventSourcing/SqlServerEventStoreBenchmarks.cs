// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// Benchmarks for SqlServerEventStore operations.
/// </summary>
/// <remarks>
/// Requires a SQL Server instance. Set the BENCHMARK_SQL_CONNECTIONSTRING environment variable
/// to enable these benchmarks. When not set, all benchmarks return immediately.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class SqlServerEventStoreBenchmarks
{
	private static readonly string? ConnectionString =
		Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");

	private SqlServerEventStore? _eventStore;
	private SqlServerSnapshotStore? _snapshotStore;
	private string _aggregateWith5Events = null!;
	private string _aggregateWith50Events = null!;
	private string _aggregateWith500Events = null!;
	private string _iterationAggregateId = null!;

	private static bool IsAvailable => ConnectionString is not null;

	[GlobalSetup]
	public void GlobalSetup()
	{
		if (!IsAvailable)
		{
			return;
		}

		_eventStore = new SqlServerEventStore(
			ConnectionString!,
			NullLogger<SqlServerEventStore>.Instance);

		_snapshotStore = new SqlServerSnapshotStore(
			ConnectionString!,
			NullLogger<SqlServerSnapshotStore>.Instance);

		// Ensure schema exists
		EnsureSchemaAsync().GetAwaiter().GetResult();

		// Pre-populate aggregates
		_aggregateWith5Events = CreateAggregateWithEvents(5);
		_aggregateWith50Events = CreateAggregateWithEvents(50);
		_aggregateWith500Events = CreateAggregateWithEvents(500);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_iterationAggregateId = Guid.NewGuid().ToString();
	}

	#region Append Benchmarks

	/// <summary>
	/// Benchmark: Append single event to a new aggregate.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<AppendResult?> AppendSingleEvent()
	{
		if (!IsAvailable)
		{
			return null;
		}

		var events = CreateEvents(_iterationAggregateId, 1);
		return await _eventStore!.AppendAsync(
			_iterationAggregateId, "BenchmarkAggregate", events, -1, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Append batch of 10 events to a new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult?> AppendBatchEvents()
	{
		if (!IsAvailable)
		{
			return null;
		}

		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, 10);
		return await _eventStore!.AppendAsync(
			aggregateId, "BenchmarkAggregate", events, -1, CancellationToken.None);
	}

	#endregion

	#region Load Benchmarks

	/// <summary>
	/// Benchmark: Load aggregate with 5 events (small).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>?> LoadSmallAggregate()
	{
		if (!IsAvailable)
		{
			return null;
		}

		return await _eventStore!.LoadAsync(
			_aggregateWith5Events, "BenchmarkAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 50 events (medium).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>?> LoadMediumAggregate()
	{
		if (!IsAvailable)
		{
			return null;
		}

		return await _eventStore!.LoadAsync(
			_aggregateWith50Events, "BenchmarkAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 500 events (large).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>?> LoadLargeAggregate()
	{
		if (!IsAvailable)
		{
			return null;
		}

		return await _eventStore!.LoadAsync(
			_aggregateWith500Events, "BenchmarkAggregate", CancellationToken.None);
	}

	#endregion

	#region Snapshot Benchmarks

	/// <summary>
	/// Benchmark: Save a snapshot for an aggregate.
	/// </summary>
	[Benchmark]
	public async Task SaveSnapshot()
	{
		if (!IsAvailable)
		{
			return;
		}

		var snapshot = new BenchmarkSnapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = _iterationAggregateId,
			AggregateType = "BenchmarkAggregate",
			Version = 1,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new byte[512],
			Metadata = null
		};

		await _snapshotStore!.SaveSnapshotAsync(snapshot, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load the latest snapshot for an aggregate.
	/// </summary>
	[Benchmark]
	public async Task<ISnapshot?> LoadSnapshot()
	{
		if (!IsAvailable)
		{
			return null;
		}

		// Use the pre-populated aggregate which may have snapshots
		return await _snapshotStore!.GetLatestSnapshotAsync(
			_aggregateWith5Events, "BenchmarkAggregate", CancellationToken.None);
	}

	#endregion

	#region Helpers

	private static TestDomainEvent[] CreateEvents(string aggregateId, int count)
	{
		var events = new TestDomainEvent[count];
		for (int i = 0; i < count; i++)
		{
			events[i] = new TestDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = i + 1,
				OccurredAt = DateTimeOffset.UtcNow,
				EventType = "TestDomainEvent",
				Metadata = new Dictionary<string, object>
				{
					["UserId"] = "benchmark-user",
				},
				Data = $"Benchmark event data for version {i + 1}",
			};
		}

		return events;
	}

	private string CreateAggregateWithEvents(int eventCount)
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, eventCount);
		_ = _eventStore!.AppendAsync(
			aggregateId, "BenchmarkAggregate", events, -1, CancellationToken.None)
			.GetAwaiter().GetResult();
		return aggregateId;
	}

	private async Task EnsureSchemaAsync()
	{
		await using var connection = new SqlConnection(ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// Create events table if not exists
		await using var command = new SqlCommand("""
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
				EXEC('CREATE SCHEMA dispatch');

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Events' AND schema_id = SCHEMA_ID('dispatch'))
			CREATE TABLE [dispatch].[Events] (
				[Position] BIGINT IDENTITY(1,1) PRIMARY KEY,
				[EventId] NVARCHAR(200) NOT NULL,
				[AggregateId] NVARCHAR(200) NOT NULL,
				[AggregateType] NVARCHAR(500) NOT NULL,
				[EventType] NVARCHAR(500) NOT NULL,
				[EventData] VARBINARY(MAX) NOT NULL,
				[Metadata] NVARCHAR(MAX) NULL,
				[Version] BIGINT NOT NULL,
				[OccurredAt] DATETIMEOFFSET NOT NULL,
				[IsDispatched] BIT NOT NULL DEFAULT 0,
				[DispatchedAt] DATETIMEOFFSET NULL,
				CONSTRAINT [UQ_Events_AggregateId_Version] UNIQUE ([AggregateId], [Version])
			);

			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Snapshots' AND schema_id = SCHEMA_ID('dispatch'))
			CREATE TABLE [dispatch].[Snapshots] (
				[Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
				[SnapshotId] NVARCHAR(200) NOT NULL,
				[AggregateId] NVARCHAR(200) NOT NULL,
				[AggregateType] NVARCHAR(500) NOT NULL,
				[Version] BIGINT NOT NULL,
				[Data] VARBINARY(MAX) NOT NULL,
				[Metadata] NVARCHAR(MAX) NULL,
				[CreatedAt] DATETIMEOFFSET NOT NULL
			);
			""", connection);

		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	#endregion
}

/// <summary>
/// Simple ISnapshot implementation for benchmark scenarios.
/// </summary>
internal sealed class BenchmarkSnapshot : ISnapshot
{
	public required string SnapshotId { get; init; }
	public required string AggregateId { get; init; }
	public required string AggregateType { get; init; }
	public required long Version { get; init; }
	public required DateTimeOffset CreatedAt { get; init; }
	public required byte[] Data { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
}
