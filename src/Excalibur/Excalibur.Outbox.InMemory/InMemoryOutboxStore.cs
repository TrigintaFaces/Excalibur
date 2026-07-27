// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Data.InMemory.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.InMemory;

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
public sealed partial class InMemoryOutboxStore : IFencedOutboxStore, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<string, OutboundMessage> _messages = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, object> _messageLocks = new(StringComparer.Ordinal);

	/// <summary>
	/// Side-map of claim leases, keyed by message ID. Kept separate from <see cref="OutboundMessage"/> (the
	/// public/shared domain model) rather than adding lease fields to it directly.
	/// </summary>
	private readonly ConcurrentDictionary<string, (DateTimeOffset LeasedAt, string LeasedBy)> _leases = new(StringComparer.Ordinal);

	/// <summary>
	/// Side-map of failure-anchored visibility floors, keyed by message ID. A failed (sub-ceiling) message
	/// is re-claimable only once <c>now &gt;= floor</c>, where the floor is stamped at the failure instant
	/// (not the lease). This is the canonical <c>MarkFailedAsync</c> re-claimability contract: never
	/// re-claimable in the same drain cycle (no zero-backoff hot-loop), never terminally dropped
	/// (at-least-once). Kept off the shared <see cref="OutboundMessage"/> model, mirroring <see cref="_leases"/>.
	/// </summary>
	private readonly ConcurrentDictionary<string, DateTimeOffset> _nextAttempt = new(StringComparer.Ordinal);

	/// <summary>
	/// Guards the select-and-lease sequence in <see cref="GetUnsentMessagesAsync(int, CancellationToken)"/> so that choosing the
	/// claimable batch and recording its leases happens atomically -- otherwise two concurrent callers could
	/// both select the same eligible messages before either records a lease.
	/// </summary>
	private readonly Lock _claimLock = new();

	/// <summary>
	/// The highest outbox fencing token observed so far, used to fail-closed reject mark-sent calls and
	/// exclude claims from a superseded (stale) leader. Guarded by <see cref="_claimLock"/>.
	/// </summary>
	private long _fencingHighWaterMark;

	private readonly string _processorId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
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
#pragma warning disable IL2026, IL3050 // In-memory store uses reflection-based serialization
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());
#pragma warning restore IL2026, IL3050

		var outbound = OutboundMessage.FromContext(messageType, payload, messageType, context);

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
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		GetUnsentMessagesCore(batchSize, fencingToken: null, cancellationToken);

	/// <inheritdoc />
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
		GetUnsentMessagesCore(batchSize, fencingToken, cancellationToken);

	private ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesCore(int batchSize, long? fencingToken, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var now = DateTimeOffset.UtcNow;
		var leaseCutoff = now - TimeSpan.FromSeconds(_options.LeaseTimeoutSeconds);

		// Atomically select-and-lease under a single lock: choosing the claimable batch and recording its
		// leases happens as one step, so two concurrent callers (pollers in the same process) can never
		// both select the same eligible message -- mirrors the SQL Server / MongoDB atomic-claim contract.
		lock (_claimLock)
		{
			if (fencingToken.HasValue)
			{
				if (fencingToken.Value < _fencingHighWaterMark)
				{
					// Presented token is stale (superseded leader): exclude all rows from the claim rather
					// than throwing -- this is a set-based operation.
					return new ValueTask<IEnumerable<OutboundMessage>>(Array.Empty<OutboundMessage>());
				}

				_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fencingToken.Value);
			}

			// AD-251-3: Use array-based approach to avoid ToList() allocation
			var count = 0;
			foreach (var m in _messages.Values)
			{
				if (IsClaimable(m, now, leaseCutoff))
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
				if (IsClaimable(m, now, leaseCutoff))
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
			var claimed = new OutboundMessage[resultSize];
			for (var i = 0; i < resultSize; i++)
			{
				claimed[i] = candidates[i];
				_leases[candidates[i].Id] = (now, _processorId);
			}

			return new ValueTask<IEnumerable<OutboundMessage>>(claimed);
		}
	}

	/// <summary>
	/// Determines whether a message is eligible to be claimed: staged, due (not scheduled for the future),
	/// and either unleased or its lease has gone stale (a crash-recovery reclaim).
	/// </summary>
	private bool IsClaimable(OutboundMessage message, DateTimeOffset now, DateTimeOffset leaseCutoff)
	{
		// Staged (never attempted) and Failed (attempted, sub-retry-ceiling — still owed at-least-once
		// delivery) are both eligible; Sent/Sending/DeadLettered are not. A Failed message re-enters the
		// claimable set only once its post-failure lease has aged past the coarse lease-timeout floor
		// below (never in the same drain cycle — no zero-backoff hot-loop), and it is never terminally
		// dropped (at-least-once). This is the canonical MarkFailedAsync re-claimability contract shared
		// across the outbox family; fine-grained backoff is the separate MarkFailedWithBackoffAsync path.
		if ((message.Status is not OutboxStatus.Staged and not OutboxStatus.Failed)
			|| (message.ScheduledAt != null && message.ScheduledAt > now))
		{
			return false;
		}

		// R1 failure-anchored floor: a failed message is not re-claimable until its post-failure visibility
		// window elapses (no zero-backoff hot-loop). Enforced by this store-owned timestamp, never a
		// dispatcher-side clock delta.
		if (_nextAttempt.TryGetValue(message.Id, out var nextAttempt) && now < nextAttempt)
		{
			return false;
		}

		return !_leases.TryGetValue(message.Id, out var lease) || lease.LeasedAt < leaseCutoff;
	}

	/// <inheritdoc/>
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
		MarkSentCore(messageId, fencingToken: null, cancellationToken);

	/// <inheritdoc />
	public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) =>
		MarkSentCore(messageId, fencingToken, cancellationToken);

	private ValueTask MarkSentCore(string messageId, long? fencingToken, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (fencingToken.HasValue)
		{
			lock (_claimLock)
			{
				if (fencingToken.Value < _fencingHighWaterMark)
				{
					throw new StaleOutboxFencingTokenException(
						$"The presented outbox fencing token ({fencingToken.Value}) is lower than the recorded high-water mark ({_fencingHighWaterMark}).")
					{
						PresentedToken = fencingToken.Value,
						HighWaterToken = _fencingHighWaterMark,
					};
				}

				_fencingHighWaterMark = Math.Max(_fencingHighWaterMark, fencingToken.Value);
			}
		}

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

		_ = _leases.TryRemove(messageId, out _);
		_ = _nextAttempt.TryRemove(messageId, out _);

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

		var now = DateTimeOffset.UtcNow;

		// R2 — reservation-ownership guard: a MarkFailed reported against a reservation a DIFFERENT
		// processor now holds is a no-op. A superseded/zombie processor's late failure report cannot free a
		// live successor's lease, so it can never trigger a second concurrent delivery. Mirrors the SQL
		// family's "dispatcher_id IN (NULL, @caller)" claim predicate; the unreserved-input path (stage then
		// fail without ever claiming) has no lease and proceeds.
		if (_leases.TryGetValue(messageId, out var lease)
			&& !string.Equals(lease.LeasedBy, _processorId, StringComparison.Ordinal))
		{
			return default;
		}

		// R3 — attempts are non-decreasing across re-claims: never let a stale late writer move the count
		// DOWN, which would weaken the processor's DLQ-ceiling (termination) guarantee. Capture the prior
		// persisted count BEFORE MarkFailed (which itself increments RetryCount) and take the max against
		// the caller-reported count, so a stale lower report cannot lower the authoritative value.
		var priorRetryCount = message.RetryCount;
		message.MarkFailed(errorMessage);
		message.RetryCount = Math.Max(priorRetryCount, retryCount);
		message.LastAttemptAt = now;

		// R1 — failure-anchored visibility floor: the message re-enters the claimable set only after the
		// dedicated failure-backoff floor F elapses FROM THE FAILURE INSTANT — never the same drain cycle (no
		// zero-backoff hot-loop), never terminally (at-least-once; the outbox must not silently drop it). F is
		// decoupled from the crash-recovery lease window and sized to exceed the poll interval. The failure
		// stays observable via GetFailedMessages / GetStatistics (Status == Failed). Fine-grained backoff
		// remains the separate MarkFailedWithBackoffAsync path. Free the caller-owned reservation so the
		// floor — not a lingering lease — governs the next claim.
		_nextAttempt[messageId] = now + TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds);
		_ = _leases.TryRemove(messageId, out _);

		LogMessageFailed(messageId, errorMessage, retryCount);

		return default;
	}

	/// <inheritdoc/>
	public ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_messages.TryGetValue(messageId, out var message))
		{
			// Mirror MarkFailedAsync: silent return for missing messages
			return default;
		}

		message.Status = OutboxStatus.DeadLettered;
		message.LastError = reason;

		// Clear per-message lock, claim lease, and failure-visibility floor for hygiene. DeadLettered is
		// terminal — the claim predicate already excludes it, so no floor is needed to keep it out.
		_ = _messageLocks.TryRemove(messageId, out _);
		_ = _leases.TryRemove(messageId, out _);
		_ = _nextAttempt.TryRemove(messageId, out _);

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
	public ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
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
				_ = _leases.TryRemove(message.Id, out _);
				_ = _nextAttempt.TryRemove(message.Id, out _);
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
		_leases.Clear();
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
		if (TryGetEvictionCandidateId(out var messageId))
		{
			_ = _messages.TryRemove(messageId, out _);
			_ = _messageLocks.TryRemove(messageId, out _);
			_ = _leases.TryRemove(messageId, out _);
			_ = _nextAttempt.TryRemove(messageId, out _);
		}
	}

	private bool TryGetEvictionCandidateId(out string messageId)
	{
		OutboundMessage? oldestSentMessage = null;
		DateTimeOffset? oldestSentAt = null;
		OutboundMessage? oldestOverallMessage = null;
		var oldestCreatedAt = DateTimeOffset.MaxValue;

		foreach (var message in _messages.Values)
		{
			if (message.CreatedAt < oldestCreatedAt)
			{
				oldestCreatedAt = message.CreatedAt;
				oldestOverallMessage = message;
			}

			if (message.Status != OutboxStatus.Sent)
			{
				continue;
			}

			if (oldestSentMessage is null ||
				Nullable.Compare(message.SentAt, oldestSentAt) < 0)
			{
				oldestSentMessage = message;
				oldestSentAt = message.SentAt;
			}
		}

		var candidate = oldestSentMessage ?? oldestOverallMessage;
		if (candidate is null)
		{
			messageId = string.Empty;
			return false;
		}

		messageId = candidate.Id;
		return true;
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
