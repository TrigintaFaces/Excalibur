// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing;
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
		try
		{
			// Start SQL Server container
			_sqlContainer = new MsSqlBuilder()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04")
				.WithCreateParameterModifier(parameters => parameters.HostConfig.Memory = 4L * 1024 * 1024 * 1024)
				.Build();

			using var startupDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			await _sqlContainer.StartAsync(startupDeadline.Token);
			_connectionString = _sqlContainer.GetConnectionString();

			// Create EventStore table
			await CreateEventStoreTableAsync();

			// Initialize event store
			_eventStore = new SqlServerEventStore(_connectionString, NullLogger<SqlServerEventStore>.Instance, BenchmarkTenantContext.SingleTenant);

			// Pre-populate aggregates with different event counts
			_aggregateWith10Events = await CreateAggregateWithEventsAsync(10);
			_aggregateWith100Events = await CreateAggregateWithEventsAsync(100);
			_aggregateWith1000Events = await CreateAggregateWithEventsAsync(1000);
		}
		catch (Exception setupFailure)
		{
			try { await GlobalCleanup(); }
			catch (Exception cleanupFailure) { setupFailure.Data["BenchmarkCleanupFailure"] = cleanupFailure; }
			throw;
		}
	}

	/// <summary>
	/// Cleanup SQL Server container after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		var container = _sqlContainer;
		_sqlContainer = null;
		_eventStore = null;
		_connectionString = null;
		if (container != null)
		{
			await container.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
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
	/// Benchmark: Load versions 951 through 999 (49 events; the lower bound is exclusive).
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
	/// Benchmark: Ten concurrent reads of the same 100-event aggregate.
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

		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			deadline.Token);

		if (result.Outcome != AppendOutcome.Committed || result.NextExpectedVersion != eventCount - 1)
		{
			throw new InvalidOperationException($"Benchmark seed did not commit {eventCount} events: {result.Outcome}");
		}

		var stored = await _eventStore.LoadAsync(aggregateId, "TestAggregate", deadline.Token);
		if (stored.Count != eventCount)
		{
			throw new InvalidOperationException("Benchmark seed durable event count differs");
		}

		for (int i = 0; i < stored.Count; i++)
		{
			var value = stored[i];
			if (value.Version != i || value.AggregateId != aggregateId || value.AggregateType != "TestAggregate"
				|| value.TenantId != BenchmarkTenantContext.SingleTenant.TenantId || value.GlobalPosition <= 0
				|| value.EventData is not { Length: > 0 } || value.Metadata is not { Length: > 0 } || value.ArchivedAt != null)
			{
				throw new InvalidOperationException("Benchmark seed durable event identity differs");
			}
		}

		return aggregateId;
	}

	private Task CreateEventStoreTableAsync() => SqlServerBenchmarkSchema.CreateAsync(_sqlContainer!);
}
