// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IL2026 // JSON serialization fallback path uses reflection when consumer does not provide source-gen JsonSerializerOptions
#pragma warning disable IL3050 // Generic JSON serialization may require dynamic code generation

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
public sealed partial class CosmosDbProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TProjection>
	: IProjectionStore<TProjection>, IPageableProjectionStore<TProjection>, IAsyncDisposable, IDisposable
	where TProjection : class
{
	/// <summary>
	/// Root-level key for the framework metadata object. All framework-managed fields
	/// (id, type, updatedAt, origId) are nested under this key to prevent collisions
	/// with consumer projection properties. MongoDB and DynamoDB stores use the same key.
	/// </summary>
	private const string MetadataKey = "_projection";

	/// <summary>Metadata field: the original projection ID passed to UpsertAsync.</summary>
	private const string MetaFieldId = "id";

	/// <summary>Metadata field: the projection type discriminator for shared-container filtering.</summary>
	private const string MetaFieldType = "type";

	/// <summary>Metadata field: UTC timestamp of the last upsert.</summary>
	private const string MetaFieldUpdatedAt = "updatedAt";

	/// <summary>
	/// Metadata field: preserves the projection's original 'id' value before it is
	/// overwritten with the Base64-encoded compound document key. Restored during
	/// deserialization by <see cref="StripAndDeserialize"/>.
	/// </summary>
	private const string MetaFieldOrigId = "origId";

	/// <summary>
	/// Cosmos DB root-level partition key field. This MUST remain at the document root
	/// because the container's partition key path is <c>/projectionType</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This field exposes the .NET type name (e.g., <c>OrderProjection</c>) as a
	/// queryable Cosmos DB attribute. This is an inherent consequence of the shared-container
	/// design with type-based partitioning — the partition key value must be readable by
	/// Cosmos DB and cannot be nested or encrypted. Consumers storing sensitive type names
	/// should use opaque projection class names or a dedicated container per projection type.
	/// </para>
	/// </remarks>
	private const string PartitionKeyField = "projectionType";

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
		_jsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
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
			// Use ReadItemStreamAsync to get the raw response stream, then parse directly
			// to JsonNode. This avoids the double-parse overhead of ReadItemAsync<JsonElement>
			// followed by GetRawText() + JsonNode.Parse() in StripAndDeserialize.
			using var response = await _container!.ReadItemStreamAsync(
				documentId,
				new PartitionKey(_projectionType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}

			response.EnsureSuccessStatusCode();

			var node = await JsonNode.ParseAsync(response.Content, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return StripAndDeserialize(node);
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

		// Serialize projection to JsonElement — projection properties live at the document root.
		// Framework metadata is isolated under a nested '_projection' object to prevent
		// field name collisions with consumer projection properties.
		var projectionJson = JsonSerializer.SerializeToElement(projection, _jsonOptions);
		var merged = new Dictionary<string, object?>();

		// Add all projection properties at root level first
		foreach (var prop in projectionJson.EnumerateObject())
		{
			merged[prop.Name] = prop.Value;
		}

		// Build framework metadata — preserve the projection's original 'id' if present
		// so it can be restored during deserialization (see StripAndDeserialize).
		var metadata = new Dictionary<string, object?>
		{
			[MetaFieldId] = id,
			[MetaFieldType] = _projectionType,
			[MetaFieldUpdatedAt] = DateTimeOffset.UtcNow.ToString("O"),
		};

		if (merged.TryGetValue("id", out var origId))
		{
			metadata[MetaFieldOrigId] = origId;
		}

		merged[MetadataKey] = metadata;

		// Cosmos DB requires 'id' as document identifier and 'projectionType' as partition key
		// at the document root — these are database engine requirements, not framework metadata.
		merged["id"] = CreateDocumentId(id);
		merged[PartitionKeyField] = _projectionType;

		_ = await _container!.UpsertItemAsync(
			merged,
			new PartitionKey(_projectionType),
			new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
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
			_ = await _container!.DeleteItemAsync<object>(
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

		// Cosmos SQL query returning full documents — metadata is stripped by StripAndDeserialize
		var queryText = $"SELECT VALUE c FROM c WHERE c.{PartitionKeyField} = @projectionType{whereClause}{orderByClause}{paginationClause}";

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
		using var iterator = _container!.GetItemQueryIterator<JsonElement>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var item in response)
			{
				// Convert JsonElement to JsonNode for StripAndDeserialize.
				// QueryAsync iterates multiple results so stream-based parsing is not
				// practical — the SDK returns JsonElement from the query iterator.
				var node = JsonNode.Parse(item.GetRawText());
				var projection = StripAndDeserialize(node);
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

		var queryText = $"SELECT VALUE COUNT(1) FROM c WHERE c.{PartitionKeyField} = @projectionType{whereClause}";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@projectionType", _projectionType);

		foreach (var (paramName, paramValue) in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(paramName, paramValue);
		}

		using var iterator = _container!.GetItemQueryIterator<long>(queryDefinition);
		if (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			return response.FirstOrDefault();
		}

		return 0;
	}

	/// <inheritdoc/>
	public async Task<PagedResult<TProjection>> QueryPagedAsync(
		IDictionary<string, object>? filters,
		int pageNumber,
		int pageSize,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(filters);
		var orderByClause = BuildOrderByClause(options);
		var offset = (pageNumber - 1) * pageSize;

		// Single-roundtrip: run data + count queries in parallel
		var dataQueryText = $"SELECT VALUE c FROM c WHERE c.{PartitionKeyField} = @projectionType{whereClause}{orderByClause} OFFSET @skip LIMIT @take";
		var countQueryText = $"SELECT VALUE COUNT(1) FROM c WHERE c.{PartitionKeyField} = @projectionType{whereClause}";

		var dataQueryDef = new QueryDefinition(dataQueryText)
			.WithParameter("@projectionType", _projectionType)
			.WithParameter("@skip", offset)
			.WithParameter("@take", pageSize);

		var countQueryDef = new QueryDefinition(countQueryText)
			.WithParameter("@projectionType", _projectionType);

		foreach (var (paramName, paramValue) in parameters)
		{
			dataQueryDef = dataQueryDef.WithParameter(paramName, paramValue);
			countQueryDef = countQueryDef.WithParameter(paramName, paramValue);
		}

		// Execute both queries concurrently for single-roundtrip semantics
		var dataTask = ExecuteQueryAsync(dataQueryDef, cancellationToken);
		var countTask = ExecuteCountQueryAsync(countQueryDef, cancellationToken);

		await Task.WhenAll(dataTask, countTask).ConfigureAwait(false);

		var items = await dataTask.ConfigureAwait(false);
		var totalCount = await countTask.ConfigureAwait(false);

		return new PagedResult<TProjection>(items, pageNumber, pageSize, totalCount);
	}

	private async Task<List<TProjection>> ExecuteQueryAsync(
		QueryDefinition queryDefinition,
		CancellationToken cancellationToken)
	{
		var results = new List<TProjection>();
		using var iterator = _container!.GetItemQueryIterator<JsonElement>(queryDefinition);

		while (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var item in response)
			{
				var node = JsonNode.Parse(item.GetRawText());
				var projection = StripAndDeserialize(node);
				if (projection is not null)
				{
					results.Add(projection);
				}
			}
		}

		return results;
	}

	private async Task<long> ExecuteCountQueryAsync(
		QueryDefinition queryDefinition,
		CancellationToken cancellationToken)
	{
		using var iterator = _container!.GetItemQueryIterator<long>(queryDefinition);
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
				FilterOperator.Equals => $"c.{propertyName} = {paramName}",
				FilterOperator.NotEquals => $"c.{propertyName} != {paramName}",
				FilterOperator.GreaterThan => $"c.{propertyName} > {paramName}",
				FilterOperator.GreaterThanOrEqual => $"c.{propertyName} >= {paramName}",
				FilterOperator.LessThan => $"c.{propertyName} < {paramName}",
				FilterOperator.LessThanOrEqual => $"c.{propertyName} <= {paramName}",
				FilterOperator.Contains => BuildContainsCondition(propertyName, value, paramName, parameters),
				FilterOperator.In => BuildInCondition(propertyName, value, paramName, parameters, ref paramIndex),
				_ => $"c.{propertyName} = {paramName}"
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
		return $"CONTAINS(c.{propertyName}, {paramName}, true)";
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
			return $"c.{propertyName} = {paramName}";
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

		// Use ARRAY_CONTAINS(@array, c.property)
		var arrayParamName = $"@p{paramIndex++}";
		parameters.Add((arrayParamName, values.ToArray()));

		return $"ARRAY_CONTAINS({arrayParamName}, c.{propertyName})";
	}

	private static string BuildOrderByClause(QueryOptions? options)
	{
		if (options?.OrderBy is null)
		{
			return " ORDER BY c.id"; // Default ordering for consistent pagination
		}

		var propertyName = $"{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";
		var direction = options.Descending ? "DESC" : "ASC";

		return $" ORDER BY c.{propertyName} {direction}";
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

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
		};

		if (_options.Client.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.Client.ConsistencyLevel.Value;
		}

		if (_options.Client.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.Client.PreferredRegions.ToList();
		}

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

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Strips framework metadata and Cosmos DB system fields from a parsed JSON node,
	/// restores the projection's original 'id' if preserved, and deserializes to TProjection.
	/// </summary>
	/// <param name="node">
	/// A <see cref="JsonNode"/> representing the raw Cosmos DB document. Accepts <see langword="null"/>
	/// (returns <see langword="default"/>). Called from <see cref="GetByIdAsync"/> with a stream-parsed
	/// node and from <see cref="QueryAsync"/> with a <see cref="JsonElement"/>-converted node.
	/// </param>
	private TProjection? StripAndDeserialize(JsonNode? node)
	{
		if (node is not JsonObject obj)
		{
			return default;
		}

		// Restore projection's original 'id' from metadata if it was preserved during write
		if (obj.TryGetPropertyValue(MetadataKey, out var metaNode) && metaNode is JsonObject meta)
		{
			if (meta.TryGetPropertyValue(MetaFieldOrigId, out var origId) && origId is not null)
			{
				obj["id"] = origId.DeepClone();
			}
			else
			{
				obj.Remove("id");
			}
		}
		else
		{
			obj.Remove("id");
		}

		// Remove framework metadata object
		obj.Remove(MetadataKey);

		// Remove Cosmos DB partition key (stored at root for the database engine)
		obj.Remove(PartitionKeyField);

		// Remove Cosmos DB system fields
		obj.Remove("_rid");
		obj.Remove("_self");
		obj.Remove("_etag");
		obj.Remove("_attachments");
		obj.Remove("_ts");

		return obj.Deserialize<TProjection>(_jsonOptions);
	}

	[LoggerMessage(DataCosmosDbEventId.ProjectionStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB projection store with container '{ContainerName}' for type '{ProjectionType}'")]
	private partial void LogInitialized(string containerName, string projectionType);

	[LoggerMessage(DataCosmosDbEventId.ProjectionUpserted, LogLevel.Debug, "Upserted projection {ProjectionType}/{Id}")]
	private partial void LogUpserted(string projectionType, string id);

	[LoggerMessage(DataCosmosDbEventId.ProjectionDeleted, LogLevel.Debug, "Deleted projection {ProjectionType}/{Id}")]
	private partial void LogDeleted(string projectionType, string id);
}
