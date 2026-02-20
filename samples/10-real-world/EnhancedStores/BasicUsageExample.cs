// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)

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
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Examples.EnhancedStores;

/// <summary>
///     Basic usage example demonstrating enhanced store registration and configuration.
///     Implements R9.51 enhanced message deduplication and R7.12 high-performance patterns.
/// </summary>
public class BasicUsageExample
{
	/// <summary>
	///     Example demonstrating basic enhanced store registration with default settings.
	/// </summary>
	public static IServiceCollection RegisterEnhancedStoresBasic(IServiceCollection services)
	{
		// Register telemetry provider first (required for enhanced stores)
		services.AddDispatchTelemetry(options =>
		{
			options.ServiceName = "EnhancedStoreExample";
			options.EnableEnhancedStoreObservability = true;
			options.EnableMetrics = true;
			options.EnableTracing = true;
		});

		// Register underlying store implementations
		services.AddScoped<IInboxStore, InMemoryInboxStore>();
		services.AddScoped<IOutboxStore, InMemoryOutboxStore>();
		services.AddScoped<IScheduleStore, InMemoryScheduleStore>();

		// Register enhanced stores with default options
		services.AddEnhancedInboxStore();
		services.AddEnhancedOutboxStore();
		services.AddEnhancedScheduleStore();

		return services;
	}

	/// <summary>
	///     Example demonstrating enhanced store registration with custom configuration.
	/// </summary>
	public static IServiceCollection RegisterEnhancedStoresWithCustomOptions(IServiceCollection services)
	{
		// Register telemetry provider
		services.AddDispatchTelemetry(options =>
		{
			options.ServiceName = "CustomEnhancedStoreExample";
			options.EnableEnhancedStoreObservability = true;
			options.SamplingRatio = 0.1; // 10% sampling for production
		});

		// Register underlying stores
		services.AddScoped<IInboxStore, InMemoryInboxStore>();
		services.AddScoped<IOutboxStore, InMemoryOutboxStore>();
		services.AddScoped<IScheduleStore, InMemoryScheduleStore>();

		// Configure enhanced inbox store with custom options
		services.AddEnhancedInboxStore(options =>
		{
			options.EnableAdvancedDeduplication = true;
			options.EnableContentBasedDeduplication = true;
			options.DeduplicationCacheSize = 50000;
			options.ContentDeduplicationWindow = TimeSpan.FromMinutes(15);
			options.MaxConcurrentOperations = 200;
		});

		// Configure enhanced outbox store with custom retry settings
		services.AddEnhancedOutboxStore(options =>
		{
			options.EnableBatchStaging = true;
			options.EnableExponentialBackoff = true;
			options.StagingBatchSize = 200;
			options.MaxRetryAttempts = 5;
			options.BaseRetryDelay = TimeSpan.FromSeconds(2);
			options.MaxRetryDelay = TimeSpan.FromMinutes(10);
		});

		// Configure enhanced schedule store with bulk operations
		services.AddEnhancedScheduleStore(options =>
		{
			options.EnableDuplicateDetection = true;
			options.EnableExecutionTimeIndexing = true;
			options.EnableBatchOperations = true;
			options.ScheduleCacheSize = 25000;
			options.ExecutionTimeIndexSize = 100000;
			options.BatchSize = 500;
		});

		return services;
	}

	/// <summary>
	///     Example demonstrating enhanced store registration using performance profiles.
	/// </summary>
	public static IServiceCollection RegisterEnhancedStoresWithProfiles(IServiceCollection services, string environment)
	{
		// Register telemetry provider with environment-specific settings
		services.AddDispatchTelemetry(environment switch
		{
			"Development" => DispatchTelemetryOptions.CreateDevelopmentProfile(),
			"Production" => DispatchTelemetryOptions.CreateProductionProfile(),
			_ => DispatchTelemetryOptions.CreateThroughputProfile()
		});

		// Register underlying stores
		services.AddScoped<IInboxStore, InMemoryInboxStore>();
		services.AddScoped<IOutboxStore, InMemoryOutboxStore>();
		services.AddScoped<IScheduleStore, InMemoryScheduleStore>();

		// Register enhanced stores with environment-specific profiles
		services.AddEnhancedInboxStore(environment switch
		{
			"Development" => EnhancedInboxOptions.CreateDevelopmentProfile(),
			"Production" => EnhancedInboxOptions.CreateProductionProfile(),
			"Throughput" => EnhancedInboxOptions.CreateThroughputProfile(),
			_ => EnhancedInboxOptions.CreateReliabilityProfile()
		});

		services.AddEnhancedOutboxStore(environment switch
		{
			"Development" => EnhancedOutboxOptions.CreateDevelopmentProfile(),
			"Production" => EnhancedOutboxOptions.CreateProductionProfile(),
			"Throughput" => EnhancedOutboxOptions.CreateThroughputProfile(),
			_ => EnhancedOutboxOptions.CreateReliabilityProfile()
		});

		services.AddEnhancedScheduleStore(environment switch
		{
			"Development" => EnhancedScheduleOptions.CreateDevelopmentProfile(),
			"Production" => EnhancedScheduleOptions.CreateProductionProfile(),
			"Throughput" => EnhancedScheduleOptions.CreateThroughputProfile(),
			_ => EnhancedScheduleOptions.CreateReliabilityProfile()
		});

		return services;
	}
}

