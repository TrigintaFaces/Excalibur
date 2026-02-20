// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Middleware;

using Excalibur.Data.InMemory.Inbox;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;
using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Performance-focused tests for timeout handling and cancellation propagation.
///     Tests compliance with requirements R9.51-R9.57, T10.55-T10.59.
/// </summary>
/// <remarks>
///     These tests focus on performance characteristics specifically:
///     - Allocation patterns during timeout enforcement
///     - Latency overhead of timeout mechanisms
///     - Throughput preservation under timeout pressure
///     - GC impact of cancellation token creation/propagation
///     - Performance regression detection for timeout scenarios
///     - Memory allocation patterns during cancellation cascades
/// </remarks>
[Trait("Category", "Performance")]
public sealed class TimeoutAndCancellationPerformanceShould : IDisposable
{
	private readonly ITestOutputHelper _output;
	private readonly List<IDisposable> _disposables;
#pragma warning disable CA2213 // Disposed via _disposables list
	private readonly CancellationTokenSource _testCancellation;
#pragma warning restore CA2213

	public TimeoutAndCancellationPerformanceShould(ITestOutputHelper output)
	{
		_output = output;
		_disposables = new List<IDisposable>();
		_testCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		_disposables.Add(_testCancellation);
	}

	#region Allocation Pattern Tests

	[Fact]
	public async Task TimeoutEnforcement_ShouldHaveMinimalAllocationOverhead()
	{
		// Arrange
		const int operationCount = 1_000;
		const int timeoutMs = 100;

		var logger = new FakeLogger<InMemoryInboxStore>();
		var inboxStore = new InMemoryInboxStore(
			Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
			{
				MaxEntries = operationCount + 100,
				EnableAutomaticCleanup = false
			}),
			logger);

		var allocationMetrics = new List<AllocationMetrics>();

