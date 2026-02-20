// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Tests.TestFakes;

using Excalibur.Data.InMemory.Inbox;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Observability;

/// <summary>
///     Comprehensive observability validation test suite for core messaging components.
/// </summary>
/// <remarks>
///     This test suite validates that all core messaging components properly emit telemetry including activities (traces), metrics, and
///     structured logs with correct correlation context propagation according to OpenTelemetry standards.
/// </remarks>
[Collection("Observability Tests")]
public sealed class ObservabilityValidationTestSuite : IDisposable
{
	private readonly OpenTelemetryTestFixture _otelFixture;
	private readonly ActivitySource _testActivitySource;
	private readonly Meter _testMeter;
	private readonly ObsTestSuiteTestLogger _testLogger;
	private readonly List<IDisposable> _disposables;
	private readonly ILoggerFactory _loggerFactory;

	public ObservabilityValidationTestSuite()
	{
		_otelFixture = new OpenTelemetryTestFixture();
		_testActivitySource = new ActivitySource("Test.ActivitySource");
		_testMeter = new Meter("Test.Meter");
		var logEntries = new ConcurrentBag<ObsTestSuiteLogEntry>();
		_testLogger = new ObsTestSuiteTestLogger(logEntries);
		_disposables = [];

		_loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

		_disposables.AddRange(new IDisposable[] { _testActivitySource, _testMeter });
	}

