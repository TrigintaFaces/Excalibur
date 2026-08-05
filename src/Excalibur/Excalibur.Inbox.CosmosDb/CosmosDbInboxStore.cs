// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Inbox.Observability;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Cosmos DB implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides message deduplication using Cosmos DB's native document model.
/// Documents are keyed by a composite of (MessageId:HandlerType).
/// </para>
/// <para>
/// Uses handler_type as partition key for optimal query patterns where messages
/// are typically queried by handler type.
/// </para>
/// </remarks>
public sealed partial class CosmosDbInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IScopedTransactionalInboxStore, IInboxStoreCapabilities, IInboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbInboxOptions _options;
	private readonly ILogger<CosmosDbInboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// Ambient tenant context. When active, the tenant is composed INTO the dedup id (via ScopedId) so two
	// tenants' identical (messageId, handlerType) can never collide on the dedup key, and is stamped on write.
	private readonly ITenantContext? _tenantContext;

	private CosmosClient? _client;
	private Container? _container;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// Optional ambient tenant context; when active it composes the tenant into the dedup <c>id</c> and stamps
	/// the tenant on write. When <see langword="null"/> the untenanted sentinel is composed, so an untenanted
	/// deployment is a single named partition rather than an unbounded shared one.
	/// </param>
	public CosmosDbInboxStore(
		IOptions<CosmosDbInboxOptions> options,
		ILogger<CosmosDbInboxStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_tenantContext = tenantContext;
	}

	// The keyed partition seam resolves the ambient tenant to a concrete term (a real tenant, or the reserved
	// untenanted sentinel) — never null, never empty. A keyed store must never emit an empty tenant segment.
	private string TenantTerm => KeyedTenantPartition.FromContext(_tenantContext).TenantId;

	// Composes the ambient tenant into the dedup id via the keyed partition seam, so the dedup/claim key —
	// and thus every keyed read/write/claim — is tenant-isolated by construction.
	private string ScopedId(string messageId, string handlerType)
		=> CosmosDbInboxDocument.CreateId(messageId, handlerType, TenantTerm);

	/// <summary>
	/// Initializes the Cosmos DB client and container reference.
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

			var clientOptions = CreateClientOptions();
			_client = CreateClient(clientOptions);

			Database database;

			if (_options.CreateContainerIfNotExists)
			{
				// The database is provisioned too, not just the container. Both sibling Cosmos stores
				// create only the container because they run against an account that already has the
				// database; the inbox's first-run target is an EMPTY account (a fresh emulator), where
				// GetDatabase returns a handle to something that does not exist and the container create
				// then fails NotFound. Provisioning both is what makes first run work.
				var databaseResponse = await _client!
					.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
				database = databaseResponse.Database;

				var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath);

				// Enable the TTL subsystem at creation with -1 (no blanket default) rather than relying on
				// the read-then-replace below: -1 means "per-item ttl honored, nothing expires by default",
				// so an unprocessed dedup record never expires out from under an in-flight handler.
				if (_options.DefaultTimeToLiveSeconds > 0)
				{
					containerProperties.DefaultTimeToLive = -1;
				}

				var containerResponse2 = await database
					.CreateContainerIfNotExistsAsync(containerProperties, cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				_container = containerResponse2.Container;
			}
			else
			{
				database = _client!.GetDatabase(_options.DatabaseName);
				_container = database.GetContainer(_options.ContainerName);
			}

			// Verify connectivity and ensure the container's TTL subsystem is enabled. Cosmos ignores a
			// per-item `ttl` unless the container has TTL turned on; we enable it with -1 (no blanket
			// default) so ONLY terminal entries carrying a per-item ttl are reaped — unprocessed dedup
			// records never expire out from under an in-flight handler.
			var containerResponse = await _container!.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			if (_options.DefaultTimeToLiveSeconds > 0 && containerResponse.Resource.DefaultTimeToLive is null)
			{
				containerResponse.Resource.DefaultTimeToLive = -1;
				_ = await _container!.ReplaceContainerAsync(
					containerResponse.Resource,
					cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
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
		var document = CosmosDbInboxDocument.FromInboxEntry(entry);

		// Compose the ambient tenant INTO the dedup id and stamp the row, so the dedup key + every keyed read
		// isolate per tenant — two tenants' identical (messageId, handlerType) can never collide.
		document.Id = ScopedId(messageId, handlerType);
		document.TenantId = TenantTerm;

		// The stored partition-key field (handler_type) MUST equal the partition we write to, so it matches the
		// uniform partition selection: the shared partition-key value when configured, else the handler type.
		document.HandlerType = ResolvePartitionKeyValue(handlerType);

		try
		{
			_ = await _container!.CreateItemAsync(
				document,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
				cancellationToken).ConfigureAwait(false);

			LogCreatedEntry(messageId, handlerType);
			return entry;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
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

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;

			if (document.Status == (int)InboxStatus.Processed)
			{
				throw new InvalidOperationException(
					$"Message '{messageId}' for handler '{handlerType}' is already marked as processed.");
			}

			document.Status = (int)InboxStatus.Processed;
			document.ProcessedAt = DateTimeOffset.UtcNow;

			// Stamp the retention TTL now that the entry is terminal so the dedup record is reaped.
			if (_options.DefaultTimeToLiveSeconds > 0)
			{
				document.Ttl = _options.DefaultTimeToLiveSeconds;
			}

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { IfMatchEtag = response.ETag },
				cancellationToken).ConfigureAwait(false);

			LogMarkedProcessed(messageId, handlerType);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			document.Status = (int)InboxStatus.Processing;
			document.LastAttemptAt = DateTimeOffset.UtcNow;

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { IfMatchEtag = response.ETag },
				cancellationToken).ConfigureAwait(false);

			LogMarkedProcessing(messageId, handlerType);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Create a minimal document for atomic first-writer-wins using CreateItemAsync
		var now = DateTimeOffset.UtcNow;
		var document = new CosmosDbInboxDocument
		{
			Id = ScopedId(messageId, handlerType),
			TenantId = TenantTerm,
			MessageId = messageId,
			HandlerType = ResolvePartitionKeyValue(handlerType),
			MessageType = string.Empty,
			Payload = string.Empty,
			Status = (int)InboxStatus.Processed,
			ReceivedAt = now,
			ProcessedAt = now,
			// Terminal on creation (first-writer-wins) → stamp retention TTL so the dedup record is reaped.
			Ttl = _options.DefaultTimeToLiveSeconds > 0 ? _options.DefaultTimeToLiveSeconds : null
		};

		try
		{
			_ = await _container!.CreateItemAsync(
				document,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { EnableContentResponseOnWrite = false },
				cancellationToken).ConfigureAwait(false);

			LogFirstProcessor(messageId, handlerType);
			return true;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			// Document already exists - another processor got there first
			LogDuplicateDetected(messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state via CreateItemAsync, which fails
		// with a 409 Conflict on an existing item (already claimed/processed) => not claimed. Finalized via
		// MarkProcessedAsync, removed via ReleaseAsync. No TTL on the non-terminal claim (a transient claim must
		// not auto-reap; only the terminal record carries the retention TTL).
		var now = DateTimeOffset.UtcNow;
		var document = new CosmosDbInboxDocument
		{
			Id = ScopedId(messageId, handlerType),
			TenantId = TenantTerm,
			MessageId = messageId,
			HandlerType = ResolvePartitionKeyValue(handlerType),
			MessageType = string.Empty,
			Payload = string.Empty,
			Status = (int)InboxStatus.Processing,
			ReceivedAt = now
		};

		try
		{
			_ = await _container!.CreateItemAsync(
				document,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { EnableContentResponseOnWrite = false },
				cancellationToken).ConfigureAwait(false);

			LogFirstProcessor(messageId, handlerType);
			return true;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			LogDuplicateDetected(messageId, handlerType);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			// Never delete a finalized (Processed) entry.
			if (response.Resource.Status == (int)InboxStatus.Processed)
			{
				return;
			}

			// Optimistic-concurrency delete: the IfMatchEtag guard fails (PreconditionFailed) if the item changed
			// since the read (e.g. was finalized), so a finalized claim is never deleted even under a race.
			_ = await _container!.DeleteItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { IfMatchEtag = response.ETag },
				cancellationToken).ConfigureAwait(false);
		}
		catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
		{
			// NotFound => already removed or never claimed; PreconditionFailed => changed since read (e.g.
			// finalized). Both are no-ops: never delete a finalized claim, and a missing entry needs no release.
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return response.Resource.Status == (int)InboxStatus.Processed;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return response.Resource.ToInboxEntry();
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			document.Status = (int)InboxStatus.Failed;
			document.LastError = errorMessage;
			document.LastAttemptAt = DateTimeOffset.UtcNow;
			document.RetryCount++;

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { IfMatchEtag = response.ETag },
				cancellationToken).ConfigureAwait(false);

			LogMarkedFailed(messageId, handlerType, errorMessage);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Entry doesn't exist - nothing to mark as failed
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = ScopedId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				ResolvePartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			document.Status = (int)InboxStatus.Failed;
			document.LastError = errorMessage;
			document.LastAttemptAt = DateTimeOffset.UtcNow;

			// Set the retry count EXACTLY (no increment) so a transient short-circuit leaves the entry
			// re-admittable without consuming a delivery attempt (FR-4).
			document.RetryCount = retryCount;

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				ResolvePartitionKey(handlerType),
				new ItemRequestOptions { IfMatchEtag = response.ETag },
				cancellationToken).ConfigureAwait(false);

			LogMarkedFailed(messageId, handlerType, errorMessage);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Entry doesn't exist - nothing to mark as failed
		}
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var queryParts = new List<string> { "SELECT * FROM c WHERE c.status = @status" };
		var parameters = new Dictionary<string, object> { ["@status"] = (int)InboxStatus.Failed };

		if (maxRetries > 0)
		{
			queryParts.Add("AND c.retry_count < @maxRetries");
			parameters["@maxRetries"] = maxRetries;
		}

		if (olderThan.HasValue)
		{
			queryParts.Add("AND c.last_attempt_at < @olderThan");
			parameters["@olderThan"] = olderThan.Value.ToString("O");
		}

		queryParts.Add("ORDER BY c.retry_count ASC, c.last_attempt_at ASC");

		var queryText = string.Join(" ", queryParts);
		var queryDefinition = new QueryDefinition(queryText);

		foreach (var param in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(param.Key, param.Value);
		}

		var queryOptions = new QueryRequestOptions { MaxItemCount = batchSize };
		var results = new List<InboxEntry>();

		using var iterator = _container!.GetItemQueryIterator<CosmosDbInboxDocument>(queryDefinition, requestOptions: queryOptions);

		while (iterator.HasMoreResults && results.Count < batchSize)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var document in response)
			{
				if (results.Count >= batchSize)
				{
					break;
				}

				results.Add(document.ToInboxEntry());
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		const string queryText = "SELECT * FROM c";
		var results = new List<InboxEntry>();

		using var iterator = _container!.GetItemQueryIterator<CosmosDbInboxDocument>(new QueryDefinition(queryText));

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			results.AddRange(response.Select(d => d.ToInboxEntry()));
		}

		return results;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		const string queryText = @"
			SELECT
				COUNT(1) as total,
				SUM(c.status = @processed ? 1 : 0) as processed,
				SUM(c.status = @failed ? 1 : 0) as failed,
				SUM(c.status = @received OR c.status = @processing ? 1 : 0) as pending
			FROM c";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@processed", (int)InboxStatus.Processed)
			.WithParameter("@failed", (int)InboxStatus.Failed)
			.WithParameter("@received", (int)InboxStatus.Received)
			.WithParameter("@processing", (int)InboxStatus.Processing);

		using var iterator = _container!.GetItemQueryIterator<InboxStatisticsDto>(queryDefinition);

		if (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			var result = response.FirstOrDefault();

			if (result != null)
			{
				return new InboxStatistics
				{
					TotalEntries = (int)result.Total,
					ProcessedEntries = (int)result.Processed,
					FailedEntries = (int)result.Failed,
					PendingEntries = (int)result.Pending
				};
			}
		}

		return new InboxStatistics();
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		const string queryText = "SELECT c.id, c.handler_type FROM c WHERE c.status = @status AND c.processed_at < @cutoff";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@status", (int)InboxStatus.Processed)
			.WithParameter("@cutoff", olderThan.ToString("O"));

		var documentsToDelete = new List<(string Id, string HandlerType)>();

		using var iterator = _container!.GetItemQueryIterator<CosmosDbInboxDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			documentsToDelete.AddRange(response.Select(d => (d.Id, d.HandlerType)));
		}

		var deletedCount = 0;
		foreach (var (id, handlerType) in documentsToDelete)
		{
			try
			{
				_ = await _container!.DeleteItemAsync<CosmosDbInboxDocument>(
					id,
					ResolvePartitionKey(handlerType),
					cancellationToken: cancellationToken).ConfigureAwait(false);
				deletedCount++;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				// Already deleted, continue
			}
		}

		LogCleanedUp(deletedCount, olderThan);
		return deletedCount;
	}

	/// <inheritdoc/>
	/// <remarks>The Cosmos DB store implements <see cref="IClaimableInboxStore"/> directly.</remarks>
	public bool SupportsClaim => true;

	/// <inheritdoc/>
	/// <remarks>The Cosmos DB store implements <see cref="IProcessingTrackingInboxStore"/> directly.</remarks>
	public bool SupportsProcessingTracking => true;

	/// <inheritdoc/>
	/// <remarks>
	/// A Cosmos DB <c>TransactionalBatch</c> is single-partition, so atomic handler-plus-mark processing is only
	/// possible when the handler's writes share a partition key with the inbox mark. The capability is therefore
	/// gated on <see cref="CosmosDbInboxOptions.SharedPartitionKey"/> being configured; without it the store
	/// advertises no transactional capability and callers fall back to the at-least-once idempotent claim
	/// protocol rather than falsely advertising atomicity.
	/// </remarks>
	public bool SupportsTransactional => _options.SharedPartitionKey is not null;

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

		if (_options.SharedPartitionKey is null)
		{
			throw new NotSupportedException(
				"Transactional inbox processing requires 'CosmosDbInboxOptions.SharedPartitionKey' to be configured " +
				"so the handler's writes and the inbox processed-mark share a single partition (a Cosmos DB " +
				"TransactionalBatch is single-partition). Without it the store reports SupportsTransactional=false " +
				"and the caller must use the idempotent claim protocol.");
		}

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Uniform partition selection: resolves to the shared partition key (non-null here), the same value used
		// by the store's read/mark/claim paths, so the transactional mark and the non-transactional reads agree.
		var partitionKey = ResolvePartitionKey(handlerType);
		var documentId = ScopedId(messageId, handlerType);

		// Duplicate check in the SHARED partition (where transactional marks are written): a message already
		// marked processed is a duplicate — do not run the handler again.
		try
		{
			var existing = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (existing.Resource.Status == (int)InboxStatus.Processed)
			{
				LogDuplicateDetected(messageId, handlerType);
				return false;
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// No prior mark in the shared partition — proceed to process.
		}

		var batch = _container!.CreateTransactionalBatch(partitionKey);

		// The handler enlists its own operations onto this batch (via scope.AsCosmosBatch()); those writes must
		// target the shared partition key or the batch will reject them.
		await handler(new CosmosInboxTransactionScope(batch, partitionKey), cancellationToken).ConfigureAwait(false);

		// Add the processed-mark to the SAME batch so it commits atomically with the handler's writes.
			// CreateItem (below), not UpsertItem, is the exactly-once guard: Cosmos serializes single-partition batch
			// commits and CreateItem fails the batch with 409 Conflict when the id already exists, so the second of
			// two concurrent redeliveries is rejected (its handler writes roll back with it), redelivered, and the
			// pre-read sees Processed. First-writer-wins = exactly-once; Upsert would let both write-sets commit.
		// The mark lives in the shared partition (handler_type = shared partition-key value) so it can share the
		// batch; the composite id still encodes the real handler type.
		var now = DateTimeOffset.UtcNow;
		var markDocument = new CosmosDbInboxDocument
		{
			Id = documentId,
			TenantId = TenantTerm,
			MessageId = messageId,
			HandlerType = ResolvePartitionKeyValue(handlerType),
			MessageType = string.Empty,
			Payload = string.Empty,
			Status = (int)InboxStatus.Processed,
			ReceivedAt = now,
			ProcessedAt = now,
			Ttl = _options.DefaultTimeToLiveSeconds > 0 ? _options.DefaultTimeToLiveSeconds : null
		};

		_ = batch.CreateItem(markDocument);

		using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"Transactional inbox batch failed with status '{response.StatusCode}' for message '{messageId}' " +
				$"and handler '{handlerType}'. The handler's writes and the processed-mark were rolled back.");
		}

		LogMarkedProcessed(messageId, handlerType);
		return true;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock?.Dispose();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,

			// fmjwqy (SA HYBRID): framework-built client uses STJ so persisted documents'
			// [JsonPropertyName] attributes are honored (SDK v3 default Newtonsoft ignores them).
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
			},
		};

		if (_options.Client.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.Client.ConsistencyLevel.Value;
		}

		if (_options.Client.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.Client.PreferredRegions.ToList();
		}

		// Honor a consumer-supplied HttpClientFactory (as every other Cosmos store does), e.g. the Testcontainers
		// Cosmos emulator's cert-bypassing HttpClient — without it the framework-built client cannot reach an
		// emulator serving a self-signed cert, and the transactional path is not real-infra testable.
		if (_options.Client.HttpClientFactory is not null)
		{
			options.HttpClientFactory = _options.Client.HttpClientFactory;
		}

		return options;
	}

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.Client.ConnectionString))
		{
			return new CosmosClient(_options.Client.ConnectionString, options);
		}

		return new CosmosClient(_options.Client.AccountEndpoint, _options.Client.AccountKey, options);
	}

	/// <summary>
	/// Resolves the partition key for an operation on the given handler type: the configured shared partition
	/// key when set (so every operation — read, mark, claim, and the transactional batch — targets one partition
	/// and stays self-consistent), otherwise the per-handler partition.
	/// </summary>
	private PartitionKey ResolvePartitionKey(string handlerType) =>
		new(_options.SharedPartitionKey ?? handlerType);

	/// <summary>
	/// Resolves the partition-key <em>value</em> to stamp on a written document's <c>handler_type</c> field so
	/// it matches the partition the document is written to (Cosmos derives the partition from the document's
	/// partition-key path, so the field and the write partition must agree).
	/// </summary>
	private string ResolvePartitionKeyValue(string handlerType) =>
		_options.SharedPartitionKey ?? handlerType;

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Typed DTO for deserializing inbox statistics aggregate query results.
	/// </summary>
	private sealed class InboxStatisticsDto
	{
		[JsonPropertyName("total")]
		public long Total { get; set; }

		[JsonPropertyName("processed")]
		public long Processed { get; set; }

		[JsonPropertyName("failed")]
		public long Failed { get; set; }

		[JsonPropertyName("pending")]
		public long Pending { get; set; }
	}
}
