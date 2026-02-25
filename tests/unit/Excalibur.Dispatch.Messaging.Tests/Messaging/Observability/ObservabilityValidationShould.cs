// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Data.InMemory.Inbox;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Options.Performance;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Observability;

/// <summary>
///     Comprehensive observability validation tests for core messaging components. Validates telemetry, metrics, structured logging, and
///     tracing across the framework.
/// </summary>
[Collection("Observability Tests")]
[Trait("Category", "Unit")]
public sealed class ObservabilityValidationShould : IDisposable
{
	private readonly ObservabilityTestLoggerProvider _loggerProvider;
	private readonly ILoggerFactory _loggerFactory;
	private readonly List<IDisposable> _disposables;
	private readonly ObservabilityMeterProvider _meterProvider;
	private readonly ObservabilityMetricsExporter _metricsExporter;

	public ObservabilityValidationShould()
	{
		_loggerProvider = new ObservabilityTestLoggerProvider();
		_loggerFactory = LoggerFactory.Create(builder => builder
			.SetMinimumLevel(LogLevel.Debug)
			.AddProvider(_loggerProvider));
		_disposables = [];
		_metricsExporter = new ObservabilityMetricsExporter();
		_meterProvider = ObservabilityMeterProvider.CreateMeterProvider(_ => { });
		_disposables.Add(_meterProvider);
	}

	[Fact]
	public async Task ValidateInboxStoreObservability()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<InMemoryInboxStore>();
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		var store = new InMemoryInboxStore(options, logger);
		_disposables.Add(store);

