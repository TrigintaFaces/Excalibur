// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// AWS DynamoDB implementation of the cloud-native event store.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification =
		"Event store implementations orchestrate AWS SDK types, serialization, and domain abstractions - high coupling is inherent to the coordinator pattern.")]
public sealed partial class DynamoDbEventStore : ICloudNativeEventStore, ICloudNativeProviderInfo,
	ICloudNativeEventStoreChangeFeed, ICloudNativeEventStoreInfo, IEventStore, IAsyncDisposable
{
	/// <summary>The maximum number of items DynamoDB allows in a single <c>TransactWriteItems</c> call.</summary>
	private const int DynamoTransactItemLimit = 100;

	/// <summary>
	/// The leading segment every partition key this store writes carries, ahead of the owning tenant.
	/// </summary>
	/// <remarks>
	/// Declared once and consumed by both the key builder and the legacy-item probe, so the shape the store
	/// writes and the shape it refuses to read cannot drift apart.
	/// </remarks>
	private const string TenantKeyPrefix = "t:";

	// Set only once the legacy-item probe has come back clean. Separate from _initialized because the probe
	// is deliberately NOT on the initialisation path: it runs on the first read that comes back empty, which
	// is the first moment an unaddressable item could be mistaken for an absent one.
	private volatile bool _legacyItemsProbed;

	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams? _streamsClient;
	private readonly DynamoDbEventStoreOptions _options;
	private readonly ILogger<DynamoDbEventStore> _logger;
	private readonly ITenantContext _tenantContext;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// The single canonical event contract (camelCase + string-enum + null-ignore) shared by every event
	// store. Using the default serializer here would write PascalCase / enum-as-number bodies that mis-read
	// when loaded through the canonical read path (the cross-path fault).
	private readonly JsonSerializerOptions _jsonOptions = EventSerializationDefaults.CreateCanonicalOptions();

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;

	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStore" /> class without a Streams client.
	/// </summary>
	/// <remarks>
	/// The store reads and writes events through the DynamoDB client alone; the Streams client is needed
	/// only to serve a change feed. A store built this way is fully functional for appends, loads, and
	/// version queries, and reports the change feed as unavailable rather than failing to construct. Use
	/// the overload that also takes an <see cref="IAmazonDynamoDBStreams" /> to consume the change feed.
	/// </remarks>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public DynamoDbEventStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbEventStoreOptions> options,
		ILogger<DynamoDbEventStore> logger,
		ITenantContext tenantContext)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = null;
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStore" /> class.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="streamsClient"> The DynamoDB Streams client. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public DynamoDbEventStore(
		IAmazonDynamoDB client,
		IAmazonDynamoDBStreams streamsClient,
		IOptions<DynamoDbEventStoreOptions> options,
		ILogger<DynamoDbEventStore> logger,
		ITenantContext tenantContext)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
	}

	/// <inheritdoc />
	public CloudPersistenceProviderType CloudProvider => CloudPersistenceProviderType.DynamoDb;

	/// <summary>
	/// Returns the DynamoDB Streams client, or throws when this store was constructed without one.
	/// </summary>
	/// <returns> The Streams client backing the change-feed operations. </returns>
	/// <exception cref="InvalidOperationException">
	/// The store was constructed from a DynamoDB client alone, with no accompanying Streams client, so the
	/// change feed cannot be served.
	/// </exception>
	private IAmazonDynamoDBStreams EnsureStreamsClient() =>
		_streamsClient ?? throw new InvalidOperationException(
			"The DynamoDB event store has no DynamoDB Streams client, so the change feed is unavailable. " +
			"Supply one with the registration's StreamsClient/StreamsClientFactory, or register an " +
			"IAmazonDynamoDBStreams in the container. Configuring the store by service URL or region " +
			"builds both clients automatically.");

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ICloudNativeProviderInfo))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativeEventStoreChangeFeed))
		{
			// Only advertise the change feed when it can actually be served. Without a Streams client the
			// capability is genuinely absent, and the contract asks for null rather than an instance that
			// throws on first use.
			return _streamsClient is null ? null : this;
		}

		if (serviceType == typeof(ICloudNativeEventStoreInfo))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc />
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
			var events = new List<CloudStoredEvent>();
			double totalCapacity = 0;

			var request = new QueryRequest
			{
				TableName = _options.EventsTableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new AttributeValue { S = streamId } },
				ConsistentRead = true,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			do
			{
				var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;

				foreach (var item in response.Items)
				{
					events.Add(ToCloudStoredEvent(item));
				}

				request.ExclusiveStartKey = response.LastEvaluatedKey;
			} while (request.ExclusiveStartKey?.Count > 0);

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

			return new CloudEventLoadResult(events, totalCapacity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
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
			var events = new List<CloudStoredEvent>();
			double totalCapacity = 0;

			var request = new QueryRequest
			{
				TableName = _options.EventsTableName,
				KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk AND {_options.SortKeyAttribute} > :version",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":pk"] = new AttributeValue { S = streamId },
					[":version"] = new AttributeValue { N = fromVersion.ToString() }
				},
				ConsistentRead = true,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			do
			{
				var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;

				foreach (var item in response.Items)
				{
					events.Add(ToCloudStoredEvent(item));
				}

				request.ExclusiveStartKey = response.LastEvaluatedKey;
			} while (request.ExclusiveStartKey?.Count > 0);

			LogLoadingEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, totalCapacity);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
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
		var operationResult = WriteStoreTelemetry.Results.Success;
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var eventsList = events.ToList();
		var correlationId = ExtractCorrelationId(eventsList);
		var messageId = ExtractEventId(eventsList);
		if (eventsList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				operationResult,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		// On the ATOMIC (transactional) path, DynamoDB's TransactWriteItems hard-caps at 100 items and offers
		// no >100 atomic primitive, so an all-or-nothing append (the IEventStore.AppendAsync contract) is
		// impossible beyond 100 events. Reject at the boundary BEFORE any write rather than risk a torn
		// event-stream prefix — callers split into ≤100-event appends. (A torn append is event-stream
		// corruption, which event sourcing must never produce.) The non-transactional opt-out path
		// (UseTransactionalWrite=false) is NOT rejected: the consumer explicitly traded away atomicity for
		// the per-item PutItem path, so >100 is its accepted (documented non-atomic) behavior.
		if (_options.UseTransactionalWrite && eventsList.Count > DynamoTransactItemLimit)
		{
			throw new EventBatchTooLargeException(
				nameof(events),
				eventsList.Count,
				DynamoTransactItemLimit,
				$"DynamoDB atomic append is limited to {DynamoTransactItemLimit} events per call; split the batch into appends of at most {DynamoTransactItemLimit} events, or set UseTransactionalWrite=false to opt into the non-atomic per-item path.");
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventsList.Count, expectedVersion);

		var streamId = BuildStreamId(aggregateType, aggregateId);

		LogAppendingEvents(streamId, aggregateType);

		try
		{
			// Contiguity + concurrency pre-check — match the SQL/InMemory contract (currentVersion must equal
			// expectedVersion). The attribute_not_exists(#pk) condition rejects a STALE expectedVersion
			// (collision on an existing (pk,version)) but NOT a GAP: an expectedVersion beyond the stream tail
			// targets an unused key, so the conditional write would silently succeed and leave a hole in the
			// stream. Re-read the tail (-1 for an empty stream) and reject a non-contiguous expectedVersion
			// before writing. The concurrent-writer race is still caught by the ConditionalCheckFailed paths.
			var precheckVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			if (precheckVersion != expectedVersion)
			{
				operationResult = WriteStoreTelemetry.Results.Conflict;
				LogConcurrencyConflict(streamId, expectedVersion);
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, precheckVersion, 0);
			}

			// Transactional path is guaranteed ≤100 by the guard above, so a single atomic TransactWriteItems
			// covers it. The opt-out path handles any count via the per-item PutItem loop (non-atomic).
			CloudAppendResult appendResult;
			if (_options.UseTransactionalWrite)
			{
				appendResult = await AppendWithTransactionAsync(
						streamId, aggregateId, aggregateType, eventsList, expectedVersion, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				// Transactional writes opted OUT (UseTransactionalWrite=false): honor the choice with the
				// per-item conditional PutItem path (no TransactWriteItems — avoids the 2× WCU and the extra
				// IAM permission a transaction requires).
				appendResult = await AppendSequentiallyAsync(
						streamId, aggregateId, aggregateType, eventsList, expectedVersion, cancellationToken)
					.ConfigureAwait(false);
			}

			if (appendResult.Success)
			{
				_ = (activity?.SetTag(EventSourcingTags.Version, appendResult.NextExpectedVersion));
				activity.SetOperationResult(EventSourcingTagValues.Success);
				operationResult = WriteStoreTelemetry.Results.Success;
			}
			else if (appendResult.IsConcurrencyConflict)
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				operationResult = WriteStoreTelemetry.Results.Conflict;
			}
			else
			{
				activity.SetOperationResult(EventSourcingTagValues.Failure);
				operationResult = WriteStoreTelemetry.Results.Failure;
			}

			return appendResult;
		}
		// Only a provider fault normalizes to a failure result. Cancellation, and any programming error
		// (a null reference, a bad argument), propagates untouched: the caller asked to stop, or the code is
		// wrong. Neither is a store outcome, and neither should be retried by a resilience pipeline.
		catch (AmazonDynamoDBException ex)
		{
			operationResult = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Failed to append events to {AggregateType}/{AggregateId}", aggregateType, aggregateId);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);

			// Liskov (MS-01): report a transient store fault as a failed result — never propagate a raw
			// AWS SDK exception (a leaked provider exception is the substitutability violation). Version
			// conflicts are already returned above; every other fault returns a failure handled uniformly
			// across providers.
			return CloudAppendResult.CreateFailure(ex.Message, requestCharge: 0d);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"append",
				operationResult,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<IChangeFeedSubscription<CloudStoredEvent>> SubscribeToChangesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		// Refused before the store is touched: a change feed that cannot be served should say so without a
		// round-trip, and the refusal must not be mistaken for a connectivity fault.
		var streamsClient = EnsureStreamsClient();

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new DynamoDbEventStoreStreamsSubscription(
			_client,
			streamsClient,
			_options,
			_logger);

		return subscription;
	}

	/// <inheritdoc />
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

		var request = new QueryRequest
		{
			TableName = _options.EventsTableName,
			KeyConditionExpression = $"{_options.PartitionKeyAttribute} = :pk",
			ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new AttributeValue { S = streamId } },
			ScanIndexForward = false, // Descending order
			Limit = 1,
			ProjectionExpression = "version",
			ConsistentRead = true
		};

		var response = await _client.QueryAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Items.Count == 0)
		{
			// AppendAsync prechecks through here, so guarding this answer also guards the write that would
			// otherwise start a second, disjoint history at version 0.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return -1;
		}

		var versionAttr = response.Items[0].GetValueOrDefault("version");
		return versionAttr != null && long.TryParse(versionAttr.N, out var version) ? version : -1;
	}

	#region IEventStore Implementation

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadAsync(aggregateId, aggregateType, partitionKey, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
	async ValueTask<IReadOnlyList<StoredEvent>> IEventStore.LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await LoadFromVersionAsync(aggregateId, aggregateType, partitionKey, fromVersion, null, cancellationToken)
			.ConfigureAwait(false);
		return result.Events.Select(ToStoredEvent).ToList();
	}

	/// <inheritdoc />
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
		var partitionKey = new PartitionKey(BuildStreamId(aggregateType, aggregateId));
		var result = await AppendAsync(aggregateId, aggregateType, partitionKey, events, expectedVersion, cancellationToken)
			.ConfigureAwait(false);

		if (result.Success)
		{
			// DynamoDB has no store-wide global sequence across items/streams; global ordering is
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

	#endregion IEventStore Implementation

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_initLock?.Dispose();
		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Composes the DynamoDB partition key for one stream, with the owning tenant as its leading segment.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the stream's IDENTITY rather than a filter applied to it. A filter would scope
	/// reads while leaving two tenants sharing one item set and one sort-key sequence, so the second tenant
	/// to use an aggregate identifier would be told it has a concurrency conflict on a stream it never
	/// wrote. Composing the key gives each tenant its own partition, its own items, and its own version
	/// sequence, and makes a cross-tenant read unaddressable rather than merely filtered out.
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

	private byte[] SerializeEvent(IDomainEvent evt, string? aggregateId, string? aggregateType)
	{
#pragma warning disable IL2026, IL3050
		return _hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(evt, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _jsonOptions);
#pragma warning restore IL2026, IL3050
	}

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

	private async Task<CloudAppendResult> AppendWithTransactionAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var transactItems = new List<TransactWriteItem>();
		var version = expectedVersion;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			transactItems.Add(new TransactWriteItem
			{
				Put = new Put
				{
					TableName = _options.EventsTableName,
					Item = doc,
					ConditionExpression = "attribute_not_exists(#pk)",
					ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _options.PartitionKeyAttribute }
				}
			});
		}

		try
		{
			var request = new TransactWriteItemsRequest
			{
				TransactItems = transactItems,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			var response = await _client.TransactWriteItemsAsync(request, cancellationToken)
				.ConfigureAwait(false);

			var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;

			LogEventsAppended(streamId, events.Count, totalCapacity);
			return CloudAppendResult.CreateSuccess(version, totalCapacity);
		}
		catch (TransactionCanceledException ex) when (
			ex.CancellationReasons?.Any(static r => r.Code == "ConditionalCheckFailed") == true)
		{
			// Map to a concurrency conflict ONLY when the transaction was cancelled by the version
			// condition (attribute_not_exists(#pk) violated). Other cancellation reasons — throttling,
			// capacity, item-size, TransactionConflict — are NOT version conflicts; let them propagate so a
			// transient/operational failure is not silently misreported as a concurrency conflict.
			LogConcurrencyConflict(streamId, expectedVersion);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, version, 0);
		}
	}

	private async Task<CloudAppendResult> AppendSequentiallyAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		// Non-transactional opt-out path (UseTransactionalWrite=false): a per-item conditional PutItem loop.
		// Each PutItem uses attribute_not_exists(#pk) so a version collision raises ConditionalCheckFailed,
		// mapped to a concurrency conflict. Not atomic across events by design — the caller opted out of
		// transactions; for atomic multi-event appends leave UseTransactionalWrite enabled (the default).
		var version = expectedVersion;
		double totalCapacity = 0;

		foreach (var evt in events)
		{
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, evt, version);

			var request = new PutItemRequest
			{
				TableName = _options.EventsTableName,
				Item = doc,
				ConditionExpression = "attribute_not_exists(#pk)",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = _options.PartitionKeyAttribute },
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
			};

			try
			{
				var response = await _client.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
				totalCapacity += response.ConsumedCapacity?.CapacityUnits ?? 0;
			}
			catch (ConditionalCheckFailedException)
			{
				LogConcurrencyConflict(streamId, expectedVersion);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, version, totalCapacity);
			}
		}

		LogEventsAppended(streamId, events.Count, totalCapacity);
		return CloudAppendResult.CreateSuccess(version, totalCapacity);
	}

	private Dictionary<string, AttributeValue> CreateEventDocument(
		string streamId,
		string aggregateId,
		string aggregateType,
		IDomainEvent evt,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(evt.GetType());

		return new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyAttribute] = new AttributeValue { S = streamId },
			[_options.SortKeyAttribute] = new AttributeValue { N = version.ToString() },
			["eventId"] = new AttributeValue { S = evt.EventId.ToString() },
			["aggregateId"] = new AttributeValue { S = aggregateId },
			["aggregateType"] = new AttributeValue { S = aggregateType },
			["eventType"] = new AttributeValue { S = eventTypeName },
			["version"] = new AttributeValue { N = version.ToString() },
			["timestamp"] = new AttributeValue { S = evt.OccurredAt.ToString("O") },
			["eventData"] = new AttributeValue { S = Convert.ToBase64String(SerializeEvent(evt, aggregateId, aggregateType)) },
