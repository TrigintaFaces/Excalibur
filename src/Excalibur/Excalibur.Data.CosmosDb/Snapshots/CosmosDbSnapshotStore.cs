// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using System.Security.Cryptography;

using Excalibur.Data;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Snapshots;

/// <summary>
/// Cosmos DB implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic snapshot operations with upsert semantics using ETag-based optimistic concurrency.
/// Uses aggregateType as partition key for efficient queries within aggregate type boundaries.
/// Stores only the latest snapshot per aggregate (no snapshot history).
/// </para>
/// <para>
/// The read-check-upsert pattern with ETag ensures older snapshots don't overwrite newer ones,
/// maintaining consistency in concurrent scenarios.
/// </para>
/// </remarks>
public sealed partial class CosmosDbSnapshotStore : ISnapshotStore, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbSnapshotStoreOptions _options;
	private readonly ILogger<CosmosDbSnapshotStore> _logger;
	/// <summary>Spin guard on the ETag conflict loop: the point at which contention is treated as a fault.</summary>
	/// <remarks>
	/// This is NOT a writer budget and must not be tuned against an expected number of concurrent writers.
	/// Correctness does not depend on its value, and that half is load-bearing: the ONLY way a pass leaves
	/// without writing is the version guard, which acts on a version it has just read, so no value of this
	/// bound can turn an abandoned write into a silent success.
	///
	/// What correctness does NOT rest on is a claim this comment used to make — that every pass either
	/// stores this snapshot or observes a newer one and skips. That is a false dichotomy and it omits the
	/// outcome that matters: a pass can observe a version LOWER than this one, attempt the replace, and
	/// have it rejected, neither storing nor skipping. Reading the dichotomy as exhaustive is what makes
	/// the exhaustion fault below look unreachable. It is not. The loop cannot livelock, because every
	/// rejection is paid for by another writer's committed write, but it can starve, so the fault at the
	/// end of <see cref="UpsertWithVersionGuardAsync"/> is live code.
	///
	/// The bound exists only to stop an unbounded spin against a pathologically contended partition.
	/// Sizing it from a writer count would make a correctness claim the loop does not need and cannot
	/// honour, since the real writer count is unbounded; raising it buys nothing and lowering it only
	/// makes starvation less rare.
	/// </remarks>
	private const int MaxConcurrentWriteAttempts = 16;

	/// <summary>
	/// Upper bound on a single wait between contended write attempts, so that a large configured base
	/// delay cannot turn a contention pause into a multi-second stall.
	/// </summary>
	/// <remarks>
	/// A losing writer here has not been blocked by anything — its conditional write was rejected in one
	/// round trip — so the wait exists only to separate writers that were rejected at the same instant,
	/// not to outlast a lock. A quarter of a second is already far longer than the round trip it is
	/// spreading.
	/// </remarks>
	private static readonly TimeSpan MaxContendedWriteBackoff = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// Time source for the wait between contended write attempts. Injected so a test can drive that wait
	/// instead of sleeping on a wall clock: the contention path is the one this store most needs to be
	/// able to exercise, and it is untestable when its own timing cannot be controlled.
	/// </summary>
	private readonly TimeProvider _timeProvider;

	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private volatile bool _initialized;
	/// <summary>Whether this instance CREATED the client and may therefore dispose it.</summary>
	/// <remarks>
	/// A type disposes what it creates and never what it is handed. An injected client is owned by the
	/// composition root — in DI normally a singleton shared by the whole application — so disposing it here
	/// terminates Cosmos access for every other consumer the moment the first store is disposed. That is
	/// exactly what happened: one disposed store left every later operation throwing
	/// <c>ObjectDisposedException: Accessing CosmosClient after it is disposed</c>, an error naming this
	/// disposal rather than anything the caller did.
	/// </remarks>
	private bool _ownsClient;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="timeProvider">
	/// Time source for the wait between contended write attempts. Defaults to
	/// <see cref="System.TimeProvider.System"/> when not supplied.
	/// </param>
	public CosmosDbSnapshotStore(
		IOptions<CosmosDbSnapshotStoreOptions> options,
		ILogger<CosmosDbSnapshotStore> logger,
		ITenantContext tenantContext,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSnapshotStore"/> class over a client the host owns.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">The ambient tenant context.</param>
	/// <param name="client">The Cosmos client registered by the host. Borrowed, never disposed here.</param>
	/// <remarks>
	/// Selected by dependency injection whenever a <see cref="CosmosClient"/> is registered, which the
	/// Cosmos registration does. Borrowing that client is what keeps a host enabling several Cosmos
	/// features on one connection pool rather than one per feature, and the store does not dispose it.
	/// </remarks>
	/// <param name="timeProvider">
	/// Time source for the wait between contended write attempts. Defaults to
	/// <see cref="System.TimeProvider.System"/> when not supplied.
	/// </param>
	public CosmosDbSnapshotStore(
		IOptions<CosmosDbSnapshotStoreOptions> options,
		ILogger<CosmosDbSnapshotStore> logger,
		ITenantContext tenantContext,
		CosmosClient client,
		TimeProvider? timeProvider = null)
		: this(options, logger, tenantContext, timeProvider)
	{
		ArgumentNullException.ThrowIfNull(client);
		_client = client;
	}

	/// <summary>
	/// Initializes the Cosmos DB client and container reference.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			var clientOptions = CreateClientOptions();
			// Only when the host supplied none. A store that borrows the registered client shares its
			// connection pool with every other Cosmos feature instead of opening a second one.
			if (_client is null)
			{
				_client = CreateClient(clientOptions);
				_ownsClient = true;
			}

			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath);

				// Enable TTL on the container if configured
				if (_options.DefaultTtlSeconds != 0)
				{
					containerProperties.DefaultTimeToLive = _options.DefaultTtlSeconds;
				}

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					_options.ContainerThroughput,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.ContainerName);
			}

			_initialized = true;
			LogInitialized(_options.ContainerName);
		}
		finally
		{
			_ = _initLock.Release();
		}
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, CurrentTenantScope.TenantId);

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
				documentId,
				new PartitionKey(aggregateType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return response.Resource.ToSnapshot();
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			result = WriteStoreTelemetry.Results.NotFound;
			return null;
		}
		catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
		{
			result = WriteStoreTelemetry.Results.Failure;
			LogTransientError("load", aggregateType, aggregateId, ex);
			throw;
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
				WriteStoreTelemetry.Providers.CosmosDb,
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

		var document = CosmosDbSnapshotDocument.FromSnapshot(snapshot, CurrentTenantScope.TenantId);
		var partitionKey = new PartitionKey(snapshot.AggregateType);

		try
		{
			// Try to read existing snapshot to check version
			var readResponse = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
				document.Id,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var existing = readResponse.Resource;

			// Version guard: only replace if new version is higher
			if (existing.Version >= snapshot.Version)
			{
				result = WriteStoreTelemetry.Results.Conflict;
				LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
				return;
			}

			// Replace with ETag-based optimistic concurrency
			_ = await _container!.ReplaceItemAsync(
				document,
				document.Id,
				partitionKey,
				new ItemRequestOptions
				{
					IfMatchEtag = readResponse.ETag,
					EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite
				},
				cancellationToken).ConfigureAwait(false);

			LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// No existing snapshot, create new
			try
			{
				_ = await _container!.CreateItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
					cancellationToken).ConfigureAwait(false);

				LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
			catch (CosmosException createEx) when (createEx.StatusCode == HttpStatusCode.Conflict)
			{
				// Race condition: another process created the document between our read and create
				// Re-read to check version and potentially replace
				var conflictReadResponse = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
					document.Id,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				if (conflictReadResponse.Resource.Version >= snapshot.Version)
				{
					// A newer or equal snapshot already exists, skip silently
					result = WriteStoreTelemetry.Results.Conflict;
					LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}

				// Our version is newer, replace with ETag.
				//
				// This call sits INSIDE a catch block, so nothing it throws can be caught by the sibling
				// catch clauses of the enclosing try — including the transient 429/503 clause below, which
				// is therefore unreachable from here. The loop reports its own telemetry through
				// setResult for that reason; it cannot rely on an outer clause to do it.
				await UpsertWithVersionGuardAsync(
					document, partitionKey, snapshot, r => result = r, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			// Lost the ETag race to a concurrent writer. Hand the write to the same bounded guard loop the
			// create-race path above uses, rather than retrying once and giving up: the loop re-reads on
			// every pass and leaves without writing only on the measured fact that a version at least as
			// high is already stored. Retrying once and then returning abandoned the write on the
			// assumption that a newer snapshot existed, having never established it, and reported the
			// abandonment to the caller as a successful save.
			//
			// Same structural caveat as the sibling call: inside a catch block, so the loop owns its own
			// telemetry.
			await UpsertWithVersionGuardAsync(
				document, partitionKey, snapshot, r => result = r, cancellationToken).ConfigureAwait(false);
		}
		catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
		{
			result = WriteStoreTelemetry.Results.Failure;
			LogTransientError("save", snapshot.AggregateType, snapshot.AggregateId, ex);
			throw;
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
				WriteStoreTelemetry.Providers.CosmosDb,
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, CurrentTenantScope.TenantId);

		try
		{
			_ = await _container!.DeleteItemAsync<CosmosDbSnapshotDocument>(
				documentId,
				new PartitionKey(aggregateType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			LogSnapshotDeleted(aggregateType, aggregateId);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			result = WriteStoreTelemetry.Results.NotFound;
			// Already deleted or never existed, nothing to do
		}
		catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
		{
			result = WriteStoreTelemetry.Results.Failure;
			LogTransientError("delete", aggregateType, aggregateId, ex);
			throw;
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
				WriteStoreTelemetry.Providers.CosmosDb,
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, CurrentTenantScope.TenantId);

		try
		{
			// Read the snapshot first to check version
			var readResponse = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
				documentId,
				new PartitionKey(aggregateType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var existing = readResponse.Resource;

			// Only delete if version is less than olderThanVersion
			if (existing.Version < olderThanVersion)
			{
				_ = await _container!.DeleteItemAsync<CosmosDbSnapshotDocument>(
					documentId,
					new PartitionKey(aggregateType),
					new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
					cancellationToken).ConfigureAwait(false);

				LogSnapshotOlderDeleted(aggregateType, aggregateId, olderThanVersion);
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			result = WriteStoreTelemetry.Results.NotFound;
			// Already deleted or never existed, nothing to do
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			result = WriteStoreTelemetry.Results.Conflict;
			// ETag mismatch - the snapshot was modified, which means a newer version exists
			// In this case, we don't delete since a newer snapshot should be kept
		}
		catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
		{
			result = WriteStoreTelemetry.Results.Failure;
			LogTransientError("delete_older_than", aggregateType, aggregateId, ex);
			throw;
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
				WriteStoreTelemetry.Providers.CosmosDb,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		DisposeClientIfOwned();
		_initLock?.Dispose();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		DisposeClientIfOwned();
		_initLock?.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			// Use System.Text.Json serializer to respect [JsonPropertyName] attributes
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
			}
		};

		if (_options.Client.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.Client.ConsistencyLevel.Value;
		}

		if (_options.Client.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.Client.PreferredRegions.ToList();
		}

		if (_options.Client.HttpClientFactory != null)
		{
			options.HttpClientFactory = _options.Client.HttpClientFactory;
		}

		return options;
	}

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.Client.ConnectionString))
		{
			return new CosmosClient(_options.Client.ConnectionString, options);
		}

		return new CosmosClient(_options.Client.AccountEndpoint, _options.Client.AccountKey, options);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	[LoggerMessage(DataCosmosDbEventId.SnapshotStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB snapshot store with container '{ContainerName}'")]
	private partial void LogInitialized(string containerName);

	[LoggerMessage(DataCosmosDbEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataCosmosDbEventId.SnapshotVersionSkipped, LogLevel.Debug,
		"Skipped saving older snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotVersionSkipped(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataCosmosDbEventId.SnapshotDeleted, LogLevel.Debug, "Deleted snapshot for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotDeleted(string aggregateType, string aggregateId);

	[LoggerMessage(DataCosmosDbEventId.SnapshotOlderDeleted, LogLevel.Debug,
		"Deleted snapshot older than version {Version} for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotOlderDeleted(string aggregateType, string aggregateId, long version);

	[LoggerMessage(LogLevel.Warning,
		"Cosmos DB transient error during {Operation} for {AggregateType}/{AggregateId}")]
	private partial void LogTransientError(string operation, string aggregateType, string aggregateId, Exception ex);

	/// <summary>Disposes the Cosmos client only when this instance created it.</summary>
	private void DisposeClientIfOwned()
	{
		if (_ownsClient)
		{
			_client?.Dispose();
		}
	}

	/// <summary>
	/// Stores the snapshot under ETag concurrency, re-reading and re-applying the version guard on every
	/// pass, and creating the document when the key is empty.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A pass observes one of THREE states, not two. The document may be ABSENT, in which case the write is
	/// a create; it may hold a version at least as high as this one, in which case this write is redundant
	/// and the pass leaves on that measured fact; or it may hold an older version, in which case the pass
	/// replaces it conditional on the ETag it just read.
	/// </para>
	/// <para>
	/// The absent state is reachable mid-loop and not only on entry: a snapshot delete, an erasure, or
	/// container time-to-live can empty the key between attempts, returning a later pass to the state the
	/// first one faced. A loop that models only the two version-comparison outcomes has nowhere to put
	/// that, so the not-found escapes the save — and because this method is called from inside a catch
	/// clause, it escapes past the sibling clauses that would have recorded a fault, leaving a lost write
	/// recorded as a success.
	/// </para>
	/// <para>
	/// Telemetry is reported through <paramref name="setResult" /> rather than left to the caller's catch
	/// clauses, which are unreachable from here for the same reason.
	/// </para>
	/// </remarks>
	/// <param name="document">The document to store.</param>
	/// <param name="partitionKey">The partition the document belongs to.</param>
	/// <param name="snapshot">The snapshot being saved, which carries the version under guard.</param>
	/// <param name="setResult">Records the operation's telemetry outcome on the caller's behalf.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	private async Task UpsertWithVersionGuardAsync(
		CosmosDbSnapshotDocument document,
		PartitionKey partitionKey,
		ISnapshot snapshot,
		Action<string> setResult,
		CancellationToken cancellationToken)
	{
		try
		{
			for (var attempt = 0; attempt < MaxConcurrentWriteAttempts; attempt++)
			{
				ItemResponse<CosmosDbSnapshotDocument>? latest;

				try
				{
					latest = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
						document.Id,
						partitionKey,
						cancellationToken: cancellationToken).ConfigureAwait(false);
				}
				catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
				{
					latest = null;
				}

				if (latest is null)
				{
					try
					{
						_ = await _container!.CreateItemAsync(
							document,
							partitionKey,
							new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
							cancellationToken).ConfigureAwait(false);

						LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
						return;
					}
					catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
					{
						// Lost the create race. A document exists again, which says nothing yet about whose
						// version it holds — re-run the guard against it on the next pass and let it decide.
					}
				}
				else if (latest.Resource.Version >= snapshot.Version)
				{
					setResult(WriteStoreTelemetry.Results.Conflict);
					LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}
				else
				{
					try
					{
						_ = await _container!.ReplaceItemAsync(
							document,
							document.Id,
							partitionKey,
							new ItemRequestOptions
							{
								IfMatchEtag = latest.ETag,
								EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite
							},
							cancellationToken).ConfigureAwait(false);

						LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
						return;
					}
					catch (CosmosException ex)
						when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
					{
						// Either another writer committed between this pass's read and its replace, or the
						// document was removed in that window. Resource-not-found outranks the precondition,
						// so a vanished document answers 404 here rather than 412; matching only the
						// precondition let that one escape the loop. Both resolve the same way — re-read and
						// re-evaluate on the next pass, which now finds the key empty and creates it.
					}
				}

				await DelayBetweenAttemptsAsync(attempt + 1, cancellationToken).ConfigureAwait(false);
			}

			// Exhaustion is a FAULT, not a skip. Falling out of this loop silently would drop the snapshot
			// and tell no one: the caller would observe a successful SaveSnapshotAsync while nothing was
			// written. That is strictly worse than the raw 412 this loop replaced, because a thrown 412 at
			// least reached the caller. The version guard is the ONLY legitimate way to leave without
			// writing, and it logs and returns explicitly.
			//
			// AND REACHING HERE IS POSSIBLE. The loop cannot livelock: every rejected replace is paid for
			// by another writer's committed one, because a precondition failure establishes that the
			// stored version strictly increased between this pass's read and its write. But it can STARVE.
			// Exhausting the bound requires that many distinct commits, each strictly increasing, each
			// still strictly below this snapshot's version, and every one of them landing inside this
			// writer's read-to-replace window. Rare, and reachable — so this fault is live code, and the
			// exception clause documenting it on the contract is load-bearing rather than decorative.
			var latestVersion = await ReadCurrentVersionOrDefaultAsync(document.Id, partitionKey, cancellationToken)
				.ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(CosmosDbSnapshotDocument),
				document.Id,
				snapshot.Version,
				latestVersion);
		}
		catch (Exception)
		{
			// Both callers invoke this from inside a catch clause, so the sibling clauses of their try —
			// the transient 429/503 clause included — cannot catch anything thrown from here. Without this,
			// an exhausted or throttled contended save escapes while the operation is still recorded as a
			// successful one. Recording the fault is the half of that defect fixable without restructuring
			// the caller; LogTransientError remains in a clause this path cannot reach and does not fire.
			setResult(WriteStoreTelemetry.Results.Failure);
			throw;
		}
	}

	/// <summary>
	/// Waits before the next attempt, drawing the wait at random from an exponentially growing interval.
	/// </summary>
	/// <remarks>
	/// The randomisation is the load-bearing part rather than the growth. Contending writers are rejected
	/// at nearly the same instant, so a wait computed only from the attempt number is identical for all of
	/// them — they wake together and reproduce the collision they were waiting out. Drawing from a range
	/// spreads them apart, which is what lets the contention drain. Without any wait at all, a losing
	/// writer spends its whole attempt budget inside the window in which it is losing. The wait runs on the
	/// injected time source, so a test can drive it rather than sleep through it.
	/// </remarks>
	/// <param name="attempt">The one-based attempt that was just lost.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when the wait has elapsed.</returns>
	private Task DelayBetweenAttemptsAsync(int attempt, CancellationToken cancellationToken)
	{
		var baseDelay = (double)_options.ContendedWriteBackoffMilliseconds;

		// A configured base delay may legitimately exceed the cap; the ceiling can never be below the
		// first wait, so take whichever is larger rather than rejecting the value.
		var cap = Math.Max(MaxContendedWriteBackoff.TotalMilliseconds, baseDelay);
		var ceiling = (int)Math.Min(baseDelay * Math.Pow(2, attempt - 1), cap);

		// The framework's general-purpose generator is rejected by analysis wherever randomness is drawn,
		// so this path draws from the cryptographic one instead. It runs only after a write has already
		// been rejected, so the extra cost is charged to a round trip that has just been spent anyway.
		var wait = ceiling <= 0 ? 0 : RandomNumberGenerator.GetInt32(ceiling + 1);

		return Task.Delay(TimeSpan.FromMilliseconds(wait), _timeProvider, cancellationToken);
	}

	/// <summary>
	/// Reads the currently stored snapshot version for diagnostics on the exhaustion path, returning -1 when
	/// the document cannot be read. Never throws: it runs only while reporting another failure, and must not
	/// replace that failure with its own.
	/// </summary>
	private async Task<long> ReadCurrentVersionOrDefaultAsync(
		string documentId,
		PartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		try
		{
			var latest = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return latest.Resource.Version;
		}
		catch (CosmosException)
		{
			return -1;
		}
	}
}
