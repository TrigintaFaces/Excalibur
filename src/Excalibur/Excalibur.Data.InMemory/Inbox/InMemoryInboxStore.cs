// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Inbox;

/// <summary>
/// In-memory implementation of <see cref="IInboxStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides thread-safe message deduplication using ConcurrentDictionary.
/// Messages are keyed by a composite of (MessageId, HandlerType), allowing the same message
/// to be processed independently by multiple handlers.
/// </para>
/// <para>
/// This store is intended for testing scenarios only. Data is lost on application restart.
/// </para>
/// </remarks>
public sealed class InMemoryInboxStore : IInboxStore, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, InboxEntry> _entries = new(StringComparer.Ordinal);
	private readonly InMemoryInboxOptions _options;
	private readonly ILogger<InMemoryInboxStore> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryInboxStore(
		IOptions<InMemoryInboxOptions> options,
		ILogger<InMemoryInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc/>
	public ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// Enforce capacity limits before attempting to add
		if (_options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
		{
			EvictOldestEntry();
		}

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);

		// Use TryAdd for atomic create-if-not-exists semantics
		if (!_entries.TryAdd(key, entry))
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
		}

		_logger.LogDebug("Created inbox entry for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return new ValueTask<InboxEntry>(entry);
	}

	/// <inheritdoc/>
	public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		if (!_entries.TryGetValue(key, out var entry))
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		if (entry.Status == InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Message '{messageId}' for handler '{handlerType}' is already marked as processed.");
		}

		entry.MarkProcessed();

		_logger.LogDebug("Marked inbox entry as processed for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// Atomic first-writer-wins using TryAdd
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = [],
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow
		};

		if (_entries.TryAdd(key, entry))
		{
			_logger.LogDebug("First processor for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);
			return new ValueTask<bool>(true);
		}

		_logger.LogDebug("Duplicate detected for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);
		return new ValueTask<bool>(false);
	}

	/// <inheritdoc/>
	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);
		var isProcessed = _entries.TryGetValue(key, out var entry) &&
						  entry.Status == InboxStatus.Processed;

		return new ValueTask<bool>(isProcessed);
	}

	/// <inheritdoc/>
	public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);
		_ = _entries.TryGetValue(key, out var entry);

		return new ValueTask<InboxEntry?>(entry);
	}

	/// <inheritdoc/>
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		if (!_entries.TryGetValue(key, out var entry))
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		entry.MarkFailed(errorMessage);
		_logger.LogWarning("Marked inbox entry as failed for message {MessageId} and handler {HandlerType}: {Error}",
			messageId, handlerType, errorMessage);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var count = 0;
		foreach (var e in _entries.Values)
		{
			if (e.Status == InboxStatus.Failed &&
				(maxRetries <= 0 || e.RetryCount < maxRetries) &&
				(!olderThan.HasValue || e.LastAttemptAt < olderThan.Value))
			{
				count++;
			}
		}

		if (count == 0)
		{
			return new ValueTask<IEnumerable<InboxEntry>>(Array.Empty<InboxEntry>());
		}

		var candidates = new InboxEntry[count];
		var idx = 0;
		foreach (var e in _entries.Values)
		{
			if (e.Status == InboxStatus.Failed &&
				(maxRetries <= 0 || e.RetryCount < maxRetries) &&
				(!olderThan.HasValue || e.LastAttemptAt < olderThan.Value))
			{
				candidates[idx++] = e;
			}
		}

		Array.Sort(candidates, static (a, b) =>
		{
			var retryCompare = a.RetryCount.CompareTo(b.RetryCount);
			return retryCompare != 0 ? retryCompare : Nullable.Compare(a.LastAttemptAt, b.LastAttemptAt);
		});

		var resultSize = Math.Min(batchSize, candidates.Length);
		var failedEntries = resultSize == candidates.Length
			? candidates
			: candidates.AsSpan(0, resultSize).ToArray();

		return new ValueTask<IEnumerable<InboxEntry>>(failedEntries);
	}

	/// <inheritdoc/>
	public ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Use array-based approach to avoid ToList() allocation
		var entries = new InboxEntry[_entries.Count];
		_entries.Values.CopyTo(entries, 0);
		return new ValueTask<IEnumerable<InboxEntry>>(entries);
	}

	/// <inheritdoc/>
	public ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// AD-251-3: Single-pass counting without multiple enumeration
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
			TotalEntries = total,
			ProcessedEntries = processed,
			FailedEntries = failed,
			PendingEntries = pending
		});
	}

	/// <inheritdoc/>
	public ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cutoffDate = DateTimeOffset.UtcNow - retentionPeriod;
		var count = 0;

		foreach (var kvp in _entries.ToArray())
		{
			var entry = kvp.Value;
			if (entry is { Status: InboxStatus.Processed, ProcessedAt: not null } &&
				entry.ProcessedAt.Value <= cutoffDate && _entries.TryRemove(kvp.Key, out _))
			{
				count++;
			}
		}

		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}",
			count, cutoffDate);

		return new ValueTask<int>(count);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_entries.Clear();
		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private static string GetKey(string messageId, string handlerType)
		=> $"{messageId}:{handlerType}";

	private void EvictOldestEntry()
	{
		var oldestEntry = _entries.Values
			.OrderBy(e => e.ReceivedAt)
			.FirstOrDefault();

		if (oldestEntry != null)
		{
			var oldKey = GetKey(oldestEntry.MessageId, oldestEntry.HandlerType);
			_ = _entries.TryRemove(oldKey, out _);
		}
	}
}
