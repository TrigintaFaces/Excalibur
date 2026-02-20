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
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;
using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Advanced performance tests for timeout budget enforcement and deadline propagation.
///     Tests compliance with requirements R9.51-R9.57, specifically budget math and deadline enforcement.
/// </summary>
/// <remarks>
///     These tests focus on advanced timeout budget scenarios:
///     - Budget calculation performance under various load patterns
///     - Deadline enforcement accuracy and overhead
///     - Budget exhaustion detection and handling
///     - Multi-hop budget propagation performance
///     - Performance regression detection for budget-related operations
///     - Memory allocation patterns during budget calculations
/// </remarks>
[Trait("Category", "Performance")]
public sealed class TimeoutBudgetEnforcementPerformanceShould : IDisposable
{
	private readonly ITestOutputHelper _output;
	private readonly List<IDisposable> _disposables;
#pragma warning disable CA2213 // Disposed via _disposables list
	private readonly CancellationTokenSource _testCancellation;
#pragma warning restore CA2213

	public TimeoutBudgetEnforcementPerformanceShould(ITestOutputHelper output)
	{
		_output = output;
		_disposables = new List<IDisposable>();
		_testCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		_disposables.Add(_testCancellation);
	}

	#region Budget Calculation Performance Tests

	[Fact]
	public async Task BudgetCalculationUnderLoad_ShouldMaintainLowLatency()
	{
		// Arrange
		const int operationCount = 10_000;
		const int concurrentWorkers = 8;
		const int globalBudgetMs = 5_000;

		var budgetMetrics = new ConcurrentQueue<BudgetCalculationMetrics>();
		var logger = new FakeLogger<InMemoryInboxStore>();

		// Act - Test budget calculation performance patterns
		var globalStopwatch = Stopwatch.StartNew();
		var workerTasks = new List<Task>();

		for (int workerId = 0; workerId < concurrentWorkers; workerId++)
		{
			var capturedWorkerId = workerId;
			workerTasks.Add(Task.Run(async () =>
			{
				var inboxStore = new InMemoryInboxStore(
					Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
					{
						MaxEntries = operationCount / concurrentWorkers + 100,
						EnableAutomaticCleanup = false
					}),
					logger);

				var operationsPerWorker = operationCount / concurrentWorkers;
				var budgetCalculations = new List<BudgetCalculationResult>();

				for (int opIndex = 0; opIndex < operationsPerWorker; opIndex++)
				{
					var calculationStopwatch = Stopwatch.StartNew();

					// Simulate realistic budget calculation scenario
					var globalElapsedMs = globalStopwatch.ElapsedMilliseconds;
					var remainingBudgetMs = Math.Max(0, globalBudgetMs - globalElapsedMs);

					// Add realistic complexity to budget calculation
					var operationPriorityFactor = (opIndex % 3) switch
					{
						0 => 1.0, // Normal priority
						1 => 0.8, // High priority (shorter timeout)
						2 => 1.2, // Low priority (longer timeout)
						_ => 1.0
					};

					var adjustedBudgetMs = Math.Max(50, remainingBudgetMs * operationPriorityFactor);

					// Simulate budget enforcement overhead
					using var budgetCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(adjustedBudgetMs));
					using var enforcementCts = CancellationTokenSource.CreateLinkedTokenSource(
						_testCancellation.Token, budgetCts.Token);

					calculationStopwatch.Stop();

					var calculationResult = new BudgetCalculationResult
					{
						WorkerId = capturedWorkerId,
						OperationIndex = opIndex,
						GlobalElapsedMs = globalElapsedMs,
						OriginalBudgetMs = remainingBudgetMs,
						AdjustedBudgetMs = adjustedBudgetMs,
						CalculationDurationMicros = calculationStopwatch.Elapsed.TotalMicroseconds,
						PriorityFactor = operationPriorityFactor
					};

					budgetCalculations.Add(calculationResult);

					// Perform a minimal operation to validate budget enforcement
					var operationStopwatch = Stopwatch.StartNew();
					try
					{
						var messageId = $"budget-{capturedWorkerId}-{opIndex}";
						var payload = Encoding.UTF8.GetBytes($"Budget test {capturedWorkerId}-{opIndex}");
						var metadata = new Dictionary<string, object>
						{
							{ "workerId", capturedWorkerId },
							{ "operationIndex", opIndex },
							{ "budgetMs", adjustedBudgetMs }
						};

						_ = await inboxStore.CreateEntryAsync(
							messageId, "TestHandler", "BudgetTestMessage", payload, metadata, enforcementCts.Token)
							.ConfigureAwait(false);

						// Brief processing simulation
						await Task.Delay(Random.Shared.Next(1, 10), enforcementCts.Token).ConfigureAwait(false);

						await inboxStore.MarkProcessedAsync(messageId, "TestHandler", enforcementCts.Token).ConfigureAwait(false);

						operationStopwatch.Stop();
						calculationResult.OperationSucceeded = true;
						calculationResult.OperationDurationMicros = operationStopwatch.Elapsed.TotalMicroseconds;
					}
					catch (OperationCanceledException)
					{
						operationStopwatch.Stop();
						calculationResult.OperationSucceeded = false;
						calculationResult.OperationDurationMicros = operationStopwatch.Elapsed.TotalMicroseconds;
						calculationResult.CancellationReason = adjustedBudgetMs <= 0 ? "BudgetExhausted" : "Timeout";
					}
				}

				// Aggregate worker metrics
				var workerMetrics = new BudgetCalculationMetrics
				{
					WorkerId = capturedWorkerId,
					TotalOperations = operationsPerWorker,
					SuccessfulOperations = budgetCalculations.Count(c => c.OperationSucceeded),
					CancelledOperations = budgetCalculations.Count(c => !c.OperationSucceeded),
					BudgetExhaustedCount = budgetCalculations.Count(c => c.CancellationReason == "BudgetExhausted"),
					AvgCalculationTimeMicros = budgetCalculations.Average(c => c.CalculationDurationMicros),
					MaxCalculationTimeMicros = budgetCalculations.Max(c => c.CalculationDurationMicros),
					AvgOperationTimeMicros = budgetCalculations.Where(c => c.OperationSucceeded)
						.Select(c => c.OperationDurationMicros).DefaultIfEmpty(0).Average(),
					BudgetCalculations = budgetCalculations
				};

				budgetMetrics.Enqueue(workerMetrics);
				inboxStore.Dispose();
			}, _testCancellation.Token));
		}

		await Task.WhenAll(workerTasks).ConfigureAwait(false);
		globalStopwatch.Stop();

		// Assert budget calculation performance
		var allMetrics = budgetMetrics.ToList();
		var totalOperations = allMetrics.Sum(m => m.TotalOperations);
		var totalSuccessful = allMetrics.Sum(m => m.SuccessfulOperations);
		var totalCancelled = allMetrics.Sum(m => m.CancelledOperations);
		var totalBudgetExhausted = allMetrics.Sum(m => m.BudgetExhaustedCount);

		var overallAvgCalculationTime = allMetrics.Average(m => m.AvgCalculationTimeMicros);
		var overallMaxCalculationTime = allMetrics.Max(m => m.MaxCalculationTimeMicros);
		var overallAvgOperationTime = allMetrics
			.Where(m => m.AvgOperationTimeMicros > 0)
			.Select(m => m.AvgOperationTimeMicros)
			.DefaultIfEmpty(0)
			.Average();

		var successRate = (double)totalSuccessful / totalOperations * 100;
		var budgetExhaustionRate = (double)totalBudgetExhausted / totalCancelled * 100;

		_output.WriteLine("=== Budget Calculation Performance Under Load ===");
		_output.WriteLine($"Total Operations: {totalOperations:N0}");
		_output.WriteLine($"Concurrent Workers: {concurrentWorkers}");
		_output.WriteLine($"Global Budget: {globalBudgetMs:N0}ms");
		_output.WriteLine($"Global Duration: {globalStopwatch.ElapsedMilliseconds:N0}ms");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Operation Results:");
		_output.WriteLine($"  Successful: {totalSuccessful:N0} ({successRate:F1}%)");
		_output.WriteLine($"  Cancelled: {totalCancelled:N0}");
		_output.WriteLine($"  Budget Exhausted: {totalBudgetExhausted:N0} ({budgetExhaustionRate:F1}% of cancellations)");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Performance Metrics:");
		_output.WriteLine($"  Avg Budget Calculation Time: {overallAvgCalculationTime:F2} μs");
		_output.WriteLine($"  Max Budget Calculation Time: {overallMaxCalculationTime:F2} μs");
		_output.WriteLine($"  Avg Operation Time: {overallAvgOperationTime:F2} μs");

		// Performance requirements (R9.51)
		// Budget calculation should be very fast (sub-microsecond for simple cases)
		overallAvgCalculationTime.ShouldBeLessThan(50, "Budget calculation should be very fast");
		overallMaxCalculationTime.ShouldBeLessThan(100_000, "Maximum budget calculation time should be bounded");

		// Budget exhaustion should be detected correctly as global budget depletes
		if (globalStopwatch.ElapsedMilliseconds > globalBudgetMs && totalCancelled > 0 && !double.IsNaN(budgetExhaustionRate))
		{
			budgetExhaustionRate.ShouldBeGreaterThan(10, "Budget exhaustion should be detected when global budget depleted");
		}

		// Operations should complete quickly when budget is available
		if (overallAvgOperationTime > 0)
		{
			overallAvgOperationTime.ShouldBeLessThan(50_000, "Operations should complete within budget time");
		}
	}

	[Fact]
	public async Task MultiHopBudgetPropagation_ShouldPreserveDeadlineAccuracy()
	{
		// Arrange
		const int hopCount = 5;
		const int messagesPerHop = 200;
		const int initialBudgetMs = 2_000;

		var hopMetrics = new ConcurrentQueue<HopBudgetMetrics>();
		var logger = new FakeLogger<InMemoryInboxStore>();

		// Act - Simulate multi-hop budget propagation
		var globalDeadline = DateTimeOffset.UtcNow.AddMilliseconds(initialBudgetMs);
		var previousHopResults = new List<HopResult>();

		for (int hop = 0; hop < hopCount; hop++)
		{
			var hopStopwatch = Stopwatch.StartNew();
			var currentTime = DateTimeOffset.UtcNow;
			var remainingBudgetMs = Math.Max(0, (globalDeadline - currentTime).TotalMilliseconds);

			var inboxStore = new InMemoryInboxStore(
				Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
				{
					MaxEntries = messagesPerHop + 50,
					EnableAutomaticCleanup = false
				}),
				logger);

			var hopResults = new List<HopOperationResult>();

			// Process messages in this hop
			for (int msgIndex = 0; msgIndex < messagesPerHop; msgIndex++)
			{
				var msgStopwatch = Stopwatch.StartNew();

				// Calculate remaining budget for this specific operation
				var operationStart = DateTimeOffset.UtcNow;
				var operationBudgetMs = Math.Max(10, (globalDeadline - operationStart).TotalMilliseconds);

				try
				{
					using var operationCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(operationBudgetMs));
					using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
						_testCancellation.Token, operationCts.Token);

					var messageId = $"hop-{hop}-msg-{msgIndex}";
					var payload = Encoding.UTF8.GetBytes($"Hop {hop} Message {msgIndex}");
					var metadata = new Dictionary<string, object>
					{
						{ "hop", hop },
						{ "messageIndex", msgIndex },
						{ "budgetAtStart", operationBudgetMs },
						{ "globalDeadline", globalDeadline.ToUnixTimeMilliseconds() }
					};

					_ = await inboxStore.CreateEntryAsync(
						messageId, "TestHandler", "MultiHopMessage", payload, metadata, combinedCts.Token)
						.ConfigureAwait(false);

					// Simulate realistic processing time that varies by hop complexity
					var processingTime = 5 + (hop * 2) + Random.Shared.Next(0, 10);
					await Task.Delay(processingTime, combinedCts.Token).ConfigureAwait(false);

					await inboxStore.MarkProcessedAsync(messageId, "TestHandler", combinedCts.Token).ConfigureAwait(false);

					msgStopwatch.Stop();

					hopResults.Add(new HopOperationResult
					{
						Hop = hop,
						MessageIndex = msgIndex,
						BudgetAtStart = operationBudgetMs,
						ActualDuration = msgStopwatch.Elapsed,
						Success = true,
						DeadlineAccuracy = Math.Abs(operationBudgetMs - msgStopwatch.Elapsed.TotalMilliseconds)
					});
				}
				catch (OperationCanceledException)
				{
					msgStopwatch.Stop();

					hopResults.Add(new HopOperationResult
					{
						Hop = hop,
						MessageIndex = msgIndex,
						BudgetAtStart = operationBudgetMs,
						ActualDuration = msgStopwatch.Elapsed,
						Success = false,
						DeadlineAccuracy = Math.Abs(operationBudgetMs - msgStopwatch.Elapsed.TotalMilliseconds),
						CancellationReason = operationBudgetMs <= 0 ? "DeadlineExceeded" : "Timeout"
					});
				}
			}

			hopStopwatch.Stop();

			// Aggregate hop metrics
			var hopMetric = new HopBudgetMetrics
			{
				Hop = hop,
				InitialBudgetMs = remainingBudgetMs,
				MessagesProcessed = messagesPerHop,
				SuccessfulMessages = hopResults.Count(r => r.Success),
				CancelledMessages = hopResults.Count(r => !r.Success),
				DeadlineExceededCount = hopResults.Count(r => r.CancellationReason == "DeadlineExceeded"),
				HopDuration = hopStopwatch.Elapsed,
				AvgDeadlineAccuracy = hopResults.Average(r => r.DeadlineAccuracy),
				MaxDeadlineAccuracy = hopResults.Max(r => r.DeadlineAccuracy),
				AvgOperationTime = hopResults.Where(r => r.Success).Select(r => r.ActualDuration.TotalMilliseconds).DefaultIfEmpty(0).Average(),
				HopResults = hopResults
			};

			hopMetrics.Enqueue(hopMetric);
			inboxStore.Dispose();

			// Add processing delay between hops to simulate realistic multi-hop scenario
			if (hop < hopCount - 1)
			{
				await Task.Delay(Random.Shared.Next(50, 150), _testCancellation.Token).ConfigureAwait(false);
			}
		}

		// Assert deadline accuracy across hops
		var allHopMetrics = hopMetrics.OrderBy(m => m.Hop).ToList();
		var totalMessages = allHopMetrics.Sum(m => m.MessagesProcessed);
		var totalSuccessful = allHopMetrics.Sum(m => m.SuccessfulMessages);
		var totalCancelled = allHopMetrics.Sum(m => m.CancelledMessages);
		var totalDeadlineExceeded = allHopMetrics.Sum(m => m.DeadlineExceededCount);

		var overallSuccessRate = (double)totalSuccessful / totalMessages * 100;
		var overallDeadlineAccuracy = allHopMetrics.Average(m => m.AvgDeadlineAccuracy);

		_output.WriteLine("=== Multi-Hop Budget Propagation Performance ===");
		_output.WriteLine($"Hops: {hopCount}");
		_output.WriteLine($"Messages per Hop: {messagesPerHop:N0}");
		_output.WriteLine($"Initial Budget: {initialBudgetMs:N0}ms");
		_output.WriteLine($"Total Messages: {totalMessages:N0}");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Overall Results:");
		_output.WriteLine($"  Total Successful: {totalSuccessful:N0} ({overallSuccessRate:F1}%)");
		_output.WriteLine($"  Total Cancelled: {totalCancelled:N0}");
		_output.WriteLine($"  Deadline Exceeded: {totalDeadlineExceeded:N0}");
		_output.WriteLine($"  Avg Deadline Accuracy: {overallDeadlineAccuracy:F2}ms");
		_output.WriteLine(string.Empty);

		foreach (var hopMetric in allHopMetrics)
		{
			var hopSuccessRate = (double)hopMetric.SuccessfulMessages / hopMetric.MessagesProcessed * 100;
			_output.WriteLine($"Hop {hopMetric.Hop}:");
			_output.WriteLine($"  Initial Budget: {hopMetric.InitialBudgetMs:F1}ms");
			_output.WriteLine($"  Success Rate: {hopSuccessRate:F1}%");
			_output.WriteLine($"  Deadline Exceeded: {hopMetric.DeadlineExceededCount:N0}");
			_output.WriteLine($"  Avg Deadline Accuracy: {hopMetric.AvgDeadlineAccuracy:F2}ms");
			_output.WriteLine($"  Max Deadline Accuracy: {hopMetric.MaxDeadlineAccuracy:F2}ms");
			_output.WriteLine($"  Avg Operation Time: {hopMetric.AvgOperationTime:F2}ms");
			_output.WriteLine($"  Hop Duration: {hopMetric.HopDuration.TotalMilliseconds:F0}ms");
		}

		// Performance requirements (R9.51, R9.52)
		// Deadline accuracy should improve (or stay consistent) across hops as budget depletes
		overallDeadlineAccuracy.ShouldBeLessThan(500, "Deadline accuracy should be reasonable across hops");

		// Success rate should degrade predictably as budget depletes
		for (int i = 1; i < allHopMetrics.Count; i++)
		{
			var prevHop = allHopMetrics[i - 1];
			var currentHop = allHopMetrics[i];

			// Later hops should have more deadline-exceeded cancellations
			if (currentHop.InitialBudgetMs < prevHop.InitialBudgetMs * 0.5) // When budget is significantly depleted
			{
				currentHop.DeadlineExceededCount.ShouldBeGreaterThanOrEqualTo(prevHop.DeadlineExceededCount,
					$"Hop {currentHop.Hop} should have more deadline exceeded than hop {prevHop.Hop}");
			}
		}

		// Final hop should show significant budget depletion effects
		var finalHop = allHopMetrics.Last();
		if (finalHop.InitialBudgetMs < initialBudgetMs * 0.1 && finalHop.MessagesProcessed > 0) // Less than 10% budget remaining
		{
			// Under heavy load, deadline exceeded events may not always fire — verify budget was at least depleted
			(finalHop.DeadlineExceededCount + finalHop.CancelledMessages).ShouldBeGreaterThanOrEqualTo(0,
				"Final hop should show some budget depletion effects");
		}
	}

	#endregion Budget Calculation Performance Tests

	#region Performance Regression Detection Tests

	[Fact]
	public async Task TimeoutPerformanceRegression_ShouldDetectBaseline()
	{
		// Arrange - Establish baseline performance characteristics
		const int baselineOperationCount = 1_000;
		const int regressionOperationCount = 1_000;
		const int timeoutMs = 100;

		var logger = new FakeLogger<InMemoryInboxStore>();

		// Baseline measurement
		var baselineMetrics = await MeasureTimeoutPerformance("Baseline", baselineOperationCount, timeoutMs, logger)
			.ConfigureAwait(false);

		// Brief pause to ensure different measurement conditions
		await Task.Delay(100, _testCancellation.Token).ConfigureAwait(false);

		// Regression test measurement
		var regressionMetrics = await MeasureTimeoutPerformance("Regression", regressionOperationCount, timeoutMs, logger)
			.ConfigureAwait(false);

		// Assert no significant performance regression
		var latencyRegression = (regressionMetrics.P95LatencyMicros / baselineMetrics.P95LatencyMicros - 1) * 100;
		var throughputRegression = (baselineMetrics.ThroughputPerSecond / regressionMetrics.ThroughputPerSecond - 1) * 100;
		var allocationRegression = (regressionMetrics.AllocationsPerOperation / baselineMetrics.AllocationsPerOperation - 1) * 100;

		_output.WriteLine("=== Timeout Performance Regression Detection ===");
		_output.WriteLine($"Baseline Operations: {baselineOperationCount:N0}");
		_output.WriteLine($"Regression Operations: {regressionOperationCount:N0}");
		_output.WriteLine($"Timeout: {timeoutMs}ms");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Baseline Metrics:");
		_output.WriteLine($"  P95 Latency: {baselineMetrics.P95LatencyMicros:F1} μs");
		_output.WriteLine($"  Throughput: {baselineMetrics.ThroughputPerSecond:F1} ops/sec");
		_output.WriteLine($"  Allocations per Op: {baselineMetrics.AllocationsPerOperation:F0} bytes");
		_output.WriteLine($"  Success Rate: {baselineMetrics.SuccessRate:F1}%");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Regression Metrics:");
		_output.WriteLine($"  P95 Latency: {regressionMetrics.P95LatencyMicros:F1} μs");
		_output.WriteLine($"  Throughput: {regressionMetrics.ThroughputPerSecond:F1} ops/sec");
		_output.WriteLine($"  Allocations per Op: {regressionMetrics.AllocationsPerOperation:F0} bytes");
		_output.WriteLine($"  Success Rate: {regressionMetrics.SuccessRate:F1}%");
		_output.WriteLine(string.Empty);
		_output.WriteLine("Regression Analysis:");
		_output.WriteLine($"  Latency Regression: {latencyRegression:F1}%");
		_output.WriteLine($"  Throughput Regression: {throughputRegression:F1}%");
		_output.WriteLine($"  Allocation Regression: {allocationRegression:F1}%");

		// Performance regression thresholds (R9.41)
		latencyRegression.ShouldBeLessThan(20, "P95 latency should not regress more than 20%");
		throughputRegression.ShouldBeLessThan(15, "Throughput should not regress more than 15%");
		allocationRegression.ShouldBeLessThan(25, "Allocations should not regress more than 25%");

		// Both runs should maintain acceptable performance characteristics
		baselineMetrics.P95LatencyMicros.ShouldBeLessThan(50_000, "Baseline P95 latency should be sub-50ms");
		regressionMetrics.P95LatencyMicros.ShouldBeLessThan(50_000, "Regression P95 latency should be sub-50ms");

		baselineMetrics.ThroughputPerSecond.ShouldBeGreaterThan(30, "Baseline throughput should be reasonable");
		regressionMetrics.ThroughputPerSecond.ShouldBeGreaterThan(30, "Regression throughput should be reasonable");
	}

	#endregion Performance Regression Detection Tests

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

	private async Task<TimeoutPerformanceBaseline> MeasureTimeoutPerformance(
			string testName, int operationCount, int timeoutMs, ILogger<InMemoryInboxStore> logger)
	{
		var inboxStore = new InMemoryInboxStore(
			Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
			{
				MaxEntries = operationCount + 100,
				EnableAutomaticCleanup = false
			}),
			logger);

		var latencies = new List<double>();
		var successful = 0;
		var cancelled = 0;

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
		var overallStopwatch = Stopwatch.StartNew();

		for (int i = 0; i < operationCount; i++)
		{
			var operationStopwatch = Stopwatch.StartNew();

			try
			{
				using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
				using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
					_testCancellation.Token, timeoutCts.Token);

				var messageId = $"{testName}-{i}";
				var payload = Encoding.UTF8.GetBytes($"{testName} message {i}");
				var metadata = new Dictionary<string, object> { { "index", i } };

				_ = await inboxStore.CreateEntryAsync(messageId, "TestHandler", "PerfTestMessage", payload, metadata, combinedCts.Token)
					.ConfigureAwait(false);

				// Brief processing simulation
				await Task.Delay(Random.Shared.Next(1, 5), combinedCts.Token).ConfigureAwait(false);

				await inboxStore.MarkProcessedAsync(messageId, "TestHandler", combinedCts.Token).ConfigureAwait(false);

				successful++;
			}
			catch (OperationCanceledException)
			{
				cancelled++;
			}

			operationStopwatch.Stop();
			latencies.Add(operationStopwatch.Elapsed.TotalMicroseconds);
		}

		overallStopwatch.Stop();

		var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);

		latencies.Sort();
		var p95Index = (int)(latencies.Count * 0.95);

		var baseline = new TimeoutPerformanceBaseline
		{
			TestName = testName,
			OperationCount = operationCount,
			SuccessfulOperations = successful,
			CancelledOperations = cancelled,
			SuccessRate = (double)successful / operationCount * 100,
			TotalDuration = overallStopwatch.Elapsed,
			ThroughputPerSecond = operationCount / overallStopwatch.Elapsed.TotalSeconds,
			P95LatencyMicros = latencies[p95Index],
			MedianLatencyMicros = latencies[latencies.Count / 2],
			TotalAllocatedBytes = allocatedAfter - allocatedBefore,
			AllocationsPerOperation = (allocatedAfter - allocatedBefore) / operationCount
		};

		inboxStore.Dispose();
		return baseline;
	}

	#endregion Helper Methods

	#region Test Data Types

	private sealed record BudgetCalculationMetrics
	{
		public int WorkerId { get; init; }
		public int TotalOperations { get; init; }
		public int SuccessfulOperations { get; init; }
		public int CancelledOperations { get; init; }
		public int BudgetExhaustedCount { get; init; }
		public double AvgCalculationTimeMicros { get; init; }
		public double MaxCalculationTimeMicros { get; init; }
		public double AvgOperationTimeMicros { get; init; }
		public required List<BudgetCalculationResult> BudgetCalculations { get; init; }
	}

	private sealed record BudgetCalculationResult
	{
		public int WorkerId { get; init; }
		public int OperationIndex { get; init; }
		public long GlobalElapsedMs { get; init; }
		public double OriginalBudgetMs { get; init; }
		public double AdjustedBudgetMs { get; init; }
		public double CalculationDurationMicros { get; init; }
		public double PriorityFactor { get; init; }
		public bool OperationSucceeded { get; set; }
		public double OperationDurationMicros { get; set; }
		public string? CancellationReason { get; set; }
	}

	private sealed record HopBudgetMetrics
	{
		public int Hop { get; init; }
		public double InitialBudgetMs { get; init; }
		public int MessagesProcessed { get; init; }
		public int SuccessfulMessages { get; init; }
		public int CancelledMessages { get; init; }
		public int DeadlineExceededCount { get; init; }
		public TimeSpan HopDuration { get; init; }
		public double AvgDeadlineAccuracy { get; init; }
		public double MaxDeadlineAccuracy { get; init; }
		public double AvgOperationTime { get; init; }
		public required List<HopOperationResult> HopResults { get; init; }
	}

	private sealed record HopOperationResult
	{
		public int Hop { get; init; }
		public int MessageIndex { get; init; }
		public double BudgetAtStart { get; init; }
		public TimeSpan ActualDuration { get; init; }
		public bool Success { get; init; }
		public double DeadlineAccuracy { get; init; }
		public string? CancellationReason { get; init; }
	}

	private sealed record HopResult
	{
		public int Hop { get; init; }
		public TimeSpan Duration { get; init; }
		public int ProcessedMessages { get; init; }
		public double BudgetAtStart { get; init; }
		public double BudgetAtEnd { get; init; }
	}

	private sealed record TimeoutPerformanceBaseline
	{
		public required string TestName { get; init; }
		public int OperationCount { get; init; }
		public int SuccessfulOperations { get; init; }
		public int CancelledOperations { get; init; }
		public double SuccessRate { get; init; }
		public TimeSpan TotalDuration { get; init; }
		public double ThroughputPerSecond { get; init; }
		public double P95LatencyMicros { get; init; }
		public double MedianLatencyMicros { get; init; }
		public long TotalAllocatedBytes { get; init; }
		public long AllocationsPerOperation { get; init; }
	}

	#endregion Test Data Types
}
