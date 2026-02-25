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
/// Cosmos DB implementation of <see cref="IGrantRequestProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key for optimal query patterns where grants
/// are typically queried by tenant scope. Null tenants use "__null__" as the partition key.
/// </para>
/// <para>
/// Uses UpsertItemAsync for save operations to handle both insert and update scenarios.
/// </para>
/// </remarks>
public sealed partial class CosmosDbGrantService : IGrantRequestProvider, IGrantQueryProvider, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbAuthorizationOptions _options;
	private readonly ILogger<CosmosDbGrantService> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbGrantService"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbGrantService(
		IOptions<CosmosDbAuthorizationOptions> options,
		ILogger<CosmosDbGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
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

		var documentId = GrantDocument.CreateId(userId, tenantId, grantType, qualifier);
		var partitionKey = new PartitionKey(GrantDocument.GetPartitionKey(tenantId));

		try
		{
			if (revokedBy is not null && revokedOn.HasValue)
			{
				// Soft delete by marking as revoked
				var response = await _container.ReadItemAsync<GrantDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				var document = response.Resource;
				document.IsRevoked = true;
				document.RevokedBy = revokedBy;
				document.RevokedOn = revokedOn;

				_ = await _container.ReplaceItemAsync(
					document,
					documentId,
					partitionKey,
					new ItemRequestOptions { IfMatchEtag = response.ETag },
					cancellationToken).ConfigureAwait(false);

				LogGrantRevoked(userId, tenantId ?? "null", grantType, qualifier);
				return 1;
			}
			else
			{
				// Hard delete
				_ = await _container.DeleteItemAsync<GrantDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				LogGrantDeleted(userId, tenantId ?? "null", grantType, qualifier);
				return 1;
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
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

		var documentId = GrantDocument.CreateId(userId, tenantId, grantType, qualifier);
		var partitionKey = new PartitionKey(GrantDocument.GetPartitionKey(tenantId));

		try
		{
			var response = await _container.ReadItemAsync<GrantDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return !response.Resource.IsRevoked;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
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

		var partitionKeyValue = GrantDocument.GetPartitionKey(tenantId);
		var queryParts = new List<string>
		{
			"SELECT * FROM c WHERE c.tenant_id = @tenantId",
			"AND c.grant_type = @grantType",
			"AND c.qualifier = @qualifier",
			"AND c.is_revoked = false"
		};

		var queryDefinition = new QueryDefinition(string.Join(" ", queryParts))
			.WithParameter("@tenantId", partitionKeyValue)
			.WithParameter("@grantType", grantType)
			.WithParameter("@qualifier", qualifier);

		if (userId is not null)
		{
			queryDefinition = new QueryDefinition(string.Join(" ", queryParts) + " AND c.user_id = @userId")
				.WithParameter("@tenantId", partitionKeyValue)
				.WithParameter("@grantType", grantType)
				.WithParameter("@qualifier", qualifier)
				.WithParameter("@userId", userId);
		}

		var queryOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKeyValue) };

		var results = new List<Grant>();
		using var iterator = _container.GetItemQueryIterator<GrantDocument>(queryDefinition, requestOptions: queryOptions);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			results.AddRange(response.Select(d => d.ToGrant()));
		}

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

		var documentId = GrantDocument.CreateId(userId, tenantId, grantType, qualifier);
		var partitionKey = new PartitionKey(GrantDocument.GetPartitionKey(tenantId));

		try
		{
			var response = await _container.ReadItemAsync<GrantDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			return document.IsRevoked ? null : document.ToGrant();
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query since we're querying by userId, not tenant
		const string queryText = "SELECT * FROM c WHERE c.user_id = @userId AND c.is_revoked = false";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@userId", userId);

		var results = new List<Grant>();
		using var iterator = _container.GetItemQueryIterator<GrantDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			results.AddRange(response.Select(d => d.ToGrant()));
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(grant);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = GrantDocument.FromGrant(grant);
		var partitionKey = new PartitionKey(document.TenantId);

		var options = new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite };

		_ = await _container.UpsertItemAsync(
			document,
			partitionKey,
			options,
			cancellationToken).ConfigureAwait(false);

		LogGrantSaved(grant.UserId, grant.TenantId ?? "null", grant.GrantType, grant.Qualifier);
		return 1;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query since we're querying by userId, not tenant
		const string queryText = "SELECT * FROM c WHERE c.user_id = @userId AND c.is_revoked = false";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@userId", userId);

		var result = new Dictionary<string, object>();
		using var iterator = _container.GetItemQueryIterator<GrantDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var doc in response)
			{
				var key = $"{doc.GrantType}:{doc.Qualifier}";
				result[key] = doc.ToGrant();
			}
		}

		return result;
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
			_container = database.GetContainer(_options.GrantsContainerName);

			// Verify connectivity
			_ = await _container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			_initialized = true;
			LogInitialized(_options.DatabaseName, _options.GrantsContainerName);
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

	[LoggerMessage(DataCosmosDbEventId.GrantServiceInitialized, LogLevel.Debug,
		"Cosmos DB grant service initialized for database '{DatabaseName}', container '{ContainerName}'")]
	private partial void LogInitialized(string databaseName, string containerName);

	[LoggerMessage(DataCosmosDbEventId.GrantSaved, LogLevel.Debug,
		"Grant saved: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantSaved(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataCosmosDbEventId.GrantDeleted, LogLevel.Debug,
		"Grant deleted: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantDeleted(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataCosmosDbEventId.GrantRevoked, LogLevel.Debug,
		"Grant revoked: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantRevoked(string userId, string tenantId, string grantType, string qualifier);
}
