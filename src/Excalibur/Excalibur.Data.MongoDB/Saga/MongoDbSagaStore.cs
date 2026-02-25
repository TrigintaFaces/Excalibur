// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Saga;

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
	private readonly IJsonSerializer _serializer;
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
		IJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
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
		IJsonSerializer serializer)
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

		var filter = Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaId);
		var document = await _collection
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (document is null || string.IsNullOrEmpty(document.StateJson))
		{
			return null;
		}

		var result = await _serializer
			.DeserializeAsync<TSagaState>(document.StateJson)
			.ConfigureAwait(false);

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

		var stateJson = _serializer.Serialize(sagaState);
		var now = DateTimeOffset.UtcNow;

		var filter = Builders<MongoDbSagaDocument>.Filter.Eq(d => d.SagaId, sagaState.SagaId);

		// Use UpdateOneAsync with SetOnInsert to preserve createdUtc on updates
		var update = Builders<MongoDbSagaDocument>.Update
			.Set(d => d.SagaType, typeof(TSagaState).Name)
			.Set(d => d.StateJson, stateJson)
			.Set(d => d.IsCompleted, sagaState.Completed)
			.Set(d => d.UpdatedUtc, now)
			.SetOnInsert(d => d.SagaId, sagaState.SagaId)
			.SetOnInsert(d => d.CreatedUtc, now);

		var options = new UpdateOptions { IsUpsert = true };

		_ = await _collection.UpdateOneAsync(filter, update, options, cancellationToken)
			.ConfigureAwait(false);

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

		_ = await _collection.Indexes.CreateManyAsync(
			[typeIndex, completedIndex],
			cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	[LoggerMessage(DataMongoDbEventId.SagaStateLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataMongoDbEventId.SagaStateSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);
}
