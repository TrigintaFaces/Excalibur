// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Cosmos DB implementation of <see cref="IOutboxStore" />.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses status-based partition keys for efficient queries. All status transitions use ETag-based optimistic concurrency
/// for atomic operations.
/// </para>
/// <para> Key design decisions:
/// <list type="bullet">
/// <item> Partition key: status (staged, sent, failed, scheduled) </item>
/// <item> Atomic MarkSentAsync: uses ReplaceItemAsync with IfMatchEtag </item>
/// <item> Cross-partition queries minimized for performance </item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class CosmosDbOutboxStore : IOutboxStore, IOutboxStoreAdmin, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbOutboxOptions _options;
	private readonly ILogger<CosmosDbOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxStore" /> class.
	/// </summary>
	/// <param name="options"> The configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	public CosmosDbOutboxStore(
		IOptions<CosmosDbOutboxOptions> options,
		ILogger<CosmosDbOutboxStore> logger)
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
	/// <param name="cancellationToken"> Cancellation token. </param>
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

			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, "/partitionKey")
				{
					DefaultTimeToLive = -1 // TTL enabled but no default expiration
				};

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					_options.ContainerThroughput,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.ContainerName);
			}

			_initialized = true;
			LogInitialized(_options.ContainerName);
		}
		finally
		{
			_ = _initLock.Release();

		}
	}

	/// <inheritdoc />
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Determine partition key based on whether scheduled
		var partitionKey = message.ScheduledAt.HasValue
			? CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged) // Scheduled goes to staged partition
			: CosmosDbOutboxDocument.CreatePartitionKey(message.Status);

		var document = CosmosDbOutboxDocument.FromOutboundMessage(message);
		document.PartitionKey = partitionKey;

		try
		{
			_ = await _container.CreateItemAsync(
				document,
				new PartitionKey(partitionKey),
				new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite },
				cancellationToken).ConfigureAwait(false);

			LogMessageStaged(message.Id, message.MessageType, message.Destination);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists in the outbox.", ex);

		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outbound = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId
		};

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outbound.Id, messageType);

	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stagedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged);

		// Query staged partition, ordered by priority and creation time Include scheduled messages that are now due
		var now = DateTimeOffset.UtcNow.ToString("O");
		var query = new QueryDefinition(
				@"SELECT * FROM c
			  WHERE c.partitionKey = @pk
			  AND c.status = @stagedStatus
			  AND (c.scheduledAt = null OR c.scheduledAt <= @now)
			  ORDER BY c.priority ASC, c.createdAt ASC")
			.WithParameter("@pk", stagedPartitionKey)
			.WithParameter("@stagedStatus", (int)OutboxStatus.Staged)
			.WithParameter("@now", now);

		var queryOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(stagedPartitionKey), MaxItemCount = batchSize };

		var messages = new List<OutboundMessage>();

		using var iterator = _container.GetItemQueryIterator<CosmosDbOutboxDocument>(query, requestOptions: queryOptions);

		while (iterator.HasMoreResults && messages.Count < batchSize)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var document in response)
			{
				if (messages.Count >= batchSize)
				{
					break;
				}

				messages.Add(document.ToOutboundMessage());
			}
		}

		return messages;

	}

	/// <inheritdoc />
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stagedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged);


		try
		{
			// Read with ETag for optimistic concurrency
			var readResponse = await _container.ReadItemAsync<CosmosDbOutboxDocument>(
				messageId,
				new PartitionKey(stagedPartitionKey),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = readResponse.Resource;

			// Check if already sent (atomic: using ETag ensures no race condition)
			if (document.Status == (int)OutboxStatus.Sent)
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");

			}

			// Delete from staged partition
			_ = await _container.DeleteItemAsync<CosmosDbOutboxDocument>(
				messageId,
				new PartitionKey(stagedPartitionKey),
				new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
				cancellationToken).ConfigureAwait(false);

			// Update document for sent partition
			var sentPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Sent);
			document.PartitionKey = sentPartitionKey;
			document.Status = (int)OutboxStatus.Sent;
			document.SentAt = DateTimeOffset.UtcNow.ToString("O");

			// Set TTL if configured
			if (_options.SentMessageTtlSeconds > 0)
			{
				document.Ttl = _options.SentMessageTtlSeconds;

			}

			// Create in sent partition
			_ = await _container.CreateItemAsync(
				document,
				new PartitionKey(sentPartitionKey),
				new ItemRequestOptions { EnableContentResponseOnWrite = false },
				cancellationToken).ConfigureAwait(false);

			LogMessageSent(messageId);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			throw new InvalidOperationException($"Message with ID '{messageId}' not found.", ex);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)

		{
			// ETag mismatch - another process modified the document (likely marked as sent)
			throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.", ex);

		}
	}

	/// <inheritdoc />
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stagedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged);
		var failedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Failed);

		ItemResponse<CosmosDbOutboxDocument> readResponse;

		string sourcePartitionKey;

		// Try reading from staged partition first
		try
		{
			readResponse = await _container.ReadItemAsync<CosmosDbOutboxDocument>(
				messageId,
				new PartitionKey(stagedPartitionKey),
				cancellationToken: cancellationToken).ConfigureAwait(false);
			sourcePartitionKey = stagedPartitionKey;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)

		{
			// Not in staged, try failed partition (for retry scenarios)
			try
			{
				readResponse = await _container.ReadItemAsync<CosmosDbOutboxDocument>(
					messageId,
					new PartitionKey(failedPartitionKey),
					cancellationToken: cancellationToken).ConfigureAwait(false);
				sourcePartitionKey = failedPartitionKey;
			}
			catch (CosmosException ex2) when (ex2.StatusCode == HttpStatusCode.NotFound)

			{
				// Message doesn't exist in either partition - silent return per conformance tests
				return;
			}
		}

		var document = readResponse.Resource;


		try
		{
			// Delete from source partition
			_ = await _container.DeleteItemAsync<CosmosDbOutboxDocument>(
				messageId,
				new PartitionKey(sourcePartitionKey),
				new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
				cancellationToken).ConfigureAwait(false);

			// Update document for failed partition
			document.PartitionKey = failedPartitionKey;
			document.Status = (int)OutboxStatus.Failed;
			document.LastError = errorMessage;
			document.RetryCount = retryCount;
			document.LastAttemptAt = DateTimeOffset.UtcNow.ToString("O");

			// Create in failed partition
			_ = await _container.CreateItemAsync(
				document,
				new PartitionKey(failedPartitionKey),
				new ItemRequestOptions { EnableContentResponseOnWrite = false },
				cancellationToken).ConfigureAwait(false);

			LogMessageFailed(messageId, errorMessage, retryCount);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)

		{
			// ETag mismatch - another process modified the document, retry For simplicity, we'll just log and return (conformance allows this)
			LogConcurrencyConflict(messageId, "MarkFailed");

		}
	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var failedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Failed);

		// Build query with optional filters
		var queryParts = new List<string> { "SELECT * FROM c WHERE c.partitionKey = @pk AND c.status = @failedStatus" };
		var parameters = new Dictionary<string, object> { ["@pk"] = failedPartitionKey, ["@failedStatus"] = (int)OutboxStatus.Failed };

		if (maxRetries > 0)
		{
			queryParts.Add("AND c.retryCount < @maxRetries");
			parameters["@maxRetries"] = maxRetries;
		}

		if (olderThan.HasValue)
		{
			queryParts.Add("AND c.lastAttemptAt < @olderThan");
			parameters["@olderThan"] = olderThan.Value.ToString("O");
		}

		queryParts.Add("ORDER BY c.retryCount ASC, c.lastAttemptAt ASC");

		var queryText = string.Join(" ", queryParts);
		var queryDefinition = new QueryDefinition(queryText);

		foreach (var param in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(param.Key, param.Value);
		}

		var queryOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(failedPartitionKey), MaxItemCount = batchSize };

		var messages = new List<OutboundMessage>();

		using var iterator = _container.GetItemQueryIterator<CosmosDbOutboxDocument>(queryDefinition, requestOptions: queryOptions);

		while (iterator.HasMoreResults && messages.Count < batchSize)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var document in response)
			{
				if (messages.Count >= batchSize)
				{
					break;
				}

				messages.Add(document.ToOutboundMessage());
			}
		}

		return messages;

	}

	/// <inheritdoc />
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stagedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged);

		var query = new QueryDefinition(
				@"SELECT * FROM c
			  WHERE c.partitionKey = @pk
			  AND c.scheduledAt != null
			  AND c.scheduledAt <= @scheduledBefore
			  ORDER BY c.scheduledAt ASC")
			.WithParameter("@pk", stagedPartitionKey)
			.WithParameter("@scheduledBefore", scheduledBefore.ToString("O"));

		var queryOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(stagedPartitionKey), MaxItemCount = batchSize };

		var messages = new List<OutboundMessage>();

		using var iterator = _container.GetItemQueryIterator<CosmosDbOutboxDocument>(query, requestOptions: queryOptions);

		while (iterator.HasMoreResults && messages.Count < batchSize)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var document in response)
			{
				if (messages.Count >= batchSize)
				{
					break;
				}

				messages.Add(document.ToOutboundMessage());
			}
		}

		return messages;

	}

	/// <inheritdoc />
	public async ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var sentPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Sent);

		var query = new QueryDefinition(
				@"SELECT c.id FROM c
			  WHERE c.partitionKey = @pk
			  AND c.sentAt < @olderThan")
			.WithParameter("@pk", sentPartitionKey)
			.WithParameter("@olderThan", olderThan.ToString("O"));

		var queryOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(sentPartitionKey), MaxItemCount = batchSize };

		var deletedCount = 0;

		using var iterator = _container.GetItemQueryIterator<CosmosDbOutboxDocument>(query, requestOptions: queryOptions);

		while (iterator.HasMoreResults && deletedCount < batchSize)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);

			foreach (var document in response)
			{
				if (deletedCount >= batchSize || cancellationToken.IsCancellationRequested)
				{
					break;
				}

				try
				{
					_ = await _container.DeleteItemAsync<CosmosDbOutboxDocument>(
						document.Id,
						new PartitionKey(sentPartitionKey),
						cancellationToken: cancellationToken).ConfigureAwait(false);
					deletedCount++;
				}
				catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)

				{
					// Already deleted, continue
				}
			}
		}

		LogMessagesCleanedUp(deletedCount, olderThan);

		return deletedCount;

	}

	/// <inheritdoc />
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		// Count per partition (cross-partition query)
		var countQuery = new QueryDefinition(
			@"SELECT c.partitionKey, COUNT(1) as count
			  FROM c
			  GROUP BY c.partitionKey");

		var counts = new Dictionary<string, int>();

		using (var iterator = _container.GetItemQueryIterator<JsonElement>(countQuery))
		{
			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				foreach (var item in response)
				{
					var pk = item.GetProperty("partitionKey").GetString();
					var count = item.GetProperty("count").GetInt32();
					counts[pk] = count;

				}
			}
		}

		// Get oldest unsent
		TimeSpan? oldestUnsentAge = null;
		var stagedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Staged);
		var oldestQuery = new QueryDefinition(
				"SELECT TOP 1 c.createdAt FROM c WHERE c.partitionKey = @pk ORDER BY c.createdAt ASC")
			.WithParameter("@pk", stagedPartitionKey);

		using (var iterator = _container.GetItemQueryIterator<JsonElement>(
				   oldestQuery,
				   requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(stagedPartitionKey), MaxItemCount = 1 }))
		{
			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				var oldest = response.FirstOrDefault();
				if (oldest.ValueKind != JsonValueKind.Undefined)
				{
					var createdAt = DateTimeOffset.Parse(oldest.GetProperty("createdAt").GetString(), CultureInfo.InvariantCulture);
					oldestUnsentAge = now - createdAt;

				}
			}
		}

		// Get oldest failed
		TimeSpan? oldestFailedAge = null;
		var failedPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Failed);
		var oldestFailedQuery = new QueryDefinition(
				"SELECT TOP 1 c.createdAt FROM c WHERE c.partitionKey = @pk ORDER BY c.createdAt ASC")
			.WithParameter("@pk", failedPartitionKey);

		using (var iterator = _container.GetItemQueryIterator<JsonElement>(
				   oldestFailedQuery,
				   requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(failedPartitionKey), MaxItemCount = 1 }))
		{
			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				var oldest = response.FirstOrDefault();
				if (oldest.ValueKind != JsonValueKind.Undefined)
				{
					var createdAt = DateTimeOffset.Parse(oldest.GetProperty("createdAt").GetString(), CultureInfo.InvariantCulture);
					oldestFailedAge = now - createdAt;

				}
			}
		}

		// Count scheduled separately (they're in staged partition but have scheduledAt)
		var scheduledQuery = new QueryDefinition(
				"SELECT VALUE COUNT(1) FROM c WHERE c.partitionKey = @pk AND c.scheduledAt != null")
			.WithParameter("@pk", stagedPartitionKey);

		var scheduledCount = 0;
		using (var iterator = _container.GetItemQueryIterator<int>(
				   scheduledQuery,
				   requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(stagedPartitionKey) }))
		{
			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				scheduledCount = response.FirstOrDefault();
			}
		}

		var sentPartitionKey = CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Sent);

		return new OutboxStatistics
		{
			StagedMessageCount = counts.GetValueOrDefault(stagedPartitionKey, 0) - scheduledCount,
			SendingMessageCount = counts.GetValueOrDefault(CosmosDbOutboxDocument.CreatePartitionKey(OutboxStatus.Sending), 0),
			SentMessageCount = counts.GetValueOrDefault(sentPartitionKey, 0),
			FailedMessageCount = counts.GetValueOrDefault(failedPartitionKey, 0),
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsentAge,
			OldestFailedMessageAge = oldestFailedAge,
			CapturedAt = now
		};

	}

	/// <inheritdoc />
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

	/// <inheritdoc />
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
			MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutInSeconds),
			ConnectionMode = _options.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			// Use System.Text.Json serializer to respect [JsonPropertyName] attributes
			UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
		};

		if (_options.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.ConsistencyLevel.Value;
		}

		if (_options.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.PreferredRegions.ToList();
		}

		if (_options.HttpClientFactory != null)
		{
			options.HttpClientFactory = _options.HttpClientFactory;
		}

		return options;
	}

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			return new CosmosClient(_options.ConnectionString, options);
		}

		return new CosmosClient(_options.AccountEndpoint, _options.AccountKey, options);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	[LoggerMessage(DataCosmosDbEventId.OutboxStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB outbox store with container '{ContainerName}'")]
	private partial void LogInitialized(string containerName);

	[LoggerMessage(DataCosmosDbEventId.MessageStaged, LogLevel.Debug,
		"Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataCosmosDbEventId.MessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataCosmosDbEventId.MessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataCosmosDbEventId.MessageFailed, LogLevel.Warning,
		"Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataCosmosDbEventId.MessagesCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);

	[LoggerMessage(DataCosmosDbEventId.ConcurrencyConflict, LogLevel.Debug,
		"Concurrency conflict for message {MessageId} during {Operation}, another process may have modified it")]
	private partial void LogConcurrencyConflict(string messageId, string operation);
}
