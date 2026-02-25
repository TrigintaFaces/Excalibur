// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

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
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public CosmosDbSnapshotStore(
		IOptions<CosmosDbSnapshotStoreOptions> options,
		ILogger<CosmosDbSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId);

		try
		{
			var response = await _container.ReadItemAsync<CosmosDbSnapshotDocument>(
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

		var document = CosmosDbSnapshotDocument.FromSnapshot(snapshot);
		var partitionKey = new PartitionKey(snapshot.AggregateType);

		try
		{
			// Try to read existing snapshot to check version
			var readResponse = await _container.ReadItemAsync<CosmosDbSnapshotDocument>(
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
			_ = await _container.ReplaceItemAsync(
				document,
				document.Id,
				partitionKey,
				new ItemRequestOptions
				{
					IfMatchEtag = readResponse.ETag,
					EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite
				},
				cancellationToken).ConfigureAwait(false);

			LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// No existing snapshot, create new
			try
			{
				_ = await _container.CreateItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions { EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite },
					cancellationToken).ConfigureAwait(false);

				LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
			catch (CosmosException createEx) when (createEx.StatusCode == HttpStatusCode.Conflict)
			{
				// Race condition: another process created the document between our read and create
				// Re-read to check version and potentially replace
				var conflictReadResponse = await _container.ReadItemAsync<CosmosDbSnapshotDocument>(
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
				_ = await _container.ReplaceItemAsync(
					document,
					document.Id,
					partitionKey,
					new ItemRequestOptions
					{
						IfMatchEtag = conflictReadResponse.ETag,
						EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite
					},
					cancellationToken).ConfigureAwait(false);

				LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
		{
			// ETag mismatch - another process modified the document
			// Re-read and check if newer version exists
			try
			{
				var rereadResponse = await _container.ReadItemAsync<CosmosDbSnapshotDocument>(
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
				_ = await _container.ReplaceItemAsync(
					document,
					document.Id,
					partitionKey,
					new ItemRequestOptions
					{
						IfMatchEtag = rereadResponse.ETag,
						EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId);

		try
		{
			_ = await _container.DeleteItemAsync<CosmosDbSnapshotDocument>(
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

		var documentId = CosmosDbSnapshotDocument.CreateId(aggregateId);

		try
		{
			// Read the snapshot first to check version
			var readResponse = await _container.ReadItemAsync<CosmosDbSnapshotDocument>(
				documentId,
				new PartitionKey(aggregateType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var existing = readResponse.Resource;

			// Only delete if version is less than olderThanVersion
			if (existing.Version < olderThanVersion)
			{
				_ = await _container.DeleteItemAsync<CosmosDbSnapshotDocument>(
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
		_client?.Dispose();
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
		_client?.Dispose();
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutInSeconds),
			ConnectionMode = _options.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			// Use System.Text.Json serializer to respect [JsonPropertyName] attributes
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
			}
		};

		if (_options.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.ConsistencyLevel.Value;
		}

		if (_options.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.PreferredRegions.ToList();
		}

		if (_options.HttpClientFactory != null)
		{
			options.HttpClientFactory = _options.HttpClientFactory;
		}

		return options;
	}

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			return new CosmosClient(_options.ConnectionString, options);
		}

		return new CosmosClient(_options.AccountEndpoint, _options.AccountKey, options);
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
}
