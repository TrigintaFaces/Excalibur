// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.A3.Authorization;
using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Cosmos DB implementation of <see cref="IGrantStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key for optimal query patterns where grants
/// are typically queried by tenant scope.
/// </para>
/// <para>
/// Uses UpsertItemAsync for save operations to handle both insert and update scenarios.
/// </para>
/// </remarks>
public sealed partial class CosmosDbGrantStore : IGrantStore, IDurableGrantStore, IGrantQueryStore, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbAuthorizationOptions _options;
	private readonly ILogger<CosmosDbGrantStore> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	/// <summary>
	/// Whether this store created the Cosmos client it holds, and may therefore dispose it.
	/// </summary>
	/// <remarks>
	/// A store handed the host's shared client must not dispose it: the client is a singleton several
	/// features share, and disposing it leaves every other feature throwing ObjectDisposedException from
	/// a call that names this store's disposal rather than anything the caller did. The flag is set only
	/// on the path that constructs one.
	/// </remarks>
	private bool _ownsClient;
	private Container? _container;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbGrantStore"/> class.
	/// </summary>
	/// <param name="options">The Cosmos DB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="timeProvider">Time source used to evaluate grant expiry. Defaults to <see cref="System.TimeProvider.System"/> when not supplied.</param>
	public CosmosDbGrantStore(
		IOptions<CosmosDbAuthorizationOptions> options,
		ILogger<CosmosDbGrantStore> logger,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbGrantStore"/> class over a client the host owns.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="client">The Cosmos client registered by the host. Borrowed, never disposed here.</param>
	/// <param name="timeProvider">The time source, or <see langword="null"/> for the system clock.</param>
	/// <remarks>
	/// Selected by dependency injection whenever a <see cref="CosmosClient"/> is registered, which the
	/// Cosmos registration does. Borrowing that client is what keeps a host enabling several Cosmos
	/// features on one connection pool rather than one per feature, and the store does not dispose it.
	/// </remarks>
	public CosmosDbGrantStore(
		IOptions<CosmosDbAuthorizationOptions> options,
		ILogger<CosmosDbGrantStore> logger,
		CosmosClient client,
		TimeProvider? timeProvider = null)
		: this(options, logger, timeProvider)
	{
		ArgumentNullException.ThrowIfNull(client);
		_client = client;
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
		var partitionKey = new PartitionKey(tenantId);

		try
		{
			if (revokedBy is not null && revokedOn.HasValue)
			{
				// Soft delete by marking as revoked
				var response = await _container!.ReadItemAsync<GrantDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				var document = response.Resource;
				document.IsRevoked = true;
				document.RevokedBy = revokedBy;
				document.RevokedOn = revokedOn;

				_ = await _container!.ReplaceItemAsync(
					document,
					documentId,
					partitionKey,
					new ItemRequestOptions { IfMatchEtag = response.ETag },
					cancellationToken).ConfigureAwait(false);

				LogGrantRevoked(userId, tenantId, grantType, qualifier);
				return 1;
			}
			else
			{
				// Hard delete
				_ = await _container!.DeleteItemAsync<GrantDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				LogGrantDeleted(userId, tenantId, grantType, qualifier);
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
		var partitionKey = new PartitionKey(tenantId);

		try
		{
			var response = await _container!.ReadItemAsync<GrantDocument>(
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

		var partitionKeyValue = tenantId;
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
		using var iterator = _container!.GetItemQueryIterator<GrantDocument>(queryDefinition, requestOptions: queryOptions);

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
		var partitionKey = new PartitionKey(tenantId);

		try
		{
			var response = await _container!.ReadItemAsync<GrantDocument>(
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
	public Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken) =>
		GetAllGrantsAsync(userId, includeExpired: false, cancellationToken);

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Cross-partition query since we're querying by userId, not tenant
		const string queryText = "SELECT * FROM c WHERE c.user_id = @userId AND c.is_revoked = false";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@userId", userId);

		// Default-secure: exclude expired grants unless explicitly requested.
		var now = _timeProvider.GetUtcNow();
		var results = new List<Grant>();
		using var iterator = _container!.GetItemQueryIterator<GrantDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			results.AddRange(response.Select(d => d.ToGrant()).Where(g => includeExpired || g.IsActive(now)));
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

		var options = new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite };

		_ = await _container!.UpsertItemAsync(
			document,
			partitionKey,
			options,
			cancellationToken).ConfigureAwait(false);

		LogGrantSaved(grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier);
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
		using var iterator = _container!.GetItemQueryIterator<GrantDocument>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var doc in response)
			{
				var grant = doc.ToGrant();
				var key = $"{grant.TenantId}:{grant.GrantType}:{grant.Qualifier}";
				result[key] = grant;
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
			// Only when the host supplied none. A store that borrows the registered client shares its
			// connection pool with every other Cosmos feature instead of opening a second one.
			if (_client is null)
			{
				_client = CreateClient(clientOptions);
				_ownsClient = true;
			}

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
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IDurableGrantStore))
		{
			return this;
		}

		if (serviceType == typeof(IGrantQueryStore))
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

		if (_ownsClient)
		{
			_client?.Dispose();
		}

		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
			}
		};

		if (_options.Client.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.Client.ConsistencyLevel.Value;
		}

		if (_options.Client.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.Client.PreferredRegions.ToList();
		}

		if (_options.Client.HttpClientFactory is not null)
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
