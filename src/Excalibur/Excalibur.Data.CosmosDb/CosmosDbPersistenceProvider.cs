// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Internal interface for create operations with document.
/// </summary>
internal interface ICloudBatchCreateOperation
{
	/// <summary>
	/// Gets the document to create.
	/// </summary>
	object Document { get; }
}

/// <summary>
/// Internal interface for replace operations with document.
/// </summary>
internal interface ICloudBatchReplaceOperation
{
	/// <summary>
	/// Gets the replacement document.
	/// </summary>
	object Document { get; }
}

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native persistence provider.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud persistence providers inherently couple with many SDK and abstraction types.")]
public sealed partial class CosmosDbPersistenceProvider : ICloudNativePersistenceProvider, IPersistenceProviderHealth,
	IPersistenceProviderTransaction, IAsyncDisposable
{
	private readonly CosmosDbOptions _options;
	private readonly ILogger<CosmosDbPersistenceProvider> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Database? _database;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbPersistenceProvider"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbPersistenceProvider(
		IOptions<CosmosDbOptions> options,
		ILogger<CosmosDbPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();

		Name = _options.Name;
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
	public CloudProviderType CloudProvider => CloudProviderType.CosmosDb;

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

			var clientOptions = CreateClientOptions();
			_client = CreateClient(clientOptions);
			_database = _client.GetDatabase(_options.DatabaseName);

			// Verify connectivity by reading database properties
			_ = await _database.ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			_initialized = true;
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
		EnsureInitialized();

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
		EnsureInitialized();

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);

		try
		{
			var response = await container.CreateItemAsync(
				document,
				cosmosPartitionKey,
				new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite },
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
	public async Task<CloudOperationResult<TDocument>> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var container = GetContainer();
		var cosmosPartitionKey = ToCosmosPartitionKey(partitionKey);
		var requestOptions = new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite };

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
		EnsureInitialized();

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
		EnsureInitialized();

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

		if (consistencyOptions?.ConsistencyLevel == Abstractions.CloudNative.ConsistencyLevel.Session &&
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
		EnsureInitialized();

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
	public async Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var container = _database.GetContainer(containerName);
		var subscription = new CosmosDbChangeFeedSubscription<TDocument>(
			container,
			options ?? ChangeFeedOptions.Default,
			_logger);

		await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
		return subscription;
	}

	#region IDocumentPersistenceProvider Implementation

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseCloudNativeMethodsInsteadOfExecuteDocument);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncForTransactionalOperations);
	}

	/// <inheritdoc/>
	public Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<Abstractions.IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncForBatchOperations);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncOrBulkExecution);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.UseQueryAsyncForAggregations);
	}

	/// <inheritdoc/>
	public Task<string> ExecuteIndexOperationAsync<TConnection>(
		Abstractions.IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(ErrorMessages.IndexManagementViaContainerPolicy);
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "CosmosDB",
			["Name"] = Name,
			["DatabaseName"] = _options.DatabaseName ?? "Unknown",
			["IsAvailable"] = IsAvailable
		};

		try
		{
			var response = await _database.ReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
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
		EnsureInitialized();

		var container = _database.GetContainer(collectionName);
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
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest) =>
		documentRequest != null;

	/// <inheritdoc/>
	public IEnumerable<string> GetSupportedOperationTypes() =>
		["Create", "Read", "Update", "Delete", "Query", "Batch", "ChangeFeed"];

	#endregion IDocumentPersistenceProvider Implementation

	#region IPersistenceProvider Implementation

	/// <inheritdoc/>
	public string ConnectionString => _options.ConnectionString
	                                  ?? (_options.AccountEndpoint != null ? $"AccountEndpoint={_options.AccountEndpoint}" : string.Empty);

	/// <inheritdoc/>
	public Abstractions.Resilience.IDataRequestRetryPolicy RetryPolicy => CosmosDbRetryPolicy.Instance;

	/// <inheritdoc/>
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		Abstractions.IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(ErrorMessages.UseCloudNativeMethodsGeneric);
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		Abstractions.IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(ErrorMessages.UseExecuteBatchAsyncForTransactionalOperations);
	}

	/// <inheritdoc/>
	public ITransactionScope CreateTransactionScope(
		System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		throw new NotSupportedException(ErrorMessages.CosmosDbUsesTransactionalBatches);
	}

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
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		// Cosmos DB SDK manages connections internally via Direct mode or Gateway mode.
		// Connection pool statistics are not directly exposed by the SDK.
		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ConnectionMode"] = _options.UseDirectMode ? "Direct" : "Gateway",
			["IsInitialized"] = _initialized,
			["IsDisposed"] = _disposed
		};

		return Task.FromResult<IDictionary<string, object>?>(stats);
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderTransaction))
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
		LogDisposing(Name);

		_client?.Dispose();
		_initLock.Dispose();

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

		if (consistencyOptions.ConsistencyLevel == Abstractions.CloudNative.ConsistencyLevel.Session &&
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

	private static void AddOperationToBatch(TransactionalBatch batch, ICloudBatchOperation operation)
	{
		switch (operation.OperationType)
		{
			case CloudBatchOperationType.Create:
				if (operation is ICloudBatchCreateOperation createOp)
				{
					_ = batch.CreateItem(createOp.Document);
				}

				break;

			case CloudBatchOperationType.Replace:
				if (operation is ICloudBatchReplaceOperation replaceOp)
				{
					_ = batch.ReplaceItem(operation.DocumentId, replaceOp.Document);
				}

				break;

			case CloudBatchOperationType.Upsert:
				if (operation is ICloudBatchUpsertOperation upsertOp)
				{
					_ = batch.UpsertItem(upsertOp.Document);
				}

				break;

			case CloudBatchOperationType.Delete:
				_ = batch.DeleteItem(operation.DocumentId);
				break;

			case CloudBatchOperationType.Read:
				_ = batch.ReadItem(operation.DocumentId);
				break;

			case CloudBatchOperationType.Patch:
				break;

			default:
				throw new NotSupportedException($"Operation type {operation.OperationType} is not supported.");
		}
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			ApplicationName = _options.ApplicationName ?? "Excalibur.Data.CosmosDb",
			MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite,
			AllowBulkExecution = _options.AllowBulkExecution,
			RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutInSeconds),
			EnableTcpConnectionEndpointRediscovery = _options.EnableTcpConnectionEndpointRediscovery
		};

		if (_options.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.ConsistencyLevel.Value;
		}

		if (_options.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.PreferredRegions.ToList();
		}

		if (_options.UseDirectMode)
		{
			options.ConnectionMode = ConnectionMode.Direct;
		}
		else
		{
			options.ConnectionMode = ConnectionMode.Gateway;
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

	private Container GetContainer(string? containerName = null) =>
		_database.GetContainer(containerName ?? _options.DefaultContainerName
			?? throw new InvalidOperationException(ErrorMessages.NoContainerNameSpecified));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void EnsureInitialized()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			throw new InvalidOperationException(
				$"Provider '{Name}' has not been initialized. Call InitializeAsync first.");
		}
	}
}

