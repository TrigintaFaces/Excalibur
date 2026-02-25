// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.DynamoDb.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Authorization;

/// <summary>
/// DynamoDB implementation of <see cref="IActivityGroupGrantService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key for optimal query patterns where activity group grants
/// are typically queried by tenant scope. Null tenants use "__null__" as the partition key.
/// </para>
/// <para>
/// Uses PutItemAsync for upsert operations and BatchWriteItemAsync for bulk deletes.
/// </para>
/// </remarks>
public sealed partial class DynamoDbActivityGroupGrantService : IActivityGroupGrantService, IAsyncDisposable, IDisposable
{
	private const int MaxBatchSize = 25; // DynamoDB limit for BatchWriteItem

	private readonly DynamoDbAuthorizationOptions _options;
	private readonly ILogger<DynamoDbActivityGroupGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private IAmazonDynamoDB? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbActivityGroupGrantService"/> class.
	/// </summary>
	/// <param name="options">The DynamoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbActivityGroupGrantService(
		IOptions<DynamoDbAuthorizationOptions> options,
		ILogger<DynamoDbActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbActivityGroupGrantService"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The DynamoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbActivityGroupGrantService(
		IAmazonDynamoDB client,
		IOptions<DynamoDbAuthorizationOptions> options,
		ILogger<DynamoDbActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(
		string userId,
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Use GSI to find all activity groups for this user with this grant type
		var request = new QueryRequest
		{
			TableName = _options.ActivityGroupsTableName,
			IndexName = _options.UserIndexName,
			KeyConditionExpression = $"{ActivityGroupItem.GsiUserIdAttribute} = :userId",
			FilterExpression = $"{ActivityGroupItem.GrantTypeAttribute} = :grantType",
			ProjectionExpression = $"{ActivityGroupItem.PartitionKeyAttribute}, {ActivityGroupItem.SortKeyAttribute}",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":userId"] = new() { S = userId },
				[":grantType"] = new() { S = grantType }
			}
		};

		var itemsToDelete = new List<(string PK, string SK)>();
		QueryResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
			itemsToDelete.AddRange(response.Items.Select(item =>
				(ActivityGroupItem.GetTenantIdPK(item), ActivityGroupItem.GetSK(item))));
		} while (response.LastEvaluatedKey?.Count > 0);

		// Delete in batches of 25 (DynamoDB limit)
		var deletedCount = await DeleteItemsInBatchesAsync(itemsToDelete, cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantsDeletedByUser(userId, grantType, deletedCount);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteAllActivityGroupGrantsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Scan to find all activity groups with this grant type
		var scanRequest = new ScanRequest
		{
			TableName = _options.ActivityGroupsTableName,
			FilterExpression = $"{ActivityGroupItem.GrantTypeAttribute} = :grantType",
			ProjectionExpression = $"{ActivityGroupItem.PartitionKeyAttribute}, {ActivityGroupItem.SortKeyAttribute}",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":grantType"] = new() { S = grantType } }
		};

		var itemsToDelete = new List<(string PK, string SK)>();
		ScanResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				scanRequest.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.ScanAsync(scanRequest, cancellationToken).ConfigureAwait(false);
			itemsToDelete.AddRange(response.Items.Select(item =>
				(ActivityGroupItem.GetTenantIdPK(item), ActivityGroupItem.GetSK(item))));
		} while (response.LastEvaluatedKey?.Count > 0);

		// Delete in batches of 25 (DynamoDB limit)
		var deletedCount = await DeleteItemsInBatchesAsync(itemsToDelete, cancellationToken).ConfigureAwait(false);

		LogAllActivityGroupGrantsDeleted(grantType, deletedCount);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> InsertActivityGroupGrantAsync(
		string userId,
		string fullName,
		string? tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var item = ActivityGroupItem.ToItem(
			userId,
			fullName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			now,
			now);

		var request = new PutItemRequest { TableName = _options.ActivityGroupsTableName, Item = item };

		_ = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantInserted(userId, grantType, qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Scan to find all activity groups with this grant type and get distinct user IDs
		var scanRequest = new ScanRequest
		{
			TableName = _options.ActivityGroupsTableName,
			FilterExpression = $"{ActivityGroupItem.GrantTypeAttribute} = :grantType",
			ProjectionExpression = ActivityGroupItem.UserIdAttribute,
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":grantType"] = new() { S = grantType } }
		};

		var userIds = new HashSet<string>();
		ScanResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				scanRequest.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.ScanAsync(scanRequest, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				_ = userIds.Add(ActivityGroupItem.GetUserId(item));
			}
		} while (response.LastEvaluatedKey?.Count > 0);

		return userIds.ToList();
	}

	/// <summary>
	/// Initializes the DynamoDB client.
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

			_client = CreateClient();

			// Verify connectivity by describing the table
			_ = await _client.DescribeTableAsync(_options.ActivityGroupsTableName, cancellationToken).ConfigureAwait(false);

			_initialized = true;
			LogInitialized(_options.ActivityGroupsTableName);
		}
		finally
		{
			_ = _initLock.Release();
		}
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

	private async Task<int> DeleteItemsInBatchesAsync(
		List<(string PK, string SK)> items,
		CancellationToken cancellationToken)
	{
		var deletedCount = 0;

		for (var i = 0; i < items.Count; i += MaxBatchSize)
		{
			var batch = items.Skip(i).Take(MaxBatchSize).ToList();
			var writeRequests = batch.Select(item => new WriteRequest
			{
				DeleteRequest = new DeleteRequest
				{
					Key = new Dictionary<string, AttributeValue>
					{
						[ActivityGroupItem.PartitionKeyAttribute] = new() { S = item.PK },
						[ActivityGroupItem.SortKeyAttribute] = new() { S = item.SK }
					}
				}
			}).ToList();

			var batchRequest = new BatchWriteItemRequest
			{
				RequestItems = new Dictionary<string, List<WriteRequest>> { [_options.ActivityGroupsTableName] = writeRequests }
			};

			var batchResponse = await _client.BatchWriteItemAsync(batchRequest, cancellationToken)
				.ConfigureAwait(false);

			var unprocessedCount = batchResponse.UnprocessedItems.TryGetValue(
				_options.ActivityGroupsTableName, out var unprocessed)
				? unprocessed.Count
				: 0;

			deletedCount += writeRequests.Count - unprocessedCount;
		}

		return deletedCount;
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds),
			MaxErrorRetry = _options.MaxRetryAttempts
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
			var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
			return new AmazonDynamoDBClient(credentials, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	[LoggerMessage(DataDynamoDbEventId.ActivityGroupServiceInitialized, LogLevel.Debug,
		"DynamoDB activity group service initialized for table '{TableName}'")]
	private partial void LogInitialized(string tableName);

	[LoggerMessage(DataDynamoDbEventId.ActivityGroupGrantInserted, LogLevel.Debug,
		"Activity group grant inserted: userId={UserId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogActivityGroupGrantInserted(string userId, string grantType, string qualifier);

	[LoggerMessage(DataDynamoDbEventId.ActivityGroupGrantsDeletedByUser, LogLevel.Debug,
		"Activity group grants deleted by user: userId={UserId}, grantType={GrantType}, count={Count}")]
	private partial void LogActivityGroupGrantsDeletedByUser(string userId, string grantType, int count);

	[LoggerMessage(DataDynamoDbEventId.ActivityGroupAllGrantsDeleted, LogLevel.Debug,
		"All activity group grants deleted: grantType={GrantType}, count={Count}")]
	private partial void LogAllActivityGroupGrantsDeleted(string grantType, int count);
}
