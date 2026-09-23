// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Text.Json;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Metadata;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Outbox.MongoDB;

/// <summary>
/// MongoDB-based implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses FindOneAndUpdate with status filter for atomic status transitions.
/// This prevents race conditions in MarkSentAsync by ensuring the status
/// check and update happen atomically in a single database operation.
/// </para>
/// <para>
/// Messages are indexed by status, priority, and scheduling for efficient queries.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "Store class coordinates the MongoDB driver, outbox document mapping, and dispatch metadata/context extraction by design (parity with SqlServerOutboxStore).")]
public sealed partial class MongoDbOutboxStore : IOutboxStore, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IBackoffSchedulableOutboxStore, IAsyncDisposable, ITenantPartitionedStore
{
	private readonly MongoDbOutboxOptions _options;
	private readonly ILogger<MongoDbOutboxStore> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbOutboxDocument>? _collection;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	// R1 at-least-once: a plainly-failed message stays re-claimable once its NextAttemptAt
	// floor elapses, mirroring the SqlServer claim predicate (Status IN Staged/Failed/PartiallyFailed).
	// DeadLettered (5) is terminal and deliberately excluded. Static field (not an inline array arg) per CA1861.
	private static readonly int[] ReclaimableStatuses =
		[(int)OutboxStatus.Staged, (int)OutboxStatus.Failed, (int)OutboxStatus.PartiallyFailed];