/// <summary>
/// Batch operation for creating a document.
/// </summary>
public sealed class CloudBatchCreateOperation : ICloudBatchOperation, ICloudBatchCreateOperation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudBatchCreateOperation"/> class.
	/// </summary>
	/// <param name="documentId">The document ID.</param>
	/// <param name="document">The document to create.</param>
	public CloudBatchCreateOperation(string documentId, object document)
	{
		DocumentId = documentId;
		Document = document;
	}

	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Create;

	/// <inheritdoc/>
	public string DocumentId { get; }

	/// <inheritdoc/>
	public object Document { get; }
}

/// <summary>
/// Batch operation for replacing a document.
/// </summary>
public sealed class CloudBatchReplaceOperation : ICloudBatchOperation, ICloudBatchReplaceOperation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudBatchReplaceOperation"/> class.
	/// </summary>
	/// <param name="documentId">The document ID.</param>
	/// <param name="document">The replacement document.</param>
	public CloudBatchReplaceOperation(string documentId, object document)
	{
		DocumentId = documentId;
		Document = document;
	}

	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Replace;

	/// <inheritdoc/>
	public string DocumentId { get; }

	/// <inheritdoc/>
	public object Document { get; }
}

/// <summary>
/// Batch operation for upserting a document.
/// </summary>
public sealed class CloudBatchUpsertOperation : ICloudBatchOperation, ICloudBatchUpsertOperation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudBatchUpsertOperation"/> class.
	/// </summary>
	/// <param name="documentId">The document ID.</param>
	/// <param name="document">The document to upsert.</param>
	public CloudBatchUpsertOperation(string documentId, object document)
	{
		DocumentId = documentId;
		Document = document;
	}

	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Upsert;

	/// <inheritdoc/>
	public string DocumentId { get; }

	/// <inheritdoc/>
	public object Document { get; }
}

/// <summary>
/// Batch operation for deleting a document.
/// </summary>
public sealed class CloudBatchDeleteOperation : ICloudBatchOperation
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudBatchDeleteOperation"/> class.
	/// </summary>
	/// <param name="documentId">The document ID.</param>
	public CloudBatchDeleteOperation(string documentId)
	{
		DocumentId = documentId;
	}

	/// <inheritdoc/>
	public CloudBatchOperationType OperationType => CloudBatchOperationType.Delete;

	/// <inheritdoc/>
	public string DocumentId { get; }
}

/// <summary>
/// Internal interface for upsert operations with document.
/// </summary>
internal interface ICloudBatchUpsertOperation
{
	/// <summary>
	/// Gets the document to upsert.
	/// </summary>
	object Document { get; }
}
