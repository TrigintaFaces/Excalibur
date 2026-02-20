// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native outbox store.
/// </summary>
public sealed partial class CosmosDbOutboxStore : ICloudNativeOutboxStore, IAsyncDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly CosmosDbOutboxOptions _options;
	private readonly ILogger<CosmosDbOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private CosmosClient? _client;
	private Database? _database;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbOutboxStore(
		IOptions<CosmosDbOutboxOptions> options,
		ILogger<CosmosDbOutboxStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();
	}

	/// <inheritdoc/>
	public CloudProviderType ProviderType => CloudProviderType.CosmosDb;

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

			var clientOptions = CreateClientOptions();
			_client = CreateClient(clientOptions);
			_database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, "/partitionKey")
				{
					DefaultTimeToLive = _options.DefaultTimeToLiveSeconds
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
		var document = ToDocument(message, partitionKey);
		var cosmosPartitionKey = new CosmosPartitionKey(partitionKey.Value);

		try
		{
			var response = await _container.CreateItemAsync(
				document,
				cosmosPartitionKey,
				new ItemRequestOptions { EnableContentResponseOnWrite = true },
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Add", response.RequestCharge);

			var resultMessage = FromDocument(response.Resource);
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
		var batch = _container.CreateTransactionalBatch(cosmosPartitionKey);

		foreach (var message in messages)
		{
			var document = ToDocument(message, partitionKey);
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

			var iterator = _container.GetItemQueryIterator<OutboxDocument>(query, requestOptions: queryOptions);

			if (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				foreach (var doc in response.Resource)
				{
					messages.Add(FromDocument(doc));
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
			var readResponse = await _container.ReadItemAsync<OutboxDocument>(
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

			var replaceResponse = await _container.ReplaceItemAsync(
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
		var documents = new List<(OutboxDocument Doc, string ETag)>();
		foreach (var messageId in messageIds)
		{
			try
			{
				var response = await _container.ReadItemAsync<OutboxDocument>(
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
		var batch = _container.CreateTransactionalBatch(cosmosPartitionKey);
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

			var iterator = _container.GetItemQueryIterator<dynamic>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRequestCharge += response.RequestCharge;

				foreach (var item in response.Resource)
				{
					string id = item.id;
					try
					{
						var deleteResponse = await _container.DeleteItemAsync<object>(
							id,
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
				_container,
				options ?? ChangeFeedOptions.Default,
				_logger);

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
			var readResponse = await _container.ReadItemAsync<OutboxDocument>(
				messageId,
				cosmosPartitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = readResponse.Resource;
			document.RetryCount++;
			document.LastError = errorMessage;

			var replaceResponse = await _container.ReplaceItemAsync(
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

		_client?.Dispose();
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private static OutboxDocument ToDocument(CloudOutboxMessage message, IPartitionKey partitionKey) =>
		new()
		{
			Id = message.MessageId,
			PartitionKey = partitionKey.Value,
			MessageType = message.MessageType,
			Payload = Convert.ToBase64String(message.Payload),
			Headers = message.Headers != null
				? JsonSerializer.Serialize(message.Headers, JsonOptions)
				: null,
			AggregateId = message.AggregateId,
			AggregateType = message.AggregateType,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			CreatedAt = message.CreatedAt.ToString("o"),
			PublishedAt = message.PublishedAt?.ToString("o"),
			IsPublished = message.IsPublished,
			RetryCount = message.RetryCount,
			LastError = message.LastError
		};

	private static CloudOutboxMessage FromDocument(OutboxDocument doc) =>
		new()
		{
			MessageId = doc.Id,
			MessageType = doc.MessageType,
			Payload = Convert.FromBase64String(doc.Payload),
			Headers = !string.IsNullOrEmpty(doc.Headers)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.Headers, JsonOptions)
				: null,
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			CorrelationId = doc.CorrelationId,
			CausationId = doc.CausationId,
			CreatedAt = DateTimeOffset.Parse(doc.CreatedAt, CultureInfo.InvariantCulture),
			PublishedAt = !string.IsNullOrEmpty(doc.PublishedAt) ? DateTimeOffset.Parse(doc.PublishedAt, CultureInfo.InvariantCulture) : null,
			RetryCount = doc.RetryCount,
			LastError = doc.LastError,
			PartitionKeyValue = doc.PartitionKey,
			ETag = doc.ETag
		};

	private CosmosClientOptions CreateClientOptions() =>
		new()
		{
			ApplicationName = "Excalibur.Outbox.CosmosDb",
			MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeInSeconds),
			ConnectionMode = _options.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway
		};

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			return new CosmosClient(_options.ConnectionString, options);
		}

		return new CosmosClient(_options.AccountEndpoint, _options.AccountKey, options);
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

	/// <summary>
	/// Internal document representation for Cosmos DB storage.
	/// </summary>
	private sealed class OutboxDocument
	{
		public required string Id { get; set; }
		public required string PartitionKey { get; set; }
		public required string MessageType { get; set; }
		public required string Payload { get; set; }
		public string? Headers { get; set; }
		public string? AggregateId { get; set; }
		public string? AggregateType { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public required string CreatedAt { get; set; }
		public string? PublishedAt { get; set; }
		public bool IsPublished { get; set; }
		public int RetryCount { get; set; }
		public string? LastError { get; set; }
		public string? ETag { get; set; }
		public int? Ttl { get; set; }
	}
}
