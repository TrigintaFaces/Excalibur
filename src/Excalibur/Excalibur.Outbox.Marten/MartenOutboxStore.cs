// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten.Diagnostics;

using global::Marten;

using Npgsql;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

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
/// <para>
/// <b>Tenancy: the discriminator is carried on the document, never applied as a filter.</b> Each staged
/// document records the tenant of the message it projects, and every drained message hands that value
/// back, so the owning tenant is re-established from the document rather than inferred from ambient
/// state. This store reads no ambient tenant on any path, and its declaration of
/// <see cref="ITenantPartitionedStore"/> states that mechanism explicitly.
/// </para>
/// <para>
/// The drain is deliberately estate-wide, and that is a requirement rather than an omission: one
/// dispatcher serves every tenant, so a drain narrowed to an ambient tenant would read it as absent,
/// claim the empty set, and stall delivery for all of them. The remaining statements load, update, and
/// delete a document by its identity, where a tenant term could not exclude a foreign document — the
/// identity already addresses at most one — and could only turn the correct document into none.
/// </para>
/// </remarks>
public sealed partial class MartenOutboxStore : IOutboxStore, IOutboxStoreAdmin, ITenantPartitionedStore, IDisposable
{
	private readonly IDocumentStore _store;
	private readonly MartenOutboxStoreOptions _options;
	private readonly ILogger<MartenOutboxStore> _logger;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Identifies this store instance as the holder of a claim.
	/// </summary>
	/// <remarks>
	/// Per instance, not per call: a claim belongs to the dispatcher that will do the sending, and one
	/// store instance is one dispatcher. Two instances — the multi-process case this claim exists for —
	/// get different ids and therefore never both hold the same message.
	/// </remarks>
	private readonly string _dispatcherId = Guid.NewGuid().ToString("N");

	private readonly SemaphoreSlim _claimsTableLock = new(1, 1);

