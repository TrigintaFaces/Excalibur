// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Cosmos DB implementation of <see cref="IActivityGroupGrantService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key for optimal query patterns where activity group grants
/// are typically queried by tenant scope. Null tenants use "__null__" as the partition key.
/// </para>
/// <para>
/// Uses UpsertItemAsync for insert operations to handle duplicates gracefully.
/// </para>
/// </remarks>
public sealed partial class CosmosDbActivityGroupGrantService : IActivityGroupGrantService, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbAuthorizationOptions _options;
	private readonly ILogger<CosmosDbActivityGroupGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbActivityGroupGrantService"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbActivityGroupGrantService(
		IOptions<CosmosDbAuthorizationOptions> options,
		ILogger<CosmosDbActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query since we're deleting by userId, not tenant
		const string queryText = "SELECT c.id, c.tenant_id FROM c WHERE c.user_id = @userId AND c.grant_type = @grantType";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@userId", userId)
			.WithParameter("@grantType", grantType);

		var documentsToDelete = new List<(string Id, string TenantId)>();
		using var iterator = _container.GetItemQueryIterator<ActivityGroupDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			documentsToDelete.AddRange(response.Select(d => (d.Id, d.TenantId)));
		}

		var deletedCount = 0;
		foreach (var (id, tenantId) in documentsToDelete)
		{
			try
			{
				_ = await _container.DeleteItemAsync<ActivityGroupDocument>(
					id,
					new PartitionKey(tenantId),
					cancellationToken: cancellationToken).ConfigureAwait(false);
				deletedCount++;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				// Already deleted, continue
			}
		}

		LogActivityGroupGrantsDeletedByUser(userId, grantType, deletedCount);
		return deletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteAllActivityGroupGrantsAsync(string grantType, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query to find all documents with this grant type
		const string queryText = "SELECT c.id, c.tenant_id FROM c WHERE c.grant_type = @grantType";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@grantType", grantType);

		var documentsToDelete = new List<(string Id, string TenantId)>();
		using var iterator = _container.GetItemQueryIterator<ActivityGroupDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			documentsToDelete.AddRange(response.Select(d => (d.Id, d.TenantId)));
		}

		var deletedCount = 0;
		foreach (var (id, tenantId) in documentsToDelete)
		{
			try
			{
				_ = await _container.DeleteItemAsync<ActivityGroupDocument>(
					id,
					new PartitionKey(tenantId),
					cancellationToken: cancellationToken).ConfigureAwait(false);
				deletedCount++;
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				// Already deleted, continue
			}
		}

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
		var document = new ActivityGroupDocument
		{
			Id = ActivityGroupDocument.CreateId(userId, tenantId, grantType, qualifier),
			TenantId = ActivityGroupDocument.GetPartitionKey(tenantId),
			OriginalTenantId = tenantId,
			UserId = userId,
			FullName = fullName,
			GrantType = grantType,
			Qualifier = qualifier,
			ExpiresOn = expiresOn,
			GrantedBy = grantedBy,
			CreatedAt = now,
			UpdatedAt = now
		};

		var partitionKey = new PartitionKey(document.TenantId);
		var options = new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite };

		_ = await _container.UpsertItemAsync(
			document,
			partitionKey,
			options,
			cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantInserted(userId, grantType, qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query to get distinct user IDs
		// Note: Cosmos DB doesn't support DISTINCT in all scenarios, so we query and dedupe in memory
		const string queryText = "SELECT c.user_id FROM c WHERE c.grant_type = @grantType";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@grantType", grantType);

		var userIds = new HashSet<string>();
		using var iterator = _container.GetItemQueryIterator<ActivityGroupDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var doc in response)
			{
				_ = userIds.Add(doc.UserId);
			}
		}

		return userIds.ToList();
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

			var database = _client.GetDatabase(_options.DatabaseName);
			_container = database.GetContainer(_options.ActivityGroupsContainerName);

			// Verify connectivity
			_ = await _container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			_initialized = true;
			LogInitialized(_options.DatabaseName, _options.ActivityGroupsContainerName);
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

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutInSeconds),
			ConnectionMode = _options.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
			}
		};

		if (_options.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.ConsistencyLevel.Value;
		}

		if (_options.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.PreferredRegions.ToList();
		}

		if (_options.HttpClientFactory is not null)
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

	[LoggerMessage(DataCosmosDbEventId.ActivityGroupServiceInitialized, LogLevel.Debug,
		"Cosmos DB activity group service initialized for database '{DatabaseName}', container '{ContainerName}'")]
	private partial void LogInitialized(string databaseName, string containerName);

	[LoggerMessage(DataCosmosDbEventId.ActivityGroupGrantSaved, LogLevel.Debug,
		"Activity group grant inserted: userId={UserId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogActivityGroupGrantInserted(string userId, string grantType, string qualifier);

	[LoggerMessage(DataCosmosDbEventId.ActivityGroupGrantsDeletedByUser, LogLevel.Debug,
		"Activity group grants deleted by user: userId={UserId}, grantType={GrantType}, count={Count}")]
	private partial void LogActivityGroupGrantsDeletedByUser(string userId, string grantType, int count);

	[LoggerMessage(DataCosmosDbEventId.ActivityGroupAllGrantsDeleted, LogLevel.Debug,
		"All activity group grants deleted: grantType={GrantType}, count={Count}")]
	private partial void LogAllActivityGroupGrantsDeleted(string grantType, int count);
}
