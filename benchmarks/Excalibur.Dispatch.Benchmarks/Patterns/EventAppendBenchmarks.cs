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
[InvocationCount(1, unrollFactor: 1)]
public class EventAppendBenchmarks
{
	private readonly List<TestDomainEvent> _singleEventBatch = new();
	private readonly List<TestDomainEvent> _tenEventBatch = new();
	private readonly List<TestDomainEvent> _hundredEventBatch = new();
	private readonly List<TestDomainEvent> _thousandEventBatch = new();
	private MsSqlContainer? _sqlContainer;
	private SqlServerEventStore? _eventStore;
	private string? _connectionString;
	private bool _canReset;
	private bool _iterationReady;

	/// <summary>
	/// Initialize SQL Server container and event store before benchmarks.
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

			// Pre-generate event batches for benchmarks
			_singleEventBatch.Clear();
			_tenEventBatch.Clear();
			_hundredEventBatch.Clear();
			_thousandEventBatch.Clear();
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
			_canReset = true;
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
		_canReset = false;
		_iterationReady = false;
		if (container != null)
		{
			await container.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
		}
	}

	/// <summary>Starts an empty owned-database epoch outside the measured invocation.</summary>
	[IterationSetup]
	public void IterationSetup()
	{
		if (!_canReset) throw new InvalidOperationException("An ambiguous or failed append prohibits benchmark database reuse");
		_canReset = false;
		_iterationReady = false;
		SqlServerBenchmarkSchema.ResetEventsAsync(_sqlContainer!).GetAwaiter().GetResult();
		_canReset = true;
		_iterationReady = true;
	}

	/// <summary>
	/// Benchmark: Single event append to new aggregate.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<AppendResult> AppendSingleEvent()
	{
		BeginInvocation();
		var aggregateId = Guid.NewGuid().ToString();
		var @event = CreateTestEvent(aggregateId, 1);

		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { @event },
			expectedVersion: -1,
			CancellationToken.None);
		return CompleteInvocation(result);
	}

	/// <summary>
	/// Benchmark: Batch append of 10 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendTenEvents()
	{
		BeginInvocation();
		var aggregateId = Guid.NewGuid().ToString();
		var events = _tenEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
		return CompleteInvocation(result);
	}

	/// <summary>
	/// Benchmark: Batch append of 100 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendHundredEvents()
	{
		BeginInvocation();
		var aggregateId = Guid.NewGuid().ToString();
		var events = _hundredEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
		return CompleteInvocation(result);
	}

	/// <summary>
	/// Benchmark: Batch append of 1000 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendThousandEvents()
	{
		BeginInvocation();
		var aggregateId = Guid.NewGuid().ToString();
		var events = _thousandEventBatch.Select(e => CreateTestEvent(aggregateId, e.Version)).ToList();

		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
		return CompleteInvocation(result);
	}

	/// <summary>
	/// Benchmark: Concurrent append to 10 different aggregates (simulates high load).
	/// </summary>
	[Benchmark]
	public async Task ConcurrentAppendTenAggregates()
	{
		BeginInvocation();
		// ValueTask requires .AsTask() for Task.WhenAll (Sprint 250 ValueTask migration)
		var tasks = new List<Task<AppendResult>>(10);

		try
		{
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
		}
		catch
		{
			// A synchronous submission failure must still drain operations already submitted.
			try { _ = await Task.WhenAll(tasks); }
			catch { /* Preserve the original submission failure; this database is never reused. */ }
			throw;
		}

		var results = await Task.WhenAll(tasks);
		foreach (var result in results) RequireCommitted(result);
		_canReset = true;
	}

	/// <summary>
	/// Benchmark: Seed five events and append a sixth with optimistic concurrency.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendToExistingAggregate()
	{
		BeginInvocation();
		// Pre-create aggregate with 5 events
		var aggregateId = Guid.NewGuid().ToString();
		var seeded = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			Enumerable.Range(1, 5).Select(v => CreateTestEvent(aggregateId, v)),
			expectedVersion: -1,
			CancellationToken.None);
		RequireCommitted(seeded);

		// Benchmark: append 6th event
		var @event = CreateTestEvent(aggregateId, 6);
		var result = await _eventStore.AppendAsync(
			aggregateId,
			"TestAggregate",
			new[] { @event },
			expectedVersion: 4,
			CancellationToken.None);
		return CompleteInvocation(result);
	}

	private void BeginInvocation()
	{
		if (!_canReset || !_iterationReady) throw new InvalidOperationException("The append requires a fresh, unambiguous iteration");
		_canReset = false;
		_iterationReady = false;
	}

	private AppendResult CompleteInvocation(AppendResult result)
	{
		RequireCommitted(result);
		_canReset = true;
		return result;
	}

	private static void RequireCommitted(AppendResult result)
	{
		if (result.Outcome != AppendOutcome.Committed)
		{
			throw new InvalidOperationException($"Benchmark append did not commit: {result.Outcome}; {result.ErrorMessage}");
		}
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

	private Task CreateEventStoreTableAsync() => SqlServerBenchmarkSchema.CreateAsync(_sqlContainer!);
}
