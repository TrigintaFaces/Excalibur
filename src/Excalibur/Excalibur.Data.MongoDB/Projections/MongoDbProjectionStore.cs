// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Text.RegularExpressions;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

using System.Diagnostics.CodeAnalysis;

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
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification = "One store implements four contracts over a driver whose document, filter, "
		+ "serialization and error-category types are separate namespaces. Splitting the positioned "
		+ "write into its own type would duplicate the document layout, which is the one thing the "
		+ "conditional and unconditional writes must agree on exactly.")]
public sealed partial class MongoDbProjectionStore<TProjection> : IProjectionStore<TProjection>, IPageableProjectionStore<TProjection>,
	IPositionedProjectionStore<TProjection>, IAsyncDisposable
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
	/// <c>BsonClassMap</c> convention maps any property
	/// named <c>Id</c> to the BSON element <c>_id</c>. During write we replace <c>_id</c>
	/// with the compound <c>{projectionType}:{id}</c> key; this field stores the original
	/// so <see cref="StripProjectionMetadata"/> can restore it before deserialization.
	/// If a consumer registers a custom <c>BsonClassMap</c>
	/// that suppresses or renames the Id mapping, <c>origId</c> will simply be absent and
	/// the compound key is removed instead — deserialization still succeeds because
	/// <c>IgnoreExtraElementsConvention</c> is
	/// registered globally via <see cref="MongoDbConventionInitializer"/>.
	/// </summary>
	private const string MetaFieldOrigId = "origId";

	/// <summary>
	/// The last global-stream position folded into this projection.
	/// </summary>
	/// <remarks>
	/// Under the framework metadata object rather than the document root, so it cannot collide with a
	/// consumer projection property of the same name.
	/// </remarks>
	private const string MetaFieldPosition = "lastAppliedPosition";

	private readonly MongoDbProjectionStoreOptions _options;
	private readonly ILogger<MongoDbProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<BsonDocument>? _collection;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;
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

		var documentId = CreateDocumentId(id);
		// THE FIELD IS WRITTEN, NOT OMITTED, AND THAT IS THE CHANGE.
		//
		// A ReplaceOne swaps the whole document, so a position the row used to carry vanishes with it.
		// Omitting the field left the row reading back exactly like a row that never had a position --
		// and those two must stay DISTINGUISHABLE: a never-positioned row IS a complete fold
		// whose coordinate is merely unknown, whereas a row whose state was just replaced holds a fold over
		// no known prefix at all. A positioned write refuses BOTH -- neither carries a number it can advance
		// from -- so what the distinction decides is not the next write but what the row can honestly be
		// said to hold while it waits to be rebuilt, which is what an operator reads it for.
		//
		// The sentinel rides the same single document replace as the state, so there is no window in
		// which a destroyed position is recorded as a never-established one.
		var document = BuildDocument(id, projection, ProjectionPosition.Unplaceable);

		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		_ = await _collection!.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
			.ConfigureAwait(false);

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc />
	/// <remarks>
	/// The same single <c>ReplaceOne</c> upsert as <see cref="UpsertAsync"/>, differing only in what it
	/// asserts: this state IS a complete fold, only its coordinate unknown.
	/// Unconditional on purpose -- the caller is claiming completeness, not a place in the stream, so
	/// there is no position for a condition to be written against.
	/// <para>
	/// <b>The row is still REFUSED by a positioned write</b>, which has no number to advance from. What
	/// this buys over the blind surface is that the row reads back as a complete answer awaiting a
	/// coordinate rather than as a state related to no prefix at all; a rebuild is what numbers it.
	/// </para>
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

		_ = await _collection!.ReplaceOneAsync(
			Builders<BsonDocument>.Filter.Eq("_id", CreateDocumentId(id)),
			BuildDocument(id, projection, ProjectionPosition.Unnumbered),
			new ReplaceOptions { IsUpsert = true },
			cancellationToken).ConfigureAwait(false);

		LogUpserted(_projectionType, id);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// The same single <c>ReplaceOne</c> as <see cref="UpsertUnnumberedAsync"/>, differing in two things:
	/// the position the document carries is a real number rather than the unnumbered sentinel, and
	/// <c>IsUpsert = false</c>. The filter matches on <c>_id</c> alone, so the write is unconditional on
	/// POSITION -- a state folded from an empty seed has no prior prefix for a condition to be written
	/// against -- while remaining conditional on the document EXISTING.
	/// </para>
	/// <para>
	/// <c>IsUpsert = false</c> is the load-bearing option. An absent document means the projection was
	/// DELETED, deletion is how erasure removes personal data, and a whole-stream replay is precisely the
	/// write that could reconstruct it. <c>MatchedCount</c> is what reports the absence, and it comes from
	/// the write itself rather than from a second read the document could be deleted between.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		TProjection projection,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var result = await _collection!.ReplaceOneAsync(
			Builders<BsonDocument>.Filter.Eq("_id", CreateDocumentId(id)),
			BuildDocument(id, projection, ProjectionPosition.At(newPosition)),
			new ReplaceOptions { IsUpsert = false },
			cancellationToken).ConfigureAwait(false);

		if (result.MatchedCount == 0)
		{
			return new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished);
		}

		LogUpserted(_projectionType, id);

		return new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied);
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
	/// Deserialization uses <c>BsonSerializer</c> with
	/// <c>BsonClassMap</c> conventions (not System.Text.Json).
	/// Projection types that rely on <c>[JsonPropertyName]</c> attributes for field mapping
	/// should also declare <c>[BsonElement]</c> attributes to ensure consistent round-trip
	/// serialization. Without matching BSON attributes, renamed fields may deserialize as
	/// <see langword="null"/> because <c>IgnoreExtraElementsConvention</c>
	/// silently skips unrecognized elements rather than failing.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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

		// Disposed AFTER _disposed is set, and the ordering is the whole point. _disposed is what
		// stops a caller reaching WaitAsync/Release, so destroying the semaphore first creates an
		// interval where the guard is gone but callers are still admitted. In that interval an
		// in-flight initialiser's Release() throws ObjectDisposedException from its finally --
		// replacing whatever the try produced, including the real diagnostic -- and any caller
		// already blocked in WaitAsync is never signalled at all.
		//
		// The earlier comment here claimed disposing first meant "a throw later still frees the
		// handle". That was backwards: it does not protect against a later throw, it maximises the
		// window in which the initialiser's Release is guaranteed to throw. try/finally is what
		// frees a handle on a throw.
		_initLock?.Dispose();

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
	/// The MongoDB driver's default <c>BsonClassMap</c>
	/// convention maps any C# property named <c>Id</c> to the BSON element <c>_id</c>.
	/// During write (<see cref="UpsertAsync"/>) we overwrite <c>_id</c> with a compound
	/// document key (<c>{projectionType}:{id}</c>) and stash the original value in
	/// <c>_projection.origId</c>. This method reverses that transformation so
	/// <c>BsonSerializer</c> maps it back to the
	/// projection's <c>Id</c> property correctly.
	/// </para>
	/// <para>
	/// If the projection type has no <c>Id</c> property (i.e., <c>origId</c> was never
	/// stored), the compound <c>_id</c> is removed entirely to prevent
	/// <c>BsonSerializer</c> from injecting it into an
	/// unexpected member. Deserialization still succeeds because
	/// <c>IgnoreExtraElementsConvention</c>
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


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check under the lock: the winner of the race above completed initialisation
			// while this caller was waiting, and repeating the work would be wrong as well as wasteful.
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
		}
		finally
		{
			_ = _initLock.Release();
		}
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

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// ONE read for both. Reading them separately admits a writer in between, after which the
		// position the caller holds certifies a prefix its state does not contain -- and the
		// conditional write below would ACCEPT, losing every event in the gap.
		var filter = Builders<BsonDocument>.Filter.Eq("_id", CreateDocumentId(id));
		var document = await _collection!.Find(filter).FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document is null)
		{
			// An absent document is not a fold over any prefix. Unnumbered would assert a complete fold
			// over state that does not exist.
			return (null, ProjectionPosition.Unplaceable);
		}

		var position = ReadPosition(document);
		StripProjectionMetadata(document);
		return (BsonSerializer.Deserialize<TProjection>(document), position);
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// One <c>ReplaceOne</c> against a filter carrying the condition, so the state and its position are
	/// written as a single document update — atomic on one document by construction, with no window in
	/// which the position is ahead of the state it describes.
	/// </para>
	/// <para>
	/// <b><c>IsUpsert</c> is false, deliberately.</b> An upsert would create the document when the
	/// filter fails to match, which is exactly the two cases that must NOT create it: a stale expected
	/// position, and a projection deleted by erasure. The null branch is a separate insert, so absence
	/// is the condition rather than a side effect.
	/// </para>
	/// <para>
	/// The filter carries BOTH conjuncts: the stored position equals what the caller read, AND the new
	/// position exceeds it. The second is not redundant — a caller reads its expected value, so on a
	/// redelivery the first is satisfied by construction and only the forward-only rule refuses it.
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
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		ArgumentNullException.ThrowIfNull(projection);

		// A NEGATIVE EXPECTATION IS THE ADOPT LICENCE THROUGH A DIFFERENT DOOR. The negatives are the
		// sentinel space for the two states that carry no number, so a caller naming one as "the position I
		// read" would be matching a row this store refuses by design. ExpectedPositionOrNull is the only
		// legal source for this argument and never yields a negative.
		if (expectedPosition is { } claimed)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(claimed, nameof(expectedPosition));
		}

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(id);
		var document = BuildDocument(id, projection, ProjectionPosition.At(newPosition));
		var positionField = $"{MetadataKey}.{MetaFieldPosition}";

		if (expectedPosition is { } expected)
		{
			var filter = Builders<BsonDocument>.Filter.And(
				Builders<BsonDocument>.Filter.Eq("_id", documentId),
				Builders<BsonDocument>.Filter.Eq(positionField, expected),
				Builders<BsonDocument>.Filter.Lt(positionField, newPosition));

			var result = await _collection!
				.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = false }, cancellationToken)
				.ConfigureAwait(false);

			if (result.MatchedCount > 0)
			{
				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
			}

			// Nothing matched. Distinguish "the row moved on" from "the row is gone": recreating a
			// deleted projection would reinstate data that erasure removed.
			var current = await ReadCurrentPositionAsync(documentId, cancellationToken).ConfigureAwait(false);

			if (!current.Exists)
			{
				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null);
			}

			// Terminal, not a supersede: there is no number to advance from, and re-reading yields the
			// same value and the same refusal, so Superseded here is an unbounded redelivery loop. That is the
			// arm a careless edit reintroduces, and a numberless row landing in it retries forever.
			//
			// BOTH no-number states report the SAME outcome, deliberately. This result describes what the WRITE
			// did; it carries no state, and the position was read at a different instant from the one the write
			// was refused at, so an outcome characterising the stored STATE would attribute a property of the
			// row-at-read-time to a write refused earlier. A caller that needs to know what the row holds reads
			// its position, where ProjectionPositionKind reports it as a measured fact.
			return current.Position.Kind == ProjectionPositionKind.Positioned
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, current.Position.Value)
				: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
		}

		// The caller read no position, so this is INSERT-IF-ABSENT and NOTHING ELSE:
		//
		//   absent                      -> inserted                  -> Applied
		//   present, a real position    -> duplicate _id, refused     -> Superseded
		//   present, no number          -> duplicate _id, refused     -> Unplaceable
		//
		// AN InsertOne CANNOT UPDATE, and that is the point rather than a convenience. The previous shape
		// was a filtered upsert whose filter MATCHED a document holding the unnumbered sentinel -- or no
		// position field at all -- and REPLACED it, stamping this batch's position onto a state whose
		// prefix nobody established. Every event below that position was then absent from the read model
		// while the position said it was present. With an insert there is no update arm to guard, so
		// adopting an existing document is inexpressible here instead of merely avoided.
		try
		{
			await _collection!.InsertOneAsync(document, options: null, cancellationToken)
				.ConfigureAwait(false);

			return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// An ordinary outcome, reported rather than thrown: the document is already there, so the
			// insert collided with its _id. Which refusal it is depends on what that document holds -- a
			// row carrying no number has nothing for a positioned write to advance from, so telling the
			// caller to retry would spin it forever.
			var current = await ReadCurrentPositionAsync(documentId, cancellationToken).ConfigureAwait(false);

			if (!current.Exists)
			{
				// Deleted between the insert and this read. The next attempt inserts it.
				return new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, null);
			}

			return current.Position.Kind == ProjectionPositionKind.Positioned
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, current.Position.Value)
				: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <c>IsUpsert = false</c>, and that is the load-bearing option. An absent document means the
	/// projection was DELETED, deletion is how erasure removes personal data, and an upsert here would
	/// reinstate what the erasure removed.
	/// </para>
	/// <para>
	/// The document is rebuilt carrying the SAME position it is matched on, so the write rewrites the
	/// state and leaves the position where it was.
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
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentOutOfRangeException.ThrowIfNegative(atPosition);
		ArgumentNullException.ThrowIfNull(projection);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(id);
		var positionField = $"{MetadataKey}.{MetaFieldPosition}";

		// Carries atPosition, so the position is written back unchanged rather than dropped.
		var document = BuildDocument(id, projection, ProjectionPosition.At(atPosition));

		var filter = Builders<BsonDocument>.Filter.And(
			Builders<BsonDocument>.Filter.Eq("_id", documentId),
			Builders<BsonDocument>.Filter.Eq(positionField, atPosition));

		var result = await _collection!
			.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = false }, cancellationToken)
			.ConfigureAwait(false);

		if (result.MatchedCount > 0)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition);
		}

		var current = await ReadCurrentPositionAsync(documentId, cancellationToken).ConfigureAwait(false);

		if (!current.Exists)
		{
			return new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null);
		}

		// A document with no established position -- unnumbered or unplaceable, and neither can be
		// matched -- carries nothing a re-fold can be placed against, and re-reading cannot change
		// that.
		return current.Position.Kind == ProjectionPositionKind.Positioned
			? new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, current.Position.Value)
			: new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null);
	}

	/// <summary>
	/// Builds the stored document, always stamping what it asserts about the prefix folded into it.
	/// </summary>
	/// <remarks>
	/// Shared by the unconditional and positioned writes so the two cannot drift into producing
	/// documents the other cannot read back.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private BsonDocument BuildDocument(string id, TProjection projection, ProjectionPosition position)
	{
		// Properties live at the document root; framework metadata is isolated under a nested object
		// to prevent collisions with consumer projection properties.
		var document = projection.ToBsonDocument();

		// The BsonClassMap convention maps an 'Id' property to '_id'. Preserve the original value so
		// StripProjectionMetadata can restore it on the way out.
		var metadata = new BsonDocument
		{
			[MetaFieldId] = id,
			[MetaFieldType] = _projectionType,
			// UtcDateTime, not DateTimeOffset. BSON has no offset-carrying date type, and the driver
			// does not guess: BsonValue.Create(DateTimeOffset) throws ArgumentException, so EVERY write
			// through this store threw before this line was corrected. Nothing is lost by converting --
			// the value is UtcNow, so its offset is zero by construction.
			[MetaFieldUpdatedAt] = BsonValue.Create(DateTimeOffset.UtcNow.UtcDateTime),
		};

		// Unconditionally, including for the two states that carry no number. An absent field is
		// indistinguishable from a field nobody wrote, so leaving it out is how "this state cannot be
		// placed" became "this state was never placed" -- the one distinction a positioned writer has
		// to act on. The encoding is the shared one, never this provider's own sentinel.
		metadata[MetaFieldPosition] = position.ToStored();

		if (document.Contains("_id") && document["_id"] != BsonNull.Value)
		{
			metadata[MetaFieldOrigId] = document["_id"];
		}

		document["_id"] = CreateDocumentId(id);
		document[MetadataKey] = metadata;

		return document;
	}

	private async Task<(bool Exists, ProjectionPosition Position)> ReadCurrentPositionAsync(
		BsonValue documentId,
		CancellationToken cancellationToken)
	{
		var document = await _collection!
			.Find(Builders<BsonDocument>.Filter.Eq("_id", documentId))
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		// The position returned alongside Exists=false is not a reading of anything; every caller tests
		// Exists first. It is UNPLACEABLE rather than Unnumbered even so: a don't-care value that would be
		// a false claim if anyone read it is worse than one that would be true, and the next reader will not
		// know it was meant to be ignored.
		return document is null
			? (false, ProjectionPosition.Unplaceable)
			: (true, ReadPosition(document));
	}

	/// <summary>Decodes the stored field into one of the three states.</summary>
	/// <remarks>
	/// <para>
	/// An ABSENT field, and a value that is not a stored number at all, BOTH read as
	/// <see cref="ProjectionPositionKind.Unplaceable"/> -- because both go through
	/// <see cref="ProjectionPosition.FromStored"/> with a null, and that is what it does with one. Neither
	/// is evidence of a fold: an absent field is absence of evidence, and a field holding a string is a row
	/// this provider has no reading of at all.
	/// </para>
	/// <para>
	/// <b>Every failure case routes through FromStored rather than constructing a value here</b>, which is
	/// what makes "the eight providers cannot drift" true rather than merely intended. This method used to
	/// return <c>Unnumbered</c> directly on both of those paths, so it was one of the two providers the
	/// sentence excluded while carrying it.
	/// </para>
	/// </remarks>
	private static ProjectionPosition ReadPosition(BsonDocument document)
	{
		if (!document.Contains(MetadataKey) || document[MetadataKey] is not BsonDocument meta
			|| !meta.Contains(MetaFieldPosition))
		{
			return ProjectionPosition.FromStored(null);
		}

		var value = meta[MetaFieldPosition];
		return ProjectionPosition.FromStored(
			value.IsInt64 || value.IsInt32 ? value.ToInt64() : null);
	}
}
