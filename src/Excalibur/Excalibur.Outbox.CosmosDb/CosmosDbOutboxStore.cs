// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native outbox store.
/// </summary>
public sealed partial class CosmosDbOutboxStore : ICloudNativeOutboxStore, ICloudNativeOutboxStoreBatch, ICloudNativeOutboxStoreClaim, IAsyncDisposable, ITenantPartitionedStore
{
	private readonly CosmosDbOutboxOptions _options;
	private readonly ILogger<CosmosDbOutboxStore> _logger;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private CosmosClient? _client;

	// Whether this store built the client it holds. A borrowed client belongs to the container that
	// registered it, and disposing something you did not create is how one feature's shutdown breaks
	// another feature that is still running against the same account.
	private bool _ownsClient;
	private Database? _database;
	private Container? _container;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="checkpointStore">
	/// Optional durable change-feed checkpoint store (DI-supplied; the registered
	/// <see cref="IChangeFeedCheckpointStore"/> — in-memory default or the durable Cosmos store when the
	/// consumer opts in). Flowed into the outbox change-feed subscription so continuation survives restarts.
	/// <see langword="null"/> only for manual construction without DI (in-memory-only).
	/// </param>
	public CosmosDbOutboxStore(
		IOptions<CosmosDbOutboxOptions> options,
		ILogger<CosmosDbOutboxStore> logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;
		_options.Validate();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxStore"/> class over a client supplied by
	/// the caller.
	/// </summary>
	/// <remarks>
	/// This is the constructor dependency injection selects when a <see cref="CosmosClient"/> is registered,
	/// and it is the reason a host that enables several Cosmos features opens one connection pool rather
	/// than one per feature. The client's lifetime belongs to whoever registered it, so this store never
	/// disposes it.
	/// </remarks>
	/// <param name="client">The Cosmos client to borrow. Its lifetime belongs to the caller.</param>
	/// <param name="options">The Cosmos DB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="checkpointStore">
	/// Optional durable change-feed checkpoint store, as for the other constructor.
	/// </param>
	public CosmosDbOutboxStore(
		CosmosClient client,
		IOptions<CosmosDbOutboxOptions> options,
		ILogger<CosmosDbOutboxStore> logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
		: this(options, logger, checkpointStore)
	{
		ArgumentNullException.ThrowIfNull(client);

		_client = client;
		_ownsClient = false;
	}

	/// <inheritdoc/>
	public CloudPersistenceProviderType ProviderType => CloudPersistenceProviderType.CosmosDb;

	/// <summary>
	/// Initializes the Cosmos DB client, database, and container.
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

			LogInitializing(_options.ContainerName);

			// Only build a client when none was supplied. A store that overwrites an injected client would
			// leave the registered singleton unused while still opening a second connection pool.
			if (_client is null)
			{
				_client = CosmosDbOutboxClientFactory.Create(_options);
				_ownsClient = true;
			}

			_database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, "/partitionKey")
				{
					// -1 enables the TTL subsystem while expiring nothing by default, so an item's own ttl
					// decides its lifetime. A POSITIVE value here would expire EVERY item that does not
					// override it -- including messages that have not been delivered yet, which is the one
					// thing an outbox exists to prevent. Retention is applied per message on publish.
					DefaultTimeToLive = -1
				};

				var response = await _database.CreateContainerIfNotExistsAsync(
					containerProperties,
					_options.ContainerThroughput,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = _database.GetContainer(_options.ContainerName);
			}

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
		var document = CosmosDbOutboxDocumentMap.ToDocument(message, partitionKey);
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);

