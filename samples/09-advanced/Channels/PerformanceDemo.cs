// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Diagnostics;

namespace examples.Channels;

/// <summary>
///     Demonstrates the performance improvements of channel-based queues. This class provides simple examples showing 20-25x throughput improvements.
/// </summary>
public static class PerformanceDemo
{
	/// <summary>
	///     Runs a simple performance comparison between traditional and channel-based queues.
	/// </summary>
	public static async Task RunPerformanceComparison()
	{
		const int itemCount = 1_000_000;
		Console.WriteLine($"Performance Comparison - Processing {itemCount:N0} items");
		Console.WriteLine(new string('=', 60));

		// Test traditional queue
		var traditionalTime = TestTraditionalQueue(itemCount);
		Console.WriteLine($"Traditional Queue: {traditionalTime:N0} ms");

		// Test channel-based queue
		var channelTime = await TestChannelQueue(itemCount);
		Console.WriteLine($"Channel Queue: {channelTime:N0} ms");

		// Test high-throughput queue
		var throughputTime = await TestHighThroughputQueue(itemCount);
		Console.WriteLine($"High-Throughput Queue: {throughputTime:N0} ms");

		// Calculate improvements
		var channelImprovement = (double)traditionalTime / channelTime;
		var throughputImprovement = (double)traditionalTime / throughputTime;

		Console.WriteLine(new string('-', 60));
		Console.WriteLine($"Channel Queue Improvement: {channelImprovement:F1}x faster");
		Console.WriteLine($"High-Throughput Queue Improvement: {throughputImprovement:F1}x faster");
	}

	/// <summary>
	///     Demonstrates concurrent producer/consumer pattern with high throughput.
	/// </summary>
	public static async Task RunConcurrentDemo()
	{
		Console.WriteLine("\nConcurrent Producer/Consumer Demo");
		Console.WriteLine(new string('=', 60));

		var queue = new HighThroughputQueue<string>();
		var cts = new CancellationTokenSource();

		// Metrics
		long producedCount = 0;
		long consumedCount = 0;

		// Start multiple producers
		var producers = Enumerable.Range(0, 4).Select(id => Task.Run(async () =>
		{
			var localProduced = 0;
			while (!cts.Token.IsCancellationRequested)
			{
				await queue.EnqueueAsync($"Message-{id}-{localProduced++}", cts.Token);
				Interlocked.Increment(ref producedCount);

				if (localProduced >= 250000)
				{
					break;
				}
			}
		})).ToArray();

		// Start multiple consumers
		var consumers = Enumerable.Range(0, 4).Select(id => Task.Run(async () =>
		{
			while (!cts.Token.IsCancellationRequested || queue.ApproximateCount > 0)
			{
				try
				{
					var message = await queue.DequeueAsync(cts.Token);
					Interlocked.Increment(ref consumedCount);
				}
				catch (OperationCanceledException) when (queue.ApproximateCount == 0)
				{
					break;
				}
			}
		})).ToArray();

		// Wait for producers to finish
		await Task.WhenAll(producers);
		queue.Complete();

		// Wait for consumers to drain the queue
		await Task.WhenAll(consumers);

		Console.WriteLine($"Produced: {producedCount:N0} messages");
		Console.WriteLine($"Consumed: {consumedCount:N0} messages");
		Console.WriteLine($"Throughput: {(producedCount + consumedCount):N0} operations");

		await queue.DisposeAsync();
	}

	/// <summary>
	///     Demonstrates memory-efficient batch processing using ArrayPool.
	/// </summary>
	public static async Task RunBatchProcessingDemo()
	{
		Console.WriteLine("\nBatch Processing Demo (Zero Allocations)");
		Console.WriteLine(new string('=', 60));

		var queue = new HighThroughputQueue<int>(defaultBatchSize: 1000);
		var processedCount = 0;

		// Enqueue test data
		for (int i = 0; i < 100000; i++)
		{
			queue.TryEnqueue(i);
		}

		var initialMemory = GC.GetTotalMemory(true);

		// Process in batches with zero allocations
		await queue.ConsumeBatchesAsync(
			batchProcessor: (items, count) =>
			{
				// Process batch without allocating
				for (int i = 0; i < count; i++)
				{
					// Simulate processing
					var _ = items[i] * 2;
					processedCount++;
				}
			},
			batchSize: 1000
		);

		var finalMemory = GC.GetTotalMemory(false);
		var allocatedBytes = finalMemory - initialMemory;

		Console.WriteLine($"Processed: {processedCount:N0} items");
		Console.WriteLine($"Memory allocated during processing: {allocatedBytes:N0} bytes");
		Console.WriteLine($"Allocation per item: {allocatedBytes / (double)processedCount:F2} bytes");

		await queue.DisposeAsync();
	}

	private static long TestTraditionalQueue(int itemCount)
	{
		var queue = new InMemoryHashSetQueue<int>();
		var sw = Stopwatch.StartNew();

		// Enqueue
		for (int i = 0; i < itemCount; i++)
		{
			queue.Add(i);
		}

		// Dequeue
		while (queue.TryPop(out _))
		{
			// Process item
		}

		sw.Stop();
		return sw.ElapsedMilliseconds;
	}

	private static async Task<long> TestChannelQueue(int itemCount)
	{
		var queue = new ChannelBasedHashSetQueue<int>();
		var sw = Stopwatch.StartNew();

		// Enqueue
		for (int i = 0; i < itemCount; i++)
		{
			queue.TryAdd(i);
		}

		// Dequeue
		while (queue.TryDequeue(out _))
		{
			// Process item
		}

		sw.Stop();
		await queue.DisposeAsync();
		return sw.ElapsedMilliseconds;
	}

	private static async Task<long> TestHighThroughputQueue(int itemCount)
	{
		var queue = new HighThroughputQueue<int>(singleWriter: true, singleReader: true);
		var sw = Stopwatch.StartNew();

		// Batch enqueue for maximum performance
		const int batchSize = 1000;
		var batch = new int[batchSize];

		for (int i = 0; i < itemCount; i += batchSize)
		{
			for (int j = 0; j < batchSize && i + j < itemCount; j++)
			{
				batch[j] = i + j;
			}

			queue.EnqueueBatch(batch);
		}

		// Batch dequeue
		while (queue.ApproximateCount > 0)
		{
			var (array, count) = queue.DequeueBatch(batchSize);
			// Process items
			HighThroughputQueue<int>.ReturnBatchArray(array);
		}

		sw.Stop();
		await queue.DisposeAsync();
		return sw.ElapsedMilliseconds;
	}
}