	/// <summary>
	/// Initializes a new instance of the <see cref="MartenOutboxStore"/> class.
	/// </summary>
	/// <param name="store"> The Marten document store. </param>
	/// <param name="options"> The outbox store options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="timeProvider">
	/// The clock used for the instants this store records rather than compares — a send time, an attempt
	/// time, the moment a statistics snapshot was taken. Defaults to <see cref="TimeProvider.System"/>.
	/// <para>
	/// It is deliberately NOT the clock that decides claim eligibility. A lease is written by one machine
	/// and judged by another, so any predicate reading this clock would compare two unsynchronised ones;
	/// those instants come from <c>clock_timestamp()</c> inside the claim statement instead. Injecting the
	/// clock here is what lets a test drive this store's clock arbitrarily far from the database's and
	/// observe that the claim does not move — which is the property, stated as a test.
	/// </para>
	/// </param>
	public MartenOutboxStore(
		IDocumentStore store,
		IOptions<MartenOutboxStoreOptions> options,
		ILogger<MartenOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_store = store;
		_options = options.Value;
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
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
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var now = _timeProvider.GetUtcNow();

		await using var session = _store.LightweightSession();

		// Candidates, not yet a batch to dispatch. This read alone used to BE the drain, which meant two
		// dispatchers polling together saw the same rows and both sent them — every message delivered
		// twice for as long as more than one instance was running. The claim below is what narrows this
		// to the messages belonging to this dispatcher.
		// Staged (never attempted) and Failed (attempted, still below the retry ceiling) are both owed
		// delivery. Restricting this to Staged would withhold a failed message permanently, which looks
		// like a well-behaved backoff from the outside and is actually a silent drop. What keeps a failed
		// message from being retried immediately is the floor in the claim table, not its absence here.
		var candidates = await session.Query<MartenOutboxDocument>()
			.Where(d => (d.Status == OutboxStatus.Staged || d.Status == OutboxStatus.Failed)
				&& (d.ScheduledAt == null || d.ScheduledAt <= now))
			.OrderBy(d => d.Priority)
			.ThenBy(d => d.CreatedAt)
			.Take(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (candidates.Count == 0)
		{
			return [];
		}

		await using var connection = await OpenClaimConnectionAsync(cancellationToken).ConfigureAwait(false);

		var claimed = await MartenOutboxClaims.ClaimAsync(
			connection,
			_options.ClaimsSchemaName,
			_options.ClaimsTableName,
			[.. candidates.Select(static d => d.Id)],
			_dispatcherId,
			_options.ClaimTimeout,
			cancellationToken).ConfigureAwait(false);

		// Order is preserved from the candidate query, so a partially-claimed batch still leaves this
		// dispatcher's messages in priority and age order.
		return candidates
			.Where(d => claimed.Contains(d.Id))
			.Select(static d => d.ToOutbound())
			.ToList();
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		await using var session = _store.LightweightSession();

		var document = await session.LoadAsync<MartenOutboxDocument>(messageId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Message with ID '{messageId}' not found.");

		// The status read above cannot decide this. Every caller has its own session and a session applies
		// no optimistic concurrency, so concurrent callers all observe a not-yet-sent message and all
		// write it sent. The transition is arbitrated in the claim table instead, where exactly one caller
		// can win it, and the loser is told the message was already settled rather than settling it again.
		await using (var connection = await OpenClaimConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			var won = await MartenOutboxClaims.TrySettleAsync(
				connection,
				_options.ClaimsSchemaName,
				_options.ClaimsTableName,
				messageId,
				cancellationToken).ConfigureAwait(false);

			if (!won)
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
			}
		}

		document.Status = OutboxStatus.Sent;
		document.SentAt = _timeProvider.GetUtcNow();
		document.LastError = null;

		session.Update(document);
		await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// The claim row is deliberately NOT released here. It stays as the terminal tombstone that made
		// this transition single-winner; releasing it would let the next caller settle the same message
		// again. It is removed when the message itself is purged.
		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		// The claim release and the document update are ONE transaction on ONE connection, and the reason is
		// termination rather than tidiness. As two independent writes, a crash between them left the claim
		// released under a floor while the document still read Staged with its old attempt count. Once the
		// floor elapsed the message was claimed again, failed again, and recorded the same count again — and
		// because the dead-letter ceiling is driven by that count, the message was retried without end and
		// never dead-lettered. Every other provider performs this as a single statement; this one did not.
		//
		// Rolling back on any failure is the safe direction: nothing is released, so the message stays claimed
		// by this dispatcher and is retried when the claim ages out — with its attempt count intact.
		// Through the shared helper, not a bare CreateConnection: the helper also ensures the claim table
		// exists, which Marten does not manage and which a consumer or a test fixture may recreate under a
		// live store.
		await using var connection = await OpenClaimConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// The document write cannot arbitrate this. Every caller opens its own session and a session applies
		// no optimistic concurrency, so concurrent callers all read the message and all write it — last one
		// wins, on every field. The decision is taken in the claim table instead, where one conditional
		// statement both checks that this dispatcher owns the claim and releases it under a floor. A caller
		// that does not own the claim, or whose message is already settled, is told so here and writes nothing.
		var entitled = await MartenOutboxClaims.TryRecordFailureAsync(
			connection,
			transaction,
			_options.ClaimsSchemaName,
			_options.ClaimsTableName,
			messageId,
			_dispatcherId,
			TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds),
			cancellationToken).ConfigureAwait(false);

		if (!entitled)
		{
			// Silent, like the missing-message case below. The report is stale rather than erroneous: this
			// dispatcher lost the claim, and the message belongs to whoever holds it now.
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		// Enlisted on the transaction above rather than opening its own: Marten writes through it and leaves
		// the commit to us, which is what lets both writes land together. This is the store's documented
		// seam for joining an existing transaction, so no part of the unit of work is hand-rolled.
		//
		// Note this does NOT contradict the drain's rule that a claim must not ride the session's unit of
		// work. That rule exists because a read-only drain disposes its session without saving, which would
		// roll a claim back; this path always reaches a decision — commit or rollback — so there is no
		// session lifetime for the claim to be lost to.
		await using var session = _store.LightweightSession(global::Marten.Services.SessionOptions.ForTransaction(transaction));

		var document = await session.LoadAsync<MartenOutboxDocument>(messageId, cancellationToken).ConfigureAwait(false);
		if (document is null)
		{
			// Mirror the conformance expectation: a missing message is a silent no-op on mark-failed. The
			// claim release is rolled back with it, so the two stay consistent.
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		document.Status = OutboxStatus.Failed;
		document.LastError = errorMessage;

		// The recorded count never decreases. The retry ceiling that eventually gives up on a message is
		// driven by it, so a late report carrying a lower number would push that ceiling further away
		// every time it arrived, and the message would be retried without end.
		if (retryCount > document.RetryCount)
		{
			document.RetryCount = retryCount;
		}

		document.LastAttemptAt = _timeProvider.GetUtcNow();

		session.Update(document);
		await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// The single commit. Before it, neither write is visible; after it, both are.
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
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
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
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

		// Purging the messages is what bounds the claim table. A settled message keeps its claim row as
		// the terminal tombstone that made its transition single-winner, so nothing else removes it: the
		// row goes when the message goes, and skipping this would grow the table by one row per message
		// ever sent.
		await using (var connection = await OpenClaimConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			await MartenOutboxClaims.PurgeAsync(
				connection,
				_options.ClaimsSchemaName,
				_options.ClaimsTableName,
				ids,
				cancellationToken).ConfigureAwait(false);
		}

		LogMessagesCleanedUp(ids.Count, olderThan);
		return ids.Count;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		var now = _timeProvider.GetUtcNow();

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

	/// <summary>
	/// Releases the lock guarding one-time creation of the claim table.
	/// </summary>
	/// <remarks>
	/// The Marten <c>IDocumentStore</c> is supplied by the consumer and stays theirs to dispose; the only
	/// thing this store owns is the lock.
	/// </remarks>
	public void Dispose() => _claimsTableLock.Dispose();

	/// <summary>
	/// Opens a connection to the Marten database with the claim table present.
	/// </summary>
	/// <remarks>
	/// A SEPARATE connection built by Marten's own factory, never the session's, so a claim does not ride
	/// a unit of work that a read-only drain discards. The claim table is ensured on every call rather
	/// than once per store instance: Marten manages the schema and a consumer or test fixture may recreate
	/// it under a live store, after which a cached "already created" is simply wrong. The body carries the
	/// full reasoning for both choices.
	/// </remarks>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The open connection. The caller disposes it.</returns>
	private async Task<NpgsqlConnection> OpenClaimConnectionAsync(CancellationToken cancellationToken)
	{
		// A SEPARATE connection, built by Marten's own factory, and the caller disposes it.
		//
		// The claim must not ride the session's unit of work: Marten owns that transaction and rolls it
		// back when a session is disposed without SaveChanges, which is exactly what a read-only drain
		// does. Claims written on the session's connection vanished with it, so every claimer saw an
		// empty claim table and two of them won the same messages — the defect this exists to close,
		// reproduced through the mechanism meant to prevent it. Autocommit per statement is what makes a
		// claim durable the instant it is taken, which is the only sense of "claimed" that means anything
		// to a second dispatcher polling concurrently.
		//
		// Marten's factory, not the session connection's ConnectionString: Npgsql strips the password
		// from that string once the connection is open, so a connection built from it cannot authenticate.
		var connection = _store.Options.Tenancy.Default.Database.CreateConnection();

		try
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await connection.DisposeAsync().ConfigureAwait(false);
			throw;
		}

		// Ensured on every use rather than cached behind a flag. The flag would be a statement about a
		// database this store does not own: Marten manages the schema, and a consumer (or a test fixture)
		// may recreate it underneath a live store, after which a cached "already created" is simply wrong
		// and every claim fails with an undefined-table error. CREATE TABLE IF NOT EXISTS is one cheap
		// statement per drain and cannot go stale.
		await _claimsTableLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await MartenOutboxClaims.EnsureTableAsync(
				connection, _options.ClaimsSchemaName, _options.ClaimsTableName, cancellationToken)
				.ConfigureAwait(false);
		}
		finally
		{
			_ = _claimsTableLock.Release();
		}

		return connection;
	}

	/// <summary>
	/// Releases a message's claim, if the claim table has been created.
	/// </summary>
	/// <param name="session">The session whose connection to use.</param>
	/// <param name="messageId">The message whose claim is released.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	private async Task ReleaseClaimAsync(
		IDocumentSession session,
		string messageId,
		CancellationToken cancellationToken)
	{
		await using var connection = await OpenClaimConnectionAsync(cancellationToken).ConfigureAwait(false);

		await MartenOutboxClaims.ReleaseAsync(
			connection, _options.ClaimsSchemaName, _options.ClaimsTableName, messageId, cancellationToken)
			.ConfigureAwait(false);
	}
}