		try
		{
			var response = await _container!.CreateItemAsync(
				document,
				cosmosPartitionKey,
				new ItemRequestOptions { EnableContentResponseOnWrite = true },
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Add", response.RequestCharge);

			var resultMessage = CosmosDbOutboxDocumentMap.FromDocument(response.Resource);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: true,
				statusCode: (int)response.StatusCode,
				requestCharge: response.RequestCharge,
				document: resultMessage,
				etag: response.ETag,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"add",
				message.MessageId,
				message.CorrelationId,
				message.CausationId);
			LogOperationFailed("Add", ex.Message, ex);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
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
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);
		var batch = _container!.CreateTransactionalBatch(cosmosPartitionKey);

		foreach (var message in messages)
		{
			var document = CosmosDbOutboxDocumentMap.ToDocument(message, partitionKey);
			_ = batch.CreateItem(document);
		}

		try
		{
			using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("AddBatch", response.RequestCharge);

			var operationResults = new List<CloudOperationResult>();
			for (var i = 0; i < response.Count; i++)
			{
				var opResult = response.GetOperationResultAtIndex<object>(i);
				operationResults.Add(new CloudOperationResult(
					success: opResult.IsSuccessStatusCode,
					statusCode: (int)opResult.StatusCode,
					requestCharge: 0,
					etag: opResult.ETag));
			}

			if (!response.IsSuccessStatusCode)
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: response.IsSuccessStatusCode,
				requestCharge: response.RequestCharge,
				operationResults: operationResults,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"add_batch");
			LogOperationFailed("AddBatch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: ex.RequestCharge,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
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
		var query = new QueryDefinition(
				"SELECT * FROM c WHERE c.partitionKey = @pk AND c.isPublished = false ORDER BY c.createdAt ASC")
			.WithParameter("@pk", partitionKey.Value);

		var queryOptions = new QueryRequestOptions { PartitionKey = new CosmosPartitionKey(partitionKey.Value), MaxItemCount = batchSize };

		try
		{
			var messages = new List<CloudOutboxMessage>();
			double totalRequestCharge = 0;
			string? continuationToken = null;
			string? sessionToken = null;

			var iterator = _container!.GetItemQueryIterator<CosmosDbOutboxDocument>(query, requestOptions: queryOptions);

			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				foreach (var doc in response.Resource)
				{
					messages.Add(CosmosDbOutboxDocumentMap.FromDocument(doc));
				}

				totalRequestCharge += response.RequestCharge;
				continuationToken = response.ContinuationToken;
				sessionToken = response.Headers.Session;
			}

			LogOperationCompleted("GetPending", totalRequestCharge);

			return new CloudQueryResult<CloudOutboxMessage>(
				messages,
				totalRequestCharge,
				continuationToken,
				sessionToken);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"get_pending");
			LogOperationFailed("GetPending", ex.Message, ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"get_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The atomic step is <c>ReplaceItem</c> under <c>IfMatchEtag</c>. Cosmos admits the replace only while
	/// the document still carries the ETag this claimant read, so of two claimants that read the same
	/// document exactly one replace succeeds and the other is refused with <c>412 PreconditionFailed</c>.
	/// That refusal is the mechanism working, not a fault: the loser simply does not get the message.
	/// </para>
	/// <para>
	/// The query that precedes it only nominates candidates. It cannot itself exclude a competitor — two
	/// claimants querying at the same instant see the same rows — so nothing is decided until the
	/// conditional replace lands. A plain upsert here would be silently wrong: an upsert has no
	/// precondition to fail, so both claimants would succeed and both would publish.
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
			var candidates = await QueryClaimableIdsAsync(partitionKey, batchSize, leaseCutoff, cancellationToken)
				.ConfigureAwait(false);

			var totalRequestCharge = candidates.RequestCharge;
			var sessionToken = candidates.SessionToken;
			var claimed = new List<CloudOutboxMessage>(candidates.Ids.Count);

			foreach (var messageId in candidates.Ids)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var attempt = await TryClaimOneAsync(messageId, partitionKey, claimantId, leaseCutoff, cancellationToken)
					.ConfigureAwait(false);

				totalRequestCharge += attempt.RequestCharge;
				sessionToken = attempt.SessionToken ?? sessionToken;

				if (attempt.Claimed is not null)
				{
					claimed.Add(attempt.Claimed);
				}
			}

