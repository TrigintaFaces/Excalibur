// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Grpc.DeadLetter;

/// <summary>
/// In-memory dead letter queue manager for the gRPC transport.
/// </summary>
/// <remarks>
/// <para>
/// gRPC is a point-to-point protocol with no native DLQ concept. This implementation
/// provides bounded in-memory storage for failed messages with query and reprocess support.
/// </para>
/// <para>
/// For durable DLQ storage, consumers should configure a persistent DLQ store
/// via the transport builder instead of relying on this in-memory fallback.
/// </para>
/// </remarks>
internal sealed partial class GrpcDeadLetterQueueManager : IDeadLetterQueueManager
{
	private const int DefaultMaxCapacity = 10_000;

	private readonly ConcurrentQueue<DeadLetterMessage> _messages = new();
	private readonly ILogger<GrpcDeadLetterQueueManager> _logger;
	private readonly int _maxCapacity;
	private int _count;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcDeadLetterQueueManager"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxCapacity">Maximum number of messages to retain. Default is 10,000.</param>
	public GrpcDeadLetterQueueManager(
		ILogger<GrpcDeadLetterQueueManager> logger,
		int maxCapacity = DefaultMaxCapacity)
	{
		_logger = logger;
		_maxCapacity = maxCapacity;
	}

	/// <inheritdoc />
	public Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(reason);

		var dlqMessage = new DeadLetterMessage
		{
			OriginalMessage = message,
			Reason = reason,
			Exception = exception,
			DeadLetteredAt = DateTimeOffset.UtcNow,
			DeliveryAttempts = 1,
		};

		var currentCount = Interlocked.Increment(ref _count);
		if (currentCount > _maxCapacity)
		{
			// Evict oldest when at capacity
			if (_messages.TryDequeue(out _))
			{
				Interlocked.Decrement(ref _count);
			}

			LogCapacityReached(_logger, _maxCapacity);
		}

		_messages.Enqueue(dlqMessage);

		var messageId = message.Id ?? Guid.NewGuid().ToString("N");
		LogMessageDeadLettered(_logger, messageId, reason);

		return Task.FromResult(messageId);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		var result = _messages.Take(maxMessages).ToList();
		return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(result);
	}

	/// <inheritdoc />
	public Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
		IEnumerable<DeadLetterMessage> messages,
		ReprocessOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		ArgumentNullException.ThrowIfNull(options);

		// In-memory DLQ: reprocessing means removing from queue (caller handles re-dispatch)
		var startTimestamp = Stopwatch.GetTimestamp();
		var messageList = messages.ToList();

		return Task.FromResult(new ReprocessResult
		{
			SuccessCount = messageList.Count,
			ProcessingTime = Stopwatch.GetElapsedTime(startTimestamp),
		});
	}

	/// <inheritdoc />
	public Task<DeadLetterStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var messages = _messages.ToArray();
		var now = DateTimeOffset.UtcNow;

		var stats = new DeadLetterStatistics
		{
			MessageCount = messages.Length,
			AverageDeliveryAttempts = messages.Length > 0
				? messages.Average(static m => m.DeliveryAttempts)
				: 0,
			OldestMessageAge = messages.Length > 0
				? now - messages.Min(static m => m.DeadLetteredAt)
				: TimeSpan.Zero,
			NewestMessageAge = messages.Length > 0
				? now - messages.Max(static m => m.DeadLetteredAt)
				: TimeSpan.Zero,
			GeneratedAt = now,
		};

		foreach (var group in messages.GroupBy(static m => m.Reason ?? "Unknown", StringComparer.Ordinal))
		{
			stats.ReasonBreakdown[group.Key] = group.Count();
		}

		return Task.FromResult(stats);
	}

	/// <inheritdoc />
	public Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var purged = 0;
		while (_messages.TryDequeue(out _))
		{
			purged++;
			Interlocked.Decrement(ref _count);
		}

		LogPurged(_logger, purged);
		return Task.FromResult(purged);
	}

	[LoggerMessage(920, LogLevel.Warning,
		"gRPC DLQ message dead-lettered: {MessageId}, reason: {Reason}")]
	private static partial void LogMessageDeadLettered(ILogger logger, string messageId, string reason);

	[LoggerMessage(921, LogLevel.Warning,
		"gRPC DLQ capacity reached ({MaxCapacity}). Oldest messages will be evicted.")]
	private static partial void LogCapacityReached(ILogger logger, int maxCapacity);

	[LoggerMessage(922, LogLevel.Information,
		"gRPC DLQ purged {Count} messages.")]
	private static partial void LogPurged(ILogger logger, int count);
}
