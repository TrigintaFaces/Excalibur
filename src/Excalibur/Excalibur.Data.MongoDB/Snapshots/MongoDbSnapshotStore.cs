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
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbSnapshotDocument>? _collection;
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
	/// Initializes a new instance of the <see cref="MongoDbSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The snapshot store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public MongoDbSnapshotStore(
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);
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
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public MongoDbSnapshotStore(
		IMongoClient client,
		IOptions<MongoDbSnapshotStoreOptions> options,
		ILogger<MongoDbSnapshotStore> logger,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, CurrentTenantScope.TenantId);
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

		var document = MongoDbSnapshotDocument.FromSnapshot(snapshot, CurrentTenantScope.TenantId);

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
		//
		// Two attempts are sufficient for the argument: the first can lose the insert race, and by
		// the second a document exists for the guard to compare against. The bound is higher because
		// the document can be removed between attempts -- retention trimming, an erasure -- which
		// returns a later attempt to the empty state the first one faced. Five bounds the pathology
		// without changing the reasoning; it is not a probability knob.
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
				catch (MongoWriteException ex) when (IsIdIndexDuplicate(ex) && attempt < maxAttempts)
				{
					// Lost an insert race. Whether that means we are superseded is not yet known --
					// re-run the guard against the document that now exists and let it decide.
					cancellationToken.ThrowIfCancellationRequested();
				}
			}
		}
		catch (MongoWriteException ex) when (IsIdIndexDuplicate(ex))
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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, CurrentTenantScope.TenantId);
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

		var documentId = MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, CurrentTenantScope.TenantId);
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

	/// <summary>
	/// Builds the client, database handle and collection handle once, however many callers arrive
	/// together.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The obvious consequence of racing here is the null dereference -- one caller assigns the
	/// client and is still assigning the collection when another sees a non-null client, skips the
	/// block and uses a collection that is still null. That window is only a few instructions wide,
	/// which is why it was so hard to reproduce. It is NOT the dominant consequence.
	/// </para>
	/// <para>
	/// The dominant one is that every caller that lost the race had already CONSTRUCTED a client,
	/// and only one of them was ever stored in the field. The others were unreachable the moment
	/// they were overwritten, and a client owns a connection pool and background server-monitoring
	/// threads. Disposal below can only release the client the field is holding, so each lost race
	/// leaked one pool for the lifetime of the process. Unlike the null dereference that happens on
	/// EVERY lost race, not on the narrow interleaving -- so the defect that was easy to miss was
	/// also the one that was certain to occur.
	/// </para>
	/// <para>
	/// Publication is safe without building into locals first. The three fields are written inside
	/// the lock and are followed by a volatile write of the flag, so a caller that takes the early
	/// return above has performed a volatile read of that same flag and cannot observe the fields as
	/// they were before it. The flag is the release, the early-return check is the acquire, and the
	/// pair is what makes the fields visible rather than merely assigned.
	/// </para>
	/// </remarks>
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
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <summary>
	/// Whether a duplicate-key error came from the <c>_id</c> index, which is the only one the
	/// version-guard retry reasons about.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The retry above treats a duplicate key as "a document exists, re-run the guard against it",
	/// and on exhaustion it concludes "this snapshot is superseded". Both statements are only true
	/// of the <c>_id</c> index. A duplicate on any OTHER unique index means something entirely
	/// different, and the loop would spin against it five times and then report a version conflict
	/// that never happened -- a wrong verdict, reported confidently, with the real violation lost.
	/// Nothing in this store creates a second unique index, so today the distinction is theoretical;
	/// it stops being theoretical the moment a consumer adds one to the collection.
	/// </para>
	/// <para>
	/// An unrecognised message is treated as an <c>_id</c> conflict, which is deliberately the
	/// pre-existing behaviour. The index name is parsed out of a server message, and a message
	/// format this code does not recognise must not turn a routine version conflict into a thrown
	/// exception on the common path. So the narrowing only ever fires when a DIFFERENT index is
	/// positively identified: unknown means behave as before, and only a name we can read and that
	/// is not <c>_id_</c> escalates.
	/// </para>
	/// </remarks>
	private static bool IsIdIndexDuplicate(MongoWriteException exception)
	{
		if (exception.WriteError?.Code != 11000)
		{
			return false;
		}

		var message = exception.WriteError.Message ?? string.Empty;
		var marker = message.IndexOf("index:", StringComparison.Ordinal);
		if (marker < 0)
		{
			return true;
		}

		var remainder = message.AsSpan(marker + "index:".Length).TrimStart();
		var space = remainder.IndexOf(' ');
		var indexName = space < 0 ? remainder : remainder[..space];

		return indexName.SequenceEqual("_id_");
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