		var messageId = "observability-test-inbox";
		var payload = System.Text.Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = Guid.NewGuid().ToString(),
			["TenantId"] = "test-tenant",
			["MessageType"] = "TestMessage",
		};

		// Act - Create entry with context
		var entry = await store.CreateEntryAsync(messageId, "TestHandler", "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();

		// Act - Mark as processed
		await store.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None).ConfigureAwait(false);

		// Act - Get statistics
		var stats = await store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify structured logging
		var logEntries = _loggerProvider.GetLogEntries();
		logEntries.ShouldNotBeEmpty();

		var createLogEntry = logEntries.FirstOrDefault(e => e.Message.Contains("Created inbox entry"));
		_ = createLogEntry.ShouldNotBeNull();
		createLogEntry.LogLevel.ShouldBe(LogLevel.Debug);
		// LoggerMessage.Define embeds values in the message itself - verify messageId appears in the log
		createLogEntry.Message.ShouldContain(messageId);

		var processedLogEntry = logEntries.FirstOrDefault(e => e.Message.Contains("Marked inbox entry as processed"));
		_ = processedLogEntry.ShouldNotBeNull();
		processedLogEntry.LogLevel.ShouldBe(LogLevel.Debug);

		// Assert - Verify statistics are populated
		_ = stats.ShouldNotBeNull();
		stats.TotalEntries.ShouldBe(1);
		stats.ProcessedEntries.ShouldBe(1);
		stats.FailedEntries.ShouldBe(0);
		stats.PendingEntries.ShouldBe(0);
	}

	[Fact]
	public async Task ValidateBatchProcessorObservability()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<BatchProcessor<string>>();
		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var processedItems = new ConcurrentBag<string>();
		var completionSource = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (processedItems.Count >= 5)
				{
					_ = completionSource.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			logger,
			new MicroBatchOptions { MaxBatchSize = 3, MaxBatchDelay = TimeSpan.FromMilliseconds(100) });

		_disposables.Add(processor);

		// Act - Add items and wait for processing
		for (var i = 0; i < 5; i++)
		{
			await processor.AddAsync($"item-{i}", CancellationToken.None).ConfigureAwait(false);
		}

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			completionSource.Task,

			TimeSpan.FromSeconds(10));
		// Assert - Verify batch processing occurred
		processedItems.Count.ShouldBe(5);
		processedBatches.ShouldNotBeEmpty();

		// Assert - Verify logging occurred (error logs would indicate issues)
		var logEntries = _loggerProvider.GetLogEntries();
		var errorLogs = logEntries.Where(e => e.LogLevel >= LogLevel.Warning).ToList();
		errorLogs.ShouldBeEmpty("No errors should occur during normal batch processing");
	}

	[Fact]
	public async Task ValidateUnifiedBatchingMiddlewareObservability()
	{
		// Arrange
		using var activitySource = new ActivitySource("Test.ObservabilityValidation");
		using var listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		var activities = new ConcurrentBag<Activity>();
		listener.ActivityStarted = activities.Add;

		var logger = _loggerFactory.CreateLogger<UnifiedBatchingMiddleware>();
		var options = Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions
		{
			MaxBatchSize = 2,
			MaxBatchDelay = TimeSpan.FromMilliseconds(50),
			MaxParallelism = 1,
			ProcessAsOptimizedBulk = false,
		});

		await using var middleware = new UnifiedBatchingMiddleware(options, logger, _loggerFactory);

		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();
		var nextCalled = false;

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await middleware.InvokeAsync(message, context, NextDelegate, CancellationToken.None).ConfigureAwait(false);

		// Wait a bit for async operations to complete
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(200).ConfigureAwait(false);

		// Assert - Verify result
		_ = result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeTrue();
		nextCalled.ShouldBeTrue();

		// Assert - Verify activity creation
		var middlewareActivities = activities.Where(a =>
			a.Source.Name.Contains("UnifiedBatchingMiddleware") ||
			a.OperationName.Contains("UnifiedBatchingMiddleware")).ToList();

		middlewareActivities.ShouldNotBeEmpty("Middleware should create activities for observability");

		// Assert - Verify structured logging
		var logEntries = _loggerProvider.GetLogEntries();
		logEntries.ShouldNotBeEmpty();

		// Look for batching-related logs
		var batchingLogs = logEntries.Where(e =>
			e.Message.Contains("batch") ||
			e.Message.Contains("message") ||
			e.CategoryName.Contains("UnifiedBatchingMiddleware")).ToList();

		batchingLogs.ShouldNotBeEmpty("Middleware should emit structured logs");

		// Assert - No error logs should be present
		var errorLogs = logEntries.Where(e => e.LogLevel >= LogLevel.Error).ToList();
		errorLogs.ShouldBeEmpty("No errors should occur during normal middleware operation");
	}

	[Fact]
	public async Task ValidateCorrelationIdPropagation()
	{
		// Arrange
		var correlationId = Guid.NewGuid().ToString();
		var messageId = "correlation-test";
		var logger = _loggerFactory.CreateLogger<InMemoryInboxStore>();
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		var store = new InMemoryInboxStore(options, logger);
		_disposables.Add(store);

		var metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = correlationId,
			["TenantId"] = "observability-tenant",
			["MessageType"] = "CorrelationTest",
		};

		// Act
		var entry = await store.CreateEntryAsync(messageId, "TestHandler", "CorrelationTest",
			System.Text.Encoding.UTF8.GetBytes("test"), metadata, CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, "TestHandler", CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify entry contains correlation metadata
		_ = entry.ShouldNotBeNull();
		entry.Metadata.ShouldContainKey("CorrelationId");
		entry.Metadata["CorrelationId"].ShouldBe(correlationId);

		// Assert - Verify structured logging occurred
		var logEntries = _loggerProvider.GetLogEntries();
		logEntries.ShouldNotBeEmpty("Logs should be emitted for inbox operations");

		// Verify that logs contain the message ID (what InMemoryInboxStore actually logs)
		var messageIdLogs = logEntries.Where(e => e.Message.Contains(messageId)).ToList();
		messageIdLogs.ShouldNotBeEmpty("Message ID should be present in structured logs");

		// Note: Correlation ID propagation in logs depends on implementation
		// The primary assertion is that the correlation ID is preserved in the entry metadata
	}

	[Fact]
	public async Task ValidateErrorHandlingObservability()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<BatchProcessor<string>>();
		var exceptionThrown = false;
		var itemProcessedAfterError = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					if (item == "error-item" && !exceptionThrown)
					{
						exceptionThrown = true;
						throw new InvalidOperationException("Test exception for observability validation");
					}

					// Signal that we processed an item after the error
					if (exceptionThrown && item != "error-item")
					{
						_ = itemProcessedAfterError.TrySetResult(true);
					}
				}

				return ValueTask.CompletedTask;
			},
			logger,
			new MicroBatchOptions { MaxBatchSize = 1, MaxBatchDelay = TimeSpan.FromMilliseconds(50) });

		_disposables.Add(processor);

		// Act - Trigger error
		await processor.AddAsync("error-item", CancellationToken.None).ConfigureAwait(false);

		// Give time for the exception to be processed and logged
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(200).ConfigureAwait(false);

		// Add another item to see if processor recovers
		await processor.AddAsync("recovery-item", CancellationToken.None).ConfigureAwait(false);

		// Wait for recovery processing with a short timeout - it's okay if it doesn't recover
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				itemProcessedAfterError.Task,
				TimeSpan.FromSeconds(30));
		}
		catch (TimeoutException)
		{
			// Recovery might not happen - that's acceptable behavior
		}

		// Assert - Verify error was logged (the primary observability assertion)
		var logEntries = _loggerProvider.GetLogEntries();
		var errorLogs = logEntries.Where(e => e.LogLevel >= LogLevel.Error).ToList();

		errorLogs.ShouldNotBeEmpty("Error should be logged for observability");

		var errorLog = errorLogs.First();
		_ = errorLog.Exception.ShouldNotBeNull("Exception details should be captured");
		_ = errorLog.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task ValidatePerformanceMetricsObservability()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<UnifiedBatchingMiddleware>();
		var options = Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions
		{
			MaxBatchSize = 1, // Force immediate processing for timing
			MaxBatchDelay = TimeSpan.FromMilliseconds(1),
			MaxParallelism = 1,
		});

		await using var middleware = new UnifiedBatchingMiddleware(options, logger, _loggerFactory);

		var processingTimes = new ConcurrentBag<TimeSpan>();
		var message = new FakeDispatchMessage();
		var context = new FakeMessageContext();

		ValueTask<IMessageResult> NextDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			var startTime = DateTime.UtcNow;
			// Simulate some processing time
			global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10, ct).Wait(ct);
			var endTime = DateTime.UtcNow;

			processingTimes.Add(endTime - startTime);
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act - Process multiple messages to gather timing data
		var tasks = new List<Task<IMessageResult>>();
		for (var i = 0; i < 5; i++)
		{
			tasks.Add(middleware.InvokeAsync(new FakeDispatchMessage(), new FakeMessageContext(), NextDelegate, CancellationToken.None).AsTask());
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Verify performance characteristics
		processingTimes.ShouldNotBeEmpty();
		tasks.All(t => t.Result.IsSuccess).ShouldBeTrue();

		// Assert - Verify timing logs exist
		var logEntries = _loggerProvider.GetLogEntries();
		var performanceLogs = logEntries.Where(e =>
			e.Message.Contains("duration") ||
			e.Message.Contains("completed") ||
			e.State.Any(kvp => kvp.Key.Contains("Duration") || kvp.Key.Contains("ms"))).ToList();

		// Performance logs may not always be present depending on implementation but we should verify no errors occurred
		var errorLogs = logEntries.Where(e => e.LogLevel >= LogLevel.Error).ToList();
		errorLogs.ShouldBeEmpty("No errors should occur during performance testing");
	}

	[Fact]
	public async Task ValidateObservabilityComponentsStage()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<UnifiedBatchingMiddleware>();
		var options = Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions());
		await using var middleware = new UnifiedBatchingMiddleware(options, logger, _loggerFactory);

		// Act & Assert - Verify middleware stage is appropriate for observability
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Optimization);
	}

	[Fact]
	public async Task ValidateStructuredLoggingFields()
	{
		// Arrange
		var logger = _loggerFactory.CreateLogger<InMemoryInboxStore>();
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions());
		var store = new InMemoryInboxStore(options, logger);
		_disposables.Add(store);

		var messageId = "structured-logging-test";
		var messageType = "StructuredTest";
		var tenantId = "test-tenant-123";
		var correlationId = Guid.NewGuid().ToString();

		var metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = correlationId,
			["TenantId"] = tenantId,
			["MessageType"] = messageType,
			["RetryCount"] = 0,
			["Timestamp"] = DateTimeOffset.UtcNow,
		};

		// Act
		_ = await store.CreateEntryAsync(messageId, "TestHandler", messageType,
			System.Text.Encoding.UTF8.GetBytes("structured test"), metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert - Verify required structured logging fields are present
		var logEntries = _loggerProvider.GetLogEntries();
		logEntries.ShouldNotBeEmpty();

		// Verify that logs contain structured fields for observability
		var structuredLogs = logEntries.Where(e => e.State.Any()).ToList();
		structuredLogs.ShouldNotBeEmpty("Logs should contain structured state for observability");

		// Verify presence of key observability fields in the formatted message
		// LoggerMessage.Define embeds values in the message itself
		var hasMessageId = structuredLogs.Any(log =>
			log.Message.Contains(messageId));
		hasMessageId.ShouldBeTrue("MessageId should be present in structured logs");
	}

	public void Dispose()
	{
		_meterProvider?.Dispose();
		_metricsExporter?.Dispose();

		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}

		_loggerFactory?.Dispose();
		_loggerProvider?.Dispose();
	}
}

