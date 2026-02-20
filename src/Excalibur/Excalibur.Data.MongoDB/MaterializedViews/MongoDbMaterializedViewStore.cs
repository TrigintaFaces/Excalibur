// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.MaterializedViews;

/// <summary>
/// MongoDB implementation of <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores materialized views as BSON documents in MongoDB with the following schema:
/// <list type="bullet">
/// <item><c>materialized_views</c> collection for view data</item>
/// <item><c>materialized_view_positions</c> collection for position tracking</item>
/// </list>
/// </para>
/// <para>
/// Uses ReplaceOneAsync with IsUpsert=true for thread-safe upsert operations.
/// </para>
/// </remarks>
public sealed partial class MongoDbMaterializedViewStore : IMaterializedViewStore, IAsyncDisposable
{
	private readonly MongoDbMaterializedViewStoreOptions _options;
	private readonly ILogger<MongoDbMaterializedViewStore> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbMaterializedViewDocument>? _viewsCollection;
	private IMongoCollection<MongoDbMaterializedViewPositionDocument>? _positionsCollection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbMaterializedViewStore(
		IOptions<MongoDbMaterializedViewStoreOptions> options,
		ILogger<MongoDbMaterializedViewStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbMaterializedViewStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbMaterializedViewStore(
		IMongoClient client,
		IOptions<MongoDbMaterializedViewStoreOptions> options,
		ILogger<MongoDbMaterializedViewStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_viewsCollection = _database.GetCollection<MongoDbMaterializedViewDocument>(_options.ViewsCollectionName);
		_positionsCollection = _database.GetCollection<MongoDbMaterializedViewPositionDocument>(_options.PositionsCollectionName);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("BSON deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("BSON deserialization might require runtime code generation.")]
	public async ValueTask<TView?> GetAsync<TView>(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
		where TView : class
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = MongoDbMaterializedViewDocument.CreateId(viewName, viewId);
		var filter = Builders<MongoDbMaterializedViewDocument>.Filter.Eq(d => d.Id, documentId);

		var document = await _viewsCollection
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document == null)
		{
			LogViewNotFound(viewName, viewId);
			return null;
		}

		LogViewLoaded(viewName, viewId);
		return BsonSerializer.Deserialize<TView>(document.Data);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("BSON serialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("BSON serialization might require runtime code generation.")]
	public async ValueTask SaveAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		CancellationToken cancellationToken)
		where TView : class
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		ArgumentNullException.ThrowIfNull(view);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var document = new MongoDbMaterializedViewDocument
		{
			Id = MongoDbMaterializedViewDocument.CreateId(viewName, viewId),
			ViewName = viewName,
			ViewId = viewId,
			Data = view.ToBsonDocument(),
			CreatedAt = now,
			UpdatedAt = now
		};

		var filter = Builders<MongoDbMaterializedViewDocument>.Filter.Eq(d => d.Id, document.Id);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		_ = await _viewsCollection.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
			.ConfigureAwait(false);

		LogViewSaved(viewName, viewId);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteAsync(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = MongoDbMaterializedViewDocument.CreateId(viewName, viewId);
		var filter = Builders<MongoDbMaterializedViewDocument>.Filter.Eq(d => d.Id, documentId);

		var result = await _viewsCollection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

		if (result.DeletedCount > 0)
		{
			LogViewDeleted(viewName, viewId);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<long?> GetPositionAsync(
		string viewName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbMaterializedViewPositionDocument>.Filter.Eq(d => d.Id, viewName);

		var document = await _positionsCollection
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document == null)
		{
			return null;
		}

		LogPositionLoaded(viewName, document.Position);
		return document.Position;
	}

	/// <inheritdoc/>
	public async ValueTask SavePositionAsync(
		string viewName,
		long position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var document = new MongoDbMaterializedViewPositionDocument
		{
			Id = viewName,
			ViewName = viewName,
			Position = position,
			CreatedAt = now,
			UpdatedAt = now
		};

		var filter = Builders<MongoDbMaterializedViewPositionDocument>.Filter.Eq(d => d.Id, viewName);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		_ = await _positionsCollection.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
			.ConfigureAwait(false);

		LogPositionSaved(viewName, position);
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
			_viewsCollection = _database.GetCollection<MongoDbMaterializedViewDocument>(_options.ViewsCollectionName);
			_positionsCollection = _database.GetCollection<MongoDbMaterializedViewPositionDocument>(_options.PositionsCollectionName);
		}

		// Create indexes for views collection
		var viewIndexBuilder = Builders<MongoDbMaterializedViewDocument>.IndexKeys;

		var viewNameIndex = new CreateIndexModel<MongoDbMaterializedViewDocument>(
			viewIndexBuilder.Ascending(d => d.ViewName),
			new CreateIndexOptions { Name = "ix_view_name" });

		var viewIdIndex = new CreateIndexModel<MongoDbMaterializedViewDocument>(
			viewIndexBuilder.Combine(
				viewIndexBuilder.Ascending(d => d.ViewName),
				viewIndexBuilder.Ascending(d => d.ViewId)),
			new CreateIndexOptions { Name = "ix_view_name_id" });

		_ = await _viewsCollection.Indexes.CreateManyAsync(
			[viewNameIndex, viewIdIndex],
			cancellationToken).ConfigureAwait(false);

		// Create indexes for positions collection
		var positionIndexBuilder = Builders<MongoDbMaterializedViewPositionDocument>.IndexKeys;

		var positionViewNameIndex = new CreateIndexModel<MongoDbMaterializedViewPositionDocument>(
			positionIndexBuilder.Ascending(d => d.ViewName),
			new CreateIndexOptions { Name = "ix_view_name" });

		_ = await _positionsCollection.Indexes.CreateManyAsync(
			[positionViewNameIndex],
			cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	#region Logging

	[LoggerMessage(
		EventId = DataMongoDbEventId.ProjectionUpserted,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} loaded")]
	private partial void LogViewLoaded(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataMongoDbEventId.DocumentNotFound,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} not found")]
	private partial void LogViewNotFound(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataMongoDbEventId.DocumentReplaced,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} saved")]
	private partial void LogViewSaved(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataMongoDbEventId.DocumentDeleted,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} deleted")]
	private partial void LogViewDeleted(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataMongoDbEventId.DocumentFound,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} loaded: {Position}")]
	private partial void LogPositionLoaded(string viewName, long position);

	[LoggerMessage(
		EventId = DataMongoDbEventId.DocumentUpdated,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} saved: {Position}")]
	private partial void LogPositionSaved(string viewName, long position);

	#endregion
}
