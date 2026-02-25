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
/// DynamoDB implementation of <see cref="IGrantRequestProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key for optimal query patterns where grants
/// are typically queried by tenant scope. Null tenants use "__null__" as the partition key.
/// </para>
/// <para>
/// Uses PutItemAsync for save operations (upsert) and UpdateItemAsync for soft deletes.
/// </para>
/// </remarks>
public sealed partial class DynamoDbGrantService : IGrantRequestProvider, IGrantQueryProvider, IAsyncDisposable, IDisposable
{
	private readonly DynamoDbAuthorizationOptions _options;
	private readonly ILogger<DynamoDbGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private IAmazonDynamoDB? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbGrantService"/> class.
	/// </summary>
	/// <param name="options">The DynamoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbGrantService(
		IOptions<DynamoDbAuthorizationOptions> options,
		ILogger<DynamoDbGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbGrantService"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The DynamoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbGrantService(
		IAmazonDynamoDB client,
		IOptions<DynamoDbAuthorizationOptions> options,
		ILogger<DynamoDbGrantService> logger)
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
	public async Task<int> DeleteGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = GrantItem.CreateKey(tenantId, userId, grantType, qualifier);

		try
		{
			if (revokedBy is not null && revokedOn.HasValue)
			{
				// Soft delete by marking as revoked
				var (updateExpression, expressionValues) = GrantItem.CreateRevokeUpdate(revokedBy, revokedOn.Value);

				var updateRequest = new UpdateItemRequest
				{
					TableName = _options.GrantsTableName,
					Key = key,
					UpdateExpression = updateExpression,
					ExpressionAttributeValues = expressionValues,
					ConditionExpression = $"attribute_exists({GrantItem.PartitionKeyAttribute})"
				};

				_ = await _client.UpdateItemAsync(updateRequest, cancellationToken).ConfigureAwait(false);
				LogGrantRevoked(userId, tenantId ?? "null", grantType, qualifier);
				return 1;
			}
			else
			{
				// Hard delete
				var deleteRequest = new DeleteItemRequest
				{
					TableName = _options.GrantsTableName,
					Key = key,
					ConditionExpression = $"attribute_exists({GrantItem.PartitionKeyAttribute})"
				};

				_ = await _client.DeleteItemAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
				LogGrantDeleted(userId, tenantId ?? "null", grantType, qualifier);
				return 1;
			}
		}
		catch (ConditionalCheckFailedException)
		{
			// Grant doesn't exist
			return 0;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> GrantExistsAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = GrantItem.CreateKey(tenantId, userId, grantType, qualifier);

		var request = new GetItemRequest
		{
			TableName = _options.GrantsTableName,
			Key = key,
			ConsistentRead = _options.UseConsistentReads,
			ProjectionExpression = $"{GrantItem.IsRevokedAttribute}"
		};

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return false;
		}

		// Check if revoked
		if (response.Item.TryGetValue(GrantItem.IsRevokedAttribute, out var isRevokedAttr) &&
			isRevokedAttr.BOOL == true)
		{
			return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
		string? userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = GrantItem.CreatePK(tenantId);

		var expressionValues = new Dictionary<string, AttributeValue>
		{
			[":pk"] = new() { S = pk },
			[":grantType"] = new() { S = grantType },
			[":qualifier"] = new() { S = qualifier },
			[":revoked"] = new() { BOOL = false }
		};

		var filterExpression =
			$"{GrantItem.GrantTypeAttribute} = :grantType AND {GrantItem.QualifierAttribute} = :qualifier AND {GrantItem.IsRevokedAttribute} = :revoked";

		if (userId is not null)
		{
			expressionValues[":userId"] = new() { S = userId };
			filterExpression += $" AND {GrantItem.UserIdAttribute} = :userId";
		}

		var request = new QueryRequest
		{
			TableName = _options.GrantsTableName,
			KeyConditionExpression = $"{GrantItem.PartitionKeyAttribute} = :pk",
			FilterExpression = filterExpression,
			ExpressionAttributeValues = expressionValues,
			ConsistentRead = _options.UseConsistentReads
		};

		var results = new List<Grant>();
		QueryResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				var grant = GrantItem.FromItem(item);
				if (grant is not null)
				{
					results.Add(grant);
				}
			}
		} while (response.LastEvaluatedKey?.Count > 0);

		return results;
	}

	/// <inheritdoc/>
	public async Task<Grant?> GetGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var key = GrantItem.CreateKey(tenantId, userId, grantType, qualifier);

		var request = new GetItemRequest { TableName = _options.GrantsTableName, Key = key, ConsistentRead = _options.UseConsistentReads };

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return null;
		}

		return GrantItem.FromItem(response.Item);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Use GSI for cross-tenant user queries
		var request = new QueryRequest
		{
			TableName = _options.GrantsTableName,
			IndexName = _options.UserIndexName,
			KeyConditionExpression = $"{GrantItem.GsiUserIdAttribute} = :userId",
			FilterExpression = $"{GrantItem.IsRevokedAttribute} = :revoked",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":userId"] = new() { S = userId },
				[":revoked"] = new() { BOOL = false }
			}
		};

		var results = new List<Grant>();
		QueryResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				var grant = GrantItem.FromItem(item);
				if (grant is not null)
				{
					results.Add(grant);
				}
			}
		} while (response.LastEvaluatedKey?.Count > 0);

		return results;
	}

	/// <inheritdoc/>
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(grant);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var item = GrantItem.ToItem(grant);

		var request = new PutItemRequest { TableName = _options.GrantsTableName, Item = item };

		_ = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);

		LogGrantSaved(grant.UserId, grant.TenantId ?? "null", grant.GrantType, grant.Qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Use GSI for cross-tenant user queries
		var request = new QueryRequest
		{
			TableName = _options.GrantsTableName,
			IndexName = _options.UserIndexName,
			KeyConditionExpression = $"{GrantItem.GsiUserIdAttribute} = :userId",
			FilterExpression = $"{GrantItem.IsRevokedAttribute} = :revoked",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":userId"] = new() { S = userId },
				[":revoked"] = new() { BOOL = false }
			}
		};

		var result = new Dictionary<string, object>();
		QueryResponse? response = null;

		do
		{
			if (response?.LastEvaluatedKey?.Count > 0)
			{
				request.ExclusiveStartKey = response.LastEvaluatedKey;
			}

			response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				var grant = GrantItem.FromItem(item);
				if (grant is not null)
				{
					var key = $"{grant.GrantType}:{grant.Qualifier}";
					result[key] = grant;
				}
			}
		} while (response.LastEvaluatedKey?.Count > 0);

		return result;
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
			_ = await _client.DescribeTableAsync(_options.GrantsTableName, cancellationToken).ConfigureAwait(false);

			_initialized = true;
			LogInitialized(_options.GrantsTableName);
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		if (serviceType == typeof(IGrantQueryProvider))
		{
			return this;
		}

		return null;
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

	[LoggerMessage(DataDynamoDbEventId.GrantServiceInitialized, LogLevel.Debug,
		"DynamoDB grant service initialized for table '{TableName}'")]
	private partial void LogInitialized(string tableName);

	[LoggerMessage(DataDynamoDbEventId.GrantSaved, LogLevel.Debug,
		"Grant saved: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantSaved(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataDynamoDbEventId.GrantDeleted, LogLevel.Debug,
		"Grant deleted: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantDeleted(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataDynamoDbEventId.GrantRevoked, LogLevel.Debug,
		"Grant revoked: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantRevoked(string userId, string tenantId, string grantType, string qualifier);
}
