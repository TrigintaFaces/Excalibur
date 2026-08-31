// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data;
using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Saga.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state using MongoDB document storage.
/// Uses UpdateOneAsync with SetOnInsert for atomic upserts that preserve the original
/// creation timestamp while updating other fields.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based configuration for most users</description></item>
/// <item><description>Advanced: Existing IMongoClient for shared client instances</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class MongoDbSagaStore : ISagaStore, IAsyncDisposable
{
	private readonly MongoDbSagaOptions _options;
	private readonly ILogger<MongoDbSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;

	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// The leading segment every document identifier this store writes carries, ahead of the owning tenant.
	/// </summary>
	/// <remarks>
	/// Declared once and consumed by both the key builder and the legacy-document probe, so the shape the
	/// store writes and the shape it refuses to read cannot drift apart.
	/// </remarks>
	private const string TenantKeyPrefix = "t:";

	/// <summary>
	/// Exclusive upper bound of the tenant-prefixed key range, used by the legacy-document probe.
	/// </summary>
	/// <remarks>
	/// <c>':'</c> is U+003A and <c>';'</c> is U+003B, so every identifier beginning with
	/// <see cref="TenantKeyPrefix"/> sorts inside <c>["t:", "t;")</c> and every identifier outside that range
	/// lacks the prefix. Expressing the probe as two range comparisons keeps it served by the <c>_id</c>
	/// index rather than by a collection scan.
	/// </remarks>
	private const string TenantKeyPrefixUpperBound = "t;";

	/// <summary>
	/// Composes the stored document identifier (<c>_id</c>) from the owning tenant and the saga identifier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the document's IDENTITY, not only a term the query carries. Sagas are correlated
	/// by a business key, so two tenants legitimately run a saga at the same identifier; keyed on that
	/// identifier alone they are ONE document, and the second tenant's create collides with the first
	/// tenant's row. The tenant filter can then refuse the collision but cannot let the second tenant have
	/// its own saga, so the isolation control degenerates into an estate-wide uniqueness constraint on the
	/// saga identifier. With the tenant in the key each tenant addresses its own document, and a cross-tenant
	/// write is unaddressable rather than merely refused.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default and a genuinely untenanted saga resolves the reserved untenanted sentinel, so
	/// every identifier this store writes carries a tenant segment and none can be produced without one.
	/// </para>
	/// </remarks>
	/// <param name="sagaId">The caller-supplied saga identifier.</param>
	/// <returns>The document identifier as stored in <c>_id</c>.</returns>
	private string BuildDocumentId(Guid sagaId) =>
		$"{TenantKeyPrefix}{CurrentTenantScope.TenantId}:{sagaId}";

	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoCollection<MongoDbSagaDocument>? _collection;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs at the first point the store would act on
	// the ABSENCE of a document, which is the first moment an unaddressable saga could be mistaken for one
	// that was never started.
	private volatile bool _legacyDocumentsProbed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	public MongoDbSagaStore(
		IOptions<MongoDbSagaOptions> options,
		ILogger<MongoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Shared client instances across multiple stores</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with existing MongoDB infrastructure</description></item>
	/// </list>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// </remarks>
	public MongoDbSagaStore(
		IMongoClient client,
		IOptions<MongoDbSagaOptions> options,
		ILogger<MongoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_collection = client.GetDatabase(_options.DatabaseName)
			.GetCollection<MongoDbSagaDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Type-isolation: scope the load to BOTH SagaId AND SagaType. The store persists+indexes
		// SagaType on save, so loading by SagaId alone would return a saga of a DIFFERENT type that shares the
		// Guid, then deserialize its StateJson into the wrong TSagaState (silent data corruption). A typed
		// LoadAsync<TSagaState>(id) must return null when no saga of that type exists at the id — the contract
		// already enforced structurally by InMemory (`state is TSagaState`), Cosmos, Firestore, and DynamoDb.
		var filter = Builders<MongoDbSagaDocument>.Filter.And(
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Id, BuildDocumentId(sagaId)),
			TenantFilter(),
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaType, typeof(TSagaState).Name));
		var document = await _collection!
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document is null)
		{
			// The ABSENCE decision, and the one the caller acts on: a null here is read as "no saga in
			// flight", so the caller starts the saga over and re-fires every compensating action and external
			// call it already performed. A document written under the pre-tenant identifier answers exactly
			// this way, because the keyed read cannot address it.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return null;
		}

		if (string.IsNullOrEmpty(document.StateJson))
		{
			// Not an absence: the document was found, so the collection is addressable and needs no probe.
			return null;
		}

		var result = _serializer.Deserialize<TSagaState>(document.StateJson);
		if (result is not null)
		{
			// The authoritative optimistic-concurrency version is the dedicated BSON field, NOT the version
			// embedded in StateJson — the blob is serialized BEFORE the store-owns-increment write-back, so it
			// carries the stale pre-save version (e.g. 0). Apply the persisted version so load-modify-save
			// gates against the real value instead of always comparing against the stale embedded one.
			result.Version = document.Version;
		}

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable IL2026, IL3050 // AOT: MongoDB saga store uses reflection-based JSON serialization
		var stateJson = _serializer.Serialize(sagaState);
