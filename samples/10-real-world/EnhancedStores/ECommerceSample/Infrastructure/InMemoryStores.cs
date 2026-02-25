// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample;

/// <summary>
/// In-memory implementation of IInboxStore for sample application. Provides basic inbox functionality for demonstration purposes.
/// </summary>
public sealed partial class InMemoryInboxStore(ILogger<InMemoryInboxStore> logger) : IInboxStore
{
	private readonly ConcurrentDictionary<string, InboxEntry> _entries = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryInboxStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var key = GetKey(messageId, handlerType);

		if (!_entries.TryAdd(key, entry))
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
		}

		LogCreatedInboxEntry(messageId, messageType);
		return new ValueTask<InboxEntry>(entry);
	}

	public ValueTask MarkProcessedAsync(
		string messageId,
		string handlerType,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var key = GetKey(messageId, handlerType);
		if (!_entries.TryGetValue(key, out var entry))
		{
			LogNotFoundForProcessing(messageId);
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		if (entry.Status == InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Message '{messageId}' for handler '{handlerType}' is already marked as processed.");
		}

		entry.MarkProcessed();
		LogMarkedAsProcessed(messageId);
		return ValueTask.CompletedTask;
	}

	public ValueTask<bool> TryMarkAsProcessedAsync(
		string messageId,
		string handlerType,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var key = GetKey(messageId, handlerType);
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = [],
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow
		};

		return new ValueTask<bool>(_entries.TryAdd(key, entry));
	}

	public ValueTask<bool> IsProcessedAsync(
		string messageId,
		string handlerType,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var key = GetKey(messageId, handlerType);
		var isProcessed = _entries.TryGetValue(key, out var entry) &&
						  entry.Status == InboxStatus.Processed;
		return new ValueTask<bool>(isProcessed);
	}

	public ValueTask<InboxEntry?> GetEntryAsync(
		string messageId,
		string handlerType,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var key = GetKey(messageId, handlerType);
		_ = _entries.TryGetValue(key, out var entry);
		return new ValueTask<InboxEntry?>(entry);
	}

	public ValueTask MarkFailedAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var key = GetKey(messageId, handlerType);
		if (_entries.TryGetValue(key, out var entry))
		{
			entry.MarkFailed(errorMessage);
			LogMarkedAsFailed(messageId, errorMessage);
		}
		else
		{
			LogNotFoundForFailureMarking(messageId);
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken = default)
	{
		var query = _entries.Values.Where(static e => e.Status == InboxStatus.Failed);

		if (maxRetries > 0)
		{
			query = query.Where(e => e.RetryCount < maxRetries);
		}

		if (olderThan.HasValue)
		{
			query = query.Where(e => e.LastAttemptAt < olderThan.Value);
		}

		var failed = query
			.OrderBy(static e => e.ReceivedAt)
			.Take(batchSize);

		return new ValueTask<IEnumerable<InboxEntry>>(failed);
	}

	public ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(
		CancellationToken cancellationToken = default)
	{
		var allEntries = _entries.Values
			.OrderBy(static e => e.ReceivedAt)
			.AsEnumerable();

		return new ValueTask<IEnumerable<InboxEntry>>(allEntries);
	}

	public ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
	{
		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		foreach (var entry in _entries.Values)
		{
			total++;
			switch (entry.Status)
			{
				case InboxStatus.Processed:
					processed++;
					break;

				case InboxStatus.Failed:
					failed++;
					break;

				case InboxStatus.Received:
				case InboxStatus.Processing:
					pending++;
					break;
			}
		}

		return new ValueTask<InboxStatistics>(new InboxStatistics
		{
			PendingEntries = pending,
			ProcessedEntries = processed,
			FailedEntries = failed,
			TotalEntries = total
		});
	}

	public ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
	{
		var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
		var removed = 0;

		foreach (var kvp in _entries.ToArray())
		{
			var entry = kvp.Value;
			if (entry is { Status: InboxStatus.Processed, ProcessedAt: not null } &&
				entry.ProcessedAt.Value < cutoff &&
				_entries.TryRemove(kvp.Key, out _))
			{
				removed++;
			}
		}

		LogCleanedUpProcessedEntries(removed);
		return new ValueTask<int>(removed);
	}

	private static string GetKey(string messageId, string handlerType) => $"{messageId}:{handlerType}";

	[LoggerMessage(1001, LogLevel.Debug, "📥 Created inbox entry for message {MessageId} ({MessageType})")]
	private partial void LogCreatedInboxEntry(string messageId, string messageType);

	[LoggerMessage(1002, LogLevel.Debug, "✅ Marked inbox entry {MessageId} as processed")]
	private partial void LogMarkedAsProcessed(string messageId);

	[LoggerMessage(1003, LogLevel.Warning, "⚠️ Inbox entry {MessageId} not found for processing")]
	private partial void LogNotFoundForProcessing(string messageId);

	[LoggerMessage(1004, LogLevel.Debug, "❌ Marked inbox entry {MessageId} as failed: {ErrorMessage}")]
	private partial void LogMarkedAsFailed(string messageId, string errorMessage);

	[LoggerMessage(1005, LogLevel.Warning, "⚠️ Inbox entry {MessageId} not found for failure marking")]
	private partial void LogNotFoundForFailureMarking(string messageId);

	[LoggerMessage(1006, LogLevel.Debug, "🧹 Cleaned up {Count} processed inbox entries")]
	private partial void LogCleanedUpProcessedEntries(int count);
}