	// Sent and DeadLettered are TERMINAL: no completion may move a message out of either, and that holds
	// for the CURRENT claim holder as much as a superseded one. Excluding only Sent is insufficient --
	// returning a dead-lettered message to the failed set makes it claimable again, so a message we
	// decided to stop delivering is delivered after all.
	private static readonly int[] TerminalStatuses =
		[(int)OutboxStatus.Sent, (int)OutboxStatus.DeadLettered];

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider used for backoff/claim-gate decisions. Defaults to <see cref="TimeProvider.System"/>.</param>
	public MongoDbOutboxStore(
		IOptions<MongoDbOutboxOptions> options,
		ILogger<MongoDbOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">The time provider used for backoff/claim-gate decisions. Defaults to <see cref="TimeProvider.System"/>.</param>
	public MongoDbOutboxStore(
		IMongoClient client,
		IOptions<MongoDbOutboxOptions> options,
		ILogger<MongoDbOutboxStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<MongoDbOutboxDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = MongoDbOutboxDocument.FromOutboundMessage(message);

		try
		{
			await _collection!.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogMessageStaged(message.Id, message.MessageType, message.Destination);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists in the outbox.", ex);
		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
#pragma warning disable IL2026, IL3050 // AOT: MongoDB provider uses reflection-based JSON serialization
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), EventSerializationDefaults.Canonical);
#pragma warning restore IL2026, IL3050

		// honor a consumer-set routing destination (TransactionalOutboxWriter.SetDestination →
		// context) rather than persisting the message type name as the destination — parity with the
		// SQL/Postgres outbox stores. Falls back to the namespace-qualified type name (FullName, not
		// the bare Name) when no destination was set: two message types with the same short name in
		// different namespaces must not collapse to one destination. Reuses messageType above -- same value.
		var destination = context.ExtractMetadata().GetDestination() ?? messageType;
		var outbound = OutboundMessage.FromContext(messageType, payload, destination, context);

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		GetUnsentMessagesCoreAsync(batchSize, cancellationToken);


	private async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesCoreAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var leaseTimeoutMs = (long)TimeSpan.FromSeconds(_options.LeaseTimeoutSeconds).TotalMilliseconds;

		// Atomically claim up to batchSize staged documents via a per-document FindOneAndUpdate loop.
		// Each FindOneAndUpdate is atomic on the server, so two concurrent pollers can never claim the
		// same document — this mirrors the SQL Server UPDATE...OUTPUT lease-claim contract (lease columns
		// while the status remains Staged, stale-lease reclamation for crash recovery).
		var sort = Builders<MongoDbOutboxDocument>.Sort
			.Ascending(d => d.Priority)
			.Ascending(d => d.CreatedAt);

		// Per-message lease claim — the hard at-most-once.
		//
		// The lease instant is stamped by the SERVER ($$NOW), which is why this is an aggregation pipeline
		// rather than the update builder. A lease is written by one dispatcher and judged by another, so a
		// lease stamped from this process's clock and a cutoff computed from another's compare two machines
		// that have no reason to agree. Where they disagree by more than the lease timeout, the second
		// dispatcher reads a live lease as expired and claims a message the first is still delivering — and
		// the atomic FindOneAndUpdate does not prevent it, because the two are not racing: the second write
		// is the only one at that instant and succeeds on a predicate that was already wrong. Reading and
		// writing the lease from the one clock that both dispatchers share removes the disagreement.
		var claimSetFields = new BsonDocument
		{
			{ "leasedAt", "$$NOW" },

			// $literal, because inside an aggregation stage a string beginning with '$' is read as
			// a field path rather than as the value it plainly is.
			{ "leasedBy", new BsonDocument("$literal", _options.ProcessorId) },
		};

		var claimUpdate = new PipelineUpdateDefinition<MongoDbOutboxDocument>(
			PipelineDefinition<MongoDbOutboxDocument, MongoDbOutboxDocument>.Create(
				new BsonDocument("$set", claimSetFields)));

		var claimOptions = new FindOneAndUpdateOptions<MongoDbOutboxDocument>
		{
			Sort = sort,
			ReturnDocument = ReturnDocument.After
		};

		var claimed = new List<MongoDbOutboxDocument>(batchSize);

		for (var i = 0; i < batchSize; i++)
		{
			// Get staged messages that are (a) not scheduled or scheduled for now, (b) not held by a
			// failure-backoff gate or whose backoff has elapsed (NextAttemptAt is the dedicated
			// per-message backoff gate, distinct from the ScheduledAt send-time), and (c) not currently
			// leased by another poller, or whose lease has gone stale (crash recovery).
			// Every instant compared here is the server's, via $$NOW. $expr is what allows it: a plain
			// field-to-value filter can only compare against a value this process computed, which is the
			// clock that must not decide this. A field that is absent rather than null still compares
			// equal to null under $expr, so a document that never carried the field is treated as
			// ungated exactly as before.
			var claimFilter = new BsonDocumentFilterDefinition<MongoDbOutboxDocument>(new BsonDocument
			{
				{ "status", new BsonDocument("$in", new BsonArray(ReclaimableStatuses)) },
				{
					"$expr", new BsonDocument("$and", new BsonArray
					{
						// Due: unscheduled, or its requested send time has arrived on the server's clock.
						new BsonDocument("$or", new BsonArray
						{
							new BsonDocument("$eq", new BsonArray { StoredInstant("scheduledAt"), BsonNull.Value }),
							new BsonDocument("$lte", new BsonArray { StoredInstant("scheduledAt"), "$$NOW" }),
						}),

						// Past its failure floor: never failed, or the backoff stamped at the failure has
						// elapsed.
						new BsonDocument("$or", new BsonArray
						{
							new BsonDocument("$eq", new BsonArray { StoredInstant("nextAttemptAt"), BsonNull.Value }),
							new BsonDocument("$lte", new BsonArray { StoredInstant("nextAttemptAt"), "$$NOW" }),
						}),

						// Claimable: never leased, or the previous holder's lease has expired. This is the
						// term the whole server-clock arrangement exists for.
						new BsonDocument("$or", new BsonArray
						{
							new BsonDocument("$eq", new BsonArray { StoredInstant("leasedAt"), BsonNull.Value }),
							new BsonDocument("$lt", new BsonArray
							{
								StoredInstant("leasedAt"),
								new BsonDocument("$subtract", new BsonArray { "$$NOW", leaseTimeoutMs }),
							}),
						}),
					})
				},
			});

			var claimedDocument = await _collection!
				.FindOneAndUpdateAsync(claimFilter, claimUpdate, claimOptions, cancellationToken)
				.ConfigureAwait(false);

			if (claimedDocument == null)
			{
				break;
			}

			claimed.Add(claimedDocument);
		}

		return claimed.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
		MarkSentCoreAsync(messageId, cancellationToken);


	private async ValueTask MarkSentCoreAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbOutboxDocument>.Filter;
		var now = _timeProvider.GetUtcNow();

		// Use FindOneAndUpdate with status filter for atomic transition
		// This ensures no race condition: only one caller can successfully transition the status
		// We use Nin(TerminalStatuses) so that a message in EITHER terminal state -- Sent or
		// DeadLettered -- cannot be reopened by a late completion. Excluding only Sent would let a
		// completion that arrives after the message was dead-lettered record it as delivered.
		var atomicFilter = filter.And(
			filter.Eq(d => d.Id, messageId),
			filter.Nin(d => d.Status, TerminalStatuses));

		var update = Builders<MongoDbOutboxDocument>.Update
			.Set(d => d.Status, (int)OutboxStatus.Sent)
			.Set(d => d.SentAt, now)
			.Set(d => d.LastError, null)
			.Set(d => d.LeasedAt, null)
			.Set(d => d.LeasedBy, null);

		var result = await _collection!.FindOneAndUpdateAsync(
			atomicFilter,
			update,
			new FindOneAndUpdateOptions<MongoDbOutboxDocument> { ReturnDocument = ReturnDocument.Before },
			cancellationToken).ConfigureAwait(false);

		// If result is null, the message doesn't exist or is already in a terminal state.
		if (result == null)
		{
			var current = await _collection!
				.Find(filter.Eq(d => d.Id, messageId))
				.FirstOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false)
				?? throw new InvalidOperationException($"Message with ID '{messageId}' not found.");

			if (current.Status == (int)OutboxStatus.Sent)
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
			}

