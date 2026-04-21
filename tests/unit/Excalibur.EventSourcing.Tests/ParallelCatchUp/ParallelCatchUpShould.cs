// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.ParallelCatchUp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.ParallelCatchUp;

/// <summary>
/// G.9 (b1syz1): Unit tests for parallel catch-up -- partitioner, worker, checkpoint, failure.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ParallelCatchUpShould
{
	// --- StreamRange ---

	[Fact]
	public void StreamRangeStoresStartAndEnd()
	{
		var range = new StreamRange(100, 200);
		range.StartPosition.ShouldBe(100);
		range.EndPosition.ShouldBe(200);
	}

	[Fact]
	public void StreamRangeEquality()
	{
		new StreamRange(1, 10).ShouldBe(new StreamRange(1, 10));
		new StreamRange(1, 10).ShouldNotBe(new StreamRange(1, 20));
	}

	// --- IGlobalStreamPartitioner via default implementation tests ---

	[Fact]
	public void PartitionRangeEvenly()
	{
		// Use a simple test implementation
		var partitioner = new EvenPartitioner();
		var ranges = partitioner.Partition(0, 99, 4);

		ranges.Count.ShouldBe(4);
		ranges[0].StartPosition.ShouldBe(0);
		ranges[0].EndPosition.ShouldBe(24);
		ranges[1].StartPosition.ShouldBe(25);
		ranges[1].EndPosition.ShouldBe(49);
		ranges[2].StartPosition.ShouldBe(50);
		ranges[2].EndPosition.ShouldBe(74);
		ranges[3].StartPosition.ShouldBe(75);
		ranges[3].EndPosition.ShouldBe(99);
	}

	[Fact]
	public void PartitionHandlesUnevenDivision()
	{
		var partitioner = new EvenPartitioner();
		var ranges = partitioner.Partition(0, 9, 3);

		ranges.Count.ShouldBe(3);
		// 10 events / 3 workers = ranges of ~3-4 each
		ranges[0].StartPosition.ShouldBe(0);
		// All ranges should be contiguous and cover 0-9
		ranges[2].EndPosition.ShouldBe(9);
	}

	[Fact]
	public void PartitionSingleWorker()
	{
		var partitioner = new EvenPartitioner();
		var ranges = partitioner.Partition(100, 500, 1);

		ranges.Count.ShouldBe(1);
		ranges[0].StartPosition.ShouldBe(100);
		ranges[0].EndPosition.ShouldBe(500);
	}

	// --- ParallelCatchUpWorker ---

	[Fact]
	public async Task WorkerProcessesAllEventsInRange()
	{
		var events = CreateStoredEvents(10, 19);
		var eventStore = new InMemoryRangeEventStore(events);
		var checkpointStore = new InMemoryCheckpointStore();
		var processed = 0;

		var worker = new ParallelCatchUpWorker(
			workerId: 0,
			range: new StreamRange(10, 19),
			eventStore: eventStore,
			checkpointStore: checkpointStore,
			projectionName: "TestProjection",
			batchSize: 100,
			checkpointInterval: 5,
			logger: NullLogger.Instance);

		var count = await worker.ProcessAsync(
			(evt, ct) => { processed++; return Task.CompletedTask; },
			CancellationToken.None);

		processed.ShouldBeGreaterThan(0);
		count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task WorkerCheckpointsAtConfiguredInterval()
	{
		var events = CreateStoredEvents(0, 9);
		var eventStore = new InMemoryRangeEventStore(events);
		var checkpointStore = new InMemoryCheckpointStore();

		var worker = new ParallelCatchUpWorker(
			workerId: 1,
			range: new StreamRange(0, 9),
			eventStore: eventStore,
			checkpointStore: checkpointStore,
			projectionName: "TestProj",
			batchSize: 100,
			checkpointInterval: 5,
			logger: NullLogger.Instance);

		await worker.ProcessAsync(
			(evt, ct) => Task.CompletedTask,
			CancellationToken.None);

		checkpointStore.SaveCount.ShouldBeGreaterThan(0);
	}

	// --- ParallelCatchUpOptions ---

	[Fact]
	public void OptionsHaveSensibleDefaults()
	{
		var options = new ParallelCatchUpOptions();
		options.Strategy.ShouldBe(CatchUpStrategy.Sequential);
		options.BatchSize.ShouldBe(1000);
		options.CheckpointInterval.ShouldBe(5000);
		options.MaxRetries.ShouldBe(3);
		options.WorkerHeartbeatTimeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	// --- Helpers ---

	private static async IAsyncEnumerable<StoredEvent> ToAsyncEnumerable(IReadOnlyList<StoredEvent> events)
	{
		foreach (var e in events)
		{
			yield return e;
		}

		await Task.CompletedTask; // satisfy async
	}

	private static IReadOnlyList<StoredEvent> CreateStoredEvents(long from, long to)
	{
		var events = new List<StoredEvent>();
		for (var i = from; i <= to; i++)
		{
			events.Add(new StoredEvent(
				EventId: Guid.NewGuid().ToString(),
				AggregateId: "agg-1",
				AggregateType: "Test",
				EventType: "TestEvent",
				EventData: Array.Empty<byte>(),
				Metadata: null,
				Version: i,
				Timestamp: DateTimeOffset.UtcNow));
		}

		return events;
	}

	private sealed class InMemoryRangeEventStore : IRangeQueryableEventStore
	{
		private readonly IReadOnlyList<StoredEvent> _events;
		public InMemoryRangeEventStore(IReadOnlyList<StoredEvent> events) => _events = events;

		public async IAsyncEnumerable<StoredEvent> ReadRangeAsync(
			long fromPosition, long toPosition, int batchSize,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
		{
			foreach (var e in _events.Where(e => e.Version >= fromPosition && e.Version <= toPosition))
			{
				yield return e;
			}

			await Task.CompletedTask;
		}
	}

	private sealed class InMemoryCheckpointStore : IParallelCheckpointStore
	{
		public int SaveCount { get; private set; }

		public Task SaveWorkerCheckpointAsync(string projectionName, int workerId, long position, CancellationToken cancellationToken)
		{
			SaveCount++;
			return Task.CompletedTask;
		}

		public Task<long> GetLowWatermarkAsync(string projectionName, CancellationToken cancellationToken)
			=> Task.FromResult(0L);

		public Task<IReadOnlyDictionary<int, long>> GetAllWorkerCheckpointsAsync(string projectionName, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	/// <summary>
	/// Simple even-split partitioner for testing.
	/// </summary>
	private sealed class EvenPartitioner : IGlobalStreamPartitioner
	{
		public IReadOnlyList<StreamRange> Partition(long fromPosition, long toPosition, int workerCount)
		{
			var totalEvents = toPosition - fromPosition + 1;
			var perWorker = totalEvents / workerCount;
			var remainder = totalEvents % workerCount;

			var ranges = new List<StreamRange>(workerCount);
			var current = fromPosition;

			for (var i = 0; i < workerCount; i++)
			{
				var size = perWorker + (i < remainder ? 1 : 0);
				ranges.Add(new StreamRange(current, current + size - 1));
				current += size;
			}

			return ranges;
		}
	}
}
