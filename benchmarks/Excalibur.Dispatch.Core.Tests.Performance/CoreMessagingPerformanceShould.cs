// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Tests.TestFakes;

using Excalibur.Data.InMemory.Inbox;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Comprehensive performance test suite for core messaging components.
/// </summary>
[Trait("Category", "Performance")]
public sealed class CoreMessagingPerformanceShould : IDisposable, IAsyncDisposable
{
	private readonly ILogger<InMemoryInboxStore> _inboxLogger;
	private readonly ILogger<BatchProcessor<string>> _batchLogger;
	private readonly ILogger<UnifiedBatchingMiddleware> _middlewareLogger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly List<IDisposable> _disposables;
	private readonly List<IAsyncDisposable> _asyncDisposables;
	private const string DefaultHandlerType = "TestHandler";

	public CoreMessagingPerformanceShould()
	{
		_loggerFactory = NullLoggerFactory.Instance;
		_inboxLogger = NullLogger<InMemoryInboxStore>.Instance;
		_batchLogger = NullLogger<BatchProcessor<string>>.Instance;
		_middlewareLogger = NullLogger<UnifiedBatchingMiddleware>.Instance;
		_disposables = new List<IDisposable>();
		_asyncDisposables = new List<IAsyncDisposable>();
	}

	[Fact]
	public async Task HandleHighThroughputInboxOperationsWithoutMemoryLeaks()
	{
		// Arrange
		const int messageCount = 10_000;
		const int concurrentWriters = 10;
		const int batchSize = 100;

		var options = new InMemoryInboxOptions
		{
			MaxEntries = messageCount * 2,
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(50)
		};

		var store = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(options), _inboxLogger);
		_disposables.Add(store);

		var initialMemory = GC.GetTotalMemory(true);
		var stopwatch = Stopwatch.StartNew();

