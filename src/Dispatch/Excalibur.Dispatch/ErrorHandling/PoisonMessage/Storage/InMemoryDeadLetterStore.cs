// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// In-memory implementation of the dead letter store for development and testing.
/// </summary>
public sealed partial class InMemoryDeadLetterStore : IDeadLetterStore
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

		var message = _messages.Values.FirstOrDefault(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));
		return Task.FromResult(message);
	}

	/// <inheritdoc />
	public Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(
		DeadLetterFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		var query = _messages.Values.AsEnumerable();

		if (!string.IsNullOrWhiteSpace(filter.MessageType))
		{
			query = query.Where(m => string.Equals(m.MessageType, filter.MessageType, StringComparison.Ordinal));
		}

		if (!string.IsNullOrWhiteSpace(filter.Reason))
		{
			query = query.Where(m => m.Reason.Contains(filter.Reason, StringComparison.OrdinalIgnoreCase));
		}

		if (filter.FromDate.HasValue)
		{
			query = query.Where(m => m.MovedToDeadLetterAt >= filter.FromDate.Value);
		}

		if (filter.ToDate.HasValue)
		{
			query = query.Where(m => m.MovedToDeadLetterAt <= filter.ToDate.Value);
		}

		if (filter.IsReplayed.HasValue)
		{
			query = query.Where(m => m.IsReplayed == filter.IsReplayed.Value);
		}

		if (!string.IsNullOrWhiteSpace(filter.SourceSystem))
		{
			query = query.Where(m => string.Equals(m.SourceSystem, filter.SourceSystem, StringComparison.Ordinal));
		}

		if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
		{
			query = query.Where(m => string.Equals(m.CorrelationId, filter.CorrelationId, StringComparison.Ordinal));
		}

		var results = query
			.OrderByDescending(m => m.MovedToDeadLetterAt)
			.Skip(filter.Skip)
			.Take(filter.MaxResults)
			.ToList();

		return Task.FromResult<IEnumerable<DeadLetterMessage>>(results);
	}

	/// <inheritdoc />
	public Task MarkAsReplayedAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var message = _messages.Values.FirstOrDefault(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));
		if (message != null)
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

		var messageToDelete = _messages.Values.FirstOrDefault(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));
		if (messageToDelete != null)
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
		var messagesToRemove = _messages.Values
			.Where(m => m.MovedToDeadLetterAt < cutoffDate)
			.ToList();

		var removedCount = 0;
		foreach (var message in messagesToRemove)
		{
			if (_messages.TryRemove(message.Id, out _))
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
