// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Data.InMemory.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Outbox;

/// <summary>
/// In-memory implementation of <see cref="IOutboxStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe message storage using ConcurrentDictionary.
/// Messages are keyed by their unique message ID.
/// </para>
/// <para>
/// This store is intended for testing scenarios only. Data is lost on application restart.
/// </para>
/// </remarks>
public sealed partial class InMemoryOutboxStore : IOutboxStore, IOutboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, OutboundMessage> _messages = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, object> _messageLocks = new(StringComparer.Ordinal);
	private readonly InMemoryOutboxOptions _options;
	private readonly ILogger<InMemoryOutboxStore> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryOutboxStore(
		IOptions<InMemoryOutboxOptions> options,
		ILogger<InMemoryOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc/>
	public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_messages.ContainsKey(message.Id))
		{
			throw new InvalidOperationException($"Message with ID '{message.Id}' already exists in the outbox.");
		}

		// Enforce capacity limits
		EnforceCapacityLimit();

		if (!_messages.TryAdd(message.Id, message))
		{
			throw new InvalidOperationException($"Message with ID '{message.Id}' already exists in the outbox.");
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outbound = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId
		};

		// Enforce capacity limits
		EnforceCapacityLimit();

		if (!_messages.TryAdd(outbound.Id, outbound))
		{
			throw new InvalidOperationException($"Failed to enqueue message with ID '{outbound.Id}'.");
		}

		LogMessageEnqueued(outbound.Id, messageType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var now = DateTimeOffset.UtcNow;

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var count = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Staged && (m.ScheduledAt == null || m.ScheduledAt <= now))
			{
				count++;
			}
		}

		if (count == 0)
		{
			return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
		}

		var candidates = new OutboundMessage[count];
		var idx = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Staged && (m.ScheduledAt == null || m.ScheduledAt <= now))
			{
				candidates[idx++] = m;
			}
		}

		Array.Sort(candidates, static (a, b) =>
		{
			var priorityCompare = a.Priority.CompareTo(b.Priority);
			return priorityCompare != 0 ? priorityCompare : a.CreatedAt.CompareTo(b.CreatedAt);
		});

		var resultSize = Math.Min(batchSize, candidates.Length);
		var unsent = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<OutboundMessage>>(unsent);
	}

	/// <inheritdoc/>
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_messages.TryGetValue(messageId, out var message))
		{
			throw new InvalidOperationException($"Message with ID '{messageId}' not found.");
		}

		// Use per-message locking to ensure atomic status transition
		var messageLock = _messageLocks.GetOrAdd(messageId, _ => new object());

		lock (messageLock)
		{
			if (message.Status == OutboxStatus.Sent)
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
			}

			message.MarkSent();
		}

		LogMessageSent(messageId);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_messages.TryGetValue(messageId, out var message))
		{
			// Silent return for missing messages per conformance tests expectation
			return default;
		}

		message.MarkFailed(errorMessage);
		message.RetryCount = retryCount;

		LogMessageFailed(messageId, errorMessage, retryCount);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var count = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Failed &&
				(maxRetries <= 0 || m.RetryCount < maxRetries) &&
				(!olderThan.HasValue || m.LastAttemptAt < olderThan.Value))
			{
				count++;
			}
		}

		if (count == 0)
		{
			return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
		}

		var candidates = new OutboundMessage[count];
		var idx = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Failed &&
				(maxRetries <= 0 || m.RetryCount < maxRetries) &&
				(!olderThan.HasValue || m.LastAttemptAt < olderThan.Value))
			{
				candidates[idx++] = m;
			}
		}

		Array.Sort(candidates, static (a, b) =>
		{
			var retryCompare = a.RetryCount.CompareTo(b.RetryCount);
			return retryCompare != 0 ? retryCompare : Nullable.Compare(a.LastAttemptAt, b.LastAttemptAt);
		});

		var resultSize = Math.Min(batchSize, candidates.Length);
		var failed = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<OutboundMessage>>(failed);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var count = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Staged && m.ScheduledAt.HasValue && m.ScheduledAt.Value <= scheduledBefore)
			{
				count++;
			}
		}

		if (count == 0)
		{
			return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
		}

		var candidates = new OutboundMessage[count];
		var idx = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Staged && m.ScheduledAt.HasValue && m.ScheduledAt.Value <= scheduledBefore)
			{
				candidates[idx++] = m;
			}
		}

		Array.Sort(candidates, static (a, b) => Nullable.Compare(a.ScheduledAt, b.ScheduledAt));

		var resultSize = Math.Min(batchSize, candidates.Length);
		var scheduled = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<OutboundMessage>>(scheduled);
	}

	/// <inheritdoc/>
	public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var candidateCount = 0;
		foreach (var m in _messages.Values)
		{
			if (m.Status == OutboxStatus.Sent && m.SentAt.HasValue && m.SentAt.Value < olderThan)
			{
				candidateCount++;
			}
		}

		if (candidateCount == 0)
		{
			LogMessagesCleanedUp(0, olderThan);
			return new ValueTask<int>(0);
		}

		var toRemove = new OutboundMessage[Math.Min(candidateCount, batchSize)];
		var idx = 0;
		foreach (var m in _messages.Values)
		{
			if (idx >= toRemove.Length)
			{
				break;
			}

			if (m.Status == OutboxStatus.Sent && m.SentAt.HasValue && m.SentAt.Value < olderThan)
			{
				toRemove[idx++] = m;
			}
		}

		var count = 0;
		for (var i = 0; i < idx; i++)
		{
			var message = toRemove[i];
			if (_messages.TryRemove(message.Id, out _))
			{
				_ = _messageLocks.TryRemove(message.Id, out _);
				count++;
			}
		}

		LogMessagesCleanedUp(count, olderThan);

		return new ValueTask<int>(count);
	}

	/// <inheritdoc/>
	public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Single-pass statistics without ToList() allocations
		var now = DateTimeOffset.UtcNow;
		var stagedCount = 0;
		var sendingCount = 0;
		var sentCount = 0;
		var failedCount = 0;
		var scheduledCount = 0;
		OutboundMessage? oldestUnsent = null;
		OutboundMessage? oldestFailed = null;

		foreach (var message in _messages.Values)
		{
			switch (message.Status)
			{
				case OutboxStatus.Staged:
					stagedCount++;
					if (message.ScheduledAt.HasValue)
					{
						scheduledCount++;
					}
					else if (oldestUnsent == null || message.CreatedAt < oldestUnsent.CreatedAt)
					{
						// Unsent = staged without schedule, or scheduled and due
						oldestUnsent = message;
					}

					// Check scheduled messages that are due
					if (message.ScheduledAt.HasValue && message.ScheduledAt <= now &&
						(oldestUnsent == null || message.CreatedAt < oldestUnsent.CreatedAt))
					{
						oldestUnsent = message;
					}

					break;

				case OutboxStatus.Sending:
					sendingCount++;
					break;

				case OutboxStatus.Sent:
					sentCount++;
					break;

				case OutboxStatus.Failed:
					failedCount++;
					if (oldestFailed == null || message.CreatedAt < oldestFailed.CreatedAt)
					{
						oldestFailed = message;
					}

					break;
			}
		}

		return new ValueTask<OutboxStatistics>(new OutboxStatistics
		{
			StagedMessageCount = stagedCount,
			SendingMessageCount = sendingCount,
			SentMessageCount = sentCount,
			FailedMessageCount = failedCount,
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsent != null ? now - oldestUnsent.CreatedAt : null,
			OldestFailedMessageAge = oldestFailed != null ? now - oldestFailed.CreatedAt : null,
			CapturedAt = now
		});
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_messages.Clear();
		_messageLocks.Clear();
		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private void EnforceCapacityLimit()
	{
		if (_options.MaxMessages > 0 && _messages.Count >= _options.MaxMessages)
		{
			EvictOldestSentMessage();
		}
	}

	private void EvictOldestSentMessage()
	{
		// First try to evict sent messages
		var oldestSent = _messages.Values
			.Where(m => m.Status == OutboxStatus.Sent)
			.OrderBy(m => m.SentAt)
			.FirstOrDefault();

		if (oldestSent != null)
		{
			_ = _messages.TryRemove(oldestSent.Id, out _);
			_ = _messageLocks.TryRemove(oldestSent.Id, out _);
			return;
		}

		// If no sent messages, evict oldest message regardless of status
		var oldest = _messages.Values
			.OrderBy(m => m.CreatedAt)
			.FirstOrDefault();

		if (oldest != null)
		{
			_ = _messages.TryRemove(oldest.Id, out _);
			_ = _messageLocks.TryRemove(oldest.Id, out _);
		}
	}

	#region High-Performance Logging

	[LoggerMessage(DataInMemoryEventId.OutboxMessageStaged, LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataInMemoryEventId.OutboxMessageFailed, LogLevel.Warning,
		"Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataInMemoryEventId.OutboxCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);

	#endregion High-Performance Logging
}
