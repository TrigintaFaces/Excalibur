// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Messaging.Abstractions.Inbox;
using Excalibur.Dispatch.Messaging.Abstractions.Outbox;
using Excalibur.Dispatch.Delivery.Inbox.Enhanced;
using Excalibur.Dispatch.Delivery.Outbox.Enhanced;
using Excalibur.Dispatch.Delivery.Scheduling;
using Excalibur.Dispatch.Delivery.Scheduling.Enhanced;
using Excalibur.Dispatch.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores;

/// <summary>
///     High-performance example demonstrating enhanced stores under heavy load.
///     Showcases R7.12 high-performance patterns and R8.21 telemetry integration.
/// </summary>
public class HighPerformanceExample
{
	/// <summary>
	///     Configure enhanced stores for maximum throughput scenarios.
	/// </summary>
	public static IServiceCollection ConfigureForHighThroughput(IServiceCollection services)
	{
		// Configure telemetry for minimal overhead
		services.AddDispatchTelemetry(options =>
		{
			options.ServiceName = "HighThroughputExample";
			options.EnableEnhancedStoreObservability = true;
			options.EnableHotPathMetrics = false; // Disable for maximum performance
			options.SamplingRatio = 0.001; // 0.1% sampling
			options.MetricBatchSize = 2000;
			options.ExportTimeout = TimeSpan.FromSeconds(5);
		});

		// Register high-performance store implementations
		services.AddScoped<IInboxStore, HighPerformanceInMemoryInboxStore>();
		services.AddScoped<IOutboxStore, HighPerformanceInMemoryOutboxStore>();
		services.AddScoped<IScheduleStore, HighPerformanceInMemoryScheduleStore>();

		// Configure enhanced stores for maximum throughput
		services.AddEnhancedInboxStore(options =>
		{
			options.EnableAdvancedDeduplication = false; // Disable for maximum speed
			options.EnableContentBasedDeduplication = false;
			options.EnableHotPathOptimization = true;
			options.DeduplicationCacheSize = 100000;
			options.MaxConcurrentOperations = 1000; // High concurrency
		});

		services.AddEnhancedOutboxStore(options =>
		{
			options.EnableBatchStaging = true;
			options.EnableExponentialBackoff = false; // Disable for consistent timing
			options.EnableStagingOptimization = true;
			options.StagingBatchSize = 1000; // Large batches
			options.MaxConcurrentStagingOperations = 500;
			options.MaxRetryAttempts = 1; // Minimal retries for speed
		});

		services.AddEnhancedScheduleStore(options =>
		{
			options.EnableDuplicateDetection = false; // Disable for speed
			options.EnableExecutionTimeIndexing = true;
			options.EnableBatchOperations = true;
			options.EnableHotPathOptimization = true;
			options.ScheduleCacheSize = 100000;
			options.ExecutionTimeIndexSize = 500000;
			options.BatchSize = 1000;
		});

		return services;
	}
}

/// <summary>
///     High-throughput message processor demonstrating enhanced inbox store performance.
/// </summary>
public class HighThroughputInboxProcessor : BackgroundService
{
	private readonly IInboxStore _inboxStore;
	private readonly ILogger<HighThroughputInboxProcessor> _logger;
	private readonly Counter<long> _messagesProcessed;
	private readonly Histogram<double> _processingLatency;
	private const int BatchSize = 1000;
	private const int MaxConcurrency = 100;