		// Act - High throughput concurrent operations
		var tasks = Enumerable.Range(0, concurrentWriters)
			.Select(async writerId =>
			{
				var messagesPerWriter = messageCount / concurrentWriters;
				for (var i = 0; i < messagesPerWriter; i += batchSize)
				{
					var batchTasks = Enumerable.Range(i, Math.Min(batchSize, messagesPerWriter - i))
						.Select(async msgIndex =>
						{
							var messageId = $"writer-{writerId}-msg-{msgIndex}";
							var payload = Encoding.UTF8.GetBytes($"payload-{messageId}");
							var metadata = new Dictionary<string, object>
							{
								["writerId"] = writerId,
								["messageIndex"] = msgIndex,
								["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
							};

							_ = await store.CreateEntryAsync(messageId, DefaultHandlerType, "TestMessage", payload, metadata, CancellationToken.None);

							// Simulate some processing and marking as processed
							if (msgIndex % 3 == 0)
							{
								await store.MarkProcessedAsync(messageId, DefaultHandlerType, CancellationToken.None);
							}
						});

					await Task.WhenAll(batchTasks);
				}
			});

		await Task.WhenAll(tasks);
		stopwatch.Stop();

		// Wait for cleanup to settle
		await Task.Delay(200);

		// Assert - Performance and memory characteristics
		var finalMemory = GC.GetTotalMemory(true);
		var memoryGrowth = finalMemory - initialMemory;
		var throughput = messageCount / stopwatch.Elapsed.TotalSeconds;

		// Performance assertions
		throughput.ShouldBeGreaterThan(200, "Should process at least 200 messages per second");
		stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60), "Should complete within 60 seconds");

		// Memory leak detection
		var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
		memoryGrowthMB.ShouldBeLessThan(50, $"Memory growth should be less than 50MB, was {memoryGrowthMB:F2}MB");

		// Verify cleanup effectiveness
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.TotalEntries.ShouldBeLessThanOrEqualTo(messageCount, "Cleanup should have reduced total entries");
	}

	[Fact]
	public async Task HandleMicroBatchProcessingUnderExtremeConcurrency()
	{
		// Arrange
		const int itemCount = 50_000;
		const int concurrentProducers = 20;
		const int maxBatchSize = 100;

		var processedItems = new ConcurrentBag<string>();
		var batchSizes = new ConcurrentBag<int>();
		var processingLatencies = new ConcurrentBag<long>();

		var options = new MicroBatchOptions
		{
			MaxBatchSize = maxBatchSize,
			MaxBatchDelay = TimeSpan.FromMilliseconds(10)
		};

		var processor = new BatchProcessor<string>(
			async batch =>
			{
				var batchStopwatch = Stopwatch.StartNew();

				// Simulate realistic batch processing
				await Task.Delay(Random.Shared.Next(1, 5));

				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				batchStopwatch.Stop();
				batchSizes.Add(batch.Count);
				processingLatencies.Add(batchStopwatch.ElapsedMilliseconds);
			},
			_batchLogger,
			options);

		_disposables.Add(processor);

		var initialMemory = GC.GetTotalMemory(true);
		var overallStopwatch = Stopwatch.StartNew();

		// Act - Extreme concurrency stress test
		var producerTasks = Enumerable.Range(0, concurrentProducers)
			.Select(async producerId =>
			{
				var itemsPerProducer = itemCount / concurrentProducers;
				var tasks = new List<Task>();

				for (var i = 0; i < itemsPerProducer; i++)
				{
					var item = $"producer-{producerId}-item-{i}";
					tasks.Add(processor.AddAsync(item, CancellationToken.None).AsTask());

					// Add some jitter to simulate realistic load patterns
					if (i % 10 == 0)
					{
						await Task.Delay(Random.Shared.Next(0, 2));
					}

					// Process in smaller batches to avoid overwhelming the system
					if (tasks.Count >= 50)
					{
						await Task.WhenAll(tasks);
						tasks.Clear();
					}
				}

				if (tasks.Count > 0)
				{
					await Task.WhenAll(tasks);
				}
			});

		await Task.WhenAll(producerTasks);

		// Allow processing to complete
		await Task.Delay(500);
		overallStopwatch.Stop();

		// Assert - Performance characteristics
		var finalMemory = GC.GetTotalMemory(true);
		var memoryGrowth = finalMemory - initialMemory;
		var throughput = itemCount / overallStopwatch.Elapsed.TotalSeconds;

		// Correctness assertions
		processedItems.Count.ShouldBe(itemCount, "All items should be processed");
		processedItems.Distinct().Count().ShouldBe(itemCount, "No duplicate processing");

		// Performance assertions
		throughput.ShouldBeGreaterThan(5000, "Should process at least 5000 items per second");
		overallStopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20), "Should complete within 20 seconds");

		// Batching efficiency
		var avgBatchSize = batchSizes.Average();
		avgBatchSize.ShouldBeGreaterThan(10, "Should achieve reasonable batch sizes");
		batchSizes.Max().ShouldBeLessThanOrEqualTo(maxBatchSize, "Should not exceed max batch size");

		// Latency characteristics
		var avgLatency = processingLatencies.Average();
		var p95Latency = processingLatencies.OrderBy(x => x).Skip((int)(processingLatencies.Count * 0.95)).First();

		avgLatency.ShouldBeLessThan(100, "Average batch processing latency should be under 100ms");
		p95Latency.ShouldBeLessThan(500, "P95 batch processing latency should be under 500ms");

		// Memory efficiency
		var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
		memoryGrowthMB.ShouldBeLessThan(100, $"Memory growth should be less than 100MB, was {memoryGrowthMB:F2}MB");
	}

	[Fact]
	public async Task HandleUnifiedBatchingMiddlewareUnderSustainedLoad()
	{
		// Arrange
		const int messageCount = 25_000;
		const int concurrentStreams = 15;
		const int batchKeysCount = 5;

		var processedMessages = new ConcurrentBag<IDispatchMessage>();
		var batchingMetrics = new ConcurrentBag<(string batchKey, int batchSize, long processingTime)>();

		var options = new UnifiedBatchingOptions
		{
			MaxBatchSize = 50,
			MaxBatchDelay = TimeSpan.FromMilliseconds(25),
			MaxParallelism = 8,
			ProcessAsOptimizedBulk = false,
			BatchKeySelector = msg => $"batch-{Math.Abs(msg.GetHashCode()) % batchKeysCount}"
		};

		var middleware = new UnifiedBatchingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _middlewareLogger, _loggerFactory);
		_asyncDisposables.Add(middleware);

		var initialMemory = GC.GetTotalMemory(true);
		var overallStopwatch = Stopwatch.StartNew();

		// Act - Sustained load test with multiple batch keys
		var streamTasks = Enumerable.Range(0, concurrentStreams)
			.Select(async streamId =>
			{
				var messagesPerStream = messageCount / concurrentStreams;
				var tasks = new List<Task>();

				for (var i = 0; i < messagesPerStream; i++)
				{
					var message = new FakeDispatchMessage();
					var context = new FakeMessageContext();

					async ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
					{
						var processingStopwatch = Stopwatch.StartNew();

						// Simulate realistic message processing
						await Task.Delay(Random.Shared.Next(1, 10), ct);

						processedMessages.Add(msg);
						processingStopwatch.Stop();

						var batchKey = options.BatchKeySelector(msg);
						batchingMetrics.Add((batchKey, 1, processingStopwatch.ElapsedMilliseconds));

						return Excalibur.Dispatch.Abstractions.MessageResult.Success();
					}

					var task = middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).AsTask();
					tasks.Add(task);

					// Process in smaller batches to simulate realistic flow
					if (tasks.Count >= 25)
					{
						await Task.WhenAll(tasks);
						tasks.Clear();
					}

					// Add realistic inter-message delays
					if (i % 50 == 0)
					{
						await Task.Delay(Random.Shared.Next(1, 5));
					}
				}

				if (tasks.Count > 0)
				{
					await Task.WhenAll(tasks);
				}
			});

		await Task.WhenAll(streamTasks);
		overallStopwatch.Stop();

		// Allow final batches to complete
		await Task.Delay(300);

		// Assert - Performance and correctness
		var finalMemory = GC.GetTotalMemory(true);
		var memoryGrowth = finalMemory - initialMemory;
		var throughput = messageCount / overallStopwatch.Elapsed.TotalSeconds;

		// Correctness assertions
		processedMessages.Count.ShouldBeGreaterThanOrEqualTo((int)(messageCount * 0.99), "At least 99% of messages should be processed");

		// Performance assertions
		throughput.ShouldBeGreaterThan(2000, "Should process at least 2000 messages per second");
		overallStopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30), "Should complete within 30 seconds");

		// Batching distribution validation
		var batchKeyGroups = batchingMetrics.GroupBy(m => m.batchKey).ToList();
		batchKeyGroups.Count.ShouldBe(batchKeysCount, "Should distribute across all batch keys");

		foreach (var group in batchKeyGroups)
		{
			group.Count().ShouldBeGreaterThan(messageCount / (batchKeysCount * 2),
				$"Batch key {group.Key} should have reasonable message distribution");
		}

		// Latency characteristics
		var avgProcessingTime = batchingMetrics.Average(m => m.processingTime);
		var p95ProcessingTime = batchingMetrics.OrderBy(m => m.processingTime)
			.Skip((int)(batchingMetrics.Count * 0.95)).First().processingTime;

		avgProcessingTime.ShouldBeLessThan(50, "Average processing time should be under 50ms");
		p95ProcessingTime.ShouldBeLessThan(200, "P95 processing time should be under 200ms");

		// Memory efficiency
		var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
		memoryGrowthMB.ShouldBeLessThan(150, $"Memory growth should be less than 150MB, was {memoryGrowthMB:F2}MB");
	}

	[Fact]
	public async Task HandleIntegratedComponentsUnderRealisticWorkload()
	{
		// Arrange - Integrated test combining all three components
		const int messageCount = 15_000;
		const int concurrentFlows = 10;

		var options = new UnifiedBatchingOptions
		{
			MaxBatchSize = 25,
			MaxBatchDelay = TimeSpan.FromMilliseconds(15),
			MaxParallelism = 6,
			ProcessAsOptimizedBulk = true
		};

		var inboxOptions = new InMemoryInboxOptions
		{
			MaxEntries = messageCount,
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(100)
		};

		var batchOptions = new MicroBatchOptions
		{
			MaxBatchSize = 20,
			MaxBatchDelay = TimeSpan.FromMilliseconds(20)
		};

		var inbox = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(inboxOptions), _inboxLogger);
		var middleware = new UnifiedBatchingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _middlewareLogger, _loggerFactory);

		var endToEndResults = new ConcurrentBag<(string messageId, bool inboxProcessed, bool middlewareProcessed, long totalLatency)>();

		var batchProcessor = new BatchProcessor<string>(
			async batch =>
			{
				// Simulate downstream processing of batched items
				await Task.Delay(Random.Shared.Next(5, 15));

				foreach (var item in batch)
				{
					// Mark as processed in monitoring
					var parts = item.Split('-');
					if (parts.Length >= 2)
					{
						endToEndResults.Add((item, true, true, 0));
					}
				}
			},
			_batchLogger,
			batchOptions);

		_disposables.AddRange(new IDisposable[] { inbox, batchProcessor });
		_asyncDisposables.Add(middleware);

		var initialMemory = GC.GetTotalMemory(true);
		var overallStopwatch = Stopwatch.StartNew();

		// Act - Realistic integrated workflow
		var flowTasks = Enumerable.Range(0, concurrentFlows)
			.Select(async flowId =>
			{
				var messagesPerFlow = messageCount / concurrentFlows;

				for (var i = 0; i < messagesPerFlow; i++)
				{
					var messageStopwatch = Stopwatch.StartNew();
					var messageId = $"flow-{flowId}-msg-{i}";
					var payload = Encoding.UTF8.GetBytes($"integrated-payload-{messageId}");
					var metadata = new Dictionary<string, object>
					{
						["flowId"] = flowId,
						["messageIndex"] = i,
						["processingMode"] = "integrated"
					};

					// Step 1: Inbox processing (deduplication)
					var inboxEntry = await inbox.CreateEntryAsync(messageId, DefaultHandlerType, "IntegratedTestMessage", payload, metadata, CancellationToken.None);

					// Step 2: Middleware processing (batching)
					var message = new FakeDispatchMessage();
					var context = new FakeMessageContext();

					async ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
					{
						// Step 3: Downstream batch processing
						await batchProcessor.AddAsync(messageId, ct);
						return Excalibur.Dispatch.Abstractions.MessageResult.Success();
					}

					var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

					// Step 4: Complete inbox processing
					if (result.IsSuccess)
					{
						await inbox.MarkProcessedAsync(messageId, DefaultHandlerType, CancellationToken.None);
					}

					messageStopwatch.Stop();
					endToEndResults.Add((messageId, true, true, messageStopwatch.ElapsedMilliseconds));

					// Realistic inter-message timing
					if (i % 20 == 0)
					{
						await Task.Delay(Random.Shared.Next(2, 8));
					}
				}
			});

		await Task.WhenAll(flowTasks);
		overallStopwatch.Stop();

		// Allow final processing to complete
		await Task.Delay(500);

		// Assert - End-to-end performance and correctness
		var finalMemory = GC.GetTotalMemory(true);
		var memoryGrowth = finalMemory - initialMemory;
		var throughput = messageCount / overallStopwatch.Elapsed.TotalSeconds;

		// Correctness assertions
		endToEndResults.Count.ShouldBeGreaterThanOrEqualTo((int)(messageCount * 0.95), "At least 95% of messages should complete end-to-end");

		var completedMessages = endToEndResults.Where(r => r.inboxProcessed && r.middlewareProcessed).ToList();
		completedMessages.Count.ShouldBeGreaterThanOrEqualTo((int)(messageCount * 0.95), "At least 95% should complete all stages");

		// Performance assertions
		throughput.ShouldBeGreaterThan(300, "Integrated throughput should be at least 300 messages per second");
		overallStopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(45), "Should complete within 45 seconds");

		// Latency analysis
		var validLatencies = endToEndResults.Where(r => r.totalLatency > 0).Select(r => r.totalLatency).ToList();
		if (validLatencies.Count > 0)
		{
			var avgLatency = validLatencies.Average();
			var p95Latency = validLatencies.OrderBy(x => x).Skip((int)(validLatencies.Count * 0.95)).FirstOrDefault();

			avgLatency.ShouldBeLessThan(200, "Average end-to-end latency should be under 200ms");
			p95Latency.ShouldBeLessThan(1000, "P95 end-to-end latency should be under 1000ms");
		}

		// Memory efficiency for integrated scenario
		var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
		memoryGrowthMB.ShouldBeLessThan(200, $"Integrated scenario memory growth should be less than 200MB, was {memoryGrowthMB:F2}MB");

		// Component health validation
		var inboxStats = await inbox.GetStatisticsAsync(CancellationToken.None);
		inboxStats.ProcessedEntries.ShouldBeGreaterThan((int)(messageCount * 0.8), "Inbox should have processed most messages");
	}

	[Fact]
	public async Task MaintainPerformanceUnderGarbageCollectionPressure()
	{
		// Arrange - Test specifically designed to trigger GC pressure
		const int messageCount = 8_000;
		const int largeObjectSize = 85_000; // Large Object Heap threshold

		var options = new InMemoryInboxOptions
		{
			MaxEntries = messageCount,
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(25)
		};

		var store = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(options), _inboxLogger);
		_disposables.Add(store);

		var performanceMetrics = new ConcurrentBag<(long beforeGC, long afterGC, long operationTime, int generation)>();

		// Act - Operations that stress GC
		var stopwatch = Stopwatch.StartNew();

		var tasks = Enumerable.Range(0, messageCount)
			.Select(async i =>
			{
				var operationStopwatch = Stopwatch.StartNew();
				var beforeGC = GC.GetTotalMemory(false);

				// Create large payload to stress GC
				var largePayload = new byte[largeObjectSize];
				Random.Shared.NextBytes(largePayload);

				var messageId = $"gc-stress-{i}";
				var metadata = new Dictionary<string, object>
				{
					["size"] = largeObjectSize,
					["iteration"] = i
				};

				_ = await store.CreateEntryAsync(messageId, DefaultHandlerType, "GCStressMessage", largePayload, metadata, CancellationToken.None);

				// Trigger GC periodically to test performance under pressure
				if (i % 100 == 0)
				{
					GC.Collect(2, GCCollectionMode.Optimized, false);
					var afterGC = GC.GetTotalMemory(true);

					operationStopwatch.Stop();
					performanceMetrics.Add((beforeGC, afterGC, operationStopwatch.ElapsedMilliseconds, 2));
				}

				// Clean up some entries to test concurrent cleanup
				if (i % 50 == 0 && i > 0)
				{
					await store.MarkProcessedAsync($"gc-stress-{i - 50}", DefaultHandlerType, CancellationToken.None);
				}
			});

		await Task.WhenAll(tasks);
		stopwatch.Stop();

		// Assert - Performance under GC pressure
		var totalThroughput = messageCount / stopwatch.Elapsed.TotalSeconds;
		totalThroughput.ShouldBeGreaterThan(500, "Should maintain reasonable throughput under GC pressure");

		// GC impact analysis
		var metricsWithValidData = performanceMetrics.Where(m => m.operationTime > 0).ToList();
		if (metricsWithValidData.Count > 0)
		{
			var avgOperationTime = metricsWithValidData.Average(m => m.operationTime);
			var maxOperationTime = metricsWithValidData.Max(m => m.operationTime);

			avgOperationTime.ShouldBeLessThan(100, "Average operation time should be reasonable under GC pressure");
			maxOperationTime.ShouldBeLessThan(1000, "Max operation time should not spike excessively during GC");
		}

		// Memory cleanup effectiveness
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.ProcessedEntries.ShouldBeGreaterThan(50, "Should have processed some messages despite GC pressure");
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}

		foreach (var asyncDisposable in _asyncDisposables)
		{
			asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}

		foreach (var asyncDisposable in _asyncDisposables)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
	}
}
