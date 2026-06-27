// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data;
using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Saga.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state using MongoDB document storage.
/// Uses UpdateOneAsync with SetOnInsert for atomic upserts that preserve the original
/// creation timestamp while updating other fields.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based configuration for most users</description></item>
/// <item><description>Advanced: Existing IMongoClient for shared client instances</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class MongoDbSagaStore : ISagaStore, IAsyncDisposable
{
	private readonly MongoDbSagaOptions _options;
	private readonly ILogger<MongoDbSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoCollection<MongoDbSagaDocument>? _collection;
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	public MongoDbSagaStore(
		IOptions<MongoDbSagaOptions> options,
		ILogger<MongoDbSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Shared client instances across multiple stores</description></item>
	/// <item><description>Custom connection pooling</description></item>
	/// <item><description>Integration with existing MongoDB infrastructure</description></item>
	/// </list>
	/// </remarks>
	public MongoDbSagaStore(
		IMongoClient client,
		IOptions<MongoDbSagaOptions> options,
		ILogger<MongoDbSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_collection = client.GetDatabase(_options.DatabaseName)
			.GetCollection<MongoDbSagaDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Type-isolation (1f5om2): scope the load to BOTH SagaId AND SagaType. The store persists+indexes
		// SagaType on save, so loading by SagaId alone would return a saga of a DIFFERENT type that shares the
		// Guid, then deserialize its StateJson into the wrong TSagaState (silent data corruption). A typed
		// LoadAsync<TSagaState>(id) must return null when no saga of that type exists at the id — the contract
		// already enforced structurally by InMemory (`state is TSagaState`), Cosmos, Firestore, and DynamoDb.
		var filter = Builders<MongoDbSagaDocument>.Filter.And(
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaId),
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaType, typeof(TSagaState).Name));
		var document = await _collection!
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document is null || string.IsNullOrEmpty(document.StateJson))
		{
			return null;
		}

		var result = _serializer.Deserialize<TSagaState>(document.StateJson);
		if (result is not null)
		{
			// The authoritative optimistic-concurrency version is the dedicated BSON field, NOT the version
			// embedded in StateJson — the blob is serialized BEFORE the store-owns-increment write-back, so it
			// carries the stale pre-save version (e.g. 0). Apply the persisted version so load-modify-save
			// gates against the real value instead of always comparing against the stale embedded one.
			result.Version = document.Version;
		}

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable IL2026, IL3050 // AOT: MongoDB saga store uses reflection-based JSON serialization
		var stateJson = _serializer.Serialize(sagaState);
#pragma warning restore IL2026, IL3050
		// UpdatedUtc/CreatedUtc are DateTime fields; use a DateTime value so the Update builder does not emit a
		// Convert(d.UpdatedUtc, DateTimeOffset) node — the MongoDB LINQ provider cannot translate that and
		// throws ExpressionNotSupportedException on every real save (the unit tests mock IMongoCollection and
		// never exercise the translation, so the integration conformance lock is what surfaced it).
		var now = DateTime.UtcNow;
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency (bd-e1tsq2), mirroring SqlServerSagaStore's version-gated MERGE: the update
		// only matches a document whose persisted version equals the loaded (expected) version, and advances
		// it by one. The {_id, version} filter + upsert is the canonical MongoDB pattern:
		//   - new saga (no document)      -> filter doesn't match -> upsert INSERTs a fresh document;
		//   - in-sync update              -> filter matches        -> version-gated update succeeds;
		//   - stale version (concurrent write) -> filter doesn't match -> upsert attempts an INSERT on the
		//     already-present _id -> E11000 duplicate key, which we surface as ConcurrencyException instead of
		//     silently overwriting the newer write (the previous blind upsert lost the update).
		var filter = Builders<MongoDbSagaDocument>.Filter.And(
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaState.SagaId),
			Builders<MongoDbSagaDocument>.Filter.Eq(d => d.Version, expectedVersion));

		var update = Builders<MongoDbSagaDocument>.Update
			.Set(d => d.SagaType, typeof(TSagaState).Name)
			.Set(d => d.StateJson, stateJson)
			.Set(d => d.IsCompleted, sagaState.Completed)
			.Set(d => d.UpdatedUtc, now)
			.Set(d => d.Version, expectedVersion + 1)
			.SetOnInsert(d => d.SagaId, sagaState.SagaId)
			.SetOnInsert(d => d.CreatedUtc, now);

		// No-resurrect guard (SqlServer reference contract): only a brand-new saga (expected version 0) may
		// be inserted. For a stale save (expected > 0) we do NOT upsert — a missing/version-moved document is
		// a deleted/completed saga and must throw rather than resurrect at a high version (zombie saga).
		var isInsert = expectedVersion == 0;
		var options = new UpdateOptions { IsUpsert = isInsert };

		try
		{
			var result = await _collection!.UpdateOneAsync(filter, update, options, cancellationToken)
				.ConfigureAwait(false);

			if (!isInsert && result.MatchedCount == 0)
			{
				// Update-only path matched nothing: the saga was deleted or its version moved on. Throw
				// instead of resurrecting (mirrors the MERGE's "@ExpectedVersion = 0"-guarded INSERT branch).
				var current = await _collection!
					.Find(Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaState.SagaId))
					.FirstOrDefaultAsync(cancellationToken)
					.ConfigureAwait(false);

				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					current?.Version ?? -1L);
			}
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Reachable only on the insert path (expected == 0): a document already exists at this _id but
			// not at version 0 → a concurrent create / stale insert. Surface as a concurrency conflict.
			var current = await _collection!
				.Find(Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaState.SagaId))
				.FirstOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		LogSagaSaved(typeof(TSagaState).Name, sagaState.SagaId, sagaState.Completed);
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
			_collection = _client.GetDatabase(_options.DatabaseName)
				.GetCollection<MongoDbSagaDocument>(_options.CollectionName);
		}

		// Create indexes for efficient queries
		var indexBuilder = Builders<MongoDbSagaDocument>.IndexKeys;

		// Index on sagaType for type-based queries
		var typeIndex = new CreateIndexModel<MongoDbSagaDocument>(
			indexBuilder.Ascending(d => d.SagaType),
			new CreateIndexOptions { Name = "ix_saga_type" });

		// Index on isCompleted for filtering active sagas
		var completedIndex = new CreateIndexModel<MongoDbSagaDocument>(
			indexBuilder.Ascending(d => d.IsCompleted),
			new CreateIndexOptions { Name = "ix_is_completed" });

		_ = await _collection!.Indexes.CreateManyAsync(
			[typeIndex, completedIndex],
			cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	[LoggerMessage(DataMongoDbEventId.SagaStateLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataMongoDbEventId.SagaStateSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);
}