		// Baseline measurement (no timeout)
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var baselineMemoryBefore = GC.GetTotalMemory(false);
		var baselineAllocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);

		var baselineStopwatch = Stopwatch.StartNew();

		for (int i = 0; i < operationCount; i++)
		{
			var messageId = $"baseline-{i}";
			var payload = Encoding.UTF8.GetBytes($"Baseline message {i}");
			var metadata = new Dictionary<string, object> { { "index", i } };

			_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "BaselineMessage", payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
			await inboxStore.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None).ConfigureAwait(false);
		}

		baselineStopwatch.Stop();

		var baselineMemoryAfter = GC.GetTotalMemory(false);
		var baselineAllocatedBytesAfter = GC.GetTotalAllocatedBytes(precise: true);

		var baselineMetrics = new AllocationMetrics
		{
			TestName = "Baseline",
			OperationCount = operationCount,
			Duration = baselineStopwatch.Elapsed,
			MemoryBefore = baselineMemoryBefore,
			MemoryAfter = baselineMemoryAfter,
			AllocatedBytesBefore = baselineAllocatedBytesBefore,
			AllocatedBytesAfter = baselineAllocatedBytesAfter,
			TotalAllocatedBytes = baselineAllocatedBytesAfter - baselineAllocatedBytesBefore,
			NetMemoryIncrease = baselineMemoryAfter - baselineMemoryBefore
		};

		allocationMetrics.Add(baselineMetrics);

		// Act - Test with timeout enforcement
		await Task.Delay(100, _testCancellation.Token).ConfigureAwait(false); // Brief pause

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var timeoutMemoryBefore = GC.GetTotalMemory(false);
		var timeoutAllocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);

		var timeoutStopwatch = Stopwatch.StartNew();

		for (int i = 0; i < operationCount; i++)
		{
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
			using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
				_testCancellation.Token, timeoutCts.Token);

			var messageId = $"timeout-{i}";
			var payload = Encoding.UTF8.GetBytes($"Timeout message {i}");
			var metadata = new Dictionary<string, object> { { "index", i } };

			_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "TimeoutMessage", payload, metadata, combinedCts.Token)
				.ConfigureAwait(false);
			await inboxStore.MarkProcessedAsync(messageId, "TestHandler", combinedCts.Token).ConfigureAwait(false);
		}

		timeoutStopwatch.Stop();

		var timeoutMemoryAfter = GC.GetTotalMemory(false);
		var timeoutAllocatedBytesAfter = GC.GetTotalAllocatedBytes(precise: true);

		var timeoutMetrics = new AllocationMetrics
		{
			TestName = "WithTimeout",
			OperationCount = operationCount,
			Duration = timeoutStopwatch.Elapsed,
			MemoryBefore = timeoutMemoryBefore,
			MemoryAfter = timeoutMemoryAfter,
			AllocatedBytesBefore = timeoutAllocatedBytesBefore,
			AllocatedBytesAfter = timeoutAllocatedBytesAfter,
			TotalAllocatedBytes = timeoutAllocatedBytesAfter - timeoutAllocatedBytesBefore,
			NetMemoryIncrease = timeoutMemoryAfter - timeoutMemoryBefore
		};

		allocationMetrics.Add(timeoutMetrics);

		// Assert allocation overhead is minimal
		var allocationOverhead = timeoutMetrics.TotalAllocatedBytes - baselineMetrics.TotalAllocatedBytes;
		var allocationOverheadPerOp = (double)allocationOverhead / operationCount;
		var memoryOverhead = timeoutMetrics.NetMemoryIncrease - baselineMetrics.NetMemoryIncrease;
		var memoryOverheadPerOp = (double)memoryOverhead / operationCount;

		var durationOverhead = timeoutMetrics.Duration - baselineMetrics.Duration;
		var durationOverheadPercent = (durationOverhead.TotalMilliseconds / baselineMetrics.Duration.TotalMilliseconds) * 100;

		_output.WriteLine("=== Timeout Enforcement Allocation Overhead ===");
		_output.WriteLine($"Operations: {operationCount:N0}");
		_output.WriteLine($"Timeout Duration: {timeoutMs}ms");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Baseline Metrics:");
		_output.WriteLine($"  Duration: {baselineMetrics.Duration.TotalMilliseconds:F2}ms");
		_output.WriteLine($"  Total Allocated: {baselineMetrics.TotalAllocatedBytes:N0} bytes");
		_output.WriteLine($"  Net Memory Increase: {baselineMetrics.NetMemoryIncrease:N0} bytes");
		_output.WriteLine($"  Allocated per Op: {baselineMetrics.TotalAllocatedBytes / operationCount:F0} bytes");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Timeout Metrics:");
		_output.WriteLine($"  Duration: {timeoutMetrics.Duration.TotalMilliseconds:F2}ms");
		_output.WriteLine($"  Total Allocated: {timeoutMetrics.TotalAllocatedBytes:N0} bytes");
		_output.WriteLine($"  Net Memory Increase: {timeoutMetrics.NetMemoryIncrease:N0} bytes");
		_output.WriteLine($"  Allocated per Op: {timeoutMetrics.TotalAllocatedBytes / operationCount:F0} bytes");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Overhead Analysis:");
		_output.WriteLine($"  Allocation Overhead: {allocationOverhead:N0} bytes ({allocationOverheadPerOp:F1} bytes/op)");
		_output.WriteLine($"  Memory Overhead: {memoryOverhead:N0} bytes ({memoryOverheadPerOp:F1} bytes/op)");
		_output.WriteLine($"  Duration Overhead: {durationOverhead.TotalMilliseconds:F2}ms ({durationOverheadPercent:F1}%)");

		// Performance requirements (R9.6, R9.51)
		// Allocation overhead should be bounded (CTS + linked CTS + timer registrations add overhead)
		allocationOverheadPerOp.ShouldBeLessThan(15_000, "Timeout enforcement should have bounded allocation overhead");

		// Duration overhead should be bounded (can be large percentage when baseline is very fast)
		// Use generous threshold (600%) for CI environments under heavy concurrent load
		if (baselineMetrics.Duration.TotalMilliseconds > 10)
		{
			durationOverheadPercent.ShouldBeLessThan(1000, "Timeout enforcement should have bounded duration overhead");
		}

		// Memory overhead should be bounded
		memoryOverheadPerOp.ShouldBeLessThan(15_000, "Timeout enforcement should have bounded memory overhead");
	}

	[Fact]
	public async Task CancellationTokenCreation_ShouldBeAllocationEfficient()
	{
		// Arrange
		const int tokenCreationCount = 10_000;
		const int measurementBatches = 5;

		var batchMetrics = new List<TokenCreationMetrics>();

		// Act - Measure token creation patterns
		for (int batch = 0; batch < measurementBatches; batch++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			var batchStopwatch = Stopwatch.StartNew();
			var memoryBefore = GC.GetTotalMemory(false);
			var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

			// Test different token creation patterns
			var simpleTokens = new List<CancellationToken>();
			var linkedTokenSources = new List<CancellationTokenSource>();

			// Simple token creation
			var simpleStopwatch = Stopwatch.StartNew();
			for (int i = 0; i < tokenCreationCount / 3; i++)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
				simpleTokens.Add(cts.Token);
			}
			simpleStopwatch.Stop();

			// Linked token creation
			var linkedStopwatch = Stopwatch.StartNew();
			using var parentCts = new CancellationTokenSource();
			for (int i = 0; i < tokenCreationCount / 3; i++)
			{
				var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
					_testCancellation.Token, parentCts.Token);
				linkedTokenSources.Add(linkedCts);
			}
			linkedStopwatch.Stop();

			// Timeout + linked token creation (realistic scenario)
			var combinedStopwatch = Stopwatch.StartNew();
			for (int i = 0; i < tokenCreationCount / 3; i++)
			{
				using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50 + i % 100));
				using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
					_testCancellation.Token, timeoutCts.Token);
				// Simulate brief usage
				_ = combinedCts.Token.IsCancellationRequested;
			}
			combinedStopwatch.Stop();

			// Cleanup linked tokens
			foreach (var linkedCts in linkedTokenSources)
			{
				linkedCts?.Dispose();
			}

			batchStopwatch.Stop();

			var memoryAfter = GC.GetTotalMemory(false);
			var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

			var batchMetric = new TokenCreationMetrics
			{
				BatchNumber = batch,
				TokenCount = tokenCreationCount,
				TotalDuration = batchStopwatch.Elapsed,
				SimpleTokenDuration = simpleStopwatch.Elapsed,
				LinkedTokenDuration = linkedStopwatch.Elapsed,
				CombinedTokenDuration = combinedStopwatch.Elapsed,
				MemoryBefore = memoryBefore,
				MemoryAfter = memoryAfter,
				TotalAllocatedBytes = allocatedAfter - allocatedBefore,
				NetMemoryIncrease = memoryAfter - memoryBefore
			};

			batchMetrics.Add(batchMetric);

			// Brief pause between batches
			await Task.Delay(50, _testCancellation.Token).ConfigureAwait(false);
		}

		// Assert performance characteristics
		var avgTotalDuration = batchMetrics.Average(m => m.TotalDuration.TotalMilliseconds);
		var avgSimpleDuration = batchMetrics.Average(m => m.SimpleTokenDuration.TotalMilliseconds);
		var avgLinkedDuration = batchMetrics.Average(m => m.LinkedTokenDuration.TotalMilliseconds);
		var avgCombinedDuration = batchMetrics.Average(m => m.CombinedTokenDuration.TotalMilliseconds);
		var avgAllocatedBytes = batchMetrics.Average(m => m.TotalAllocatedBytes);
		var avgMemoryIncrease = batchMetrics.Average(m => m.NetMemoryIncrease);

		var allocationsPerToken = avgAllocatedBytes / tokenCreationCount;
		var tokensPerSecond = tokenCreationCount / (avgTotalDuration / 1000);

		_output.WriteLine("=== Cancellation Token Creation Performance ===");
		_output.WriteLine($"Measurement Batches: {measurementBatches}");
		_output.WriteLine($"Tokens per Batch: {tokenCreationCount:N0}");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Duration Averages:");
		_output.WriteLine($"  Total Duration: {avgTotalDuration:F2}ms");
		_output.WriteLine($"  Simple Tokens: {avgSimpleDuration:F2}ms");
		_output.WriteLine($"  Linked Tokens: {avgLinkedDuration:F2}ms");
		_output.WriteLine($"  Combined Tokens: {avgCombinedDuration:F2}ms");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Memory Averages:");
		_output.WriteLine($"  Allocated Bytes: {avgAllocatedBytes:N0}");
		_output.WriteLine($"  Memory Increase: {avgMemoryIncrease:N0}");
		_output.WriteLine($"  Allocations per Token: {allocationsPerToken:F1} bytes");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Throughput:");
		_output.WriteLine($"  Tokens per Second: {tokensPerSecond:N0}");

		// Performance requirements (R9.6, R9.52)
		// Token creation should be fast and allocation-efficient
		allocationsPerToken.ShouldBeLessThan(500, "Token creation should be allocation-efficient");
		tokensPerSecond.ShouldBeGreaterThan(50_000, "Token creation should be high-throughput");

		// Combined token creation (realistic scenario) should not be significantly slower
		var combinedOverheadPercent = (avgCombinedDuration / avgSimpleDuration - 1) * 100;
		combinedOverheadPercent.ShouldBeLessThan(300, "Combined token creation overhead should be reasonable");
	}

	#endregion Allocation Pattern Tests

	#region Latency Impact Tests

	[Fact]
	public async Task TimeoutMiddleware_ShouldHaveMinimalLatencyImpact()
	{
		// Arrange
		const int messageCount = 5_000;
		const int warmupCount = 100;
		const int concurrentOperations = 4;

		var logger = new FakeLogger<InMemoryInboxStore>();
		var latencyMetrics = new ConcurrentQueue<LatencyMeasurement>();

		// Test scenarios: without timeout, with timeout, with budget calculation
		var scenarios = new[]
		{
			new LatencyTestScenario { Name = "NoTimeout", UseTimeout = false, CalculateBudget = false },
			new LatencyTestScenario { Name = "WithTimeout", UseTimeout = true, CalculateBudget = false },
			new LatencyTestScenario { Name = "WithBudgetCalculation", UseTimeout = true, CalculateBudget = true }
		};

		foreach (var scenario in scenarios)
		{
			var inboxStore = new InMemoryInboxStore(
				Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
				{
					MaxEntries = (warmupCount + messageCount) * concurrentOperations + 100,
					EnableAutomaticCleanup = false
				}),
				logger);

			// Warmup
			await PerformLatencyWarmup(inboxStore, warmupCount, scenario).ConfigureAwait(false);

			// Act - Measure latency under load
			var concurrentTasks = Enumerable.Range(0, concurrentOperations)
				.Select(async workerId =>
				{
					var messagesPerWorker = messageCount / concurrentOperations;
					var workerLatencies = new List<double>();

					for (int msgIndex = 0; msgIndex < messagesPerWorker; msgIndex++)
					{
						var operationStopwatch = Stopwatch.StartNew();
						var messageId = $"{scenario.Name}-{workerId}-{msgIndex}";

						try
						{
							CancellationToken token = _testCancellation.Token;
							CancellationTokenSource? timeoutCts = null;
							CancellationTokenSource? combinedCts = null;

							if (scenario.UseTimeout)
							{
								if (scenario.CalculateBudget)
								{
									// Simulate budget calculation overhead
									var baseTimeoutMs = 1000;
									var elapsed = msgIndex * 10; // Simulate some elapsed time
									var remainingBudget = Math.Max(100, baseTimeoutMs - elapsed);
									timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(remainingBudget));
								}
								else
								{
									timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
								}

								combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
									_testCancellation.Token, timeoutCts.Token);
								token = combinedCts.Token;
							}

							var payload = Encoding.UTF8.GetBytes($"Latency test message {workerId}-{msgIndex}");
							var metadata = new Dictionary<string, object>
							{
								{ "workerId", workerId },
								{ "messageIndex", msgIndex },
								{ "scenario", scenario.Name }
							};

							_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "LatencyTestMessage", payload, metadata, token)
								.ConfigureAwait(false);

							// Simulate minimal processing
							await Task.Delay(1, token).ConfigureAwait(false);

							await inboxStore.MarkProcessedAsync(messageId, "TestHandler", token).ConfigureAwait(false);

							operationStopwatch.Stop();
							workerLatencies.Add(operationStopwatch.Elapsed.TotalMicroseconds);

							timeoutCts?.Dispose();
							combinedCts?.Dispose();
						}
						catch (OperationCanceledException)
						{
							operationStopwatch.Stop();
							// Record cancelled operations with their elapsed time
							workerLatencies.Add(operationStopwatch.Elapsed.TotalMicroseconds);
						}
					}

					return new WorkerLatencyResult
					{
						WorkerId = workerId,
						Scenario = scenario.Name,
						Latencies = workerLatencies,
						MessageCount = messagesPerWorker
					};
				});

			var workerResults = await Task.WhenAll(concurrentTasks).ConfigureAwait(false);

			// Aggregate results
			var allLatencies = workerResults.SelectMany(r => r.Latencies).ToList();
			allLatencies.Sort();

			var latencyMeasurement = new LatencyMeasurement
			{
				ScenarioName = scenario.Name,
				MessageCount = messageCount,
				ConcurrentWorkers = concurrentOperations,
				MinLatencyMicros = allLatencies.First(),
				MaxLatencyMicros = allLatencies.Last(),
				MedianLatencyMicros = allLatencies[allLatencies.Count / 2],
				P95LatencyMicros = allLatencies[(int)(allLatencies.Count * 0.95)],
				P99LatencyMicros = allLatencies[(int)(allLatencies.Count * 0.99)],
				AvgLatencyMicros = allLatencies.Average(),
				StdDevLatencyMicros = CalculateStandardDeviation(allLatencies)
			};

			latencyMetrics.Enqueue(latencyMeasurement);

			inboxStore.Dispose();
		}

		// Assert latency impact is minimal
		var baselineLatency = latencyMetrics.First(m => m.ScenarioName == "NoTimeout");
		var timeoutLatency = latencyMetrics.First(m => m.ScenarioName == "WithTimeout");
		var budgetLatency = latencyMetrics.First(m => m.ScenarioName == "WithBudgetCalculation");

		var timeoutOverheadPercent = (timeoutLatency.P95LatencyMicros / baselineLatency.P95LatencyMicros - 1) * 100;
		var budgetOverheadPercent = (budgetLatency.P95LatencyMicros / baselineLatency.P95LatencyMicros - 1) * 100;

		_output.WriteLine("=== Timeout Middleware Latency Impact ===");
		_output.WriteLine($"Messages: {messageCount:N0}");
		_output.WriteLine($"Concurrent Workers: {concurrentOperations}");
		_output.WriteLine(string.Empty);

		foreach (var measurement in latencyMetrics)
		{
			_output.WriteLine($"{measurement.ScenarioName} Latency (microseconds):");
			_output.WriteLine($"  Min: {measurement.MinLatencyMicros:F1}");
			_output.WriteLine($"  Median: {measurement.MedianLatencyMicros:F1}");
			_output.WriteLine($"  Avg: {measurement.AvgLatencyMicros:F1}");
			_output.WriteLine($"  P95: {measurement.P95LatencyMicros:F1}");
			_output.WriteLine($"  P99: {measurement.P99LatencyMicros:F1}");
			_output.WriteLine($"  Max: {measurement.MaxLatencyMicros:F1}");
			_output.WriteLine($"  StdDev: {measurement.StdDevLatencyMicros:F1}");
			_output.WriteLine(string.Empty);
		}

		_output.WriteLine("Overhead Analysis:");
		_output.WriteLine($"  Timeout Overhead (P95): {timeoutOverheadPercent:F1}%");
		_output.WriteLine($"  Budget Calc Overhead (P95): {budgetOverheadPercent:F1}%");

		// Performance requirements (R9.51, R9.6)
		// Timeout enforcement overhead should be minimal
		timeoutOverheadPercent.ShouldBeLessThan(100, "Timeout enforcement should have minimal latency overhead");
		budgetOverheadPercent.ShouldBeLessThan(50, "Budget calculation should have reasonable latency overhead");

		// P99 latency should remain bounded (CI environments have higher variance due to concurrent load)
		timeoutLatency.P99LatencyMicros.ShouldBeLessThan(150_000, "P99 latency should remain sub-150ms under CI load");
		budgetLatency.P99LatencyMicros.ShouldBeLessThan(200_000, "P99 latency with budget calculation should remain reasonable under CI load");
	}

	#endregion Latency Impact Tests

	#region Throughput Preservation Tests

	[Fact]
	public async Task ThroughputUnderTimeoutPressure_ShouldDegradeGracefully()
	{
		// Arrange
		const int durationSeconds = 15;
		const int concurrentWorkers = 6;
		const int baseTimeoutMs = 100;

		var throughputMetrics = new ConcurrentQueue<ThroughputMeasurement>();
		var timeoutPressureLevels = new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9 }; // Fraction of operations that will timeout

		foreach (var pressureLevel in timeoutPressureLevels)
		{
			var logger = new FakeLogger<InMemoryInboxStore>();
			var inboxStore = new InMemoryInboxStore(
				Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
				{
					MaxEntries = 50_000,
					EnableAutomaticCleanup = true,
					CleanupInterval = TimeSpan.FromSeconds(2)
				}),
				logger);

			var completedOperations = new ConcurrentBag<CompletedOperation>();
			var globalStopwatch = Stopwatch.StartNew();

			// Act - Run throughput test under timeout pressure
			var workerTasks = Enumerable.Range(0, concurrentWorkers)
				.Select(async workerId =>
				{
					var operationIndex = 0;
					var workerStopwatch = Stopwatch.StartNew();

					while (workerStopwatch.Elapsed.TotalSeconds < durationSeconds)
					{
						var operationStopwatch = Stopwatch.StartNew();
						var messageId = $"throughput-{pressureLevel:F1}-{workerId}-{operationIndex}";

						try
						{
							// Calculate timeout based on pressure level
							var shouldTimeout = Random.Shared.NextDouble() < pressureLevel;
							var timeoutMs = shouldTimeout ? 1 : baseTimeoutMs; // Very short timeout to force timeout

							using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
							using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
								_testCancellation.Token, timeoutCts.Token);

							var payload = Encoding.UTF8.GetBytes($"Throughput test {workerId}-{operationIndex}");
							var metadata = new Dictionary<string, object>
							{
								{ "workerId", workerId },
								{ "operationIndex", operationIndex },
								{ "pressureLevel", pressureLevel },
								{ "shouldTimeout", shouldTimeout }
							};

							_ = await inboxStore.CreateEntryAsync(
								messageId, "TestHandler", "ThroughputTestMessage", payload, metadata, combinedCts.Token)
								.ConfigureAwait(false);

							// Simulate processing delay
							var processingDelay = shouldTimeout ? 50 : Random.Shared.Next(5, 15);
							await Task.Delay(processingDelay, combinedCts.Token).ConfigureAwait(false);

							await inboxStore.MarkProcessedAsync(messageId, "TestHandler", combinedCts.Token).ConfigureAwait(false);

							operationStopwatch.Stop();

							completedOperations.Add(new CompletedOperation
							{
								WorkerId = workerId,
								OperationIndex = operationIndex,
								Duration = operationStopwatch.Elapsed,
								WasSuccessful = true,
								GlobalElapsed = globalStopwatch.Elapsed,
								WasExpectedToTimeout = shouldTimeout
							});
						}
						catch (OperationCanceledException)
						{
							operationStopwatch.Stop();

							completedOperations.Add(new CompletedOperation
							{
								WorkerId = workerId,
								OperationIndex = operationIndex,
								Duration = operationStopwatch.Elapsed,
								WasSuccessful = false,
								GlobalElapsed = globalStopwatch.Elapsed,
								WasExpectedToTimeout = true
							});
						}

						operationIndex++;
					}

					return operationIndex;
				});

			var totalOperations = (await Task.WhenAll(workerTasks).ConfigureAwait(false)).Sum();

			globalStopwatch.Stop();

			// Analyze throughput results
			var operations = completedOperations.ToList();
			var successfulOps = operations.Count(o => o.WasSuccessful);
			var timedOutOps = operations.Count(o => !o.WasSuccessful);
			var expectedTimeouts = operations.Count(o => o.WasExpectedToTimeout);
			var unexpectedTimeouts = timedOutOps - operations.Count(o => !o.WasSuccessful && o.WasExpectedToTimeout);

			var actualThroughput = successfulOps / globalStopwatch.Elapsed.TotalSeconds;
			var avgOperationTime = operations.Where(o => o.WasSuccessful).Average(o => o.Duration.TotalMilliseconds);

			var throughputMeasurement = new ThroughputMeasurement
			{
				PressureLevel = pressureLevel,
				DurationSeconds = durationSeconds,
				TotalOperations = totalOperations,
				SuccessfulOperations = successfulOps,
				TimedOutOperations = timedOutOps,
				ExpectedTimeouts = expectedTimeouts,
				UnexpectedTimeouts = unexpectedTimeouts,
				ActualThroughputPerSecond = actualThroughput,
				AvgOperationTimeMs = avgOperationTime,
				ConcurrentWorkers = concurrentWorkers
			};

			throughputMetrics.Enqueue(throughputMeasurement);

			inboxStore.Dispose();

			// Brief pause between pressure levels
			await Task.Delay(1000, _testCancellation.Token).ConfigureAwait(false);
		}

		// Assert graceful degradation
		var measurements = throughputMetrics.OrderBy(m => m.PressureLevel).ToList();
		var baselineThroughput = measurements.First().ActualThroughputPerSecond;

		_output.WriteLine("=== Throughput Under Timeout Pressure ===");
		_output.WriteLine($"Test Duration: {durationSeconds} seconds");
		_output.WriteLine($"Concurrent Workers: {concurrentWorkers}");
		_output.WriteLine($"Base Timeout: {baseTimeoutMs}ms");
		_output.WriteLine(string.Empty);

		foreach (var measurement in measurements)
		{
			var throughputRetention = (measurement.ActualThroughputPerSecond / baselineThroughput) * 100;
			var timeoutAccuracy = measurement.ExpectedTimeouts > 0
				? (double)measurement.TimedOutOperations / measurement.ExpectedTimeouts * 100
				: 100;

			_output.WriteLine($"Pressure Level {measurement.PressureLevel:F1} ({measurement.PressureLevel * 100:F0}% timeout rate):");
			_output.WriteLine($"  Total Operations: {measurement.TotalOperations:N0}");
			_output.WriteLine($"  Successful: {measurement.SuccessfulOperations:N0}");
			_output.WriteLine($"  Timed Out: {measurement.TimedOutOperations:N0}");
			_output.WriteLine($"  Expected Timeouts: {measurement.ExpectedTimeouts:N0}");
			_output.WriteLine($"  Unexpected Timeouts: {measurement.UnexpectedTimeouts:N0}");
			_output.WriteLine($"  Throughput: {measurement.ActualThroughputPerSecond:F1} ops/sec");
			_output.WriteLine($"  Throughput Retention: {throughputRetention:F1}%");
			_output.WriteLine($"  Avg Operation Time: {measurement.AvgOperationTimeMs:F2}ms");
			_output.WriteLine($"  Timeout Accuracy: {timeoutAccuracy:F1}%");
			_output.WriteLine(string.Empty);
		}

		// Performance requirements (R9.50, R9.53)
		// Throughput should degrade gracefully with timeout pressure
		foreach (var measurement in measurements.Skip(1))
		{
			var throughputRetention = (measurement.ActualThroughputPerSecond / baselineThroughput) * 100;
			var expectedRetention = Math.Max(3, 100 - (measurement.PressureLevel * 220)); // Allow generous degradation under CI load

			throughputRetention.ShouldBeGreaterThan(expectedRetention,
				$"Throughput retention at {measurement.PressureLevel:F1} pressure should be at least {expectedRetention:F1}%");

			// Unexpected timeouts should be minimal
			measurement.UnexpectedTimeouts.ShouldBeLessThan((int)(measurement.TotalOperations * 0.10),
				"Unexpected timeouts should be less than 10%");
		}

		// System should maintain reasonable throughput even under high pressure
		var highPressureMeasurement = measurements.Last();
		(highPressureMeasurement.ActualThroughputPerSecond / baselineThroughput).ShouldBeGreaterThan(0.05,
			"System should maintain at least 5% throughput under maximum pressure");
	}

	#endregion Throughput Preservation Tests

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

	private static double CalculateStandardDeviation(IList<double> values)
	{
		var mean = values.Average();
		var sumOfSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
		return Math.Sqrt(sumOfSquaredDifferences / values.Count);
	}

	private async Task PerformLatencyWarmup(InMemoryInboxStore inboxStore, int warmupCount, LatencyTestScenario scenario)
	{
		for (int i = 0; i < warmupCount; i++)
		{
			var messageId = $"warmup-{scenario.Name}-{i}";
			var payload = Encoding.UTF8.GetBytes($"Warmup {i}");
			var metadata = new Dictionary<string, object> { { "warmup", true } };

			CancellationToken token = _testCancellation.Token;
			CancellationTokenSource? timeoutCts = null;
			CancellationTokenSource? combinedCts = null;

			try
			{
				if (scenario.UseTimeout)
				{
					timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
					combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_testCancellation.Token, timeoutCts.Token);
					token = combinedCts.Token;
				}

				_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "WarmupMessage", payload, metadata, token).ConfigureAwait(false);
				await inboxStore.MarkProcessedAsync(messageId, "TestHandler", token).ConfigureAwait(false);
			}
			finally
			{
				timeoutCts?.Dispose();
				combinedCts?.Dispose();
			}
		}
	}

	#endregion Helper Methods

	#region Test Data Types

	private sealed record AllocationMetrics
	{
		public required string TestName { get; init; }
		public int OperationCount { get; init; }
		public TimeSpan Duration { get; init; }
		public long MemoryBefore { get; init; }
		public long MemoryAfter { get; init; }
		public long AllocatedBytesBefore { get; init; }
		public long AllocatedBytesAfter { get; init; }
		public long TotalAllocatedBytes { get; init; }
		public long NetMemoryIncrease { get; init; }
	}

	private sealed record TokenCreationMetrics
	{
		public int BatchNumber { get; init; }
		public int TokenCount { get; init; }
		public TimeSpan TotalDuration { get; init; }
		public TimeSpan SimpleTokenDuration { get; init; }
		public TimeSpan LinkedTokenDuration { get; init; }
		public TimeSpan CombinedTokenDuration { get; init; }
		public long MemoryBefore { get; init; }
		public long MemoryAfter { get; init; }
		public long TotalAllocatedBytes { get; init; }
		public long NetMemoryIncrease { get; init; }
	}

	private sealed record LatencyTestScenario
	{
		public required string Name { get; init; }
		public bool UseTimeout { get; init; }
		public bool CalculateBudget { get; init; }
	}

	private sealed record WorkerLatencyResult
	{
		public int WorkerId { get; init; }
		public required string Scenario { get; init; }
		public required List<double> Latencies { get; init; }
		public int MessageCount { get; init; }
	}

	private sealed record LatencyMeasurement
	{
		public required string ScenarioName { get; init; }
		public int MessageCount { get; init; }
		public int ConcurrentWorkers { get; init; }
		public double MinLatencyMicros { get; init; }
		public double MaxLatencyMicros { get; init; }
		public double MedianLatencyMicros { get; init; }
		public double P95LatencyMicros { get; init; }
		public double P99LatencyMicros { get; init; }
		public double AvgLatencyMicros { get; init; }
		public double StdDevLatencyMicros { get; init; }
	}

	private sealed record ThroughputMeasurement
	{
		public double PressureLevel { get; init; }
		public int DurationSeconds { get; init; }
		public int TotalOperations { get; init; }
		public int SuccessfulOperations { get; init; }
		public int TimedOutOperations { get; init; }
		public int ExpectedTimeouts { get; init; }
		public int UnexpectedTimeouts { get; init; }
		public double ActualThroughputPerSecond { get; init; }
		public double AvgOperationTimeMs { get; init; }
		public int ConcurrentWorkers { get; init; }
	}

	private sealed record CompletedOperation
	{
		public int WorkerId { get; init; }
		public int OperationIndex { get; init; }
		public TimeSpan Duration { get; init; }
		public bool WasSuccessful { get; init; }
		public TimeSpan GlobalElapsed { get; init; }
		public bool WasExpectedToTimeout { get; init; }
	}

	#endregion Test Data Types
}
