// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Projections;

/// <summary>
/// Cosmos DB implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using native Cosmos DB JSON documents with UpsertItemAsync
/// for atomic insert-or-update operations. Uses projectionType as partition key for efficient
/// queries within projection type boundaries.
/// </para>
/// <para>
/// Supports dictionary-based filters translated to Cosmos SQL query syntax.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed partial class CosmosDbProjectionStore<TProjection> : IProjectionStore<TProjection>, IAsyncDisposable, IDisposable
	where TProjection : class
{
	private readonly CosmosDbProjectionStoreOptions _options;
	private readonly ILogger<CosmosDbProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly JsonSerializerOptions _jsonOptions;
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbProjectionStore(
		IOptions<CosmosDbProjectionStoreOptions> options,
		ILogger<CosmosDbProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
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

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath);

				if (_options.DefaultTtlSeconds != 0)
				{
					containerProperties.DefaultTimeToLive = _options.DefaultTtlSeconds;
				}

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					_options.ContainerThroughput,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.ContainerName);
			}

			_initialized = true;
			LogInitialized(_options.ContainerName, _projectionType);
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(id);

		try
		{
			var response = await _container.ReadItemAsync<CosmosDbProjectionDocument>(
				documentId,
				new PartitionKey(_projectionType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return DeserializeData(response.Resource.Data);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = new CosmosDbProjectionDocument
		{
			Id = CreateDocumentId(id),
			ProjectionId = id,
			ProjectionType = _projectionType,
			Data = SerializeData(projection),
			UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
		};

		_ = await _container.UpsertItemAsync(
			document,
			new PartitionKey(_projectionType),
			new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite },
			cancellationToken).ConfigureAwait(false);

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(id);

		try
		{
			_ = await _container.DeleteItemAsync<CosmosDbProjectionDocument>(
				documentId,
				new PartitionKey(_projectionType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			LogDeleted(_projectionType, id);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Already deleted or never existed, nothing to do (idempotent delete)
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);
		var paginationClause = BuildPaginationClause(options);

		// Cosmos SQL query selecting data property
		var queryText = $"SELECT c.data FROM c WHERE c.projectionType = @projectionType{whereClause}{orderByClause}{paginationClause}";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@projectionType", _projectionType);

		// Add filter parameters
		foreach (var (paramName, paramValue) in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(paramName, paramValue);
		}

		// Add pagination parameters
		if (options?.Skip is not null)
		{
			queryDefinition = queryDefinition.WithParameter("@skip", options.Skip.Value);
		}

		if (options?.Take is not null)
		{
			queryDefinition = queryDefinition.WithParameter("@take", options.Take.Value);
		}

		var results = new List<TProjection>();
		using var iterator = _container.GetItemQueryIterator<QueryResult>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var item in response)
			{
				var projection = DeserializeData(item.Data);
				if (projection is not null)
				{
					results.Add(projection);
				}
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(filters);

		var queryText = $"SELECT VALUE COUNT(1) FROM c WHERE c.projectionType = @projectionType{whereClause}";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@projectionType", _projectionType);

		foreach (var (paramName, paramValue) in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(paramName, paramValue);
		}

		using var iterator = _container.GetItemQueryIterator<long>(queryDefinition);
		if (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			return response.FirstOrDefault();
		}

		return 0;
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

	private static (string WhereClause, List<(string Name, object Value)> Parameters) BuildWhereClause(
		IDictionary<string, object>? filters)
	{
		var parameters = new List<(string Name, object Value)>();

		if (filters is null || filters.Count == 0)
		{
			return (string.Empty, parameters);
		}

		var conditions = new List<string>();
		var paramIndex = 0;

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);
			var paramName = $"@p{paramIndex++}";

			// Cosmos DB uses direct property access with camelCase naming
			var propertyName = $"{char.ToLowerInvariant(parsed.PropertyName[0])}{parsed.PropertyName[1..]}";

			var condition = parsed.Operator switch
			{
				FilterOperator.Equals => $"c.data.{propertyName} = {paramName}",
				FilterOperator.NotEquals => $"c.data.{propertyName} != {paramName}",
				FilterOperator.GreaterThan => $"c.data.{propertyName} > {paramName}",
				FilterOperator.GreaterThanOrEqual => $"c.data.{propertyName} >= {paramName}",
				FilterOperator.LessThan => $"c.data.{propertyName} < {paramName}",
				FilterOperator.LessThanOrEqual => $"c.data.{propertyName} <= {paramName}",
				FilterOperator.Contains => BuildContainsCondition(propertyName, value, paramName, parameters),
				FilterOperator.In => BuildInCondition(propertyName, value, paramName, parameters, ref paramIndex),
				_ => $"c.data.{propertyName} = {paramName}"
			};

			// Add parameter for simple operators (not In or Contains which handle their own)
			if (parsed.Operator is not FilterOperator.In and not FilterOperator.Contains)
			{
				parameters.Add((paramName, value));
			}

			conditions.Add(condition);
		}

		return ($" AND {string.Join(" AND ", conditions)}", parameters);
	}

	private static string BuildContainsCondition(
		string propertyName,
		object value,
		string paramName,
		List<(string Name, object Value)> parameters)
	{
		// Cosmos DB CONTAINS function with case-insensitive search (third parameter = true)
		parameters.Add((paramName, value?.ToString() ?? string.Empty));
		return $"CONTAINS(c.data.{propertyName}, {paramName}, true)";
	}

	private static string BuildInCondition(
		string propertyName,
		object value,
		string paramName,
		List<(string Name, object Value)> parameters,
		ref int paramIndex)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			// Single value, treat as equals
			parameters.Add((paramName, value));
			return $"c.data.{propertyName} = {paramName}";
		}

		// Cosmos DB uses ARRAY_CONTAINS for IN operations
		var values = new List<object>();
		foreach (var item in enumerable)
		{
			values.Add(item);
		}

		if (values.Count == 0)
		{
			return "false"; // Empty IN clause, always false
		}

		// Use ARRAY_CONTAINS(@array, c.data.property)
		var arrayParamName = $"@p{paramIndex++}";
		parameters.Add((arrayParamName, values.ToArray()));

		return $"ARRAY_CONTAINS({arrayParamName}, c.data.{propertyName})";
	}

	private static string BuildOrderByClause(QueryOptions? options)
	{
		if (options?.OrderBy is null)
		{
			return " ORDER BY c.id"; // Default ordering for consistent pagination
		}

		var propertyName = $"{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";
		var direction = options.Descending ? "DESC" : "ASC";

		return $" ORDER BY c.data.{propertyName} {direction}";
	}

	private static string BuildPaginationClause(QueryOptions? options)
	{
		if (options?.Skip is null && options?.Take is null)
		{
			return string.Empty;
		}

		// Cosmos DB uses OFFSET N LIMIT N (must include both if either is specified)
		if (options?.Take is null)
		{
			return " OFFSET @skip LIMIT 2147483647"; // Max int for unlimited
		}

		if (options?.Skip is null)
		{
			return " OFFSET 0 LIMIT @take"; // No skip, start from beginning
		}

		return " OFFSET @skip LIMIT @take";
	}

	private static string CreateDocumentId(string projectionId)
	{
		// URL-safe encoding for document ID
		var bytes = System.Text.Encoding.UTF8.GetBytes(projectionId);
		return Convert.ToBase64String(bytes)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private JsonElement SerializeData(TProjection projection)
	{
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		using var document = JsonDocument.Parse(json);
		return document.RootElement.Clone();
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private TProjection? DeserializeData(JsonElement element)
	{
		if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
		{
			return null;
		}

		return JsonSerializer.Deserialize<TProjection>(element.GetRawText(), _jsonOptions);
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
			UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
		};

		if (_options.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.ConsistencyLevel.Value;
		}

		if (_options.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.PreferredRegions.ToList();
		}

		if (_options.HttpClientFactory != null)
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

	[LoggerMessage(DataCosmosDbEventId.ProjectionStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB projection store with container '{ContainerName}' for type '{ProjectionType}'")]
	private partial void LogInitialized(string containerName, string projectionType);

	[LoggerMessage(DataCosmosDbEventId.ProjectionUpserted, LogLevel.Debug, "Upserted projection {ProjectionType}/{Id}")]
	private partial void LogUpserted(string projectionType, string id);

	[LoggerMessage(DataCosmosDbEventId.ProjectionDeleted, LogLevel.Debug, "Deleted projection {ProjectionType}/{Id}")]
	private partial void LogDeleted(string projectionType, string id);

	/// <summary>
	/// Internal document structure for Cosmos DB storage.
	/// </summary>
	private sealed class CosmosDbProjectionDocument
	{
		[JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

		[JsonPropertyName("projectionId")] public string ProjectionId { get; set; } = string.Empty;

		[JsonPropertyName("projectionType")] public string ProjectionType { get; set; } = string.Empty;

		[JsonPropertyName("data")] public JsonElement Data { get; set; }

		[JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; } = string.Empty;
	}

	/// <summary>
	/// Result structure for queries selecting only the data field.
	/// </summary>
	private sealed class QueryResult
	{
		[JsonPropertyName("data")] public JsonElement Data { get; set; }
	}
}
