// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL2046, IL3050, IL3051 // AOT: Cloud-native provider uses reflection-based serialization

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Excalibur.Data;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Saga.CosmosDb;

/// <summary>
/// Cosmos DB implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state using Cosmos DB document storage.
/// Uses read-then-upsert pattern to preserve the original creation timestamp
/// while updating other fields on subsequent saves.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based configuration for most users</description></item>
/// <item><description>Advanced: Existing CosmosClient for shared client instances</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class CosmosDbSagaStore : ISagaStore, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbSagaOptions _options;
	private readonly ILogger<CosmosDbSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;

	private readonly ITenantContext? _tenantContext;
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
	/// Initializes a new instance of the <see cref="CosmosDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public CosmosDbSagaStore(
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing Cosmos DB client.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Shared client instances across multiple stores</description></item>
	/// <item><description>Custom connection configuration</description></item>
	/// <item><description>Integration with existing Cosmos DB infrastructure</description></item>
	/// </list>
	/// </remarks>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public CosmosDbSagaStore(
		CosmosClient client,
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;

		// Injected: the caller owns this client's lifetime; this store must not dispose it.
		_ownsClient = false;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CosmosDbSagaDocument.CreateId(sagaId);
		var sagaType = typeof(TSagaState).Name;

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
				documentId,
				new PartitionKey(sagaType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;

			// A saga belonging to another tenant is "not found" from this scope's perspective — the same
			// answer a tenant-filtered query would have given, reached one step later.
			if (!OwnedByCurrentScope(document))
			{
				return null;
			}

			if (string.IsNullOrEmpty(document.StateJson))
			{
				return null;
			}

			var result = _serializer.Deserialize<TSagaState>(document.StateJson);
			if (result is not null)
			{
				// The authoritative optimistic-concurrency version is the dedicated document field, NOT the
				// version embedded in StateJson (serialized before the store-owns-increment write-back, so it
				// carries the stale pre-save version). Apply it so load-modify-save gates against the real value.
				result.Version = document.Version;
			}

			LogSagaLoaded(sagaType, sagaId);
			return result;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stateJson = _serializer.Serialize(sagaState);
		var now = DateTimeOffset.UtcNow;
		var sagaType = typeof(TSagaState).Name;
		var documentId = CosmosDbSagaDocument.CreateId(sagaState.SagaId);
		var partitionKey = new PartitionKey(sagaType);
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency (bd-e1tsq2), mirroring SqlServerSagaStore's version-gated MERGE: the
		// persisted version must equal the loaded (expected) version, otherwise a ConcurrencyException is
		// thrown instead of silently overwriting a newer write (the previous unconditional upsert lost
		// concurrent updates). IfMatchEtag closes the read->write race window: a writer that commits between
		// our read and our upsert changes the document's _etag, so the upsert fails with 412 PreconditionFailed.
		try
		{
			// Try to read existing saga to obtain the current version + etag and preserve createdUtc.
			var readResponse = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var existing = readResponse.Resource;

			// Ownership BEFORE version. Without this check a save under tenant A that carries tenant B's
			// SagaId reads B's document and, if the versions happen to agree, upserts A's state over it —
			// a cross-tenant OVERWRITE, not merely a disclosure. When they disagree it instead throws a
			// ConcurrencyException carrying B's version number, which discloses a row this caller is not
			// entitled to know exists. Treating it as absent gives the same answer a tenant-filtered query
			// would: this scope has no saga at that id, so the insert path below decides what happens next.
			if (!OwnedByCurrentScope(existing))
			{
				// NOT a ConcurrencyException. A conflict is transient and a correct caller reloads and retries;
				// this is permanent, so that caller would retry a cross-tenant write forever. The distinct type
				// also carries no version, which keeps the other tenant's state out of the error entirely.
				throw new TenantIsolationViolationException(nameof(SagaState), sagaState.SagaId.ToString());
			}

			if (existing.Version != expectedVersion)
			{
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					existing.Version);
			}

			// Update document preserving createdUtc, advancing the version.
			//
			// TenantId is carried over from the EXISTING document, never recomputed from the ambient scope:
			// ownership is fixed when the saga is created. Recomputing it here would let a save under a
			// different scope re-home an existing saga instead of being refused — the overwrite leak. The
			// ownership check above has already established that this scope may touch this document at all.
			var document = new CosmosDbSagaDocument
			{
				Id = documentId,
				TenantId = existing.TenantId,
				SagaId = sagaState.SagaId,
				SagaType = sagaType,
				StateJson = stateJson,
				IsCompleted = sagaState.Completed,
				CompletedAt = sagaState.CompletedAt?.UtcDateTime,
				Version = expectedVersion + 1,
				CreatedUtc = existing.CreatedUtc, // Preserve original
				UpdatedUtc = now
			};

			try
			{
				_ = await _container!.UpsertItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions
					{
						EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
						IfMatchEtag = readResponse.ETag,
					},
					cancellationToken).ConfigureAwait(false);
			}
			catch (CosmosException upsertEx) when (upsertEx.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				// A concurrent writer modified the document between our read and our write (etag mismatch).
				// Re-read the current version so the exception carries the real actual version (diagnostic
				// parity with the other saga stores) rather than a -1 "unknown" sentinel (e25wz5).
				var current = await LoadAsync<TSagaState>(sagaState.SagaId, cancellationToken).ConfigureAwait(false);
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					current?.Version ?? -1L);
			}

			LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// No-resurrect guard (SqlServer reference contract): only a brand-new saga (expected version 0)
			// may be created. A stale save (expected > 0) against a missing document is a deleted/completed
			// saga — throw rather than resurrect it at a high version (zombie saga). Mirrors the MERGE's
			// "@ExpectedVersion = 0"-guarded INSERT branch.
			if (expectedVersion != 0)
			{
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					actualVersion: -1L);
			}

			// No existing saga, create new with current timestamp as createdUtc. This is the ONE place a
			// tenant is assigned: at creation, from the ambient scope, or the saga's own tenant when unscoped.
			var scope = TenantScope.FromContext(_tenantContext);
			var document = new CosmosDbSagaDocument
			{
				Id = documentId,
				TenantId = scope.IsScoped ? scope.TenantId : sagaState.TenantId,
				SagaId = sagaState.SagaId,
				SagaType = sagaType,
				StateJson = stateJson,
				IsCompleted = sagaState.Completed,
				CompletedAt = sagaState.CompletedAt?.UtcDateTime,
				Version = expectedVersion + 1,
				CreatedUtc = now,
				UpdatedUtc = now
			};

			try
			{
				_ = await _container!.CreateItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
					cancellationToken).ConfigureAwait(false);

				LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
			}
			catch (CosmosException createEx) when (createEx.StatusCode == HttpStatusCode.Conflict)
			{
				// Race: another process created the document between our NotFound read and our create. That
				// is a concurrency conflict — surface it instead of clobbering the other writer's state.
				// The conflicting document may belong to another tenant; the ownership check below keeps its
				// version out of the exception for the same reason as the save-path read above.
				var conflictReadResponse = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					OwnedByCurrentScope(conflictReadResponse.Resource)
						? conflictReadResponse.Resource.Version
						: -1L);
			}
		}

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;
	}

	/// <inheritdoc/>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		// This store has no tenant discriminator: it persists the saga state as a serialized blob, so the
		// tenant travels INSIDE the document rather than as a queryable field. It cannot build a server-side
		// tenant predicate, which makes it an untenanted-only store -- a coherent, supported shape under the
		// settled semantics, where every row it owns lives in the untenanted partition.
		//
		// So an unscoped purge is correct and proceeds. A SCOPED purge is refused rather than serviced,
		// because the only thing this store could do with a tenant is ignore it -- and ignoring it here means
		// deleting every OTHER tenant's completed sagas while reporting success. This is a range delete with
		// no reachability gate: unlike a point load, the caller needs nothing but a timestamp to destroy
		// another tenant's data. Failing loud is the one honest answer available to it.
		var scope = TenantScope.FromContext(_tenantContext);
		if (scope.IsScoped)
		{
			throw new TenantScopeNotSupportedException(
				$"This saga store cannot purge within a tenant scope. Store type: '{GetType().FullName}'. " +
				"It persists saga state as a serialized document, so the tenant is not a queryable field and " +
				"no tenant predicate can be applied. Servicing the call would delete every tenant's completed " +
				"sagas. Use a store that discriminates by tenant (SQL Server, Postgres, Oracle), or call " +
				"PurgeAllTenantsCompletedBeforeAsync if an estate-wide sweep is what you intended.");
		}

		return PurgeAllTenantsCompletedBeforeAsync(threshold, cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep, and the only purge this store can perform. It is identical to the unscoped
	/// path above because a store with no tenant discriminator cannot distinguish the two — which is exactly
	/// why the scoped call refuses instead of silently landing here.
	/// </remarks>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query the dedicated completedAt field directly (not out of StateJson) so a running saga
		// (completedAt == null) is never purged. IS_DEFINED guards documents written before this field
		// existed; the cutoff is compared as UTC so it lines up with the stored UTC value.
		var cutoff = threshold.UtcDateTime;
		var query = new QueryDefinition(
				"SELECT c.id, c.sagaType FROM c WHERE IS_DEFINED(c.completedAt) AND c.completedAt != null AND c.completedAt < @cutoff")
			.WithParameter("@cutoff", cutoff);

		var purged = 0;

		// Cosmos has no bulk "delete where"; page the matches and delete each by (id, partition key).
		using var iterator = _container!.GetItemQueryIterator<CosmosDbSagaDocument>(query);
		while (iterator.HasMoreResults)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			foreach (var document in page)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					_ = await _container!.DeleteItemAsync<CosmosDbSagaDocument>(
						document.Id,
						new PartitionKey(document.SagaType),
						cancellationToken: cancellationToken).ConfigureAwait(false);
					purged++;
				}
				catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
				{
					// Already removed by a concurrent sweep/completion — not an error for a retention purge.
				}
			}
		}

		LogSagasPurged(purged, threshold);
		return purged;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

			if (_client == null)
			{
				var clientOptions = CreateClientOptions();
				_client = CreateClient(clientOptions);

				// Created here, so this store owns it and disposes it.
				_ownsClient = true;
			}

			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath);

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

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
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

	/// <summary>
	/// Returns whether a document belongs to the store's current scope.
	/// </summary>
	/// <remarks>
	/// Cosmos point reads (<c>ReadItemAsync</c>) address a document by id and partition key; there is no
	/// server-side predicate to attach, so ownership has to be checked after the read rather than expressed in
	/// the query. That is weaker than filtering — the document crosses the wire before it is rejected — but it
	/// is the strongest control available without folding the tenant into the partition key, which Cosmos does
	/// not allow to change without recreating the container. The caller never receives another tenant's saga.
	/// </remarks>
	private bool OwnedByCurrentScope(CosmosDbSagaDocument document)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		return string.Equals(
			document.TenantId,
			scope.IsScoped ? scope.TenantId : null,
			StringComparison.Ordinal);
	}

	[LoggerMessage(DataCosmosDbEventId.SagaStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB saga store with container '{ContainerName}'")]
	private partial void LogInitialized(string containerName);

	[LoggerMessage(DataCosmosDbEventId.SagaStateLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataCosmosDbEventId.SagaStateSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);

	[LoggerMessage(DataCosmosDbEventId.SagaStatePurged, LogLevel.Information, "Purged {Count} completed sagas older than {Threshold}")]
	private partial void LogSagasPurged(int count, DateTimeOffset threshold);

	/// <summary>Disposes the Cosmos client only when this instance created it.</summary>
	private void DisposeClientIfOwned()
	{
		if (_ownsClient)
		{
			_client?.Dispose();
		}
	}
}