	public HighThroughputInboxProcessor(
		IInboxStore inboxStore,
		ILogger<HighThroughputInboxProcessor> logger,
		IDispatchTelemetryProvider telemetryProvider)
	{
		_inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var meter = telemetryProvider.GetMeter("HighThroughputExample");
		_messagesProcessed = meter.CreateCounter<long>("high_throughput.messages_processed");
		_processingLatency = meter.CreateHistogram<double>("high_throughput.processing_latency_ms");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting high-throughput inbox processor");

		var messageQueue = new Queue<(string Id, string Type, byte[] Payload, Dictionary<string, object> Metadata)>();

		// Generate test messages
		for (var i = 0; i < 100000; i++)
		{
			messageQueue.Enqueue((
				Id: $"msg-{i:D6}",
				Type: "TestMessage",
				Payload: System.Text.Encoding.UTF8.GetBytes($"Test message {i}"),
				Metadata: new Dictionary<string, object> { ["sequence"] = i, ["timestamp"] = DateTimeOffset.UtcNow }
			));
		}

		_logger.LogInformation("Generated {Count} test messages for processing", messageQueue.Count);

		var stopwatch = Stopwatch.StartNew();
		var processedCount = 0;

		// Process messages in batches with controlled concurrency
		while (messageQueue.Count > 0 && !stoppingToken.IsCancellationRequested)
		{
			var batch = new List<(string Id, string Type, byte[] Payload, Dictionary<string, object> Metadata)>();

			// Create batch
			for (var i = 0; i < BatchSize && messageQueue.Count > 0; i++)
			{
				batch.Add(messageQueue.Dequeue());
			}

			// Process batch with controlled concurrency
			var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
			var tasks = batch.Select(async message =>
			{
				await semaphore.WaitAsync(stoppingToken);
				try
				{
					using var timer = _processingLatency.StartTimer();
					await ProcessMessageAsync(message.Id, message.Type, message.Payload, message.Metadata, stoppingToken);
					_messagesProcessed.Add(1);
					Interlocked.Increment(ref processedCount);
				}
				finally
				{
					semaphore.Release();
				}
			});

			await Task.WhenAll(tasks);

			if (processedCount % 10000 == 0)
			{
				var elapsed = stopwatch.Elapsed;
				var throughput = processedCount / elapsed.TotalSeconds;
				_logger.LogInformation("Processed {Count} messages in {Elapsed:F2}s (throughput: {Throughput:F0} msg/s)",
					processedCount, elapsed.TotalSeconds, throughput);
			}
		}

		stopwatch.Stop();
		var finalThroughput = processedCount / stopwatch.Elapsed.TotalSeconds;
		_logger.LogInformation("Completed processing {Count} messages in {Elapsed:F2}s (final throughput: {Throughput:F0} msg/s)",
			processedCount, stopwatch.Elapsed.TotalSeconds, finalThroughput);
	}

	private async Task ProcessMessageAsync(
		string messageId,
		string messageType,
		byte[] payload,
		Dictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		try
		{
			// Fast duplicate check using enhanced store optimizations
			if (await _inboxStore.IsAlreadyProcessedAsync(messageId, cancellationToken))
			{
				return;
			}

			// Create entry (enhanced store provides optimized deduplication)
			await _inboxStore.CreateEntryAsync(messageId, messageType, payload, metadata, cancellationToken);

			// Simulate fast processing
			await Task.Delay(1, cancellationToken);

			// Mark as processed
			await _inboxStore.MarkProcessedAsync(messageId, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process message {MessageId}", messageId);
			throw;
		}
	}
}

/// <summary>
///     High-throughput outbox processor demonstrating batch staging optimization.
/// </summary>
public class HighThroughputOutboxProcessor : BackgroundService
{
	private readonly IOutboxStore _outboxStore;
	private readonly ILogger<HighThroughputOutboxProcessor> _logger;
	private readonly Counter<long> _messagesSent;
	private readonly Histogram<double> _batchProcessingTime;
	private const int BatchSize = 500;
	private const int StagingBatchSize = 1000;

	public HighThroughputOutboxProcessor(
		IOutboxStore outboxStore,
		ILogger<HighThroughputOutboxProcessor> logger,
		IDispatchTelemetryProvider telemetryProvider)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var meter = telemetryProvider.GetMeter("HighThroughputExample");
		_messagesSent = meter.CreateCounter<long>("high_throughput.messages_sent");
		_batchProcessingTime = meter.CreateHistogram<double>("high_throughput.batch_processing_time_ms");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting high-throughput outbox processor");

		// Stage messages in large batches for better performance
		await StageMessagesInBulk(stoppingToken);

		// Process staged messages continuously
		while (!stoppingToken.IsCancellationRequested)
		{
			using var timer = _batchProcessingTime.StartTimer();

			try
			{
				// Get pending messages in batches (enhanced store optimizes this)
				var pendingMessages = await _outboxStore.GetPendingMessagesAsync(BatchSize, stoppingToken);
				var messagesList = pendingMessages.ToList();

				if (!messagesList.Any())
				{
					await Task.Delay(100, stoppingToken); // Brief pause when no messages
					continue;
				}

				// Process batch with parallel execution
				var tasks = messagesList.Select(async message =>
				{
					try
					{
						// Simulate fast message sending
						await Task.Delay(5, stoppingToken);

						// Mark as sent (enhanced store tracks this efficiently)
						await _outboxStore.MarkSentAsync(message.Id, stoppingToken);
						_messagesSent.Add(1);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to send message {MessageId}", message.Id);
						await _outboxStore.MarkFailedAsync(message.Id, ex.Message, 1, null, stoppingToken);
					}
				});

				await Task.WhenAll(tasks);

				_logger.LogDebug("Processed batch of {Count} outbound messages", messagesList.Count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in outbox processing loop");
				await Task.Delay(1000, stoppingToken); // Pause on error
			}
		}
	}

