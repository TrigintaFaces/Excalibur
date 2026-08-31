// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Excalibur.Data;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Snapshots;

/// <summary>
/// Firestore-based implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses a simple collection design with documents keyed by a composite id of tenant term, aggregate
/// type and aggregate id.
/// Version ordering is enforced by a conditional write -- older versions never overwrite newer ones.
/// </para>
/// <para>
/// The write is lock-free: the store reads the document, then creates it when it was absent or updates it
/// under the update time the read observed. Both forms are evaluated by the server as part of the write
/// itself, so no lock is held across a round trip and a writer that loses simply re-reads. A snapshot
/// write is idempotent under re-attempt -- a writer overtaken while it waited finds the higher version
/// already stored and returns without writing -- which is what makes a bounded attempt count correct
/// rather than merely hopeful.
/// </para>
/// </remarks>
public sealed partial class FirestoreSnapshotStore : ISnapshotStore, IAsyncDisposable, IDisposable
{
	// Leading segment of every document id, kept so an id is recognisable as this store's and so the
	// composed shape is unchanged from what this store has always written.
	private const string TenantSegmentMarker = "t";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreSnapshotStoreOptions _options;
	private readonly ILogger<FirestoreSnapshotStore> _logger;
	/// <summary>
	/// Time source for the wait between contended write attempts. Injected so a test can drive that wait
	/// instead of sleeping on a wall clock: the contention path is the one this store most needs to be
	/// able to exercise, and it is untestable when its own timing cannot be controlled.
	/// </summary>
	private readonly TimeProvider _timeProvider;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

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
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="timeProvider">
	/// Time source for the wait between contended write attempts. Defaults to
	/// <see cref="System.TimeProvider.System"/> when not supplied.
	/// </param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public FirestoreSnapshotStore(
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger,
		ITenantContext tenantContext,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreSnapshotStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="timeProvider">
	/// Time source for the wait between contended write attempts. Defaults to
	/// <see cref="System.TimeProvider.System"/> when not supplied.
	/// </param>
	public FirestoreSnapshotStore(
		FirestoreDb db,
		IOptions<FirestoreSnapshotStoreOptions> options,
		ILogger<FirestoreSnapshotStore> logger,
		ITenantContext tenantContext,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection!.Document(documentId);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return null;
			}

#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
			var snapshotResult = FromFirestoreDocument(snapshot);
#pragma warning restore IL2026, IL3050
			LogSnapshotRetrieved(aggregateType, aggregateId, snapshotResult.Version);
			return snapshotResult;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(snapshot.AggregateType, snapshot.AggregateId);
		var docRef = _collection!.Document(documentId);

