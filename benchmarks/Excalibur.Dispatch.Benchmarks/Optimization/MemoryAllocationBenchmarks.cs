// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Threading.Channels;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks for memory allocation patterns in processor components.
/// Validates that Phase 1 performance optimizations achieve &lt;1KB per batch allocation.
/// </summary>
/// <remarks>
/// Performance Targets (from sprint-34-plan.md):
/// - Memory Allocation: &lt;1KB per batch (SHOULD PASS)
///
/// Key optimizations being validated:
/// - ArrayPool for batch buffers
/// - Span/Memory&lt;T&gt; for zero-copy operations
/// - Pre-allocated buffers where possible
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MemoryAllocationBenchmarks
{
	private Channel<byte[]>? _channel;
	private byte[][]? _preAllocatedPayloads;

	/// <summary>
	/// Batch size for memory tests.
	/// </summary>
	[Params(10, 100, 500)]
	public int BatchSize { get; set; }

	/// <summary>
	/// Payload size in bytes.
	/// </summary>
	[Params(256, 1024)]
	public int PayloadSize { get; set; }

	/// <summary>
	/// Initialize test data.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		_channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(10000)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false,
			AllowSynchronousContinuations = false,
		});

		// Pre-allocate payloads
		_preAllocatedPayloads = new byte[1000][];
		for (var i = 0; i < _preAllocatedPayloads.Length; i++)
		{
			_preAllocatedPayloads[i] = new byte[PayloadSize];
			Random.Shared.NextBytes(_preAllocatedPayloads[i]);
		}
	}

	/// <summary>
	/// Setup for each iteration.
	/// </summary>
	[IterationSetup]
	public void IterationSetup()
	{
		// Clear and refill channel
		while (_channel.Reader.TryRead(out _))
		{ }

		for (var i = 0; i < BatchSize && i < _preAllocatedPayloads.Length; i++)
		{
			_ = _channel.Writer.TryWrite(_preAllocatedPayloads[i]);
		}
	}

	/// <summary>
	/// Benchmark: Batch processing with ArrayPool (optimized).
	/// Target: &lt;1KB allocation per batch.
	/// </summary>
	[Benchmark(Baseline = true)]
	public int ProcessBatchWithArrayPool()
	{
		// Rent buffer from pool
		var buffer = ArrayPool<byte[]>.Shared.Rent(BatchSize);
		try
		{
			var count = 0;
			while (count < BatchSize && _channel.Reader.TryRead(out var item))
			{
				buffer[count++] = item;
			}

			// Process items
			var totalBytes = 0;
			for (var i = 0; i < count; i++)
			{
				totalBytes += buffer[i].Length;
			}

			return totalBytes;
		}
		finally
		{
			ArrayPool<byte[]>.Shared.Return(buffer, clearArray: true);
		}
	}

	/// <summary>
	/// Benchmark: Batch processing without ArrayPool (baseline).
	/// Shows allocation overhead without pooling.
	/// </summary>
	[Benchmark]
	public int ProcessBatchWithoutArrayPool()
	{
		// Allocate new array each time (no pooling)
		var buffer = new byte[BatchSize][];

		var count = 0;
		while (count < BatchSize && _channel.Reader.TryRead(out var item))
		{
			buffer[count++] = item;
		}

		// Process items
		var totalBytes = 0;
		for (var i = 0; i < count; i++)
		{
			if (buffer[i] != null)
			{
				totalBytes += buffer[i].Length;
			}
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Using List&lt;T&gt; for dynamic batching.
	/// Shows allocation pattern comparison.
	/// </summary>
	[Benchmark]
	public int ProcessBatchWithList()
	{
		var buffer = new List<byte[]>(BatchSize);

		while (buffer.Count < BatchSize && _channel.Reader.TryRead(out var item))
		{
			buffer.Add(item);
		}

		// Process items
		var totalBytes = 0;
		foreach (var item in buffer)
		{
			totalBytes += item.Length;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: ChannelBatchUtilities dequeue (actual implementation).
	/// </summary>
	[Benchmark]
	public async Task<int> ProcessBatchChannelBatchUtilities()
	{
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			_channel.Reader,
			BatchSize,
			CancellationToken.None);

		// Process items
		var totalBytes = 0;
		foreach (var item in batch)
		{
			totalBytes += item.Length;
		}

		return totalBytes;
	}

	/// <summary>
	/// Benchmark: Span-based payload processing.
	/// </summary>
	[Benchmark]
	public int ProcessPayloadsWithSpan()
	{
		var totalProcessed = 0;

		for (var i = 0; i < BatchSize && i < _preAllocatedPayloads.Length; i++)
		{
			var span = _preAllocatedPayloads[i].AsSpan();

			// Simulate processing without allocation
			var sum = 0;
			foreach (var b in span)
			{
				sum += b;
			}

			totalProcessed += span.Length;
		}

		return totalProcessed;
	}

	/// <summary>
	/// Benchmark: Memory&lt;T&gt; based payload processing.
	/// </summary>
	[Benchmark]
	public int ProcessPayloadsWithMemory()
	{
		var totalProcessed = 0;

		for (var i = 0; i < BatchSize && i < _preAllocatedPayloads.Length; i++)
		{
			Memory<byte> memory = _preAllocatedPayloads[i];

			// Simulate processing
			var span = memory.Span;
			var sum = 0;
			foreach (var b in span)
			{
				sum += b;
			}

			totalProcessed += memory.Length;
		}

		return totalProcessed;
	}

	/// <summary>
	/// Benchmark: StringBuilder vs string concatenation for metadata.
	/// </summary>
	[Benchmark]
	public string BuildMetadataStringBuilder()
	{
		var sb = new System.Text.StringBuilder(256);

		for (var i = 0; i < 10; i++)
		{
			_ = sb.Append("Key").Append(i).Append('=').Append("Value").Append(i).Append(';');
		}

		return sb.ToString();
	}

	/// <summary>
	/// Benchmark: String interpolation for metadata.
	/// </summary>
	[Benchmark]
	public string BuildMetadataInterpolation()
	{
		var result = string.Empty;

		for (var i = 0; i < 10; i++)
		{
			result += $"Key{i}=Value{i};";
		}

		return result;
	}

	/// <summary>
	/// Benchmark: Pre-allocated string array join for metadata.
	/// </summary>
	[Benchmark]
	public string BuildMetadataArrayJoin()
	{
		var parts = new string[10];

		for (var i = 0; i < 10; i++)
		{
			parts[i] = $"Key{i}=Value{i}";
		}

		return string.Join(';', parts);
	}
}
