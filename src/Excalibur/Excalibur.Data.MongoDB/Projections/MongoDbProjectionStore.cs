// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
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
public sealed partial class MongoDbProjectionStore<TProjection> : IProjectionStore<TProjection>, IAsyncDisposable
	where TProjection : class
{
	private readonly MongoDbProjectionStoreOptions _options;
	private readonly ILogger<MongoDbProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbProjectionDocument>? _collection;
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
		_collection = _database.GetCollection<MongoDbProjectionDocument>(_options.CollectionName);
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
		var filter = Builders<MongoDbProjectionDocument>.Filter.Eq(d => d.Id, documentId);

		var document = await _collection
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return document?.ToProjection<TProjection>();
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

		var document = MongoDbProjectionDocument.FromProjection(id, _projectionType, projection);
		var filter = Builders<MongoDbProjectionDocument>.Filter.Eq(d => d.Id, document.Id);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		_ = await _collection.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
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
		var filter = Builders<MongoDbProjectionDocument>.Filter.Eq(d => d.Id, documentId);

		_ = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

		LogDeleted(_projectionType, id);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = BuildFilter(filters);
		var sort = BuildSort(options);

		var findFluent = _collection.Find(filter).Sort(sort);

		if (options?.Skip is not null)
		{
			findFluent = findFluent.Skip(options.Skip.Value);
		}

		if (options?.Take is not null)
		{
			findFluent = findFluent.Limit(options.Take.Value);
		}

		var documents = await findFluent.ToListAsync(cancellationToken).ConfigureAwait(false);

		var results = new List<TProjection>();
		foreach (var doc in documents)
		{
			var projection = doc.ToProjection<TProjection>();
			if (projection is not null)
			{
				results.Add(projection);
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

		var filter = BuildFilter(filters);

		return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		// MongoDB client doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	private static FilterDefinition<MongoDbProjectionDocument> BuildContainsFilter(
		FilterDefinitionBuilder<MongoDbProjectionDocument> builder,
		string fieldName,
		object value)
	{
		// Case-insensitive regex for contains
		var pattern = value?.ToString() ?? string.Empty;
		var regex = new BsonRegularExpression(pattern, "i");
		return builder.Regex(fieldName, regex);
	}

	private static FilterDefinition<MongoDbProjectionDocument> BuildInFilter(
		FilterDefinitionBuilder<MongoDbProjectionDocument> builder,
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

	private static SortDefinition<MongoDbProjectionDocument> BuildSort(QueryOptions? options)
	{
		var builder = Builders<MongoDbProjectionDocument>.Sort;

		if (options?.OrderBy is null)
		{
			// Default ordering by document id for consistent pagination
			return builder.Ascending(d => d.Id);
		}

		var fieldName = $"data.{char.ToLowerInvariant(options.OrderBy[0])}{options.OrderBy[1..]}";

		return options.Descending
			? builder.Descending(fieldName)
			: builder.Ascending(fieldName);
	}

	private FilterDefinition<MongoDbProjectionDocument> BuildFilter(IDictionary<string, object>? filters)
	{
		var builder = Builders<MongoDbProjectionDocument>.Filter;

		// Always filter by projection type
		var typeFilter = builder.Eq(d => d.ProjectionType, _projectionType);

		if (filters is null || filters.Count == 0)
		{
			return typeFilter;
		}

		var conditions = new List<FilterDefinition<MongoDbProjectionDocument>> { typeFilter };

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);
			// MongoDB uses camelCase field names within the data document
			var fieldName = $"data.{char.ToLowerInvariant(parsed.PropertyName[0])}{parsed.PropertyName[1..]}";

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
			_collection = _database.GetCollection<MongoDbProjectionDocument>(_options.CollectionName);
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
		var indexBuilder = Builders<MongoDbProjectionDocument>.IndexKeys;

		// Index on projectionType for filtering by type
		var typeIndex = new CreateIndexModel<MongoDbProjectionDocument>(
			indexBuilder.Ascending(d => d.ProjectionType),
			new CreateIndexOptions { Name = "ix_projectionType" });

		// Compound index on projectionType + projectionId for efficient lookups
		var compoundIndex = new CreateIndexModel<MongoDbProjectionDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(d => d.ProjectionType),
				indexBuilder.Ascending(d => d.ProjectionId)),
			new CreateIndexOptions { Name = "ix_type_id" });

		_ = await _collection.Indexes.CreateManyAsync(
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

	/// <summary>
	/// Internal document structure for MongoDB storage.
	/// </summary>
	internal sealed class MongoDbProjectionDocument
	{
		/// <summary>
		/// Gets or sets the unique document identifier (projectionType:projectionId).
		/// </summary>
		[BsonId]
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the original projection identifier.
		/// </summary>
		[BsonElement("projectionId")]
		public string ProjectionId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the projection type name for filtering.
		/// </summary>
		[BsonElement("projectionType")]
		public string ProjectionType { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the projection data as a BSON document.
		/// </summary>
		[BsonElement("data")]
		public BsonDocument Data { get; set; } = new();

		/// <summary>
		/// Gets or sets the last update timestamp.
		/// </summary>
		[BsonElement("updatedAt")]
		public DateTimeOffset UpdatedAt { get; set; }

		/// <summary>
		/// Creates a document from a projection instance.
		/// </summary>
		/// <typeparam name="T">The projection type.</typeparam>
		/// <param name="id">The projection identifier.</param>
		/// <param name="projectionType">The projection type name.</param>
		/// <param name="projection">The projection instance.</param>
		/// <returns>A new document instance.</returns>
		public static MongoDbProjectionDocument FromProjection<T>(string id, string projectionType, T projection)
			where T : class
		{
			return new MongoDbProjectionDocument
			{
				Id = $"{projectionType}:{id}",
				ProjectionId = id,
				ProjectionType = projectionType,
				Data = projection.ToBsonDocument(),
				UpdatedAt = DateTimeOffset.UtcNow
			};
		}

		/// <summary>
		/// Converts the document back to a projection instance.
		/// </summary>
		/// <typeparam name="T">The projection type.</typeparam>
		/// <returns>The deserialized projection, or null if data is empty.</returns>
		public T? ToProjection<T>()
			where T : class
		{
			if (Data is null || Data.ElementCount == 0)
			{
				return null;
			}

			return global::MongoDB.Bson.Serialization.BsonSerializer.Deserialize<T>(Data);
		}
	}
}