/// <summary>
/// In-memory implementation of IOutboxStore for sample application. Provides basic outbox functionality for demonstration purposes.
/// </summary>
public sealed partial class InMemoryOutboxStore(ILogger<InMemoryOutboxStore> logger) : IOutboxStore
{
	private readonly ConcurrentDictionary<string, OutboundMessage> _messages = new();
	private readonly ILogger<InMemoryOutboxStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (!_messages.TryAdd(message.Id, message))
		{
			throw new InvalidOperationException($"Duplicate message ID: {message.Id}");
		}

		LogStagedOutboxMessage(message.Id, message.MessageType);
		return ValueTask.CompletedTask;
	}

	public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var outboundMessage = new OutboundMessage(
				context.MessageType,
				System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message)),
				"default", // Sample implementation uses default destination
				null) // Headers no longer available on IDispatchMessage; using null for sample implementation
		{
			CorrelationId = context.CorrelationId,
			TenantId = context.TenantId
		};

		return StageMessageAsync(outboundMessage, cancellationToken);
	}

	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize = 100,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		var unsent = _messages.Values
			.Where(static m => m.Status == OutboxStatus.Staged)
			.OrderBy(static m => m.CreatedAt)
			.Take(batchSize);

		return new ValueTask<IEnumerable<OutboundMessage>>(unsent);
	}

	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(messageId);

		if (_messages.TryGetValue(messageId, out var message))
		{
			message.MarkSent();
			LogMarkedAsSent(messageId);
		}
		else
		{
			throw new InvalidOperationException($"Outbox message {messageId} not found or already sent");
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		if (_messages.TryGetValue(messageId, out var message))
		{
			message.MarkFailed(errorMessage);
			message.RetryCount = retryCount;
			LogMarkedAsFailedWithRetry(messageId, errorMessage, retryCount);
		}
		else
		{
			throw new InvalidOperationException($"Outbox message {messageId} not found");
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries = 3, DateTimeOffset? olderThan = null,
		int batchSize = 100, CancellationToken cancellationToken = default)
	{
		var query = _messages.Values.Where(m => m.Status == OutboxStatus.Failed && m.RetryCount < maxRetries);

		if (olderThan.HasValue)
		{
			query = query.Where(m => m.LastAttemptAt < olderThan.Value);
		}

		var failed = query
			.OrderBy(static m => m.CreatedAt)
			.Take(batchSize);

		return new ValueTask<IEnumerable<OutboundMessage>>(failed);
	}

	public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize = 100,
		CancellationToken cancellationToken = default)
	{
		var scheduled = _messages.Values
			.Where(m => m.Status == OutboxStatus.Staged && m.ScheduledAt.HasValue && m.ScheduledAt.Value <= scheduledBefore)
			.OrderBy(static m => m.ScheduledAt)
			.Take(batchSize);

		return new ValueTask<IEnumerable<OutboundMessage>>(scheduled);
	}

	public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize = 1000,
		CancellationToken cancellationToken = default)
	{
		var toRemove = _messages.Values
			.Where(m => m.Status == OutboxStatus.Sent && m.SentAt < olderThan)
			.Select(m => m.Id)
			.Take(batchSize)
			.ToList();

		foreach (var messageId in toRemove)
		{
			_ = _messages.TryRemove(messageId, out _);
		}

		LogCleanedUpSentMessages(toRemove.Count);
		return new ValueTask<int>(toRemove.Count);
	}

	public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
	{
		var messages = _messages.Values.ToList();
		var now = DateTimeOffset.UtcNow;

		var statistics = new OutboxStatistics
		{
			StagedMessageCount = messages.Count(m => m.Status == OutboxStatus.Staged),
			SendingMessageCount = messages.Count(m => m.Status == OutboxStatus.Sending),
			SentMessageCount = messages.Count(m => m.Status == OutboxStatus.Sent),
			FailedMessageCount = messages.Count(m => m.Status == OutboxStatus.Failed),
			ScheduledMessageCount = messages.Count(m => m.ScheduledAt.HasValue && m.ScheduledAt.Value > now),
			OldestUnsentMessageAge = messages
				.Where(m => m.Status == OutboxStatus.Staged)
				.OrderBy(m => m.CreatedAt)
				.FirstOrDefault()?.GetAge(),
			OldestFailedMessageAge = messages
				.Where(m => m.Status == OutboxStatus.Failed)
				.OrderBy(m => m.CreatedAt)
				.FirstOrDefault()?.GetAge()
		};

		return new ValueTask<OutboxStatistics>(statistics);
	}

	public ValueTask<bool> TryMarkSentAndReceivedAsync(
		string messageId,
		InboxEntry inboxEntry,
		CancellationToken cancellationToken = default) =>
		new(false);

	[LoggerMessage(1001, LogLevel.Debug, "📤 Staged outbox message {MessageId} ({MessageType})")]
	private partial void LogStagedOutboxMessage(string messageId, string messageType);

	[LoggerMessage(1002, LogLevel.Debug, "✅ Marked outbox message {MessageId} as sent")]
	private partial void LogMarkedAsSent(string messageId);

	[LoggerMessage(1003, LogLevel.Debug, "❌ Marked outbox message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMarkedAsFailedWithRetry(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(1004, LogLevel.Debug, "🧹 Cleaned up {Count} sent outbox messages")]
	private partial void LogCleanedUpSentMessages(int count);
}

/// <summary>
/// In-memory implementation of IScheduleStore for sample application. Provides basic schedule functionality for demonstration purposes.
/// </summary>
public sealed partial class InMemoryScheduleStore(ILogger<InMemoryScheduleStore> logger) : IScheduleStore
{
	private readonly ConcurrentDictionary<Guid, IScheduledMessage> _schedules = new();
	private readonly ILogger<InMemoryScheduleStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var allSchedules = _schedules.Values
			.OrderBy(static s => s.NextExecutionUtc ?? DateTimeOffset.MaxValue)
			.AsEnumerable();

		return Task.FromResult(allSchedules);
	}

	public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (string.IsNullOrEmpty(message.MessageBody))
		{
			throw new ArgumentException("MessageBody cannot be null or empty", nameof(message));
		}

		if (string.IsNullOrEmpty(message.MessageName))
		{
			throw new ArgumentException("MessageName cannot be null or empty", nameof(message));
		}

		// Upsert behavior - add or update existing
		_ = _schedules.AddOrUpdate(message.Id, message, (key, existing) => message);

		LogStoredSchedule(message.Id, message.MessageName, message.NextExecutionUtc);

		return Task.CompletedTask;
	}

	public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken = default)
	{
		if (_schedules.TryRemove(scheduleId, out var removedSchedule))
		{
			LogCompletedSchedule(scheduleId, removedSchedule.MessageName);
		}
		else
		{
			// Idempotent behavior - completing non-existent schedule is not an error
			LogScheduleNotFoundForCompletion(scheduleId);
		}

		return Task.CompletedTask;
	}

	[LoggerMessage(1001, LogLevel.Debug, "📅 Stored schedule {ScheduleId} ({MessageName}) for execution at {NextExecution}")]
	private partial void LogStoredSchedule(Guid scheduleId, string messageName, DateTimeOffset? nextExecution);

	[LoggerMessage(1002, LogLevel.Debug, "✅ Completed and removed schedule {ScheduleId} ({MessageName})")]
	private partial void LogCompletedSchedule(Guid scheduleId, string messageName);

	[LoggerMessage(1003, LogLevel.Debug, "⚠️ Schedule {ScheduleId} not found for completion (already completed or never existed)")]
	private partial void LogScheduleNotFoundForCompletion(Guid scheduleId);
}
