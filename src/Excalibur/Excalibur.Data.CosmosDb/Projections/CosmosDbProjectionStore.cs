// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Data.Validation;
using Excalibur.EventSourcing;

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
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification = "One store implements four contracts over an SDK whose document, query, request-option "
		+ "and error types are separate namespaces. Splitting the positioned write into its own type would "
		+ "duplicate the document layout, which is the one thing the conditional and unconditional writes "
		+ "must agree on exactly.")]
public sealed partial class CosmosDbProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TProjection>
	: IProjectionStore<TProjection>, IPageableProjectionStore<TProjection>, ICursorProjectionStore<TProjection>,
		IPositionedProjectionStore<TProjection>, IAsyncDisposable, IDisposable
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

	/// <summary>The last global-stream position folded into this projection.</summary>
	/// <remarks>
	/// Nested under the framework metadata object so it cannot collide with a consumer property.
	/// </remarks>
	private const string MetaFieldPosition = "lastAppliedPosition";

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
		_jsonOptions = _options.JsonSerializerOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbProjectionStore{TProjection}"/> class over a
	/// client the host owns.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="client">The Cosmos client registered by the host. Borrowed, never disposed here.</param>
	/// <remarks>
	/// Selected by dependency injection whenever a <see cref="CosmosClient"/> is registered, which the
	/// Cosmos registration does. Borrowing that client is what keeps a host enabling several Cosmos
	/// features on one connection pool rather than one per feature, and the store does not dispose it.
	/// </remarks>
	public CosmosDbProjectionStore(
		IOptions<CosmosDbProjectionStoreOptions> options,
		ILogger<CosmosDbProjectionStore<TProjection>> logger,
		CosmosClient client)
		: this(options, logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		_client = client;
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// THE FIELD IS WRITTEN, NOT OMITTED, AND THAT IS THE CHANGE.
		//
		// This write replaces the whole document, so a position the row used to carry disappears with
		// it. Omitting the field left the row reading back exactly like a row that never had a position
		// -- and those two must be treated OPPOSITELY: a never-positioned row IS a complete fold and is
		// adoptable, whereas a row whose state was just replaced by a value this store cannot relate to
		// the stream is not. Adopting the second stamps a position onto a state that does not contain
		// that prefix, and every event below it is then silently missing from the read model forever.
		//
		// Writing the unplaceable sentinel makes the row self-describing instead. It rides the same
		// single document write as the state, so there is no window in which a destroyed position is
		// recorded as a never-established one.
		using var payload = Serialize(BuildDocument(id, projection, ProjectionPosition.Unplaceable));

		using var response = await _container!.UpsertItemStreamAsync(
			payload,
			new PartitionKey(_projectionType),
			new ItemRequestOptions { EnableContentResponseOnWrite = false },
			cancellationToken).ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<CursorPagedResult<TProjection>> QueryCursorAsync(
		IDictionary<string, object>? filters,
		string? cursor,
		int pageSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var (whereClause, parameters) = BuildWhereClause(filters);

		// Cursor-based paging uses Cosmos DB's native continuation token instead of OFFSET/LIMIT,
		// which is O(offset) in RU cost on deep pages. A stable ORDER BY (default c.id) makes
		// page boundaries deterministic under concurrent writes.
		var orderByClause = BuildOrderByClause(null);
		var queryText = $"SELECT VALUE c FROM c WHERE c.{PartitionKeyField} = @projectionType{whereClause}{orderByClause}";

		var queryDefinition = new QueryDefinition(queryText)
			.WithParameter("@projectionType", _projectionType);
		foreach (var (paramName, paramValue) in parameters)
		{
			queryDefinition = queryDefinition.WithParameter(paramName, paramValue);
		}

		// The cursor carries both the Cosmos continuation token AND the total record count. The
		// total is computed ONCE (on the first page) and carried forward so continuation pages
		// never re-issue a COUNT query.
		var (continuation, carriedTotal) = DecodeCursor(cursor);

		// Fill exactly one page of MATCHED items. The iterator is recreated each round with the
		// prior round's continuation token and MaxItemCount set to only what is still needed, so a
		// round never over-reads past the page boundary (no skip/duplicate across pages) even when
		// StripAndDeserialize discards a malformed document.
		var results = new List<TProjection>(pageSize);
		do
		{
			var remaining = pageSize - results.Count;
			using var iterator = _container!.GetItemQueryIterator<JsonElement>(
				queryDefinition,
				continuation,
				new QueryRequestOptions { MaxItemCount = remaining });

			if (!iterator.HasMoreResults)
			{
				continuation = null;
				break;
			}

			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var item in response)
			{
				var projection = StripAndDeserialize(JsonNode.Parse(item.GetRawText()));
				if (projection is not null)
				{
					results.Add(projection);
				}
			}

			continuation = response.ContinuationToken;
		}
		while (results.Count < pageSize && !string.IsNullOrEmpty(continuation));

		// Compute the total once (first page) and carry it forward on continuation pages.
		var totalRecords = carriedTotal ?? await CountAsync(filters, cancellationToken).ConfigureAwait(false);

		// Surface a next cursor only when Cosmos signalled more results. When the query is exhausted
		// (continuation null/empty) the walk ends cleanly with no phantom next page.
		var nextCursor = string.IsNullOrEmpty(continuation)
			? null
			: CursorEncoder.Encode(continuation, totalRecords);

		return new CursorPagedResult<TProjection>(results, pageSize, totalRecords, nextCursor);
	}

	private static (string? Continuation, long? Total) DecodeCursor(string? cursor)
	{
		var values = CursorEncoder.Decode(cursor);
		if (values is not { Length: > 0 } || values[0] is not string continuation)
		{
			return (null, null);
		}

		var total = values.Length > 1 && values[1] is long carried ? carried : (long?)null;
		return (continuation, total);
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

			// Cosmos DB does not support parameterized identifiers, so the property name is
			// interpolated into the SQL. Allowlist-validate it (ASCII letters/digits/underscore)
			// to prevent SQL injection via crafted filter keys before interpolation.
			SqlIdentifierValidator.ThrowIfInvalid(parsed.PropertyName, nameof(filters));

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

	internal static string BuildOrderByClause(QueryOptions? options)
	{
		if (options?.OrderBy is null)
		{
			return " ORDER BY c.id"; // Default ordering for consistent pagination
		}

		// OrderBy is interpolated into Cosmos SQL (identifiers cannot be parameterized).
		// Allowlist-validate to prevent SQL injection via a crafted OrderBy value.
		SqlIdentifierValidator.ThrowIfInvalid(options.OrderBy, nameof(options));

		var propertyName = $"{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";
		var direction = options.Descending ? "DESC" : "ASC";

		// Always append the unique `c.id` tiebreaker so page boundaries are deterministic even when the
		// consumer-supplied OrderBy is non-unique (). Without it, documents with equal sort values have
		// undefined relative order across page requests and can be skipped or duplicated between adjacent pages.
		return string.Equals(propertyName, "id", StringComparison.Ordinal)
			? $" ORDER BY c.{propertyName} {direction}"
			: $" ORDER BY c.{propertyName} {direction}, c.id";
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
		// URL-safe (unpadded base64url) encoding for the document ID via the BCL primitive — byte-identical
		// to the former Convert.ToBase64String + '+'/'-' , '/'/'_' , TrimEnd('=') hand-roll, so existing
		// document IDs remain valid.
		var bytes = System.Text.Encoding.UTF8.GetBytes(projectionId);
		return System.Buffers.Text.Base64Url.EncodeToString(bytes);
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
			UseSystemTextJsonSerializerWithOptions = _options.JsonSerializerOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions()
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

	/// <inheritdoc />
	/// <remarks>
	/// Reads the document once and returns both halves. The ETag is deliberately NOT surfaced to the
	/// caller: it is an implementation detail of the write below, and putting a Cosmos concept into a
	/// provider-neutral contract would make the contract unimplementable elsewhere.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		var (projection, position, _) = await ReadWithEtagAsync(id, cancellationToken).ConfigureAwait(false);
		return (projection, position);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The same single document write as <see cref="UpsertAsync"/>, differing only in what it asserts:
	/// this state IS a complete fold, so a later positioned writer may adopt the row. Unconditional on
	/// purpose -- the caller is claiming completeness, not a place in the stream, so there is no
	/// position for a condition to be written against.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task UpsertUnnumberedAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var payload = Serialize(BuildDocument(id, projection, ProjectionPosition.Unnumbered));

		using var response = await _container!.UpsertItemStreamAsync(
			payload,
			new PartitionKey(_projectionType),
			new ItemRequestOptions { EnableContentResponseOnWrite = false },
			cancellationToken).ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b>Two round trips, and the signature cannot avoid it.</b> Cosmos conditions a write on an
	/// ETAG, which is not derivable from a position. So the store reads the document to learn both the
	/// ETag and the stored position, then writes with IfMatchEtag. The read makes the position
	/// comparison possible; the ETag makes the write atomic against a concurrent writer.
	/// </para>
	/// <para>
	/// <b>A spurious refusal is possible, and it is safe.</b> Any unrelated write moves the ETag, so a
	/// caller can be told Superseded while the position is unchanged. It re-reads and retries, which is
	/// correct: ignoring the ETag instead would accept a write that clobbered the other writer.
	/// </para>
	/// <para>
	/// The null branch is CreateItemAsync, whose 409 IS the refusal. Never an upsert, which would let a
	/// late starter reset a live projection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		TProjection projection,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = BuildDocument(id, projection, ProjectionPosition.At(newPosition));
		var partition = new PartitionKey(_projectionType);

		if (expectedPosition is null)
		{
			try
			{
				using var payload = Serialize(document);

				using var created = await _container!.CreateItemStreamAsync(payload, partition,
					new ItemRequestOptions { EnableContentResponseOnWrite = false }, cancellationToken)
					.ConfigureAwait(false);

				if (created.StatusCode == HttpStatusCode.Conflict)
				{
					throw new CosmosException("create conflicted", HttpStatusCode.Conflict, 0, string.Empty, 0);
				}

				created.EnsureSuccessStatusCode();

				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
			}
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
			{
				// Something is already there. It is either a projection a positioned writer is
				// advancing -- the late starter this branch exists to refuse -- or a row carrying no
				// position at all, written by the unconditional path (a rebuild, a recovery, a row
				// written before this store recorded positions).
				//
				// The second case must be ADOPTED, not refused. A caller reading an unpositioned row
				// has no position to claim, so refusing it would refuse every subsequent attempt
				// identically: a silent permanent stall rather than a conflict.
				var (_, conflicting, conflictEtag) = await ReadWithEtagAsync(id, cancellationToken)
					.ConfigureAwait(false);

				if (conflictEtag is null)
				{
					// Deleted between the create and this read. The next attempt creates it.
					return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, null);
				}

				// ADOPTION MATCHES EXACTLY ONE OF THE THREE STATES. An unnumbered row is a complete
				// fold whose coordinate is merely unknown, so folding this batch onto it and stamping
				// this batch's position states something true. The other two are refusals, and they
				// are refused DIFFERENTLY: a positioned row means a real writer is ahead (re-read and
				// retry), while an unplaceable row can never be adopted at all, so telling the caller
				// to retry would spin it forever against a row that will never change.
				return conflicting.Kind switch
				{
					ProjectionPositionKind.Positioned => new ProjectionAdvanceResult(
						ProjectionAdvanceOutcome.Superseded, conflicting.Value),
					ProjectionPositionKind.Unplaceable => new ProjectionAdvanceResult(
						ProjectionAdvanceOutcome.Unplaceable, null),
					_ => await ReplaceAtEtagAsync(id, document, conflictEtag, newPosition, cancellationToken)
						.ConfigureAwait(false),
				};
			}
		}

		var (_, storedPosition, etag) = await ReadWithEtagAsync(id, cancellationToken).ConfigureAwait(false);

		if (etag is null)
		{
			// Gone: deleted, which is how erasure removes personal data. Re-folding the event stream
			// would reinstate it, so this is settled rather than retried.
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
		}

		// An unplaceable row is terminal and is reported as such rather than as a supersede. A
		// superseded caller re-reads and retries; this row yields the same refusal on every re-read,
		// so reporting Superseded here is an unbounded redelivery loop against a projection that can
		// only be fixed by rebuilding it.
		if (storedPosition.Kind == ProjectionPositionKind.Unplaceable)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
		}

		// Both conjuncts, checked before the write: the stored position is the one the caller read, and
		// the new position is ahead of it. The second is not redundant, because the caller obtained its
		// expected value BY READING IT, so a redelivery satisfies the first by construction.
		//
		// ExpectedPositionOrNull renders an unnumbered row as null, which is right here: the caller
		// arrived with a non-null expectation, so an unnumbered row does not match it and the write is
		// refused. Adoption is the expectedPosition-is-null branch above, and only that branch.
		var held = storedPosition.ExpectedPositionOrNull;
		if (held != expectedPosition || newPosition <= held)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held);
		}

		return await ReplaceAtEtagAsync(id, document, etag, newPosition, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Reads the document with its ETag, checks the position matches, and replaces under that ETag.
	/// There is no create path: an absent document means the projection was DELETED, deletion is how
	/// erasure removes personal data, and recreating it would reinstate what was erased.
	/// </para>
	/// <para>
	/// The ETag makes the replace conditional on the document not having changed between the read and
	/// the write, so a concurrent writer cannot be overwritten even though the position is unchanged.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		TProjection projection,
		long atPosition,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(atPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var (_, storedPosition, etag) = await ReadWithEtagAsync(id, cancellationToken).ConfigureAwait(false);

		if (etag is null)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
		}

		if (storedPosition.Kind != ProjectionPositionKind.Positioned)
		{
			// The document carries no established position -- unnumbered or unplaceable, and neither
			// can be matched -- so there is nothing a re-fold can be placed against and re-reading
			// cannot change that.
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null);
		}

		var stored = storedPosition.Value;

		if (stored != atPosition)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, stored);
		}

		// Carries atPosition, so the position is written back unchanged.
		var document = BuildDocument(id, projection, storedPosition);

		var advanced = await ReplaceAtEtagAsync(id, document, etag, atPosition, cancellationToken)
			.ConfigureAwait(false);

		// The replace helper speaks the ADVANCE vocabulary. Translating here rather than reusing its
		// result type is the whole point of the separate type: an advance's Superseded is settled when
		// the store is ahead, and a re-fold's never is.
		return advanced.Outcome switch
		{
			ProjectionAdvanceOutcome.Applied => new(ProjectionRefoldOutcome.Applied, atPosition),
			ProjectionAdvanceOutcome.Vanished => new(ProjectionRefoldOutcome.Vanished, null),
			_ => new(ProjectionRefoldOutcome.Superseded, advanced.CurrentPosition),
		};
	}

	/// <summary>Replaces the document only if it still carries the ETag the caller read.</summary>
	/// <remarks>
	/// The ETag is what makes the write atomic against a concurrent writer. The position comparison the
	/// caller already performed is what refuses a re-delivery; neither substitutes for the other.
	/// </remarks>
	private async Task<ProjectionAdvanceResult> ReplaceAtEtagAsync(
		string id,
		Dictionary<string, object?> document,
		string etag,
		long newPosition,
		CancellationToken cancellationToken)
	{
		try
		{
			using var payload = Serialize(document);

			using var response = await _container!.ReplaceItemStreamAsync(payload, CreateDocumentId(id),
				new PartitionKey(_projectionType),
				new ItemRequestOptions { IfMatchEtag = etag, EnableContentResponseOnWrite = false },
				cancellationToken).ConfigureAwait(false);

			if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
			{
				throw new CosmosException("conditional replace refused", response.StatusCode, 0, string.Empty, 0);
			}

			response.EnsureSuccessStatusCode();

			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			var (_, current, _) = await ReadWithEtagAsync(id, cancellationToken).ConfigureAwait(false);

			// Same discrimination as the pre-write check, and it has to be repeated here because the
			// row can become unplaceable BETWEEN the caller's read and this replace -- an unconditional
			// upsert landing in that window is exactly what moves the ETag and causes this refusal.
			return current.Kind == ProjectionPositionKind.Unplaceable
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null)
				: new ProjectionAdvanceResult(
					ProjectionAdvanceOutcome.Superseded, current.ExpectedPositionOrNull);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
		}
	}

	/// <summary>Reads the projection, its stored position and its ETag in one request.</summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task<(TProjection? Projection, ProjectionPosition Position, string? ETag)> ReadWithEtagAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			using var response = await _container!.ReadItemStreamAsync(
				CreateDocumentId(id),
				new PartitionKey(_projectionType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return (null, ProjectionPosition.Unnumbered, null);
			}

			response.EnsureSuccessStatusCode();

			var node = await JsonNode.ParseAsync(response.Content, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			// ORDER IS LOAD-BEARING: read the position BEFORE deserializing.
			//
			// StripAndDeserialize MUTATES the node -- it removes the framework metadata object so the
			// remaining properties deserialize cleanly into the projection. C# evaluates tuple elements
			// left to right, so writing (StripAndDeserialize(node), ReadPosition(node), ...) strips the
			// metadata and THEN looks for the position inside it, which is always absent by then. The
			// position read back as null on every read, so every conditional write took the
			// claims-absence path and the condition it is supposed to enforce never engaged.
			var position = ReadPosition(node);

			return (StripAndDeserialize(node), position, response.Headers.ETag);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// A null ETag is what says "no document". The position returned alongside it is not a
			// reading of anything and no caller may act on it -- every branch tests the ETag first.
			return (null, ProjectionPosition.Unnumbered, null);
		}
	}

	/// <summary>
	/// Builds the stored document: projection properties at the root, framework metadata nested.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The projection state.</param>
	/// <param name="position">
	/// What this write asserts about the prefix folded into <paramref name="projection"/>. Always
	/// stored, in all three of its states -- see the remarks.
	/// </param>
	/// <remarks>
	/// One shape for both write paths, deliberately. Two builders would drift, and the drift would be
	/// a document the other path cannot read back.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private Dictionary<string, object?> BuildDocument(
		string id,
		TProjection projection,
		ProjectionPosition position)
	{
		// Projection properties live at the document root; framework metadata is isolated under a
		// nested object so it cannot collide with a consumer property.
		var projectionJson = JsonSerializer.SerializeToElement(projection, _jsonOptions);
		var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

		foreach (var prop in projectionJson.EnumerateObject())
		{
			merged[prop.Name] = prop.Value;
		}

		var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			[MetaFieldId] = id,
			[MetaFieldType] = _projectionType,
			[MetaFieldUpdatedAt] = DateTimeOffset.UtcNow.ToString("O"),
		};

		// Unconditionally, including for the two states that carry no number. An absent field is
		// indistinguishable from a field nobody wrote, so leaving it out is how "this state cannot be
		// placed" became "this state was never placed" -- the one distinction a positioned writer has
		// to act on. The encoding is ProjectionPosition's, never this provider's own sentinel.
		metadata[MetaFieldPosition] = position.ToStored();

		// Preserved so the projection's own 'id' survives the document id overwrite below.
		if (merged.TryGetValue("id", out var origId))
		{
			metadata[MetaFieldOrigId] = origId;
		}

		merged[MetadataKey] = metadata;

		// Engine requirements, not framework metadata: both must sit at the document root.
		merged["id"] = CreateDocumentId(id);
		merged[PartitionKeyField] = _projectionType;

		return merged;
	}

	/// <summary>
	/// Serializes a stored document to UTF-8 JSON with THIS store's options.
	/// </summary>
	/// <param name="document">The document to write.</param>
	/// <returns>A stream positioned at the start, for the container's stream API.</returns>
	/// <remarks>
	/// <para>
	/// <b>The client's serializer must not touch this document, and that is not a preference.</b> The
	/// document's values are <see cref="JsonElement"/> instances taken from the projection's own
	/// serialization. This SDK's default serializer is Newtonsoft, which does not know that type and
	/// writes it as its PUBLIC PROPERTIES — so a string property lands as
	/// <c>{"ValueKind":3}</c>, an object where a string belongs. The write succeeds. The read then
	/// fails, or worse, succeeds with nonsense.
	/// </para>
	/// <para>
	/// Serializing here with the same options the reader uses removes the mismatch entirely, and removes
	/// the dependence on which serializer a consumer happened to configure on the client they supplied.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private MemoryStream Serialize(Dictionary<string, object?> document)
	{
		var buffer = new MemoryStream();
		JsonSerializer.Serialize(buffer, document, _jsonOptions);
		buffer.Position = 0;
		return buffer;
	}

	/// <summary>Decodes the stored field into one of the three states.</summary>
	/// <remarks>
	/// An ABSENT field reads as <see cref="ProjectionPositionKind.Unnumbered"/>, which is what
	/// <see cref="ProjectionPosition.FromStored"/> does with a null. That is correct and deliberate: a
	/// document written before this field existed IS a complete fold, only its coordinate is unknown, so
	/// it stays adoptable. Every provider goes through FromStored so the eight of them cannot drift.
	/// </remarks>
	private static ProjectionPosition ReadPosition(JsonNode? node)
	{
		var value = node?[MetadataKey]?[MetaFieldPosition];
		return ProjectionPosition.FromStored(value?.GetValue<long>());
	}
}
