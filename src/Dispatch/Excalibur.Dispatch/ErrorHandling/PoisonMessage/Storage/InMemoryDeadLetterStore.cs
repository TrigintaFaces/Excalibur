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
	private readonly ConcurrentDictionary<string, StoredEntry> _messages = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryDeadLetterStore> _logger;
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDeadLetterStore" /> class.
	/// </summary>
	/// <param name="tenantContext"> The ambient tenant, or <see langword="null" /> in a single-tenant host. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public InMemoryDeadLetterStore(ITenantContext? tenantContext, ILogger<InMemoryDeadLetterStore> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);

		// Optional by construction: a single-tenant host registers no ITenantContext, which resolves to the
		// untenanted partition — a concrete term, so entries are never stored or read unscoped.
		_tenantContext = tenantContext;
		_logger = logger;
	}

	/// <summary>
	/// Gets the tenant term this store reads and writes under: the ambient tenant, or the reserved
	/// untenanted sentinel when no tenant is resolved.
	/// </summary>
	/// <remarks>
	/// This is the default <see cref="IDeadLetterStore" /> — the one a consumer gets without choosing a
	/// provider — so an unscoped implementation here is the most likely to be running in practice. A
	/// dead-letter entry holds the failed message body, so cross-tenant reads disclose message content.
	/// </remarks>
	private string CurrentTenantTerm =>
		KeyedTenantPartition.FromScope(TenantScope.FromContext(_tenantContext)).TenantId;

	/// <inheritdoc />
	public Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// The owning tenant is captured from AMBIENT CONTEXT at write time and held beside the message
		// rather than on it: DeadLetterMessage is caller-supplied, so a tenant carried on the DTO could
		// name someone else's.
		_messages[message.Id] = new StoredEntry(CurrentTenantTerm, message);

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
		var tenantId = CurrentTenantTerm;
		foreach (var entry in _messages.Values)
		{
			// Tenant first: a filter that specifies nothing must still return only the caller's own
			// entries, never the whole estate.
			if (!string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal))
			{
				continue;
			}

			var message = entry.Message;
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
	public Task<long> GetCountAsync(CancellationToken cancellationToken)
	{
		// Scoped like every other read: an estate-wide total tells one tenant how many failures every
		// other tenant has, which is an inference channel even though no message body is returned.
		var tenantId = CurrentTenantTerm;
		var count = 0L;
		foreach (var entry in _messages.Values)
		{
			if (string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal))
			{
				count++;
			}
		}

		return Task.FromResult(count);
	}

	/// <inheritdoc />
	public Task<int> CleanupOldMessagesAsync(int retentionDays, CancellationToken cancellationToken)
	{
		var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);
		var tenantId = CurrentTenantTerm;
		var messageIdsToRemove = new List<string>();
		foreach (var entry in _messages.Values)
		{
			// A destructive operation is scoped hardest: without the tenant term one tenant's cleanup would
			// delete another tenant's un-replayed entries, which is silent data loss rather than disclosure.
			if (string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)
				&& entry.Message.MovedToDeadLetterAt < cutoffDate)
			{
				messageIdsToRemove.Add(entry.Message.Id);
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

	/// <summary>
	/// Resolves a message by its identifier <em>within the caller's tenant</em>.
	/// </summary>
	/// <remarks>
	/// This is the single lookup chokepoint for reading, replaying and deleting, so scoping it here scopes
	/// all three: an identifier belonging to another tenant is indistinguishable from one that does not
	/// exist, which is the behaviour that keeps the caller from learning anything about another tenant's
	/// entries by probing.
	/// </remarks>
	private bool TryGetByMessageId(string messageId, out DeadLetterMessage message)
	{
		var tenantId = CurrentTenantTerm;
		foreach (var candidate in _messages.Values)
		{
			if (string.Equals(candidate.TenantId, tenantId, StringComparison.Ordinal)
				&& string.Equals(candidate.Message.MessageId, messageId, StringComparison.Ordinal))
			{
				message = candidate.Message;
				return true;
			}
		}

		message = null!;
		return false;
	}

	/// <summary>
	/// A stored entry: the message together with the tenant that owned the write.
	/// </summary>
	/// <param name="TenantId"> The tenant term captured when the entry was stored. </param>
	/// <param name="Message"> The dead-lettered message. </param>
	private sealed record StoredEntry(string TenantId, DeadLetterMessage Message);

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