		try
		{
			await SaveWithConditionalWriteAsync(docRef, documentId, snapshot, r => result = r, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"save",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Upper bound on a single wait between contended write attempts, so that a large configured base
	/// delay cannot turn a contention pause into a multi-second stall.
	/// </summary>
	/// <remarks>
	/// A losing writer here has not been blocked by anything -- its conditional write was rejected in one
	/// round trip -- so the wait exists only to separate writers that were rejected at the same instant,
	/// not to outlast a lock. A quarter of a second is already far longer than the round trip it is
	/// spreading.
	/// </remarks>
	private static readonly TimeSpan MaxContendedWriteBackoff = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// Stores the snapshot with a lock-free conditional write, re-reading and re-attempting while it keeps
	/// losing the document to another writer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each pass reads the document and then makes the write conditional on what that read observed:
	/// <c>CreateAsync</c> when the document was absent, which the server rejects if it exists by then, and
	/// <c>UpdateAsync</c> under <see cref="Precondition.LastUpdated"/> when it was present, which the
	/// server rejects if the document has moved on since. Neither takes a lock, so a rejected writer is
	/// told immediately rather than waiting for one to be released.
	/// </para>
	/// <para>
	/// The loop makes progress regardless of how many writers contend, and the argument does not depend on
	/// the attempt count: a pass is only re-attempted because some other writer's write landed, that write
	/// strictly raised the stored version, and versions do not decrease -- so this writer either stores its
	/// own snapshot or, on a later pass, reads a version at or above its own and returns through the guard.
	/// The bound is a guard against an unbounded spin, and reaching it is a fault rather than an expected
	/// outcome, which is why it ends in a throw rather than a silent return.
	/// </para>
	/// </remarks>
	private async Task SaveWithConditionalWriteAsync(
		DocumentReference docRef,
		string documentId,
		ISnapshot snapshot,
		Action<string> setResult,
		CancellationToken cancellationToken)
	{
		for (var attempt = 1; attempt <= _options.MaxContendedWriteAttempts; attempt++)
		{
			var existing = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (existing.Exists)
			{
				var existingVersion = existing.GetValue<long>("version");
				if (existingVersion >= snapshot.Version)
				{
					// Version guard: the only legitimate way to leave without writing.
					setResult(WriteStoreTelemetry.Results.Conflict);
					LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version, existingVersion);
					return;
				}

				try
				{
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
					var updates = ToFirestoreUpdate(snapshot);
#pragma warning restore IL2026, IL3050

					// UpdateTime is null only when the document does not exist, and this branch is the one
					// where it does.
					_ = await docRef.UpdateAsync(
						updates,
						Precondition.LastUpdated(existing.UpdateTime!.Value),
						cancellationToken).ConfigureAwait(false);

					LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}
				catch (RpcException ex) when (IsLostUpdateRace(ex))
				{
					LogContendedWriteRetried(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version, attempt);
				}
			}
			else
			{
				try
				{
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
					var document = ToFirestoreDocument(snapshot);
#pragma warning restore IL2026, IL3050

					_ = await docRef.CreateAsync(document, cancellationToken).ConfigureAwait(false);

					LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}
				catch (RpcException ex) when (IsLostCreateRace(ex))
				{
					LogContendedWriteRetried(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version, attempt);
				}
			}

			await DelayBetweenAttemptsAsync(attempt, cancellationToken).ConfigureAwait(false);
		}

		// Exhaustion is a FAULT, not a skip. Falling out silently would drop the snapshot and tell no one:
		// the caller would observe a successful SaveSnapshotAsync while nothing was written. Report it in
		// the abstraction's own currency -- every other provider reports write contention as
		// ConcurrencyException -- never as a raw gRPC status, and never as silence.
		setResult(WriteStoreTelemetry.Results.Failure);

		throw new ConcurrencyException(
			nameof(FirestoreSnapshotStore),
			documentId,
			snapshot.Version,
			await ReadCurrentVersionOrDefaultAsync(docRef, cancellationToken).ConfigureAwait(false));
	}

	/// <summary>
	/// True when the status is the server rejecting a conditional UPDATE because the document is no longer
	/// the one that was read. Deliberately narrow: DeadlineExceeded is NOT included, because it also covers
	/// a genuinely unreachable backend, and re-attempting that would turn an infrastructure fault into a
	/// slow, silent one.
	/// </summary>
	/// <remarks>
	/// FailedPrecondition is the update time no longer matching. NotFound is the document having been
	/// deleted between the read and the write, which the next pass handles by creating it instead. Aborted
	/// is the server reporting contention on the write itself.
	/// </remarks>
	/// <param name="ex">The status returned by the rejected update.</param>
	/// <returns><see langword="true"/> when the update should be re-attempted.</returns>
	private static bool IsLostUpdateRace(RpcException ex) =>
		ex.StatusCode is StatusCode.FailedPrecondition or StatusCode.NotFound or StatusCode.Aborted;

	/// <summary>
	/// True when the status is the server rejecting a CREATE because another writer created the document
	/// first, or reporting contention on the write itself.
	/// </summary>
	/// <param name="ex">The status returned by the rejected create.</param>
	/// <returns><see langword="true"/> when the create should be re-attempted.</returns>
	private static bool IsLostCreateRace(RpcException ex) =>
		ex.StatusCode is StatusCode.AlreadyExists or StatusCode.Aborted;

	/// <summary>
	/// Reads the currently stored snapshot version for diagnostics on the exhaustion path, returning -1
	/// when the document cannot be read. Never throws: it runs only while reporting another failure and
	/// must not replace that failure with its own.
	/// </summary>
	/// <param name="docRef">The snapshot document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The stored version, or -1 when it cannot be read.</returns>
	private static async Task<long> ReadCurrentVersionOrDefaultAsync(
		DocumentReference docRef,
		CancellationToken cancellationToken)
	{
		try
		{
			var doc = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			return doc.Exists ? doc.GetValue<long>("version") : -1;
		}
		catch (RpcException)
		{
			return -1;
		}
	}

	/// <summary>
	/// Waits before the next attempt, drawing the wait at random from an exponentially growing interval.
	/// </summary>
	/// <remarks>
	/// The randomisation is the load-bearing part rather than the growth. Contending writers are rejected
	/// at nearly the same instant, so a wait computed only from the attempt number is identical for all of
	/// them -- they wake together and reproduce the collision they were waiting out. Drawing from a range
	/// spreads them apart, which is what lets the contention drain. The wait runs on the injected time
	/// source, so a test can drive it rather than sleep through it.
	/// </remarks>
	/// <param name="attempt">The one-based attempt that was just lost.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when the wait has elapsed.</returns>
	private Task DelayBetweenAttemptsAsync(int attempt, CancellationToken cancellationToken)
	{
		var baseDelay = (double)_options.ContendedWriteBackoffMilliseconds;

		// A configured base delay may legitimately exceed the cap; the ceiling can never be below the
		// first wait, so take whichever is larger rather than rejecting the value.
		var cap = Math.Max(MaxContendedWriteBackoff.TotalMilliseconds, baseDelay);
		var ceiling = (int)Math.Min(baseDelay * Math.Pow(2, attempt - 1), cap);

		// The framework's general-purpose generator is the obvious fit for spreading out retries and is
		// rejected by analysis wherever randomness is drawn, so this path draws from the cryptographic one
		// instead. It runs only after a write has already been rejected, so the extra cost is charged to a
		// round trip that has just been spent anyway.
		var wait = ceiling <= 0 ? 0 : RandomNumberGenerator.GetInt32(ceiling + 1);

		return Task.Delay(TimeSpan.FromMilliseconds(wait), _timeProvider, cancellationToken);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection!.Document(documentId);

		try
		{
			_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			LogSnapshotDeleted(aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"delete",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// For Firestore's simple one-snapshot-per-aggregate model, we only delete
		// if the current snapshot's version is older than the specified version
		var documentId = CreateDocumentId(aggregateType, aggregateId);
		var docRef = _collection!.Document(documentId);

		try
		{
			// The version test and the delete are one atomic step, for the same reason the save path above
			// is: this store keeps one document per aggregate, so "delete if older" and "overwrite with
			// newer" address the SAME document. Split the test from the delete and a concurrent save lands
			// in the gap -- the read sees a stale version, decides to delete, and the delete removes the
			// NEWER snapshot that arrived in between. The condition was true when it was evaluated and
			// false when it was acted on.
			//
			// A transaction rather than the save path's conditional write: a delete conditioned on the
			// document's update time would abort whenever the document had merely been rewritten, and this
			// operation wants to re-evaluate the version instead of failing. Deletes are not on the
			// contended path the save path is.
			var deleted = false;

			await _db!.RunTransactionAsync(
				async transaction =>
				{
					var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

					if (!snapshot.Exists)
					{
						result = WriteStoreTelemetry.Results.NotFound;
						return;
					}

					// Re-read inside the transaction: this is the value the delete is conditioned on, and
					// Firestore aborts and retries the transaction if the document changed under it.
					var currentVersion = snapshot.GetValue<long>("version");
					if (currentVersion < olderThanVersion)
					{
						transaction.Delete(docRef);
						deleted = true;
					}
				},
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (deleted)
			{
				LogSnapshotsDeletedOlderThan(1, olderThanVersion, aggregateType, aggregateId);
			}
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.Firestore,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
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

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
	}

	/// <summary>
	/// Builds the document id from the tenant term, aggregate type and aggregate id.
	/// </summary>
	/// <param name="aggregateType">The aggregate type.</param>
	/// <param name="aggregateId">The aggregate ID.</param>
	/// <returns>A composite document ID.</returns>
	/// <remarks>
	/// <para>
	/// Uses "_" as the separator to match the convention this provider's grant store already follows,
	/// rather than importing the ":" shape used by the other document providers. A Firestore document id
	/// may not contain "/", so the separator choice is constrained. The escaping rule, and why the join is
	/// injective, is stated once on the shared composer, which this method delegates to rather than keeping
	/// a second copy of.
	/// </para>
	/// <para>
	/// Every id carries a tenant segment, including an untenanted host's: the tenant term is total, so it
	/// always yields the reserved untenanted value rather than nothing. There is deliberately no
	/// tenant-less id shape -- one shape per document means a read and a write can never disagree about
	/// which of two shapes to address, which is the failure a second, tenant-omitting form would admit.
	/// Documents written by a build that predates the tenant segment carry the older, tenant-less id and
	/// are not addressed by this one. That is tolerable here and only here: a snapshot whose key misses is
	/// simply not found, and the aggregate rebuilds from its event stream, so no migration is required to
	/// reach them.
	/// </para>
	/// </remarks>
	private string CreateDocumentId(string aggregateType, string aggregateId) =>
		FirestoreDocumentId.Compose(TenantSegmentMarker, CurrentTenantScope.TenantId, aggregateType, aggregateId);

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object> ToFirestoreDocument(ISnapshot snapshot)
	{
		var doc = new Dictionary<string, object>
		{
			["snapshotId"] = snapshot.SnapshotId,
			["aggregateId"] = snapshot.AggregateId,
			["aggregateType"] = snapshot.AggregateType,
			["version"] = snapshot.Version,
			["createdAt"] = snapshot.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
			["data"] = Blob.CopyFrom(snapshot.Data.ToArray())
		};

		// Serialize metadata as JSON string if present
		if (snapshot.Metadata is { Count: > 0 })
		{
			doc["metadata"] = JsonSerializer.Serialize(snapshot.Metadata, JsonOptions);
		}

		return doc;
	}

	/// <summary>
	/// Builds the field map for the conditional update: every field the create path writes, plus an
	/// explicit delete of the one field that is sometimes absent.
	/// </summary>
	/// <remarks>
	/// A Firestore update MERGES -- fields not named in the map are left as they are -- whereas the write
	/// this replaced overwrote the whole document. The two are the same operation only if every field is
	/// named, and <c>metadata</c> is the one field a snapshot may not carry: without the explicit delete, a
	/// snapshot with no metadata would leave the PREVIOUS snapshot's metadata attached, and a reader would
	/// hand that back as this snapshot's own. Deriving the map from <see cref="ToFirestoreDocument"/> keeps
	/// the stored shape defined in exactly one place, so the two paths cannot drift apart.
	/// </remarks>
	/// <param name="snapshot">The snapshot being stored.</param>
	/// <returns>The complete field map, with <c>metadata</c> deleted when the snapshot has none.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object> ToFirestoreUpdate(ISnapshot snapshot)
	{
		var document = ToFirestoreDocument(snapshot);

		if (!document.ContainsKey("metadata"))
		{
			document["metadata"] = FieldValue.Delete;
		}

		return document;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static ISnapshot FromFirestoreDocument(DocumentSnapshot doc)
	{
		Blob? dataBlob = doc.TryGetValue<Blob>("data", out var blob) ? blob : null;

		IDictionary<string, object>? metadata = null;
		if (doc.TryGetValue<string>("metadata", out var metadataJson) && !string.IsNullOrEmpty(metadataJson))
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson, JsonOptions);
		}

		return new Snapshot
		{
			SnapshotId = doc.GetValue<string>("snapshotId"),
			AggregateId = doc.GetValue<string>("aggregateId"),
			AggregateType = doc.GetValue<string>("aggregateType"),
			Version = doc.GetValue<long>("version"),
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
			Data = dataBlob?.ByteString.ToByteArray() ?? [],
			Metadata = metadata
		};
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

#pragma warning disable CS0618 // CredentialsPath/JsonCredentials are obsolete but replacements require significant refactoring
		if (!string.IsNullOrEmpty(_options.CredentialsPath))
		{
			builder.CredentialsPath = _options.CredentialsPath;
		}
		else if (!string.IsNullOrEmpty(_options.CredentialsJson))
		{
			builder.JsonCredentials = _options.CredentialsJson;
		}
#pragma warning restore CS0618

		_db = await builder.BuildAsync().ConfigureAwait(false);
		_collection = _db.Collection(_options.CollectionName);
		_initialized = true;

		}

		finally

		{

			_ = _initLock.Release();

		}
	}

	// Logging methods using LoggerMessage source generator
	[LoggerMessage(DataFirestoreEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataFirestoreEventId.SnapshotVersionSkipped, LogLevel.Debug,
		"Skipped older snapshot for {AggregateType}/{AggregateId} at version {Version} (existing version: {ExistingVersion})")]
	private partial void LogSnapshotVersionSkipped(string aggregateType, string aggregateId, long version, long existingVersion);

	[LoggerMessage(DataFirestoreEventId.SnapshotRetrieved, LogLevel.Debug,
		"Retrieved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotRetrieved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataFirestoreEventId.SnapshotDeleted, LogLevel.Debug, "Deleted snapshot for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotDeleted(string aggregateType, string aggregateId);

	[LoggerMessage(DataFirestoreEventId.SnapshotsDeletedOlderThan, LogLevel.Information,
		"Deleted {Count} snapshots older than version {Version} for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotsDeletedOlderThan(int count, long version, string aggregateType, string aggregateId);

	[LoggerMessage(DataFirestoreEventId.SnapshotWriteRetried, LogLevel.Debug,
		"Contended snapshot write for {AggregateType}/{AggregateId} at version {Version} lost attempt {Attempt}; re-reading")]
	private partial void LogContendedWriteRetried(string aggregateType, string aggregateId, long version, int attempt);
}
