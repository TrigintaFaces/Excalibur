// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten.Diagnostics;

using global::Marten;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Marten (PostgreSQL document store) implementation of <see cref="IOutboxStore"/> and
/// <see cref="IOutboxStoreAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each operation composes a Marten <see cref="IDocumentSession"/> from the injected
/// <see cref="IDocumentStore"/> and commits it with <c>SaveChangesAsync</c>, so staging rides
/// Marten's own unit-of-work transaction.
/// </para>
/// <para>
/// Staging uses <c>IDocumentSession.Insert</c> — a real conditional write that fails when a message
/// with the same id already exists — rather than an upsert, so a concurrent duplicate is rejected
/// rather than silently overwriting the original (the exactly-once staging invariant).
/// </para>
/// </remarks>
public sealed partial class MartenOutboxStore : IOutboxStore, IOutboxStoreAdmin
{
	private readonly IDocumentStore _store;
	private readonly MartenOutboxStoreOptions _options;
	private readonly ILogger<MartenOutboxStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MartenOutboxStore"/> class.
	/// </summary>
	/// <param name="store"> The Marten document store. </param>
	/// <param name="options"> The outbox store options. </param>
	/// <param name="logger"> The logger instance. </param>
	public MartenOutboxStore(
		IDocumentStore store,
		IOptions<MartenOutboxStoreOptions> options,
		ILogger<MartenOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_store = store;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var document = MartenOutboxDocument.FromOutbound(message);

		await using var session = _store.LightweightSession();

		// Insert (NOT Store/upsert) so a duplicate id is a real conditional-write conflict, keeping
		// staging exactly-once. A concurrent duplicate surfaces as DocumentAlreadyExistsException.
		session.Insert(document);

		try
		{
			await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (global::JasperFx.DocumentAlreadyExistsException ex)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists in the outbox.", ex);
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);
	}

	/// <inheritdoc/>
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

#pragma warning disable IL2026, IL3050 // Reflection-based serialization; this package is not AOT-compatible.
		var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());
#pragma warning restore IL2026, IL3050
		var messageType = message.GetType().FullName ?? message.GetType().Name;

		// Canonical context->message factory so tenant/correlation/causation are never dropped.
		var outbound = OutboundMessage.FromContext(messageType, payload, messageType, context);

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var now = DateTimeOffset.UtcNow;

		await using var session = _store.QuerySession();

		var documents = await session.Query<MartenOutboxDocument>()
			.Where(d => d.Status == OutboxStatus.Staged && (d.ScheduledAt == null || d.ScheduledAt <= now))
			.OrderBy(d => d.Priority)
			.ThenBy(d => d.CreatedAt)
			.Take(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(static d => d.ToOutbound()).ToList();
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		await using var session = _store.LightweightSession();

		var document = await session.LoadAsync<MartenOutboxDocument>(messageId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Message with ID '{messageId}' not found.");

		if (document.Status == OutboxStatus.Sent)
		{
			throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
		}

		document.Status = OutboxStatus.Sent;
		document.SentAt = DateTimeOffset.UtcNow;
		document.LastError = null;

		session.Update(document);
		await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await using var session = _store.LightweightSession();

		var document = await session.LoadAsync<MartenOutboxDocument>(messageId, cancellationToken).ConfigureAwait(false);
		if (document is null)
		{
			// Mirror the conformance expectation: a missing message is a silent no-op on mark-failed.
			return;
		}

		document.Status = OutboxStatus.Failed;
		document.LastError = errorMessage;
		document.RetryCount = retryCount;
		document.LastAttemptAt = DateTimeOffset.UtcNow;

		session.Update(document);
		await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		await using var session = _store.QuerySession();

		var query = session.Query<MartenOutboxDocument>()
			.Where(d => d.Status == OutboxStatus.Failed);

		if (maxRetries > 0)
		{
			query = query.Where(d => d.RetryCount < maxRetries);
		}

		if (olderThan.HasValue)
		{
			var threshold = olderThan.Value;
			query = query.Where(d => d.LastAttemptAt < threshold);
		}

		var documents = await query
			.OrderBy(d => d.RetryCount)
			.ThenBy(d => d.LastAttemptAt)
			.Take(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(static d => d.ToOutbound()).ToList();
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		await using var session = _store.QuerySession();

		var documents = await session.Query<MartenOutboxDocument>()
			.Where(d => d.Status == OutboxStatus.Staged && d.ScheduledAt != null && d.ScheduledAt <= scheduledBefore)
			.OrderBy(d => d.ScheduledAt)
			.Take(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(static d => d.ToOutbound()).ToList();
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var limit = Math.Min(batchSize, _options.CleanupBatchSize);

		await using var session = _store.LightweightSession();

		var ids = await session.Query<MartenOutboxDocument>()
			.Where(d => d.Status == OutboxStatus.Sent && d.SentAt != null && d.SentAt < olderThan)
			.OrderBy(d => d.SentAt)
			.Take(limit)
			.Select(d => d.Id)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (ids.Count == 0)
		{
			LogMessagesCleanedUp(0, olderThan);
			return 0;
		}

		foreach (var id in ids)
		{
			session.Delete<MartenOutboxDocument>(id);
		}

		await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		LogMessagesCleanedUp(ids.Count, olderThan);
		return ids.Count;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		await using var session = _store.QuerySession();

		var staged = await session.Query<MartenOutboxDocument>()
			.CountAsync(d => d.Status == OutboxStatus.Staged, cancellationToken).ConfigureAwait(false);
		var sending = await session.Query<MartenOutboxDocument>()
			.CountAsync(d => d.Status == OutboxStatus.Sending, cancellationToken).ConfigureAwait(false);
		var sent = await session.Query<MartenOutboxDocument>()
			.CountAsync(d => d.Status == OutboxStatus.Sent, cancellationToken).ConfigureAwait(false);
		var failed = await session.Query<MartenOutboxDocument>()
			.CountAsync(d => d.Status == OutboxStatus.Failed, cancellationToken).ConfigureAwait(false);
		var scheduled = await session.Query<MartenOutboxDocument>()
			.CountAsync(d => d.Status == OutboxStatus.Staged && d.ScheduledAt != null, cancellationToken).ConfigureAwait(false);

		return new OutboxStatistics
		{
			StagedMessageCount = staged,
			SendingMessageCount = sending,
			SentMessageCount = sent,
			FailedMessageCount = failed,
			ScheduledMessageCount = scheduled,
			CapturedAt = now,
		};
	}

	[LoggerMessage(OutboxMartenEventId.OutboxMessageStaged, LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(OutboxMartenEventId.OutboxMessageEnqueued, LogLevel.Debug,
		"Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(OutboxMartenEventId.OutboxMessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(OutboxMartenEventId.OutboxMessageFailed, LogLevel.Warning,
		"Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(OutboxMartenEventId.OutboxMessagesCleanedUp, LogLevel.Information,
		"Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);
}
