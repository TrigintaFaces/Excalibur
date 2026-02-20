// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// Benchmarks for InMemoryEventStore operations.
/// </summary>
/// <remarks>
/// AD-221-9 Baseline Targets:
/// - Aggregate load (100 events): &lt; 5ms
/// - Event append (single): &lt; 1ms
/// - Concurrent aggregate ops (10): &lt; 50ms
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class InMemoryEventStoreBenchmarks
{
	private InMemoryEventStore _eventStore = null!;
	private string _aggregateWith10Events = null!;
	private string _aggregateWith100Events = null!;
	private string _aggregateWith1000Events = null!;
	private string _aggregateWith10000Events = null!;
	private TestDomainEvent[] _singleEventBatch = null!;
	private TestDomainEvent[] _tenEventBatch = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_eventStore = new InMemoryEventStore();

		// Pre-populate aggregates with different event counts
		_aggregateWith10Events = CreateAggregateWithEvents(10);
		_aggregateWith100Events = CreateAggregateWithEvents(100);
		_aggregateWith1000Events = CreateAggregateWithEvents(1000);
		_aggregateWith10000Events = CreateAggregateWithEvents(10000);

		// Pre-create event batches for append benchmarks
		_singleEventBatch = CreateEvents("new-agg-1", 1);
		_tenEventBatch = CreateEvents("new-agg-10", 10);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_eventStore.Clear();
	}

	#region Load Benchmarks

	/// <summary>
	/// Benchmark: Load aggregate with 10 events (small aggregate).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate_10Events()
	{
		return await _eventStore.LoadAsync(_aggregateWith10Events, "TestAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 100 events (medium aggregate).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate_100Events()
	{
		return await _eventStore.LoadAsync(_aggregateWith100Events, "TestAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 1000 events (large aggregate).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate_1000Events()
	{
		return await _eventStore.LoadAsync(_aggregateWith1000Events, "TestAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate with 10000 events (very large aggregate).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregate_10000Events()
	{
		return await _eventStore.LoadAsync(_aggregateWith10000Events, "TestAggregate", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Load aggregate from specific version (partial load).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> LoadAggregateFromVersion_Last50of1000()
	{
		return await _eventStore.LoadAsync(_aggregateWith1000Events, "TestAggregate", 950, CancellationToken.None);
	}

	#endregion Load Benchmarks

	#region Append Benchmarks

	/// <summary>
	/// Benchmark: Append single event to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendSingleEvent()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, 1);
		return await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Append batch of 10 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendBatch_10Events()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, 10);
		return await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Append batch of 100 events to new aggregate.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendBatch_100Events()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, 100);
		return await _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None);
	}

	#endregion Append Benchmarks

	#region Concurrent Benchmarks

	/// <summary>
	/// Benchmark: Concurrent load of 10 different aggregates.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentLoad_10Aggregates()
	{
		var tasks = new List<Task<IReadOnlyList<StoredEvent>>>(10);
		for (int i = 0; i < 10; i++)
		{
			tasks.Add(_eventStore.LoadAsync(_aggregateWith100Events, "TestAggregate", CancellationToken.None).AsTask());
		}
		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmark: Concurrent append to 10 different aggregates.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentAppend_10Aggregates()
	{
		var tasks = new List<Task<AppendResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			var events = CreateEvents(aggregateId, 1);
			tasks.Add(_eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None).AsTask());
		}
		_ = await Task.WhenAll(tasks);
	}

	#endregion Concurrent Benchmarks

	#region Outbox Pattern Benchmarks

	/// <summary>
	/// Benchmark: Get undispatched events (outbox polling).
	/// </summary>
	[Benchmark]
	public async Task<IReadOnlyList<StoredEvent>> GetUndispatchedEvents_Batch100()
	{
		return await _eventStore.GetUndispatchedEventsAsync(100, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Mark event as dispatched.
	/// </summary>
	[Benchmark]
	public async Task MarkEventAsDispatched()
	{
		var undispatched = await _eventStore.GetUndispatchedEventsAsync(1, CancellationToken.None);
		if (undispatched.Count > 0)
		{
			await _eventStore.MarkEventAsDispatchedAsync(undispatched[0].EventId, CancellationToken.None);
		}
	}

	#endregion Outbox Pattern Benchmarks

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
					["TenantId"] = "benchmark-tenant",
				},
				Data = $"Test event data for version {i + 1}",
			};
		}
		return events;
	}

	private string CreateAggregateWithEvents(int eventCount)
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, eventCount);
		_ = _eventStore.AppendAsync(aggregateId, "TestAggregate", events, -1, CancellationToken.None).GetAwaiter().GetResult();
		return aggregateId;
	}

	#endregion Helpers
}
