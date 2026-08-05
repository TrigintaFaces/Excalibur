// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// MongoDB BSON serialization requires dynamic code; interface cannot be annotated per AOT checklist.
// UnconditionalSuppressMessage does not suppress IL2046/IL3051 at the compiler level.
#pragma warning disable IL2046, IL2026, IL3050, IL3051

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.EventSourcing;

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
/// View documents are written with an upserting replace. Checkpoint positions are never replaced: every
/// write path advances them with <c>$max</c>, so a delayed or retried write carrying an older position
/// cannot rewind the checkpoint and replay events the projection has already applied.
/// </para>
/// </remarks>
public sealed partial class MongoDbMaterializedViewStore : IAtomicMaterializedViewStore, IAsyncDisposable
{
	private readonly MongoDbMaterializedViewStoreOptions _options;
	private readonly ILogger<MongoDbMaterializedViewStore> _logger;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbMaterializedViewDocument>? _viewsCollection;
	private IMongoCollection<MongoDbMaterializedViewPositionDocument>? _positionsCollection;
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
		_ownsClient = true;
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

		var document = await _viewsCollection!
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

		_ = await _viewsCollection!.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
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

		var result = await _viewsCollection!.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

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

		var document = await _positionsCollection!
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

		// Monotonic position advance ($max never lowers a higher checkpoint), matching SaveViewAndPositionAsync.
		// A blind replace would let a delayed or retried write carrying an older position rewind the checkpoint,
		// replaying events the projection has already applied. $max is evaluated by the server, so concurrent
		// writers need no read-then-write coordination here.
		var filter = Builders<MongoDbMaterializedViewPositionDocument>.Filter.Eq(d => d.Id, viewName);
		var update = Builders<MongoDbMaterializedViewPositionDocument>.Update
			.Max(d => d.Position, position)
			.Set(d => d.ViewName, viewName)
			.Set(d => d.UpdatedAt, now)
			.SetOnInsert(d => d.CreatedAt, now);
		var updateOptions = new UpdateOptions { IsUpsert = true };

		_ = await _positionsCollection!.UpdateOneAsync(filter, update, updateOptions, cancellationToken)
			.ConfigureAwait(false);

		LogPositionSaved(viewName, position);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Reads the option that actually governs the behaviour rather than a proxy for it. MongoDB commits the
	/// view and the checkpoint together only inside a multi-document transaction, which requires
	/// <c>UseTransactions</c> and a replica set or sharded cluster. With it disabled the two writes are
	/// unsynchronised, so this store reports that it cannot currently deliver exactly-once.
	/// </remarks>
	public bool SupportsAtomicWrites => _options.UseTransactions;

	/// <inheritdoc/>
	/// <remarks>
	/// Crash-atomic: the view replace and the checkpoint advance run in one multi-document ACID transaction,
	/// which requires <c>UseTransactions</c> and a replica set or sharded cluster. When <c>UseTransactions</c>
	/// is disabled the two writes cannot be committed together and this method throws rather than degrade to
	/// at-least-once; <see cref="SupportsAtomicWrites"/> reports that state before it is reached. The position
	/// advance is monotonic (<c>$max</c>), so a delayed write never rewinds the checkpoint.
	/// </remarks>
	/// <exception cref="InvalidOperationException"><c>UseTransactions</c> is disabled.</exception>
	public async ValueTask SaveViewAndPositionAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		long position,
		CancellationToken cancellationToken)
		where TView : class
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		ArgumentNullException.ThrowIfNull(view);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var viewDocument = new MongoDbMaterializedViewDocument
		{
			Id = MongoDbMaterializedViewDocument.CreateId(viewName, viewId),
			ViewName = viewName,
			ViewId = viewId,
			Data = view.ToBsonDocument(),
			CreatedAt = now,
			UpdatedAt = now
		};
		var viewFilter = Builders<MongoDbMaterializedViewDocument>.Filter.Eq(d => d.Id, viewDocument.Id);
		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		// Monotonic position advance ($max never lowers a higher checkpoint).
		var positionFilter = Builders<MongoDbMaterializedViewPositionDocument>.Filter.Eq(d => d.Id, viewName);
		var positionUpdate = Builders<MongoDbMaterializedViewPositionDocument>.Update
			.Max(d => d.Position, position)
			.Set(d => d.ViewName, viewName)
			.Set(d => d.UpdatedAt, now)
			.SetOnInsert(d => d.CreatedAt, now);
		var updateOptions = new UpdateOptions { IsUpsert = true };

		// The view and the position live in two collections, so only a multi-document transaction commits
		// them together. Without one this method cannot keep the promise its interface makes, and quietly
		// issuing the two writes anyway would degrade an exactly-once projection to at-least-once at
		// runtime — invisibly, and only observable as a double-counted view after a crash. Refuse instead.
		// A caller reaching this throw was constructed past MaterializedViewProcessor's startup check.
		if (!_options.UseTransactions)
		{
			throw new InvalidOperationException(
				"MongoDbMaterializedViewStore cannot save a view and its checkpoint atomically because "
				+ "UseTransactions is disabled. Multi-document transactions require a replica set or sharded "
				+ "cluster; enable UseTransactions on MongoDbMaterializedViewStoreOptions, or back exactly-once "
				+ "projections with a store whose atomic writes are unconditional.");
		}

		using var session = await _client!.StartSessionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		_ = await session.WithTransactionAsync(
			async (s, ct) =>
			{
				_ = await _viewsCollection!.ReplaceOneAsync(s, viewFilter, viewDocument, replaceOptions, ct).ConfigureAwait(false);
				_ = await _positionsCollection!.UpdateOneAsync(s, positionFilter, positionUpdate, updateOptions, ct).ConfigureAwait(false);
				return true;
			},
			cancellationToken: cancellationToken).ConfigureAwait(false);

		LogViewSaved(viewName, viewId);
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
		_initLock.Dispose();

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
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

			_ = await _viewsCollection!.Indexes.CreateManyAsync(
				[viewNameIndex, viewIdIndex],
				cancellationToken).ConfigureAwait(false);

			// Create indexes for positions collection
			var positionIndexBuilder = Builders<MongoDbMaterializedViewPositionDocument>.IndexKeys;

			var positionViewNameIndex = new CreateIndexModel<MongoDbMaterializedViewPositionDocument>(
				positionIndexBuilder.Ascending(d => d.ViewName),
				new CreateIndexOptions { Name = "ix_view_name" });

			_ = await _positionsCollection!.Indexes.CreateManyAsync(
				[positionViewNameIndex],
				cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
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
