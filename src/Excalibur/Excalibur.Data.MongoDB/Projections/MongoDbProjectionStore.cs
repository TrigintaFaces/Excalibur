// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Text.RegularExpressions;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Projections;

/// <summary>
/// MongoDB implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides projection storage using native BSON documents with ReplaceOneAsync
/// and IsUpsert=true for atomic insert-or-update operations. Uses projectionType
/// for efficient queries within projection type boundaries.
/// </para>
/// <para>
/// Supports dictionary-based filters translated to MongoDB Filter.Builder syntax.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed partial class MongoDbProjectionStore<TProjection> : IProjectionStore<TProjection>, IPageableProjectionStore<TProjection>, IAsyncDisposable
	where TProjection : class
{
	/// <summary>
	/// Root-level key for the framework metadata object. All framework-managed fields
	/// (id, type, updatedAt, origId) are nested under this key to prevent collisions
	/// with consumer projection properties. CosmosDB and DynamoDB stores use the same key.
	/// </summary>
	private const string MetadataKey = "_projection";

	/// <summary>Metadata field: the original projection ID passed to UpsertAsync.</summary>
	private const string MetaFieldId = "id";

	/// <summary>Metadata field: the projection type discriminator for shared-collection filtering.</summary>
	private const string MetaFieldType = "type";

	/// <summary>Metadata field: UTC timestamp of the last upsert.</summary>
	private const string MetaFieldUpdatedAt = "updatedAt";

	/// <summary>
	/// Metadata field: preserves the projection's original <c>_id</c> value before it is
	/// overwritten with the compound document key. The MongoDB driver's default
	/// <see cref="MongoDB.Bson.Serialization.BsonClassMap"/> convention maps any property
	/// named <c>Id</c> to the BSON element <c>_id</c>. During write we replace <c>_id</c>
	/// with the compound <c>{projectionType}:{id}</c> key; this field stores the original
	/// so <see cref="StripProjectionMetadata"/> can restore it before deserialization.
	/// If a consumer registers a custom <see cref="MongoDB.Bson.Serialization.BsonClassMap"/>
	/// that suppresses or renames the Id mapping, <c>origId</c> will simply be absent and
	/// the compound key is removed instead — deserialization still succeeds because
	/// <see cref="MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention"/> is
	/// registered globally via <see cref="MongoDbConventionInitializer"/>.
	/// </summary>
	private const string MetaFieldOrigId = "origId";

	private readonly MongoDbProjectionStoreOptions _options;
	private readonly ILogger<MongoDbProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<BsonDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbProjectionStore(
		IOptions<MongoDbProjectionStoreOptions> options,
		ILogger<MongoDbProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_ownsClient = true;

		MongoDbConventionInitializer.EnsureRegistered();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbProjectionStore{TProjection}"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbProjectionStore(
		IMongoClient client,
		IOptions<MongoDbProjectionStoreOptions> options,
		ILogger<MongoDbProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<BsonDocument>(_options.CollectionName);

		MongoDbConventionInitializer.EnsureRegistered();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <inheritdoc cref="QueryAsync" path="/remarks"/>
	/// </remarks>
	public async Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(id);
		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);

		var document = await _collection!
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document is null)
		{
			return null;
		}

		StripProjectionMetadata(document);
		return BsonSerializer.Deserialize<TProjection>(document);
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

		// Serialize projection to flat BsonDocument — properties live at the document root.
		// Framework metadata is isolated under a nested '_projection' object to prevent
		// field name collisions with consumer projection properties.
		var document = projection.ToBsonDocument();
		var documentId = CreateDocumentId(id);

		// BsonClassMap convention maps the 'Id' property to '_id'. Preserve the original
		// value so it can be restored during deserialization (see StripProjectionMetadata).
		var metadata = new BsonDocument
		{
			[MetaFieldId] = id,
			[MetaFieldType] = _projectionType,
			[MetaFieldUpdatedAt] = BsonValue.Create(DateTimeOffset.UtcNow),
		};

		if (document.Contains("_id") && document["_id"] != BsonNull.Value)
		{
			metadata[MetaFieldOrigId] = document["_id"];
		}

		document["_id"] = documentId;
		document[MetadataKey] = metadata;

		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		_ = await _collection!.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
			.ConfigureAwait(false);

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
		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);

		_ = await _collection!.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

		LogDeleted(_projectionType, id);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Deserialization uses <see cref="MongoDB.Bson.Serialization.BsonSerializer"/> with
	/// <see cref="MongoDB.Bson.Serialization.BsonClassMap"/> conventions (not System.Text.Json).
	/// Projection types that rely on <c>[JsonPropertyName]</c> attributes for field mapping
	/// should also declare <c>[BsonElement]</c> attributes to ensure consistent round-trip
	/// serialization. Without matching BSON attributes, renamed fields may deserialize as
	/// <see langword="null"/> because <see cref="MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention"/>
	/// silently skips unrecognized elements rather than failing.
	/// </remarks>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = BuildFilter(filters);
		var sort = BuildSort(options);

		var findFluent = _collection!.Find(filter).Sort(sort);

		if (options?.Skip is not null)
		{
			findFluent = findFluent.Skip(options.Skip.Value);
		}

		if (options?.Take is not null)
		{
			findFluent = findFluent.Limit(options.Take.Value);
		}

		var documents = await findFluent.ToListAsync(cancellationToken).ConfigureAwait(false);

		var results = new List<TProjection>(documents.Count);
		foreach (var doc in documents)
		{
			StripProjectionMetadata(doc);
			results.Add(BsonSerializer.Deserialize<TProjection>(doc));
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

		var filter = BuildFilter(filters);

		return await _collection!.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
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

		var filter = BuildFilter(filters);
		var sort = BuildSort(options);
		var offset = (pageNumber - 1) * pageSize;

		// Execute data + count queries concurrently for single-roundtrip semantics
		var dataTask = _collection!.Find(filter)
			.Sort(sort)
			.Skip(offset)
			.Limit(pageSize)
			.ToListAsync(cancellationToken);

		var countTask = _collection!.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

		await Task.WhenAll(dataTask, countTask).ConfigureAwait(false);

		var documents = await dataTask.ConfigureAwait(false);
		var totalCount = await countTask.ConfigureAwait(false);

		var results = new List<TProjection>(documents.Count);
		foreach (var doc in documents)
		{
			StripProjectionMetadata(doc);
			results.Add(BsonSerializer.Deserialize<TProjection>(doc));
		}

		return new PagedResult<TProjection>(results, pageNumber, pageSize, totalCount);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
	}

	private static FilterDefinition<BsonDocument> BuildContainsFilter(
		FilterDefinitionBuilder<BsonDocument> builder,
		string fieldName,
		object value)
	{
		// Escape the search term to prevent regex injection — consumer-supplied values
		// must not be interpreted as regex metacharacters (e.g., ".*" or "(?=)").
		var literal = Regex.Escape(value?.ToString() ?? string.Empty);
		var regex = new BsonRegularExpression(literal, "i");
		return builder.Regex(fieldName, regex);
	}

	private static FilterDefinition<BsonDocument> BuildInFilter(
		FilterDefinitionBuilder<BsonDocument> builder,
		string fieldName,
		object value)
	{
		if (value is not IEnumerable enumerable || value is string)
		{
			// Single value, treat as equals
			return builder.Eq(fieldName, BsonValue.Create(value));
		}

		var values = new List<BsonValue>();
		foreach (var item in enumerable)
		{
			values.Add(BsonValue.Create(item));
		}

		if (values.Count == 0)
		{
			// Empty IN clause - return filter that matches nothing
			return builder.Eq("_nonexistent_field_", "impossible_value");
		}

		return builder.In(fieldName, values);
	}

	private static SortDefinition<BsonDocument> BuildSort(QueryOptions? options)
	{
		var builder = Builders<BsonDocument>.Sort;

		if (options?.OrderBy is null)
		{
			// Default ordering by document id for consistent pagination
			return builder.Ascending("_id");
		}

		// Projection fields are stored flat at the document root (camelCase)
		var fieldName = $"{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";

		return options.Descending
			? builder.Descending(fieldName)
			: builder.Ascending(fieldName);
	}

	private FilterDefinition<BsonDocument> BuildFilter(IDictionary<string, object>? filters)
	{
		var builder = Builders<BsonDocument>.Filter;

		// Always filter by projection type (shared collection) — metadata is nested under _projection
		var typeFilter = builder.Eq($"{MetadataKey}.{MetaFieldType}", _projectionType);

		if (filters is null || filters.Count == 0)
		{
			return typeFilter;
		}

		var conditions = new List<FilterDefinition<BsonDocument>> { typeFilter };

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);
			// Projection fields are stored flat at the document root (camelCase)
			var fieldName = $"{char.ToLowerInvariant(parsed.PropertyName[0])}{parsed.PropertyName[1..]}";

			var condition = parsed.Operator switch
			{
				FilterOperator.Equals => builder.Eq(fieldName, BsonValue.Create(value)),
				FilterOperator.NotEquals => builder.Ne(fieldName, BsonValue.Create(value)),
				FilterOperator.GreaterThan => builder.Gt(fieldName, BsonValue.Create(value)),
				FilterOperator.GreaterThanOrEqual => builder.Gte(fieldName, BsonValue.Create(value)),
				FilterOperator.LessThan => builder.Lt(fieldName, BsonValue.Create(value)),
				FilterOperator.LessThanOrEqual => builder.Lte(fieldName, BsonValue.Create(value)),
				FilterOperator.Contains => BuildContainsFilter(builder, fieldName, value),
				FilterOperator.In => BuildInFilter(builder, fieldName, value),
				_ => builder.Eq(fieldName, BsonValue.Create(value))
			};

			conditions.Add(condition);
		}

		return builder.And(conditions);
	}

	private string CreateDocumentId(string projectionId)
	{
		// Combine projection type and ID for unique document ID
		return $"{_projectionType}:{projectionId}";
	}

	/// <summary>
	/// Strips framework metadata from a <see cref="BsonDocument"/> before deserializing to
	/// <typeparamref name="TProjection"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The MongoDB driver's default <see cref="MongoDB.Bson.Serialization.BsonClassMap"/>
	/// convention maps any C# property named <c>Id</c> to the BSON element <c>_id</c>.
	/// During write (<see cref="UpsertAsync"/>) we overwrite <c>_id</c> with a compound
	/// document key (<c>{projectionType}:{id}</c>) and stash the original value in
	/// <c>_projection.origId</c>. This method reverses that transformation so
	/// <see cref="MongoDB.Bson.Serialization.BsonSerializer"/> maps it back to the
	/// projection's <c>Id</c> property correctly.
	/// </para>
	/// <para>
	/// If the projection type has no <c>Id</c> property (i.e., <c>origId</c> was never
	/// stored), the compound <c>_id</c> is removed entirely to prevent
	/// <see cref="MongoDB.Bson.Serialization.BsonSerializer"/> from injecting it into an
	/// unexpected member. Deserialization still succeeds because
	/// <see cref="MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention"/>
	/// is registered globally.
	/// </para>
	/// </remarks>
	private static void StripProjectionMetadata(BsonDocument document)
	{
		var meta = document.GetValue(MetadataKey, BsonNull.Value);

		if (meta is BsonDocument metaDoc && metaDoc.Contains(MetaFieldOrigId))
		{
			// Restore the projection's original _id (the Id property value before
			// we replaced _id with the compound document key during write).
			document["_id"] = metaDoc[MetaFieldOrigId];
		}
		else
		{
			// Projection type has no Id property (or uses a custom BsonClassMap that
			// suppresses the Id→_id mapping) — remove the compound key so
			// BsonSerializer doesn't inject it into an unexpected member.
			document.Remove("_id");
		}

		document.Remove(MetadataKey);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client == null)
		{
			var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
			settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
			settings.MaxConnectionPoolSize = _options.MaxPoolSize;

			if (_options.UseSsl)
			{
				settings.UseTls = true;
			}

			_client = new MongoClient(settings);
			_database = _client.GetDatabase(_options.DatabaseName);
			_collection = _database.GetCollection<BsonDocument>(_options.CollectionName);
		}

		if (_options.CreateIndexesOnInitialize)
		{
			await CreateIndexesAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
		LogInitialized(_options.CollectionName, _projectionType);
	}

	private async Task CreateIndexesAsync(CancellationToken cancellationToken)
	{
		var indexBuilder = Builders<BsonDocument>.IndexKeys;
		var typePath = $"{MetadataKey}.{MetaFieldType}";
		var idPath = $"{MetadataKey}.{MetaFieldId}";

		// Index on _projection.type for filtering by type (shared collection)
		var typeIndex = new CreateIndexModel<BsonDocument>(
			indexBuilder.Ascending(typePath),
			new CreateIndexOptions { Name = "ix_projection_type" });

		// Compound index on _projection.type + _projection.id for efficient lookups
		var compoundIndex = new CreateIndexModel<BsonDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(typePath),
				indexBuilder.Ascending(idPath)),
			new CreateIndexOptions { Name = "ix_projection_type_id" });

		_ = await _collection!.Indexes.CreateManyAsync(
			[typeIndex, compoundIndex],
			cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(DataMongoDbEventId.ProjectionStoreInitialized, LogLevel.Information,
		"Initialized MongoDB projection store with collection '{CollectionName}' for type '{ProjectionType}'")]
	private partial void LogInitialized(string collectionName, string projectionType);

	[LoggerMessage(DataMongoDbEventId.ProjectionUpserted, LogLevel.Debug, "Upserted projection {ProjectionType}/{Id}")]
	private partial void LogUpserted(string projectionType, string id);

	[LoggerMessage(DataMongoDbEventId.ProjectionDeleted, LogLevel.Debug, "Deleted projection {ProjectionType}/{Id}")]
	private partial void LogDeleted(string projectionType, string id);
}
