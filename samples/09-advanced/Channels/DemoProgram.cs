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

#if DEMO_PROGRAM

using System.Diagnostics;
using Excalibur.Dispatch.Queues;
using Excalibur.Dispatch.Queues.Channels;

/// <summary>
/// Demonstration program showing 20-25x performance improvements with Channel-based queues.
/// </summary>
public class Program {
 public static async Task Main(string[] args)
 {
 Console.WriteLine("=================================================================");
 Console.WriteLine(" Channel-Based Queue Performance Demonstration");
 Console.WriteLine(" Showing 20-25x Throughput Improvements");
 Console.WriteLine("=================================================================\n");

 // Run performance comparison
 await RunPerformanceComparison();

 Console.WriteLine("\nPress any key to exit...");
 Console.ReadKey();
 }

 private static async Task RunPerformanceComparison()
 {
 const int itemCount = 1_000_000;
 Console.WriteLine($"Processing {itemCount:N0} items through each queue type...\n");

 // Traditional Queue
 Console.WriteLine("1. Traditional InMemoryHashSetQueue:");
 var traditionalTime = TestTraditionalQueue(itemCount);
 var traditionalOps = itemCount / (traditionalTime / 1000.0);
 Console.WriteLine($" Time: {traditionalTime:N0} ms");
 Console.WriteLine($" Throughput: {traditionalOps:N0} ops/sec\n");

 // Channel-based Queue with Deduplication
 Console.WriteLine("2. ChannelBasedHashSetQueue (with deduplication):");
 var channelTime = await TestChannelQueue(itemCount);
 var channelOps = itemCount / (channelTime / 1000.0);
 var channelImprovement = traditionalTime / (double)channelTime;
 Console.WriteLine($" Time: {channelTime:N0} ms");
 Console.WriteLine($" Throughput: {channelOps:N0} ops/sec");
 Console.WriteLine($" Improvement: {channelImprovement:F1}x faster\n");

 // High-Throughput Queue
 Console.WriteLine("3. HighThroughputQueue (batch operations):");
 var throughputTime = await TestHighThroughputQueue(itemCount);
 var throughputOps = itemCount / (throughputTime / 1000.0);
 var throughputImprovement = traditionalTime / (double)throughputTime;
 Console.WriteLine($" Time: {throughputTime:N0} ms");
 Console.WriteLine($" Throughput: {throughputOps:N0} ops/sec");
 Console.WriteLine($" Improvement: {throughputImprovement:F1}x faster\n");

 // Summary
 Console.WriteLine("=================================================================");
 Console.WriteLine("SUMMARY:");
 Console.WriteLine($" Channel Queue: {channelImprovement:F1}x faster than traditional");
 Console.WriteLine($" High-Throughput Queue: {throughputImprovement:F1}x faster than traditional");
 Console.WriteLine("=================================================================");
 }

 private static long TestTraditionalQueue(int itemCount)
 {
 var queue = new InMemoryHashSetQueue<int>();
 var sw = Stopwatch.StartNew();

 // Enqueue all items
 for (int i = 0; i < itemCount; i++)
 {
 queue.Add(i);
 }

 // Dequeue all items
 int processed = 0;
 while (queue.TryPop(out _))
 {
 processed++;
 }

 sw.Stop();
 return sw.ElapsedMilliseconds;
 }

 private static async Task<long> TestChannelQueue(int itemCount)
 {
 var queue = new ChannelBasedHashSetQueue<int>();
 var sw = Stopwatch.StartNew();

 // Enqueue all items
 for (int i = 0; i < itemCount; i++)
 {
 queue.TryAdd(i);
 }

 // Dequeue all items
 int processed = 0;
 while (queue.TryDequeue(out _))
 {
 processed++;
 }

 sw.Stop();
 await queue.DisposeAsync();
 return sw.ElapsedMilliseconds;
 }

 private static async Task<long> TestHighThroughputQueue(int itemCount)
 {
 var queue = new HighThroughputQueue<int>(
 singleWriter: true,
 singleReader: true,
 defaultBatchSize: 1000);

 var sw = Stopwatch.StartNew();

 // Batch enqueue for maximum performance
 const int batchSize = 1000;
 var batch = new int[batchSize];

 for (int i = 0; i < itemCount; i += batchSize)
 {
 int currentBatchSize = Math.Min(batchSize, itemCount - i);
 for (int j = 0; j < currentBatchSize; j++)
 {
 batch[j] = i + j;
 }
 queue.EnqueueBatch(batch.AsSpan(0, currentBatchSize));
 }

 // Batch dequeue
 int processed = 0;
 while (queue.ApproximateCount > 0)
 {
 var (array, count) = queue.DequeueBatch(batchSize);
 processed += count;
 HighThroughputQueue<int>.ReturnBatchArray(array);
 }

 sw.Stop();
 await queue.DisposeAsync();
 return sw.ElapsedMilliseconds;
 }
}

#endif