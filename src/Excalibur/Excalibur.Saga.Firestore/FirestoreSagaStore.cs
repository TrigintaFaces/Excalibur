// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Data.Firestore;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Firestore;

/// <summary>
/// Firestore implementation of <see cref="ISagaStore"/> using Firestore documents per saga instance.
/// </summary>
/// <remarks>
/// <para>
/// Uses Firestore transactions for optimistic concurrency. Each saga instance is stored as a document
/// keyed by the owning tenant composed with the saga identifier and type -- see <c>GetDocumentId</c> for
/// why the tenant belongs to the identity rather than only to a check applied after the fetch.
/// </para>
/// <para>
/// Uses read-then-set pattern to preserve the createdUtc timestamp on updates.
/// </para>
/// </remarks>
public sealed partial class FirestoreSagaStore : ISagaStore, IAsyncDisposable
{
	private readonly FirestoreSagaOptions _options;
	private readonly ILogger<FirestoreSagaStore> _logger;
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
	/// lacks the prefix. Firestore orders documents by name, and within one collection every name shares a
	/// parent, so ordering reduces to the identifier itself and the range holds.
	/// </remarks>
	private const string TenantKeyPrefixUpperBound = "t;";

	private FirestoreDb? _db;
	private CollectionReference? _collection;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs at the first point the store would act on
	// the ABSENCE of a document, which is the first moment an unaddressable saga could be mistaken for one
	// that was never started. (The injection constructor sets _initialized in its body anyway, so
	// initialisation is not a path here.)
	private volatile bool _legacyDocumentsProbed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSagaStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore saga options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public FirestoreSagaStore(
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
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
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSagaStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore saga options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public FirestoreSagaStore(
		FirestoreDb db,
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(sagaId, typeof(TSagaState).Name);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			// The ABSENCE decision, and the one the caller acts on: a null here is read as "no saga in
			// flight", so the caller starts the saga over and re-fires every compensating action and external
			// call it already performed. A document written under the pre-tenant identifier answers exactly
			// this way, because the keyed read cannot address it.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return null;
		}

		// Another tenant's saga is "not found" from this scope — the answer a filtered query would give.
		if (!OwnedByCurrentScope(snapshot))
		{
			return null;
		}

		var stateJson = snapshot.GetValue<string>("stateJson");
		var result = _serializer.Deserialize<TSagaState>(stateJson);
		if (result is not null)
		{
			// The authoritative optimistic-concurrency version is the dedicated "version" field, NOT the
			// version embedded in stateJson (serialized before the store-owns-increment write-back, so it
			// carries the stale pre-save version). Apply it so load-modify-save gates against the real value.
			result.Version = snapshot.TryGetValue<long>("version", out var persistedVersion) ? persistedVersion : 0L;
		}

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("The saga state is serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var stateJson = _serializer.Serialize(sagaState);
		var sagaType = typeof(TSagaState).Name;
		var docId = GetDocumentId(sagaState.SagaId, sagaType);
		var docRef = _collection!.Document(docId);
		var expectedVersion = sagaState.Version;

		if (expectedVersion == 0)
		{
			// The create path acts on absence too, and more destructively than the load: the transaction
			// addresses the NEW identifier, so a saga already running under the old one is not seen as
			// existing - it is simply invisible, and a second, duplicate saga is written beside it. Probed
			// outside the transaction, before anything is modified, so a retry of the transaction body does
			// not repeat it.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
		}

		// Optimistic concurrency, honoring this store's documented "uses transactions
		// optimistic concurrency" contract (previously a non-transactional read-then-SetAsync that lost
		// concurrent writes). The read of the current version and the version-gated write happen inside one
		// Firestore transaction (RunTransactionAsync auto-retries on contention, re-reading on each attempt),
		// mirroring SqlServerSagaStore's version-gated MERGE: the write only proceeds when the persisted
		// version equals the loaded (expected) version, otherwise a ConcurrencyException is thrown.
		await _db!.RunTransactionAsync(
			async transaction =>
			{
				var existingSnapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				var currentVersion = 0L;
				var createdUtc = now;
				if (existingSnapshot.Exists)
				{
					if (existingSnapshot.TryGetValue<long>("version", out var persistedVersion))
					{
						currentVersion = persistedVersion;
					}

					if (existingSnapshot.TryGetValue<Timestamp>("createdUtc", out var createdTimestamp))
					{
						createdUtc = createdTimestamp.ToDateTimeOffset();
					}
				}

				// Ownership BEFORE version, and inside the transaction so the check and the write are atomic.
				// Without it a save carrying another tenant's saga id would compare versions against THEIR
				// document and, on a match, overwrite it — a cross-tenant write, not merely a disclosure.
				if (existingSnapshot.Exists && !OwnedByCurrentScope(existingSnapshot))
				{
					// NOT a ConcurrencyException: a conflict is transient and invites a retry, this is permanent
					// and a retry can never succeed. Distinct type, and no version disclosed.
					throw new TenantIsolationViolationException(nameof(SagaState), sagaState.SagaId.ToString());
				}

				if (currentVersion != expectedVersion)
				{
					throw new ConcurrencyException(
						nameof(SagaState),
						sagaState.SagaId.ToString(),
						expectedVersion,
						currentVersion);
				}

				// Ownership is carried over from the existing document when there is one, and assigned from the
				// ambient scope only at creation — never recomputed on update, which would re-home the saga.
				var scope = CurrentTenantScope;
				var owner = existingSnapshot.Exists && existingSnapshot.TryGetValue<string>("tenantId", out var existingOwner)
					? existingOwner
					: scope.TenantId;

				var data = new Dictionary<string, object>
				{
					["sagaId"] = sagaState.SagaId.ToString(),
					["sagaType"] = sagaType,
					["stateJson"] = stateJson,
					["isCompleted"] = sagaState.Completed,
					["version"] = expectedVersion + 1,
					["createdUtc"] = Timestamp.FromDateTimeOffset(createdUtc),
					["updatedUtc"] = Timestamp.FromDateTimeOffset(now)
				};

				// Persist completedAt only when the saga is completed. Omitting the field for a running saga
				// (Set replaces the whole document, so any prior value is cleared) means a Firestore range
				// filter on completedAt structurally excludes running sagas from age-based purge.
				if (sagaState.CompletedAt is { } completedAt)
				{
					data["completedAt"] = Timestamp.FromDateTimeOffset(completedAt);
				}


				// The untenanted partition is a MISSING field, not a null one — the same shape as Mongo's
				// [BsonIgnoreIfNull] and DynamoDb's attribute_not_exists condition. Writing an explicit null
				// would make "no tenant" a value, and a value can be matched by a query that meant to match
				// something else.
				if (!string.IsNullOrEmpty(owner))
				{
					data["tenantId"] = owner;
				}

				transaction.Set(docRef, data);
			},
			options: null,
			cancellationToken).ConfigureAwait(false);

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The tenant IS a discriminator on this document: it is written as its own top-level field beside the
	/// state blob, not inside it, so the query below applies it as a real predicate rather than refusing on
	/// the grounds that it cannot. <see cref="TenantScope.TenantId"/> is total -- untenanted, the single-tenant
	/// default, and a real tenant all bind a concrete term -- so this purge always filters, never refuses.
	/// </remarks>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, CurrentTenantScope.TenantId, cancellationToken);

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep: no tenant predicate, every tenant's completed sagas in range. Reachable only by
	/// calling this method directly, never as a fallback from the scoped purge above.
	/// </remarks>
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, tenantId: null, cancellationToken);

	/// <summary>
	/// Deletes completed, aged saga documents, optionally confined to one tenant.
	/// </summary>
	/// <param name="threshold">Sagas completed strictly before this instant are eligible.</param>
	/// <param name="tenantId">
	/// The tenant term to filter on, or <see langword="null"/> for the estate-wide sweep that applies no
	/// tenant predicate at all.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task<int> PurgeCoreAsync(DateTimeOffset threshold, string? tenantId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// A Firestore range filter matches ONLY documents that contain the field, so a running saga — which
		// never writes completedAt (see SaveAsync) — is structurally excluded. This mirrors the Mongo
		// "completedAt != null AND completedAt < cutoff" purge without needing an explicit null guard.
		var cutoff = Timestamp.FromDateTimeOffset(threshold.ToUniversalTime());
		var query = _collection!.WhereLessThan("completedAt", cutoff);
		if (tenantId is not null)
		{
			query = query.WhereEqualTo("tenantId", tenantId);
		}

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var removed = 0;
		// Firestore write batches cap at 500 operations; chunk the deletes to stay within the limit.
		const int maxBatchSize = 500;
		WriteBatch? batch = null;
		var batched = 0;

		foreach (var document in snapshot.Documents)
		{
			cancellationToken.ThrowIfCancellationRequested();

			batch ??= _db!.StartBatch();
			batch.Delete(document.Reference);
			removed++;
			batched++;

			if (batched == maxBatchSize)
			{
				await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
				batch = null;
				batched = 0;
			}
		}

		if (batch is not null && batched > 0)
		{
			await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

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
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Composes the Firestore document identifier from the owning tenant, the saga identifier and the saga type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the document's IDENTITY, not only of the ownership check applied after the fetch.
	/// Sagas are correlated by a business key, so two tenants legitimately run a saga at the same identifier;
	/// keyed on that identifier alone they are ONE document, and the ownership check can then refuse the second
	/// tenant's write but cannot give it a document of its own -- the isolation control degenerates into an
	/// estate-wide uniqueness constraint on the saga identifier. With the tenant in the identifier each tenant
	/// addresses its own document, so a cross-tenant write is unaddressable rather than merely refused.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty), so every identifier this store writes carries a
	/// tenant segment and none can be produced without one.
	/// </para>
	/// </remarks>
	/// <param name="sagaId">The caller-supplied saga identifier.</param>
	/// <param name="sagaType">The saga state type name.</param>
	/// <returns>The Firestore document identifier.</returns>
	private string GetDocumentId(Guid sagaId, string sagaType) =>
		$"{TenantKeyPrefix}{CurrentTenantScope.TenantId}:{sagaId}_{sagaType}";

	/// <summary>
	/// Returns whether a document belongs to the store's current scope.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Firestore addresses a saga by document id, so a keyed read carries no predicate and ownership is
	/// established after the fetch. On the SAVE path the equivalent check runs inside the transaction, so the
	/// ownership test and the write it guards are atomic — a document cannot change owner between them.
	/// </para>
	/// <para>
	/// Now that the document identifier itself carries the tenant, this is redundant for any document THIS store
	/// wrote: the identity and the stored field are assigned once, from the same scope, and the field is never
	/// re-stamped, so the two cannot disagree. It is retained because it is the check that still holds for a
	/// document this store did NOT write — one at a colliding identifier whose stored owner differs is treated as
	/// absent rather than read or overwritten.
	/// </para>
	/// </remarks>
	private bool OwnedByCurrentScope(DocumentSnapshot snapshot)
	{
		var scope = CurrentTenantScope;
		var owner = snapshot.TryGetValue<string>("tenantId", out var value) ? value : null;
		return string.Equals(owner, scope.TenantId, StringComparison.Ordinal);
	}

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
	/// Deliberately not on the initialisation path. Probing there would spend two range reads on every
	/// process start - on every serverless cold start, forever - to detect a condition that can only hold
	/// across a one-time upgrade, and would make the store unusable without a live collection even for
	/// operations that never read one. Here it costs nothing at startup, nothing on a read that finds a
	/// document, and at most one probe per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-absence decisions may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra pair of range reads and nothing else - cheaper than
	/// serialising every empty read behind a lock. The flag is set only once the probe has come back clean,
	/// so a collection that holds legacy documents refuses every call rather than only the first.
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
	/// already happened. On the create path the same silence lets a second, duplicate saga be written beside
	/// the original. Refusing converts that silence into a failure while both the state and the correlation
	/// are still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted document is a question about the
	/// deployment rather than about the data, so it cannot be decided here; the message states the procedure
	/// instead.
	/// </para>
	/// <para>
	/// The tenant lives in the document NAME on this provider rather than in a field, so the probe filters on
	/// <see cref="FieldPath.DocumentId"/>. Two range reads rather than one, because Firestore has no negated
	/// prefix operator: the identifiers that lack the prefix are exactly those sorting below
	/// <see cref="TenantKeyPrefix"/> and those sorting at or above <see cref="TenantKeyPrefixUpperBound"/>.
	/// Each is limited to a single document.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one saga document whose identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(CancellationToken cancellationToken)
	{
		Query[] probes =
		[
			_collection!.WhereLessThan(FieldPath.DocumentId, TenantKeyPrefix).Limit(1),
			_collection!.WhereGreaterThanOrEqualTo(FieldPath.DocumentId, TenantKeyPrefixUpperBound).Limit(1)
		];

		foreach (var probe in probes)
		{
			var snapshot = await probe.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (snapshot.Count == 0)
			{
				continue;
			}

			var legacyDocumentId = snapshot.Documents[0].Id;

			throw new InvalidOperationException(
				$"Saga collection '{_options.CollectionName}' holds at least one saga document whose " +
				$"identifier ('{legacyDocumentId}') carries no tenant segment, so it was written by a release " +
				$"that stored sagas without one. Those documents are unaddressable under the current key " +
				$"shape: a load of the saga they belong to reports no saga in flight, so the caller starts it " +
				$"again and re-runs every compensating action and external call it has already performed, and " +
				$"a create writes a second saga beside the first. Nothing has been modified. Stop the saga " +
				$"host, export every saga document, re-key each one by prefixing " +
				$"'{TenantKeyPrefix}<tenantId>:' with the tenant that owns the saga, re-import, and start the " +
				$"application again.");
		}
	}

	[LoggerMessage(DataFirestoreEventId.SagaLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataFirestoreEventId.SagaSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);

	[LoggerMessage(DataFirestoreEventId.SagasPurged, LogLevel.Debug, "Purged {PurgedCount} completed sagas older than {Threshold}")]
	private partial void LogSagasPurged(int purgedCount, DateTimeOffset threshold);

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check inside the lock: the winner finished while this caller waited.
			if (_initialized)
			{
				return;
			}
			var builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };

			if (!string.IsNullOrEmpty(_options.EmulatorHost))
			{
				// Point this client at the emulator directly. The process-wide FIRESTORE_EMULATOR_HOST
				// variable is first-write-wins, so routing through it lets a second store silently talk to
				// another store's emulator. Endpoint and EmulatorDetection.EmulatorOnly are mutually
				// exclusive -- setting both throws -- so an explicit endpoint with insecure credentials is
				// the combination that reaches an emulator per instance.
				builder.Endpoint = _options.EmulatorHost;
				builder.ChannelCredentials = ChannelCredentials.Insecure;
			}

			if (!string.IsNullOrEmpty(_options.CredentialsPath))
			{
#pragma warning disable CS0618 // Obsolete CredentialsPath/JsonCredentials
				builder.CredentialsPath = _options.CredentialsPath;
#pragma warning restore CS0618
			}
			else if (!string.IsNullOrEmpty(_options.CredentialsJson))
			{
#pragma warning disable CS0618
				builder.JsonCredentials = _options.CredentialsJson;
#pragma warning restore CS0618
			}

			_db = await builder.BuildAsync().ConfigureAwait(false);
			_collection = _db.Collection(_options.CollectionName);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}
}
