// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages ordering key state and sequence tracking for Google Pub/Sub messages.
/// </summary>
public sealed partial class OrderingKeyManager : IDisposable
{
	private readonly ConcurrentDictionary<string, OrderingKeyState> _orderingKeyStates;
	private readonly ILogger<OrderingKeyManager> _logger;
	private readonly IGooglePubSubMetrics _metrics;
	private readonly Timer _cleanupTimer;
	private readonly TimeSpan _stateTimeout;
	private readonly int _maxOrderingKeys;
	private readonly SemaphoreSlim _cleanupSemaphore;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingKeyManager" /> class.
	/// </summary>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="metrics"> Metrics collector. </param>
	/// <param name="stateTimeout"> Timeout for inactive ordering key states. </param>
	/// <param name="maxOrderingKeys"> Maximum number of ordering keys to track. </param>
	public OrderingKeyManager(
		ILogger<OrderingKeyManager> logger,
		IGooglePubSubMetrics metrics,
		TimeSpan? stateTimeout = null,
		int maxOrderingKeys = 10000)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_stateTimeout = stateTimeout ?? TimeSpan.FromMinutes(30);
		_maxOrderingKeys = maxOrderingKeys;

		_orderingKeyStates = new ConcurrentDictionary<string, OrderingKeyState>(
			Environment.ProcessorCount * 2,
			_maxOrderingKeys,
			StringComparer.Ordinal);

		_cleanupSemaphore = new SemaphoreSlim(1, 1);

		// Schedule periodic cleanup
		_cleanupTimer = new Timer(
			_ => _ = CleanupInactiveStatesAsync(),
			state: null,
			_stateTimeout,
			_stateTimeout);

