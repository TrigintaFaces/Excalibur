// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;
using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Inbox.Observability;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.Firestore;

/// <summary>
/// Firestore-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses CreateAsync for atomic first-writer-wins semantics.
/// Document path: {CollectionName}/{documentId}, where the id is composed injectively from the
/// tenant, message id, and handler type. Percent-encoding the terms also escapes '/', which Firestore
/// reads as a path separator — an unescaped one in a message id addressed a nested collection, so the
/// entry was written somewhere the matching read never looked.
/// Catches RpcException with StatusCode.AlreadyExists for conflict detection.
/// </remarks>
public sealed partial class FirestoreInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin, IAsyncDisposable
{
	/// <summary>Bounded retries for the precondition-guarded conditional delete in <see cref="ReleaseAsync"/>.</summary>
	private const int ReleaseMaxRetries = 5;

	/// <summary>
	/// Test-only seam: when non-null, invoked once inside <see cref="ReleaseAsync"/> in the window between
	/// the status read and the conditional delete, so a test can deterministically interleave a concurrent
	/// finalize and exercise the conditional-delete guard. Always <see langword="null"/> in production
	/// (single null-check ⇒ zero overhead).
	/// </summary>
	internal Func<CancellationToken, Task>? ReleaseRaceHookForTests { get; set; }

