// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch;

using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// MongoDB-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses InsertOneAsync with unique index on (messageId, handlerType) for atomic first-writer-wins semantics.
/// The tenant is a component of the composite dedup identity, so the write/dedup/claim paths and keyed reads
/// are isolated by construction — two tenants carrying the same message id never dedup against each other.
/// Catches MongoWriteException with duplicate key error (11000) for conflict detection.
/// <para>
/// The <see cref="IInboxStoreAdmin"/> operator surface (<c>GetAllEntries</c>, <c>GetFailedEntries</c>,
/// <c>GetStatistics</c>, <c>Cleanup</c>) queries <c>Filter.Empty</c> and is <b>estate-wide by design</b>:
/// it serves operators and background services (dashboards, retention, retry processors), not the
/// per-tenant request path. A message handler depends on <see cref="IInboxStore"/>, which does not expose
/// these methods, so a per-tenant caller cannot reach a cross-tenant read or delete. This estate-wide scan
/// is a deliberate global, not an oversight — matching <c>SqlServerInboxStore</c>.
/// </para>
/// </remarks>
[SuppressMessage("Maintainability", "CA1506:AvoidExcessiveClassCoupling",
	Justification = "Large provider store; the tenant-isolation additions (ITenantContext + TenantScope) are the minimal necessary for the P0 cross-tenant dedup-key fix, and the remaining coupling is inherent to the MongoDB driver surface and the multi-interface inbox contract.")]
public sealed partial class MongoDbInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IScopedTransactionalInboxStore, IInboxStoreCapabilities, IInboxStoreAdmin, IAsyncDisposable
{
	private const int DuplicateKeyErrorCode = 11000;

	private readonly MongoDbInboxOptions _options;
	private readonly ILogger<MongoDbInboxStore> _logger;

	// Ambient tenant context. When active, the tenant is composed INTO the unique _id (via ScopedId) so two
	// tenants' identical (messageId, handlerType) can never collide on the dedup key, and is stamped on write.
	// When null / no resolved tenant (non-multi-tenant), keys and rows are byte-identical to the un-scoped form.
	private readonly ITenantContext? _tenantContext;

	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbInboxDocument>? _collection;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">Optional ambient tenant context; when active it scopes the dedup <c>_id</c> and stamps the tenant on write (null = non-multi-tenant, byte-identical behavior).</param>
	public MongoDbInboxStore(
		IOptions<MongoDbInboxOptions> options,
		ILogger<MongoDbInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">Optional ambient tenant context; when active it scopes the dedup <c>_id</c> and stamps the tenant on write (null = non-multi-tenant, byte-identical behavior).</param>
	public MongoDbInboxStore(
		IMongoClient client,
		IOptions<MongoDbInboxOptions> options,
		ILogger<MongoDbInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<MongoDbInboxDocument>(_options.CollectionName);
	}

	// Composes the tenant INTO the unique _id when multi-tenancy is active (byte-identical when None), so the
	// dedup/claim key — and thus every keyed read/write/claim — is tenant-isolated by construction.
	private string ScopedId(string messageId, string handlerType)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return MongoDbInboxDocument.CreateId(messageId, handlerType, scope.IsScoped ? scope.TenantId : null);
	}

	// The tenant to stamp on a written row: the ambient tenant when scoped, else the supplied fallback
	// (the entry's own tenant, or null for the minimal claim/mark documents) — byte-identical when non-MT.
	private string? StampTenant(string? fallback = null)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return scope.IsScoped ? scope.TenantId : fallback;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
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

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var document = MongoDbInboxDocument.FromInboxEntry(entry);
		// Tenant-scope the unique _id and stamp the row so dedup + every keyed read isolate per tenant.
		document.Id = ScopedId(entry.MessageId, entry.HandlerType);
		document.TenantId = StampTenant(entry.TenantId);

