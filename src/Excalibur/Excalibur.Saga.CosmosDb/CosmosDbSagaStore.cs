// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Net;

using Excalibur.Data;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
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

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs at the first point the store would act on
	// the ABSENCE of a document, which is the first moment an unaddressable saga could be mistaken for one
	// that was never started.
	private volatile bool _legacyDocumentsProbed;

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
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public CosmosDbSagaStore(
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
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
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public CosmosDbSagaStore(
		CosmosClient client,
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
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
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// The tenant is part of the document's IDENTITY, so this scope addresses its own document rather than
		// a shared one it must then be refused access to. The ownership check below is retained on top: it is
		// redundant for a document this store wrote (identity and stored field are assigned once from the same
		// scope, and the field is never re-stamped) and is the check that still holds for one it did not.
		var documentId = CosmosDbSagaDocument.CreateId(CurrentTenantScope.TenantId, sagaId);
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
			// The ABSENCE decision, and the one the caller acts on: a null here is read as "no saga in
			// flight", so the caller starts the saga over and re-fires every compensating action and external
			// call it already performed. A document written under the pre-tenant identifier answers exactly
			// this way, because the point read cannot address it.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return null;
		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("The saga state is serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stateJson = _serializer.Serialize(sagaState);
		var now = DateTimeOffset.UtcNow;
		var sagaType = typeof(TSagaState).Name;
		var documentId = CosmosDbSagaDocument.CreateId(CurrentTenantScope.TenantId, sagaState.SagaId);
		var partitionKey = new PartitionKey(sagaType);
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency, mirroring SqlServerSagaStore's version-gated MERGE: the
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
				// parity with the other saga stores) rather than a -1 "unknown" sentinel.
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

			// The create path acts on absence too, and more destructively than the load: CreateItemAsync
			// addresses the NEW identifier, so a saga already running under the old one does not produce a
			// 409 - it is simply invisible, and a second, duplicate saga is created beside it. Probed before
			// the write, while nothing has been modified.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);

			// No existing saga, create new with current timestamp as createdUtc. This is the ONE place a
			// tenant is assigned: at creation, from the ambient scope, or the saga's own tenant when unscoped.
			var scope = CurrentTenantScope;
			var document = new CosmosDbSagaDocument
			{
				Id = documentId,
				TenantId = scope.TenantId,
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
	/// <remarks>
	/// The tenant IS a discriminator on this document: it is persisted as its own top-level field beside the
	/// state blob, not inside it, so the query below applies it as a real predicate rather than refusing on the
	/// grounds that it cannot. <see cref="TenantScope.TenantId"/> is total -- untenanted, the single-tenant
	/// default, and a real tenant all bind a concrete term -- so this purge always filters, never refuses.
	/// </remarks>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, CurrentTenantScope.TenantId, cancellationToken);

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep: no tenant predicate, every tenant's completed sagas in range. Reachable only by
	/// calling this method directly, never as a fallback from the scoped purge above.
	/// </remarks>
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, tenantId: null, cancellationToken);

	/// <summary>
	/// Deletes completed, aged saga documents, optionally confined to one tenant.
	/// </summary>
	/// <param name="threshold">Sagas completed strictly before this instant are eligible.</param>
	/// <param name="tenantId">
	/// The tenant term to filter on, or <see langword="null"/> for the estate-wide sweep that applies no
	/// tenant predicate at all.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task<int> PurgeCoreAsync(DateTimeOffset threshold, string? tenantId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Query the dedicated completedAt field directly (not out of StateJson) so a running saga
		// (completedAt == null) is never purged. IS_DEFINED guards documents written before this field
		// existed; the cutoff is compared as UTC so it lines up with the stored UTC value.
		var cutoff = threshold.UtcDateTime;
		var queryText = "SELECT c.id, c.sagaType FROM c WHERE IS_DEFINED(c.completedAt) AND c.completedAt != null AND c.completedAt < @cutoff";
		if (tenantId is not null)
		{
			queryText += " AND c.tenantId = @tenantId";
		}

		var query = new QueryDefinition(queryText).WithParameter("@cutoff", cutoff);
		if (tenantId is not null)
		{
			query = query.WithParameter("@tenantId", tenantId);
		}

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

	/// <summary>
	/// Verifies, at most once per store instance, that an absent saga is genuinely absent rather than merely
	/// unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of a document, and from
	/// nowhere else. A read that returns a document proves the container is addressable and needs no probe;
	/// only silence is ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path. Probing there would spend a request on every process
	/// start - on every serverless cold start, forever - to detect a condition that can only hold across a
	/// one-time upgrade, and would make the store unusable without a live container even for operations that
	/// never read one. Here it costs nothing at startup, nothing on a read that finds a document, and at most
	/// one request per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-absence decisions may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra request and nothing else - cheaper than serialising every
	/// empty read behind a lock. The flag is set only once the probe has come back clean, so a container that
	/// holds legacy documents refuses every call rather than only the first.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureEmptyReadIsTrustworthyAsync(CancellationToken cancellationToken)
	{
		if (_legacyDocumentsProbed)
		{
			return;
		}

		await RefuseLegacyUntenantedDocumentsAsync(_container!, cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	/// <summary>
	/// Refuses when the saga container still holds a document written under the untenanted identifier of an
	/// earlier release. Called only through <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides
	/// when it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such a document is unaddressable under the current key shape, and the failure that follows is silent:
	/// a load returns NO SAGA rather than an error, so the caller treats a saga that is already part-executed
	/// as new and starts it again - re-firing every compensating action and every external call that has
	/// already happened. On the create path the same silence lets a second, duplicate saga be created beside
	/// the original. Refusing converts that silence into a failure while both the state and the correlation
	/// are still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted document is a question about the
	/// deployment rather than about the data, so it cannot be decided here; the message states the procedure
	/// instead.
	/// </para>
	/// </remarks>
	/// <param name="container">The saga container to probe.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The container holds at least one saga document whose identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(
		Container container,
		CancellationToken cancellationToken)
	{
		// SELECT VALUE yields the identifier itself, so the probe reads the same whichever serializer the
		// consumer-supplied client is configured with.
		var query = new QueryDefinition(
				"SELECT TOP 1 VALUE c.id FROM c WHERE c.id < @prefix OR c.id >= @upperBound")
			.WithParameter("@prefix", CosmosDbSagaDocument.TenantKeyPrefix)
			.WithParameter("@upperBound", CosmosDbSagaDocument.TenantKeyPrefixUpperBound);

		using var iterator = container.GetItemQueryIterator<string>(
			query,
			requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

		// A cross-partition query can return an empty page while later partitions still hold results, so the
		// pages are drained rather than sampled. TOP 1 bounds the total.
		while (iterator.HasMoreResults)
		{
			var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			var legacyDocumentId = page.FirstOrDefault();

			if (legacyDocumentId is null)
			{
				continue;
			}

			throw new InvalidOperationException(
				$"Saga container '{_options.ContainerName}' holds at least one saga document whose " +
				$"identifier ('{legacyDocumentId}') carries no tenant segment, so it was written by a release " +
				$"that stored sagas without one. Those documents are unaddressable under the current key " +
				$"shape: a load of the saga they belong to reports no saga in flight, so the caller starts it " +
				$"again and re-runs every compensating action and external call it has already performed, and " +
				$"a create writes a second saga beside the first. Nothing has been modified. Stop the saga " +
				$"host, export every saga document, re-key each one by prefixing " +
				$"'{CosmosDbSagaDocument.TenantKeyPrefix}<tenantId>:' with the tenant that owns the saga, " +
				$"re-import, and start the application again.");
		}
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
		var scope = CurrentTenantScope;
		return string.Equals(
			document.TenantId,
			scope.TenantId,
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