	private async Task StageMessagesInBulk(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Staging messages in bulk for high-throughput testing");

		var stagingTasks = new List<Task>();
		var totalMessages = 50000;
		var batchCount = (totalMessages + StagingBatchSize - 1) / StagingBatchSize;

		for (var batch = 0; batch < batchCount; batch++)
		{
			var batchIndex = batch;
			stagingTasks.Add(Task.Run(async () =>
			{
				var startIndex = batchIndex * StagingBatchSize;
				var endIndex = Math.Min(startIndex + StagingBatchSize, totalMessages);

				for (var i = startIndex; i < endIndex; i++)
				{
					var payload = System.Text.Encoding.UTF8.GetBytes($"Outbound message {i}");
					var metadata = new Dictionary<string, object>
					{
						["sequence"] = i,
						["batch"] = batchIndex,
						["timestamp"] = DateTimeOffset.UtcNow
					};

					await _outboxStore.StageMessageAsync("TestOutbound", payload, metadata,
						MessagePriority.Normal, cancellationToken);
				}

				_logger.LogDebug("Staged batch {BatchIndex} ({StartIndex}-{EndIndex})", batchIndex, startIndex, endIndex - 1);
			}, cancellationToken));
		}

		await Task.WhenAll(stagingTasks);
		_logger.LogInformation("Completed staging {Count} messages in {BatchCount} batches", totalMessages, batchCount);
	}
}

/// <summary>
///     High-throughput scheduler demonstrating bulk operations and execution time indexing.
/// </summary>
public class HighThroughputScheduler : BackgroundService
{
	private readonly IScheduleStore _scheduleStore;
	private readonly ILogger<HighThroughputScheduler> _logger;
	private readonly Counter<long> _schedulesExecuted;
	private readonly Histogram<double> _schedulingLatency;

	public HighThroughputScheduler(
		IScheduleStore scheduleStore,
		ILogger<HighThroughputScheduler> logger,
		IDispatchTelemetryProvider telemetryProvider)
	{
		_scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var meter = telemetryProvider.GetMeter("HighThroughputExample");
		_schedulesExecuted = meter.CreateCounter<long>("high_throughput.schedules_executed");
		_schedulingLatency = meter.CreateHistogram<double>("high_throughput.scheduling_latency_ms");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting high-throughput scheduler");

		// Create bulk schedules for testing
		await CreateBulkSchedules(stoppingToken);

		// Process schedules continuously with optimized execution time queries
		while (!stoppingToken.IsCancellationRequested)
		{
			using var timer = _schedulingLatency.StartTimer();

			try
			{
				var now = DateTimeOffset.UtcNow;
				var windowEnd = now.AddMinutes(1);

				// Use enhanced store's execution time indexing for fast queries
				var readySchedules = await _scheduleStore.GetSchedulesReadyForExecutionAsync(now, windowEnd, stoppingToken);
				var schedulesList = readySchedules.ToList();

				if (!schedulesList.Any())
				{
					await Task.Delay(1000, stoppingToken);
					continue;
				}

				// Process schedules in parallel
				var tasks = schedulesList.Select(async schedule =>
				{
					try
					{
						// Simulate schedule execution
						await Task.Delay(10, stoppingToken);

						// Complete schedule
						await _scheduleStore.CompleteAsync(schedule.Id, stoppingToken);
						_schedulesExecuted.Add(1);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Failed to execute schedule {ScheduleId}", schedule.Id);
					}
				});

				await Task.WhenAll(tasks);

				if (schedulesList.Count > 0)
				{
					_logger.LogDebug("Executed {Count} scheduled messages", schedulesList.Count);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in scheduler processing loop");
				await Task.Delay(5000, stoppingToken);
			}
		}
	}

	private async Task CreateBulkSchedules(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Creating bulk schedules for high-throughput testing");

		var schedules = new List<ScheduledMessage>();
		var baseTime = DateTimeOffset.UtcNow.AddMinutes(1);

		// Create schedules spread over the next hour
		for (var i = 0; i < 10000; i++)
		{
			var executionTime = baseTime.AddSeconds(i % 3600); // Spread over 1 hour
			var schedule = new ScheduledMessage
			{
				Id = Guid.NewGuid(),
				MessageName = "HighThroughputScheduled",
				MessageBody = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"Scheduled message {i}")),
				NextExecutionUtc = executionTime,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				Status = ScheduledMessageStatus.Pending,
				RetryCount = 0,
				TenantId = "high-throughput-test",
				CorrelationId = Guid.NewGuid().ToString()
			};

			schedules.Add(schedule);
		}

