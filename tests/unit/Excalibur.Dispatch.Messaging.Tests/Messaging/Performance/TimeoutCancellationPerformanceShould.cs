// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Performance;

/// <summary>
///     Performance tests for timeout and cancellation handling.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Performance")]
public sealed class TimeoutCancellationPerformanceShould : IDisposable
{
	private readonly ILogger<UnifiedBatchingMiddleware> _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly List<IDisposable> _disposables;

	public TimeoutCancellationPerformanceShould()
	{
		_logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<UnifiedBatchingMiddleware>.Instance;
		_loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
		_disposables = [];
	}

	[Fact]
	public async Task HandleHighFrequencyCancellationWithMinimalOverhead()
	{
		// Arrange
		const int operationCount = 1000;
		double totalCancellationOverheadMicroseconds = 0;
		var processedCount = 0;

		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(50) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				_ = Interlocked.Add(ref processedCount, batch.Count);
				return ValueTask.CompletedTask;
			},
			Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor<string>>.Instance,
			options);

		_disposables.Add(processor);

		// Act - Measure cancellation token creation overhead
		var stopwatch = Stopwatch.StartNew();
		for (var i = 0; i < operationCount; i++)
		{
			var tokenStart = Stopwatch.GetTimestamp();
			using var cts = new CancellationTokenSource();
			await cts.CancelAsync().ConfigureAwait(false);
			totalCancellationOverheadMicroseconds += Stopwatch.GetElapsedTime(tokenStart).TotalMicroseconds;

			try
			{
				await processor.AddAsync($"item-{i}", cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected for cancelled tokens
			}
		}

		stopwatch.Stop();

		// Allow processing to complete
		await Task.Delay(200).ConfigureAwait(false);

		// Assert
		var avgCancellationOverhead = totalCancellationOverheadMicroseconds / operationCount;
		var totalThroughput = operationCount / stopwatch.Elapsed.TotalSeconds;

		// CI-friendly: Coverage instrumentation and VM scheduling inflate micro-bench timings.
		avgCancellationOverhead.ShouldBeLessThan(20); // Less than 20 microseconds per cancellation
		totalThroughput.ShouldBeGreaterThan(2500); // At least 2.5K operations/second
		processedCount.ShouldBe(0); // No items should be processed due to cancellation
	}

	[Fact]
	public async Task MaintainPerformanceUnderTimeoutPressure()
	{
		// Arrange - Use fewer messages and more generous timeouts to avoid test hanging
		const int messageCount = 50;
		const int timeoutMs = 500; // More generous timeout
		var handlerTimeouts = 0;
		var middlewareTimeouts = 0;
		var successfulCompletions = 0;
		var processingTimes = new ConcurrentBag<TimeSpan>();

		ValueTask<IMessageResult> TimeoutAwareHandler(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			var start = Stopwatch.GetTimestamp();

			// Don't use Task.Run with ct - it can cause hanging when the token is cancelled
			// before the task starts
			return InnerHandler();

			async ValueTask<IMessageResult> InnerHandler()
			{
				try
				{
					ct.ThrowIfCancellationRequested();

					// Simulate variable processing time (20-80ms to mostly succeed within timeout)
					var processingDelay = Random.Shared.Next(20, 80);
					await Task.Delay(processingDelay, ct).ConfigureAwait(false);

					var end = Stopwatch.GetTimestamp();
					processingTimes.Add(TimeSpan.FromTicks(end - start));

					_ = Interlocked.Increment(ref successfulCompletions);
					return MessageResult.Success();
				}
				catch (OperationCanceledException)
				{
					_ = Interlocked.Increment(ref handlerTimeouts);
					throw;
				}
			}
		}

		var options = new UnifiedBatchingOptions
		{
			MaxBatchSize = 5,
			MaxBatchDelay = TimeSpan.FromMilliseconds(10),
			MaxParallelism = Environment.ProcessorCount,
		};

		await using var middleware = new UnifiedBatchingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger, _loggerFactory);

		var overallStopwatch = Stopwatch.StartNew();

		// Act - Send messages with timeout constraints
		// Use a master timeout to prevent test from hanging indefinitely
		using var masterCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		var tasks = Enumerable.Range(0, messageCount)
			.Select(async i =>
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(masterCts.Token);
				cts.CancelAfter(timeoutMs);

				var message = new FakeDispatchMessage();
				var context = new FakeMessageContext();

				try
				{
					_ = await middleware.InvokeAsync(message, context, TimeoutAwareHandler, cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// Track cancellations that happen at the middleware level
					// (before the handler is invoked or in middleware itself)
					_ = Interlocked.Increment(ref middlewareTimeouts);
				}
			});

		await Task.WhenAll(tasks).ConfigureAwait(false);
		overallStopwatch.Stop();

		// Assert - Verify the test completes within reasonable time and the system doesn't hang
		// The middleware's batching behavior causes variable completion/timeout rates:
		// - Some messages complete successfully (handler runs, increments successfulCompletions)
		// - Some timeout in handler (handler runs but times out, increments handlerTimeouts, then rethrows)
		// - Some timeout before handler (middleware catches cancellation, increments middlewareTimeouts)
		// Note: handlerTimeouts are also caught at middleware level, so they're counted in both

		// Key assertion: The test completes without hanging - all tasks finished
		// This proves the system handles timeout pressure gracefully
		overallStopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(30);

		// Some messages should succeed - with 500ms timeout and 20-80ms processing, most should complete
		successfulCompletions.ShouldBeGreaterThan(0);

		// The total tracked should be reasonable (allows for timing variations)
		// Due to batching, some messages may succeed without the handler being invoked yet,
		// or cancellation may propagate differently
		var totalTracked = successfulCompletions + middlewareTimeouts;
		totalTracked.ShouldBeGreaterThan(0);
	}

	[Fact]
	public Task OptimizeTimeoutTokenLinkingPerformance()
	{
		// Arrange
		const int linkingOperations = 10000;
		double totalLinkingTimeMicroseconds = 0;

		using var parentCts = new CancellationTokenSource();
		var childTokens = new List<CancellationTokenSource>();

		try
		{
			// Act - Measure token linking overhead
			var stopwatch = Stopwatch.StartNew();

			for (var i = 0; i < linkingOperations; i++)
			{
				var linkStart = Stopwatch.GetTimestamp();

				var childCts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token);
				childTokens.Add(childCts);

				totalLinkingTimeMicroseconds += Stopwatch.GetElapsedTime(linkStart).TotalMicroseconds;
			}

			stopwatch.Stop();

			// Assert
			var avgLinkingTime = totalLinkingTimeMicroseconds / linkingOperations;
			var totalThroughput = linkingOperations / stopwatch.Elapsed.TotalSeconds;

			// CI-friendly: token linking costs vary significantly under coverage and contention.
			avgLinkingTime.ShouldBeLessThan(100); // Less than 100 microseconds per linking
			totalThroughput.ShouldBeGreaterThan(10000); // At least 10K linkings/second
		}
		finally
		{
			// Cleanup
			foreach (var cts in childTokens)
			{
				cts?.Dispose();
			}
		}

		return Task.CompletedTask;
	}

	[Fact]
	public async Task HandleCascadingCancellationEfficiently()
	{
		// Arrange
		const int cascadeDepth = 50;
		const int operationsPerLevel = 20;
		var totalCancellations = 0;

		var rootCts = new CancellationTokenSource();
		var disposables = new List<CancellationTokenSource> { rootCts };

		try
		{
			// Create a cascade of linked cancellation tokens
			var currentToken = rootCts.Token;
			var levelTokens = new List<CancellationTokenSource>();

			for (var level = 0; level < cascadeDepth; level++)
			{
				var levelCts = CancellationTokenSource.CreateLinkedTokenSource(currentToken);
				levelTokens.Add(levelCts);
				disposables.Add(levelCts);
				currentToken = levelCts.Token;
			}

			// Act - Start operations at each level and track cancellation
			var operationTasks = new List<Task>();
			var allOperationsStarted = new TaskCompletionSource<bool>();
			var operationsStartedCount = 0;
			var expectedOperations = cascadeDepth * operationsPerLevel;

			for (var level = 0; level < cascadeDepth; level++)
			{
				var levelToken = levelTokens[level].Token;

				for (var op = 0; op < operationsPerLevel; op++)
				{
					operationTasks.Add(Task.Run(async () =>
					{
						try
						{
							// Signal that this operation has started
							if (Interlocked.Increment(ref operationsStartedCount) == expectedOperations)
							{
								_ = allOperationsStarted.TrySetResult(true);
							}

							// Wait for cancellation
							await Task.Delay(Timeout.Infinite, levelToken).ConfigureAwait(false);
						}
						catch (OperationCanceledException)
						{
							_ = Interlocked.Increment(ref totalCancellations);
						}
					}));
				}
			}

			// Wait for all operations to start before triggering cancellation
			_ = await allOperationsStarted.Task.ConfigureAwait(false);

			// Measure cancellation propagation time
			var propagationStopwatch = Stopwatch.StartNew();
			await rootCts.CancelAsync().ConfigureAwait(false);

			// Wait for all operations to be cancelled
			await Task.WhenAll(operationTasks)
				.WaitAsync(TimeSpan.FromSeconds(30))
				.ConfigureAwait(false);
			propagationStopwatch.Stop();

			// Assert
			var expectedCancellations = cascadeDepth * operationsPerLevel;

			totalCancellations.ShouldBe(expectedCancellations);
			// Cancellation propagation through a 50-deep cascade should complete quickly.
			// Profiling-based CI runs (coverage/CodeQL) can inflate cancellation callback latency significantly.
			var maxPropagationMilliseconds = IsInstrumentedRuntime() ? 30000d : 200d;
			propagationStopwatch.Elapsed.TotalMilliseconds.ShouldBeLessThan(maxPropagationMilliseconds);
		}
		finally
		{
			foreach (var cts in disposables)
			{
				cts?.Dispose();
			}
		}
	}

	[Fact]
	public Task OptimizeTimeoutBudgetCalculationPerformance()
	{
		// Arrange
		const int calculationCount = 100000;
		var baselineTime = DateTime.UtcNow;
		var timeoutBudget = TimeSpan.FromSeconds(30);

		// Act - Measure budget calculation overhead
		var stopwatch = Stopwatch.StartNew();

		for (var i = 0; i < calculationCount; i++)
		{
			// Simulate budget calculation
			var elapsed = DateTime.UtcNow - baselineTime;
			var remaining = timeoutBudget - elapsed;
			var hasTimeLeft = remaining > TimeSpan.Zero;

			// Use the result to prevent optimization
			if (!hasTimeLeft && i == calculationCount - 1)
			{
				// This should rarely execute but prevents dead code elimination
			}
		}

		stopwatch.Stop();

		// Assert
		var avgCalculationTime = stopwatch.Elapsed.TotalNanoseconds / calculationCount;
		var totalThroughput = calculationCount / stopwatch.Elapsed.TotalSeconds;

		// CI-friendly: this path includes DateTime.UtcNow calls; enforce a practical budget under coverage.
		avgCalculationTime.ShouldBeLessThan(25000); // Less than 25 microseconds per calculation
		totalThroughput.ShouldBeGreaterThan(40000); // At least 40K calculations/second

		return Task.CompletedTask;
	}

	[Fact]
	public async Task HandleTimeoutRecoveryWithMinimalLatencyImpact()
	{
		// Arrange - Test that processor continues to function after cancelled operations
		const int initialOperations = 50;
		const int recoveryOperations = 50;
		var processedInitial = 0;
		var processedRecovery = 0;
		var cancelledOperations = 0;

		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromMilliseconds(20) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					if (item.StartsWith("initial-"))
					{
						_ = Interlocked.Increment(ref processedInitial);
					}
					else if (item.StartsWith("recovery-"))
					{
						_ = Interlocked.Increment(ref processedRecovery);
					}
				}

				return ValueTask.CompletedTask;
			},
			Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor<string>>.Instance,
			options);

		_disposables.Add(processor);

		var recoveryStopwatch = Stopwatch.StartNew();

		// Act - Phase 1: Send initial operations, some with pre-cancelled tokens
		var initialTasks = Enumerable.Range(0, initialOperations)
			.Select(async i =>
			{
				// Pre-cancel every other token to simulate timeout scenario
				using var cts = new CancellationTokenSource();
				if (i % 2 == 0)
				{
					await cts.CancelAsync().ConfigureAwait(false);
				}

				try
				{
					await processor.AddAsync($"initial-{i}", cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					_ = Interlocked.Increment(ref cancelledOperations);
				}
			});

		await Task.WhenAll(initialTasks).ConfigureAwait(false);

		// Phase 2: Send recovery operations with valid tokens
		var recoveryTasks = Enumerable.Range(0, recoveryOperations)
			.Select(async i =>
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await processor.AddAsync($"recovery-{i}", cts.Token).ConfigureAwait(false);
			});

		await Task.WhenAll(recoveryTasks).ConfigureAwait(false);

		// Allow processing to complete — generous delay for cross-process CPU starvation under full-suite load
		await Task.Delay(3000).ConfigureAwait(false);

		recoveryStopwatch.Stop();

		// Assert
		// Half of initial operations should be cancelled (pre-cancelled tokens)
		cancelledOperations.ShouldBe(initialOperations / 2);

		// Other half of initial + all recovery operations should be processed
		processedInitial.ShouldBe(initialOperations / 2);
		processedRecovery.ShouldBe(recoveryOperations);

		// Total recovery time should be reasonable (generous for full-suite VS Test Explorer load)
		recoveryStopwatch.Elapsed.TotalMilliseconds.ShouldBeLessThan(30000);
	}

	[Fact]
	public async Task MaintainLowAllocationUnderCancellationStress()
	{
		// Arrange
		const int stressOperations = 1000;
		var beforeGC = GC.GetTotalMemory(true);

		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(25) };

		var processor = new BatchProcessor<string>(
			_ => ValueTask.CompletedTask,
			Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor<string>>.Instance,
			options);

		_disposables.Add(processor);

		// Act - Generate cancellation stress
		var tasks = Enumerable.Range(0, stressOperations)
			.Select(async i =>
			{
				using var cts = new CancellationTokenSource();

				// Cancel half the operations randomly
				if (i % 2 == 0)
				{
					await cts.CancelAsync().ConfigureAwait(false);
				}

				try
				{
					await processor.AddAsync($"stress-item-{i}", cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					// Expected for cancelled operations
				}
			});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Allow processing and cleanup
		await Task.Delay(200).ConfigureAwait(false);

		var afterGC = GC.GetTotalMemory(true);
		var allocatedBytes = afterGC - beforeGC;
		var bytesPerOperation = allocatedBytes / (double)stressOperations;

		// Assert
		// Each operation involves CancellationTokenSource (~48-64 bytes), Task state machine (~32-48 bytes),
		// string allocation, and lambda closures. 150 bytes/op is realistic for async operations.
		bytesPerOperation.ShouldBeLessThan(150); // Less than 150 bytes per operation
		allocatedBytes.ShouldBeLessThan(150000); // Total under 150KB for all operations
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}
	}

	private static bool IsInstrumentedRuntime()
	{
		return string.Equals(Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING"), "1", StringComparison.Ordinal)
			   || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEQL_RUNNER"))
			   || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEQL_ACTION_VERSION"));
	}
}
