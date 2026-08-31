// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.EventSourcing.Observability;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of the cloud-native event store.
/// </summary>
public sealed partial class CosmosDbEventStore : ICloudNativeEventStore, ICloudNativeProviderInfo,
	ICloudNativeEventStoreChangeFeed, ICloudNativeEventStoreInfo, IEventStore, IAsyncDisposable
{
	/// <summary>
	/// The leading segment every stream identifier this store writes carries, ahead of the owning tenant.
	/// </summary>
	/// <remarks>
	/// Declared once and consumed by both the key builder and the legacy-document probe, so the shape the
	/// store writes and the shape it refuses to read cannot drift apart.
	/// </remarks>
	private const string TenantKeyPrefix = "t:";

	/// <summary>
	/// Exclusive upper bound of the tenant-prefixed key range, used by the legacy-document probe.
	/// </summary>
	/// <remarks>
	/// <c>':'</c> is U+003A and <c>';'</c> is U+003B, so every identifier beginning with
	/// <see cref="TenantKeyPrefix"/> sorts inside <c>["t:", "t;")</c> and every identifier outside that
	/// range lacks the prefix. Expressing the probe as a range keeps it served by the container's ordered
	/// index rather than by a scan.
	/// </remarks>
	private const string TenantKeyPrefixUpperBound = "t;";

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs on the first read that comes back
	// empty, which is the first moment an unaddressable document could be mistaken for an absent one.
	private volatile bool _legacyDocumentsProbed;

	private readonly CosmosClient _cosmosClient;
	private readonly IOptions<CosmosDbEventStoreOptions> _options;

	/// <summary>
	/// Writes event payloads and metadata under the canonical wire contract, through the host's
	/// source-generated type-info resolver when one was configured.
	/// </summary>
	private readonly CosmosDbEventPayloadWriter _payloadWriter;
	private readonly ILogger<CosmosDbEventStore> _logger;
	private readonly ITenantContext _tenantContext;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;

	private Container? _container;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStore"/> class.
	/// </summary>
	/// <param name="cosmosClient">The Cosmos DB client.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="checkpointStore">
	/// Optional durable change-feed checkpoint store (DI-supplied; the registered
	/// <see cref="IChangeFeedCheckpointStore"/> — in-memory default or the durable Cosmos store when the
	/// consumer opts in). Flowed into the event-store change-feed subscription so continuation survives
	/// restarts. <see langword="null"/> only for manual construction without DI (in-memory-only).
	///
	/// </param>
	public CosmosDbEventStore(
		CosmosClient cosmosClient,
		IOptions<CosmosDbEventStoreOptions> options,
		ILogger<CosmosDbEventStore> logger,
		ITenantContext tenantContext,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_checkpointStore = checkpointStore;
		_payloadWriter = new CosmosDbEventPayloadWriter(_options.Value.EventTypeInfoResolver);
	}

	/// <inheritdoc/>
	public CloudPersistenceProviderType CloudProvider => CloudPersistenceProviderType.CosmosDb;

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ICloudNativeProviderInfo))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativeEventStoreChangeFeed))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativeEventStoreInfo))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc/>
	public async Task<CloudEventLoadResult> LoadAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(partitionKey);
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var query = new QueryDefinition("SELECT * FROM c WHERE c.streamId = @streamId ORDER BY c.version")
				.WithParameter("@streamId", streamId);

			var events = new List<CloudStoredEvent>();
			double totalRu = 0;
			string? sessionToken = null;

			var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

			using var iterator = _container!.GetItemQueryIterator<EventDocument>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRu += response.RequestCharge;
				sessionToken = response.Headers.Session;

				foreach (var doc in response)
				{
					events.Add(ToCloudStoredEvent(doc));
				}
			}

			if (events.Count == 0)
			{
				// An empty result is the ambiguous one: either this aggregate was never written, or it was
				// written under the untenanted key shape and is unaddressable now. Refuse before reporting
				// emptiness to a caller who would read it as a new aggregate.
				await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			}

			LogLoadingEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalRu, sessionToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudEventLoadResult> LoadFromVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long fromVersion,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(partitionKey);
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var streamId = BuildStreamId(aggregateType, aggregateId);
			var query = new QueryDefinition(
					"SELECT * FROM c WHERE c.streamId = @streamId AND c.version > @fromVersion ORDER BY c.version")
				.WithParameter("@streamId", streamId)
				.WithParameter("@fromVersion", fromVersion);

			var events = new List<CloudStoredEvent>();
			double totalRu = 0;
			string? sessionToken = null;

			var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

			using var iterator = _container!.GetItemQueryIterator<EventDocument>(query, requestOptions: queryOptions);

			while (iterator.HasMoreResults)
			{
				var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
				totalRu += response.RequestCharge;
				sessionToken = response.Headers.Session;

				foreach (var doc in response)
				{
					events.Add(ToCloudStoredEvent(doc));
				}
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalRu, sessionToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// When transactional batching is enabled, an append commits as a single atomic Cosmos DB
	/// transactional batch within the stream's partition. Cosmos DB caps a transactional batch at 100
	/// operations and offers no larger atomic primitive, so an append of <b>more than 100 events</b> is
	/// rejected with <see cref="EventBatchTooLargeException"/> before anything is written, rather than
	/// committed as a sequence of batches that could leave a torn prefix behind. Callers split the append
	/// into batches of at most 100 events, or set <c>UseTransactionalBatch=false</c> to opt into the
	/// documented non-atomic sequential path.
	/// </remarks>
	public async Task<CloudAppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(partitionKey);
		ArgumentNullException.ThrowIfNull(events);
		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var eventList = events.ToList();
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);
		if (eventList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				result,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		// On the ATOMIC (transactional-batch) path, Cosmos DB hard-caps a TransactionalBatch at 100 operations
		// and offers no >100 atomic primitive, so an all-or-nothing append (the IEventStore.AppendAsync
		// contract) is impossible beyond 100 events. Reject at the boundary BEFORE any write rather than
		// commit sequential batches and risk a torn event-stream prefix -- callers split into <=100-event
		// appends. (A torn append is event-stream corruption, which event sourcing must never produce, and a
		// consumer holding a torn stream cannot detect it: it has a prefix and no suffix, and every later read
		// is consistent with a shorter history.) The non-transactional opt-out path (UseTransactionalBatch=false)
		// is NOT rejected: the consumer explicitly traded away atomicity for the per-item sequential path, so
		// >100 is its accepted (documented non-atomic) behavior.
		if (_options.Value.UseTransactionalBatch && eventList.Count > MaxTransactionalBatchOperations)
		{
			throw new EventBatchTooLargeException(
				nameof(events),
				eventList.Count,
				MaxTransactionalBatchOperations,
				$"Cosmos DB atomic append is limited to {MaxTransactionalBatchOperations} events per call; split the batch into appends of at most {MaxTransactionalBatchOperations} events, or set UseTransactionalBatch=false to opt into the non-atomic sequential path.");
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		var streamId = BuildStreamId(aggregateType, aggregateId);
		LogAppendingEvents(streamId, aggregateType);

		var pk = new Microsoft.Azure.Cosmos.PartitionKey(streamId);

		try
		{
			// Contiguity + concurrency pre-check — match the SQL/InMemory contract (currentVersion must equal
			// expectedVersion). The id-uniqueness guard ('{streamId}:{version}' → 409) rejects a STALE
			// expectedVersion (collision on an existing version) but NOT a GAP: an expectedVersion beyond the
			// stream tail targets an unused id, so the write would silently succeed and leave a hole in the
			// stream. Re-read the tail (MAX(c.version), -1 for an empty stream) and reject a non-contiguous
			// expectedVersion before writing. The concurrent-writer race is still caught by the 409 catch below.
			var precheckVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			if (precheckVersion != expectedVersion)
			{
				LogConcurrencyConflict(streamId, expectedVersion);
				result = WriteStoreTelemetry.Results.Conflict;
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, precheckVersion, 0);
			}

			CloudAppendResult appendResult;
			if (_options.Value.UseTransactionalBatch && eventList.Count > 1)
			{
				appendResult = await AppendWithTransactionAsync(
						streamId, aggregateId, aggregateType, partitionKey, eventList, expectedVersion, pk, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				appendResult = await AppendSequentiallyAsync(
						streamId, aggregateId, aggregateType, eventList, expectedVersion, pk, cancellationToken)
					.ConfigureAwait(false);
			}

			if (appendResult.Success)
			{
				_ = (activity?.SetTag(EventSourcingTags.Version, appendResult.NextExpectedVersion));
				activity.SetOperationResult(EventSourcingTagValues.Success);
			}
			else if (appendResult.IsConcurrencyConflict)
			{
				// A batch reports its outcome by returning, not by throwing, so a lost race arrives here
				// rather than at the conflict handler below. It is the same outcome and is recorded as one.
				LogConcurrencyConflict(streamId, expectedVersion);
				result = WriteStoreTelemetry.Results.Conflict;
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			}
			else
			{
				result = WriteStoreTelemetry.Results.Failure;
				activity.SetOperationResult(EventSourcingTagValues.Failure);
			}

			return appendResult;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			LogConcurrencyConflict(streamId, expectedVersion);
			result = WriteStoreTelemetry.Results.Conflict;
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion, ex.RequestCharge);
		}
		// Only a provider fault normalizes to a failure result. Cancellation, and any programming error
		// (a null reference, a bad argument), propagates untouched: the caller asked to stop, or the code is
		// wrong. Neither is a store outcome, and neither should be retried by a resilience pipeline.
		catch (CosmosException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);

			// Liskov (MS-01): a transient store fault is REPORTED as a failed result, never propagated as a
			// raw provider exception — a leaked CosmosException is the substitutability violation. Version
			// conflicts are already returned above; every other fault returns a failure the caller handles
			// uniformly across providers.
			var requestCharge = ex is CosmosException cosmosEx ? cosmosEx.RequestCharge : 0d;
			return CloudAppendResult.CreateFailure(ex.Message, requestCharge);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.CosmosDb,
				"append",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new CosmosDbEventStoreChangeFeedSubscription(
			_container!,
			_options.Value,
			_logger,
			_checkpointStore);

		return subscription;
	}

	/// <inheritdoc/>
	public async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(partitionKey);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var streamId = BuildStreamId(aggregateType, aggregateId);
		var query = new QueryDefinition(
				"SELECT VALUE MAX(c.version) FROM c WHERE c.streamId = @streamId")
			.WithParameter("@streamId", streamId);

		var queryOptions = new QueryRequestOptions { PartitionKey = new Microsoft.Azure.Cosmos.PartitionKey(streamId) };

		using var iterator = _container!.GetItemQueryIterator<long?>(query, requestOptions: queryOptions);

		if (iterator.HasMoreResults)
		{
			var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			var version = response.FirstOrDefault();

			if (version is null)
			{
				// AppendAsync prechecks through here, so guarding this answer also guards the write that
				// would otherwise start a second, disjoint history at version 0.
				await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			}

			return version ?? -1;
		}

		await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
		return -1;
	}

	// IEventStore implementation
	/// <inheritdoc/>
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		var partitionKey = new Data.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadAsync(aggregateId, aggregateType, partitionKey, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc/>
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		var partitionKey = new Data.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadFromVersionAsync(aggregateId, aggregateType, partitionKey, fromVersion, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc/>
	async ValueTask<AppendResult> IEventStore.AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(events);
		var partitionKey = new Data.CloudNative.PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await AppendAsync(aggregateId, aggregateType, partitionKey, events, expectedVersion, cancellationToken)
			.ConfigureAwait(false);

		if (result.Success)
		{
			// Cosmos DB has no store-wide global sequence across partitions/streams; global ordering is
			// unsupported for this provider, so no global first-event position is reported.
			// A successful CloudAppendResult always states the version it advanced the stream to.
			return AppendResult.CreateSuccess(result.NextExpectedVersion!.Value, firstEventPosition: null);
		}

		if (result.IsConcurrencyConflict)
		{
			// A concurrency conflict is the one failure that measured the stream's actual version, so it
			// always states one.
			return AppendResult.CreateConcurrencyConflict(expectedVersion, result.NextExpectedVersion!.Value);
		}

		return AppendResult.CreateFailure(result.ErrorMessage ?? "Unknown error");
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
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
		await Task.CompletedTask.ConfigureAwait(false);
	}



	private EventDocument CreateEventDocument(
		string streamId,
		string aggregateId,
		string aggregateType,
		IDomainEvent evt,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(evt.GetType());

		return new EventDocument
		{
			Id = $"{streamId}:{version}",
			StreamId = streamId,
			EventId = evt.EventId.ToString(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			EventType = eventTypeName,
			Version = version,
			Timestamp = evt.OccurredAt,
#pragma warning disable IL2026, IL3050
			EventData = _payloadWriter.SerializeEvent(evt, aggregateId, aggregateType),
			Metadata = evt.Metadata != null ? _payloadWriter.SerializeMetadata(evt.Metadata) : null
#pragma warning restore IL2026, IL3050
		};
	}

	/// <summary>
	/// Composes the Cosmos partition key for one stream, with the owning tenant as its leading segment.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the stream's IDENTITY rather than a filter applied to it. A filter would scope
	/// reads while leaving two tenants sharing one document set and one version sequence, so the second
	/// tenant to use an aggregate identifier would be told it has a concurrency conflict on a stream it
	/// never wrote. Composing the key gives each tenant its own logical partition, its own documents, and
	/// its own version sequence, and makes a cross-tenant read unaddressable rather than merely filtered.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default, and a genuinely untenanted row resolves the reserved untenanted sentinel. So
	/// every key carries a tenant segment and none can be produced without one.
	/// </para>
	/// </remarks>
	private string BuildStreamId(string aggregateType, string aggregateId) =>
		$"{TenantKeyPrefix}{TenantScope.FromContext(_tenantContext).TenantId}:{aggregateType}:{aggregateId}";

	private static string? ExtractCorrelationId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (@event.Metadata == null)
			{
				continue;
			}

			if (@event.Metadata.TryGetValue("CorrelationId", out var correlationId) ||
				@event.Metadata.TryGetValue("correlationId", out correlationId))
			{
				return correlationId?.ToString();
			}
		}

		return null;
	}

	private static string? ExtractEventId(IEnumerable<IDomainEvent> events)
	{
		foreach (var @event in events)
		{
			if (!string.IsNullOrWhiteSpace(@event.EventId))
			{
				return @event.EventId;
			}
		}

		return null;
	}

	private static CloudStoredEvent ToCloudStoredEvent(EventDocument doc) =>
		new()
		{
			EventId = doc.EventId,
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			EventType = doc.EventType,
			Version = doc.Version,
			Timestamp = doc.Timestamp,
			EventData = doc.EventData,
			Metadata = doc.Metadata,
			PartitionKeyValue = doc.StreamId,
			DocumentId = doc.Id,
			ETag = doc.ETag
		};

	private static StoredEvent ToStoredEvent(CloudStoredEvent cloudEvent) =>
		new(
			cloudEvent.EventId,
			cloudEvent.AggregateId,
			cloudEvent.AggregateType,
			cloudEvent.EventType,
			cloudEvent.EventData,
			cloudEvent.Metadata,
			cloudEvent.Version,
			cloudEvent.Timestamp);

	/// <summary>
	/// Refuses when the events container still holds a document written under the untenanted key shape of an
	/// earlier release. Called only through <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides
	/// when it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such a document is unaddressable under the current key shape, and the failure that follows is the
	/// worst one available: a load returns an EMPTY STREAM rather than an error, so the caller sees a new
	/// aggregate, appends at version 0, and ends holding two disjoint histories under one identity. Refusing
	/// converts that silence into a failure while every event is still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted document is a question about the
	/// deployment rather than about the data, so it cannot be decided here; the message states the
	/// procedure instead.
	/// </para>
	/// </remarks>
	/// <param name="container">The events container to probe.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The container holds at least one event document whose stream identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(
		Container container,
		CancellationToken cancellationToken)
	{
		// SELECT VALUE yields the identifier itself, so the probe reads the same whichever serializer the
		// consumer-supplied client is configured with.
		var query = new QueryDefinition(
				"SELECT TOP 1 VALUE c.streamId FROM c WHERE c.streamId < @prefix OR c.streamId >= @upperBound")
			.WithParameter("@prefix", TenantKeyPrefix)
			.WithParameter("@upperBound", TenantKeyPrefixUpperBound);

		using var iterator = container.GetItemQueryIterator<string>(
			query,
			requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

		// A cross-partition query can return an empty page while later partitions still hold results, so
		// the pages are drained rather than sampled. TOP 1 bounds the total.
		while (iterator.HasMoreResults)
		{
			var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
			var legacyStreamId = page.FirstOrDefault();

			if (legacyStreamId is null)
			{
				continue;
			}

			throw new InvalidOperationException(
				$"Events container '{_options.Value.EventsContainerName}' holds at least one event " +
				$"document whose stream identifier ('{legacyStreamId}') carries no tenant segment, so it " +
				$"was written by a release that stored streams without one. Those documents are " +
				$"unaddressable under the current key shape: a load of the aggregate they belong to would " +
				$"return an empty stream, and the caller would then append a second, disjoint history " +
				$"under the same identity. Nothing has been modified. Stop writers, export every event " +
				$"document preserving version order within each stream, re-key each one by prefixing " +
				$"'{TenantKeyPrefix}<tenantId>:' with the tenant that owns the aggregate, re-import, and " +
				$"start the application again.");
		}
	}

	/// <summary>
	/// Verifies, at most once per store instance, that an empty read from the events container means the
	/// stream is genuinely absent rather than merely unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of documents, and from
	/// nowhere else. A read that returns rows proves the container is addressable and needs no probe; only
	/// silence is ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path. Probing there would spend a request on every process
	/// start - on every serverless cold start, forever - to detect a condition that can only hold across a
	/// one-time upgrade, and would make the store unconstructible without a live container. Here it costs
	/// nothing at startup, nothing on a read that finds data, and at most one request per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-empty-reads may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra request and nothing else - cheaper than serialising every
	/// empty read behind a lock. The flag is set only once the probe has come back clean, so a container
	/// that holds legacy documents refuses every call rather than only the first.
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

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check inside the lock: the winner finished while this caller waited.
			if (_initialized)
			{
				return;
			}
			var database = _cosmosClient.GetDatabase(_options.Value.DatabaseName);

			if (_options.Value.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(
					_options.Value.EventsContainerName,
					_options.Value.PartitionKeyPath);

				if (_options.Value.DefaultTimeToLiveSeconds > 0)
				{
					containerProperties.DefaultTimeToLive = _options.Value.DefaultTimeToLiveSeconds;
				}

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					ThroughputProperties.CreateManualThroughput(_options.Value.ContainerThroughput),
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.Value.EventsContainerName);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <summary>
	/// Maximum number of operations Cosmos DB permits in a single transactional batch.
	/// </summary>
	/// <remarks>
	/// Cosmos DB hard-caps a <c>TransactionalBatch</c> at 100 operations. An atomic append larger than
	/// this is refused at the boundary rather than split, because splitting cannot be all-or-nothing.
	/// </remarks>
	private const int MaxTransactionalBatchOperations = 100;

	/// <summary>
	/// Appends events using one or more Cosmos DB transactional batches.
	/// </summary>
	/// <remarks>
	/// The caller guarantees at most <see cref="MaxTransactionalBatchOperations"/> events reach here: the
	/// append boundary rejects a larger atomic batch, so one <c>TransactionalBatch</c> always covers the
	/// whole append and it is genuinely all-or-nothing. Each event's deterministic id makes a duplicate
	/// version a conflict.
	/// </remarks>
	private async Task<CloudAppendResult> AppendWithTransactionAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		List<IDomainEvent> events,
		long expectedVersion,
		Microsoft.Azure.Cosmos.PartitionKey pk,
		CancellationToken cancellationToken)
	{
		// The version the stream must still be at for this append to be appendable, kept for the failure
		// classifier below.
		var requiredVersion = expectedVersion;
		var version = expectedVersion;
		var batch = _container!.CreateTransactionalBatch(pk);

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);
			_ = batch.CreateItem(doc);
		}

		using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
		var totalRu = response.RequestCharge;

		if (!response.IsSuccessStatusCode)
		{
			return await ClassifyFailedBatchAsync(
					response, aggregateId, aggregateType, partitionKey, requiredVersion, totalRu, cancellationToken)
				.ConfigureAwait(false);
		}

		LogEventsAppended(streamId, events.Count, totalRu);
		return CloudAppendResult.CreateSuccess(version, totalRu, response.Headers.Session);
	}

	/// <summary>
	/// Classifies a transactional batch that did not succeed, as either a lost optimistic-concurrency race
	/// or a failure on the append's own account.
	/// </summary>
	/// <param name="response"> The unsuccessful batch response. </param>
	/// <param name="aggregateId"> The aggregate whose append failed. </param>
	/// <param name="aggregateType"> The aggregate type whose append failed. </param>
	/// <param name="partitionKey"> The partition holding the stream. </param>
	/// <param name="requiredVersion"> The version the stream had to be at for this batch to be appendable. </param>
	/// <param name="requestCharge"> The request units consumed by the append so far. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A concurrency-conflict result when the race was lost; otherwise a failure result. </returns>
	/// <remarks>
	/// <para>
	/// A batch REPORTS its outcome rather than throwing it. Executing a batch returns a response whose
	/// status carries the failure, so the conflict handler on the append -- which catches a thrown
	/// conflict, and is what the single-item path relies on -- never sees a batched one. Every loser of a
	/// race on this path was therefore reported as an opaque failure whose text happened to name the
	/// conflict, while the flag a caller's retry policy keys on stayed false: the caller surfaced an error
	/// instead of reloading and retrying an ordinary, expected outcome.
	/// </para>
	/// <para>
	/// A conflict status is a proof of conflict from the response alone, because every event id in the
	/// batch is derived from the stream and the version it claims, so the only document that can already
	/// exist under one is another writer's event at that version. It is read from the batch status and
	/// from each operation's status, because a failed batch reports the offending operation's status on
	/// that operation while its siblings report a failed dependency, and which of the two surfaces at the
	/// batch level is the SDK's business rather than a guarantee to rely on.
	/// </para>
	/// <para>
	/// The structural test stands behind it, for the same reason as elsewhere: a batch is atomic, so a
	/// failed one wrote nothing, and if the stream is no longer at the version this batch required, the
	/// precondition was lost to another writer whatever status surfaced. It cannot over-report -- a
	/// stream still sitting at the required version proves nothing else claimed it.
	/// </para>
	/// </remarks>
	private async Task<CloudAppendResult> ClassifyFailedBatchAsync(
		TransactionalBatchResponse response,
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		long requiredVersion,
		double requestCharge,
		CancellationToken cancellationToken)
	{
		var currentVersion = await ReadCurrentVersionAfterFailedAppendAsync(
			aggregateId, aggregateType, partitionKey, cancellationToken).ConfigureAwait(false);

		if (HasConflictStatus(response) || (currentVersion is { } version && version != requiredVersion))
		{
			return CloudAppendResult.CreateConcurrencyConflict(
				requiredVersion, currentVersion ?? requiredVersion, requestCharge);
		}

		return CloudAppendResult.CreateFailure(
			$"Transactional batch failed with status {response.StatusCode}",
			requestCharge);
	}

	/// <summary>Reports whether a batch response carries a conflict, at the batch or at any operation.</summary>
	/// <param name="response"> The batch response to inspect. </param>
	/// <returns> <see langword="true"/> when a conflict status is present. </returns>
	private static bool HasConflictStatus(TransactionalBatchResponse response)
	{
		if (response.StatusCode == HttpStatusCode.Conflict)
		{
			return true;
		}

		foreach (var operation in response)
		{
			if (operation.StatusCode == HttpStatusCode.Conflict)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Re-reads the stream's committed version after an append failed, or reports that it could not be read.</summary>
	/// <param name="aggregateId"> The aggregate whose append failed. </param>
	/// <param name="aggregateType"> The aggregate type whose append failed. </param>
	/// <param name="partitionKey"> The partition holding the stream. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The current persisted version, or <see langword="null"/> if it cannot be read. </returns>
	/// <remarks>
	/// Returns <see langword="null"/> rather than a substitute when the read fails, because the caller uses
	/// this value to decide whether the stream moved: supplying the required version there would read as
	/// "the stream did not move" and conclude "no conflict" from a measurement that never happened. The
	/// re-read's own fault is never allowed to replace the append's diagnosis, so it is swallowed to a
	/// no-value rather than propagated; cancellation still propagates.
	/// </remarks>
	private async Task<long?> ReadCurrentVersionAfterFailedAppendAsync(
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		try
		{
			return await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (CosmosException)
		{
			return null;
		}
	}

	private async Task<CloudAppendResult> AppendSequentiallyAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		Microsoft.Azure.Cosmos.PartitionKey pk,
		CancellationToken cancellationToken)
	{
		var version = expectedVersion;
		double totalRu = 0;
		string? sessionToken = null;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			var response = await _container!.CreateItemAsync(
				doc,
				pk,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			totalRu += response.RequestCharge;
			sessionToken = response.Headers.Session;
		}

		LogEventsAppended(streamId, events.Count, totalRu);
		return CloudAppendResult.CreateSuccess(version, totalRu, sessionToken);
	}
}

/// <summary>
/// Internal document model for Cosmos DB event storage.
/// </summary>
internal sealed class EventDocument
{
	/// <summary>
	/// Gets or sets the document ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("id")]
	[Newtonsoft.Json.JsonProperty("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream ID (partition key).
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("streamId")]
	[Newtonsoft.Json.JsonProperty("streamId")]
	public string StreamId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique event ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventId")]
	[Newtonsoft.Json.JsonProperty("eventId")]
	public string EventId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate ID.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("aggregateId")]
	[Newtonsoft.Json.JsonProperty("aggregateId")]
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate type.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("aggregateType")]
	[Newtonsoft.Json.JsonProperty("aggregateType")]
	public string AggregateType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event type.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventType")]
	[Newtonsoft.Json.JsonProperty("eventType")]
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event version.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("version")]
	[Newtonsoft.Json.JsonProperty("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("timestamp")]
	[Newtonsoft.Json.JsonProperty("timestamp")]
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the serialized event data.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("eventData")]
	[Newtonsoft.Json.JsonProperty("eventData")]
	public byte[] EventData { get; set; } = [];

	/// <summary>
	/// Gets or sets the serialized metadata.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("metadata")]
	[Newtonsoft.Json.JsonProperty("metadata")]
	public byte[]? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the ETag for concurrency control.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("_etag")]
	[Newtonsoft.Json.JsonProperty("_etag")]
	public string? ETag { get; set; }
}