			LogOperationCompleted("ClaimPending", totalRequestCharge);

			return new CloudQueryResult<CloudOutboxMessage>(claimed, totalRequestCharge, continuationToken: null, sessionToken);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"claim_pending");
			LogOperationFailed("ClaimPending", ex.Message, ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"claim_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <summary>
	/// Nominates the documents that look claimable right now.
	/// </summary>
	/// <remarks>
	/// This decides nothing. Two claimants querying at the same instant see the same identifiers; exclusion
	/// happens later, at the conditional replace. Its only job is to keep the conditional writes off
	/// documents that obviously cannot be claimed.
	/// </remarks>
	/// <param name="partitionKey">The partition to read.</param>
	/// <param name="batchSize">The maximum number of identifiers to return.</param>
	/// <param name="leaseCutoff">The instant before which a stamped lease has expired.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The candidate identifiers, with the cost of finding them.</returns>
	private async Task<(IReadOnlyList<string> Ids, double RequestCharge, string? SessionToken)> QueryClaimableIdsAsync(
		IPartitionKey partitionKey,
		int batchSize,
		string leaseCutoff,
		CancellationToken cancellationToken)
	{
		// The lease instant is stored round-trip ("o") in UTC, which is fixed width and therefore orders
		// correctly under the ordinal string comparison Cosmos applies to this range.
		var query = new QueryDefinition(
				"SELECT c.id FROM c WHERE c.partitionKey = @pk AND c.isPublished = false " +
				"AND (NOT IS_DEFINED(c.leasedAt) OR IS_NULL(c.leasedAt) OR c.leasedAt < @leaseCutoff) " +
				"ORDER BY c.createdAt ASC")
			.WithParameter("@pk", partitionKey.Value)
			.WithParameter("@leaseCutoff", leaseCutoff);

		var queryOptions = new QueryRequestOptions
		{
			PartitionKey = new CosmosPartitionKey(partitionKey.Value),
			MaxItemCount = batchSize
		};

		var ids = new List<string>(batchSize);
		double requestCharge = 0;
		string? sessionToken = null;

		var iterator = _container!.GetItemQueryIterator<CosmosDbOutboxIdProjection>(query, requestOptions: queryOptions);
		while (iterator.HasMoreResults && ids.Count < batchSize)
		{
			var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			requestCharge += page.RequestCharge;
			sessionToken = page.Headers.Session;

			foreach (var candidate in page.Resource)
			{
				ids.Add(candidate.Id);
				if (ids.Count == batchSize)
				{
					break;
				}
			}
		}

		return (ids, requestCharge, sessionToken);
	}

	/// <summary>
	/// Attempts to win one document, stamping the lease under an ETag precondition.
	/// </summary>
	/// <param name="messageId">The document to claim.</param>
	/// <param name="partitionKey">The partition the document lives in.</param>
	/// <param name="claimantId">The claimant to record as the lease owner.</param>
	/// <param name="leaseCutoff">The instant before which an existing lease has expired.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The claimed message when this claimant won, otherwise <see langword="null"/>, with the cost.</returns>
	private async Task<(CloudOutboxMessage? Claimed, double RequestCharge, string? SessionToken)> TryClaimOneAsync(
		string messageId,
		IPartitionKey partitionKey,
		string claimantId,
		string leaseCutoff,
		CancellationToken cancellationToken)
	{
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);
		double requestCharge = 0;

		CosmosDbOutboxDocument? document = null;
		string? readEtag = null;

		try
		{
			var readResponse = await _container!.ReadItemAsync<CosmosDbOutboxDocument>(
				messageId,
				cosmosPartitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			requestCharge += readResponse.RequestCharge;
			document = readResponse.Resource;
			readEtag = readResponse.ETag;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Deleted or aged out between the query and this read. Nothing to claim.
			requestCharge += ex.RequestCharge;
		}

		// Re-check against the document we are about to replace: the query's view is already stale, and the
		// ETag alone would not stop us taking a message another claimant leased in the meantime.
		if (document is null || document.IsPublished || !IsLeaseClaimable(document.LeasedAt, leaseCutoff))
		{
			return (null, requestCharge, null);
		}

		// The stamp is taken HERE, immediately before the write that establishes the lease, and not at the
		// start of the drain. A batch-start instant would hand the last message of an N-message batch a
		// lease that has already burned the query round-trip plus N-1 conditional writes, so its protective
		// interval would shrink as the batch grows -- and the lease is the only thing standing between a
		// slow drain and a second dispatcher publishing the same message. The eligibility cutoff is
		// deliberately NOT re-anchored: it stays at the batch-start value, because an older cutoff is the
		// conservative direction (it judges fewer leases expired) and so cannot admit a live lease.
		document.LeasedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		document.LeasedBy = claimantId;

		try
		{
			var replaceResponse = await _container!.ReplaceItemAsync(
				document,
				messageId,
				cosmosPartitionKey,
				new ItemRequestOptions { IfMatchEtag = readEtag },
				cancellationToken).ConfigureAwait(false);

			requestCharge += replaceResponse.RequestCharge;
			var claimed = CosmosDbOutboxDocumentMap.FromDocument(document) with { ETag = replaceResponse.ETag };
			return (claimed, requestCharge, replaceResponse.Headers.Session);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			// Another claimant replaced the document first. Expected under concurrency — this is the
			// exclusion doing its job, so it is neither logged as a failure nor retried here.
			return (null, requestCharge + ex.RequestCharge, null);
		}
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
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);

		try
		{
			// Read the existing document first
			var readResponse = await _container!.ReadItemAsync<CosmosDbOutboxDocument>(
				messageId,
				cosmosPartitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = readResponse.Resource;
			document.IsPublished = true;
			document.PublishedAt = DateTimeOffset.UtcNow.ToString("o");

			// If TTL is configured, set it on published messages
			if (_options.DefaultTimeToLiveSeconds > 0)
			{
				document.Ttl = _options.DefaultTimeToLiveSeconds;
			}

			var replaceResponse = await _container!.ReplaceItemAsync(
				document,
				messageId,
				cosmosPartitionKey,
				new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("MarkAsPublished", readResponse.RequestCharge + replaceResponse.RequestCharge);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)replaceResponse.StatusCode,
				requestCharge: readResponse.RequestCharge + replaceResponse.RequestCharge,
				etag: replaceResponse.ETag,
				sessionToken: replaceResponse.Headers.Session);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"mark_published",
				messageId);
			LogOperationFailed("MarkAsPublished", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
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
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);
		var publishedAt = DateTimeOffset.UtcNow.ToString("o");
		double totalRequestCharge = 0;
		var operationResults = new List<CloudOperationResult>();

		// Read all documents first
		var documents = new List<(CosmosDbOutboxDocument Doc, string ETag)>();
		foreach (var messageId in messageIds)
		{
			try
			{
				var response = await _container!.ReadItemAsync<CosmosDbOutboxDocument>(
					messageId,
					cosmosPartitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);
				totalRequestCharge += response.RequestCharge;
				documents.Add((response.Resource, response.ETag));
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				operationResults.Add(new CloudOperationResult(false, 404, ex.RequestCharge, errorMessage: "Not found"));
				totalRequestCharge += ex.RequestCharge;
			}
		}

		// Update all documents in a batch
		var batch = _container!.CreateTransactionalBatch(cosmosPartitionKey);
		foreach (var (doc, etag) in documents)
		{
			doc.IsPublished = true;
			doc.PublishedAt = publishedAt;
			if (_options.DefaultTimeToLiveSeconds > 0)
			{
				doc.Ttl = _options.DefaultTimeToLiveSeconds;
			}

			_ = batch.ReplaceItem(doc.Id, doc, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });
		}

		try
		{
			using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
			totalRequestCharge += response.RequestCharge;

			LogOperationCompleted("MarkBatchAsPublished", totalRequestCharge);

			for (var i = 0; i < response.Count; i++)
			{
				var opResult = response.GetOperationResultAtIndex<object>(i);
				operationResults.Add(new CloudOperationResult(
					success: opResult.IsSuccessStatusCode,
					statusCode: (int)opResult.StatusCode,
					requestCharge: 0,
					etag: opResult.ETag));
			}

			if (operationResults.Any(r => !r.Success))
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: response.IsSuccessStatusCode,
				requestCharge: totalRequestCharge,
				operationResults: operationResults,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"mark_batch_published");
			LogOperationFailed("MarkBatchAsPublished", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: totalRequestCharge + ex.RequestCharge,
				operationResults: operationResults,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
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
		var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToString("o");
		var query = new QueryDefinition(
				"SELECT c.id FROM c WHERE c.partitionKey = @pk AND c.isPublished = true AND c.publishedAt < @cutoff")
			.WithParameter("@pk", partitionKey.Value)
			.WithParameter("@cutoff", cutoffDate);

		var queryOptions = new QueryRequestOptions { PartitionKey = new CosmosPartitionKey(partitionKey.Value), MaxItemCount = 100 };

		try
		{
			var deletedCount = 0;
			double totalRequestCharge = 0;

			var iterator = _container!.GetItemQueryIterator<CosmosDbOutboxIdProjection>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRequestCharge += response.RequestCharge;

				foreach (var item in response.Resource)
				{
					try
					{
						var deleteResponse = await _container!.DeleteItemAsync<object>(
							item.Id,
							new CosmosPartitionKey(partitionKey.Value),
							cancellationToken: cancellationToken).ConfigureAwait(false);
						totalRequestCharge += deleteResponse.RequestCharge;
						deletedCount++;
					}
					catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
					{
						totalRequestCharge += ex.RequestCharge;
					}
				}
			}

			LogOperationCompleted("CleanupOldMessages", totalRequestCharge);

			return new CloudCleanupResult(deletedCount, totalRequestCharge);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"cleanup_old");
			LogOperationFailed("CleanupOldMessages", ex.Message, ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"cleanup_old",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA2000:Dispose objects before losing scope",
		Justification = "Ownership of subscription transfers to caller on successful return; disposed on failure path.")]
	public async Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		CosmosDbOutboxChangeFeedSubscription? subscription = null;

		try
		{
			subscription = new CosmosDbOutboxChangeFeedSubscription(
				_container!,
				options ?? ChangeFeedOptions.Default,
				_logger,
				_checkpointStore);

			await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
			return subscription;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			if (subscription is not null)
			{
				await subscription.DisposeAsync().ConfigureAwait(false);
			}

			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
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
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);

		try
		{
			var readResponse = await _container!.ReadItemAsync<CosmosDbOutboxDocument>(
				messageId,
				cosmosPartitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = readResponse.Resource;
			document.RetryCount++;
			document.LastError = errorMessage;

			var replaceResponse = await _container!.ReplaceItemAsync(
				document,
				messageId,
				cosmosPartitionKey,
				new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("IncrementRetryCount", readResponse.RequestCharge + replaceResponse.RequestCharge);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)replaceResponse.StatusCode,
				requestCharge: readResponse.RequestCharge + replaceResponse.RequestCharge,
				etag: replaceResponse.ETag,
				sessionToken: replaceResponse.Headers.Session);
		}
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"increment_retry",
				messageId);
			LogOperationFailed("IncrementRetryCount", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"increment_retry",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_ownsClient)
		{
			_client?.Dispose();
		}
		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
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
