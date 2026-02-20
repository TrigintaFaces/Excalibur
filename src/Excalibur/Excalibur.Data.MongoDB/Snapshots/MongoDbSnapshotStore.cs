// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Snapshots;

/// <summary>
/// MongoDB implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic snapshot operations with upsert (insert-or-update) semantics.
/// Uses MongoDB's ReplaceOneAsync with IsUpsert=true for thread-safe concurrent snapshot saves.
/// Stores only the latest snapshot per aggregate (no snapshot history).
/// </para>
/// <para>
/// The upsert filter includes a version guard (Lt filter) that ensures older snapshots
/// don't overwrite newer ones, maintaining consistency in concurrent scenarios.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based configuration for most users</description></item>
/// <item><description>Advanced: Existing IMongoClient for shared client instances</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class MongoDbSnapshotStore : ISnapshotStore, IAsyncDisposable
{
	private readonly MongoDbSnapshotStoreOptions _options;
	private readonly ILogger<MongoDbSnapshotStore> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbSnapshotDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbSnapshotStore(
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSnapshotStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbSnapshotStore(
		IMongoClient client,
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<MongoDbSnapshotDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
			string aggregateId,
			string aggregateType,
			CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId);

		try
		{
			var document = await _collection
					.Find(filter)
					.FirstOrDefaultAsync(cancellationToken)
					.ConfigureAwait(false);

			if (document == null)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return null;
			}

			return document.ToSnapshot();
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
					WriteStoreTelemetry.Stores.SnapshotStore,
					WriteStoreTelemetry.Providers.MongoDb,
					"load",
					result,
					stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
			ISnapshot snapshot,
			CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(snapshot);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = MongoDbSnapshotDocument.FromSnapshot(snapshot);

		// Version guard in filter: only replace if current version is less than new version
		// This prevents older snapshots from overwriting newer ones
		var filter = Builders<MongoDbSnapshotDocument>.Filter.And(
			Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, document.Id),
			Builders<MongoDbSnapshotDocument>.Filter.Lt(d => d.Version, document.Version));

		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		try
		{
			_ = await _collection.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
					.ConfigureAwait(false);

			LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
		{
			// Duplicate key error - a newer snapshot already exists
			// This is expected behavior when concurrent saves occur with older versions
			result = WriteStoreTelemetry.Results.Conflict;
			LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
					WriteStoreTelemetry.Stores.SnapshotStore,
					WriteStoreTelemetry.Providers.MongoDb,
					"save",
					result,
					stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
			string aggregateId,
			string aggregateType,
			CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId);

		try
		{
			_ = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

			LogSnapshotDeleted(aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
					WriteStoreTelemetry.Stores.SnapshotStore,
					WriteStoreTelemetry.Providers.MongoDb,
					"delete",
					result,
					stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
			string aggregateId,
			string aggregateType,
			long olderThanVersion,
			CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.And(
			Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId),
			Builders<MongoDbSnapshotDocument>.Filter.Lt(d => d.Version, olderThanVersion));

		try
		{
			_ = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

			LogSnapshotOlderDeleted(aggregateType, aggregateId, olderThanVersion);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
					WriteStoreTelemetry.Stores.SnapshotStore,
					WriteStoreTelemetry.Providers.MongoDb,
					"delete_older_than",
					result,
					stopwatch.Elapsed);
		}
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
			_collection = _database.GetCollection<MongoDbSnapshotDocument>(_options.CollectionName);
		}

		// Create indexes
		var indexBuilder = Builders<MongoDbSnapshotDocument>.IndexKeys;

		// Index on aggregateId and aggregateType for queries
		var aggregateIndex = new CreateIndexModel<MongoDbSnapshotDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(d => d.AggregateId),
				indexBuilder.Ascending(d => d.AggregateType)),
			new CreateIndexOptions { Name = "ix_aggregate" });

		// Index on version for version-based queries
		var versionIndex = new CreateIndexModel<MongoDbSnapshotDocument>(
			indexBuilder.Ascending(d => d.Version),
			new CreateIndexOptions { Name = "ix_version" });

		_ = await _collection.Indexes.CreateManyAsync(
			[aggregateIndex, versionIndex],
			cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	[LoggerMessage(DataMongoDbEventId.SnapshotSaved, LogLevel.Debug, "Saved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataMongoDbEventId.SnapshotVersionSkipped, LogLevel.Debug, "Skipped saving older snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotVersionSkipped(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataMongoDbEventId.SnapshotDeleted, LogLevel.Debug, "Deleted snapshot for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotDeleted(string aggregateType, string aggregateId);

	[LoggerMessage(DataMongoDbEventId.SnapshotOlderDeleted, LogLevel.Debug, "Deleted snapshot older than version {Version} for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotOlderDeleted(string aggregateType, string aggregateId, long version);
}