			// The message exists and is not sent, so the status predicate alone explains the miss: its status
			// changed concurrently. Kept distinct from "already marked as sent" so the two are not conflated.
			throw new InvalidOperationException($"Message with ID '{messageId}' could not be marked sent (status changed concurrently).");
		}

		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;

		// R1+R2+R3 as ONE atomic conditional write (SA non-negotiable #1/#2): the R2 ownership guard lives IN
		// the FindOneAndUpdate filter — only the current lease owner (or an already-released row) transitions,
		// so there is no read→check→write TOCTOU (the no-op class). A no-match (message absent OR owned by
		// a peer) returns null = a silent no-op, which both preserves the "silent on missing" conformance
		// contract and makes a stale processor's mark-fail a safe no-op rather than a lost update.
		var ownedFilter = filterBuilder.And(
			filterBuilder.Eq(d => d.Id, messageId),
			// Terminal-status exclusion, in the SAME atomic write as the mutation. Needs no ownership
			// evidence and is therefore correct on every store whether or not fencing is configured.
			filterBuilder.Nin(d => d.Status, TerminalStatuses),
			filterBuilder.Or(
				filterBuilder.Eq(d => d.LeasedBy, null),
				filterBuilder.Eq(d => d.LeasedBy, _options.ProcessorId)));

		// R1 floor: the message stays re-claimable (Failed is in the claim predicate) but its NextAttemptAt is
		// gated to now + F, so the plain path can neither hot-loop the drain (F > poll interval) nor drop the
		// message (at-least-once). R3: RetryCount is monotonic via $max — a stale late writer must never lower
		// it and weaken the DLQ-ceiling termination guarantee. The lease is released on the terminal-ish write.
		// The floor is measured from the SERVER's clock, because the claim predicate that reads it back is
		// evaluated there too (see GetUnsentMessagesCoreAsync). Stamping it from this process's clock and
		// judging it from another's would expire the backoff early or late by exactly however much the two
		// machines disagree — the same defect as the lease, on the retry gate instead of the claim.
		//
		// $max keeps RetryCount monotonic so a stale late writer cannot lower it and weaken the DLQ ceiling;
		// a document that has no retryCount yet yields the supplied value, as the builder form did.
		var floorMs = (long)TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds).TotalMilliseconds;
		var update = new PipelineUpdateDefinition<MongoDbOutboxDocument>(
			PipelineDefinition<MongoDbOutboxDocument, MongoDbOutboxDocument>.Create(
				new BsonDocument("$set", new BsonDocument
				{
					{ "status", (int)OutboxStatus.Failed },
					{ "lastError", new BsonDocument("$literal", errorMessage) },
					{ "retryCount", new BsonDocument("$max", new BsonArray { "$retryCount", retryCount }) },
					{ "lastAttemptAt", "$$NOW" },
					{ "nextAttemptAt", new BsonDocument("$add", new BsonArray { "$$NOW", floorMs }) },
					{ "leasedAt", BsonNull.Value },
					{ "leasedBy", BsonNull.Value },
				})));

		_ = await _collection!.FindOneAndUpdateAsync(
			ownedFilter, update,
			new FindOneAndUpdateOptions<MongoDbOutboxDocument> { ReturnDocument = ReturnDocument.After },
			cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var now = _timeProvider.GetUtcNow();

		// R2 dispatcher-ownership guard IN the atomic write condition (SA #1/#2): only the current lease owner
		// (or an already-released row) may reschedule — one FindOneAndUpdate, no read→check→write TOCTOU. A
		// no-match (absent OR peer-owned) returns null = silent no-op (conformance parity with MarkFailedAsync).
		var ownedFilter = filterBuilder.And(
			filterBuilder.Eq(d => d.Id, messageId),
			// Terminal exclusion, parity with MarkFailedAsync. The BACKOFF completion is a third path to the
			// same resurrection and was missed when the other two were guarded.
			filterBuilder.Nin(d => d.Status, TerminalStatuses),
			filterBuilder.Or(
				filterBuilder.Eq(d => d.LeasedBy, null),
				filterBuilder.Eq(d => d.LeasedBy, _options.ProcessorId)));

		// Record the failure and persist the backoff gate, keeping the message re-claimable (Staged) but
		// excluded by the claim's NextAttemptAt gate until the computed backoff has elapsed —
		// so the exponential backoff genuinely throttles re-delivery rather than the coarse claim cadence.
		// The lease is cleared here (not just left to expire) so re-staging for retry does not accidentally
		// gate the message behind the (unrelated, and typically longer) lease timeout as well.
		// R3: RetryCount is monotonic via $max — a stale late writer must never lower it.
		// The persisted gate is the LATER of the caller's computed schedule and the configured floor F, never
		// whichever the caller supplied. Binding the caller's instant alone was the defect: the backoff
		// calculator yields about a second at the first attempt, so a consumer who configured a floor of five
		// minutes was retried a second later — and because the processor PREFERS this path wherever the
		// capability is advertised, the identical failure waited F WITHOUT the capability and ignored it WITH
		// one, so the capability weakened the guarantee it exists to strengthen.
		//
		// Composed with a maximum, so relaxing the floor below F is not something ordinary use can express —
		// it takes inverting this one comparison.
		//
		// The gate is applied as a DELAY from the server's clock rather than as an absolute instant. The
		// caller hands us an absolute time computed from ITS clock, and the claim predicate reads the stored
		// gate back on the SERVER's; persisting the caller's instant compares two machines that need not
		// agree. A duration carries no clock, so converting the caller's instant to "how long from now" here
		// and re-anchoring it to $$NOW at the server preserves the caller's intent exactly while leaving one
		// clock in the comparison. The maximum is taken over the two DURATIONS, which is the same choice as
		// taking it over the two instants — gate - now == max(nextAttemptAt - now, F).
		var floorMs = (long)TimeSpan.FromSeconds(_options.FailureBackoffFloorSeconds).TotalMilliseconds;
		var callerDelayMs = (long)(nextAttemptAt - now).TotalMilliseconds;
		var gateMs = callerDelayMs > floorMs ? callerDelayMs : floorMs;

		var update = new PipelineUpdateDefinition<MongoDbOutboxDocument>(
			PipelineDefinition<MongoDbOutboxDocument, MongoDbOutboxDocument>.Create(
				new BsonDocument("$set", new BsonDocument
				{
					{ "status", (int)OutboxStatus.Staged },
					{ "nextAttemptAt", new BsonDocument("$add", new BsonArray { "$$NOW", gateMs }) },
					{ "lastError", new BsonDocument("$literal", errorMessage) },
					{ "retryCount", new BsonDocument("$max", new BsonArray { "$retryCount", retryCount }) },
					{ "lastAttemptAt", "$$NOW" },
					{ "leasedAt", BsonNull.Value },
					{ "leasedBy", BsonNull.Value },
				})));

		_ = await _collection!.FindOneAndUpdateAsync(
			ownedFilter, update,
			new FindOneAndUpdateOptions<MongoDbOutboxDocument> { ReturnDocument = ReturnDocument.After },
			cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Terminal exclusion in the SAME conditional write. Without it a dead-letter can be applied to an
		// already-sent message (recording a delivery failure that did not happen) or repeated on an
		// already-dead-lettered one (a second dead-letter row for one message, so either redrive duplicates
		// it). Neither depends on WHO reports: this binds the current claim holder too.
		var filter = Builders<MongoDbOutboxDocument>.Filter.And(
			Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.Id, messageId),
			Builders<MongoDbOutboxDocument>.Filter.Nin(d => d.Status, TerminalStatuses));

		var update = Builders<MongoDbOutboxDocument>.Update
			.Set(d => d.Status, (int)OutboxStatus.DeadLettered)
			.Set(d => d.LastError, reason);

		_ = await _collection!.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Failed);

		if (maxRetries > 0)
		{
			filter = filterBuilder.And(filter, filterBuilder.Lt(d => d.RetryCount, maxRetries));
		}

		if (olderThan.HasValue)
		{
			filter = filterBuilder.And(filter, StoredInstantBefore("lastAttemptAt", olderThan.Value, inclusive: false));
		}

		// Sort by retry count (ascending) then by last attempt time
		var sort = Builders<MongoDbOutboxDocument>.Sort
			.Ascending(d => d.RetryCount)
			.Ascending(d => d.LastAttemptAt);

		var documents = await _collection!
			.Find(filter)
			.Sort(sort)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetAllTenantsScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Staged),
			filterBuilder.Ne(d => d.ScheduledAt, null),
			StoredInstantBefore("scheduledAt", scheduledBefore, inclusive: true));

		var sort = Builders<MongoDbOutboxDocument>.Sort.Ascending(d => d.ScheduledAt);

		var documents = await _collection!
			.Find(filter)
			.Sort(sort)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Sent),
			StoredInstantBefore("sentAt", olderThan, inclusive: false));

		// Find messages to delete
		var toDelete = await _collection!
			.Find(filter)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (toDelete.Count == 0)
		{
			return 0;
		}

		// Delete by IDs
		var ids = toDelete.Select(d => d.Id).ToList();
		var deleteFilter = filterBuilder.In(d => d.Id, ids);

		var result = await _collection!.DeleteManyAsync(deleteFilter, cancellationToken).ConfigureAwait(false);

		LogMessagesCleanedUp((int)result.DeletedCount, olderThan);
		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbOutboxDocument>.Filter;
		var now = _timeProvider.GetUtcNow();

		// Count by status
		var stagedCount = (int)await _collection!.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var sendingCount = (int)await _collection!.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Sending),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var sentCount = (int)await _collection!.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Sent),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var failedCount = (int)await _collection!.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Failed),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var scheduledCount = (int)await _collection!.CountDocumentsAsync(
			filter.And(
				filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
				filter.Ne(d => d.ScheduledAt, null)),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		// Get oldest unsent
		TimeSpan? oldestUnsentAge = null;
		var oldestUnsent = await _collection!
			.Find(filter.And(
				filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
				filter.Or(
					filter.Eq(d => d.ScheduledAt, null),
					StoredInstantBefore("scheduledAt", now, inclusive: true))))
			.SortBy(d => d.CreatedAt)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (oldestUnsent != null)
		{
			oldestUnsentAge = now - oldestUnsent.CreatedAt;
		}

		// Get oldest failed
		TimeSpan? oldestFailedAge = null;
		var oldestFailed = await _collection!
			.Find(filter.Eq(d => d.Status, (int)OutboxStatus.Failed))
			.SortBy(d => d.CreatedAt)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (oldestFailed != null)
		{
			oldestFailedAge = now - oldestFailed.CreatedAt;
		}

		return new OutboxStatistics
		{
			StagedMessageCount = stagedCount,
			SendingMessageCount = sendingCount,
			SentMessageCount = sentCount,
			FailedMessageCount = failedCount,
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsentAge,
			OldestFailedMessageAge = oldestFailedAge,
			CapturedAt = now
		};
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		// Disposed AFTER _disposed is set, and the ordering is the whole point. _disposed is what
		// stops a caller reaching WaitAsync/Release, so destroying the semaphore first creates an
		// interval where the guard is gone but callers are still admitted. In that interval an
		// in-flight initialiser's Release() throws ObjectDisposedException from its finally --
		// replacing whatever the try produced, including the real diagnostic -- and any caller
		// already blocked in WaitAsync is never signalled at all.
		//
		// The earlier comment here claimed disposing first meant "a throw later still frees the
		// handle". That was backwards: it does not protect against a later throw, it maximises the
		// window in which the initialiser's Release is guaranteed to throw. try/finally is what
		// frees a handle on a throw.
		_initLock?.Dispose();

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Reads an instant field in whichever of the two durable shapes it is stored in, for use inside an
	/// aggregation expression.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A message staged by a previously published version stores its instants in the driver's default
	/// shape for a <see cref="DateTimeOffset"/> — a <c>{ DateTime, Ticks, Offset }</c> sub-document —
	/// rather than as a BSON date. Under an aggregation comparison those two are not merely unequal:
	/// BSON's canonical type ordering places every sub-document BELOW every date, so a sub-document
	/// instant reads as earlier than any cutoff that can be constructed from the server's clock, and
	/// nothing about that is reported as an error.
	/// </para>
	/// <para>
	/// Applied to the claim predicate the consequence is not a boundary case. <c>leasedAt &lt; $$NOW −
	/// leaseTimeout</c> is true for such a document at every instant, forever, so a dispatcher running
	/// this version is handed a message a dispatcher running the previous version is still delivering —
	/// no crash, no pause, no elapsed time — and re-claims it on every subsequent poll. The duplicate
	/// window is bounded by neither the lease timeout nor the retry floor, and it stays open for as long
	/// as the two versions run side by side, which is the ordinary shape of a rolling upgrade. The same
	/// ordering makes a message scheduled for next month due now, and one under an active backoff floor
	/// past its floor.
	/// </para>
	/// <para>
	/// The sub-document carries the instant it was written from in its <c>DateTime</c> member, and that
	/// member is a real BSON date, so reading it back recovers a value the server's clock can be compared
	/// against. A field that is absent or null is returned unchanged, so the null branch beside every use
	/// of this still decides those.
	/// </para>
	/// </remarks>
	/// <param name="field">The document field name.</param>
	/// <returns>An expression yielding the field's instant as a BSON date.</returns>
	private static BsonValue StoredInstant(string field) =>
		new BsonDocument("$cond", new BsonArray
		{
			new BsonDocument("$eq", new BsonArray { new BsonDocument("$type", "$" + field), "object" }),
			"$" + field + ".DateTime",
			"$" + field,
		});

	/// <summary>
	/// Matches documents whose instant field — in either durable shape — precedes <paramref name="value"/>.
	/// </summary>
	/// <remarks>
	/// The query-operator counterpart of <see cref="StoredInstant"/>, and it fails the opposite way round.
	/// Query operators are type-bracketed, so <c>sentAt &lt; date</c> does not match a sub-document rather
	/// than always matching it. An instant written by a previously published version is therefore invisible
	/// to these queries: the message it belongs to is never returned for cleanup, never listed as scheduled
	/// or failed, and is not expired by the TTL index declared over the same field, so it is retained
	/// indefinitely. The second branch reads the date out of the sub-document so those messages are found.
	/// </remarks>
	/// <param name="field">The document field name.</param>
	/// <param name="value">The exclusive or inclusive upper bound.</param>
	/// <param name="inclusive">Whether an instant equal to <paramref name="value"/> matches.</param>
	/// <returns>A filter matching either stored shape.</returns>
	private static FilterDefinition<MongoDbOutboxDocument> StoredInstantBefore(
		string field,
		DateTimeOffset value,
		bool inclusive)
	{
		var op = inclusive ? "$lte" : "$lt";
		var bound = new BsonDateTime(value.UtcDateTime);

		return new BsonDocument("$or", new BsonArray
		{
			new BsonDocument(field, new BsonDocument(op, bound)),
			new BsonDocument(field + ".DateTime", new BsonDocument(op, bound)),
		});
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check under the lock: the winner of the race above completed initialisation
			// while this caller was waiting, and repeating the work would be wrong as well as wasteful.
			if (_initialized)
			{
				return;
			}
			if (_client == null)
			{
				var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
				settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
				settings.MaxConnectionPoolSize = _options.MaxPoolSize;

				if (_options.UseSsl)
				{
					settings.UseTls = true;
				}

				_client = new MongoClient(settings);
				_database = _client.GetDatabase(_options.DatabaseName);
				_collection = _database.GetCollection<MongoDbOutboxDocument>(_options.CollectionName);
			}

			// Create indexes
			var indexBuilder = Builders<MongoDbOutboxDocument>.IndexKeys;

			// Compound index for unsent message queries: status + scheduledAt + priority + createdAt
			var unsentIndex = new CreateIndexModel<MongoDbOutboxDocument>(
				indexBuilder.Combine(
					indexBuilder.Ascending(d => d.Status),
					indexBuilder.Ascending(d => d.ScheduledAt),
					indexBuilder.Ascending(d => d.Priority),
					indexBuilder.Ascending(d => d.CreatedAt)));

			// Index on status for status-specific queries
			var statusIndex = new CreateIndexModel<MongoDbOutboxDocument>(
				indexBuilder.Ascending(d => d.Status));

			// Index for failed message queries
			var failedIndex = new CreateIndexModel<MongoDbOutboxDocument>(
				indexBuilder.Combine(
					indexBuilder.Ascending(d => d.Status),
					indexBuilder.Ascending(d => d.RetryCount),
					indexBuilder.Ascending(d => d.LastAttemptAt)));

			// TTL index on SentAt for automatic cleanup
			if (_options.SentMessageTtlSeconds > 0)
			{
				var ttlIndex = new CreateIndexModel<MongoDbOutboxDocument>(
					indexBuilder.Ascending(d => d.SentAt),
					new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(_options.SentMessageTtlSeconds) });

				_ = await _collection!.Indexes.CreateOneAsync(ttlIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			_ = await _collection!.Indexes.CreateManyAsync([unsentIndex, statusIndex, failedIndex], cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	[LoggerMessage(DataMongoDbEventId.MessageStaged, LogLevel.Debug, "Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataMongoDbEventId.MessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataMongoDbEventId.MessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataMongoDbEventId.MessageFailed, LogLevel.Warning, "Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataMongoDbEventId.MessagesCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);
}