	private readonly FirestoreInboxOptions _options;
	private readonly ILogger<FirestoreInboxStore> _logger;
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);

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
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public FirestoreInboxStore(
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public FirestoreInboxStore(
		FirestoreDb db,
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
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
		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		var data = CreateDocumentData(entry);

		try
		{
			// CreateAsync fails if document already exists
			_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);
			LogCreatedEntry(_logger, messageId, handlerType, null);
			return entry;
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
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

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Atomic finalize: read-then-CONDITIONAL-update guarded by Precondition.LastUpdated so a concurrent
		// transition between our read and write fails the precondition (re-read + re-evaluate) instead of
		// blindly overwriting — closing the TOCTOU race that let two consumers both finalize. Mirrors the
		// conditional-delete guard in ReleaseAsync.
		for (var attempt = 0; attempt < ReleaseMaxRetries; attempt++)
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			if (snapshot.GetValue<int>("status") == (int)InboxStatus.Processed)
			{
				throw new InvalidOperationException(
					$"Inbox entry already processed for message '{messageId}' and handler '{handlerType}'.");
			}

			var precondition = snapshot.UpdateTime is { } updatedAt
				? Precondition.LastUpdated(updatedAt)
				: Precondition.MustExist;

			try
			{
				_ = await docRef.UpdateAsync(
					new Dictionary<string, object>
					{
						["status"] = (int)InboxStatus.Processed,
						["processedAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
						["lastError"] = FieldValue.Delete
					}, precondition, cancellationToken: cancellationToken).ConfigureAwait(false);

				LogProcessedEntry(_logger, messageId, handlerType, null);
				return;
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
			{
				// Concurrent transition — re-read and re-evaluate (terminal check above) on the next iteration.
			}
		}

		throw new InvalidOperationException(
			$"Failed to mark inbox entry as processed for message '{messageId}' and handler '{handlerType}' after {ReleaseMaxRetries} attempts due to concurrent modification.");
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Atomic guarded transition: never downgrade a concurrently-finalized (Processed) entry back to
		// Processing (would re-admit the message → double-processing). Conditional update guarded by
		// Precondition.LastUpdated; refused/finalized → no-op.
		for (var attempt = 0; attempt < ReleaseMaxRetries; attempt++)
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			if (snapshot.GetValue<int>("status") == (int)InboxStatus.Processed)
			{
				_logger.LogDebug(
					"Skipped Processing transition for message {MessageId} and handler {HandlerType} (already finalized)",
					messageId, handlerType);
				return;
			}

			var precondition = snapshot.UpdateTime is { } updatedAt
				? Precondition.LastUpdated(updatedAt)
				: Precondition.MustExist;

			try
			{
				_ = await docRef.UpdateAsync(
					new Dictionary<string, object>
					{
						["status"] = (int)InboxStatus.Processing,
						["lastAttemptAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
					}, precondition, cancellationToken: cancellationToken).ConfigureAwait(false);

				_logger.LogDebug("Marked inbox entry as processing for message {MessageId} and handler {HandlerType}", messageId, handlerType);
				return;
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
			{
				// Concurrent transition — re-read and re-evaluate on the next iteration.
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Create a minimal document for first-writer-wins
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object>
		{
			["messageId"] = messageId,
			["handlerType"] = handlerType,
			["messageType"] = "Unknown",
			["status"] = (int)InboxStatus.Processed,
			["processedAt"] = Timestamp.FromDateTimeOffset(now),
			["receivedAt"] = Timestamp.FromDateTimeOffset(now)
		};

		// Aborted ("Transaction lock timeout") is Firestore's documented RETRYABLE contention signal, and
		// contention is the normal case on this path, not an edge case: this method exists to arbitrate
		// concurrent redelivery of the SAME message, so racing writers target the SAME document by design.
		// Previously only AlreadyExists was caught, so an Aborted escaped as a raw RpcException and the
		// method threw instead of returning the true/false its contract promises — the caller saw a handler
		// failure and the dedup outcome for that delivery was left ambiguous.
		//
		// Which failures are transient, how many attempts, and how long to wait are the provider's shared
		// retry policy's answer, not this method's. A second answer written here is how the two contending
		// paths on this document came to disagree with each other and with the policy the provider
		// advertises. The retry is bounded inside the executor rather than by a catch filter here.
		return await FirestoreRetryExecutor.ExecuteAsync(
			async ct =>
			{
				try
				{
					// CreateAsync fails if document already exists
					_ = await docRef.CreateAsync(data, ct).ConfigureAwait(false);
					LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
					return true;
				}
				catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
				{
				// First-writer-wins resolved against us.
				//
				// AMBIGUITY, stated because it is not removable without a schema change: if a PRIOR attempt
				// in this loop actually committed but its response was lost, the winner was us, and we still
				// report false. Distinguishing the two needs a writer identity stamped on the document.
					// False is the safe direction here — the framework's own caller
					// (the inbox middleware) invokes this AFTER the handler has already succeeded and
					// discards the result, so a conservative "duplicate" cannot skip work; and for any consumer
					// using the result as a claim, refusing a claim we may not hold is the fail-safe answer.
					LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
					return false;
				}
			},
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state. CreateAsync fails with
		// AlreadyExists on conflict (already claimed/processed) => not claimed. Finalized via MarkProcessedAsync,
		// removed via ReleaseAsync.
		//
		// This path contends for the same document as TryMarkAsProcessedAsync and for the same reason, so it
		// needs the same treatment of transient contention. It did not have it: a first Aborted left here as
		// a raw RpcException, so the claim threw under exactly the concurrency it exists to arbitrate, and a
		// caller asking "did I win this message" got an exception instead of an answer.
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object>
		{
			["messageId"] = messageId,
			["handlerType"] = handlerType,
			["messageType"] = "Unknown",
			["status"] = (int)InboxStatus.Processing,
			["receivedAt"] = Timestamp.FromDateTimeOffset(now)
		};

		return await FirestoreRetryExecutor.ExecuteAsync(
			async ct =>
			{
				try
				{
					_ = await docRef.CreateAsync(data, ct).ConfigureAwait(false);
					LogTryClaimSuccess(_logger, messageId, handlerType, null);
					return true;
				}
				catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
				{
					LogTryClaimDuplicate(_logger, messageId, handlerType, null);
					return false;
				}
			},
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Atomic delete-unless-Processed. Capture the snapshot's update time on read and issue a CONDITIONAL
		// delete (Precondition.LastUpdated). A concurrent MarkProcessed updates the document, so our delete
		// fails with FailedPrecondition instead of removing a now-finalized entry — we then re-read and no-op
		// if it has become Processed. This closes the read-then-delete race the plain delete left open.
		for (var attempt = 0; attempt < ReleaseMaxRetries; attempt++)
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			// Never delete a finalized (Processed) entry; no-op if absent or already finalized.
			if (!snapshot.Exists || snapshot.GetValue<int>("status") == (int)InboxStatus.Processed)
			{
				return;
			}

			// Test-only seam (null in production): lets a test interleave a concurrent finalize in the
			// read-then-delete window so the conditional-delete guard can be exercised deterministically.
			if (ReleaseRaceHookForTests is { } raceHook)
			{
				await raceHook(cancellationToken).ConfigureAwait(false);
			}

			var precondition = snapshot.UpdateTime is { } updatedAt
				? Precondition.LastUpdated(updatedAt)
				: Precondition.MustExist;

			try
			{
				_ = await docRef.DeleteAsync(precondition, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
			{
				// Another writer changed the document between our read and conditional delete — re-read and
				// re-evaluate (it may now be Processed → no-op).
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return false;
		}

		var status = snapshot.GetValue<int>("status");
		return status == (int)InboxStatus.Processed;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		return SnapshotToEntry(snapshot);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		await MarkFailedConditionalAsync(
			docRef,
			new Dictionary<string, object>
			{
				["status"] = (int)InboxStatus.Failed,
				["lastError"] = errorMessage,
				["lastAttemptAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
				["retryCount"] = FieldValue.Increment(1)
			},
			messageId,
			handlerType,
			cancellationToken).ConfigureAwait(false);

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

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Set retryCount EXACTLY (not FieldValue.Increment) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt.
		await MarkFailedConditionalAsync(
			docRef,
			new Dictionary<string, object>
			{
				["status"] = (int)InboxStatus.Failed,
				["lastError"] = errorMessage,
				["lastAttemptAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
				["retryCount"] = retryCount
			},
			messageId,
			handlerType,
			cancellationToken).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	// Atomic guarded Failed transition: conditional update guarded by Precondition.LastUpdated so a concurrent
	// finalize is never overwritten; refuse to downgrade a terminal Processed entry (no-op). Throws if the
	// entry is absent. Mirrors the conditional-delete guard in ReleaseAsync.
	private static async Task MarkFailedConditionalAsync(
		DocumentReference docRef,
		Dictionary<string, object> updates,
		string messageId,
		string handlerType,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < ReleaseMaxRetries; attempt++)
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (!snapshot.Exists)
			{
				throw new InvalidOperationException(
					$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
			}

			// Never downgrade a finalized (Processed) entry to Failed → no-op.
			if (snapshot.GetValue<int>("status") == (int)InboxStatus.Processed)
			{
				return;
			}

			var precondition = snapshot.UpdateTime is { } updatedAt
				? Precondition.LastUpdated(updatedAt)
				: Precondition.MustExist;

			try
			{
				_ = await docRef.UpdateAsync(updates, precondition, cancellationToken: cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
			{
				// Concurrent transition — re-read and re-evaluate on the next iteration.
			}
		}

		throw new InvalidOperationException(
			$"Failed to mark inbox entry as failed for message '{messageId}' and handler '{handlerType}' after {ReleaseMaxRetries} attempts due to concurrent modification.");
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var query = _collection!
			.WhereEqualTo("status", (int)InboxStatus.Failed)
			.WhereLessThan("retryCount", maxRetries);

		if (olderThan.HasValue)
		{
			query = query.WhereLessThan("lastAttemptAt", Timestamp.FromDateTimeOffset(olderThan.Value));
		}

		query = query.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(SnapshotToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var snapshot = await _collection!.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(SnapshotToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Firestore doesn't support COUNT aggregation natively without reading documents
		// For efficiency, we query each status separately with a limit of 0
		// This requires reading all documents for accurate counts

		var allDocs = await _collection!.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		foreach (var doc in allDocs.Documents)
		{
			total++;
			var status = doc.GetValue<int>("status");

			switch ((InboxStatus)status)
			{
				case InboxStatus.Processed:
					processed++;
					break;

				case InboxStatus.Failed:
					failed++;
					break;

				case InboxStatus.Received:
				case InboxStatus.Processing:
					pending++;
					break;

				default:
					break;
			}
		}

		return new InboxStatistics { TotalEntries = total, ProcessedEntries = processed, FailedEntries = failed, PendingEntries = pending };
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var cutoff = olderThan;

		var query = _collection!
			.WhereEqualTo("status", (int)InboxStatus.Processed)
			.WhereLessThan("processedAt", Timestamp.FromDateTimeOffset(cutoff));

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var deleted = 0;
		var batch = _db!.StartBatch();
		const int maxBatchSize = 500; // Firestore batch limit

		foreach (var doc in snapshot.Documents)
		{
			_ = batch.Delete(doc.Reference);
			deleted++;

			if (deleted % maxBatchSize == 0)
			{
				_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
				batch = _db!.StartBatch();
			}
		}

		if (deleted % maxBatchSize != 0)
		{
			_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

		LogCleanedUpEntries(_logger, deleted, null);
		return deleted;
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
	/// Composes the deduplication document id for an entry, discriminated by the ambient tenant.
	/// </summary>
	/// <remarks>
	/// The tenant term is part of the id, not merely a field on the document. Keying on
	/// (messageId, handlerType) alone makes the dedup decision tenant-blind: two tenants processing
	/// messages that share a message id resolve to the same document, so the second is treated as a
	/// duplicate and silently dropped -- a cross-tenant isolation breach and a message-loss bug that
	/// fails on the success path. Note the carried tenantId field was written only when non-empty, so
	/// it could not have served this purpose even as a filter.
	/// <para>Every call site routes through here, so the write id and the lookup id cannot drift apart.</para>
	/// </remarks>
	private string GetDocumentId(string messageId, string handlerType) =>
		ComposeDocumentId(CurrentTenantPartition.TenantId, messageId, handlerType);

	/// <summary>
	/// The store's document-id contract, expressed as a pure function of its three terms so it can be
	/// exercised directly. It delegates to the shared injective composition: putting the tenant term in
	/// the id is only sound if the composition is unambiguous, and joining on a separator the terms may
	/// themselves contain is not — distinct entries render one id and the later message is dropped as a
	/// duplicate that never existed.
	/// </summary>
	/// <param name="tenantId">The tenant term the entry belongs to.</param>
	/// <param name="messageId">The message identifier being deduplicated.</param>
	/// <param name="handlerType">The handler the message is being deduplicated for.</param>
	/// <returns>The document id for the entry.</returns>
	internal static string ComposeDocumentId(string tenantId, string messageId, string handlerType)
	{
		var documentId = DocumentIdPrefix + InboxDocumentKey.Compose(tenantId, messageId, handlerType);
		InboxDocumentKey.ThrowIfExceedsIdLimit(documentId, MaxDocumentIdUtf8Bytes, "Firestore");
		return documentId;
	}

	/// <summary>
	/// Firestore refuses a document id longer than 1500 bytes. The prefix counts toward it, so the check
	/// runs on the finished id. It is checked here rather than left to the server so the failure names the
	/// cause, and so it surfaces identically on the read path.
	/// </summary>
	private const int MaxDocumentIdUtf8Bytes = 1500;

	/// <summary>
	/// Prefixes every composed document id. Firestore reserves ids matching <c>__.*__</c> and rejects a
	/// write that uses one, and the reserved untenanted tenant term is itself <c>__untenanted__</c> — so
	/// without this, every id in a deployment that does not use multi-tenancy begins <c>__</c>, and the
	/// write is rejected outright whenever the handler type name happens to end in <c>__</c>. A constant
	/// leading character the pattern cannot start with removes that whole class: the id can no longer
	/// match, whatever the terms contain.
	/// </summary>
	/// <remarks>
	/// It is a constant, so it does not affect injectivity — a constant prefix over an injective
	/// composition is still injective. It is deliberately not a version marker: a versioned id shape would
	/// invite reading more than one shape at a time, which is the compatibility fallback this store does
	/// not have and should not grow.
	/// </remarks>
	private const string DocumentIdPrefix = "inbox:";

	private static Dictionary<string, object> CreateDocumentData(InboxEntry entry)
	{
		var data = new Dictionary<string, object>
		{
			["messageId"] = entry.MessageId,
			["handlerType"] = entry.HandlerType,
			["messageType"] = entry.MessageType,
			["payload"] = Blob.CopyFrom(entry.Payload),
			["metadata"] = entry.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			["receivedAt"] = Timestamp.FromDateTimeOffset(entry.ReceivedAt),
			["status"] = (int)entry.Status,
			["retryCount"] = entry.RetryCount
		};

		if (entry.ProcessedAt.HasValue)
		{
			data["processedAt"] = Timestamp.FromDateTimeOffset(entry.ProcessedAt.Value);
		}

		if (entry.LastAttemptAt.HasValue)
		{
			data["lastAttemptAt"] = Timestamp.FromDateTimeOffset(entry.LastAttemptAt.Value);
		}

		if (!string.IsNullOrEmpty(entry.LastError))
		{
			data["lastError"] = entry.LastError;
		}

		if (!string.IsNullOrEmpty(entry.CorrelationId))
		{
			data["correlationId"] = entry.CorrelationId;
		}

		if (!string.IsNullOrEmpty(entry.TenantId))
		{
			data["tenantId"] = entry.TenantId;
		}

		if (!string.IsNullOrEmpty(entry.Source))
		{
			data["source"] = entry.Source;
		}

		return data;
	}

	private static InboxEntry SnapshotToEntry(DocumentSnapshot snapshot)
	{
		Blob? payloadBlob = snapshot.TryGetValue<Blob>("payload", out var blob) ? blob : null;
		var metadataDict = snapshot.TryGetValue<Dictionary<string, object>>("metadata", out var md) ? md : new Dictionary<string, object>();

		return new InboxEntry
		{
			MessageId = snapshot.GetValue<string>("messageId"),
			HandlerType = snapshot.GetValue<string>("handlerType"),
			MessageType = snapshot.GetValue<string>("messageType"),
			Payload = payloadBlob?.ByteString.ToByteArray() ?? [],
			Metadata = metadataDict,
			ReceivedAt = snapshot.GetValue<Timestamp>("receivedAt").ToDateTimeOffset(),
			ProcessedAt = snapshot.TryGetValue<Timestamp>("processedAt", out var processedAt) ? processedAt.ToDateTimeOffset() : null,
			Status = (InboxStatus)snapshot.GetValue<int>("status"),
			LastError = snapshot.TryGetValue<string>("lastError", out var error) ? error : null,
			RetryCount = snapshot.TryGetValue<int>("retryCount", out var retryCount) ? retryCount : 0,
			LastAttemptAt =
				snapshot.TryGetValue<Timestamp>("lastAttemptAt", out var lastAttempt) ? lastAttempt.ToDateTimeOffset() : null,
			CorrelationId = snapshot.TryGetValue<string>("correlationId", out var correlationId) ? correlationId : null,
			TenantId = snapshot.TryGetValue<string>("tenantId", out var tenantId) ? tenantId : null,
			Source = snapshot.TryGetValue<string>("source", out var source) ? source : null
		};
	}

	[LoggerMessage(DataFirestoreEventId.InboxEntryCreated, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxEntryProcessed, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryMarkProcessedSuccess, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryMarkProcessedDuplicate, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryClaimSuccess, LogLevel.Debug,
		"TryClaim succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryClaimSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryClaimDuplicate, LogLevel.Debug,
		"TryClaim detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryClaimDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxEntryFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
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
