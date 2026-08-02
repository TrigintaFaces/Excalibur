// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

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
	/// Correctness does not depend on its value: each pass re-reads and re-applies the version guard, and
	/// snapshot versions only increase, so every pass either stores this snapshot or observes a newer one
	/// and skips. The loop therefore makes progress regardless of how many writers contend.
	///
	/// The bound exists only to stop an unbounded spin against a pathologically contended partition, and
	/// reaching it is a fault rather than an expected outcome — see the throw at the end of
	/// <see cref="ReplaceWithVersionGuardAsync"/>. Sizing it from a writer count would make a correctness
	/// claim the loop does not need and cannot honour, since the real writer count is unbounded.
	/// </remarks>
	private const int MaxConcurrentWriteAttempts = 16;

	private readonly ITenantContext? _tenantContext;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
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
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, the
	/// tenant becomes part of every snapshot document identifier.
	/// </param>
	public CosmosDbSnapshotStore(
		IOptions<CosmosDbSnapshotStoreOptions> options,
		ILogger<CosmosDbSnapshotStore> logger,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
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
			_client = CreateClient(clientOptions);

				// Created here, so this store owns it and disposes it.
				_ownsClient = true;

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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, TenantScope.FromContext(_tenantContext).TenantId);

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

		var document = CosmosDbSnapshotDocument.FromSnapshot(snapshot, TenantScope.FromContext(_tenantContext).TenantId);
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

				// Our version is newer, replace with ETag
				// Bounded retry rather than a bare replace. This call sits INSIDE a catch block, so a 412 thrown
				// here cannot be caught by the sibling catch clauses of the enclosing try — it escapes the
				// method as a raw Cosmos exception. That is the concurrent-writes path: every writer finds no
				// document, all race to create, the losers land here, and then race again on replace.
				await ReplaceWithVersionGuardAsync(document, partitionKey, snapshot, cancellationToken)
					.ConfigureAwait(false);
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			// ETag mismatch - another process modified the document
			// Re-read and check if newer version exists
			try
			{
				var rereadResponse = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
					document.Id,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				if (rereadResponse.Resource.Version >= snapshot.Version)
				{
					// A newer snapshot already exists, skip silently
					result = WriteStoreTelemetry.Results.Conflict;
					LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}

				// Retry once with new ETag
				_ = await _container!.ReplaceItemAsync(
					document,
					document.Id,
					partitionKey,
					new ItemRequestOptions
					{
						IfMatchEtag = rereadResponse.ETag,
						EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite
					},
					cancellationToken).ConfigureAwait(false);

				LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
			catch (CosmosException retryEx) when (retryEx.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				// Another concurrent modification - skip (a newer snapshot likely exists)
				result = WriteStoreTelemetry.Results.Conflict;
				LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, TenantScope.FromContext(_tenantContext).TenantId);

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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId, TenantScope.FromContext(_tenantContext).TenantId);

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
		_initLock.Dispose();
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
		_initLock.Dispose();

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
	/// Replaces the snapshot document under ETag concurrency, re-reading and re-applying the version guard
	/// when a competing writer wins the race.
	/// </summary>
	/// <remarks>
	/// Each pass either skips (a competitor stored a version at least as new as ours, so our write is
	/// redundant) or replaces against the current ETag. Snapshot versions only increase, so the loop makes
	/// progress; the attempt bound only stops a pathologically contended store from spinning.
	/// </remarks>
	private async Task ReplaceWithVersionGuardAsync(
		CosmosDbSnapshotDocument document,
		PartitionKey partitionKey,
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < MaxConcurrentWriteAttempts; attempt++)
		{
			var latest = await _container!.ReadItemAsync<CosmosDbSnapshotDocument>(
				document.Id,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (latest.Resource.Version >= snapshot.Version)
			{
				LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
				return;
			}

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
			catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				// Lost the race again; re-read and re-evaluate on the next pass.
			}
		}

		// Exhaustion is a FAULT, not a skip. Falling out of this loop silently would drop the snapshot and
		// tell no one: the caller would observe a successful SaveSnapshotAsync while nothing was written.
		// That is strictly worse than the raw 412 this loop replaced, because a thrown 412 at least reached
		// the caller. The version guard above is the ONLY legitimate way to leave without writing, and it
		// logs and returns explicitly. Anything reaching here has spun MaxConcurrentWriteAttempts times
		// without either winning the ETag race or being overtaken, which the loop's own invariant says
		// should not happen — so it is reported, never swallowed.
		var latestVersion = await ReadCurrentVersionOrDefaultAsync(document.Id, partitionKey, cancellationToken)
			.ConfigureAwait(false);

		throw new ConcurrencyException(
			nameof(CosmosDbSnapshotDocument),
			document.Id,
			snapshot.Version,
			latestVersion);
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