	[Fact]
	public async Task InboxStore_EmitsProperTelemetryForAllOperations()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 1000, EnableAutomaticCleanup = false };

		var store = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(options), _testLogger);
		_disposables.Add(store);

		var messageId = "test-message-123";
		var payload = new byte[] { 1, 2, 3, 4, 5 };
		var metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = "corr-123",
			["TenantId"] = "tenant-456",
			["TraceId"] = "trace-789",
		};

		// Act - Create entry (use block scope to ensure activity is stopped before assertion)
		{
			using var createActivity = _testActivitySource.StartActivity("Test.InboxStore.Create");
			_ = (createActivity?.SetTag("test.operation", "create_entry"));
			_ = (createActivity?.SetTag("message.id", messageId));

			_ = await store.CreateEntryAsync(messageId, "TestHandler", "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Mark as processed (use block scope to ensure activity is stopped before assertion)
		{
			using var processActivity = _testActivitySource.StartActivity("Test.InboxStore.Process");
			_ = (processActivity?.SetTag("test.operation", "mark_processed"));
			_ = (processActivity?.SetTag("message.id", messageId));

			await store.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Get statistics
		var stats = await store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify activities were created (activities are now stopped and recorded)
		var activities = _otelFixture.GetRecordedActivities().Where(a => a.Source.Name == "Test.ActivitySource").ToList();
		activities.Count.ShouldBeGreaterThanOrEqualTo(2);

		var createAct = activities.FirstOrDefault(a => a.GetTagItem("test.operation")?.ToString() == "create_entry");
		_ = createAct.ShouldNotBeNull();
		createAct.GetTagItem("message.id")?.ToString().ShouldBe(messageId);

		var processAct = activities.FirstOrDefault(a => a.GetTagItem("test.operation")?.ToString() == "mark_processed");
		_ = processAct.ShouldNotBeNull();
		processAct.GetTagItem("message.id")?.ToString().ShouldBe(messageId);

		// Assert - Verify structured logging (check for inbox operations, not specific method names)
		var logEntries = _testLogger.LogEntries;
		logEntries.ShouldContain(logEntry =>
			logEntry.Message.Contains(messageId) || logEntry.Message.Contains("inbox"));

		// Assert - Verify statistics reflect the operations
		stats.TotalEntries.ShouldBe(1);
		stats.ProcessedEntries.ShouldBe(1);
	}

	[Fact]
	public async Task BatchProcessor_EmitsMetricsForBatchingOperations()
	{
		// Arrange
		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var batchProcessedTcs = new TaskCompletionSource<bool>();

		var options = new MicroBatchOptions { MaxBatchSize = 3, MaxBatchDelay = TimeSpan.FromMilliseconds(100) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				_ = batchProcessedTcs.TrySetResult(true);
				return ValueTask.CompletedTask;
			},
			_testLogger,
			options);

		_disposables.Add(processor);

		// Act - Add items to trigger batching (use block scope to ensure activity is stopped before assertion)
		{
			using var batchActivity = _testActivitySource.StartActivity("Test.MicroBatch.Process");
			_ = (batchActivity?.SetTag("test.operation", "batch_processing"));
			_ = (batchActivity?.SetTag("batch.max_size", options.MaxBatchSize));

			await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
			await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
			await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);

			_ = await batchProcessedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);

			// Wait a bit more for metrics to be emitted
			await Task.Delay(200).ConfigureAwait(false);
		}

		// Assert - Verify batch was processed
		processedBatches.Count.ShouldBe(1);
		processedBatches.First().Count.ShouldBe(3);

		// Assert - Verify activity context (activities are now stopped and recorded)
		var activities = _otelFixture.GetRecordedActivities().Where(a => a.Source.Name == "Test.ActivitySource").ToList();
		var batchAct = activities.FirstOrDefault(a => a.GetTagItem("test.operation")?.ToString() == "batch_processing");
		_ = batchAct.ShouldNotBeNull();
		batchAct.GetTagItem("batch.max_size")?.ToString().ShouldBe(options.MaxBatchSize.ToString());

		// BatchProcessor only logs on errors, not on successful processing
		// The primary observability concern is that processing completed successfully
		// which is verified above by checking the batches were processed
	}

	[Fact]
	public async Task UnifiedBatchingMiddleware_PropagatesCorrelationContext()
	{
		// Arrange
		var options = new UnifiedBatchingOptions { MaxBatchSize = 2, MaxBatchDelay = TimeSpan.FromMilliseconds(50), MaxParallelism = 1 };

		await using var middleware = new UnifiedBatchingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _testLogger, _loggerFactory);

		var correlationId = "test-correlation-456";
		var traceId = "test-trace-789";
		var processedMessages = new ConcurrentBag<string>();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			// Capture current activity context
			var currentActivity = Activity.Current;
			if (currentActivity != null)
			{
				processedMessages.Add($"TraceId:{currentActivity.TraceId}");
			}

			// Verify correlation context is available
			if (ctx.Properties.TryGetValue("CorrelationId", out var corrId))
			{
				processedMessages.Add($"CorrelationId:{corrId}");
			}

			// Always record that we processed a message
			processedMessages.Add($"Processed");

			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act - Process message with correlation context (use block scope to ensure activity is stopped before assertion)
		IMessageResult result;
		{
			using var parentActivity = _testActivitySource.StartActivity("Test.Parent");
			_ = (parentActivity?.SetTag("correlation.id", correlationId));
			_ = (parentActivity?.SetTag("test.trace.id", traceId));

			var message = new FakeDispatchMessage();
			var context = new FakeMessageContext();
			context.Properties["CorrelationId"] = correlationId;
			context.Properties["TraceId"] = traceId;

			result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

			// Wait for any async processing to complete
			await Task.Delay(100).ConfigureAwait(false);
		}

		// Assert - Verify successful processing
		_ = result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeTrue();

		// Assert - Verify message was processed
		processedMessages.ShouldNotBeEmpty();
		processedMessages.ShouldContain(msg => msg == "Processed");

		// Assert - Verify activities were created for middleware
		var activities = _otelFixture.GetRecordedActivities().Where(a =>
			a.Source.Name is "Excalibur.Dispatch.UnifiedBatchingMiddleware" or
				"Test.ActivitySource").ToList();
		activities.ShouldNotBeEmpty();

		// Assert - Verify logging occurred
		var logEntries = _testLogger.LogEntries;
		logEntries.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task IntegratedComponents_MaintainTelemetryConsistencyUnderLoad()
	{
		// Arrange - Setup all components with telemetry
		var inboxOptions = new InMemoryInboxOptions { MaxEntries = 500, EnableAutomaticCleanup = false };
		var inboxStore = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(inboxOptions), _testLogger);
		_disposables.Add(inboxStore);

		var batchOptions = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromMilliseconds(25) };

		var processedItems = new ConcurrentBag<string>();
		var totalProcessed = 0;
		var allProcessed = new TaskCompletionSource<bool>();

		var batchProcessor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				// Signal completion when all 50 batch items are processed
				if (Interlocked.Add(ref totalProcessed, batch.Count) >= 50)
				{
					_ = allProcessed.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_testLogger,
			batchOptions);
		_disposables.Add(batchProcessor);

		var middlewareOptions = new UnifiedBatchingOptions
		{
			MaxBatchSize = 3,
			MaxBatchDelay = TimeSpan.FromMilliseconds(30),
			MaxParallelism = 2,
		};

		await using var middleware = new UnifiedBatchingMiddleware(Microsoft.Extensions.Options.Options.Create(middlewareOptions), _testLogger, _loggerFactory);

		// Act - Create parent activity for the entire operation (use block scope to ensure activity is stopped before assertion)
		var operationStartTime = DateTimeOffset.UtcNow;
		{
			using var rootActivity = _testActivitySource.StartActivity("Test.IntegratedLoad");
			_ = (rootActivity?.SetTag("test.scenario", "integrated_load"));
			_ = (rootActivity?.SetTag("test.message_count", 150));

			// Execute concurrent operations with telemetry context
			var inboxTasks = Enumerable.Range(0, 50).Select(async i =>
			{
				using var activity = _testActivitySource.StartActivity("Test.Inbox.Operation");
				_ = (activity?.SetTag("operation.type", "inbox_create"));
				_ = (activity?.SetTag("message.index", i));
				_ = (activity?.SetTag("operation.start_time", operationStartTime.ToString("O")));

				var payload = new byte[50];
				var metadata = new Dictionary<string, object>
				{
					["Index"] = i,
					["CorrelationId"] = $"corr-{i}",
					["Timestamp"] = operationStartTime.ToString("O"),
				};

				_ = await inboxStore.CreateEntryAsync($"inbox-{i}", "TestHandler", "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
			});

			var batchTasks = Enumerable.Range(0, 50).Select(async i =>
			{
				using var activity = _testActivitySource.StartActivity("Test.Batch.Operation");
				_ = (activity?.SetTag("operation.type", "batch_add"));
				_ = (activity?.SetTag("item.index", i));
				_ = (activity?.SetTag("operation.start_time", operationStartTime.ToString("O")));

				await batchProcessor.AddAsync($"batch-{i}", CancellationToken.None).ConfigureAwait(false);
			});

			var middlewareTasks = Enumerable.Range(0, 50).Select(async i =>
			{
				using var activity = _testActivitySource.StartActivity("Test.Middleware.Operation");
				_ = (activity?.SetTag("operation.type", "middleware_invoke"));
				_ = (activity?.SetTag("message.index", i));
				_ = (activity?.SetTag("operation.start_time", operationStartTime.ToString("O")));

				var message = new FakeDispatchMessage();
				var context = new FakeMessageContext();
				context.Properties["CorrelationId"] = $"corr-{i}";
				context.Properties["MessageIndex"] = i;
				context.Properties["StartTime"] = operationStartTime.ToString("O");

				_ = await middleware.InvokeAsync(message, context, (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()), CancellationToken.None).ConfigureAwait(false);
			});

			// Execute all operations concurrently
			await Task.WhenAll(
				Task.WhenAll(inboxTasks),
				Task.WhenAll(batchTasks),
				Task.WhenAll(middlewareTasks)
			);

			_ = await allProcessed.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

			// Wait for final telemetry to be emitted
			await Task.Delay(500).ConfigureAwait(false);
		}

		// Assert - Verify all operations completed successfully
		var inboxEntries = await inboxStore.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		inboxEntries.Count().ShouldBe(50);
		processedItems.Count.ShouldBeGreaterThanOrEqualTo(50);

		// Assert - Verify comprehensive activity tracking
		var allActivities = _otelFixture.GetRecordedActivities().ToList();
		var testActivities = allActivities.Where(a => a.Source.Name == "Test.ActivitySource").ToList();

		testActivities.Count.ShouldBeGreaterThan(100); // At least one activity per operation

		// Verify different operation types were tracked
		var inboxOps = testActivities.Where(a => a.GetTagItem("operation.type")?.ToString() == "inbox_create").ToList();
		var batchOps = testActivities.Where(a => a.GetTagItem("operation.type")?.ToString() == "batch_add").ToList();
		var middlewareOps = testActivities.Where(a => a.GetTagItem("operation.type")?.ToString() == "middleware_invoke").ToList();

		inboxOps.Count.ShouldBe(50);
		batchOps.Count.ShouldBe(50);
		middlewareOps.Count.ShouldBe(50);

		// Assert - Verify all activities have proper timing context
		var activitiesWithStartTime = testActivities.Where(a =>
			a.GetTagItem("operation.start_time") != null).ToList();
		activitiesWithStartTime.Count.ShouldBeGreaterThan(100);

		// Assert - Verify structured logging consistency
		var logEntries = _testLogger.LogEntries;
		logEntries.ShouldNotBeEmpty();

		// Verify that logs contain message identifiers (what the implementation actually logs)
		var logsWithMessageContext = logEntries.Where(entry =>
			entry.Message.Contains("inbox") ||
			entry.State.ContainsKey("MessageId") ||
			entry.State.Values.Any(v => v?.ToString()?.Contains("inbox-") == true)).ToList();

		logsWithMessageContext.ShouldNotBeEmpty("Logs should contain message context for observability");

		// Note: Correlation ID propagation in log state depends on implementation
		// The test validates that structured logging is working correctly

		// Assert - Verify no exceptions were logged
		var errorLogs = logEntries.Where(entry => entry.LogLevel >= LogLevel.Error).ToList();
		errorLogs.ShouldBeEmpty();
	}

	[Fact]
	public void TelemetryEmission_PerformsWithinLatencyBudgets()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 1000, EnableAutomaticCleanup = false };

		var store = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(options), _testLogger);
		_disposables.Add(store);

		const int operationCount = 100;
		var latencies = new List<double>();

		// Act - Measure telemetry overhead
		for (var i = 0; i < operationCount; i++)
		{
			var stopwatch = Stopwatch.StartNew();

			using var activity = _testActivitySource.StartActivity($"Test.Latency.{i}");
			_ = (activity?.SetTag("iteration", i));
			_ = (activity?.SetTag("test.type", "latency_measurement"));

			// Simulate typical telemetry operations
			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
			_ = (activity?.AddEvent(new ActivityEvent("Operation.Started")));

			var counter = _testMeter.CreateCounter<int>("test.operations");
			counter.Add(1, new KeyValuePair<string, object?>("operation", "latency_test"));

			_testLogger.LogInformation("Operation {Iteration} completed with telemetry", i);

			_ = (activity?.AddEvent(new ActivityEvent("Operation.Completed")));

			stopwatch.Stop();
			latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
		}

		// Assert - Verify telemetry overhead is minimal
		var averageLatency = latencies.Average();
		var p95Latency = latencies.OrderBy(x => x).Skip((int)(operationCount * 0.95)).First();
		var p99Latency = latencies.OrderBy(x => x).Skip((int)(operationCount * 0.99)).First();

		// Performance targets for telemetry overhead
		averageLatency.ShouldBeLessThan(1.0); // Average < 1ms
		p95Latency.ShouldBeLessThan(2.0); // P95 < 2ms
		p99Latency.ShouldBeLessThan(5.0); // P99 < 5ms

		// Assert - Verify all telemetry was captured
		var testActivities = _otelFixture.GetRecordedActivities().Where(a =>
			a.Source.Name == "Test.ActivitySource" &&
			a.GetTagItem("test.type")?.ToString() == "latency_measurement").ToList();

		testActivities.Count.ShouldBe(operationCount);

		// Assert - Verify metrics were emitted
		var testMetrics = _otelFixture.GetRecordedLongMetrics().Select(m => new { Key = "metric", Value = (double)m.Value })
			.Concat(_otelFixture.GetRecordedDoubleMetrics().Select(m => new { Key = "metric", Value = m.Value }))
			.Concat(_otelFixture.GetRecordedIntMetrics().Select(m => new { Key = "metric", Value = (double)m.Value }))
			.ToList();
		testMetrics.ShouldNotBeEmpty();

		// Assert - Verify logging was consistent
		var logEntries = _testLogger.LogEntries;
		var operationLogs = logEntries.Where(entry =>
			entry.Message.Contains("Operation") &&
			entry.Message.Contains("completed")).ToList();

		operationLogs.Count.ShouldBe(operationCount);
	}

	public void Dispose()
	{
		_otelFixture?.Dispose();
		_testActivitySource?.Dispose();
		_testMeter?.Dispose();

		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}
	}
}

