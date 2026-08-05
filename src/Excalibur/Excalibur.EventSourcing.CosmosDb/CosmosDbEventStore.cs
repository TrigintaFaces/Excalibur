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
	private readonly CosmosClient _cosmosClient;
	private readonly IOptions<CosmosDbEventStoreOptions> _options;
	private readonly ILogger<CosmosDbEventStore> _logger;
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
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;
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
	/// When transactional batching is enabled, an append of up to 100 events commits as a single atomic
	/// Cosmos DB transactional batch within the stream's partition. Cosmos DB caps a transactional batch
	/// at 100 operations, so an append of <b>more than 100 events</b> is committed in sequential batches of
	/// at most 100. Those batches are each individually atomic but are <b>not</b> a single atomic unit — a
	/// failure partway through a large append can leave earlier batches committed while later ones are not.
	/// Callers that require all-or-nothing semantics should keep a single append at or below 100 events.
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
						streamId, aggregateId, aggregateType, eventList, expectedVersion, pk, cancellationToken)
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
			return version ?? -1;
		}

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
			return AppendResult.CreateSuccess(result.NextExpectedVersion, firstEventPosition: null);
		}

		if (result.IsConcurrencyConflict)
		{
			return AppendResult.CreateConcurrencyConflict(expectedVersion, result.NextExpectedVersion);
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

	private static readonly System.Text.Json.JsonSerializerOptions CanonicalEventOptions =
		Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();

	private static EventDocument CreateEventDocument(
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
			EventData = JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), CanonicalEventOptions),
			Metadata = evt.Metadata != null ? JsonSerializer.SerializeToUtf8Bytes(evt.Metadata, CanonicalEventOptions) : null
#pragma warning restore IL2026, IL3050
		};
	}

	private static string BuildStreamId(string aggregateType, string aggregateId) =>
		$"{aggregateType}:{aggregateId}";

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
			var database = _cosmosClient.GetDatabase("events");

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
	/// Cosmos DB hard-caps a <c>TransactionalBatch</c> at 100 operations. Appends larger than this are
	/// split into multiple sequential batches (each atomic within its own partition, but not atomic across
	/// batches — see <see cref="AppendWithTransactionAsync"/>).
	/// </remarks>
	private const int MaxTransactionalBatchOperations = 100;

	/// <summary>
	/// Appends events using one or more Cosmos DB transactional batches.
	/// </summary>
	/// <remarks>
	/// Cosmos DB caps a single <c>TransactionalBatch</c> at
	/// <see cref="MaxTransactionalBatchOperations"/> operations. When the append exceeds that limit the
	/// events are committed in sequential batches of at most that size. Each batch is atomic within the
	/// stream's partition, but the batches are <b>not</b> a single atomic unit: an append of more than
	/// <see cref="MaxTransactionalBatchOperations"/> events commits in chunks, so a failure partway
	/// through can leave earlier chunks committed while later chunks are not. Optimistic-concurrency and
	/// version handling are preserved per chunk (each event's deterministic id makes a duplicate version a
	/// conflict).
	/// </remarks>
	private async Task<CloudAppendResult> AppendWithTransactionAsync(
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

		var chunkCount = (events.Count + MaxTransactionalBatchOperations - 1) / MaxTransactionalBatchOperations;
		if (chunkCount > 1)
		{
			LogLargeAppendChunked(streamId, events.Count, chunkCount, MaxTransactionalBatchOperations);
		}

		for (var offset = 0; offset < events.Count; offset += MaxTransactionalBatchOperations)
		{
			var batch = _container!.CreateTransactionalBatch(pk);
			var chunkEnd = Math.Min(offset + MaxTransactionalBatchOperations, events.Count);

			for (var i = offset; i < chunkEnd; i++)
			{
				version++;
				var doc = CreateEventDocument(streamId, aggregateId, aggregateType, events[i], version);
				_ = batch.CreateItem(doc);
			}

			using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
			totalRu += response.RequestCharge;

			if (!response.IsSuccessStatusCode)
			{
				return CloudAppendResult.CreateFailure(
					$"Transactional batch failed with status {response.StatusCode}",
					totalRu);
			}

			sessionToken = response.Headers.Session;
		}

		LogEventsAppended(streamId, events.Count, totalRu);
		return CloudAppendResult.CreateSuccess(version, totalRu, sessionToken);
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
