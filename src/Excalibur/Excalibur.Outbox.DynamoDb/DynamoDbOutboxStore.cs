// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// AWS DynamoDB implementation of the cloud-native outbox store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud outbox implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbOutboxStore : ICloudNativeOutboxStore, IAsyncDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly DynamoDbOutboxOptions _options;
	private readonly ILogger<DynamoDbOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private IAmazonDynamoDB? _client;
	private IAmazonDynamoDBStreams? _streamsClient;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The DynamoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbOutboxStore(
		IOptions<DynamoDbOutboxOptions> options,
		ILogger<DynamoDbOutboxStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();
	}

	/// <inheritdoc/>
	public CloudProviderType ProviderType => CloudProviderType.DynamoDb;

	/// <summary>
	/// Initializes the DynamoDB client and creates the table if needed.
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

			LogInitializing(_options.TableName);

			_client = CreateClient();
			_streamsClient = CreateStreamsClient();

			if (_options.CreateTableIfNotExists)
			{
				await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);
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
		var item = ToAttributeMap(message, partitionKey);

		try
		{
			var request = new PutItemRequest
			{
				TableName = _options.TableName,
				Item = item,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("Add", consumedCapacity);

			return new CloudOperationResult<CloudOutboxMessage>(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity,
				document: message);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"add",
				message.MessageId,
				message.CorrelationId,
				message.CausationId);
			LogOperationFailed("Add", ex.Message, ex);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
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
		var writeRequests = messages
			.Select(m => new WriteRequest { PutRequest = new PutRequest { Item = ToAttributeMap(m, partitionKey) } }).ToList();

		try
		{
			var request = new BatchWriteItemRequest
			{
				RequestItems = new Dictionary<string, List<WriteRequest>> { [_options.TableName] = writeRequests },
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.BatchWriteItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.Sum(c => c.WriteCapacityUnits) ?? 0;

			LogOperationCompleted("AddBatch", consumedCapacity);

			var operationResults = writeRequests.Select(_ => new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 0)).ToList();

			if (response.HttpStatusCode != HttpStatusCode.OK)
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: response.HttpStatusCode == HttpStatusCode.OK,
				requestCharge: consumedCapacity,
				operationResults: operationResults);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
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
				WriteStoreTelemetry.Providers.DynamoDb,
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
			var request = new QueryRequest
			{
				TableName = _options.TableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				FilterExpression = "isPublished = :false",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new() { S = partitionKey.Value },
					[":false"] = new() { BOOL = false }
				},
				Limit = batchSize,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.ReadCapacityUnits ?? 0;

			var messages = response.Items.Select(FromAttributeMap).ToList();

			LogOperationCompleted("GetPending", consumedCapacity);

			return new CloudQueryResult<CloudOutboxMessage>(
				messages,
				consumedCapacity,
				response.LastEvaluatedKey?.Count > 0 ? JsonSerializer.Serialize(response.LastEvaluatedKey) : null);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"get_pending");
			LogOperationFailed("GetPending", ex.Message, ex);
			return new CloudQueryResult<CloudOutboxMessage>([], 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
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
		var publishedAt = DateTimeOffset.UtcNow;
		var ttl = _options.DefaultTimeToLiveSeconds > 0
			? publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds).ToUnixTimeSeconds()
			: 0;

		try
		{
			var request = new UpdateItemRequest
			{
				TableName = _options.TableName,
				Key = new Dictionary<string, AttributeValue>
				{
					[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
					[_options.SortKeyAttribute] = new() { S = messageId }
				},
				UpdateExpression = "SET isPublished = :true, publishedAt = :publishedAt" +
								   (ttl > 0 ? $", {_options.TtlAttribute} = :ttl" : ""),
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":true"] = new() { BOOL = true },
					[":publishedAt"] = new() { S = publishedAt.ToString("o") }
				},
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			if (ttl > 0)
			{
				request.ExpressionAttributeValues[":ttl"] = new() { N = ttl.ToString() };
			}

			var response = await _client.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("MarkAsPublished", consumedCapacity);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"mark_published",
				messageId);
			LogOperationFailed("MarkAsPublished", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
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

		try
		{
			var operationResults = new List<CloudOperationResult>();
			double totalCapacity = 0;

			foreach (var messageId in messageIds)
			{
				var operationResult = await MarkAsPublishedAsync(messageId, partitionKey, cancellationToken)
					.ConfigureAwait(false);
				operationResults.Add(operationResult);
				totalCapacity += operationResult.RequestCharge;
			}

			if (operationResults.Any(r => !r.Success))
			{
				result = WriteStoreTelemetry.Results.Failure;
			}

			return new CloudBatchResult(
				success: operationResults.All(r => r.Success),
				requestCharge: totalCapacity,
				operationResults: operationResults);
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
				WriteStoreTelemetry.Providers.DynamoDb,
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
		var deletedCount = 0;
		double totalCapacity = 0;

		try
		{
			var queryRequest = new QueryRequest
			{
				TableName = _options.TableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				FilterExpression = "isPublished = :true AND publishedAt < :cutoff",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new() { S = partitionKey.Value },
					[":true"] = new() { BOOL = true },
					[":cutoff"] = new() { S = cutoffDate }
				},
				ProjectionExpression = $"{_options.PartitionKeyAttribute}, {_options.SortKeyAttribute}",
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var queryResponse = await _client.QueryAsync(queryRequest, cancellationToken).ConfigureAwait(false);
			totalCapacity += queryResponse.ConsumedCapacity?.ReadCapacityUnits ?? 0;

			foreach (var item in queryResponse.Items)
			{
				var deleteRequest = new DeleteItemRequest
				{
					TableName = _options.TableName,
					Key = new Dictionary<string, AttributeValue>
					{
						[_options.PartitionKeyAttribute] = item[_options.PartitionKeyAttribute],
						[_options.SortKeyAttribute] = item[_options.SortKeyAttribute]
					},
					ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
				};

				var deleteResponse = await _client.DeleteItemAsync(deleteRequest, cancellationToken)
					.ConfigureAwait(false);
				totalCapacity += deleteResponse.ConsumedCapacity?.WriteCapacityUnits ?? 0;
				deletedCount++;
			}

			LogOperationCompleted("CleanupOldMessages", totalCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"cleanup_old");
			LogOperationFailed("CleanupOldMessages", ex.Message, ex);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"cleanup_old",
				result,
				stopwatch.Elapsed);
		}

		return new CloudCleanupResult(deletedCount, totalCapacity);
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
		DynamoDbOutboxStreamsSubscription? subscription = null;

		try
		{
			subscription = new DynamoDbOutboxStreamsSubscription(
				_client,
				_streamsClient,
				_options.TableName,
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
				WriteStoreTelemetry.Providers.DynamoDb,
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
			var updateExpression = "SET retryCount = retryCount + :inc";
			var expressionValues = new Dictionary<string, AttributeValue> { [":inc"] = new() { N = "1" } };

			if (!string.IsNullOrEmpty(errorMessage))
			{
				updateExpression += ", lastError = :error";
				expressionValues[":error"] = new() { S = errorMessage };
			}

			var request = new UpdateItemRequest
			{
				TableName = _options.TableName,
				Key = new Dictionary<string, AttributeValue>
				{
					[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
					[_options.SortKeyAttribute] = new() { S = messageId }
				},
				UpdateExpression = updateExpression,
				ExpressionAttributeValues = expressionValues,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
			var consumedCapacity = response.ConsumedCapacity?.WriteCapacityUnits ?? 0;

			LogOperationCompleted("IncrementRetryCount", consumedCapacity);

			return new CloudOperationResult(
				success: true,
				statusCode: (int)response.HttpStatusCode,
				requestCharge: consumedCapacity);
		}
		catch (AmazonDynamoDBException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"increment_retry",
				messageId);
			LogOperationFailed("IncrementRetryCount", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: (int)ex.StatusCode,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.DynamoDb,
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
		_streamsClient?.Dispose();
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig { MaxErrorRetry = _options.MaxRetryAttempts };

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
			var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
			return new AmazonDynamoDBClient(credentials, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private IAmazonDynamoDBStreams CreateStreamsClient()
	{
		var config = new AmazonDynamoDBStreamsConfig { MaxErrorRetry = _options.MaxRetryAttempts };

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
			var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
			return new AmazonDynamoDBStreamsClient(credentials, config);
		}

		return new AmazonDynamoDBStreamsClient(config);
	}

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			var request = new CreateTableRequest
			{
				TableName = _options.TableName,
				KeySchema =
				[
					new() { AttributeName = _options.PartitionKeyAttribute, KeyType = Amazon.DynamoDBv2.KeyType.HASH },
					new() { AttributeName = _options.SortKeyAttribute, KeyType = Amazon.DynamoDBv2.KeyType.RANGE }
				],
				AttributeDefinitions =
				[
					new() { AttributeName = _options.PartitionKeyAttribute, AttributeType = ScalarAttributeType.S },
					new() { AttributeName = _options.SortKeyAttribute, AttributeType = ScalarAttributeType.S }
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			};

			if (_options.EnableStreams)
			{
				request.StreamSpecification = new StreamSpecification
				{
					StreamEnabled = true,
					StreamViewType = Amazon.DynamoDBv2.StreamViewType.NEW_AND_OLD_IMAGES
				};
			}

			_ = await _client.CreateTableAsync(request, cancellationToken).ConfigureAwait(false);

			// Wait for table to become active
			var timeout = DateTimeOffset.UtcNow.AddMinutes(2);
			while (DateTimeOffset.UtcNow < timeout)
			{
				var describe = await _client.DescribeTableAsync(_options.TableName, cancellationToken)
					.ConfigureAwait(false);
				if (describe.Table.TableStatus == TableStatus.ACTIVE)
				{
					break;
				}

				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}

			// Enable TTL if configured
			if (_options.DefaultTimeToLiveSeconds > 0)
			{
				_ = await _client.UpdateTimeToLiveAsync(
					new UpdateTimeToLiveRequest
					{
						TableName = _options.TableName,
						TimeToLiveSpecification = new TimeToLiveSpecification { Enabled = true, AttributeName = _options.TtlAttribute }
					}, cancellationToken).ConfigureAwait(false);
			}
		}
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

	private Dictionary<string, AttributeValue> ToAttributeMap(CloudOutboxMessage message, IPartitionKey partitionKey)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
			[_options.SortKeyAttribute] = new() { S = message.MessageId },
			["messageType"] = new() { S = message.MessageType },
			["payload"] = new() { S = Convert.ToBase64String(message.Payload) },
			["createdAt"] = new() { S = message.CreatedAt.ToString("o") },
			["isPublished"] = new() { BOOL = message.IsPublished },
			["retryCount"] = new() { N = message.RetryCount.ToString() }
		};

		if (message.Headers != null)
		{
			item["headers"] = new() { S = JsonSerializer.Serialize(message.Headers, JsonOptions) };
		}

		if (!string.IsNullOrEmpty(message.AggregateId))
		{
			item["aggregateId"] = new() { S = message.AggregateId };
		}

		if (!string.IsNullOrEmpty(message.AggregateType))
		{
			item["aggregateType"] = new() { S = message.AggregateType };
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			item["correlationId"] = new() { S = message.CorrelationId };
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			item["causationId"] = new() { S = message.CausationId };
		}

		if (message.PublishedAt.HasValue)
		{
			item["publishedAt"] = new() { S = message.PublishedAt.Value.ToString("o") };
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			item["lastError"] = new() { S = message.LastError };
		}

		return item;
	}

	private CloudOutboxMessage FromAttributeMap(Dictionary<string, AttributeValue> item)
	{
		return new CloudOutboxMessage
		{
			MessageId = item[_options.SortKeyAttribute].S,
			MessageType = item["messageType"].S,
			Payload = Convert.FromBase64String(item["payload"].S),
			Headers = item.TryGetValue("headers", out var headers) && !string.IsNullOrEmpty(headers.S)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(headers.S, JsonOptions)
				: null,
			AggregateId = item.TryGetValue("aggregateId", out var aggId) ? aggId.S : null,
			AggregateType = item.TryGetValue("aggregateType", out var aggType) ? aggType.S : null,
			CorrelationId = item.TryGetValue("correlationId", out var corrId) ? corrId.S : null,
			CausationId = item.TryGetValue("causationId", out var causId) ? causId.S : null,
			CreatedAt = DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
			PublishedAt = item.TryGetValue("publishedAt", out var pubAt) && !string.IsNullOrEmpty(pubAt.S)
				? DateTimeOffset.Parse(pubAt.S, CultureInfo.InvariantCulture)
				: null,
			RetryCount = int.Parse(item["retryCount"].N),
			LastError = item.TryGetValue("lastError", out var err) ? err.S : null,
			PartitionKeyValue = item[_options.PartitionKeyAttribute].S
		};
	}
}