/// <summary>
///     Example service demonstrating enhanced inbox store usage.
/// </summary>
public class EnhancedInboxService
{
	private readonly IInboxStore _inboxStore;
	private readonly ILogger<EnhancedInboxService> _logger;

	public EnhancedInboxService(IInboxStore inboxStore, ILogger<EnhancedInboxService> logger)
	{
		_inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	///     Process incoming message with enhanced deduplication.
	/// </summary>
	public async Task<bool> ProcessIncomingMessageAsync(
		string messageId,
		string messageType,
		byte[] payload,
		Dictionary<string, object> metadata,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Check if already processed (enhanced store provides optimized lookup)
			if (await _inboxStore.IsAlreadyProcessedAsync(messageId, cancellationToken))
			{
				_logger.LogInformation("Message {MessageId} already processed, skipping", messageId);
				return false;
			}

			// Create inbox entry (enhanced store provides deduplication)
			var entry = await _inboxStore.CreateEntryAsync(messageId, messageType, payload, metadata, cancellationToken);
			_logger.LogInformation("Created inbox entry for message {MessageId} of type {MessageType}", messageId, messageType);

			// Simulate message processing
			await ProcessMessageLogic(entry, cancellationToken);

			// Mark as processed
			await _inboxStore.MarkProcessedAsync(messageId, cancellationToken);
			_logger.LogInformation("Successfully processed message {MessageId}", messageId);

			return true;
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate message"))
		{
			_logger.LogWarning("Duplicate message detected: {MessageId}", messageId);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process message {MessageId}", messageId);
			await _inboxStore.MarkFailedAsync(messageId, ex.Message, cancellationToken);
			throw;
		}
	}

	private async Task ProcessMessageLogic(InboxEntry entry, CancellationToken cancellationToken)
	{
		// Simulate actual message processing
		await Task.Delay(100, cancellationToken);
		_logger.LogDebug("Processed message logic for {MessageId}", entry.MessageId);
	}
}

/// <summary>
///     Example service demonstrating enhanced outbox store usage.
/// </summary>
public class EnhancedOutboxService
{
	private readonly IOutboxStore _outboxStore;
	private readonly ILogger<EnhancedOutboxService> _logger;

	public EnhancedOutboxService(IOutboxStore outboxStore, ILogger<EnhancedOutboxService> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	///     Stage outbound message with enhanced batching capabilities.
	/// </summary>
	public async Task<string> StageOutboundMessageAsync(
		string messageType,
		byte[] payload,
		Dictionary<string, object> metadata,
		MessagePriority priority = MessagePriority.Normal,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Stage message (enhanced store provides batching optimization)
			var messageId = await _outboxStore.StageMessageAsync(messageType, payload, metadata, priority, cancellationToken);
			_logger.LogInformation("Staged outbound message {MessageId} of type {MessageType} with priority {Priority}",
				messageId, messageType, priority);

			return messageId;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to stage outbound message of type {MessageType}", messageType);
			throw;
		}
	}