		try
		{
			await _collection!.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogCreatedEntry(_logger, messageId, handlerType, null);
			return entry;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var existing = await _collection!.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
					   ?? throw new InvalidOperationException(
						   $"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		if (existing.Status == (int)InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Inbox entry already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Processed)
			.Set(d => d.ProcessedAt, DateTimeOffset.UtcNow)
			.Set(d => d.LastError, null);

		_ = await _collection!.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogProcessedEntry(_logger, messageId, handlerType, null);
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Processing)
			.Set(d => d.LastAttemptAt, DateTimeOffset.UtcNow);

		var result = await _collection!.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		if (result.MatchedCount == 0)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}", messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Create a minimal document for first-writer-wins
		var document = new MongoDbInboxDocument
		{
			Id = ScopedId(messageId, handlerType),
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			TenantId = StampTenant(),
			Status = (int)InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow,
			ReceivedAt = DateTimeOffset.UtcNow
		};

		try
		{
			await _collection!.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
			return true;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state. The unique _id
		// (messageId+handlerType) makes InsertOneAsync fail with a duplicate-key error on conflict
		// (already claimed/processed) => not claimed. Finalized via MarkProcessedAsync, removed via ReleaseAsync.
		var document = new MongoDbInboxDocument
		{
			Id = ScopedId(messageId, handlerType),
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			TenantId = StampTenant(),
			Status = (int)InboxStatus.Processing,
			ReceivedAt = DateTimeOffset.UtcNow,
			LastAttemptAt = DateTimeOffset.UtcNow
		};

		try
		{
			await _collection!.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			_logger.LogDebug("Claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return true;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			_logger.LogDebug("Claim denied (already claimed/processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var leaseMs = (long)leaseDuration.TotalMilliseconds;

		// Two-stage lease CAS. A single upsert cannot carry the reclaim predicate because MongoDB rejects
		// '$expr' in the query predicate of an upsert (Code 224). So the claim is split:
		//
		//   Stage 1 (seed, insert-if-absent): an _id-only upsert with '$setOnInsert' (NO '$expr') ensures a
		//   Received identity document exists. It only writes on insert, so a live-lease/Processed document
		//   is never disturbed. Model date fields are written by the typed serializer (never a pipeline
		//   BSON-date), keeping later typed reads coercion-safe. The concurrent upsert-insert race resolves
		//   to a single winner; losers observe a duplicate-key error and simply proceed (the row now exists).
		//
		//   Stage 2 (conditional claim, non-upsert): a '$expr'-guarded pipeline UpdateOne (IsUpsert = false,
		//   where '$expr' IS permitted) atomically claims the row IFF it is Received OR Failed (re-admit a handler-failed entry so a
		//   redelivery retries) OR (Processing AND the
		//   lease has expired), comparing the SERVER clock '$$NOW' against the server-written 'leaseExpiresAt'
		//   BSON date — never an app-side clock. A live lease or terminal Processed row fails the filter.
		//   MongoDB serializes writes per document, so exactly one concurrent caller wins the transition.
		var seedFilter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);
		var seed = Builders<MongoDbInboxDocument>.Update
			.SetOnInsert(d => d.MessageId, messageId)
			.SetOnInsert(d => d.HandlerType, handlerType)
			.SetOnInsert(d => d.MessageType, "Unknown")
			.SetOnInsert(d => d.TenantId, StampTenant())
			.SetOnInsert(d => d.Status, (int)InboxStatus.Received)
			.SetOnInsert(d => d.ReceivedAt, DateTimeOffset.UtcNow);

		try
		{
			_ = await _collection!.UpdateOneAsync(
				seedFilter,
				seed,
				new UpdateOptions { IsUpsert = true },
				cancellationToken).ConfigureAwait(false);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Lost the concurrent seed insert race; the identity document now exists — proceed to claim it.
		}

		var claimFilter = new BsonDocument
		{
			{ "_id", id },
			{
				"$or", new BsonArray
				{
					new BsonDocument("status", (int)InboxStatus.Received),
					new BsonDocument("status", (int)InboxStatus.Failed),
					new BsonDocument
					{
						{ "status", (int)InboxStatus.Processing },
						{ "$expr", new BsonDocument("$lt", new BsonArray { "$leaseExpiresAt", "$$NOW" }) }
					}
				}
			}
		};

		var claimPipeline = new BsonDocument[]
		{
			new("$set", new BsonDocument
			{
				{ "status", (int)InboxStatus.Processing },
				{ "leaseExpiresAt", new BsonDocument("$add", new BsonArray { "$$NOW", leaseMs }) }
			})
		};

		PipelineDefinition<MongoDbInboxDocument, MongoDbInboxDocument> pipeline = claimPipeline;

		var result = await _collection!.UpdateOneAsync(
			claimFilter,
			Builders<MongoDbInboxDocument>.Update.Pipeline(pipeline),
			new UpdateOptions { IsUpsert = false },
			cancellationToken).ConfigureAwait(false);

		var claimed = result.ModifiedCount > 0;

		if (claimed)
		{
			_logger.LogDebug("Lease-claimed inbox entry for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}
		else
		{
			_logger.LogDebug("Lease-claim denied (live lease or processed) for message {MessageId} and handler {HandlerType}", messageId, handlerType);
		}

		return claimed;
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Atomic conditional delete: remove the non-terminal claim only (status != Processed) so a
		// concurrently-finalized entry is never deleted. No-op if already removed or never claimed.
		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.And(
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id),
			Builders<MongoDbInboxDocument>.Filter.Ne(d => d.Status, (int)InboxStatus.Processed));

		_ = await _collection!.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.And(
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id),
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Status, (int)InboxStatus.Processed));

		var count = await _collection!.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
		return count > 0;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var document = await _collection!.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return document?.ToInboxEntry();
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		_ = await _collection!.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Failed)
			.Set(d => d.LastError, errorMessage)
			.Set(d => d.LastAttemptAt, DateTimeOffset.UtcNow)
			.Inc(d => d.RetryCount, 1);

		_ = await _collection!.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = ScopedId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		_ = await _collection!.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		// Set RetryCount EXACTLY (.Set, not .Inc) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt (FR-4).
		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Failed)
			.Set(d => d.LastError, errorMessage)
			.Set(d => d.LastAttemptAt, DateTimeOffset.UtcNow)
			.Set(d => d.RetryCount, retryCount);