#pragma warning restore IL2026, IL3050
		// UpdatedUtc/CreatedUtc are DateTime fields; use a DateTime value so the Update builder does not emit a
		// Convert(d.UpdatedUtc, DateTimeOffset) node — the MongoDB LINQ provider cannot translate that and
		// throws ExpressionNotSupportedException on every real save (the unit tests mock IMongoCollection and
		// never exercise the translation, so the integration conformance lock is what surfaced it).
		var now = DateTime.UtcNow;
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency, mirroring SqlServerSagaStore's version-gated MERGE: the update
		// only matches a document whose persisted version equals the loaded (expected) version, and advances
		// it by one. The {_id, version} filter + upsert is the canonical MongoDB pattern:
		//   - new saga (no document)      -> filter doesn't match -> upsert INSERTs a fresh document;
		//   - in-sync update              -> filter matches        -> version-gated update succeeds;
		//   - stale version (concurrent write) -> filter doesn't match -> upsert attempts an INSERT on the
		//     already-present _id -> E11000 duplicate key, which we surface as ConcurrencyException instead of
		//     silently overwriting the newer write (the previous blind upsert lost the update).
		// The tenant is part of the document's IDENTITY (see BuildDocumentId), so tenant A and tenant B address
		// two different documents and neither upsert can reach the other's row. That is what makes the arm
		// SUCCEED rather than merely fail safely: before the identity carried the tenant, both tenants keyed one
		// document, so refusing the overwrite also refused the second tenant's create.
		//
		// TenantFilter() is retained on top of that. It is now redundant by construction — the identity and the
		// stored field are both assigned once, from the same scope, and the field is never re-stamped — so it can
		// never subtract a document this store wrote. It is kept because it is the term that makes the refusal
		// hold for anything this store did NOT write: a document at a colliding identifier whose stored owner
		// differs is excluded rather than matched.
		var scope = CurrentTenantScope;
		var tenantId = scope.TenantId;

		var filter = Builders<MongoDbSagaDocument>.Filter.And(
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Id, BuildDocumentId(sagaState.SagaId)),
			TenantFilter(),
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Version, expectedVersion));

		var update = Builders<MongoDbSagaDocument>.Update
			.Set(d => d.SagaType, typeof(TSagaState).Name)
			.Set(d => d.StateJson, stateJson)
			.Set(d => d.IsCompleted, sagaState.Completed)
			.Set(d => d.CompletedAt, sagaState.CompletedAt?.UtcDateTime)
			.Set(d => d.UpdatedUtc, now)
			.Set(d => d.Version, expectedVersion + 1)
			.SetOnInsert(d => d.SagaId, sagaState.SagaId)
			// SetOnInsert, never Set: ownership is fixed when the saga is created. Re-stamping the tenant on
			// every update would let a save under a different scope quietly re-home an existing saga, which is
			// the overwrite leak wearing the costume of a fix.
			.SetOnInsert(d => d.TenantId, tenantId)
			.SetOnInsert(d => d.CreatedUtc, now);

		// No-resurrect guard (SqlServer reference contract): only a brand-new saga (expected version 0) may
		// be inserted. For a stale save (expected > 0) we do NOT upsert — a missing/version-moved document is
		// a deleted/completed saga and must throw rather than resurrect at a high version (zombie saga).
		var isInsert = expectedVersion == 0;
		var options = new UpdateOptions { IsUpsert = isInsert };

		if (isInsert)
		{
			// The conditional create acts on absence too, and more destructively than the load: the upsert
			// addresses the NEW identifier, so a saga already running under the old one does not collide with
			// it - it is simply invisible, and a second, duplicate saga is inserted beside it. Probed before
			// the write, while nothing has been modified.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			var result = await _collection!.UpdateOneAsync(filter, update, options, cancellationToken)
				.ConfigureAwait(false);

			if (!isInsert && result.MatchedCount == 0)
			{
				// Update-only path matched nothing: the saga was deleted or its version moved on. Throw
				// instead of resurrecting (mirrors the MERGE's "@ExpectedVersion = 0"-guarded INSERT branch).
				// TENANT-FILTERED, like every other read. This is a diagnostic fetch — it exists only to report
				// the persisted version in the exception below — but an unfiltered read here would return
				// ANOTHER tenant's document and put ITS version number into an error message this caller can
				// see. A cross-tenant save must look like "no such saga" (version -1), not like a conflict
				// with a row the caller is not entitled to know exists.
				var current = await _collection!
					.Find(Builders<MongoDbSagaDocument>.Filter.And(
						TenantFilter(),
						Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Id, BuildDocumentId(sagaState.SagaId))))
					.FirstOrDefaultAsync(cancellationToken)
					.ConfigureAwait(false);

				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					current?.Version ?? -1L);
			}
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Reachable only on the insert path (expected == 0): a document already exists at this _id but
			// not at version 0 → a concurrent create / stale insert. Surface as a concurrency conflict.
			// TENANT-FILTERED for the same reason as the update path above: a diagnostic read is still a read,
			// and an unfiltered one discloses another tenant's version through the exception it raises.
			var current = await _collection!
				.Find(Builders<MongoDbSagaDocument>.Filter.And(
					TenantFilter(),
					Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Id, BuildDocumentId(sagaState.SagaId))))
				.FirstOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		LogSagaSaved(typeof(TSagaState).Name, sagaState.SagaId, sagaState.Completed);
	}

	/// <inheritdoc/>
	public async Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Now a real predicate rather than a refusal. Until the tenant became a first-class document field this
		// store could not express "this tenant's completed sagas" at all, so it refused the call rather than
		// delete every tenant's rows while reporting success. The scope is expressible, so the honest answer
		// changed from "I cannot" to the filtered delete itself.
		//
		// The partition is read here and passed DOWN, rather than read inside the filter builder. This is the
		// one statement in this store confined by the tenant term and by nothing else, so the builder for it
		// takes the partition as an explicit argument: a range sweep cannot be composed here without naming
		// what it confines to.
		var filter = CompletedBeforeScoped(threshold.UtcDateTime, CurrentTenantScope);

		var result = await _collection!.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);

		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep: no tenant discriminator, every tenant's completed sagas in range. Reachable only
	/// by writing this method's name, never by omitting a scope.
	/// </remarks>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query the dedicated completedAt field directly from the document (SA ruling). Compare as UTC
		// DateTime — the MongoDB LINQ/filter provider cannot translate DateTimeOffset. Filtered to non-null so a
		// running saga (completedAt == null) is never purged.
		var filter = CompletedBefore(threshold.UtcDateTime);

		var result = await _collection!.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);

		var removed = result.IsAcknowledged ? (int)result.DeletedCount : 0;
		LogSagasPurged(removed, threshold);
		return removed;
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
				_collection = _client.GetDatabase(_options.DatabaseName)
					.GetCollection<MongoDbSagaDocument>(_options.CollectionName);
			}

			// Create indexes for efficient queries
			var indexBuilder = Builders<MongoDbSagaDocument>.IndexKeys;

			// Index on sagaType for type-based queries
			var typeIndex = new CreateIndexModel<MongoDbSagaDocument>(
				indexBuilder.Ascending(d => d.SagaType),
				new CreateIndexOptions { Name = "ix_saga_type" });

			// Index on isCompleted for filtering active sagas
			var completedIndex = new CreateIndexModel<MongoDbSagaDocument>(
				indexBuilder.Ascending(d => d.IsCompleted),
				new CreateIndexOptions { Name = "ix_is_completed" });

			_ = await _collection!.Indexes.CreateManyAsync(
				[typeIndex, completedIndex],
				cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <summary>
	/// Builds the tenant predicate every keyed operation must carry.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A scoped store matches its own tenant. An unscoped store matches the untenanted partition — documents
	/// with no tenant — rather than everything, so "no tenant established" narrows the query instead of
	/// widening it. Mongo's equality against <see langword="null"/> matches both an explicit null and a missing
	/// field, which is what documents written before this field existed look like.
	/// </para>
	/// <para>
	/// <b>What enforces its use is not this helper, and not this store.</b> The predicate is centralised
	/// here so every statement binds one value; the CONTRACT is enforced one layer out, by the saga-store
	/// conformance kit every provider runs — <c>TenantScopedLoad_MustNotSeeAnotherTenantsSaga</c> (safety),
	/// <c>TenantScopedLoad_MustSeeItsOwnSaga</c> (liveness, the arm a fail-everything-closed implementation
	/// fails), <c>TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId</c>, and
	/// <c>UntenantedPartition_MustRoundTripItsOwnSaga</c>. That is the acceptance criterion that matters: a
	/// NEW PROVIDER omitting tenancy fails those arms without anyone having to notice the omission. It is
	/// deliberately not "every statement carries a predicate" — a rule of that shape, applied uniformly, is
	/// what once added tenant terms to statements already addressed by a primary key and stopped a store
	/// marking rows it had itself claimed.
	/// </para>
	/// <para>
	/// <b>The gap, stated rather than implied:</b> those arms cover the load and save paths. The
	/// tenant-confined purge below has no conformance arm, so a provider that omitted its tenant term
	/// would delete every tenant's completed sagas and still pass the suite. Until an arm exists, that
	/// operation is held by review, and a reviewer adding a purge-shaped path here should treat a missing
	/// tenant term as the finding rather than assume this helper covered it.
	/// </para>
	/// </remarks>
	private FilterDefinition<MongoDbSagaDocument> TenantFilter()
	{
		var scope = CurrentTenantScope;
		return Builders<MongoDbSagaDocument>.Filter.Eq(d => d.TenantId, scope.TenantId);
	}

	/// <summary>
	/// The completed-before range, matching every tenant. Not confined by anything: the filtered columns are
	/// neither the document key nor covered by a unique index, so this is a genuine estate-wide sweep and is
	/// reachable only from the method whose name says so.
	/// </summary>
	private static FilterDefinition<MongoDbSagaDocument> CompletedBefore(DateTime cutoff) =>
		Builders<MongoDbSagaDocument>.Filter.And(
			// Compare as UTC DateTime -- the MongoDB filter provider cannot translate DateTimeOffset. Filtered
			// to non-null so a running saga (no completion instant) is never purged.
			Builders<MongoDbSagaDocument>.Filter.Ne(d => d.CompletedAt, null),
			Builders<MongoDbSagaDocument>.Filter.Lt(d => d.CompletedAt, cutoff));

	/// <summary>
	/// The completed-before range confined to one partition, which is supplied as an argument rather than read
	/// from ambient state.
	/// </summary>
	/// <remarks>
	/// This is the only statement in this store whose confinement rests on the tenant term alone -- the keyed
	/// paths are addressed by a document id that already composes the tenant, so a term there is redundant by
	/// construction. A helper that read the ambient scope itself could be omitted by a new range path simply
	/// not calling it, and the omission would widen a destructive sweep to every tenant while still reporting
	/// a plausible count. Taking the partition as an argument makes the confined sweep unwriteable without it,
	/// and leaves the estate-wide sweep visible as the one that names no partition.
	/// </remarks>
	private static FilterDefinition<MongoDbSagaDocument> CompletedBeforeScoped(DateTime cutoff, TenantScope partition) =>
		Builders<MongoDbSagaDocument>.Filter.And(
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.TenantId, partition.TenantId),
			CompletedBefore(cutoff));

	/// <summary>
	/// Verifies, at most once per store instance, that an absent saga is genuinely absent rather than merely
	/// unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of a document, and from
	/// nowhere else. A read that returns a document proves the collection is addressable and needs no probe;
	/// only silence is ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path. Probing there would spend a query on every process start
	/// - on every serverless cold start, forever - to detect a condition that can only hold across a one-time
	/// upgrade, and would make the store unusable without a live collection even for operations that never
	/// read one. Here it costs nothing at startup, nothing on a read that finds a document, and at most one
	/// query per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-absence decisions may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra query and nothing else - cheaper than serialising every empty
	/// read behind a lock. The flag is set only once the probe has come back clean, so a collection that
	/// holds legacy documents refuses every call rather than only the first.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureEmptyReadIsTrustworthyAsync(CancellationToken cancellationToken)
	{
		if (_legacyDocumentsProbed)
		{
			return;
		}

		await RefuseLegacyUntenantedDocumentsAsync(cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	/// <summary>
	/// Refuses when the saga collection still holds a document written under the untenanted identifier of an
	/// earlier release. Called only through <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides
	/// when it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such a document is unaddressable under the current key shape, and the failure that follows is silent:
	/// a load returns NO SAGA rather than an error, so the caller treats a saga that is already part-executed
	/// as new and starts it again - re-firing every compensating action and every external call that has
	/// already happened. On the create path the same silence lets a second, duplicate saga be inserted beside
	/// the original. Refusing converts that silence into a failure while both the state and the correlation
	/// are still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted document is a question about the
	/// deployment rather than about the data, so it cannot be decided here; the message states the procedure
	/// instead.
	/// </para>
	/// <para>
	/// The identifiers that lack the prefix are exactly those sorting below <see cref="TenantKeyPrefix"/> and
	/// those sorting at or above <see cref="TenantKeyPrefixUpperBound"/>, so the probe reads through the
	/// <c>_id</c> index and returns on the first such document rather than scanning. It projects the
	/// identifier alone, so no saga state is deserialized to answer it.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one saga document whose identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(CancellationToken cancellationToken)
	{
		var filterBuilder = Builders<MongoDbSagaDocument>.Filter;
		var legacyShape = filterBuilder.Or(
			filterBuilder.Lt(d => d.Id, TenantKeyPrefix),
			filterBuilder.Gte(d => d.Id, TenantKeyPrefixUpperBound));

		var legacyDocumentId = await _collection!
			.Find(legacyShape)
			.Project(d => d.Id)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (string.IsNullOrEmpty(legacyDocumentId))
		{
			return;
		}

		throw new InvalidOperationException(
			$"Saga collection '{_options.CollectionName}' holds at least one saga document whose identifier " +
			$"('{legacyDocumentId}') carries no tenant segment, so it was written by a release that stored " +
			$"sagas without one. Those documents are unaddressable under the current key shape: a load of the " +
			$"saga they belong to reports no saga in flight, so the caller starts it again and re-runs every " +
			$"compensating action and external call it has already performed, and a create inserts a second " +
			$"saga beside the first. Nothing has been modified. Stop the saga host, export every saga " +
			$"document, re-key each one by prefixing '{TenantKeyPrefix}<tenantId>:' with the tenant that owns " +
			$"the saga, re-import, and start the application again.");
	}

	[LoggerMessage(DataMongoDbEventId.SagaStateLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataMongoDbEventId.SagaStatePurged, LogLevel.Debug, "Purged {Count} completed sagas older than {Threshold}")]
	private partial void LogSagasPurged(int count, DateTimeOffset threshold);

	[LoggerMessage(DataMongoDbEventId.SagaStateSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]

	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);
}
