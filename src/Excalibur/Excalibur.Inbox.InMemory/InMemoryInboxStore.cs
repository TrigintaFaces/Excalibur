// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.InMemory;

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
internal sealed class InMemoryInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, InboxEntry> _entries = new(StringComparer.Ordinal);

	// Companion lease-expiry map (unix-ms) for the lease-based claim overload, plus a lock so the
	// read-decide-write across both maps is a single atomic compare-and-set. A single-process store has no
	// distributed clock skew, so the local wall clock is the authority here.
	private readonly ConcurrentDictionary<string, long> _leaseExpiryUnixMs = new(StringComparer.Ordinal);
	private readonly System.Threading.Lock _leaseClaimLock = new();
	private readonly InMemoryInboxOptions _options;
	private readonly ILogger<InMemoryInboxStore> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Timer? _cleanupTimer;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">
	/// Optional time provider used for lease-expiry and entry timestamps. Defaults to
	/// <see cref="TimeProvider.System"/>. Inject a controllable provider to make lease expiry
	/// deterministic in tests.
	/// </param>
	public InMemoryInboxStore(
		IOptions<InMemoryInboxOptions> options,
		ILogger<InMemoryInboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;

		// Only start the cleanup timer when EnableAutomaticCleanup is true
		if (_options.EnableAutomaticCleanup)
		{
			_cleanupTimer = new Timer(
				_ => PerformScheduledCleanup(),
				state: null,
				_options.CleanupInterval,
				_options.CleanupInterval);
		}
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

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

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

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

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
	public ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
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

		// Durably mark Processing. The stored entry is the live reference, so the transition (and the
		// LastAttemptAt stamp the stuck-processing timeout reads) is observable via GetEntryAsync.
		entry.MarkProcessing();

		_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}",
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
			ProcessedAt = _timeProvider.GetUtcNow()
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
	public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// Enforce capacity limits before attempting to add.
		if (_options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
		{
			EvictOldestEntry();
		}

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state. A successful claim is
		// finalized to Processed via MarkProcessedAsync, or removed via ReleaseAsync on handler failure.
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = [],
			Status = InboxStatus.Processing
		};

		if (_entries.TryAdd(key, entry))
		{
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);
			return new ValueTask<bool>(true);
		}

		_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);
		return new ValueTask<bool>(false);
	}

	/// <inheritdoc/>
	public ValueTask<bool> TryClaimAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);
		var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

		// Single atomic lease CAS under the lock: claim IFF absent, Received, or an expired-lease Processing
		// entry (reclaiming a dead processor). A live Processing lease or a terminal Processed entry is denied.
		lock (_leaseClaimLock)
		{
			var exists = _entries.TryGetValue(key, out var existing);

			var claimable = !exists
				|| existing!.Status == InboxStatus.Received
				|| existing.Status == InboxStatus.Failed
				|| (existing.Status == InboxStatus.Processing
					&& (!_leaseExpiryUnixMs.TryGetValue(key, out var expiry) || expiry < nowMs));

			if (!claimable)
			{
				_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}",
					messageId, handlerType);
				return new ValueTask<bool>(false);
			}

			if (!exists && _options.MaxEntries > 0 && _entries.Count >= _options.MaxEntries)
			{
				EvictOldestEntry();
			}

			_entries[key] = new InboxEntry
			{
				MessageId = messageId,
				HandlerType = handlerType,
				MessageType = string.Empty,
				Payload = [],
				Status = InboxStatus.Processing,
				ReceivedAt = existing?.ReceivedAt ?? _timeProvider.GetUtcNow(),
				// Preserve retry history across re-admit (never reset); the shared handler-finalize path
				// (MarkFailedAsync) is the single monotonic incrementer, so the count is exactly-once per attempt.
				RetryCount = existing?.RetryCount ?? 0
			};
			_leaseExpiryUnixMs[key] = nowMs + (long)leaseDuration.TotalMilliseconds;

			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}",
				messageId, handlerType);
			return new ValueTask<bool>(true);
		}
	}

	/// <inheritdoc/>
	public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var key = GetKey(messageId, handlerType);

		// Remove the claim so a redelivery can re-admit. No-op if already removed or never claimed.
		_ = _entries.TryRemove(key, out _);
		_ = _leaseExpiryUnixMs.TryRemove(key, out _);

		_logger.LogDebug("Released inbox claim for message {MessageId} and handler {HandlerType}",
			messageId, handlerType);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

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

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

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
	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var key = GetKey(messageId, handlerType);

		if (!_entries.TryGetValue(key, out var entry))
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		// Set the retry count EXACTLY (no increment) so a transient short-circuit (e.g. an open circuit
		// breaker) leaves the entry re-admittable without consuming a delivery attempt (FR-4).
		entry.Status = InboxStatus.Failed;
		entry.LastError = errorMessage;
		entry.RetryCount = retryCount;
		entry.LastAttemptAt = _timeProvider.GetUtcNow();

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
	public ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var activity = InboxActivitySource.StartCleanupActivity();

		var count = 0;

		foreach (var kvp in _entries.ToArray())
		{
			var entry = kvp.Value;
			if (entry is { Status: InboxStatus.Processed, ProcessedAt: not null } &&
				entry.ProcessedAt.Value <= olderThan && _entries.TryRemove(kvp.Key, out _))
			{
				count++;
			}
		}

		_logger.LogInformation("Cleaned up {Count} processed inbox entries older than {CutoffDate}",
			count, olderThan);

		return new ValueTask<int>(count);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_cleanupTimer?.Dispose();
		_entries.Clear();
		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private void PerformScheduledCleanup()
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			var cutoff = _timeProvider.GetUtcNow().Subtract(_options.RetentionPeriod);
			var count = 0;

			foreach (var kvp in _entries.ToArray())
			{
				var entry = kvp.Value;
				if (entry is { Status: InboxStatus.Processed, ProcessedAt: not null } &&
					entry.ProcessedAt.Value <= cutoff && _entries.TryRemove(kvp.Key, out _))
				{
					count++;
				}
			}

			if (count > 0)
			{
				_logger.LogDebug("Scheduled cleanup removed {Count} processed inbox entries older than {CutoffDate}",
					count, cutoff);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during scheduled inbox cleanup");
		}
	}

	private static string GetKey(string messageId, string handlerType)
		=> $"{messageId}:{handlerType}";

	private void EvictOldestEntry()
	{
		// 5uajzo / Dijkstra D5 — eviction must FAIL CLOSED, never silently drop a live dedup record.
		// (Supersedes 0yy2sp's "bounded memory takes precedence" fallback: a silently-evicted live dedup
		// marker lets a redelivery re-admit and re-process the same message — a duplicate side-effect, the
		// exact thing the inbox exists to prevent. Dedup correctness outranks bounded memory here.)
		//
		// Reclaim, in priority order, an entry whose removal CANNOT cause a duplicate:
		//   1. the oldest NON-live entry (Received/Failed) — neither a dedup marker nor an in-flight claim;
		//   2. else the oldest entry PAST the dedup window (a Processed marker older than RetentionPeriod no
		//      longer protects against a duplicate — same predicate as PerformScheduledCleanup).
		// If neither exists, every entry is a live dedup marker / in-flight claim within the window, so
		// evicting any of them would risk a duplicate: THROW instead.
		var reclaimable = _entries.Values
			.Where(static e => e.Status is not (InboxStatus.Processed or InboxStatus.Processing))
			.OrderBy(static e => e.ReceivedAt)
			.FirstOrDefault();

		if (reclaimable is null)
		{
			var cutoff = _timeProvider.GetUtcNow().Subtract(_options.RetentionPeriod);
			reclaimable = _entries.Values
				.Where(e => e is { Status: InboxStatus.Processed, ProcessedAt: not null } && e.ProcessedAt.Value <= cutoff)
				.OrderBy(static e => e.ReceivedAt)
				.FirstOrDefault();
		}

		if (reclaimable is not null)
		{
			_ = _entries.TryRemove(GetKey(reclaimable.MessageId, reclaimable.HandlerType), out _);
			return;
		}

		throw new InvalidOperationException(
			$"The in-memory inbox is at capacity ({_options.MaxEntries}) and every entry is a live deduplication " +
			"record within the retention window. Evicting one would risk re-processing a duplicate message. " +
			"Increase MaxEntries or reduce RetentionPeriod.");
	}
}
