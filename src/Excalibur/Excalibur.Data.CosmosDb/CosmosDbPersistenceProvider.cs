// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb.Resources;
using Excalibur.Data.Persistence;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native persistence provider.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud persistence providers inherently couple with many SDK and abstraction types.")]
public sealed partial class CosmosDbPersistenceProvider : ICloudNativePersistenceProvider,
	ICloudNativeProviderInfo, ICloudNativePersistenceQueryOperations, ICloudNativePersistenceBatchOperations, ICloudNativePersistenceChangeFeed,
	IPersistenceProviderHealth, IPersistenceProviderConnection, IAsyncDisposable
{
	private readonly CosmosDbOptions _options;
	private readonly ILogger<CosmosDbPersistenceProvider> _logger;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;

	/// <summary>
	/// Whether this provider created the Cosmos client it holds, and may therefore dispose it.
	/// </summary>
	/// <remarks>
	/// A provider handed the host's shared client must not dispose it: the client is a singleton several
	/// features share, and disposing it leaves every other feature throwing ObjectDisposedException from a
	/// call that names this provider's disposal rather than anything the caller did.
	/// </remarks>
	private bool _ownsClient;
	private Database? _database;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbPersistenceProvider"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="checkpointStore">
	/// Optional durable change-feed checkpoint store. Supplied by DI (the registered
	/// <see cref="IChangeFeedCheckpointStore"/> — the in-memory default, or the durable Cosmos store when
	/// the consumer opts in via <c>AddCosmosDbChangeFeedCheckpointStore</c>); flowed into every change-feed
	/// subscription so continuation survives restarts. <see langword="null"/> only for manual construction
	/// without DI (in-memory-only continuation, prior behavior).
	/// </param>
	public CosmosDbPersistenceProvider(
		IOptions<CosmosDbOptions> options,
		ILogger<CosmosDbPersistenceProvider> logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;
		_options.Validate();

		Name = string.IsNullOrWhiteSpace(_options.Name) ? "cosmosdb" : _options.Name;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbPersistenceProvider"/> class over a client the
	/// host owns.
	/// </summary>
	/// <param name="options">The Cosmos DB options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="client">The Cosmos client registered by the host. Borrowed, never disposed here.</param>
	/// <param name="checkpointStore">The durable change-feed checkpoint store, or <see langword="null"/>.</param>
	/// <remarks>
	/// Selected by dependency injection whenever a <see cref="CosmosClient"/> is registered, which the
	/// Cosmos registration does. Borrowing that client is what keeps a host enabling several Cosmos features
	/// on one connection pool rather than one per feature, and the provider does not dispose it.
	/// </remarks>
	public CosmosDbPersistenceProvider(
		IOptions<CosmosDbOptions> options,
		ILogger<CosmosDbPersistenceProvider> logger,
		CosmosClient client,
		IChangeFeedCheckpointStore? checkpointStore = null)
		: this(options, logger, checkpointStore)
	{
		ArgumentNullException.ThrowIfNull(client);
		_client = client;
	}

	/// <inheritdoc/>
	public string Name { get; }

	/// <inheritdoc/>
	public string ProviderType => "CloudNative";

	/// <inheritdoc/>
	public bool IsAvailable => _initialized && !_disposed && _client != null;

	/// <inheritdoc/>
	public string DocumentStoreType => "CosmosDB";

	/// <inheritdoc/>
	public CloudPersistenceProviderType CloudProvider => CloudPersistenceProviderType.CosmosDb;

	/// <inheritdoc/>
	public bool SupportsMultiRegionWrites => true;

	/// <inheritdoc/>
	public bool SupportsChangeFeed => true;

	/// <summary>
	/// Gets the underlying Cosmos client for advanced scenarios.
	/// </summary>
	public CosmosClient? Client => _client;

	/// <summary>
	/// Gets the database reference.
	/// </summary>
	public Database? Database => _database;

	/// <summary>
	/// Initializes the Cosmos DB client and database reference.
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

			LogInitializing(Name);

			// Only when the host supplied none. A provider that borrows the registered client shares its
			// connection pool with every other Cosmos feature instead of opening a second one.
			var borrowed = _client is not null;
			var client = _client ?? CreateClient(CreateClientOptions());
			try
			{
				var database = client.GetDatabase(_options.DatabaseName);

				// Verify connectivity by reading database properties
				_ = await database.ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

				// Publish only once the probe has succeeded, so a failed attempt leaves the provider
				// uninitialized rather than holding a client that cannot reach its database.
				_client = client;
				_ownsClient = !borrowed;
				_database = database;
				_initialized = true;
			}
			catch
			{
				// Only a client this provider just built. Disposing a borrowed one on a failed probe would
				// take the account away from every other feature sharing it.
				if (!borrowed)
				{
					client.Dispose();
				}

				throw;
			}
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<TDocument?> GetByIdAsync<TDocument>(
		string id,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);
		var requestOptions = CreateItemRequestOptions(consistencyOptions);

		try
		{
			var response = await container.ReadItemAsync<TDocument>(
				id,
				cosmosPartitionKey,
				requestOptions,
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("GetById", response.RequestCharge);
			return response.Resource;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult<TDocument>> CreateAsync<TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);

		try
		{
			var response = await container.CreateItemAsync(
				document,
				cosmosPartitionKey,
				new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Create", response.RequestCharge);

			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: (int)response.StatusCode,
				requestCharge: response.RequestCharge,
				document: response.Resource,
				etag: response.ETag,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			LogOperationFailed("Create", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage("Trimming", "IL2095", Justification = "Implementation has stricter DynamicallyAccessedMembers than interface requires")]
	public async Task<CloudOperationResult<TDocument>> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
	TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);
		var requestOptions = new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite };

		if (!string.IsNullOrEmpty(etag))
		{
			requestOptions.IfMatchEtag = etag;
		}

		try
		{
			var response = await container.ReplaceItemAsync(
				document,
				GetDocumentId(document),
				cosmosPartitionKey,
				requestOptions,
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Update", response.RequestCharge);

			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: (int)response.StatusCode,
				requestCharge: response.RequestCharge,
				document: response.Resource,
				etag: response.ETag,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			LogOperationFailed("Update", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> DeleteAsync(
		string id,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);
		var requestOptions = new ItemRequestOptions();

		if (!string.IsNullOrEmpty(etag))
		{
			requestOptions.IfMatchEtag = etag;
		}

		try
		{
			var response = await container.DeleteItemAsync<object>(
				id,
				cosmosPartitionKey,
				requestOptions,
				cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Delete", response.RequestCharge);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.StatusCode,
				requestCharge: response.RequestCharge,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			LogOperationFailed("Delete", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: ex.RequestCharge,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudQueryResult<TDocument>> QueryAsync<TDocument>(
		string queryText,
		IPartitionKey partitionKey,
		IDictionary<string, object>? parameters,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);

		var queryDefinition = new QueryDefinition(queryText);
		if (parameters != null)
		{
			foreach (var param in parameters)
			{
				queryDefinition = queryDefinition.WithParameter($"@{param.Key}", param.Value);
			}
		}

		var queryOptions = new QueryRequestOptions { PartitionKey = cosmosPartitionKey, MaxItemCount = 100 };

		if (consistencyOptions?.ConsistencyLevel == CloudNative.ConsistencyLevel.Session &&
			!string.IsNullOrEmpty(consistencyOptions.SessionToken))
		{
			queryOptions.SessionToken = consistencyOptions.SessionToken;
		}

		var documents = new List<TDocument>();
		double totalRequestCharge = 0;
		string? continuationToken = null;
		string? sessionToken = null;

		var iterator = container.GetItemQueryIterator<TDocument>(queryDefinition, requestOptions: queryOptions);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			documents.AddRange(response.Resource);
			totalRequestCharge += response.RequestCharge;
			continuationToken = response.ContinuationToken;
			sessionToken = response.Headers.Session;

			// Break after first batch if continuation is requested
			break;
		}

		LogOperationCompleted("Query", totalRequestCharge);

		return new CloudQueryResult<TDocument>(
			documents,
			totalRequestCharge,
			continuationToken,
			sessionToken);
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> ExecuteBatchAsync(
		IPartitionKey partitionKey,
		IEnumerable<ICloudBatchOperation> operations,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);

		var batch = container.CreateTransactionalBatch(cosmosPartitionKey);
		var operationsList = operations.ToList();

		foreach (var operation in operationsList)
		{
			AddOperationToBatch(batch, operation);
		}

		try
		{
			using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Batch", response.RequestCharge);

			var operationResults = new List<CloudOperationResult>();
			for (var i = 0; i < response.Count; i++)
			{
				var opResult = response.GetOperationResultAtIndex<object>(i);
				operationResults.Add(new CloudOperationResult(
					success: opResult.IsSuccessStatusCode,
					statusCode: (int)opResult.StatusCode,
					requestCharge: 0, // Individual operation charges not available in batch
					etag: opResult.ETag));
			}

			return new CloudBatchResult(
				success: response.IsSuccessStatusCode,
				requestCharge: response.RequestCharge,
				operationResults: operationResults,
				sessionToken: response.Headers.Session);
		}
		catch (CosmosException ex)
		{
			LogOperationFailed("Batch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: ex.RequestCharge,
				operationResults: [],
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage("Trimming", "IL2095", Justification = "Implementation has stricter DynamicallyAccessedMembers than interface requires")]
	public async Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
	TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = _database!.GetContainer(containerName);
		var subscription = new CosmosDbChangeFeedSubscription<TDocument>(
			container,
			options ?? ChangeFeedOptions.Default,
			_logger,
			_checkpointStore);

		await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
		return subscription;
	}

	#region IDocumentPersistenceProvider Implementation

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseCloudNativeMethodsInsteadOfExecuteDocument);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncForTransactionalOperations);
	}

	/// <inheritdoc/>
	public Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncForBatchOperations);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncOrBulkExecution);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseQueryAsyncForAggregations);
	}

	/// <inheritdoc/>
	public Task<string> ExecuteIndexOperationAsync<TConnection>(
		IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.IndexManagementViaContainerPolicy);
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "CosmosDB",
			["Name"] = Name,
			["DatabaseName"] = _options.DatabaseName ?? "Unknown",
			["IsAvailable"] = IsAvailable
		};

		try
		{
			var response = await _database!.ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			stats["DatabaseId"] = response.Resource.Id;
			stats["DatabaseSelfLink"] = response.Resource.SelfLink;
			stats["RequestCharge"] = response.RequestCharge;
		}
		catch (Exception ex)
		{
			stats["Error"] = ex.Message;
		}

		return stats;
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var container = _database!.GetContainer(collectionName);
		var info = new Dictionary<string, object>(StringComparer.Ordinal) { ["ContainerName"] = collectionName };

		try
		{
			var response = await container.ReadContainerAsync(cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			var containerProperties = response.Resource;

			info["Id"] = containerProperties.Id;
			info["PartitionKeyPath"] = containerProperties.PartitionKeyPath;
			info["DefaultTimeToLive"] = containerProperties.DefaultTimeToLive ?? -1;
			info["RequestCharge"] = response.RequestCharge;

			if (containerProperties.IndexingPolicy != null)
			{
				info["IndexingMode"] = containerProperties.IndexingPolicy.IndexingMode.ToString();
				info["AutomaticIndexing"] = containerProperties.IndexingPolicy.Automatic;
			}
		}
		catch (Exception ex)
		{
			info["Error"] = ex.Message;
		}

		return info;
	}

	/// <inheritdoc/>
	public bool ValidateDocumentRequest<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest) =>
		documentRequest != null;

	/// <inheritdoc/>
	public IEnumerable<string> GetSupportedOperationTypes() =>
		["Create", "Read", "Update", "Delete", "Query", "Batch", "ChangeFeed"];

	#endregion IDocumentPersistenceProvider Implementation

	#region IPersistenceProvider Implementation

	/// <inheritdoc/>
	public string ConnectionString => _options.Client.ConnectionString
									  ?? (_options.Client.AccountEndpoint != null ? $"AccountEndpoint={_options.Client.AccountEndpoint}" : string.Empty);

	/// <inheritdoc/>
	public Resilience.IDataRequestRetryPolicy RetryPolicy => CosmosDbRetryPolicy.Instance;

	/// <inheritdoc/>
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken) =>
		await GetDocumentStoreStatisticsAsync(cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	public async Task InitializeAsync(
		IPersistenceOptions options,
		CancellationToken cancellationToken)
	{
		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderConnection))
		{
			return this;
		}

		// IPersistenceProviderTransaction is deliberately not offered, and is not implemented. That
		// capability's scope is ambient: created before the provider knows what will enrol in it, then
		// written through. Cosmos DB is atomic only within a transactional batch, which fixes its
		// partition key and its full operation set at construction, so there is nothing this provider
		// could return that would honour the contract. Callers needing atomicity use ExecuteBatchAsync,
		// which states those constraints in its own signature rather than discovering them at commit.

		if (serviceType == typeof(ICloudNativePersistenceQueryOperations))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativePersistenceBatchOperations))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativePersistenceChangeFeed))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativeProviderInfo))
		{
			return this;
		}

		return null;
	}

	#endregion IPersistenceProvider Implementation

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposing(Name);

		if (_ownsClient)
		{
			_client?.Dispose();
		}

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
		LogDisposing(Name);

		if (_ownsClient)
		{
			_client?.Dispose();
		}

		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private static Microsoft.Azure.Cosmos.PartitionKey ToCosmosPartitionKey(IPartitionKey partitionKey) =>
		new(partitionKey.Value);

	private static ItemRequestOptions? CreateItemRequestOptions(IConsistencyOptions? consistencyOptions)
	{
		if (consistencyOptions == null)
		{
			return null;
		}

		var options = new ItemRequestOptions();

		if (consistencyOptions.ConsistencyLevel == CloudNative.ConsistencyLevel.Session &&
			!string.IsNullOrEmpty(consistencyOptions.SessionToken))
		{
			options.SessionToken = consistencyOptions.SessionToken;
		}

		return options;
	}

	private static string GetDocumentId<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document)
	{
		// Try to get "id" property using reflection (Cosmos DB convention)
		var idProperty = typeof(TDocument).GetProperty("id") ?? typeof(TDocument).GetProperty("Id");
		if (idProperty != null)
		{
			return idProperty.GetValue(document)?.ToString()
				   ?? throw new InvalidOperationException(ErrorMessages.DocumentIdPropertyNull);
		}

		throw new InvalidOperationException(
			$"Document type {typeof(TDocument).Name} must have an 'id' or 'Id' property.");
	}

	private static void AddOperationToBatch(TransactionalBatch batch, ICloudBatchOperation operation) =>
		_ = operation.OperationType switch
		{
			CloudBatchOperationType.Create => batch.CreateItem(RequireDocument(operation)),
			CloudBatchOperationType.Replace => batch.ReplaceItem(operation.DocumentId, RequireDocument(operation)),
			CloudBatchOperationType.Upsert => batch.UpsertItem(RequireDocument(operation)),
			CloudBatchOperationType.Delete => batch.DeleteItem(operation.DocumentId),
			CloudBatchOperationType.Read => batch.ReadItem(operation.DocumentId),
			_ => throw new NotSupportedException(
				$"A Cosmos DB transactional batch cannot perform a '{operation.OperationType}' operation. Supported operations are Create, Replace, Upsert, Delete and Read.")
		};

	/// <summary>
	/// Returns the document a writing batch operation carries, or throws when the operation declares a write but
	/// supplies no payload -- a caller mistake that would otherwise be committed as an empty batch entry.
	/// </summary>
	private static object RequireDocument(ICloudBatchOperation operation) =>
		operation is ICloudBatchDocumentOperation documentOperation
			? documentOperation.Document
			: throw new ArgumentException(
				$"Batch operation '{operation.OperationType}' for document '{operation.DocumentId}' carries no document. "
				+ $"Use {nameof(CloudBatchCreateOperation)}, {nameof(CloudBatchReplaceOperation)} or {nameof(CloudBatchUpsertOperation)}, "
				+ $"or any {nameof(ICloudBatchDocumentOperation)} implementation.",
				nameof(operation));

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			ApplicationName = _options.Client.ApplicationName ?? "Excalibur.Data.CosmosDb",
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			AllowBulkExecution = _options.AllowBulkExecution,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			EnableTcpConnectionEndpointRediscovery = _options.EnableTcpConnectionEndpointRediscovery,

			// Force deterministic camelCase property naming on the SDK's default serializer. Without this the
			// Cosmos SDK v3 default (Newtonsoft, no naming policy) emits PascalCase keys, so a generic TDocument
			// with a PascalCase 'Id' serializes to 'Id' instead of the Cosmos-required lowercase 'id' — breaking
			// point-read-by-id (NotFound / auto-GUID id) and the serialize→store→reload round-trip. Mirrors the
			// CdcStateStore reference fix so every store-owned document on this provider round-trips.
			SerializerOptions = new CosmosSerializationOptions
			{
				PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
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

		if (_options.Client.UseDirectMode)
		{
			options.ConnectionMode = ConnectionMode.Direct;
		}
		else
		{
			options.ConnectionMode = ConnectionMode.Gateway;
		}

		// A consumer-supplied HttpClientFactory must reach the SDK, or the option is advertised and
		// silently dropped: a custom handler, proxy, or certificate policy configured here would have no
		// effect on this provider's connection. Every other Cosmos type in this package that builds its
		// own client already applies it; this one did not.
		if (_options.Client.HttpClientFactory != null)
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

	private Container GetContainer(string? containerName = null) =>
		_database!.GetContainer(containerName ?? _options.DefaultContainerName
			?? throw new InvalidOperationException(ErrorMessages.NoContainerNameSpecified));

	/// <summary>
	/// Initializes the provider if it is not already initialized, then returns.
	/// </summary>
	/// <remarks>
	/// Every operation drives this, so the provider is usable straight out of the container without the
	/// consumer calling <see cref="InitializeAsync(CancellationToken)"/> first. A connection failure surfaces the underlying
	/// Cosmos error naming what is unreachable, and leaves the provider uninitialized so the next
	/// operation retries.
	/// </remarks>
	private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_initialized)
		{
			return;
		}

		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}
}
