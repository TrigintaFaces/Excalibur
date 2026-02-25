// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using System.Threading.Channels;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Benchmarks for CDC (Change Data Capture) processing latency.
/// Measures the latency of change event processing operations.
/// </summary>
/// <remarks>
/// Performance Targets (from sprint-34-plan.md):
/// - CdcProcessor Latency: &lt;10ms P95 (MUST PASS)
/// - Memory Allocation: &lt;1KB per batch (SHOULD PASS)
///
/// These benchmarks focus on the critical path operations:
/// - Channel dequeue operations (ChannelBatchUtilities)
/// - Event processing pipeline
/// - LSN tracking updates
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class CdcLatencyBenchmarks
{
	private Channel<TestChangeEvent>? _channel;
	private TestChangeEvent[]? _preAllocatedEvents;

	/// <summary>
	/// Batch size for dequeue operations.
	/// </summary>
	[Params(10, 100, 500)]
	public int BatchSize { get; set; }

	/// <summary>
	/// Initialize channel and pre-allocated events.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create bounded channel similar to CdcProcessor
		_channel = Channel.CreateBounded<TestChangeEvent>(new BoundedChannelOptions(10000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		});

		// Pre-allocate test events
		_preAllocatedEvents = new TestChangeEvent[10000];
		for (var i = 0; i < _preAllocatedEvents.Length; i++)
		{
			_preAllocatedEvents[i] = CreateTestChangeEvent(i);
		}
	}

	/// <summary>
	/// Setup for each iteration - fill channel with events.
	/// </summary>
	[IterationSetup]
	public void IterationSetup()
	{
		// Clear and refill channel
		while (_channel.Reader.TryRead(out _))
		{ }

		// Fill channel with batch size + buffer
		var eventsToWrite = Math.Min(BatchSize * 2, _preAllocatedEvents.Length);
		for (var i = 0; i < eventsToWrite; i++)
		{
			_ = _channel.Writer.TryWrite(_preAllocatedEvents[i]);
		}
	}

	/// <summary>
	/// Benchmark: Dequeue batch from channel using ChannelBatchUtilities.
	/// Target: &lt;10ms P95 for batch processing latency.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<int> DequeueBatch()
	{
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			_channel.Reader,
			BatchSize,
			CancellationToken.None);

		return batch.Length;
	}

	/// <summary>
	/// Benchmark: Dequeue and process batch (simulates full CDC cycle).
	/// </summary>
	[Benchmark]
	public async Task<int> DequeueAndProcessBatch()
	{
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			_channel.Reader,
			BatchSize,
			CancellationToken.None);

		// Simulate processing (LSN tracking, event handling)
		var processed = 0;
		foreach (var evt in batch)
		{
			// Simulate lightweight event processing
			_ = evt.Lsn;
			_ = evt.SeqVal;
			_ = evt.TableName;
			processed++;
		}

		return processed;
	}

	/// <summary>
	/// Benchmark: Channel write latency (producer side).
	/// </summary>
	[Benchmark]
	public async Task<int> WriteBatch()
	{
		// Clear channel first
		while (_channel.Reader.TryRead(out _))
		{ }

		var written = 0;
		for (var i = 0; i < BatchSize && i < _preAllocatedEvents.Length; i++)
		{
			await _channel.Writer.WriteAsync(_preAllocatedEvents[i]);
			written++;
		}

		return written;
	}

	/// <summary>
	/// Benchmark: Full producer-consumer cycle.
	/// </summary>
	[Benchmark]
	public async Task<int> ProducerConsumerCycle()
	{
		// Clear channel
		while (_channel.Reader.TryRead(out _))
		{ }

		// Producer: write batch
		for (var i = 0; i < BatchSize && i < _preAllocatedEvents.Length; i++)
		{
			await _channel.Writer.WriteAsync(_preAllocatedEvents[i]);
		}

		// Consumer: read batch
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			_channel.Reader,
			BatchSize,
			CancellationToken.None);

		return batch.Length;
	}

	/// <summary>
	/// Benchmark: LSN comparison operations (critical for ordering).
	/// </summary>
	[Benchmark]
	public int LsnComparison()
	{
		var comparisonCount = 0;
		for (var i = 0; i < BatchSize && i < _preAllocatedEvents.Length - 1; i++)
		{
			var result = CompareLsn(_preAllocatedEvents[i].Lsn, _preAllocatedEvents[i + 1].Lsn);
			comparisonCount += result < 0 ? 1 : 0;
		}

		return comparisonCount;
	}

	/// <summary>
	/// Benchmark: Event-driven wait (WaitToReadAsync).
	/// Target: Should be more efficient than polling.
	/// </summary>
	[Benchmark]
	public async Task<bool> EventDrivenWait()
	{
		// Ensure channel has data
		if (_channel.Reader.Count == 0)
		{
			await _channel.Writer.WriteAsync(_preAllocatedEvents[0]);
		}

		return await _channel.Reader.WaitToReadAsync(CancellationToken.None);
	}

	private static TestChangeEvent CreateTestChangeEvent(int index)
	{
		return new TestChangeEvent
		{
			Lsn = BitConverter.GetBytes((long)index),
			SeqVal = BitConverter.GetBytes((long)(index * 10)),
			TableName = $"Table_{index % 10}",
			OperationCode = (byte)(index % 4), // 1=Delete, 2=Insert, 3=UpdateBefore, 4=UpdateAfter
			CommitTime = DateTime.UtcNow,
			Data = new Dictionary<string, object>
			{
				["Id"] = index,
				["Name"] = $"Record_{index}",
				["Value"] = index * 100,
			},
		};
	}

	/// <summary>
	/// LSN comparison helper (mirrors CdcLsnHelper.CompareLsn).
	/// </summary>
	private static int CompareLsn(byte[] lsn1, byte[] lsn2)
	{
		for (var i = 0; i < Math.Min(lsn1.Length, lsn2.Length); i++)
		{
			var cmp = lsn1[i].CompareTo(lsn2[i]);
			if (cmp != 0)
			{
				return cmp;
			}
		}

		return lsn1.Length.CompareTo(lsn2.Length);
	}

	/// <summary>
	/// Test change event for benchmarking.
	/// </summary>
	private sealed class TestChangeEvent
	{
		public required byte[] Lsn { get; init; }
		public required byte[] SeqVal { get; init; }
		public required string TableName { get; init; }
		public required byte OperationCode { get; init; }
		public required DateTime CommitTime { get; init; }
		public required Dictionary<string, object> Data { get; init; }
	}
}
