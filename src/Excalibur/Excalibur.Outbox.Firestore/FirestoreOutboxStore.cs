// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Firestore;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

using Google.Api.Gax;
using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of the cloud-native outbox store.
/// </summary>
public sealed partial class FirestoreOutboxStore : ICloudNativeOutboxStore, ICloudNativeOutboxStoreBatch, ICloudNativeOutboxStoreClaim, IAsyncDisposable, ITenantPartitionedStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreOutboxOptions _options;
	private readonly ILogger<FirestoreOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreOutboxStore(
		IOptions<FirestoreOutboxOptions> options,
		ILogger<FirestoreOutboxStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();
	}

	/// <inheritdoc/>
	public CloudPersistenceProviderType ProviderType => CloudPersistenceProviderType.Firestore;

	/// <summary>
	/// Initializes the Firestore client.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			LogInitializing(_options.CollectionName);

			_db = await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);
			_collection = _db.Collection(_options.CollectionName);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult<CloudOutboxMessage>> AddAsync(
		CloudOutboxMessage message,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var docData = ToFirestoreDocument(message, partitionKey);
		var docRef = _collection!.Document(message.MessageId);

		try
		{
			_ = await docRef.SetAsync(docData, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Add");

			return new CloudOperationResult<CloudOutboxMessage>(
				success: true,
				statusCode: 200,
				requestCharge: 1,
				document: message);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add",
				message.MessageId,
				message.CorrelationId,
				message.CausationId);
			LogOperationFailed("Add", ex.Message, ex);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> AddBatchAsync(
		IEnumerable<CloudOutboxMessage> messages,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var messageList = messages.ToList();
		var operationResults = new List<CloudOperationResult>();

		try
		{
			// Firestore batch limit is 500
			var batches = messageList
				.Select((msg, idx) => new { msg, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.msg).ToList());

			foreach (var batch in batches)
			{
				var writeBatch = _db!.StartBatch();

				foreach (var message in batch)
				{
					var docData = ToFirestoreDocument(message, partitionKey);
					var docRef = _collection!.Document(message.MessageId);
					_ = writeBatch.Set(docRef, docData);
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);

				operationResults.AddRange(batch.Select(_ => new CloudOperationResult(
					success: true,
					statusCode: 200,
					requestCharge: 1)));
			}

			LogOperationCompleted("AddBatch");

			return new CloudBatchResult(
				success: true,
				requestCharge: messageList.Count,
				operationResults: operationResults);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add_batch");
			LogOperationFailed("AddBatch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add_batch",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudQueryResult<CloudOutboxMessage>> GetPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			// OrderBy("createdAt") is load-bearing, not cosmetic: with no explicit order Firestore
			// returns documents in an unspecified order (in practice, roughly by document ID), which is
			// not FIFO. createdAt is stored as a fixed-offset ISO-8601 string (message.CreatedAt.ToString
			// ("o") on a UTC DateTimeOffset), so lexicographic string ordering is chronological ordering.
			var query = _collection!
				.WhereEqualTo("partitionKey", partitionKey.Value)
				.WhereEqualTo("isPublished", false)
				.OrderBy("createdAt")
				.Limit(batchSize);

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			var messages = snapshot.Documents.Select(FromFirestoreDocument).ToList();

			LogOperationCompleted("GetPending");

			string? continuationToken = null;
			if (snapshot.Documents.Count == batchSize && snapshot.Documents.Count > 0)
			{
				var lastDoc = snapshot.Documents[^1];
				continuationToken = lastDoc.Id;
			}

			return new CloudQueryResult<CloudOutboxMessage>(messages, snapshot.Documents.Count, continuationToken);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"get_pending");
			LogOperationFailed("GetPending", ex.Message, ex);
			return new CloudQueryResult<CloudOutboxMessage>([], 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"get_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The atomic step is a Firestore transaction. The read of the document and the write of its lease are
	/// committed together, and Firestore aborts and re-runs the transaction if the document changed
	/// underneath it, so of two claimants transacting on the same document exactly one commits a lease.
	/// </para>
	/// <para>
	/// The query that precedes it only nominates candidates and excludes nobody. Note that the transaction
	/// body can run more than once: every decision it makes is taken from the snapshot read inside that
	/// attempt, and the value it yields is produced by that attempt. Nothing is carried between attempts, so
	/// a re-run cannot report a claim an earlier attempt made and then lost.
	/// </para>
	/// </remarks>
	public async Task<CloudQueryResult<CloudOutboxMessage>> ClaimPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		string claimantId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(partitionKey);
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ArgumentException.ThrowIfNullOrWhiteSpace(claimantId);
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var leaseCutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.LeaseTimeoutSeconds)
			.ToString("o", CultureInfo.InvariantCulture);

		try
		{
			// Candidates only. Firestore cannot express "no lease field OR an expired one" as a single
			// query — an inequality on leasedAt would silently drop every document that has never been
			// claimed — so eligibility is decided inside the transaction, where it is decided atomically
			// anyway. OrderBy("createdAt") so a claim call hands out its own batch in creation order, same
			// as GetPendingAsync and closing the gap ICloudNativeOutboxStoreClaim left undocumented.
			var query = _collection!
				.WhereEqualTo("partitionKey", partitionKey.Value)
				.WhereEqualTo("isPublished", false)
				.OrderBy("createdAt")
				.Limit(batchSize);

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			var claimed = new List<CloudOutboxMessage>(snapshot.Documents.Count);

			foreach (var candidate in snapshot.Documents)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var message = await TryClaimOneAsync(candidate.Id, claimantId, leaseCutoff, cancellationToken)
					.ConfigureAwait(false);

				if (message is not null)
				{
					claimed.Add(message);
				}
			}

			LogOperationCompleted("ClaimPending");

			return new CloudQueryResult<CloudOutboxMessage>(claimed, claimed.Count);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"claim_pending");
			LogOperationFailed("ClaimPending", ex.Message, ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"claim_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Attempts to win one document inside a transaction, stamping the lease.
	/// </summary>
	/// <param name="messageId">The document to claim.</param>
	/// <param name="claimantId">The claimant to record as the lease owner.</param>
	/// <param name="leaseCutoff">The instant before which an existing lease has expired.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The claimed message when this claimant won, otherwise <see langword="null"/>.</returns>
	private async Task<CloudOutboxMessage?> TryClaimOneAsync(
		string messageId,
		string claimantId,
		string leaseCutoff,
		CancellationToken cancellationToken)
	{
		var docRef = _collection!.Document(messageId);

		return await _db!.RunTransactionAsync(
			async transaction =>
			{
				// Every local below is declared inside the attempt, so a re-run starts from the document as
				// it is NOW rather than from anything the previous attempt observed or decided.
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				if (!snapshot.Exists)
				{
					return null;
				}

				var isPublished = snapshot.ContainsField("isPublished") && snapshot.GetValue<bool>("isPublished");
				var leasedAt = snapshot.ContainsField("leasedAt") ? snapshot.GetValue<string?>("leasedAt") : null;

				if (isPublished || !IsLeaseClaimable(leasedAt, leaseCutoff))
				{
					return null;
				}

				// The stamp is taken HERE, immediately before the write that establishes the lease, and not at the
				// start of the drain. A batch-start instant would hand the last message of an N-message batch a
				// lease that has already burned the query round-trip plus N-1 conditional writes, so its protective
				// interval would shrink as the batch grows -- and the lease is the only thing standing between a
				// slow drain and a second dispatcher publishing the same message. The eligibility cutoff is
				// deliberately NOT re-anchored: it stays at the batch-start value, because an older cutoff is the
				// conservative direction (it judges fewer leases expired) and so cannot admit a live lease.
				var nowText = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

				transaction.Update(
					docRef,
					new Dictionary<string, object> { ["leasedAt"] = nowText, ["leasedBy"] = claimantId });

				return FromFirestoreDocument(snapshot) with
				{
					LeasedAt = DateTimeOffset.Parse(nowText, CultureInfo.InvariantCulture),
					LeasedBy = claimantId
				};
			},
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Decides whether a stored lease instant leaves the message claimable, given the cutoff before which a
	/// lease has expired.
	/// </summary>
	/// <param name="leasedAt">The stored lease instant, or <see langword="null"/> / empty when unclaimed.</param>
	/// <param name="leaseCutoff">The round-trip-formatted instant before which a lease has expired.</param>
	/// <returns><see langword="true"/> when the message may be claimed.</returns>
	private static bool IsLeaseClaimable(string? leasedAt, string leaseCutoff) =>
		string.IsNullOrEmpty(leasedAt) || string.CompareOrdinal(leasedAt, leaseCutoff) < 0;

	/// <inheritdoc/>
	public async Task<CloudOperationResult> MarkAsPublishedAsync(
		string messageId,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var publishedAt = DateTimeOffset.UtcNow;
		var ttlTimestamp = _options.DefaultTimeToLiveSeconds > 0
			? Timestamp.FromDateTimeOffset(publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds))
			: (Timestamp?)null;

		try
		{
			var docRef = _collection!.Document(messageId);
			var updates = new Dictionary<string, object> { ["isPublished"] = true, ["publishedAt"] = publishedAt.ToString("o") };

			if (ttlTimestamp.HasValue)
			{
				updates["expireAt"] = ttlTimestamp.Value;
			}

			_ = await docRef.UpdateAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("MarkAsPublished");

			return new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 1);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_published",
				messageId);
			LogOperationFailed("MarkAsPublished", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> MarkBatchAsPublishedAsync(
		IEnumerable<string> messageIds,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var messageIdList = messageIds.ToList();
		var publishedAt = DateTimeOffset.UtcNow;
		var ttlTimestamp = _options.DefaultTimeToLiveSeconds > 0
			? Timestamp.FromDateTimeOffset(publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds))
			: (Timestamp?)null;

		try
		{
			var batches = messageIdList
				.Select((id, idx) => new { id, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.id).ToList());

			var operationResults = new List<CloudOperationResult>();

			foreach (var batch in batches)
			{
				var writeBatch = _db!.StartBatch();

				foreach (var messageId in batch)
				{
					var docRef = _collection!.Document(messageId);
					var updates = new Dictionary<string, object> { ["isPublished"] = true, ["publishedAt"] = publishedAt.ToString("o") };

					if (ttlTimestamp.HasValue)
					{
						updates["expireAt"] = ttlTimestamp.Value;
					}

					_ = writeBatch.Update(docRef, updates);
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);

				operationResults.AddRange(batch.Select(_ => new CloudOperationResult(
					success: true,
					statusCode: 200,
					requestCharge: 1)));
			}

			LogOperationCompleted("MarkBatchAsPublished");

			return new CloudBatchResult(
				success: true,
				requestCharge: messageIdList.Count,
				operationResults: operationResults);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_batch_published");
			LogOperationFailed("MarkBatchAsPublished", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_batch_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudCleanupResult> CleanupOldMessagesAsync(
		IPartitionKey partitionKey,
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod);
		var deletedCount = 0;

		try
		{
			var query = _collection!
				.WhereEqualTo("partitionKey", partitionKey.Value)
				.WhereEqualTo("isPublished", true)
				.WhereLessThan("publishedAt", cutoffDate.ToString("o"));

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			var batches = snapshot.Documents
				.Select((doc, idx) => new { doc, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.doc).ToList());

			foreach (var batch in batches)
			{
				var writeBatch = _db!.StartBatch();

				foreach (var doc in batch)
				{
					_ = writeBatch.Delete(doc.Reference);
					deletedCount++;
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);
			}

			LogOperationCompleted("CleanupOldMessages");
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"cleanup_old");
			LogOperationFailed("CleanupOldMessages", ex.Message, ex);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"cleanup_old",
				result,
				stopwatch.Elapsed);
		}

		return new CloudCleanupResult(deletedCount, deletedCount);
	}

	/// <inheritdoc/>
	public async Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			var subscription = new FirestoreOutboxListenerSubscription(
				_db!,
				_options,
				_logger);

			await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
			return subscription;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"subscribe_new",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> IncrementRetryCountAsync(
		string messageId,
		IPartitionKey partitionKey,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			var docRef = _collection!.Document(messageId);
			var updates = new Dictionary<string, object> { ["retryCount"] = FieldValue.Increment(1) };

			if (!string.IsNullOrEmpty(errorMessage))
			{
				updates["lastError"] = errorMessage;
			}

			_ = await docRef.UpdateAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("IncrementRetryCount");

			return new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 1);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"increment_retry",
				messageId);
			LogOperationFailed("IncrementRetryCount", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"increment_retry",
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
		_initLock?.Dispose();

		return ValueTask.CompletedTask;
	}

	private static Dictionary<string, object> ToFirestoreDocument(CloudOutboxMessage message, IPartitionKey partitionKey)
	{
		var doc = new Dictionary<string, object>
		{
			["messageId"] = message.MessageId,
			["partitionKey"] = partitionKey.Value,
			["messageType"] = message.MessageType,
			["payload"] = Convert.ToBase64String(message.Payload),
			["createdAt"] = message.CreatedAt.ToString("o"),
			["isPublished"] = message.IsPublished,
			["retryCount"] = message.RetryCount
		};

		if (message.Headers != null)
		{
#pragma warning disable IL2026, IL3050
			doc["headers"] = JsonSerializer.Serialize(message.Headers, JsonOptions);
#pragma warning restore IL2026, IL3050
		}

		if (!string.IsNullOrEmpty(message.AggregateId))
		{
			doc["aggregateId"] = message.AggregateId;
		}

		if (!string.IsNullOrEmpty(message.AggregateType))
		{
			doc["aggregateType"] = message.AggregateType;
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			doc["correlationId"] = message.CorrelationId;
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			doc["causationId"] = message.CausationId;
		}

		// Always emit the tenant field, folded through the single total conversion. An untenanted
		// message binds the reserved sentinel rather than omitting the field, converging on the same
		// representation the SQL providers and Redis outbox use for "no tenant".
		doc["tenantId"] = KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId;

		if (!string.IsNullOrEmpty(message.Destination))
		{
			doc["destination"] = message.Destination;
		}

		if (message.PublishedAt.HasValue)
		{
			doc["publishedAt"] = message.PublishedAt.Value.ToString("o");
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			doc["lastError"] = message.LastError;
		}

		return doc;
	}

	private static CloudOutboxMessage FromFirestoreDocument(DocumentSnapshot doc)
	{
		return new CloudOutboxMessage
		{
			MessageId = doc.GetValue<string>("messageId"),
			MessageType = doc.GetValue<string>("messageType"),
			Payload = Convert.FromBase64String(doc.GetValue<string>("payload")),
#pragma warning disable IL2026, IL3050
			Headers = doc.ContainsField("headers") && doc.GetValue<string?>("headers") != null
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.GetValue<string>("headers"), JsonOptions)
				: null,
#pragma warning restore IL2026, IL3050
			AggregateId = doc.ContainsField("aggregateId") ? doc.GetValue<string?>("aggregateId") : null,
			AggregateType = doc.ContainsField("aggregateType") ? doc.GetValue<string?>("aggregateType") : null,
			CorrelationId = doc.ContainsField("correlationId") ? doc.GetValue<string?>("correlationId") : null,
			CausationId = doc.ContainsField("causationId") ? doc.GetValue<string?>("causationId") : null,
			// Read-tolerant: a document written before this fix carries no tenantId field at all.
			// FromStoredValue folds a missing field the same way it folds a stored null/empty/sentinel
			// — onto Untenanted — so TenantId is never null after this store reloads a document.
			TenantId = KeyedTenantPartition.FromStoredValue(
				doc.ContainsField("tenantId") ? doc.GetValue<string?>("tenantId") : null).TenantId,
			Destination = doc.ContainsField("destination") ? doc.GetValue<string?>("destination") : null,
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture),
			PublishedAt = doc.ContainsField("publishedAt") && doc.GetValue<string?>("publishedAt") != null
				? DateTimeOffset.Parse(doc.GetValue<string>("publishedAt"), CultureInfo.InvariantCulture)
				: null,
			RetryCount = doc.ContainsField("retryCount") ? doc.GetValue<int>("retryCount") : 0,
			LastError = doc.ContainsField("lastError") ? doc.GetValue<string?>("lastError") : null,
			PartitionKeyValue = doc.GetValue<string>("partitionKey"),
			LeasedAt = doc.ContainsField("leasedAt") && doc.GetValue<string?>("leasedAt") != null
				? DateTimeOffset.Parse(doc.GetValue<string>("leasedAt"), CultureInfo.InvariantCulture)
				: null,
			LeasedBy = doc.ContainsField("leasedBy") ? doc.GetValue<string?>("leasedBy") : null
		};
	}

	private async Task<FirestoreDb> CreateDatabaseAsync(CancellationToken cancellationToken)
	{
		var builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId ?? "demo-project" };

		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			// Point this client at the configured emulator directly rather than through the process-wide
			// FIRESTORE_EMULATOR_HOST variable. That variable is first-write-wins — the helper reports a
			// conflicting value by returning false — so routing through it means a second store configured
			// for a different emulator silently talks to the first one's, and keeps doing so after that
			// endpoint is gone. An explicit endpoint is per-instance and cannot be captured by another
			// store. This matches how the dependency-injection registration already builds the client.
			builder.Endpoint = _options.EmulatorHost;
			builder.ChannelCredentials = ChannelCredentials.Insecure;
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
		{
#pragma warning disable CS0618 // Obsolete CredentialsPath/JsonCredentials
			builder.CredentialsPath = _options.CredentialsPath;
#pragma warning restore CS0618
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
		{
#pragma warning disable CS0618
			builder.JsonCredentials = _options.CredentialsJson;
#pragma warning restore CS0618
		}

		return await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void EnsureInitialized()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			throw new InvalidOperationException(
				"Outbox store has not been initialized. Call InitializeAsync first.");
		}
	}
}
