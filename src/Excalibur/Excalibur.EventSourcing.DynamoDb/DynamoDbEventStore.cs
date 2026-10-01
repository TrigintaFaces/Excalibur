// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
	/// Asks whether the item carrying THIS call's first event identifier is already durably present.
	/// </summary>
	/// <param name="streamId">The stream being appended to.</param>
	/// <param name="expectedVersion">The version this call expected the stream to be at.</param>
	/// <param name="eventList">The events this call attempted to write.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The version the batch reached if our own events are present; otherwise <see langword="null"/>.</returns>
	/// <remarks>
	/// <para>
	/// A failed conditional check says the slot is taken. It does not say by whom, and the two causes need
	/// opposite answers: another writer took our version, or we took it ourselves and lost the
	/// acknowledgement. Both move the stream identically, so classifying from the version reports a
	/// durably committed append as a concurrency conflict -- and the documented remedy for a conflict is
	/// reload-and-retry, which writes the same business event again at the NEXT version, where the
	/// condition cannot catch it. Every replay then applies it twice.
	/// </para>
	/// <para>
	/// The key is deterministic -- partition by stream, sort by version -- so ONE consistent point read
	/// names the slot after the expected version. On the transactional path TransactWriteItems commits
	/// all-or-nothing, so the first event settles the whole batch and the version reached is arithmetic;
	/// THAT ATOMICITY IS LOAD-BEARING FOR CORRECTNESS there. On the per-item opt-out path the consumer has
	/// already traded atomicity away, so this reports only that the FIRST event landed, which is what it
	/// claims and no more.
	/// </para>
	/// </remarks>
	private async Task<long?> ReadCommittedAppendOutcomeAsync(
		string streamId,
		long expectedVersion,
		IReadOnlyList<IDomainEvent> eventList,
		CancellationToken cancellationToken)
	{
		if (eventList.Count == 0)
		{
			return null;
		}

		// WITNESS THE LAST EVENT, NOT THE FIRST.
		//
		// A probe that finds the FIRST item and then reports expectedVersion + count would claim the whole
		// batch from whatever prefix happened to be present -- success over
		// a torn stream, which no later read can distinguish from a shorter history.
		//
		// Items are written in version order, so the LAST one present implies every earlier one is too.
		var ourLastEventId = eventList[^1].EventId;

		// A blank event id is representable, so the probe is not always available. When it is not, fall back
		// to reporting the conflict rather than claim a totality we do not have.
		if (string.IsNullOrWhiteSpace(ourLastEventId))
		{
			return null;
		}

		var request = new GetItemRequest
		{
			TableName = _options.EventsTableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[_options.PartitionKeyAttribute] = new AttributeValue { S = streamId },
				[_options.SortKeyAttribute] = new AttributeValue { N = (expectedVersion + eventList.Count).ToString(CultureInfo.InvariantCulture) }
			},
			ProjectionExpression = "eventId",
			ConsistentRead = true
		};

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item is null || response.Item.Count == 0)
		{
			// Nothing occupies the slot, so nothing of ours landed there.
			return null;
		}

		return response.Item.TryGetValue("eventId", out var occupant)
			&& string.Equals(occupant.S, ourLastEventId, StringComparison.Ordinal)
			? expectedVersion + eventList.Count
			: null;
	}

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

		// Only advertise the change feed when it can actually be served. Without a Streams client the
		// capability is genuinely absent, and the contract asks for null rather than an instance that
		// throws on first use.
		if (serviceType == typeof(ICloudNativeEventStoreChangeFeed) && _streamsClient is null)
		{
			return null;
		}

		return serviceType.IsInstanceOfType(this) ? this : null;
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

		// DynamoDB's TransactWriteItems hard-caps at 100 items and offers no larger atomic primitive, so
		// an all-or-nothing append beyond 100 events is impossible on this provider. It is REFUSED here,
		// before any write.
		//
		// There is deliberately no opt-out. A flag permitting a >100 append by putting items one at a
		// time buys throughput with silent, permanent corruption: a failure partway leaves a prefix with
		// no suffix, and no later read can distinguish that from a stream never written further. The
		// caller splits instead, and the exception carries both numbers so the split is mechanical.
		if (eventsList.Count > DynamoTransactItemLimit)
		{
			throw new EventBatchTooLargeException(
				nameof(events),
				eventsList.Count,
				DynamoTransactItemLimit,
				$"DynamoDB atomic append is limited to {DynamoTransactItemLimit} events per call; split the batch into appends of at most {DynamoTransactItemLimit} events.");
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
				// ASK WHETHER OUR OWN EVENTS LANDED, BEFORE CLASSIFYING ANYTHING. A retry of an append whose
				// acknowledgement was lost arrives here FIRST: our own committed write is what moved the version.
				var precheckCommitted = await ReadCommittedAppendOutcomeAsync(
					streamId, expectedVersion, eventsList, cancellationToken).ConfigureAwait(false);

				if (precheckCommitted is { } precheckLanded)
				{
					activity.SetOperationResult(EventSourcingTagValues.Success);

					// RECOGNISED, not written by this call. Reporting plain success here would be true about
					// the append and silently false about the call: these items are durable, but they are a
					// prior attempt's, and anything may have happened to them since — an erasure included.
					return CloudAppendResult.CreateAlreadyCommitted(precheckLanded, 0);
				}

				operationResult = WriteStoreTelemetry.Results.Conflict;
				LogConcurrencyConflict(streamId, expectedVersion);
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, precheckVersion, 0);
			}

			// Transactional path is guaranteed ≤100 by the guard above, so a single atomic TransactWriteItems
			// covers it. The opt-out path handles any count via the per-item PutItem loop (non-atomic).
			// A SINGLE event is one conditional PutItem, which is atomic by itself. Routing it through
			// TransactWriteItems would double the write cost and demand the extra IAM permission a
			// transaction requires, and buy nothing: there is no second item to be atomic with.
			//
			// That cost saving is why the removed opt-out existed. It is kept here, where it is honest,
			// and not extended to multi-event appends, where it traded atomicity for throughput.
			CloudAppendResult appendResult;
			if (eventsList.Count > 1)
			{
				appendResult = await AppendWithTransactionAsync(
						streamId, aggregateId, aggregateType, partitionKey, eventsList, expectedVersion, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				appendResult = await AppendSequentiallyAsync(
						streamId, aggregateId, aggregateType, partitionKey, eventsList, expectedVersion, cancellationToken)
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
			return CloudAppendResult.CreateFailure(ex.Message, requestCharge: 0d, ClassifyAppendFailure(ex));
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
			//
			// CARRY THE OUTCOME ACROSS, do not flatten it to success. This hop is the only place the
			// recognised-retry state could be lost, and a caller that must not republish from live payloads
			// reads it on the far side.
			return result.Outcome == CloudAppendOutcome.AlreadyCommitted
				? AppendResult.CreateAlreadyCommitted(result.NextExpectedVersion!.Value, firstEventPosition: null)
				: AppendResult.CreateSuccess(result.NextExpectedVersion!.Value, firstEventPosition: null);
		}

		if (result.IsConcurrencyConflict)
		{
			// A concurrency conflict states the version it MEASURED, or nothing. Carry the null across
			// rather than dereferencing: the store may have detected the conflict without reading a
			// version, and a caller that gets null reloads instead of trusting a number nobody took.
			return AppendResult.CreateConcurrencyConflict(expectedVersion, result.NextExpectedVersion);
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
		Excalibur.Data.TenantScopedKey.Compose(
			TenantScope.FromContext(_tenantContext).TenantId, aggregateType, aggregateId);

	/// <summary>
	/// Classifies an append fault by its AWS exception type: DynamoDB's own throttling/availability
	/// exceptions are transient, everything else is treated as permanent.
	/// </summary>
	/// <remarks>
	/// Type-matched rather than status-code-matched: DynamoDB throttling returns HTTP 400 for
	/// <see cref="ProvisionedThroughputExceededException"/> and <see cref="ThrottlingException"/> alike --
	/// the SAME status a genuine bad request returns -- so <see cref="Amazon.Runtime.AmazonServiceException.StatusCode"/>
	/// cannot discriminate them the way Cosmos's status code can.
	/// </remarks>
	private static MessageFailureKind ClassifyAppendFailure(AmazonDynamoDBException exception) =>
		exception switch
		{
			ProvisionedThroughputExceededException => MessageFailureKind.Transient,
			ThrottlingException => MessageFailureKind.Transient,
			RequestLimitExceededException => MessageFailureKind.Transient,
			InternalServerErrorException => MessageFailureKind.Transient,
			_ => MessageFailureKind.Permanent,
		};

	private static string? ExtractCorrelationId(IEnumerable<IDomainEvent> events)
	{
		// Delegates to IDomainEvent.CorrelationId (checks OutboxHeaderNames.CorrelationId, the
		// framework declared key, then the legacy PascalCase/camelCase spellings) rather than
		// re-implementing the key-priority chain here.
		foreach (var @event in events)
		{
			if (@event.CorrelationId is { } correlationId)
			{
				return correlationId;
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

	/// <summary>
	/// Builds the deterministic idempotency token for one append attempt.
	/// </summary>
	/// <param name="streamId">The stream being appended to.</param>
	/// <param name="expectedVersion">The version this call expects the stream to be at.</param>
	/// <param name="events">The events this call is writing.</param>
	/// <returns>A stable 36-character token, or <see langword="null"/> when no stable identity is available.</returns>
	/// <remarks>
	/// Returns <see langword="null"/> when the first event carries no id, because a token that is not
	/// stable across retries is worse than none: it would claim idempotency the call does not have.
	/// </remarks>
	private static string? BuildIdempotencyToken(string streamId, long expectedVersion, IReadOnlyList<IDomainEvent> events)
	{
		if (events.Count == 0 || string.IsNullOrWhiteSpace(events[0].EventId))
		{
			return null;
		}

		var seed = string.Create(
			CultureInfo.InvariantCulture,
			$"{streamId}|{expectedVersion}|{events[0].EventId}");

		var digest = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

		return new Guid(digest.AsSpan(0, 16)).ToString();
	}

	private async Task<CloudAppendResult> AppendWithTransactionAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		var transactItems = new List<TransactWriteItem>();
		var version = expectedVersion;

		foreach (var named in events.AsNamedEvents())
		{
			var evt = named.Event;
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, named, version);

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
			// IDEMPOTENCY TOKEN -- prevent the ambiguity rather than resolve it afterwards.
			//
			// Without a ClientRequestToken, an SDK retry of a TransactWriteItems whose RESPONSE was lost
			// re-executes the whole transaction. The conditional checks then fail against rows the FIRST
			// attempt already committed, and the append is reported as a concurrency conflict even though it
			// succeeded. With the token, DynamoDB treats the retry as the same call within its idempotency
			// window and returns the original outcome. This is the provider's own mechanism for the hazard,
			// so it is used in preference to inferring the answer from a later read.
			//
			// The token is DERIVED, not random: a random one would be different on every retry and therefore
			// idempotent with nothing. It is a deterministic digest of the stream, the expected version and
			// this call's first event id, so the same append always presents the same token while two genuinely
			// different appends never collide. Formatted as a GUID because the field accepts 1-36 characters
			// and a consumer-supplied event id has no length guarantee.
			var request = new TransactWriteItemsRequest
			{
				TransactItems = transactItems,
				ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL,
				ClientRequestToken = BuildIdempotencyToken(streamId, expectedVersion, events)
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
			// Report the stream's ACTUAL committed version, not the version this writer wanted. The
			// caller reloads against the number in this result, so handing it back its own intended
			// version tells it to retry from a point that does not exist. Every other provider re-reads;
			// this matches them.
			var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			LogConcurrencyConflict(streamId, expectedVersion);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion, 0);
		}
	}

	private async Task<CloudAppendResult> AppendSequentiallyAsync(
		string streamId,
		string aggregateId,
		string aggregateType,
		IPartitionKey partitionKey,
		List<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		// THE SINGLE-EVENT PATH: one conditional PutItem, which is atomic by itself.
		//
		// attribute_not_exists(#pk) makes a version collision raise ConditionalCheckFailed, mapped to a
		// concurrency conflict. Multi-event appends do NOT come here -- they go through TransactWriteItems,
		// because writing them one at a time can leave a torn prefix. Routing a single event through a
		// transaction would double its write cost and demand the extra IAM permission for no benefit:
		// there is no second item to be atomic with.
		var version = expectedVersion;
		double totalCapacity = 0;

		foreach (var named in events.AsNamedEvents())
		{
			var evt = named.Event;
			version++;
			var doc = CreateEventDocument(streamId, aggregateId, aggregateType, named, version);

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
				// The stream's ACTUAL committed version, as above. On this path the events written before
				// the collision are already durable -- the caller opted out of atomicity -- so the tail is
				// whatever the winner and this partial run left, which only a read can answer.
				var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
					.ConfigureAwait(false);
				LogConcurrencyConflict(streamId, expectedVersion);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion, totalCapacity);
			}
		}

		LogEventsAppended(streamId, events.Count, totalCapacity);
		return CloudAppendResult.CreateSuccess(version, totalCapacity);
	}

	private Dictionary<string, AttributeValue> CreateEventDocument(
		string streamId,
		string aggregateId,
		string aggregateType,
		NamedEvent named,
		long version)
	{
		var (evt, eventTypeName) = named;

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
