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
public sealed partial class CosmosDbInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbInboxOptions _options;
	private readonly ILogger<CosmosDbInboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbInboxStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbInboxStore(
		IOptions<CosmosDbInboxOptions> options,
		ILogger<CosmosDbInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

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

			var database = _client!.GetDatabase(_options.DatabaseName);
			_container = database.GetContainer(_options.ContainerName);

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

		try
		{
			_ = await _container!.CreateItemAsync(
				document,
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
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
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			document.Status = (int)InboxStatus.Processing;
			document.LastAttemptAt = DateTimeOffset.UtcNow;

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				new PartitionKey(handlerType),
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
			Id = CosmosDbInboxDocument.CreateId(messageId, handlerType),
			MessageId = messageId,
			HandlerType = handlerType,
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
				new PartitionKey(handlerType),
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
			Id = CosmosDbInboxDocument.CreateId(messageId, handlerType),
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = string.Empty,
			Payload = string.Empty,
			Status = (int)InboxStatus.Processing,
			ReceivedAt = now
		};

		try
		{
			_ = await _container!.CreateItemAsync(
				document,
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
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
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			document.Status = (int)InboxStatus.Failed;
			document.LastError = errorMessage;
			document.LastAttemptAt = DateTimeOffset.UtcNow;
			document.RetryCount++;

			_ = await _container!.ReplaceItemAsync(
				document,
				documentId,
				new PartitionKey(handlerType),
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

		var documentId = CosmosDbInboxDocument.CreateId(messageId, handlerType);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbInboxDocument>(
				documentId,
				new PartitionKey(handlerType),
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
				new PartitionKey(handlerType),
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
					new PartitionKey(handlerType),
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
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock.Dispose();
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
		_initLock.Dispose();

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
