// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

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
	private readonly ITenantContext? _tenantContext;
	private readonly bool _ownsClient;
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
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot document identifier.
	/// </param>
	public MongoDbSnapshotStore(
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSnapshotStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot document identifier.
	/// </param>
	public MongoDbSnapshotStore(
		IMongoClient client,
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		_tenantContext = tenantContext;
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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext).TenantId);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId);

		try
		{
			var document = await _collection!
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

		var document = MongoDbSnapshotDocument.FromSnapshot(snapshot, TenantScope.FromContext(_tenantContext).TenantId);

		// Version guard in filter: only replace if current version is less than new version
		// This prevents older snapshots from overwriting newer ones
		var filter = Builders<MongoDbSnapshotDocument>.Filter.And(
			Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, document.Id),
			Builders<MongoDbSnapshotDocument>.Filter.Lt(d => d.Version, document.Version));

		var replaceOptions = new ReplaceOptions { IsUpsert = true };

		// A duplicate key here means A DOCUMENT EXISTS -- it does NOT mean a newer one does, and
		// treating those as the same thing silently discarded newer snapshots.
		//
		// The losing interleaving: when no snapshot exists yet, every concurrent writer evaluates the
		// version guard against nothing, so every one of them matches nothing and the upsert turns
		// into an INSERT of the same _id. Exactly one insert wins; the rest fail with 11000. If the
		// winner is an older version, the newer ones were thrown away -- the store then reports a
		// version lower than one it accepted, which is the opposite of the guarantee this guard
		// exists to provide.
		//
		// Retrying is what makes the guard meaningful. On the retry the document exists, so the
		// filter can finally do the comparison it was written for: if we are newer it replaces, and
		// if we are genuinely older it matches nothing, the insert collides again, and the conflict
		// is real. The loop bound only limits how many times we are willing to lose that race before
		// concluding the same thing.
		const int maxAttempts = 5;
		var attempt = 0;

		try
		{
			while (true)
			{
				attempt++;

				try
				{
					_ = await _collection!.ReplaceOneAsync(filter, document, replaceOptions, cancellationToken)
							.ConfigureAwait(false);

					LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					break;
				}
				catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000 && attempt < maxAttempts)
				{
					// Lost an insert race. Whether that means we are superseded is not yet known --
					// re-run the guard against the document that now exists and let it decide.
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
		{
			// Exhausted: a document exists and the guard did not match it on any attempt, so a
			// version at least as high as this one is already stored. This snapshot is genuinely
			// superseded and skipping it is correct.
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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext).TenantId);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId);

		try
		{
			_ = await _collection!.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, TenantScope.FromContext(_tenantContext).TenantId);
		var filter = Builders<MongoDbSnapshotDocument>.Filter.And(
			Builders<MongoDbSnapshotDocument>.Filter.Eq(d => d.Id, documentId),
			Builders<MongoDbSnapshotDocument>.Filter.Lt(d => d.Version, olderThanVersion));

		try
		{
			_ = await _collection!.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

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

		_ = await _collection!.Indexes.CreateManyAsync(
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