		// Use enhanced store's bulk operations for efficient storage
		var storedCount = await _scheduleStore.BulkStoreAsync(schedules, cancellationToken);
		_logger.LogInformation("Bulk stored {StoredCount} of {TotalCount} schedules", storedCount, schedules.Count);
	}
}

/// <summary>
///     Performance monitoring service that tracks enhanced store metrics.
/// </summary>
public class PerformanceMonitoringService : BackgroundService
{
	private readonly ILogger<PerformanceMonitoringService> _logger;
	private readonly Counter<long> _performanceReports;

	public PerformanceMonitoringService(
		ILogger<PerformanceMonitoringService> logger,
		IDispatchTelemetryProvider telemetryProvider)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var meter = telemetryProvider.GetMeter("HighThroughputExample");
		_performanceReports = meter.CreateCounter<long>("performance.reports_generated");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Starting performance monitoring service");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Generate performance report every 30 seconds
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

				var report = await GeneratePerformanceReport();
				_logger.LogInformation("Performance Report: {Report}", report);
				_performanceReports.Add(1);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error generating performance report");
			}
		}
	}

	private async Task<string> GeneratePerformanceReport()
	{
		// Simulate collecting performance metrics
		await Task.Delay(100);

		var report = new
		{
			Timestamp = DateTimeOffset.UtcNow,
			MemoryUsage = GC.GetTotalMemory(false),
			ThreadCount = Environment.ProcessorCount,
			GCCollections = new
			{
				Gen0 = GC.CollectionCount(0),
				Gen1 = GC.CollectionCount(1),
				Gen2 = GC.CollectionCount(2)
			}
		};

		return System.Text.Json.JsonSerializer.Serialize(report);
	}
}

/// <summary>
///     High-performance in-memory store implementations optimized for throughput.
/// </summary>
public class HighPerformanceInMemoryInboxStore : IInboxStore
{
	private readonly ConcurrentDictionary<string, InboxEntry> _entries = new();
	private readonly ConcurrentHashSet<string> _processedIds = new();

	public Task<InboxEntry> CreateEntryAsync(string messageId, string messageType, byte[] payload,
		Dictionary<string, object> metadata, CancellationToken cancellationToken = default)
	{
		var entry = new InboxEntry(messageId, messageType, payload, metadata, DateTimeOffset.UtcNow);
		_entries.TryAdd(messageId, entry);
		return Task.FromResult(entry);
	}

