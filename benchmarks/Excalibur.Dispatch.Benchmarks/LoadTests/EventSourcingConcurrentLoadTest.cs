// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Benchmarks.Patterns;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

namespace Excalibur.Dispatch.Benchmarks.LoadTests;

/// <summary>
/// Concurrent aggregate load/save benchmarks using InMemoryEventStore.
/// Measures throughput and concurrency behavior with parameterized writers and event counts.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
[BenchmarkCategory("LoadTest")]
public class EventSourcingConcurrentLoadTest
{
	private InMemoryEventStore _eventStore = null!;

	/// <summary>
	/// Number of concurrent writers appending to separate aggregates.
	/// </summary>
	[Params(1, 5, 10)]
	public int ConcurrentWriters { get; set; }

	/// <summary>
	/// Number of events to append per aggregate.
	/// </summary>
	[Params(10, 100)]
	public int EventsPerAggregate { get; set; }

	/// <summary>
	/// Initialize a fresh event store before each benchmark iteration.
	/// </summary>
	[IterationSetup]
	public void IterationSetup()
	{
		_eventStore = new InMemoryEventStore();
	}

	/// <summary>
	/// Benchmark: Concurrent append — each writer appends events to its own aggregate.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Concurrent Append")]
	public async Task ConcurrentAppend()
	{
		var tasks = new Task[ConcurrentWriters];

		for (var w = 0; w < ConcurrentWriters; w++)
		{
			var aggregateId = $"aggregate-{w}";
			tasks[w] = AppendEventsAsync(aggregateId, EventsPerAggregate);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Concurrent append then load — appends events, then loads all aggregates.
	/// </summary>
	[Benchmark(Description = "Append + Load")]
	public async Task ConcurrentAppendThenLoad()
	{
		var aggregateIds = new string[ConcurrentWriters];
		var appendTasks = new Task[ConcurrentWriters];

		for (var w = 0; w < ConcurrentWriters; w++)
		{
			aggregateIds[w] = $"aggregate-{w}";
			appendTasks[w] = AppendEventsAsync(aggregateIds[w], EventsPerAggregate);
		}

		await Task.WhenAll(appendTasks).ConfigureAwait(false);

		// Now load all aggregates concurrently
		var loadTasks = new Task<IReadOnlyList<StoredEvent>>[ConcurrentWriters];
		for (var w = 0; w < ConcurrentWriters; w++)
		{
			var id = aggregateIds[w];
			loadTasks[w] = _eventStore.LoadAsync(id, "LoadTestAggregate", CancellationToken.None).AsTask();
		}

		_ = await Task.WhenAll(loadTasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmark: Optimistic concurrency conflict — multiple writers attempt to append
	/// to the same aggregate, measuring conflict detection overhead.
	/// </summary>
	[Benchmark(Description = "Optimistic Concurrency Conflicts")]
	public async Task OptimisticConcurrencyConflicts()
	{
		const string sharedAggregateId = "shared-aggregate";
		var conflictCount = 0;

		// Seed with initial event
		_ = await _eventStore.AppendAsync(
			sharedAggregateId,
			"LoadTestAggregate",
			[CreateEvent(sharedAggregateId, 1)],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Each writer tries to append; most will conflict
		var tasks = new Task[ConcurrentWriters];
		for (var w = 0; w < ConcurrentWriters; w++)
		{
			tasks[w] = Task.Run(async () =>
			{
				for (var i = 0; i < EventsPerAggregate; i++)
				{
					// Always use version 0 to force conflicts on all but the first
					var result = await _eventStore.AppendAsync(
						sharedAggregateId,
						"LoadTestAggregate",
						[CreateEvent(sharedAggregateId, i + 2)],
						expectedVersion: 0,
						CancellationToken.None).ConfigureAwait(false);

					if (!result.Success)
					{
						Interlocked.Increment(ref conflictCount);
					}
				}
			});
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private async Task AppendEventsAsync(string aggregateId, int eventCount)
	{
		var events = new TestDomainEvent[eventCount];
		for (var i = 0; i < eventCount; i++)
		{
			events[i] = CreateEvent(aggregateId, i + 1);
		}

		_ = await _eventStore.AppendAsync(
			aggregateId,
			"LoadTestAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);
	}

	private static TestDomainEvent CreateEvent(string aggregateId, long version) =>
		new()
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "LoadTestEvent",
			Metadata = new Dictionary<string, object>
			{
				["Writer"] = "load-test",
			},
			Data = $"Load test event v{version}",
		};
}
