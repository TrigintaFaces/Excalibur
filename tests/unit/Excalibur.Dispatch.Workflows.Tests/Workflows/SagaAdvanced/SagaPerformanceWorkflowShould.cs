// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.SagaAdvanced;

/// <summary>
/// Saga Performance workflow tests.
/// Tests concurrent execution, throughput, latency, metrics collection, and tracing.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 183 - Functional Testing Epic Phase 3.
/// bd-0h6bf: Saga Performance Tests (5 tests).
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "183")]
[Trait("Component", "SagaAdvanced")]
[Trait("Category", "Unit")]
public sealed class SagaPerformanceWorkflowShould
{
	/// <summary>
	/// Tests that multiple saga instances execute concurrently without interference.
	/// Multiple concurrent sagas > All complete independently.
	/// </summary>
	[Fact]
	public async Task ExecuteConcurrentSagaInstances()
	{
		// Arrange
		var store = new PerformanceSagaStore();
		var metrics = new SagaMetricsCollector();
		const int concurrentSagas = 10;

		// Act - Start all sagas concurrently
		var tasks = Enumerable.Range(1, concurrentSagas).Select(async i =>
		{
			var log = new ExecutionLog();
			var saga = new PerformanceSaga(store, log, metrics);
			var sagaId = $"saga-concurrent-{i:D3}";

			await saga.StartAsync(sagaId, new OrderData { OrderId = $"ORD-{i:D3}" }).ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "ValidateOrder").ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "ReserveInventory").ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "ProcessPayment").ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "ShipOrder").ConfigureAwait(false);
			await saga.CompleteAsync(sagaId).ConfigureAwait(false);

			return sagaId;
		}).ToList();

		var completedSagaIds = await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - All sagas completed
		completedSagaIds.Length.ShouldBe(concurrentSagas);

		foreach (var sagaId in completedSagaIds)
		{
			var state = await store.GetAsync(sagaId).ConfigureAwait(true);
			_ = state.ShouldNotBeNull();
			state.Status.ShouldBe(SagaStatus.Completed);
			state.CompletedSteps.Count.ShouldBe(4);
		}

		// Assert - No cross-saga contamination
		var allStates = await store.GetAllAsync().ConfigureAwait(true);
		allStates.Count.ShouldBe(concurrentSagas);

		var uniqueOrderIds = allStates.Select(s => s.Data.OrderId).Distinct().ToList();
		uniqueOrderIds.Count.ShouldBe(concurrentSagas);

		// Assert - Metrics captured
		metrics.SagaStartedCount.ShouldBe(concurrentSagas);
		metrics.SagaCompletedCount.ShouldBe(concurrentSagas);
	}

	/// <summary>
	/// Tests saga throughput under sustained load.
	/// Process N sagas > Measure throughput > Report rate.
	/// </summary>
	[Fact]
	public async Task MeasureSagaThroughput()
	{
		// Arrange
		var store = new PerformanceSagaStore();
		var metrics = new SagaMetricsCollector();
		const int totalSagas = 50;

		// Act - Measure throughput
		var sw = Stopwatch.StartNew();

		var tasks = Enumerable.Range(1, totalSagas).Select(async i =>
		{
			var log = new ExecutionLog();
			var saga = new PerformanceSaga(store, log, metrics);
			var sagaId = $"saga-throughput-{i:D3}";

			await saga.StartAsync(sagaId, new OrderData { OrderId = $"ORD-{i:D3}" }).ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "Step1").ConfigureAwait(false);
			await saga.ProcessStepAsync(sagaId, "Step2").ConfigureAwait(false);
			await saga.CompleteAsync(sagaId).ConfigureAwait(false);
		}).ToList();

		await Task.WhenAll(tasks).ConfigureAwait(true);
		sw.Stop();

		// Assert - Reasonable throughput achieved
		var elapsedSeconds = sw.Elapsed.TotalSeconds;
		var throughput = totalSagas / elapsedSeconds;

		// Should process at least 10 sagas per second (conservative for test stability)
		throughput.ShouldBeGreaterThan(10);

		// Store throughput metric
		metrics.RecordThroughput("SagasPerSecond", throughput);

		// Verify all completed
		var allStates = await store.GetAllAsync().ConfigureAwait(true);
		allStates.Count.ShouldBe(totalSagas);
		allStates.All(s => s.Status == SagaStatus.Completed).ShouldBeTrue();
	}

	/// <summary>
	/// Tests saga step latency measurement.
	/// Execute steps > Measure individual latencies > Report p50/p95/p99.
	/// </summary>
	[Fact]
	public async Task MeasureSagaStepLatency()
	{
		// Arrange
		var store = new PerformanceSagaStore();
		var metrics = new SagaMetricsCollector();
		var log = new ExecutionLog();
		var saga = new PerformanceSaga(store, log, metrics);
		const int iterations = 20;

		// Act - Execute multiple sagas and collect latencies
		for (int i = 1; i <= iterations; i++)
		{
			var sagaId = $"saga-latency-{i:D3}";
			await saga.StartAsync(sagaId, new OrderData { OrderId = $"ORD-{i:D3}" }).ConfigureAwait(true);

			// Execute steps with latency tracking
			await saga.ProcessStepWithLatencyAsync(sagaId, "ValidateOrder").ConfigureAwait(true);
			await saga.ProcessStepWithLatencyAsync(sagaId, "ProcessPayment").ConfigureAwait(true);
			await saga.ProcessStepWithLatencyAsync(sagaId, "ShipOrder").ConfigureAwait(true);
			await saga.CompleteAsync(sagaId).ConfigureAwait(true);
		}

		// Assert - Latency metrics collected
		metrics.StepLatencies.Count.ShouldBeGreaterThan(0);

		// Calculate percentiles
		var allLatencies = metrics.StepLatencies.Values
			.SelectMany(l => l)
			.OrderBy(l => l)
			.ToList();

		allLatencies.Count.ShouldBe(iterations * 3); // 3 steps per saga

		var p50Index = (int)(allLatencies.Count * 0.50);
		var p95Index = (int)(allLatencies.Count * 0.95);
		var p99Index = (int)(allLatencies.Count * 0.99);

		var p50 = allLatencies[p50Index];
		var p95 = allLatencies[p95Index];
		var p99 = allLatencies[p99Index];

		// Assert - Latencies within reasonable bounds (< 100ms for in-memory)
		p50.ShouldBeLessThan(100);
		p95.ShouldBeLessThan(100);
		p99.ShouldBeLessThan(100);

		// Record percentiles
		metrics.RecordLatencyPercentile("p50", p50);
		metrics.RecordLatencyPercentile("p95", p95);
		metrics.RecordLatencyPercentile("p99", p99);
	}

	/// <summary>
	/// Tests saga metrics aggregation and reporting.
	/// Execute sagas > Collect metrics > Aggregate and report.
	/// </summary>
	[Fact]
	public async Task CollectAndAggregateMetrics()
	{
		// Arrange
		var store = new PerformanceSagaStore();
		var metrics = new SagaMetricsCollector();
		var log = new ExecutionLog();
		var saga = new PerformanceSaga(store, log, metrics);

		// Act - Execute sagas with various outcomes
		for (int i = 1; i <= 10; i++)
		{
			var sagaId = $"saga-metrics-{i:D3}";
			await saga.StartAsync(sagaId, new OrderData
			{
				OrderId = $"ORD-{i:D3}",
				Amount = i * 100m,
			}).ConfigureAwait(true);

			await saga.ProcessStepAsync(sagaId, "Step1").ConfigureAwait(true);

			// Simulate some failures (every 3rd saga fails)
			if (i % 3 == 0)
			{
				await saga.FailAsync(sagaId, "Simulated failure").ConfigureAwait(true);
			}
			else
			{
				await saga.ProcessStepAsync(sagaId, "Step2").ConfigureAwait(true);
				await saga.CompleteAsync(sagaId).ConfigureAwait(true);
			}
		}

		// Assert - Metrics aggregated correctly
		metrics.SagaStartedCount.ShouldBe(10);
		metrics.SagaCompletedCount.ShouldBe(7); // 10 - 3 failed
		metrics.SagaFailedCount.ShouldBe(3);

		// Assert - Success rate calculated
		var successRate = (double)metrics.SagaCompletedCount / metrics.SagaStartedCount;
		successRate.ShouldBe(0.7, 0.01);

		// Assert - Step counts tracked
		metrics.StepExecutedCount.ShouldBeGreaterThan(0);

		// Assert - Amount aggregation
		var totalAmount = metrics.TotalProcessedAmount;
		totalAmount.ShouldBeGreaterThan(0);

		// Generate metrics report
		var report = metrics.GenerateReport();
		report.ShouldContain("Started: 10");
		report.ShouldContain("Completed: 7");
		report.ShouldContain("Failed: 3");
		report.ShouldContain("Success Rate: 70");
	}

	/// <summary>
	/// Tests distributed tracing with span creation and correlation.
	/// Execute saga > Create spans > Verify trace hierarchy.
	/// </summary>
	[Fact]
	public async Task CreateDistributedTracingSpans()
	{
		// Arrange
		var store = new PerformanceSagaStore();
		var metrics = new SagaMetricsCollector();
		var tracer = new SimulatedTracer();
		var log = new ExecutionLog();
		var saga = new TracedSaga(store, log, metrics, tracer);

		var traceId = Guid.NewGuid().ToString("N");

		// Act - Execute saga with tracing
		await saga.StartWithTraceAsync("saga-traced", new OrderData { OrderId = "ORD-TRACED" }, traceId).ConfigureAwait(true);
		await saga.ProcessStepWithTraceAsync("saga-traced", "ValidateOrder").ConfigureAwait(true);
		await saga.ProcessStepWithTraceAsync("saga-traced", "ReserveInventory").ConfigureAwait(true);
		await saga.ProcessStepWithTraceAsync("saga-traced", "ProcessPayment").ConfigureAwait(true);
		await saga.ProcessStepWithTraceAsync("saga-traced", "ShipOrder").ConfigureAwait(true);
		await saga.CompleteWithTraceAsync("saga-traced").ConfigureAwait(true);

		// Assert - Trace spans created
		tracer.Spans.Count.ShouldBeGreaterThan(0);

		// Assert - Root span exists
		var rootSpan = tracer.Spans.FirstOrDefault(s => s.ParentSpanId == null);
		_ = rootSpan.ShouldNotBeNull();
		rootSpan.TraceId.ShouldBe(traceId);
		rootSpan.OperationName.ShouldBe("Saga:saga-traced");

		// Assert - Step spans exist with correct parent
		var stepSpans = tracer.Spans.Where(s => s.ParentSpanId == rootSpan.SpanId).ToList();
		stepSpans.Count.ShouldBeGreaterThanOrEqualTo(4);

		// Assert - Span names match steps
		var stepNames = stepSpans.Select(s => s.OperationName).ToList();
		stepNames.ShouldContain("Step:ValidateOrder");
		stepNames.ShouldContain("Step:ReserveInventory");
		stepNames.ShouldContain("Step:ProcessPayment");
		stepNames.ShouldContain("Step:ShipOrder");

		// Assert - All spans have same trace ID
		tracer.Spans.All(s => s.TraceId == traceId).ShouldBeTrue();

		// Assert - Spans have timing
		var completedSpans = tracer.Spans.Where(s => s.EndTime.HasValue).ToList();
		completedSpans.Count.ShouldBe(tracer.Spans.Count);

		foreach (var span in completedSpans)
		{
			span.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		}
	}

	#region Test Infrastructure

	internal enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
	}

	internal sealed class ExecutionLog
	{
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
		}
	}

	internal sealed class OrderData
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	internal sealed class PerformanceSagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public OrderData Data { get; init; } = new();
		public List<string> CompletedSteps { get; } = [];
		public string? FailureReason { get; set; }
	}

	internal sealed class PerformanceSagaStore
	{
		private readonly ConcurrentDictionary<string, PerformanceSagaState> _sagas = new();

		public Task SaveAsync(PerformanceSagaState state)
		{
			_sagas[state.SagaId] = state;
			return Task.CompletedTask;
		}

		public Task<PerformanceSagaState?> GetAsync(string sagaId)
		{
			_ = _sagas.TryGetValue(sagaId, out var state);
			return Task.FromResult(state);
		}

		public Task<List<PerformanceSagaState>> GetAllAsync()
		{
			return Task.FromResult(_sagas.Values.ToList());
		}
	}

	internal sealed class SagaMetricsCollector
	{
		private readonly object _amountLock = new();
		private readonly ConcurrentDictionary<string, double> _throughputMetrics = new();
		private readonly ConcurrentDictionary<string, double> _latencyPercentiles = new();
		private int _sagaStartedCount;
		private int _sagaCompletedCount;
		private int _sagaFailedCount;
		private int _stepExecutedCount;
		private decimal _totalProcessedAmount;
		public int SagaStartedCount => _sagaStartedCount;
		public int SagaCompletedCount => _sagaCompletedCount;
		public int SagaFailedCount => _sagaFailedCount;
		public int StepExecutedCount => _stepExecutedCount;
		public decimal TotalProcessedAmount { get { lock (_amountLock) { return _totalProcessedAmount; } } }
		public ConcurrentDictionary<string, List<double>> StepLatencies { get; } = new();

		public void RecordSagaStarted(decimal amount)
		{
			_ = Interlocked.Increment(ref _sagaStartedCount);
			lock (_amountLock)
			{
				_totalProcessedAmount += amount;
			}
		}

		public void RecordSagaCompleted()
		{
			_ = Interlocked.Increment(ref _sagaCompletedCount);
		}

		public void RecordSagaFailed()
		{
			_ = Interlocked.Increment(ref _sagaFailedCount);
		}

		public void RecordStepExecuted(string stepName, double latencyMs)
		{
			_ = Interlocked.Increment(ref _stepExecutedCount);
			_ = StepLatencies.AddOrUpdate(
				stepName,
				_ => [latencyMs],
				(_, list) =>
				{
					lock (list)
					{
						list.Add(latencyMs);
					}
					return list;
				});
		}

		public void RecordThroughput(string metricName, double value)
		{
			_throughputMetrics[metricName] = value;
		}

		public void RecordLatencyPercentile(string percentile, double value)
		{
			_latencyPercentiles[percentile] = value;
		}

		public string GenerateReport()
		{
			var successRate = _sagaStartedCount > 0 ? (double)_sagaCompletedCount / _sagaStartedCount * 100 : 0;
			return $"Started: {_sagaStartedCount}, Completed: {_sagaCompletedCount}, Failed: {_sagaFailedCount}, Success Rate: {successRate:F0}%";
		}
	}

	internal sealed class PerformanceSaga
	{
		private readonly PerformanceSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly SagaMetricsCollector _metrics;

		public PerformanceSaga(PerformanceSagaStore store, ExecutionLog log, SagaMetricsCollector metrics)
		{
			_store = store;
			_log = log;
			_metrics = metrics;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new PerformanceSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordSagaStarted(data.Amount);
			_log.Log($"Saga:Start:{sagaId}");
		}

		public async Task ProcessStepAsync(string sagaId, string step)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CompletedSteps.Add(step);
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordStepExecuted(step, 0);
			_log.Log($"{step}:Execute");
		}

		public async Task ProcessStepWithLatencyAsync(string sagaId, string step)
		{
			var sw = Stopwatch.StartNew();

			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.InProgress;
			state.CompletedSteps.Add(step);
			await _store.SaveAsync(state).ConfigureAwait(false);

			sw.Stop();
			_metrics.RecordStepExecuted(step, sw.Elapsed.TotalMilliseconds);
			_log.Log($"{step}:Execute:Latency:{sw.Elapsed.TotalMilliseconds:F2}ms");
		}

		public async Task CompleteAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.Completed;
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordSagaCompleted();
			_log.Log($"Saga:Complete:{sagaId}");
		}

		public async Task FailAsync(string sagaId, string reason)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.Failed;
			state.FailureReason = reason;
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordSagaFailed();
			_log.Log($"Saga:Failed:{sagaId}:{reason}");
		}
	}

	internal sealed class TracingSpan
	{
		public string SpanId { get; } = Guid.NewGuid().ToString("N")[..16];
		public string TraceId { get; init; } = string.Empty;
		public string? ParentSpanId { get; init; }
		public string OperationName { get; init; } = string.Empty;
		public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? EndTime { get; set; }
		public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
		public Dictionary<string, string> Tags { get; } = [];
	}

	internal sealed class SimulatedTracer
	{
		private readonly AsyncLocal<TracingSpan?> _currentSpan = new();
		public ConcurrentBag<TracingSpan> Spans { get; } = [];

		public TracingSpan StartSpan(string operationName, string traceId, string? parentSpanId = null)
		{
			var span = new TracingSpan
			{
				TraceId = traceId,
				ParentSpanId = parentSpanId ?? _currentSpan.Value?.SpanId,
				OperationName = operationName,
			};
			Spans.Add(span);
			_currentSpan.Value = span;
			return span;
		}

		public void EndSpan(TracingSpan span)
		{
			span.EndTime = DateTimeOffset.UtcNow;
			if (_currentSpan.Value?.SpanId == span.SpanId)
			{
				// Find parent span
				_currentSpan.Value = Spans.FirstOrDefault(s => s.SpanId == span.ParentSpanId);
			}
		}

		public TracingSpan? GetCurrentSpan() => _currentSpan.Value;
	}

	internal sealed class TracedSaga
	{
		private readonly PerformanceSagaStore _store;
		private readonly ExecutionLog _log;
		private readonly SagaMetricsCollector _metrics;
		private readonly SimulatedTracer _tracer;
		private TracingSpan? _rootSpan;

		public TracedSaga(PerformanceSagaStore store, ExecutionLog log, SagaMetricsCollector metrics, SimulatedTracer tracer)
		{
			_store = store;
			_log = log;
			_metrics = metrics;
			_tracer = tracer;
		}

		public async Task StartWithTraceAsync(string sagaId, OrderData data, string traceId)
		{
			_rootSpan = _tracer.StartSpan($"Saga:{sagaId}", traceId);

			var state = new PerformanceSagaState
			{
				SagaId = sagaId,
				Data = data,
				Status = SagaStatus.Pending,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordSagaStarted(data.Amount);
			_log.Log($"Saga:Start:{sagaId}:Trace:{traceId}");
		}

		public async Task ProcessStepWithTraceAsync(string sagaId, string step)
		{
			var stepSpan = _tracer.StartSpan($"Step:{step}", _rootSpan.TraceId, _rootSpan.SpanId);
			stepSpan.Tags["saga.id"] = sagaId;
			stepSpan.Tags["saga.step"] = step;

			try
			{
				var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
				if (state == null)
				{
					throw new InvalidOperationException($"Saga {sagaId} not found");
				}

				state.Status = SagaStatus.InProgress;
				state.CompletedSteps.Add(step);
				await _store.SaveAsync(state).ConfigureAwait(false);
				_log.Log($"{step}:Execute:Traced");

				stepSpan.Tags["saga.step.status"] = "completed";
			}
			finally
			{
				_tracer.EndSpan(stepSpan);
			}
		}

		public async Task CompleteWithTraceAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			if (state == null)
			{
				throw new InvalidOperationException($"Saga {sagaId} not found");
			}

			state.Status = SagaStatus.Completed;
			await _store.SaveAsync(state).ConfigureAwait(false);
			_metrics.RecordSagaCompleted();

			_rootSpan.Tags["saga.status"] = "completed";
			_tracer.EndSpan(_rootSpan);

			_log.Log($"Saga:Complete:{sagaId}:Traced");
		}
	}

	#endregion Test Infrastructure
}
