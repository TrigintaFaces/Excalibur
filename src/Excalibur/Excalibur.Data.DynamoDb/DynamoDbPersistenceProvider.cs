// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DynamoDbKeyType = Amazon.DynamoDBv2.KeyType;


namespace Excalibur.Data.DynamoDb;

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
/// AWS DynamoDB implementation of the cloud-native persistence provider.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud persistence providers inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbPersistenceProvider : ICloudNativePersistenceProvider, IPersistenceProviderHealth,
	IPersistenceProviderTransaction, IAsyncDisposable
{
	private readonly DynamoDbOptions _options;
	private readonly ILogger<DynamoDbPersistenceProvider> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private IAmazonDynamoDB? _client;
	private IAmazonDynamoDBStreams? _streamsClient;
	private bool _initialized;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The DynamoDB options. </param>
	/// <param name="logger"> The logger instance. </param>
	public DynamoDbPersistenceProvider(
		IOptions<DynamoDbOptions> options,
		ILogger<DynamoDbPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();

		Name = _options.Name;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbPersistenceProvider" /> class with an existing client.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="options"> The DynamoDB options. </param>
	/// <param name="logger"> The logger instance. </param>
	public DynamoDbPersistenceProvider(
		IAmazonDynamoDB client,
		IOptions<DynamoDbOptions> options,
		ILogger<DynamoDbPersistenceProvider> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_initialized = true;

		Name = _options.Name;
	}

	/// <inheritdoc />

	public string Name { get; }

	/// <inheritdoc />

	public string ProviderType => "CloudNative";

	/// <inheritdoc />

	public bool IsAvailable => _initialized && !_disposed && _client != null;

	/// <inheritdoc />

	public string DocumentStoreType => "DynamoDB";

	/// <inheritdoc />

	public CloudProviderType CloudProvider => CloudProviderType.DynamoDb;

	/// <inheritdoc />

	public bool SupportsMultiRegionWrites => true; // DynamoDB Global Tables

	/// <inheritdoc />

	public bool SupportsChangeFeed => true; // DynamoDB Streams

	/// <inheritdoc />
	public string ConnectionString => _options.ServiceUrl
	                                  ?? (_options.Region != null ? $"Region={_options.Region}" : string.Empty);

	/// <inheritdoc />

	public Abstractions.Resilience.IDataRequestRetryPolicy RetryPolicy => DynamoDbRetryPolicy.Instance;

	/// <summary>
	/// Gets the underlying DynamoDB client for advanced scenarios.
	/// </summary>

	public IAmazonDynamoDB? Client => _client;

	/// <summary>
	/// Initializes the DynamoDB client.
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

			LogInitializing(Name);

			_client = CreateClient();
			_streamsClient = CreateStreamsClient();

			// Verify connectivity by listing tables
			_ = await _client.ListTablesAsync(new ListTablesRequest { Limit = 1 }, cancellationToken)
				.ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task<TDocument?> GetByIdAsync<TDocument>(
		string id,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var request = new GetItemRequest
		{
			TableName = _options.DefaultTableName,
			Key = CreateKey(partitionKey, id),
			ConsistentRead = consistencyOptions?.ConsistencyLevel == ConsistencyLevel.Strong
			                 || _options.UseConsistentReads,
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		try
		{
			var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("GetById", response.ConsumedCapacity?.CapacityUnits ?? 0);

			if (!response.IsItemSet || response.Item.Count == 0)
			{
				return null;
			}

			return DeserializeDocument<TDocument>(response.Item);
		}
		catch (ResourceNotFoundException)
		{
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<CloudOperationResult<TDocument>> CreateAsync<TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var item = SerializeDocument(document, partitionKey);
		var request = new PutItemRequest
		{
			TableName = _options.DefaultTableName,
			Item = item,
			ConditionExpression = "attribute_not_exists(pk)", // Ensure item doesn't exist
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL,
			ReturnValues = ReturnValue.NONE
		};

		try
		{
			var response = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Create", response.ConsumedCapacity?.CapacityUnits ?? 0);

			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: response.ConsumedCapacity?.CapacityUnits ?? 0,
				document: document);
		}
		catch (ConditionalCheckFailedException ex)
		{
			LogOperationFailed("Create", "Item already exists", ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)HttpStatusCode.Conflict,
				requestCharge: 0,
				errorMessage: "Item already exists");
		}
		catch (AmazonDynamoDBException ex)
		{
			LogOperationFailed("Create", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc />
	public async Task<CloudOperationResult<TDocument>> UpdateAsync<TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var item = SerializeDocument(document, partitionKey);
		var request = new PutItemRequest
		{
			TableName = _options.DefaultTableName,
			Item = item,
			ConditionExpression = "attribute_exists(pk)", // Ensure item exists
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL,
			ReturnValues = ReturnValue.NONE
		};

		// If etag is provided, add version check
		if (!string.IsNullOrEmpty(etag))
		{
			request.ConditionExpression += " AND #version = :expectedVersion";
			request.ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "_version" };
			request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":expectedVersion"] = new AttributeValue { S = etag }
			};
		}

		try
		{
			var response = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Update", response.ConsumedCapacity?.CapacityUnits ?? 0);

			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: response.ConsumedCapacity?.CapacityUnits ?? 0,
				document: document);
		}
		catch (ConditionalCheckFailedException ex)
		{
			LogOperationFailed("Update", "Condition check failed (item not found or version mismatch)", ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)HttpStatusCode.PreconditionFailed,
				requestCharge: 0,
				errorMessage: "Item not found or version mismatch");
		}
		catch (AmazonDynamoDBException ex)
		{
			LogOperationFailed("Update", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc />
	public async Task<CloudOperationResult> DeleteAsync(
		string id,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var request = new DeleteItemRequest
		{
			TableName = _options.DefaultTableName,
			Key = CreateKey(partitionKey, id),
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		// If etag is provided, add version check
		if (!string.IsNullOrEmpty(etag))
		{
			request.ConditionExpression = "#version = :expectedVersion";
			request.ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "_version" };
			request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":expectedVersion"] = new AttributeValue { S = etag }
			};
		}

		try
		{
			var response = await _client.DeleteItemAsync(request, cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Delete", response.ConsumedCapacity?.CapacityUnits ?? 0);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: response.ConsumedCapacity?.CapacityUnits ?? 0);
		}
		catch (ConditionalCheckFailedException ex)
		{
			LogOperationFailed("Delete", "Version mismatch", ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)HttpStatusCode.PreconditionFailed,
				requestCharge: 0,
				errorMessage: "Version mismatch");
		}
		catch (AmazonDynamoDBException ex)
		{
			LogOperationFailed("Delete", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task<CloudQueryResult<TDocument>> QueryAsync<TDocument>(
		string queryText,
		IPartitionKey partitionKey,
		IDictionary<string, object>? parameters,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		// DynamoDB uses KeyConditionExpression instead of SQL-like queries
		var request = new QueryRequest
		{
			TableName = _options.DefaultTableName,
			KeyConditionExpression = $"{_options.DefaultPartitionKeyAttribute} = :pk",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new AttributeValue { S = partitionKey.Value } },
			ConsistentRead = consistencyOptions?.ConsistencyLevel == ConsistencyLevel.Strong
			                 || _options.UseConsistentReads,
			ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		// Add filter expression if query text is provided (as a filter, not key condition)
		if (!string.IsNullOrWhiteSpace(queryText) && queryText != "*")
		{
			request.FilterExpression = queryText;
		}

		// Add parameters
		if (parameters != null)
		{
			foreach (var param in parameters)
			{
				var key = param.Key.StartsWith(':') ? param.Key : $":{param.Key}";
				request.ExpressionAttributeValues[key] = ToAttributeValue(param.Value);
			}
		}

		var documents = new List<TDocument>();
		double totalCapacity = 0;
		string? continuationToken = null;

		try
		{
			var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

			totalCapacity = response.ConsumedCapacity?.CapacityUnits ?? 0;
			continuationToken = response.LastEvaluatedKey?.Count > 0
				? JsonSerializer.Serialize(response.LastEvaluatedKey)
				: null;

			foreach (var item in response.Items)
			{
				var doc = DeserializeDocument<TDocument>(item);
				if (doc != null)
				{
					documents.Add(doc);
				}
			}

			LogOperationCompleted("Query", totalCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogOperationFailed("Query", ex.Message, ex);
		}

		return new CloudQueryResult<TDocument>(
			documents,
			totalCapacity,
			continuationToken);
	}

	/// <inheritdoc />
	public async Task<CloudBatchResult> ExecuteBatchAsync(
		IPartitionKey partitionKey,
		IEnumerable<ICloudBatchOperation> operations,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var operationsList = operations.ToList();
		if (operationsList.Count == 0)
		{
			return new CloudBatchResult(
				success: true,
				requestCharge: 0,
				operationResults: []);
		}

		// DynamoDB TransactWriteItems for atomic batch operations
		var transactItems = new List<TransactWriteItem>();
		foreach (var operation in operationsList)
		{
			transactItems.Add(CreateTransactWriteItem(operation, partitionKey));
		}

		var request = new TransactWriteItemsRequest
		{
			TransactItems = transactItems, ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
		};

		try
		{
			var response = await _client.TransactWriteItemsAsync(request, cancellationToken).ConfigureAwait(false);

			var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
			LogOperationCompleted("Batch", totalCapacity);

			var operationResults = operationsList.Select(_ => new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 0)).ToList();

			return new CloudBatchResult(
				success: true,
				requestCharge: totalCapacity,
				operationResults: operationResults);
		}
		catch (TransactionCanceledException ex)
		{
			LogOperationFailed("Batch", ex.Message, ex);

			var operationResults = ex.CancellationReasons?.Select(r => new CloudOperationResult(
				success: r.Code == "None",
				statusCode: r.Code == "None" ? 200 : 409,
				requestCharge: 0,
				errorMessage: r.Message)).ToList() ?? [];

			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: operationResults,
				errorMessage: ex.Message);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogOperationFailed("Batch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc />
	public async Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
		TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var subscription = new DynamoDbStreamsSubscription<TDocument>(
			_client,
			_streamsClient,
			containerName,
			options ?? ChangeFeedOptions.Default,
			_logger);

		await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
		return subscription;
	}

	#region IDocumentPersistenceProvider Implementation

	/// <inheritdoc />
	public Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use cloud-native specific methods (GetByIdAsync, CreateAsync, etc.) instead of ExecuteDocumentAsync for DynamoDB.");
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for transactional operations in DynamoDB.");
	}

	/// <inheritdoc />
	public Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<Abstractions.IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for batch operations in DynamoDB.");
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use BatchWriteItem for bulk operations in DynamoDB.");
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"DynamoDB does not support aggregation queries. Use scan/query with client-side aggregation or DynamoDB PartiQL.");
	}

	/// <inheritdoc />
	public Task<string> ExecuteIndexOperationAsync<TConnection>(
		Abstractions.IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Index management in DynamoDB is done through table/GSI configuration.");
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "DynamoDB", ["Name"] = Name, ["IsAvailable"] = IsAvailable
		};

		if (_options.DefaultTableName != null)
		{
			try
			{
				var response = await _client.DescribeTableAsync(_options.DefaultTableName, cancellationToken)
					.ConfigureAwait(false);
				stats["TableName"] = response.Table.TableName;
				if (response.Table.TableStatus is { } tableStatus)
				{
					stats["TableStatus"] = tableStatus.Value;
				}

				if (response.Table.ItemCount is { } itemCount)
				{
					stats["ItemCount"] = itemCount;
				}

				if (response.Table.TableSizeBytes is { } tableSizeBytes)
				{
					stats["TableSizeBytes"] = tableSizeBytes;
				}
			}
			catch (Exception ex)
			{
				stats["Error"] = ex.Message;
			}
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var info = new Dictionary<string, object>(StringComparer.Ordinal) { ["TableName"] = collectionName };

		try
		{
			var response = await _client.DescribeTableAsync(collectionName, cancellationToken)
				.ConfigureAwait(false);
			var table = response.Table;

			if (table.TableStatus is { } tableStatus)
			{
				info["TableStatus"] = tableStatus.Value;
			}

			if (table.ItemCount is { } itemCount)
			{
				info["ItemCount"] = itemCount;
			}

			if (table.TableSizeBytes is { } tableSizeBytes)
			{
				info["TableSizeBytes"] = tableSizeBytes;
			}

			if (table.CreationDateTime is { } creationDateTime)
			{
				info["CreationDateTime"] = creationDateTime;
			}

			if (table.KeySchema.Count > 0)
			{
				var partitionKeyName = table.KeySchema
					.FirstOrDefault(k => k.KeyType == DynamoDbKeyType.HASH)
					?.AttributeName;
				var sortKeyName = table.KeySchema
					.FirstOrDefault(k => k.KeyType == DynamoDbKeyType.RANGE)
					?.AttributeName;

				if (partitionKeyName != null)
				{
					info["PartitionKey"] = partitionKeyName;
				}

				if (sortKeyName != null)
				{
					info["SortKey"] = sortKeyName;
				}
			}

			if (table.GlobalSecondaryIndexes?.Count > 0)
			{
				info["GlobalSecondaryIndexes"] = table.GlobalSecondaryIndexes.Select(g => g.IndexName).ToList();
			}

			if (table.StreamSpecification != null)
			{
				if (table.StreamSpecification.StreamEnabled is { } streamEnabled)
				{
					info["StreamEnabled"] = streamEnabled;
				}

				var streamViewType = table.StreamSpecification.StreamViewType?.Value;
				if (streamViewType != null)
				{
					info["StreamViewType"] = streamViewType;
				}
			}
		}
		catch (Exception ex)
		{
			info["Error"] = ex.Message;
		}

		return info;
	}

	/// <inheritdoc />
	public bool ValidateDocumentRequest<TConnection, TResult>(
		Abstractions.IDocumentDataRequest<TConnection, TResult> documentRequest) =>
		documentRequest != null;

	/// <inheritdoc />
	public IEnumerable<string> GetSupportedOperationTypes() =>

		["Create", "Read", "Update", "Delete", "Query", "Scan", "Batch", "Streams"];

	#endregion IPersistenceProvider Implementation

	#region IPersistenceProvider Implementation

	/// <inheritdoc />
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		Abstractions.IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Use cloud-native specific methods for DynamoDB operations.");
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		Abstractions.IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for transactional operations in DynamoDB.");
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		throw new NotSupportedException(
			"DynamoDB uses TransactWriteItems/TransactGetItems for transactions. Use ExecuteBatchAsync instead.");
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken) =>
		await GetDocumentStoreStatisticsAsync(cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task InitializeAsync(
		IPersistenceOptions options,
		CancellationToken cancellationToken)
	{
		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		// AWS SDK manages HTTP connections internally
		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ServiceUrl"] = _options.ServiceUrl ?? "AWS",
			["Region"] = _options.Region ?? "Not specified",
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

	#endregion IDocumentPersistenceProvider Implementation

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposing(Name);

		// Acquire lock before disposing to ensure no concurrent init is in progress
		_initLock.Wait();
		_client?.Dispose();
		_streamsClient?.Dispose();
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
		LogDisposing(Name);

		// Acquire lock before disposing to ensure no concurrent init is in progress
		await _initLock.WaitAsync().ConfigureAwait(false);
		_client?.Dispose();
		_streamsClient?.Dispose();
		_initLock.Dispose();
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static TDocument? DeserializeDocument<TDocument>(Dictionary<string, AttributeValue> item)
		where TDocument : class
	{
		var doc = Document.FromAttributeMap(item);
		var json = doc.ToJson();
		return JsonSerializer.Deserialize<TDocument>(json);
	}

	private static string GetDocumentId<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document)
	{
		var idProperty = typeof(TDocument).GetProperty("id") ?? typeof(TDocument).GetProperty("Id");
		return idProperty?.GetValue(document)?.ToString() ?? Guid.NewGuid().ToString();
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static AttributeValue ToAttributeValue(object value)
	{
		return value switch
		{
			string s => new AttributeValue { S = s },
			int i => new AttributeValue { N = i.ToString() },
			long l => new AttributeValue { N = l.ToString() },
			double d => new AttributeValue { N = d.ToString() },
			decimal dec => new AttributeValue { N = dec.ToString() },
			bool b => new AttributeValue { BOOL = b },
			null => new AttributeValue { NULL = true },
			_ => new AttributeValue { S = JsonSerializer.Serialize(value) }
		};
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds), MaxErrorRetry = _options.MaxRetryAttempts
		};

		if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
		{
			config.ServiceURL = _options.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
		{
			return new AmazonDynamoDBClient(_options.AccessKey, _options.SecretKey, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private IAmazonDynamoDBStreams CreateStreamsClient()
	{
		var config = new AmazonDynamoDBStreamsConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds), MaxErrorRetry = _options.MaxRetryAttempts
		};

		if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
		{
			config.ServiceURL = _options.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
		{
			return new AmazonDynamoDBStreamsClient(_options.AccessKey, _options.SecretKey, config);
		}

		return new AmazonDynamoDBStreamsClient(config);
	}

	private Dictionary<string, AttributeValue> CreateKey(IPartitionKey partitionKey, string sortKey)
	{
		var key = new Dictionary<string, AttributeValue>
		{
			[_options.DefaultPartitionKeyAttribute] = new AttributeValue { S = partitionKey.Value }
		};

		if (!string.IsNullOrEmpty(sortKey))
		{
			key[_options.DefaultSortKeyAttribute] = new AttributeValue { S = sortKey };
		}

		return key;
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private Dictionary<string, AttributeValue> SerializeDocument<TDocument>(TDocument document, IPartitionKey partitionKey)
	{
		var json = JsonSerializer.Serialize(document);
		var item = Document.FromJson(json).ToAttributeMap();

		// Ensure partition key is set
		item[_options.DefaultPartitionKeyAttribute] = new AttributeValue { S = partitionKey.Value };

		// Set sort key from document id if available
		var id = GetDocumentId(document);
		if (!string.IsNullOrEmpty(id))
		{
			item[_options.DefaultSortKeyAttribute] = new AttributeValue { S = id };
		}

		return item;
	}

	private TransactWriteItem CreateTransactWriteItem(ICloudBatchOperation operation, IPartitionKey partitionKey)
	{
		return operation.OperationType switch
		{
			CloudBatchOperationType.Create => new TransactWriteItem
			{
				Put = new Put
				{
					TableName = _options.DefaultTableName,
					Item = SerializeDocument(((ICloudBatchCreateOperation)operation).Document, partitionKey),
					ConditionExpression = "attribute_not_exists(pk)"
				}
			},
			CloudBatchOperationType.Replace or CloudBatchOperationType.Upsert => new TransactWriteItem
			{
				Put = new Put
				{
					TableName = _options.DefaultTableName,
					Item = SerializeDocument(((ICloudBatchReplaceOperation)operation).Document, partitionKey)
				}
			},
			CloudBatchOperationType.Delete => new TransactWriteItem
			{
				Delete = new Delete { TableName = _options.DefaultTableName, Key = CreateKey(partitionKey, operation.DocumentId) }
			},
			_ => throw new NotSupportedException($"Operation type {operation.OperationType} is not supported.")
		};
	}

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