		_ = await _collection!.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbInboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)InboxStatus.Failed),
			filterBuilder.Lt(d => d.RetryCount, maxRetries));

		if (olderThan.HasValue)
		{
			filter = filterBuilder.And(filter, filterBuilder.Lt(d => d.LastAttemptAt, olderThan.Value));
		}

		var documents = await _collection!
			.Find(filter)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToInboxEntry());
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documents = await _collection!
			.Find(Builders<MongoDbInboxDocument>.Filter.Empty)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToInboxEntry());
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbInboxDocument>.Filter;

		var total = await _collection!.CountDocumentsAsync(filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
		var processed = await _collection!
			.CountDocumentsAsync(filter.Eq(d => d.Status, (int)InboxStatus.Processed), cancellationToken: cancellationToken)
			.ConfigureAwait(false);
		var failed = await _collection!
			.CountDocumentsAsync(filter.Eq(d => d.Status, (int)InboxStatus.Failed), cancellationToken: cancellationToken)
			.ConfigureAwait(false);
		var pending = await _collection!.CountDocumentsAsync(
			filter.Or(
				filter.Eq(d => d.Status, (int)InboxStatus.Received),
				filter.Eq(d => d.Status, (int)InboxStatus.Processing)),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return new InboxStatistics
		{
			TotalEntries = (int)total,
			ProcessedEntries = (int)processed,
			FailedEntries = (int)failed,
			PendingEntries = (int)pending
		};
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbInboxDocument>.Filter.And(
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Status, (int)InboxStatus.Processed),
			Builders<MongoDbInboxDocument>.Filter.Lt(d => d.ProcessedAt, olderThan));

		var result = await _collection!.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);

		LogCleanedUpEntries(_logger, (int)result.DeletedCount, null);
		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	/// <remarks>The MongoDB store implements <see cref="IClaimableInboxStore"/> directly.</remarks>
	public bool SupportsClaim => true;

	/// <inheritdoc/>
	/// <remarks>The MongoDB store implements <see cref="IProcessingTrackingInboxStore"/> directly.</remarks>
	public bool SupportsProcessingTracking => true;

	/// <inheritdoc/>
	/// <remarks>
	/// Transactional (exactly-once) processing requires a MongoDB multi-document transaction, which is only
	/// available on a replica-set (or sharded) deployment. The capability is therefore opt-in through
	/// <see cref="MongoDbInboxOptions.EnableTransactions"/> so the store never falsely advertises atomicity on a
	/// standalone server; when disabled callers fall back to the at-least-once idempotent claim protocol.
	/// </remarks>
	public bool SupportsTransactional => _options.EnableTransactions;

	/// <inheritdoc/>
	public async ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(handler);

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var session = await _client!.StartSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		session.StartTransaction();

		try
		{
			// Duplicate check inside the session (transactional read): a message already marked processed is a
			// duplicate — abort the (empty) transaction and report false so the handler does not run again.
			if (await IsProcessedInSessionAsync(session, messageId, handlerType, cancellationToken).ConfigureAwait(false))
			{
				await session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
				LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
				return false;
			}

			// Run the handler; any writes it issues on this session enlist in the same transaction.
			await handler(new MongoInboxTransactionScope(session), cancellationToken).ConfigureAwait(false);

			// Mark processed within the SAME session so the processed-mark commits atomically with the handler's
			// enlisted writes.
			await MarkProcessedInSessionAsync(session, messageId, handlerType, cancellationToken).ConfigureAwait(false);

			await session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

			LogProcessedEntry(_logger, messageId, handlerType, null);
			return true;
		}
		catch
		{
			// Roll the whole transaction back — nothing is marked processed and the message is redelivered.
			// Abort with a non-cancelled token so cancellation does not skip the rollback.
			await session.AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
			throw;
		}
	}

	private async Task<bool> IsProcessedInSessionAsync(
		IClientSessionHandle session,
		string messageId,
		string handlerType,
		CancellationToken cancellationToken)
	{
		var id = ScopedId(messageId, handlerType);
		var idFilter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var existing = await _collection!
			.Find(session, idFilter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return existing is not null && existing.Status == (int)InboxStatus.Processed;
	}

	private async Task MarkProcessedInSessionAsync(
		IClientSessionHandle session,
		string messageId,
		string handlerType,
		CancellationToken cancellationToken)
	{
		var id = ScopedId(messageId, handlerType);
		var idFilter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);
		var now = DateTimeOffset.UtcNow;

		// Upsert so the mark lands whether or not a prior Received/Processing entry existed.
		var update = Builders<MongoDbInboxDocument>.Update
			.SetOnInsert(d => d.MessageId, messageId)
			.SetOnInsert(d => d.HandlerType, handlerType)
			.SetOnInsert(d => d.MessageType, "Unknown")
			.SetOnInsert(d => d.ReceivedAt, now)
			.Set(d => d.Status, (int)InboxStatus.Processed)
			.Set(d => d.ProcessedAt, now)
			.Set(d => d.LastError, null);

		_ = await _collection!.UpdateOneAsync(
			session,
			idFilter,
			update,
			new UpdateOptions { IsUpsert = true },
			cancellationToken).ConfigureAwait(false);
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
		_initLock.Dispose();

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
	}

	[LoggerMessage(DataMongoDbEventId.InboxStored, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxMarkedComplete, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxFirstProcessor, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxAlreadyProcessed, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxMarkedFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
	private static partial void LogCleanedUpEntries(ILogger logger, int count, Exception? exception);

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
				_collection = _database.GetCollection<MongoDbInboxDocument>(_options.CollectionName);
			}

			// Create indexes
			var indexBuilder = Builders<MongoDbInboxDocument>.IndexKeys;

			// Index on handlerType for handler-specific queries
			var handlerIndex = new CreateIndexModel<MongoDbInboxDocument>(
				indexBuilder.Ascending(d => d.HandlerType));

			// Index on status for filtered queries
			var statusIndex = new CreateIndexModel<MongoDbInboxDocument>(
				indexBuilder.Ascending(d => d.Status));

			// TTL index on ProcessedAt for automatic cleanup
			if (_options.DefaultTtlSeconds > 0)
			{
				var ttlIndex = new CreateIndexModel<MongoDbInboxDocument>(
					indexBuilder.Ascending(d => d.ProcessedAt),
					new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(_options.DefaultTtlSeconds) });

				_ = await _collection!.Indexes.CreateOneAsync(ttlIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			_ = await _collection!.Indexes.CreateManyAsync([handlerIndex, statusIndex], cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}
}