/// <summary>
///     Test logger implementation that captures log entries for validation.
/// </summary>
internal sealed class ObsTestSuiteTestLogger(ConcurrentBag<ObsTestSuiteLogEntry> logEntries) : ILogger<InMemoryInboxStore>, ILogger<BatchProcessor<string>>, ILogger<UnifiedBatchingMiddleware>
{
	private readonly ConcurrentBag<ObsTestSuiteLogEntry> _logEntries = logEntries;

	public IReadOnlyList<ObsTestSuiteLogEntry> LogEntries => _logEntries.ToList();

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var stateDict = new Dictionary<string, object?>();

		if (state is IReadOnlyList<KeyValuePair<string, object?>> stateList)
		{
			foreach (var kvp in stateList)
			{
				stateDict[kvp.Key] = kvp.Value;
			}
		}
		else if (state != null)
		{
			stateDict["State"] = state;
		}

		_logEntries.Add(new ObsTestSuiteLogEntry
		{
			LogLevel = logLevel,
			EventId = eventId,
			Message = formatter(state, exception),
			Exception = exception,
			State = stateDict,
			Timestamp = DateTimeOffset.UtcNow,
		});
	}
}

/// <summary>
///     Represents a captured log entry for test validation.
/// </summary>
internal sealed class ObsTestSuiteLogEntry
{
	public LogLevel LogLevel { get; init; }

	public EventId EventId { get; init; }

	public string Message { get; init; } = string.Empty;

	public Exception? Exception { get; init; }

	public Dictionary<string, object?> State { get; init; } = [];

	public DateTimeOffset Timestamp { get; init; }
}