		LogManagerInitialized(_stateTimeout, _maxOrderingKeys);
	}

	/// <summary>
	/// Records a message for an ordering key.
	/// </summary>
	/// <param name="orderingKey"> The ordering key. </param>
	/// <param name="messageId"> The message ID. </param>
	/// <param name="sequenceNumber"> Optional sequence number. </param>
	/// <returns> True if the message was recorded in sequence, false otherwise. </returns>
	/// <exception cref="ArgumentException"></exception>
	public bool RecordMessage(string orderingKey, string messageId, long? sequenceNumber = null)
	{
		if (string.IsNullOrEmpty(orderingKey))
		{
			throw new ArgumentException("Ordering key cannot be null or empty", nameof(orderingKey));
		}

		var state = _orderingKeyStates.GetOrAdd(
			orderingKey,
			static key => new OrderingKeyState(key));

		var inSequence = state.RecordMessage(messageId, sequenceNumber);

		if (!inSequence)
		{
			LogOutOfSequenceMessage(orderingKey, state.ExpectedSequence, sequenceNumber);
		}

		// Check if we need to trigger cleanup
		if (_orderingKeyStates.Count > _maxOrderingKeys * 0.9)
		{
			_ = CleanupInactiveStatesAsync();
		}

		return inSequence;
	}

	/// <summary>
	/// Marks an ordering key as failed.
	/// </summary>
	/// <param name="orderingKey"> The ordering key. </param>
	/// <param name="reason"> Failure reason. </param>
	public void MarkFailed(string orderingKey, string reason)
	{
		if (_orderingKeyStates.TryGetValue(orderingKey, out var state))
		{
			state.MarkFailed(reason);
			LogOrderingKeyFailed(orderingKey, reason);
		}
	}

	/// <summary>
	/// Resets a failed ordering key.
	/// </summary>
	/// <param name="orderingKey"> The ordering key to reset. </param>
	/// <returns> True if the key was reset, false if it wasn't in a failed state. </returns>
	public bool ResetFailedKey(string orderingKey)
	{
		if (_orderingKeyStates.TryGetValue(orderingKey, out var state) && state.IsFailed)
		{
			state.Reset();
			LogOrderingKeyReset(orderingKey);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Gets the state of an ordering key.
	/// </summary>
	/// <param name="orderingKey"> The ordering key. </param>
	/// <returns> The ordering key state, or null if not found. </returns>
	public OrderingKeyInfo? GetOrderingKeyInfo(string orderingKey)
	{
		if (_orderingKeyStates.TryGetValue(orderingKey, out var state))
		{
			return new OrderingKeyInfo
			{
				OrderingKey = orderingKey,
				MessageCount = state.MessageCount,
				LastSequence = state.LastSequence,
				ExpectedSequence = state.ExpectedSequence,
				IsFailed = state.IsFailed,
				FailureReason = state.FailureReason,
				LastActivity = state.LastActivity,
				OutOfSequenceCount = state.OutOfSequenceCount,
			};
		}

		return null;
	}

	/// <summary>
	/// Gets statistics about all ordering keys.
	/// </summary>
	/// <returns> Ordering key statistics. </returns>
	public OrderingKeyStatistics GetStatistics()
	{
		var activeCount = 0;
		var failedCount = 0;
		var totalMessages = 0L;
		var totalOutOfSequence = 0L;

		foreach (var state in _orderingKeyStates.Values)
		{
			if (state.IsFailed)
			{
				failedCount++;
			}
			else
			{
				activeCount++;
			}

			totalMessages += state.MessageCount;
			totalOutOfSequence += state.OutOfSequenceCount;
		}

		return new OrderingKeyStatistics
		{
			TotalOrderingKeys = _orderingKeyStates.Count,
			ActiveOrderingKeys = activeCount,
			FailedOrderingKeys = failedCount,
			TotalMessagesProcessed = totalMessages,
			TotalOutOfSequenceMessages = totalOutOfSequence,
		};
	}

	/// <summary>
	/// Removes all inactive ordering key states.
	/// </summary>
	/// <returns> Number of states removed. </returns>
	public async Task<int> CleanupInactiveStatesAsync()
	{
		if (!await _cleanupSemaphore.WaitAsync(0).ConfigureAwait(false))
		{
			// Cleanup already in progress
			return 0;
		}

		try
		{
			var cutoffTime = DateTimeOffset.UtcNow - _stateTimeout;
			var keysToRemove = new List<string>();

			foreach (var kvp in _orderingKeyStates)
			{
				if (kvp.Value.LastActivity < cutoffTime && !kvp.Value.IsFailed)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var key in keysToRemove)
			{
				_ = _orderingKeyStates.TryRemove(key, out _);
			}

			if (keysToRemove.Count > 0)
			{
				LogCleanupCompleted(keysToRemove.Count);
			}

			return keysToRemove.Count;
		}
		finally
		{
			_ = _cleanupSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_cleanupTimer?.Dispose();
		_cleanupSemaphore?.Dispose();
	}

	/// <summary>
	/// State tracking for a single ordering key.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "orderingKey parameter is used by the auto-property initialization")]
	private sealed class OrderingKeyState(string orderingKey)
	{
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private long _messageCount;
		private long _outOfSequenceCount;
		private long _lastSequence = -1;

		public string OrderingKey { get; } = orderingKey;

		public long MessageCount => Interlocked.Read(ref _messageCount);

		public long OutOfSequenceCount => Interlocked.Read(ref _outOfSequenceCount);

		public long LastSequence => Interlocked.Read(ref _lastSequence);

		public long ExpectedSequence => LastSequence + 1;

		public bool IsFailed { get; private set; }

		public string? FailureReason { get; private set; }

		public DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.UtcNow;

		public bool RecordMessage(string messageId, long? sequenceNumber)
		{
			lock (_lock)
			{
				LastActivity = DateTimeOffset.UtcNow;
				_ = Interlocked.Increment(ref _messageCount);

				if (!sequenceNumber.HasValue)
				{
					// No sequence tracking
					return true;
				}

				var inSequence = sequenceNumber.Value == ExpectedSequence ||
								 _lastSequence == -1; // First message

				if (inSequence)
				{
					_ = Interlocked.Exchange(ref _lastSequence, sequenceNumber.Value);
				}
				else
				{
					_ = Interlocked.Increment(ref _outOfSequenceCount);
				}

				return inSequence;
			}
		}

		public void MarkFailed(string reason)
		{
			lock (_lock)
			{
				IsFailed = true;
				FailureReason = reason;
				LastActivity = DateTimeOffset.UtcNow;
			}
		}

		public void Reset()
		{
			lock (_lock)
			{
				IsFailed = false;
				FailureReason = null;
				LastActivity = DateTimeOffset.UtcNow;
			}
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.OrderingManagerInitialized, LogLevel.Information,
		"Ordering key manager initialized with timeout {Timeout} and max keys {MaxKeys}")]
	private partial void LogManagerInitialized(TimeSpan timeout, int maxKeys);

	[LoggerMessage(GooglePubSubEventId.OutOfSequenceMessage, LogLevel.Warning,
		"Out-of-sequence message detected for ordering key {OrderingKey}. Expected: {Expected}, Received: {Received}")]
	private partial void LogOutOfSequenceMessage(string orderingKey, long? expected, long? received);

	[LoggerMessage(GooglePubSubEventId.OrderingKeyFailed, LogLevel.Warning,
		"Ordering key {OrderingKey} marked as failed: {Reason}")]
	private partial void LogOrderingKeyFailed(string orderingKey, string reason);

	[LoggerMessage(GooglePubSubEventId.OrderingKeyReset, LogLevel.Information,
		"Ordering key {OrderingKey} has been reset")]
	private partial void LogOrderingKeyReset(string orderingKey);

	[LoggerMessage(GooglePubSubEventId.OrderingKeyCleanupCompleted, LogLevel.Debug,
		"Cleaned up {Count} inactive ordering key states")]
	private partial void LogCleanupCompleted(int count);
}
