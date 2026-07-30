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
/// Document path: {CollectionName}/{messageId}_{handlerType}
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
	private readonly ITenantContext? _tenantContext;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. An absent context
	/// resolves to the reserved untenanted term, so the document id has the same shape either way.
	/// </param>
	public FirestoreInboxStore(
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host.
	/// </param>
	public FirestoreInboxStore(
		FirestoreDb db,
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				// CreateAsync fails if document already exists
				_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);
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
				// (IdempotentHandlerMiddleware) invokes this AFTER the handler has already succeeded and
				// discards the result, so a conservative "duplicate" cannot skip work; and for any consumer
				// using the result as a claim, refusing a claim we may not hold is the fail-safe answer.
				LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
				return false;
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted && attempt < ReleaseMaxRetries - 1)
			{
				// Transient same-document contention: back off and re-attempt. Bounded, so a genuinely stuck
				// document still surfaces rather than spinning.
				await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken)
					.ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state. CreateAsync fails with
		// AlreadyExists on conflict (already claimed/processed) => not claimed. Finalized via MarkProcessedAsync,
		// removed via ReleaseAsync.
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object>
		{
			["messageId"] = messageId,
			["handlerType"] = handlerType,
			["messageType"] = "Unknown",
			["status"] = (int)InboxStatus.Processing,
			["receivedAt"] = Timestamp.FromDateTimeOffset(now)
		};

		try
		{
			_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);
			LogTryClaimSuccess(_logger, messageId, handlerType, null);
			return true;
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			LogTryClaimDuplicate(_logger, messageId, handlerType, null);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

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

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection!.Document(docId);

		// Set retryCount EXACTLY (not FieldValue.Increment) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt (FR-4).
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
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

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
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		var snapshot = await _collection!.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(SnapshotToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

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
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		await EnsureInitializedAsync().ConfigureAwait(false);

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
		$"{KeyedTenantPartition.FromContext(_tenantContext).TenantId}_{messageId}_{handlerType}";

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

	private async Task EnsureInitializedAsync()
	{
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
}
