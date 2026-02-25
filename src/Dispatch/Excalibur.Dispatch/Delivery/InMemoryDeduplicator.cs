// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// High-performance in-memory implementation of message deduplication for light mode inbox processing.
/// </summary>
/// <remarks>
/// This implementation provides:
/// <list type="bullet">
/// <item> Thread-safe concurrent operations for high-throughput scenarios </item>
/// <item> Automatic cleanup of expired entries to prevent memory growth </item>
/// <item> Efficient memory usage with timestamp-based expiry tracking </item>
/// <item> Performance statistics for monitoring and optimization </item>
/// </list>
/// The deduplicator uses a concurrent dictionary for thread-safe operations and periodic cleanup to manage memory usage. It's optimized for
/// scenarios where message volumes are predictable and memory constraints are well-understood.
/// </remarks>
public sealed partial class InMemoryDeduplicator : IInMemoryDeduplicator, IDisposable
{
	private readonly ConcurrentDictionary<string, ProcessedEntry> _processedMessages = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryDeduplicator> _logger;
	private readonly Timer _cleanupTimer;
#if NET9_0_OR_GREATER

	private readonly Lock _statsLock = new();

#else

	private readonly object _statsLock = new();

#endif

	private readonly ValueStopwatch _uptime = ValueStopwatch.StartNew();

	/// <summary>
	/// Statistics tracking.
	/// </summary>
	private long _totalChecks;

	private long _duplicatesDetected;
	private long _entriesExpired;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDeduplicator"/> class.
	/// Creates a new in-memory deduplicator instance.
	/// </summary>
	/// <param name="logger"> Logger for diagnostic information. </param>
	/// <param name="cleanupInterval"> Optional interval between automatic cleanup runs. Default is 5 minutes. </param>
	public InMemoryDeduplicator(
		ILogger<InMemoryDeduplicator> logger,
		TimeSpan? cleanupInterval = null)
	{
		ArgumentNullException.ThrowIfNull(logger);

		_logger = logger;

		// Set up periodic cleanup timer
		var interval = cleanupInterval ?? TimeSpan.FromMinutes(5);
		_cleanupTimer = new Timer(
			async _ => await PerformScheduledCleanupAsync().ConfigureAwait(false),
			state: null,
			interval,
			interval);

		LogDeduplicatorInitialized(interval);
	}

	/// <inheritdoc />
	public Task<bool> IsDuplicateAsync(
		string messageId,
		TimeSpan expiry,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		if (expiry <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(expiry), ErrorMessages.ArgumentMustBePositive);
		}

		_ = Interlocked.Increment(ref _totalChecks);

		var now = DateTimeOffset.UtcNow;

		// Check if message exists and hasn't expired
		if (_processedMessages.TryGetValue(messageId, out var entry))
		{
			if (entry.ExpiresAt > now)
			{
				// Message is a duplicate
				_ = Interlocked.Increment(ref _duplicatesDetected);

				LogDuplicateDetected(messageId, entry.ExpiresAt);

				return Task.FromResult(true);
			}

			// Entry has expired, remove it
			if (_processedMessages.TryRemove(messageId, out _))
			{
				_ = Interlocked.Increment(ref _entriesExpired);
				LogExpiredEntryRemoved(messageId);
			}
		}

