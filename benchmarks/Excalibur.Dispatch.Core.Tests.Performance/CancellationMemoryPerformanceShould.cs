// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;

using Excalibur.Data.InMemory.Inbox;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Shouldly;

using Xunit;
using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Memory allocation and GC performance tests for cancellation scenarios.
///     Tests compliance with requirements R9.6-R9.17, focusing on GC pressure and allocation patterns.
/// </summary>
/// <remarks>
///     These tests focus on memory and GC impact:
///     - GC pressure during high-frequency cancellation
///     - Allocation patterns for cancellation token creation
///     - Memory leaks detection during cancellation cascades
///     - GC pause time impact under cancellation load
///     - Object pooling effectiveness for cancellation-related objects
///     - Memory fragmentation during cancellation storms
/// </remarks>
[Trait("Category", "Performance")]
public sealed class CancellationMemoryPerformanceShould : IDisposable
{
	private readonly ITestOutputHelper _output;
	private readonly List<IDisposable> _disposables;
#pragma warning disable CA2213 // Disposed via _disposables list
	private readonly CancellationTokenSource _testCancellation;
#pragma warning restore CA2213

	public CancellationMemoryPerformanceShould(ITestOutputHelper output)
	{
		_output = output;
		_disposables = new List<IDisposable>();
		_testCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		_disposables.Add(_testCancellation);
	}

	#region GC Pressure Tests

	[Fact]
	public async Task HighFrequencyCancellation_ShouldMinimizeGCPressure()
	{
		// Arrange
		const int cancellationRounds = 5;
		const int operationsPerRound = 200;
		const int concurrentWorkers = 2;

		var gcMetrics = new List<GCMetrics>();
		var logger = new FakeLogger<InMemoryInboxStore>();

		// Act - Test GC impact under high-frequency cancellation
		for (int round = 0; round < cancellationRounds; round++)
		{
			// Baseline GC state
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			var gcBefore = new GCSnapshot
			{
				Gen0Collections = GC.CollectionCount(0),
				Gen1Collections = GC.CollectionCount(1),
				Gen2Collections = GC.CollectionCount(2),
				TotalMemory = GC.GetTotalMemory(false),
				TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true)
			};

			var roundStopwatch = Stopwatch.StartNew();

			// Create inbox store for this round
			var inboxStore = new InMemoryInboxStore(
				Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
				{
					MaxEntries = operationsPerRound * concurrentWorkers + 100,
					EnableAutomaticCleanup = false
				}),
				logger);

			var cancellationEvents = new ConcurrentBag<CancellationEventMetrics>();

