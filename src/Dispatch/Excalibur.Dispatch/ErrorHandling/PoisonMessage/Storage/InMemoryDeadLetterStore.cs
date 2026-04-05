// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// In-memory implementation of the dead letter store for development and testing.
/// </summary>
public sealed partial class InMemoryDeadLetterStore : IDeadLetterStore, IDeadLetterStoreAdmin
{
	private readonly ConcurrentDictionary<string, DeadLetterMessage> _messages = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryDeadLetterStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDeadLetterStore" /> class.
	/// </summary>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public InMemoryDeadLetterStore(ILogger<InMemoryDeadLetterStore> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);
		_logger = logger;
	}

	/// <inheritdoc />
	public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		_messages[message.Id] = message;

		LogStoredDeadLetterMessage(message.MessageId, message.MessageType, message.Reason);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<DeadLetterMessage?> GetByIdAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		if (TryGetByMessageId(messageId, out var message))
		{
			return Task.FromResult((DeadLetterMessage?)message);
		}

		return Task.FromResult<DeadLetterMessage?>(null);
	}

	/// <inheritdoc />
	public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		if (filter.MaxResults <= 0)
		{
			return Task.FromResult<IEnumerable<DeadLetterMessage>>([]);
		}

		var skip = filter.Skip < 0 ? 0 : filter.Skip;
		var candidateCount = skip >= int.MaxValue - filter.MaxResults
			? int.MaxValue
			: skip + filter.MaxResults;
		var messageCount = _messages.Count;
		if (messageCount == 0)
		{
			return Task.FromResult<IEnumerable<DeadLetterMessage>>([]);
		}

		if (candidateCount > messageCount)
		{
			candidateCount = messageCount;
		}

		var newestMatches = new DeadLetterMessage[candidateCount];
		var newestCount = 0;
		var oldestIndex = 0;
		var oldestTicks = long.MaxValue;
		foreach (var message in _messages.Values)
		{
			if (!MatchesFilter(message, filter))
			{
				continue;
			}

			var priority = message.MovedToDeadLetterAt.UtcTicks;
			if (newestCount < candidateCount)
			{
				newestMatches[newestCount] = message;
				if (priority < oldestTicks)
				{
					oldestTicks = priority;
					oldestIndex = newestCount;
				}

				newestCount++;
				continue;
			}

			if (priority <= oldestTicks)
			{
				continue;
			}

			newestMatches[oldestIndex] = message;
			oldestTicks = newestMatches[0].MovedToDeadLetterAt.UtcTicks;
			oldestIndex = 0;
			for (var i = 1; i < newestCount; i++)
			{
				var candidateTicks = newestMatches[i].MovedToDeadLetterAt.UtcTicks;
				if (candidateTicks < oldestTicks)
				{
					oldestTicks = candidateTicks;
					oldestIndex = i;
				}
			}
		}

		if (newestCount == 0)
		{
			return Task.FromResult<IEnumerable<DeadLetterMessage>>([]);
		}

		Array.Sort(newestMatches, 0, newestCount, DeadLetterNewestFirstComparer.Instance);
		var results = SliceMessages(newestMatches, newestCount, skip, filter.MaxResults);

		return Task.FromResult<IEnumerable<DeadLetterMessage>>(results);
	}

	/// <inheritdoc />
	public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		if (TryGetByMessageId(messageId, out var message))
		{
			message.IsReplayed = true;
			message.ReplayedAt = DateTimeOffset.UtcNow;

			LogMarkedDeadLetterMessageAsReplayed(messageId);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		if (TryGetByMessageId(messageId, out var messageToDelete))
		{
			var removed = _messages.TryRemove(messageToDelete.Id, out _);
			if (removed)
			{
				LogDeletedDeadLetterMessage(messageId);
			}

			return Task.FromResult(removed);
		}

		return Task.FromResult(false);
	}

	/// <inheritdoc />
	public Task<long> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult((long)_messages.Count);

	/// <inheritdoc />
	public Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken)
	{
		var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);
		var messageIdsToRemove = new List<string>();
		foreach (var message in _messages.Values)
		{
			if (message.MovedToDeadLetterAt < cutoffDate)
			{
				messageIdsToRemove.Add(message.Id);
			}
		}

		var removedCount = 0;
		for (var i = 0; i < messageIdsToRemove.Count; i++)
		{
			if (_messages.TryRemove(messageIdsToRemove[i], out _))
			{
				removedCount++;
			}
		}

		if (removedCount > 0)
		{
			LogCleanedUpOldDeadLetterMessages(removedCount, retentionDays);
		}

		return Task.FromResult(removedCount);
	}

	private bool TryGetByMessageId(string messageId, out DeadLetterMessage message)
	{
		foreach (var candidate in _messages.Values)
		{
			if (string.Equals(candidate.MessageId, messageId, StringComparison.Ordinal))
			{
				message = candidate;
				return true;
			}
		}

		message = null!;
		return false;
	}

	private static bool MatchesFilter(DeadLetterMessage message, DeadLetterFilter filter)
	{
		if (!string.IsNullOrWhiteSpace(filter.MessageType) &&
		    !string.Equals(message.MessageType, filter.MessageType, StringComparison.Ordinal))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(filter.Reason) &&
		    !message.Reason.Contains(filter.Reason, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (filter.FromDate.HasValue &&
		    message.MovedToDeadLetterAt < filter.FromDate.Value)
		{
			return false;
		}

		if (filter.ToDate.HasValue &&
		    message.MovedToDeadLetterAt > filter.ToDate.Value)
		{
			return false;
		}

		if (filter.IsReplayed.HasValue &&
		    message.IsReplayed != filter.IsReplayed.Value)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceSystem) &&
		    !string.Equals(message.SourceSystem, filter.SourceSystem, StringComparison.Ordinal))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId) &&
		    !string.Equals(message.CorrelationId, filter.CorrelationId, StringComparison.Ordinal))
		{
			return false;
		}

		return true;
	}

	private static List<DeadLetterMessage> SliceMessages(
		DeadLetterMessage[] source,
		int sourceCount,
		int skip,
		int maxResults)
	{
		if (maxResults <= 0 || sourceCount == 0)
		{
			return [];
		}

		if (skip < 0)
		{
			skip = 0;
		}

		if (skip >= sourceCount)
		{
			return [];
		}

		var remainingCount = sourceCount - skip;
		var takeCount = maxResults < remainingCount ? maxResults : remainingCount;
		var result = new List<DeadLetterMessage>(takeCount);
		for (var i = 0; i < takeCount; i++)
		{
			result.Add(source[skip + i]);
		}

		return result;
	}

	private sealed class DeadLetterNewestFirstComparer : IComparer<DeadLetterMessage>
	{
		public static DeadLetterNewestFirstComparer Instance { get; } = new();

		public int Compare(DeadLetterMessage? left, DeadLetterMessage? right)
		{
			if (ReferenceEquals(left, right))
			{
				return 0;
			}

			if (left is null)
			{
				return 1;
			}

			if (right is null)
			{
				return -1;
			}

			return right.MovedToDeadLetterAt.CompareTo(left.MovedToDeadLetterAt);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.DeadLetterMessageAdded, LogLevel.Information,
		"Stored dead letter message '{MessageId}' of type '{MessageType}': {Reason}")]
	private partial void LogStoredDeadLetterMessage(string messageId, string messageType, string reason);

	[LoggerMessage(DeliveryEventId.DeadLetterMessageReplayed, LogLevel.Information,
		"Marked dead letter message '{MessageId}' as replayed")]
	private partial void LogMarkedDeadLetterMessageAsReplayed(string messageId);

	[LoggerMessage(DeliveryEventId.DeadLetterMessageRemoved, LogLevel.Information,
		"Deleted dead letter message '{MessageId}'")]
	private partial void LogDeletedDeadLetterMessage(string messageId);

	[LoggerMessage(DeliveryEventId.DeadLetterCleanupCompleted, LogLevel.Information,
		"Cleaned up {RemovedCount} old dead letter messages older than {RetentionDays} days")]
	private partial void LogCleanedUpOldDeadLetterMessages(int removedCount, int retentionDays);
}
