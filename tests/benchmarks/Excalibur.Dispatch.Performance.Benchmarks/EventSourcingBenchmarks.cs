// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for event sourcing critical paths: AppendEvents, LoadAggregate, and SnapshotLoad.
/// Uses an in-memory event store implementation to isolate framework overhead from I/O.
/// </summary>
/// <remarks>
/// These benchmarks measure the framework overhead for event sourcing operations,
/// not database I/O performance. To benchmark real database implementations, create
/// provider-specific benchmark classes that inherit from this pattern.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class EventSourcingBenchmarks
{
	private InMemoryBenchmarkEventStore _eventStore = null!;
	private string _preloadedAggregateId = null!;
	private string _snapshotAggregateId = null!;

	[Params(1, 10, 100)]
	public int EventCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_eventStore = new InMemoryBenchmarkEventStore();

		// Pre-load aggregate for LoadAggregate benchmarks
		_preloadedAggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(_preloadedAggregateId, 100);
		_eventStore.AppendAsync(
			_preloadedAggregateId,
			"BenchmarkAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None).AsTask().GetAwaiter().GetResult();

		// Pre-load aggregate with "snapshot" (events before version 50)
		_snapshotAggregateId = Guid.NewGuid().ToString();
		var snapshotEvents = CreateEvents(_snapshotAggregateId, 200);
		_eventStore.AppendAsync(
			_snapshotAggregateId,
			"BenchmarkAggregate",
			snapshotEvents,
			expectedVersion: -1,
			CancellationToken.None).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>
	/// Benchmarks appending events to a new aggregate stream.
	/// </summary>
	[Benchmark(Baseline = true)]
	public ValueTask<AppendResult> AppendEvents_NewStream()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, EventCount);
		return _eventStore.AppendAsync(
			aggregateId,
			"BenchmarkAggregate",
			events,
			expectedVersion: -1,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmarks appending events to an existing aggregate stream.
	/// </summary>
	[Benchmark]
	public async Task<AppendResult> AppendEvents_ExistingStream()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var initialEvents = CreateEvents(aggregateId, 10);
		await _eventStore.AppendAsync(
			aggregateId,
			"BenchmarkAggregate",
			initialEvents,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		var additionalEvents = CreateEvents(aggregateId, EventCount);
		return await _eventStore.AppendAsync(
			aggregateId,
			"BenchmarkAggregate",
			additionalEvents,
			expectedVersion: 9,
			CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Benchmarks loading a full aggregate event stream.
	/// </summary>
	[Benchmark]
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAggregate_FullStream()
	{
		return _eventStore.LoadAsync(
			_preloadedAggregateId,
			"BenchmarkAggregate",
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmarks loading aggregate events from a specific version (snapshot load pattern).
	/// Simulates loading events after a snapshot at version 50.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Snapshot")]
	public ValueTask<IReadOnlyList<StoredEvent>> SnapshotLoad_FromVersion50()
	{
		return _eventStore.LoadAsync(
			_snapshotAggregateId,
			"BenchmarkAggregate",
			fromVersion: 50,
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmarks loading aggregate events from version 0 (no snapshot).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Snapshot")]
	public ValueTask<IReadOnlyList<StoredEvent>> SnapshotLoad_NoSnapshot()
	{
		return _eventStore.LoadAsync(
			_snapshotAggregateId,
			"BenchmarkAggregate",
			CancellationToken.None);
	}

	/// <summary>
	/// Benchmarks concurrent append operations to different aggregates.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Concurrent")]
	public Task ConcurrentAppend_DifferentAggregates()
	{
		var tasks = new Task[10];
		for (int i = 0; i < 10; i++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			var events = CreateEvents(aggregateId, EventCount);
			tasks[i] = _eventStore.AppendAsync(
				aggregateId,
				"BenchmarkAggregate",
				events,
				expectedVersion: -1,
				CancellationToken.None).AsTask();
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmarks event creation overhead.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Creation")]
	public BenchmarkDomainEvent[] CreateEventsBenchmark()
	{
		return CreateEvents(Guid.NewGuid().ToString(), EventCount);
	}

	private static BenchmarkDomainEvent[] CreateEvents(string aggregateId, int count)
	{
		var events = new BenchmarkDomainEvent[count];
		for (int i = 0; i < count; i++)
		{
			events[i] = new BenchmarkDomainEvent
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				OccurredAt = DateTimeOffset.UtcNow,
				Data = $"BenchmarkData-{i}"
			};
		}

		return events;
	}

	/// <summary>
	/// Benchmark domain event implementation.
	/// </summary>
	public sealed class BenchmarkDomainEvent : IDomainEvent
	{
		public string EventId { get; set; } = string.Empty;
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; }
		public string EventType => nameof(BenchmarkDomainEvent);
		public IDictionary<string, object>? Metadata { get; set; }
		public string Data { get; set; } = string.Empty;
	}

	/// <summary>
	/// In-memory event store for benchmarking framework overhead without I/O.
	/// Uses StoredEvent to match the actual IEventStore interface contract.
	/// </summary>
	private sealed class InMemoryBenchmarkEventStore : IEventStore
	{
		private readonly Dictionary<string, List<StoredEvent>> _streams = new();
		private readonly object _lock = new();

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId,
			string aggregateType,
			IEnumerable<IDomainEvent> events,
			long expectedVersion,
			CancellationToken cancellationToken)
		{
			var eventsList = events.ToList();
			if (eventsList.Count == 0)
			{
				return new ValueTask<AppendResult>(AppendResult.CreateSuccess(expectedVersion, 0));
			}

			lock (_lock)
			{
				var key = $"{aggregateType}:{aggregateId}";

				if (!_streams.TryGetValue(key, out var stream))
				{
					if (expectedVersion != -1)
					{
						return new ValueTask<AppendResult>(AppendResult.CreateConcurrencyConflict(expectedVersion, -1));
					}

					stream = [];
					_streams[key] = stream;
				}
				else
				{
					var currentVersion = stream.Count - 1;
					if (expectedVersion != -1 && currentVersion != expectedVersion)
					{
						return new ValueTask<AppendResult>(AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion));
					}
				}

				long version = stream.Count;
				foreach (var evt in eventsList)
				{
					var stored = new StoredEvent(
						EventId: evt.EventId,
						AggregateId: aggregateId,
						AggregateType: aggregateType,
						EventType: evt.EventType,
						EventData: Encoding.UTF8.GetBytes($"benchmark-{version}"),
						Metadata: null,
						Version: version,
						Timestamp: evt.OccurredAt,
						IsDispatched: true);

					stream.Add(stored);
					version++;
				}

				return new ValueTask<AppendResult>(AppendResult.CreateSuccess(version - 1, stream.Count - eventsList.Count));
			}
		}

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			CancellationToken cancellationToken)
		{
			lock (_lock)
			{
				var key = $"{aggregateType}:{aggregateId}";
				if (_streams.TryGetValue(key, out var stream))
				{
					return new ValueTask<IReadOnlyList<StoredEvent>>(stream.ToList());
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			}
		}

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId,
			string aggregateType,
			long fromVersion,
			CancellationToken cancellationToken)
		{
			lock (_lock)
			{
				var key = $"{aggregateType}:{aggregateId}";
				if (_streams.TryGetValue(key, out var stream))
				{
					var result = stream.Where(e => e.Version > fromVersion).ToList();
					return new ValueTask<IReadOnlyList<StoredEvent>>(result);
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			}
		}

		public ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
			int batchSize,
			CancellationToken cancellationToken)
		{
			return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
		}

		public ValueTask MarkEventAsDispatchedAsync(
			string eventId,
			CancellationToken cancellationToken)
		{
			return ValueTask.CompletedTask;
		}
	}
}