	public Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default)
	{
		_processedIds.Add(messageId);
		_entries.TryRemove(messageId, out _); // Remove to save memory
		return Task.CompletedTask;
	}

	public Task<bool> IsAlreadyProcessedAsync(string messageId, CancellationToken cancellationToken = default) =>
		Task.FromResult(_processedIds.Contains(messageId));

	public Task<InboxEntry?> GetEntryAsync(string messageId, CancellationToken cancellationToken = default) =>
		Task.FromResult(_entries.TryGetValue(messageId, out var entry) ? entry : null);

	public Task MarkFailedAsync(string messageId, string errorMessage, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task<IEnumerable<InboxEntry>> GetFailedEntriesAsync(int maxRetries = 3, DateTimeOffset? olderThan = null,
		int batchSize = 100, CancellationToken cancellationToken = default) =>
		Task.FromResult(Enumerable.Empty<InboxEntry>());

	public Task<int> CleanupProcessedEntriesAsync(DateTimeOffset olderThan, int batchSize = 1000,
		CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public class HighPerformanceInMemoryOutboxStore : IOutboxStore
{
	private readonly ConcurrentQueue<OutboxEntry> _pendingMessages = new();
	private volatile int _pendingCount;

	public Task<string> StageMessageAsync(string messageType, byte[] payload, Dictionary<string, object> metadata,
		MessagePriority priority = MessagePriority.Normal, CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid().ToString();
		var entry = new OutboxEntry(id, messageType, payload, metadata, priority, DateTimeOffset.UtcNow);
		_pendingMessages.Enqueue(entry);
		Interlocked.Increment(ref _pendingCount);
		return Task.FromResult(id);
	}

	public Task<IEnumerable<OutboxEntry>> GetPendingMessagesAsync(int batchSize = 100,
		CancellationToken cancellationToken = default)
	{
		var batch = new List<OutboxEntry>();
		var count = Math.Min(batchSize, _pendingCount);

		for (var i = 0; i < count && _pendingMessages.TryDequeue(out var entry); i++)
		{
			batch.Add(entry);
			Interlocked.Decrement(ref _pendingCount);
		}

		return Task.FromResult<IEnumerable<OutboxEntry>>(batch);
	}

	public Task MarkSentAsync(string messageId, CancellationToken cancellationToken = default) =>
		Task.CompletedTask; // Already removed from queue

	public Task MarkFailedAsync(string messageId, string errorMessage, int retryCount,
		DateTimeOffset? nextRetryAtUtc = null, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize = 1000,
		CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public class HighPerformanceInMemoryScheduleStore : IScheduleStore
{
	private readonly ConcurrentDictionary<Guid, IScheduledMessage> _schedules = new();
	private readonly SortedDictionary<DateTimeOffset, List<IScheduledMessage>> _executionTimeIndex = new();
	private readonly object _indexLock = new();

	public Task StoreAsync(IScheduledMessage scheduledMessage, CancellationToken cancellationToken = default)
	{
		_schedules.TryAdd(scheduledMessage.Id, scheduledMessage);

		if (scheduledMessage.NextExecutionUtc.HasValue)
		{
			lock (_indexLock)
			{
				var executionTime = scheduledMessage.NextExecutionUtc.Value;
				if (!_executionTimeIndex.TryGetValue(executionTime, out var list))
				{
					list = new List<IScheduledMessage>();
					_executionTimeIndex[executionTime] = list;
				}
				list.Add(scheduledMessage);
			}
		}

		return Task.CompletedTask;
	}

	public async Task<int> BulkStoreAsync(IEnumerable<IScheduledMessage> scheduledMessages,
		CancellationToken cancellationToken = default)
	{
		var messagesList = scheduledMessages.ToList();
		var tasks = messagesList.Select(message => StoreAsync(message, cancellationToken));
		await Task.WhenAll(tasks);
		return messagesList.Count;
	}

	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult<IEnumerable<IScheduledMessage>>(_schedules.Values);

	public Task<IEnumerable<IScheduledMessage>> GetSchedulesReadyForExecutionAsync(DateTimeOffset windowStart,
		DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
	{
		var result = new List<IScheduledMessage>();

		lock (_indexLock)
		{
			foreach (var kvp in _executionTimeIndex)
			{
				if (kvp.Key >= windowStart && kvp.Key <= windowEnd)
				{
					result.AddRange(kvp.Value);
				}
				else if (kvp.Key > windowEnd)
				{
					break; // Sorted dictionary, no need to continue
				}
			}
		}

		return Task.FromResult<IEnumerable<IScheduledMessage>>(result);
	}

	public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken = default)
	{
		if (_schedules.TryRemove(scheduleId, out var schedule) && schedule.NextExecutionUtc.HasValue)
		{
			lock (_indexLock)
			{
				if (_executionTimeIndex.TryGetValue(schedule.NextExecutionUtc.Value, out var list))
				{
					list.Remove(schedule);
					if (!list.Any())
					{
						_executionTimeIndex.Remove(schedule.NextExecutionUtc.Value);
					}
				}
			}
		}

		return Task.CompletedTask;
	}
}

/// <summary>
///     Thread-safe HashSet implementation for high-performance scenarios.
/// </summary>
public class ConcurrentHashSet<T> where T : notnull
{
	private readonly HashSet<T> _hashSet = new();
	private readonly ReaderWriterLockSlim _lock = new();

	public bool Add(T item)
	{
		_lock.EnterWriteLock();
		try
		{
			return _hashSet.Add(item);
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}

	public bool Contains(T item)
	{
		_lock.EnterReadLock();
		try
		{
			return _hashSet.Contains(item);
		}
		finally
		{
			_lock.ExitReadLock();
		}
	}

	public bool Remove(T item)
	{
		_lock.EnterWriteLock();
		try
		{
			return _hashSet.Remove(item);
		}
		finally
		{
			_lock.ExitWriteLock();
		}
	}
}