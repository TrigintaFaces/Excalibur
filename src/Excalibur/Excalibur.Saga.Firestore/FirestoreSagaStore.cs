// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL2046, IL3050, IL3051 // AOT: Cloud-native provider uses reflection-based serialization
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Data.Firestore;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Firestore;

/// <summary>
/// Firestore implementation of <see cref="ISagaStore"/> using Firestore documents per saga instance.
/// </summary>
/// <remarks>
/// <para>
/// Uses Firestore transactions for optimistic concurrency. Each saga instance is stored
/// as a document keyed by "{sagaId}_{sagaType}".
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

	private readonly ITenantContext? _tenantContext;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSagaStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore saga options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public FirestoreSagaStore(
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
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
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public FirestoreSagaStore(
		FirestoreDb db,
		IOptions<FirestoreSagaOptions> options,
		ILogger<FirestoreSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
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

		// Optimistic concurrency (bd-e1tsq2), honoring this store's documented "uses transactions for
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
				var scope = TenantScope.FromContext(_tenantContext);
				var owner = existingSnapshot.Exists && existingSnapshot.TryGetValue<string>("tenantId", out var existingOwner)
					? existingOwner
					: scope.IsScoped ? scope.TenantId : sagaState.TenantId;

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
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		// This store has no tenant discriminator: it persists the saga state as a serialized blob, so the
		// tenant travels INSIDE the document rather than as a queryable field. It cannot build a server-side
		// tenant predicate, which makes it an untenanted-only store -- a coherent, supported shape under the
		// settled semantics, where every row it owns lives in the untenanted partition.
		//
		// So an unscoped purge is correct and proceeds. A SCOPED purge is refused rather than serviced,
		// because the only thing this store could do with a tenant is ignore it -- and ignoring it here means
		// deleting every OTHER tenant's completed sagas while reporting success. This is a range delete with
		// no reachability gate: unlike a point load, the caller needs nothing but a timestamp to destroy
		// another tenant's data. Failing loud is the one honest answer available to it.
		var scope = TenantScope.FromContext(_tenantContext);
		if (scope.IsScoped)
		{
			throw new TenantScopeNotSupportedException(
				$"This saga store cannot purge within a tenant scope. Store type: '{GetType().FullName}'. " +
				"It persists saga state as a serialized document, so the tenant is not a queryable field and " +
				"no tenant predicate can be applied. Servicing the call would delete every tenant's completed " +
				"sagas. Use a store that discriminates by tenant (SQL Server, Postgres, Oracle), or call " +
				"PurgeAllTenantsCompletedBeforeAsync if an estate-wide sweep is what you intended.");
		}

		return PurgeAllTenantsCompletedBeforeAsync(threshold, cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep, and the only purge this store can perform. It is identical to the unscoped
	/// path above because a store with no tenant discriminator cannot distinguish the two — which is exactly
	/// why the scoped call refuses instead of silently landing here.
	/// </remarks>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// A Firestore range filter matches ONLY documents that contain the field, so a running saga — which
		// never writes completedAt (see SaveAsync) — is structurally excluded. This mirrors the Mongo
		// "completedAt != null AND completedAt < cutoff" purge without needing an explicit null guard.
		var cutoff = Timestamp.FromDateTimeOffset(threshold.ToUniversalTime());
		var query = _collection!.WhereLessThan("completedAt", cutoff);
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
		_initLock.Dispose();
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
		return ValueTask.CompletedTask;
	}

	private static string GetDocumentId(Guid sagaId, string sagaType) =>
		$"{sagaId}_{sagaType}";

	/// <summary>
	/// Returns whether a document belongs to the store's current scope.
	/// </summary>
	/// <remarks>
	/// Firestore addresses a saga by document id, so a keyed read carries no predicate and ownership is
	/// established after the fetch. On the SAVE path the equivalent check runs inside the transaction, so the
	/// ownership test and the write it guards are atomic — a document cannot change owner between them.
	/// </remarks>
	private bool OwnedByCurrentScope(DocumentSnapshot snapshot)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		var owner = snapshot.TryGetValue<string>("tenantId", out var value) ? value : null;
		return string.Equals(owner, scope.IsScoped ? scope.TenantId : null, StringComparison.Ordinal);
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
				builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
				_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
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