	/// <summary>
	///     Process outbound messages in batches for better performance.
	/// </summary>
	public async Task ProcessOutboundMessageBatchAsync(int batchSize = 100, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get pending messages (enhanced store provides optimized batching)
			var pendingMessages = await _outboxStore.GetPendingMessagesAsync(batchSize, cancellationToken);

			if (!pendingMessages.Any())
			{
				_logger.LogDebug("No pending outbound messages to process");
				return;
			}

			_logger.LogInformation("Processing batch of {Count} outbound messages", pendingMessages.Count());

			foreach (var message in pendingMessages)
			{
				try
				{
					// Simulate sending message
					await SendMessageLogic(message, cancellationToken);

					// Mark as sent (enhanced store tracks metrics)
					await _outboxStore.MarkSentAsync(message.Id, cancellationToken);
					_logger.LogDebug("Successfully sent message {MessageId}", message.Id);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to send message {MessageId}, will retry", message.Id);
					// Enhanced store will handle retry logic automatically
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process outbound message batch");
			throw;
		}
	}

	private async Task SendMessageLogic(OutboxEntry message, CancellationToken cancellationToken)
	{
		// Simulate actual message sending
		await Task.Delay(50, cancellationToken);
		_logger.LogDebug("Sent message {MessageId} of type {MessageType}", message.Id, message.MessageType);
	}
}

/// <summary>
///     Example service demonstrating enhanced schedule store usage.
/// </summary>
public class EnhancedSchedulingService
{
	private readonly IScheduleStore _scheduleStore;
	private readonly ILogger<EnhancedSchedulingService> _logger;

	public EnhancedSchedulingService(IScheduleStore scheduleStore, ILogger<EnhancedSchedulingService> logger)
	{
		_scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	///     Schedule message with enhanced duplicate detection.
	/// </summary>
	public async Task<Guid> ScheduleMessageAsync(
		string messageName,
		byte[] messageBody,
		DateTimeOffset executionTime,
		string? cronExpression = null,
		TimeSpan? interval = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var schedule = new ScheduledMessage
			{
				Id = Guid.NewGuid(),
				MessageName = messageName,
				MessageBody = Convert.ToBase64String(messageBody),
				NextExecutionUtc = executionTime,
				CronExpression = cronExpression,
				Interval = interval,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				Status = ScheduledMessageStatus.Pending,
				RetryCount = 0,
				TenantId = "example-tenant",
				CorrelationId = Guid.NewGuid().ToString(),
				TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString(),
				SpanId = System.Diagnostics.Activity.Current?.SpanId.ToString()
			};

			// Store schedule (enhanced store provides duplicate detection)
			await _scheduleStore.StoreAsync(schedule, cancellationToken);
			_logger.LogInformation("Scheduled message {MessageName} for execution at {ExecutionTime} with ID {ScheduleId}",
				messageName, executionTime, schedule.Id);

			return schedule.Id;
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate schedule"))
		{
			_logger.LogWarning("Duplicate schedule detected for message {MessageName} at {ExecutionTime}",
				messageName, executionTime);
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to schedule message {MessageName}", messageName);
			throw;
		}
	}

	/// <summary>
	///     Process scheduled messages using enhanced execution time indexing.
	/// </summary>
	public async Task ProcessScheduledMessagesAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var now = DateTimeOffset.UtcNow;
			var windowEnd = now.AddMinutes(1); // Process next minute

			// Get schedules ready for execution (enhanced store provides optimized indexing)
			var readySchedules = await _scheduleStore.GetSchedulesReadyForExecutionAsync(now, windowEnd, cancellationToken);

			if (!readySchedules.Any())
			{
				_logger.LogDebug("No schedules ready for execution");
				return;
			}

			_logger.LogInformation("Processing {Count} scheduled messages", readySchedules.Count());

			foreach (var schedule in readySchedules)
			{
				try
				{
					// Execute scheduled message
					await ExecuteScheduledMessage(schedule, cancellationToken);

					// Mark as completed
					await _scheduleStore.CompleteAsync(schedule.Id, cancellationToken);
					_logger.LogDebug("Completed schedule {ScheduleId} for message {MessageName}",
						schedule.Id, schedule.MessageName);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to execute schedule {ScheduleId}", schedule.Id);
					// Enhanced store will handle retry logic based on configuration
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process scheduled messages");
			throw;
		}
	}

	/// <summary>
	///     Bulk schedule multiple messages for high-throughput scenarios.
	/// </summary>
	public async Task<int> BulkScheduleMessagesAsync(
		IEnumerable<(string MessageName, byte[] MessageBody, DateTimeOffset ExecutionTime)> messages,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var schedules = messages.Select(m => new ScheduledMessage
			{
				Id = Guid.NewGuid(),
				MessageName = m.MessageName,
				MessageBody = Convert.ToBase64String(m.MessageBody),
				NextExecutionUtc = m.ExecutionTime,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				Status = ScheduledMessageStatus.Pending,
				RetryCount = 0,
				TenantId = "example-tenant",
				CorrelationId = Guid.NewGuid().ToString()
			}).ToList();

			// Use enhanced store's bulk operations for better performance
			var storedCount = await _scheduleStore.BulkStoreAsync(schedules, cancellationToken);
			_logger.LogInformation("Bulk scheduled {StoredCount} of {TotalCount} messages", storedCount, schedules.Count);

			return storedCount;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to bulk schedule messages");
			throw;
		}
	}