#pragma warning disable IL2026, IL3050
			["metadata"] = evt.Metadata != null
				? new AttributeValue { S = Convert.ToBase64String(SerializeMetadata(evt.Metadata)) }
				: new AttributeValue { NULL = true }
#pragma warning restore IL2026, IL3050
		};
	}

	private CloudStoredEvent ToCloudStoredEvent(Dictionary<string, AttributeValue> item)
	{
		return new CloudStoredEvent
		{
			EventId = item["eventId"].S,
			AggregateId = item["aggregateId"].S,
			AggregateType = item["aggregateType"].S,
			EventType = item["eventType"].S,
			Version = long.Parse(item["version"].N),
			Timestamp = DateTimeOffset.Parse(item["timestamp"].S, CultureInfo.InvariantCulture),
			EventData = Convert.FromBase64String(item["eventData"].S),
			Metadata = item.TryGetValue("metadata", out var metaAttr) && !string.IsNullOrEmpty(metaAttr.S)
				? Convert.FromBase64String(metaAttr.S)
				: null,
			PartitionKeyValue = item[_options.PartitionKeyAttribute].S,
			DocumentId = $"{item[_options.PartitionKeyAttribute].S}:{item[_options.SortKeyAttribute].N}"
		};
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
			if (_initialized)
			{
				return;
			}

			if (_options.CreateTableIfNotExists)
			{
				await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <summary>
	/// Refuses when the events table still holds an item written under the untenanted partition-key shape of
	/// an earlier release. Called only through <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides
	/// when it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such an item is unaddressable under the current key shape, and the failure that follows is the worst
	/// one available: a load returns an EMPTY STREAM rather than an error, so the caller sees a new
	/// aggregate, appends at version 0, and ends holding two disjoint histories under one identity. Refusing
	/// converts that silence into a failure while every event is still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted item is a question about the deployment
	/// rather than about the data, so it cannot be decided here; the message states the procedure instead.
	/// </para>
	/// <para>
	/// The partition key is the only place the tenant appears, and DynamoDB has no ordered access across
	/// partitions, so this is one filtered <c>Scan</c> request rather than an index range read. It reads a
	/// single page: a table upgraded in place carries the old shape on EVERY item, so the first page cannot
	/// miss it, and bounding the request keeps a large correctly-keyed table from paying for a full scan at
	/// every cold start. A table that holds both shapes only beyond the first page - which takes a partial
	/// rollback to produce - is not detected here.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The table holds at least one event item whose partition key carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedItemsAsync(CancellationToken cancellationToken)
	{
		ScanResponse response;

		try
		{
			response = await _client.ScanAsync(
				new ScanRequest
				{
					TableName = _options.EventsTableName,
					ProjectionExpression = "#pk",
					FilterExpression = "NOT begins_with(#pk, :prefix)",
					ExpressionAttributeNames = new Dictionary<string, string>
					{
						["#pk"] = _options.PartitionKeyAttribute
					},
					ExpressionAttributeValues = new Dictionary<string, AttributeValue>
					{
						[":prefix"] = new AttributeValue { S = TenantKeyPrefix }
					}
				},
				cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			// The table has not been provisioned, so it holds nothing to refuse. A read against a missing
			// table still fails on its own path, with the error that path already produces.
			return;
		}

		var legacyItem = response.Items?.FirstOrDefault();

		if (legacyItem is null)
		{
			return;
		}

		var legacyKey = legacyItem.TryGetValue(_options.PartitionKeyAttribute, out var partitionKey)
			? partitionKey.S
			: "(unreadable)";

		throw new InvalidOperationException(
			$"Events table '{_options.EventsTableName}' holds at least one event item whose partition key " +
			$"('{legacyKey}') carries no tenant segment, so it was written by a release that stored " +
			$"streams without one. Those items are unaddressable under the current key shape: a load of " +
			$"the aggregate they belong to would return an empty stream, and the caller would then append " +
			$"a second, disjoint history under the same identity. Nothing has been modified. Stop writers, " +
			$"export every event item preserving version order within each stream, re-key each one by " +
			$"prefixing '{TenantKeyPrefix}<tenantId>:' with the tenant that owns the aggregate, re-import, " +
			$"and start the application again.");
	}

	/// <summary>
	/// Verifies, at most once per store instance, that an empty read from the events table means the stream
	/// is genuinely absent rather than merely unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of items, and from nowhere
	/// else. A read that returns items proves the table is addressable and needs no probe; only silence is
	/// ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path, and here that matters more than on any other provider:
	/// the probe is a filtered <c>Scan</c>, so running it at initialisation would spend a scan page on every
	/// process start - on every serverless cold start, forever - to detect a condition that can only hold
	/// across a one-time upgrade. Here it costs nothing at startup, nothing on a read that finds items, and
	/// at most one scan page per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-empty-reads may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra request and nothing else - cheaper than serialising every
	/// empty read behind a lock. The flag is set only once the probe has come back clean, so a table that
	/// holds legacy items refuses every call rather than only the first.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureEmptyReadIsTrustworthyAsync(CancellationToken cancellationToken)
	{
		if (_legacyItemsProbed)
		{
			return;
		}

		await RefuseLegacyUntenantedItemsAsync(cancellationToken).ConfigureAwait(false);
		_legacyItemsProbed = true;
	}

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client.DescribeTableAsync(_options.EventsTableName, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			var createRequest = new CreateTableRequest
			{
				TableName = _options.EventsTableName,
				KeySchema =
				[
					new KeySchemaElement(_options.PartitionKeyAttribute, Amazon.DynamoDBv2.KeyType.HASH),
					new KeySchemaElement(_options.SortKeyAttribute, Amazon.DynamoDBv2.KeyType.RANGE)
				],
				AttributeDefinitions =
				[
					new AttributeDefinition(_options.PartitionKeyAttribute, ScalarAttributeType.S),
					new AttributeDefinition(_options.SortKeyAttribute, ScalarAttributeType.N)
				]
			};

			if (_options.Throughput.UseOnDemandCapacity)
			{
				createRequest.BillingMode = BillingMode.PAY_PER_REQUEST;
			}
			else
			{
				createRequest.BillingMode = BillingMode.PROVISIONED;
				createRequest.ProvisionedThroughput = new ProvisionedThroughput(
					_options.Throughput.ReadCapacityUnits,
					_options.Throughput.WriteCapacityUnits);
			}

			if (_options.EnableStreams)
			{
				createRequest.StreamSpecification = new StreamSpecification
				{
					StreamEnabled = true,
					StreamViewType = Amazon.DynamoDBv2.StreamViewType.NEW_IMAGE
				};
			}

			try
			{
				_ = await _client.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);
			}
			catch (ResourceInUseException)
			{
				// Multi-instance cold-start race: another instance created (or is creating) the table
				// between our DescribeTable and CreateTable. Benign — fall through to wait-for-active.
			}

			// Wait for table to become active
			var describeRequest = new DescribeTableRequest { TableName = _options.EventsTableName };
			TableStatus? status;
			do
			{
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
				var response = await _client.DescribeTableAsync(describeRequest, cancellationToken)
					.ConfigureAwait(false);
				status = response.Table.TableStatus;
			} while (status != TableStatus.ACTIVE);
		}
	}

	/// <summary>
	/// Serializes event metadata, dispatching each value through the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="metadata">The event metadata to serialize.</param>
	/// <returns>The UTF-8 encoded metadata object.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
}