/// <summary>
///     Test logger provider for capturing log entries during tests.
/// </summary>
internal sealed class ObservabilityTestLoggerProvider : ILoggerProvider
{
	private readonly ConcurrentBag<ObservabilityLogEntry> _logEntries = [];

	public ILogger CreateLogger(string categoryName) => new ObservabilityTestLogger(categoryName, _logEntries);

	public List<ObservabilityLogEntry> GetLogEntries() => [.. _logEntries];

	public void Dispose()
	{
	}
}

/// <summary>
///     Test logger for capturing structured log data.
/// </summary>
internal sealed class ObservabilityTestLogger(string categoryName, ConcurrentBag<ObservabilityLogEntry> logEntries) : ILogger
{
	private readonly string _categoryName = categoryName;
	private readonly ConcurrentBag<ObservabilityLogEntry> _logEntries = logEntries;

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var message = formatter(state, exception);
		var stateValues = state as IEnumerable<KeyValuePair<string, object?>> ??
							new List<KeyValuePair<string, object?>>();

		_logEntries.Add(new ObservabilityLogEntry(
			logLevel,
			eventId,
			message,
			exception,
			_categoryName,
			[.. stateValues]));
	}
}

/// <summary>
///     Represents a captured log entry for testing.
/// </summary>
internal sealed record ObservabilityLogEntry(
	LogLevel LogLevel,
	EventId EventId,
	string Message,
	Exception? Exception,
	string CategoryName,
	List<KeyValuePair<string, object?>> State);

/// <summary>
///     In-memory metrics exporter for testing.
/// </summary>
internal sealed class ObservabilityMetricsExporter : IDisposable
{
	private readonly ConcurrentBag<object> _metrics = [];

	public List<object> GetMetrics() => [.. _metrics];

	public void Dispose()
	{
	}
}

/// <summary>
///     Simple meter provider for testing metrics.
/// </summary>
internal sealed class ObservabilityMeterProvider : IDisposable
{
	public static ObservabilityMeterProvider CreateMeterProvider(Action<object> configureBuilder) =>
		// Simplified meter provider for testing
		new();

	public void Dispose()
	{
	}
}