	private async Task ExecuteScheduledMessage(IScheduledMessage schedule, CancellationToken cancellationToken)
	{
		// Simulate actual scheduled message execution
		await Task.Delay(100, cancellationToken);
		_logger.LogDebug("Executed scheduled message {MessageName} for schedule {ScheduleId}",
			schedule.MessageName, schedule.Id);
	}
}

/// <summary>
///     In-memory implementations for demonstration purposes.
/// </summary>
public class InMemoryInboxStore : IInboxStore
{
	private readonly Dictionary<string, InboxEntry> _entries = new();
	private readonly HashSet<string> _processedIds = new();

	public Task<InboxEntry> CreateEntryAsync(string messageId, string messageType, byte[] payload,
		Dictionary<string, object> metadata, CancellationToken cancellationToken = default)
	{
		var entry = new InboxEntry(messageId, messageType, payload, metadata, DateTimeOffset.UtcNow);
		_entries[messageId] = entry;
		return Task.FromResult(entry);
	}

	public Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default)
	{
		_processedIds.Add(messageId);
		return Task.CompletedTask;
	}

	public Task<bool> IsAlreadyProcessedAsync(string messageId, CancellationToken cancellationToken = default) =>
		Task.FromResult(_processedIds.Contains(messageId));

	public Task<InboxEntry?> GetEntryAsync(string messageId, CancellationToken cancellationToken = default) =>
		Task.FromResult(_entries.TryGetValue(messageId, out var entry) ? entry : null);

	public Task MarkFailedAsync(string messageId, string errorMessage, CancellationToken cancellationToken = default)
	{
		// Implementation would update entry status
		return Task.CompletedTask;
	}

	public Task<IEnumerable<InboxEntry>> GetFailedEntriesAsync(int maxRetries = 3, DateTimeOffset? olderThan = null,
		int batchSize = 100, CancellationToken cancellationToken = default) =>
		Task.FromResult(Enumerable.Empty<InboxEntry>());

	public Task<int> CleanupProcessedEntriesAsync(DateTimeOffset olderThan, int batchSize = 1000,
		CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public class InMemoryOutboxStore : IOutboxStore
{
	private readonly Dictionary<string, OutboxEntry> _entries = new();

	public Task<string> StageMessageAsync(string messageType, byte[] payload, Dictionary<string, object> metadata,
		MessagePriority priority = MessagePriority.Normal, CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid().ToString();
		var entry = new OutboxEntry(id, messageType, payload, metadata, priority, DateTimeOffset.UtcNow);
		_entries[id] = entry;
		return Task.FromResult(id);
	}

	public Task<IEnumerable<OutboxEntry>> GetPendingMessagesAsync(int batchSize = 100,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(_entries.Values.Take(batchSize));

	public Task MarkSentAsync(string messageId, CancellationToken cancellationToken = default)
	{
		_entries.Remove(messageId);
		return Task.CompletedTask;
	}

	public Task MarkFailedAsync(string messageId, string errorMessage, int retryCount,
		DateTimeOffset? nextRetryAtUtc = null, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;

	public Task<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize = 1000,
		CancellationToken cancellationToken = default) => Task.FromResult(0);
}

public class InMemoryScheduleStore : IScheduleStore
{
	private readonly Dictionary<Guid, IScheduledMessage> _schedules = new();

	public Task StoreAsync(IScheduledMessage scheduledMessage, CancellationToken cancellationToken = default)
	{
		_schedules[scheduledMessage.Id] = scheduledMessage;
		return Task.CompletedTask;
	}

	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult<IEnumerable<IScheduledMessage>>(_schedules.Values);

	public Task<IEnumerable<IScheduledMessage>> GetSchedulesReadyForExecutionAsync(DateTimeOffset windowStart,
		DateTimeOffset windowEnd, CancellationToken cancellationToken = default) =>
		Task.FromResult(_schedules.Values.Where(s =>
			s.NextExecutionUtc.HasValue &&
			s.NextExecutionUtc >= windowStart &&
			s.NextExecutionUtc <= windowEnd));

	public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken = default)
	{
		_schedules.Remove(scheduleId);
		return Task.CompletedTask;
	}
}