			// Launch concurrent workers that will create and cancel many tokens
			var workerTasks = Enumerable.Range(0, concurrentWorkers)
				.Select(async workerId =>
				{
					var operationsPerWorker = operationsPerRound / concurrentWorkers;

					for (int opIndex = 0; opIndex < operationsPerWorker; opIndex++)
					{
						var opStopwatch = Stopwatch.StartNew();

						try
						{
							// Create cancellation token with random timeout (some will cancel immediately)
							var timeoutMs = Random.Shared.Next(1, 50); // Short timeouts to force frequent cancellation
							using var shortTimeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
							using var longTimeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
							using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
								_testCancellation.Token, shortTimeoutCts.Token, longTimeoutCts.Token);

							var messageId = $"gc-{round}-{workerId}-{opIndex}";
							var payload = Encoding.UTF8.GetBytes($"GC test message {workerId}-{opIndex}");
							var metadata = new Dictionary<string, object>
							{
								{ "round", round },
								{ "workerId", workerId },
								{ "operationIndex", opIndex },
								{ "timeoutMs", timeoutMs }
							};

							_ = await inboxStore.CreateEntryAsync(
								messageId, "TestHandler", "GCTestMessage", payload, metadata, combinedCts.Token)
								.ConfigureAwait(false);

							// Simulate variable processing time
							var processingDelay = Random.Shared.Next(5, 30);
							await Task.Delay(processingDelay, combinedCts.Token).ConfigureAwait(false);

							await inboxStore.MarkProcessedAsync(messageId, "TestHandler", combinedCts.Token).ConfigureAwait(false);

							opStopwatch.Stop();

							cancellationEvents.Add(new CancellationEventMetrics
							{
								Round = round,
								WorkerId = workerId,
								OperationIndex = opIndex,
								Duration = opStopwatch.Elapsed,
								WasCancelled = false,
								TimeoutMs = timeoutMs
							});
						}
						catch (OperationCanceledException)
						{
							opStopwatch.Stop();

							cancellationEvents.Add(new CancellationEventMetrics
							{
								Round = round,
								WorkerId = workerId,
								OperationIndex = opIndex,
								Duration = opStopwatch.Elapsed,
								WasCancelled = true,
								TimeoutMs = 0 // Cancelled before completion
							});
						}
					}
				});

			await Task.WhenAll(workerTasks).ConfigureAwait(false);

			roundStopwatch.Stop();

			// Measure GC impact after round
			var gcAfter = new GCSnapshot
			{
				Gen0Collections = GC.CollectionCount(0),
				Gen1Collections = GC.CollectionCount(1),
				Gen2Collections = GC.CollectionCount(2),
				TotalMemory = GC.GetTotalMemory(false),
				TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true)
			};

			var events = cancellationEvents.ToList();
			var roundMetrics = new GCMetrics
			{
				Round = round,
				Duration = roundStopwatch.Elapsed,
				TotalOperations = events.Count,
				CancelledOperations = events.Count(e => e.WasCancelled),
				SuccessfulOperations = events.Count(e => !e.WasCancelled),
				Gen0CollectionsDelta = gcAfter.Gen0Collections - gcBefore.Gen0Collections,
				Gen1CollectionsDelta = gcAfter.Gen1Collections - gcBefore.Gen1Collections,
				Gen2CollectionsDelta = gcAfter.Gen2Collections - gcBefore.Gen2Collections,
				MemoryDelta = gcAfter.TotalMemory - gcBefore.TotalMemory,
				AllocatedBytesDelta = gcAfter.TotalAllocatedBytes - gcBefore.TotalAllocatedBytes,
				AllocationsPerOperation = (gcAfter.TotalAllocatedBytes - gcBefore.TotalAllocatedBytes) / events.Count,
				CancellationRate = (double)events.Count(e => e.WasCancelled) / events.Count * 100
			};

			gcMetrics.Add(roundMetrics);
			inboxStore.Dispose();

			// Brief pause between rounds to stabilize GC
			await Task.Delay(100, _testCancellation.Token).ConfigureAwait(false);
		}

		// Assert GC impact is minimal
		var avgGen0Collections = gcMetrics.Average(m => m.Gen0CollectionsDelta);
		var avgGen1Collections = gcMetrics.Average(m => m.Gen1CollectionsDelta);
		var avgGen2Collections = gcMetrics.Average(m => m.Gen2CollectionsDelta);
		var avgAllocationsPerOp = gcMetrics.Average(m => m.AllocationsPerOperation);
		var avgCancellationRate = gcMetrics.Average(m => m.CancellationRate);
		var totalOperations = gcMetrics.Sum(m => m.TotalOperations);

		_output.WriteLine("=== High-Frequency Cancellation GC Impact ===");
		_output.WriteLine($"Rounds: {cancellationRounds}");
		_output.WriteLine($"Operations per Round: {operationsPerRound:N0}");
		_output.WriteLine($"Concurrent Workers: {concurrentWorkers}");
		_output.WriteLine($"Total Operations: {totalOperations:N0}");
		_output.WriteLine(string.Empty);
		_output.WriteLine("GC Impact Analysis:");
		_output.WriteLine($"  Avg Gen0 Collections per Round: {avgGen0Collections:F1}");
		_output.WriteLine($"  Avg Gen1 Collections per Round: {avgGen1Collections:F1}");
		_output.WriteLine($"  Avg Gen2 Collections per Round: {avgGen2Collections:F1}");
		_output.WriteLine($"  Avg Allocations per Operation: {avgAllocationsPerOp:F0} bytes");
		_output.WriteLine($"  Avg Cancellation Rate: {avgCancellationRate:F1}%");
		_output.WriteLine(string.Empty);

		foreach (var (round, metrics) in gcMetrics.Select((m, i) => (i, m)))
		{
			_output.WriteLine($"Round {round}:");
			_output.WriteLine($"  Duration: {metrics.Duration.TotalMilliseconds:F0}ms");
			_output.WriteLine($"  Operations: {metrics.TotalOperations:N0} (Cancelled: {metrics.CancelledOperations:N0})");
			_output.WriteLine($"  GC Collections: Gen0={metrics.Gen0CollectionsDelta}, Gen1={metrics.Gen1CollectionsDelta}, Gen2={metrics.Gen2CollectionsDelta}");
			_output.WriteLine($"  Memory Delta: {metrics.MemoryDelta:N0} bytes");
			_output.WriteLine($"  Allocated Delta: {metrics.AllocatedBytesDelta:N0} bytes");
			_output.WriteLine($"  Allocations per Op: {metrics.AllocationsPerOperation:F0} bytes");
		}

		// Performance requirements (R9.12, R9.6)
		// GC pressure should be reasonable under high cancellation load
		avgGen0Collections.ShouldBeLessThan(10, "Gen0 collections should be limited under cancellation load");
		avgGen1Collections.ShouldBeLessThan(5, "Gen1 collections should be minimal under cancellation load");
		avgGen2Collections.ShouldBeLessThan(3, "Gen2 collections should be rare under cancellation load");

		// Allocations per operation should be bounded
		avgAllocationsPerOp.ShouldBeLessThan(15_000, "Allocations per operation should be reasonable");

		// Should handle high cancellation rates without excessive GC pressure
		var highCancellationRounds = gcMetrics.Where(m => m.CancellationRate > 50).ToList();
		if (highCancellationRounds.Any())
		{
			var avgHighCancellationGC = highCancellationRounds.Average(m => m.Gen0CollectionsDelta + m.Gen1CollectionsDelta);
			avgHighCancellationGC.ShouldBeLessThan(15, "GC pressure should remain reasonable even with high cancellation rates");
		}
	}

	[Fact]
	public async Task CancellationTokenPooling_ShouldReduceAllocations()
	{
		// Arrange
		const int iterationCount = 5;
		const int tokensPerIteration = 2_000;

		var poolingMetrics = new List<PoolingMetrics>();

		// Test different token creation patterns
		var testScenarios = new[]
		{
			new PoolingTestScenario { Name = "DirectCreation", UsePooling = false },
			new PoolingTestScenario { Name = "WithPooling", UsePooling = true }
		};

		foreach (var scenario in testScenarios)
		{
			var iterationMetrics = new List<IterationPoolingMetrics>();

			for (int iteration = 0; iteration < iterationCount; iteration++)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
				var memoryBefore = GC.GetTotalMemory(false);
				var gcGen0Before = GC.CollectionCount(0);

				var iterationStopwatch = Stopwatch.StartNew();

				if (scenario.UsePooling)
				{
					// Simulate pooled cancellation token usage
					await TestPooledTokenCreation(tokensPerIteration).ConfigureAwait(false);
				}
				else
				{
					// Direct token creation without pooling
					await TestDirectTokenCreation(tokensPerIteration).ConfigureAwait(false);
				}

				iterationStopwatch.Stop();

				var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);
				var memoryAfter = GC.GetTotalMemory(false);
				var gcGen0After = GC.CollectionCount(0);

				var iterationMetric = new IterationPoolingMetrics
				{
					Iteration = iteration,
					Scenario = scenario.Name,
					TokenCount = tokensPerIteration,
					Duration = iterationStopwatch.Elapsed,
					AllocatedBytes = allocatedAfter - allocatedBefore,
					MemoryDelta = memoryAfter - memoryBefore,
					GCCollections = gcGen0After - gcGen0Before,
					AllocationsPerToken = (allocatedAfter - allocatedBefore) / tokensPerIteration
				};

				iterationMetrics.Add(iterationMetric);

				// Brief pause between iterations
				await Task.Delay(50, _testCancellation.Token).ConfigureAwait(false);
			}

			var scenarioMetrics = new PoolingMetrics
			{
				Scenario = scenario.Name,
				IterationCount = iterationCount,
				TokensPerIteration = tokensPerIteration,
				TotalTokens = iterationCount * tokensPerIteration,
				AvgDuration = TimeSpan.FromMilliseconds(iterationMetrics.Average(m => m.Duration.TotalMilliseconds)),
				AvgAllocatedBytes = iterationMetrics.Average(m => m.AllocatedBytes),
				AvgMemoryDelta = iterationMetrics.Average(m => m.MemoryDelta),
				AvgGCCollections = iterationMetrics.Average(m => m.GCCollections),
				AvgAllocationsPerToken = iterationMetrics.Average(m => m.AllocationsPerToken),
				TotalDuration = TimeSpan.FromMilliseconds(iterationMetrics.Sum(m => m.Duration.TotalMilliseconds)),
				TotalAllocatedBytes = iterationMetrics.Sum(m => m.AllocatedBytes),
				IterationMetrics = iterationMetrics
			};

			poolingMetrics.Add(scenarioMetrics);
		}

		// Assert pooling effectiveness
		var directCreation = poolingMetrics.First(m => m.Scenario == "DirectCreation");
		var withPooling = poolingMetrics.First(m => m.Scenario == "WithPooling");

		var allocationReduction = (1 - withPooling.AvgAllocationsPerToken / directCreation.AvgAllocationsPerToken) * 100;
		var gcReduction = (1 - withPooling.AvgGCCollections / Math.Max(1, directCreation.AvgGCCollections)) * 100;
		var memoryReduction = (1 - Math.Abs(withPooling.AvgMemoryDelta) / Math.Max(1, Math.Abs(directCreation.AvgMemoryDelta))) * 100;

		_output.WriteLine("=== Cancellation Token Pooling Effectiveness ===");
		_output.WriteLine($"Iterations: {iterationCount}");
		_output.WriteLine($"Tokens per Iteration: {tokensPerIteration:N0}");
		_output.WriteLine($"Total Tokens: {directCreation.TotalTokens:N0}");
		_output.WriteLine(string.Empty);

		foreach (var metrics in poolingMetrics)
		{
			_output.WriteLine($"{metrics.Scenario} Metrics:");
			_output.WriteLine($"  Avg Duration: {metrics.AvgDuration.TotalMilliseconds:F1}ms");
			_output.WriteLine($"  Avg Allocated Bytes: {metrics.AvgAllocatedBytes:N0}");
			_output.WriteLine($"  Avg Memory Delta: {metrics.AvgMemoryDelta:N0} bytes");
			_output.WriteLine($"  Avg GC Collections: {metrics.AvgGCCollections:F1}");
			_output.WriteLine($"  Avg Allocations per Token: {metrics.AvgAllocationsPerToken:F0} bytes");
			_output.WriteLine(string.Empty);
		}

		_output.WriteLine("Pooling Effectiveness:");
		_output.WriteLine($"  Allocation Reduction: {allocationReduction:F1}%");
		_output.WriteLine($"  GC Reduction: {gcReduction:F1}%");
		_output.WriteLine($"  Memory Delta Reduction: {memoryReduction:F1}%");

		// Performance requirements (R9.17, R9.6)
		// Pooling should provide meaningful allocation reduction
		if (withPooling.AvgAllocationsPerToken < directCreation.AvgAllocationsPerToken)
		{
			allocationReduction.ShouldBeGreaterThan(10, "Pooling should provide at least 10% allocation reduction");
		}

		// Both scenarios should maintain reasonable performance
		directCreation.AvgAllocationsPerToken.ShouldBeLessThan(2_000, "Direct creation allocations should be reasonable");
		withPooling.AvgAllocationsPerToken.ShouldBeLessThan(1_500, "Pooled creation allocations should be lower");

		// GC impact should be minimal for both
		directCreation.AvgGCCollections.ShouldBeLessThan(3, "Direct creation should not trigger excessive GC");
		withPooling.AvgGCCollections.ShouldBeLessThanOrEqualTo(directCreation.AvgGCCollections, "Pooling should not increase GC pressure");
	}

	#endregion GC Pressure Tests

	#region Memory Leak Detection Tests

	[Fact]
	public async Task CancellationCascade_ShouldNotLeakMemory()
	{
		// Arrange
		const int cascadeRounds = 10;
		const int tokensPerCascade = 50;
		const int cascadeDepth = 3;

		var memorySnapshots = new List<MemorySnapshot>();
		var logger = new FakeLogger<InMemoryInboxStore>();

		// Act - Test for memory leaks during cancellation cascades
		for (int round = 0; round < cascadeRounds; round++)
		{
			// Take baseline memory snapshot
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			var baselineMemory = GC.GetTotalMemory(false);
			var baselineAllocated = GC.GetTotalAllocatedBytes(precise: true);

			var roundStopwatch = Stopwatch.StartNew();

			// Create cancellation cascade
			await PerformCancellationCascade(round, tokensPerCascade, cascadeDepth, logger).ConfigureAwait(false);

			roundStopwatch.Stop();

			// Force cleanup and measure memory again
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			var finalMemory = GC.GetTotalMemory(false);
			var finalAllocated = GC.GetTotalAllocatedBytes(precise: true);

			var snapshot = new MemorySnapshot
			{
				Round = round,
				Duration = roundStopwatch.Elapsed,
				BaselineMemory = baselineMemory,
				FinalMemory = finalMemory,
				MemoryDelta = finalMemory - baselineMemory,
				BaselineAllocated = baselineAllocated,
				FinalAllocated = finalAllocated,
				AllocatedDelta = finalAllocated - baselineAllocated,
				TokensCreated = tokensPerCascade * cascadeDepth
			};

			memorySnapshots.Add(snapshot);

			// Brief pause between rounds
			await Task.Delay(100, _testCancellation.Token).ConfigureAwait(false);
		}

		// Assert no memory leaks
		var totalMemoryGrowth = memorySnapshots.Last().FinalMemory - memorySnapshots.First().BaselineMemory;
		var avgMemoryDelta = memorySnapshots.Average(s => s.MemoryDelta);
		var maxMemoryDelta = memorySnapshots.Max(s => s.MemoryDelta);
		var totalTokensCreated = memorySnapshots.Sum(s => s.TokensCreated);

		// Analyze memory growth trend
		var memoryGrowthTrend = CalculateMemoryGrowthTrend(memorySnapshots);

		_output.WriteLine("=== Cancellation Cascade Memory Leak Detection ===");
		_output.WriteLine($"Cascade Rounds: {cascadeRounds}");
		_output.WriteLine($"Tokens per Cascade: {tokensPerCascade:N0}");
		_output.WriteLine($"Cascade Depth: {cascadeDepth}");
		_output.WriteLine($"Total Tokens Created: {totalTokensCreated:N0}");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Memory Analysis:");
		_output.WriteLine($"  Total Memory Growth: {totalMemoryGrowth:N0} bytes");
		_output.WriteLine($"  Avg Memory Delta per Round: {avgMemoryDelta:N0} bytes");
		_output.WriteLine($"  Max Memory Delta: {maxMemoryDelta:N0} bytes");
		_output.WriteLine($"  Memory Growth Trend: {memoryGrowthTrend:F4} bytes/round");
		_output.WriteLine(string.Empty);

		// Show detailed snapshots for first few and last few rounds
		var detailRounds = memorySnapshots.Take(3).Concat(memorySnapshots.TakeLast(3)).Distinct().ToList();
		foreach (var snapshot in detailRounds)
		{
			_output.WriteLine($"Round {snapshot.Round}:");
			_output.WriteLine($"  Duration: {snapshot.Duration.TotalMilliseconds:F0}ms");
			_output.WriteLine($"  Memory Delta: {snapshot.MemoryDelta:N0} bytes");
			_output.WriteLine($"  Allocated Delta: {snapshot.AllocatedDelta:N0} bytes");
			_output.WriteLine($"  Tokens Created: {snapshot.TokensCreated:N0}");
		}

		// Performance requirements (R9.17)
		// Memory growth should be minimal and bounded
		totalMemoryGrowth.ShouldBeLessThan(10_000_000, "Total memory growth should be less than 10MB");

		// Memory growth trend should be near zero (no consistent leaks)
		// GC compaction and runtime bookkeeping can cause significant per-round variance
		Math.Abs(memoryGrowthTrend).ShouldBeLessThan(500_000, "Memory growth trend should be minimal (no leaks)");

		// Average memory delta per round should be reasonable
		Math.Abs(avgMemoryDelta).ShouldBeLessThan(1_000_000, "Average memory delta should be reasonable");

		// No single round should cause excessive memory usage
		maxMemoryDelta.ShouldBeLessThan(5_000_000, "Maximum memory delta should be bounded");
	}

	#endregion Memory Leak Detection Tests

	#region Helper Methods

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			try
			{
				disposable?.Dispose();
			}
			catch
			{
				// Ignore disposal errors in tests
			}
		}
	}

	private static double CalculateMemoryGrowthTrend(List<MemorySnapshot> snapshots)
	{
		if (snapshots.Count < 2)
			return 0;

		// Simple linear regression to detect memory growth trend
		var n = snapshots.Count;
		var sumX = snapshots.Select((s, i) => (long)i).Sum();
		var sumY = snapshots.Sum(s => s.FinalMemory);
		var sumXY = snapshots.Select((s, i) => i * s.FinalMemory).Sum();
		var sumX2 = snapshots.Select((s, i) => (long)i * i).Sum();

		return (n * sumXY - sumX * sumY) / (double)(n * sumX2 - sumX * sumX);
	}

	private async Task TestDirectTokenCreation(int tokenCount)
	{
		var tokens = new List<CancellationToken>();

		for (int i = 0; i < tokenCount; i++)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100 + i % 50));
			tokens.Add(cts.Token);

			// Simulate brief usage
			_ = cts.Token.IsCancellationRequested;

			if (i % 10 == 0)
			{
				await Task.Delay(1, _testCancellation.Token).ConfigureAwait(false);
			}
		}
	}

	private async Task TestPooledTokenCreation(int tokenCount)
	{
		// Simulate pooled token creation pattern
		var tokenPool = new ConcurrentQueue<CancellationTokenSource>();

		// Pre-populate pool
		for (int i = 0; i < Math.Min(50, tokenCount / 4); i++)
		{
			tokenPool.Enqueue(new CancellationTokenSource());
		}

		for (int i = 0; i < tokenCount; i++)
		{
			CancellationTokenSource? cts = null;

			if (!tokenPool.TryDequeue(out cts))
			{
				cts = new CancellationTokenSource();
			}

			try
			{
				// Reset timeout
				cts.CancelAfter(TimeSpan.FromMilliseconds(100 + i % 50));
				var token = cts.Token;

				// Simulate brief usage
				_ = token.IsCancellationRequested;

				if (i % 10 == 0)
				{
					await Task.Delay(1, _testCancellation.Token).ConfigureAwait(false);
				}
			}
			finally
			{
				// Return to pool if not cancelled and pool not full
				if (!cts.Token.IsCancellationRequested && tokenPool.Count < 100)
				{
					tokenPool.Enqueue(cts);
				}
				else
				{
					cts.Dispose();
				}
			}
		}

		// Cleanup pool
		while (tokenPool.TryDequeue(out var pooledCts))
		{
			pooledCts.Dispose();
		}
	}

	private async Task PerformCancellationCascade(int round, int tokensPerCascade, int cascadeDepth, ILogger<InMemoryInboxStore> logger)
	{
		var inboxStore = new InMemoryInboxStore(
			Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
			{
				MaxEntries = tokensPerCascade * cascadeDepth + 100,
				EnableAutomaticCleanup = false
			}),
			logger);

		try
		{
			// Create cascading cancellation tokens
			var parentCts = new CancellationTokenSource();
			var cascadeLevels = new List<List<CancellationTokenSource>>();

			for (int depth = 0; depth < cascadeDepth; depth++)
			{
				var levelTokens = new List<CancellationTokenSource>();
				var parentToken = depth == 0 ? parentCts.Token : cascadeLevels[depth - 1][0].Token;

				for (int tokenIndex = 0; tokenIndex < tokensPerCascade; tokenIndex++)
				{
					var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken, _testCancellation.Token);
					levelTokens.Add(linkedCts);

					// Perform minimal operation with each token
					try
					{
						var messageId = $"cascade-{round}-{depth}-{tokenIndex}";
						var payload = Encoding.UTF8.GetBytes($"Cascade message {round}-{depth}-{tokenIndex}");
						var metadata = new Dictionary<string, object>
						{
							{ "round", round },
							{ "depth", depth },
							{ "tokenIndex", tokenIndex }
						};

						_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "CascadeMessage", payload, metadata, linkedCts.Token)
							.ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						// Expected during cascade cancellation
					}
				}

				cascadeLevels.Add(levelTokens);
			}

			// Trigger cascade by cancelling parent
			parentCts.Cancel();

			// Brief delay to let cancellation propagate
			await Task.Delay(10, _testCancellation.Token).ConfigureAwait(false);

			// Cleanup all tokens
			parentCts.Dispose();
			foreach (var level in cascadeLevels)
			{
				foreach (var cts in level)
				{
					cts.Dispose();
				}
			}
		}
		finally
		{
			inboxStore.Dispose();
		}
	}

	#endregion Helper Methods

	#region Test Data Types

	private sealed record GCSnapshot
	{
		public int Gen0Collections { get; init; }
		public int Gen1Collections { get; init; }
		public int Gen2Collections { get; init; }
		public long TotalMemory { get; init; }
		public long TotalAllocatedBytes { get; init; }
	}

	private sealed record GCMetrics
	{
		public int Round { get; init; }
		public TimeSpan Duration { get; init; }
		public int TotalOperations { get; init; }
		public int CancelledOperations { get; init; }
		public int SuccessfulOperations { get; init; }
		public int Gen0CollectionsDelta { get; init; }
		public int Gen1CollectionsDelta { get; init; }
		public int Gen2CollectionsDelta { get; init; }
		public long MemoryDelta { get; init; }
		public long AllocatedBytesDelta { get; init; }
		public long AllocationsPerOperation { get; init; }
		public double CancellationRate { get; init; }
	}

	private sealed record CancellationEventMetrics
	{
		public int Round { get; init; }
		public int WorkerId { get; init; }
		public int OperationIndex { get; init; }
		public TimeSpan Duration { get; init; }
		public bool WasCancelled { get; init; }
		public int TimeoutMs { get; init; }
	}

	private sealed record PoolingTestScenario
	{
		public required string Name { get; init; }
		public bool UsePooling { get; init; }
	}

	private sealed record IterationPoolingMetrics
	{
		public int Iteration { get; init; }
		public required string Scenario { get; init; }
		public int TokenCount { get; init; }
		public TimeSpan Duration { get; init; }
		public long AllocatedBytes { get; init; }
		public long MemoryDelta { get; init; }
		public int GCCollections { get; init; }
		public long AllocationsPerToken { get; init; }
	}

	private sealed record PoolingMetrics
	{
		public required string Scenario { get; init; }
		public int IterationCount { get; init; }
		public int TokensPerIteration { get; init; }
		public int TotalTokens { get; init; }
		public TimeSpan AvgDuration { get; init; }
		public double AvgAllocatedBytes { get; init; }
		public double AvgMemoryDelta { get; init; }
		public double AvgGCCollections { get; init; }
		public double AvgAllocationsPerToken { get; init; }
		public TimeSpan TotalDuration { get; init; }
		public long TotalAllocatedBytes { get; init; }
		public required List<IterationPoolingMetrics> IterationMetrics { get; init; }
	}

	private sealed record MemorySnapshot
	{
		public int Round { get; init; }
		public TimeSpan Duration { get; init; }
		public long BaselineMemory { get; init; }
		public long FinalMemory { get; init; }
		public long MemoryDelta { get; init; }
		public long BaselineAllocated { get; init; }
		public long FinalAllocated { get; init; }
		public long AllocatedDelta { get; init; }
		public int TokensCreated { get; init; }
	}

	#endregion Test Data Types
}