		return Task.FromResult(false);
	}

	/// <inheritdoc />
	public Task MarkProcessedAsync(
		string messageId,
		TimeSpan expiry,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		if (expiry <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(expiry), ErrorMessages.ArgumentMustBePositive);
		}

		var now = DateTimeOffset.UtcNow;
		var expiresAt = now.Add(expiry);

		var entry = new ProcessedEntry { MessageId = messageId, ProcessedAt = now, ExpiresAt = expiresAt };

		// AddOrUpdate to handle race conditions
		_ = _processedMessages.AddOrUpdate(
			messageId,
			static (key, state) =>
			{
				_ = key;
				return state;
			},
			static (key, existing, state) =>
			{
				_ = key;
				// If the message is already processed, extend expiry if new one is later
				if (state.ExpiresAt > existing.ExpiresAt)
				{
					return state;
				}

				return existing;
			},
			entry);

		LogMessageMarkedProcessed(messageId, expiresAt);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<int> CleanupExpiredEntriesAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		var removedCount = 0;
		var expiredKeys = new List<string>();

		// Identify expired entries
		foreach (var kvp in _processedMessages)
		{
			if (kvp.Value.ExpiresAt <= now)
			{
				expiredKeys.Add(kvp.Key);
			}

			// Check for cancellation periodically
			if (removedCount % 100 == 0 && cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}

		// Remove expired entries
		foreach (var key in expiredKeys)
		{
			if (_processedMessages.TryRemove(key, out _))
			{
				removedCount++;
				_ = Interlocked.Increment(ref _entriesExpired);
			}
		}

		if (removedCount > 0)
		{
			LogCleanedUpExpiredEntries(removedCount);
		}

		return Task.FromResult(removedCount);
	}

	/// <inheritdoc />
	public DeduplicationStatistics GetStatistics()
	{
		lock (_statsLock)
		{
			var entryCount = _processedMessages.Count;

			// Estimate memory usage (rough approximation) Each entry: string key (~40 bytes avg) + ProcessedEntry (~48 bytes) + dictionary
			// overhead (~32 bytes)
			const long estimatedBytesPerEntry = 120L;
			var estimatedMemoryBytes = entryCount * estimatedBytesPerEntry;

			return new DeduplicationStatistics
			{
				TrackedMessageCount = entryCount,
				TotalChecks = _totalChecks,
				DuplicatesDetected = _duplicatesDetected,
				EstimatedMemoryUsageBytes = estimatedMemoryBytes,
				CapturedAt = DateTimeOffset.UtcNow,
			};
		}
	}

	/// <inheritdoc />
	public Task ClearAsync()
	{
		var clearedCount = _processedMessages.Count;
		_processedMessages.Clear();

		LogClearedEntries(clearedCount);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_cleanupTimer?.Dispose();
		_processedMessages.Clear();

		var finalStats = GetStatistics();
		LogDeduplicatorDisposed(finalStats, _uptime.Elapsed);
	}

	/// <summary>
	/// Performs scheduled cleanup of expired entries.
	/// </summary>
	private async Task PerformScheduledCleanupAsync()
	{
		try
		{
			var removedCount = await CleanupExpiredEntriesAsync(CancellationToken.None).ConfigureAwait(false);

			if (removedCount > 0)
			{
				LogScheduledCleanupRemoved(removedCount);
			}

			// Log statistics periodically (every 10 cleanups)
			if (_entriesExpired % 50 == 0 && _entriesExpired > 0)
			{
				var stats = GetStatistics();
				LogDeduplicatorStats(stats);
			}
		}
		catch (Exception ex)
		{
			LogScheduledCleanupError(ex);
		}
	}

	/// <summary>
	/// Internal structure to track processed message information.
	/// </summary>
	private sealed class ProcessedEntry
	{
		/// <summary>
		/// Gets the message identifier.
		/// </summary>
		/// <value>The current <see cref="MessageId"/> value.</value>
		public required string MessageId { get; init; }

		/// <summary>
		/// Gets when the message was processed.
		/// </summary>
		/// <value>The current <see cref="ProcessedAt"/> value.</value>
		public required DateTimeOffset ProcessedAt { get; init; }

		/// <summary>
		/// Gets when this entry should expire.
		/// </summary>
		/// <value>The current <see cref="ExpiresAt"/> value.</value>
		public required DateTimeOffset ExpiresAt { get; init; }
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(DeliveryEventId.DeduplicatorInitialized, LogLevel.Debug,
		"InMemoryDeduplicator initialized with cleanup interval: {CleanupInterval}")]
	private partial void LogDeduplicatorInitialized(TimeSpan cleanupInterval);

	[LoggerMessage(DeliveryEventId.DuplicateDetected, LogLevel.Trace,
		"Message {MessageId} detected as duplicate, expires at {ExpiresAt}")]
	private partial void LogDuplicateDetected(string messageId, DateTimeOffset expiresAt);

	[LoggerMessage(DeliveryEventId.ExpiredEntryRemoved, LogLevel.Trace,
		"Expired entry removed for message {MessageId}")]
	private partial void LogExpiredEntryRemoved(string messageId);

	[LoggerMessage(DeliveryEventId.MessageMarkedProcessed, LogLevel.Trace,
		"Message {MessageId} marked as processed, expires at {ExpiresAt}")]
	private partial void LogMessageMarkedProcessed(string messageId, DateTimeOffset expiresAt);

	[LoggerMessage(DeliveryEventId.CleanedUpExpiredEntries, LogLevel.Debug,
		"Cleaned up {RemovedCount} expired deduplication entries")]
	private partial void LogCleanedUpExpiredEntries(int removedCount);

	[LoggerMessage(DeliveryEventId.ClearedEntries, LogLevel.Information,
		"Cleared {ClearedCount} entries from in-memory deduplicator")]
	private partial void LogClearedEntries(int clearedCount);

	[LoggerMessage(DeliveryEventId.DeduplicatorDisposed, LogLevel.Information,
		"InMemoryDeduplicator disposed. Final stats: {Statistics}, Uptime: {Uptime}")]
	private partial void LogDeduplicatorDisposed(object statistics, TimeSpan uptime);

	[LoggerMessage(DeliveryEventId.ScheduledCleanupRemoved, LogLevel.Trace,
		"Scheduled cleanup removed {RemovedCount} expired entries")]
	private partial void LogScheduledCleanupRemoved(int removedCount);

	[LoggerMessage(DeliveryEventId.DeduplicatorStats, LogLevel.Information,
		"Deduplicator stats: {Statistics}")]
	private partial void LogDeduplicatorStats(object statistics);

	[LoggerMessage(DeliveryEventId.ScheduledCleanupError, LogLevel.Error,
		"Error during scheduled deduplication cleanup")]
	private partial void LogScheduledCleanupError(Exception ex);
}
