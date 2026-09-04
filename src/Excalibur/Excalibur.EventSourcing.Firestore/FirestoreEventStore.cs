// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Firestore;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.EventSourcing.Observability;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of the event store.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Event store implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestoreEventStore : ICloudNativeEventStore, ICloudNativeProviderInfo,
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
	/// range lacks the prefix. Expressing the probe as two range reads keeps it served by the automatic
	/// single-field index on <c>streamId</c> rather than by a collection scan.
	/// </remarks>
	private const string TenantKeyPrefixUpperBound = "t;";

	private readonly FirestoreEventStoreOptions _options;
	private readonly ILogger<FirestoreEventStore> _logger;
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
	private FirestoreDb? _db;
	private volatile bool _initialized;

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs on the first read that comes back empty,
	// which is the first moment an unaddressable document could be mistaken for an absent one. (The
	// injection constructor sets _initialized in its body anyway, so initialisation is not a path here.)
	private volatile bool _legacyDocumentsProbed;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStore" /> class.
	/// </summary>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public FirestoreEventStore(
		IOptions<FirestoreEventStoreOptions> options,
		ILogger<FirestoreEventStore> logger,
		ITenantContext tenantContext)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStore" /> class with an existing database.
	/// </summary>
	/// <param name="db"> The Firestore database. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	// Both public constructors accept an ITenantContext, so DI construction would otherwise depend on which
	// of the OTHER parameters happen to be registered. This one is the injection constructor: the shipped
	// registration always registers a FirestoreDb.
	[ActivatorUtilitiesConstructor]
	public FirestoreEventStore(
		FirestoreDb db,
		IOptions<FirestoreEventStoreOptions> options,
		ILogger<FirestoreEventStore> logger,
		ITenantContext tenantContext)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
		_initialized = true;
	}

	/// <inheritdoc />
	public CloudPersistenceProviderType CloudProvider => CloudPersistenceProviderType.Firestore;

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
			return this;
		}

		if (serviceType == typeof(ICloudNativeEventStoreInfo))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Gets the Firestore database instance.
	/// </summary>
	internal FirestoreDb? Database => _db;

	/// <summary>
	/// Initializes the Firestore client.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
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

			LogInitializing(_options.EventsCollectionName);
			_options.Validate();

			_db = await CreateDatabaseAsync().ConfigureAwait(false);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
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

		var streamId = BuildStreamId(aggregateType, aggregateId);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType);

		try
		{
			var events = new List<CloudStoredEvent>();
			var query = _db!.Collection(_options.EventsCollectionName)
				.WhereEqualTo("streamId", streamId)
				.OrderBy("version");

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
				var cloudEvent = ToCloudStoredEvent(doc);
				events.Add(cloudEvent);
			}

			if (events.Count == 0)
			{
				// An empty result is the ambiguous one: either this aggregate was never written, or it was
				// written under the untenanted key shape and is unaddressable now. Refuse before reporting
				// emptiness to a caller who would read it as a new aggregate.
				await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			}

			LogLoadedEvents(streamId, events.Count);

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, 0, null);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load");
			_logger.LogError(ex, "Error loading events for stream {StreamId}", streamId);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
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

		var streamId = BuildStreamId(aggregateType, aggregateId);

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var events = new List<CloudStoredEvent>();
			var query = _db!.Collection(_options.EventsCollectionName)
				.WhereEqualTo("streamId", streamId)
				.WhereGreaterThan("version", fromVersion)
				.OrderBy("version");

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
				var cloudEvent = ToCloudStoredEvent(doc);
				events.Add(cloudEvent);
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return new CloudEventLoadResult(events, 0, null);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load_from_version");
			_logger.LogError(ex, "Error loading events from version for stream {StreamId}", streamId);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"load_from_version",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// An append commits as a single Firestore transaction, so it is all-or-nothing. Firestore caps a
	/// transaction at 500 operations and offers no larger atomic primitive, so an append of more events
	/// than <see cref="FirestoreEventStoreOptions.MaxBatchSize"/> (500 by default, and never more) is
	/// rejected with <see cref="EventBatchTooLargeException"/> before anything is written, rather than
	/// partially committed. Callers split the append into batches of at most that many events.
	/// </remarks>
	/// <exception cref="EventBatchTooLargeException">
	/// <paramref name="events"/> contains more events than this store can append atomically.
	/// </exception>
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
				WriteStoreTelemetry.Providers.Firestore,
				"append",
				operationResult,
				stopwatch.Elapsed);
			return CloudAppendResult.CreateSuccess(expectedVersion, 0);
		}

		// Every event in an append is written inside ONE Firestore transaction, and Firestore hard-caps a
		// transaction (and a batched write) at 500 operations, offering no larger atomic primitive. So an
		// all-or-nothing append -- the IEventStore.AppendAsync contract -- is impossible beyond that limit.
		// Reject at the boundary BEFORE the transaction opens rather than let the driver reject it midway:
		// the driver's rejection is a permanent condition that no retry can clear, but it arrives as an
		// exception this store would flatten into CreateFailure, which the contract reserves for faults that
		// COULD succeed on retry. (A torn append is event-stream corruption, which event sourcing must never
		// produce, and a consumer holding a torn stream cannot detect it: it has a prefix and no suffix, and
		// every later read is consistent with a shorter history.) Unlike the other cloud providers this store
		// offers no non-atomic opt-out path, so the guard is unconditional. MaxBatchSize lets a host lower the
		// limit below 500, which Firestore's own guidance calls for when documents or their index entries are
		// large enough to approach the 10 MiB per-transaction size cap.
		if (eventsList.Count > _options.MaxBatchSize)
		{
			throw new EventBatchTooLargeException(
				nameof(events),
				eventsList.Count,
				_options.MaxBatchSize,
				$"Firestore atomic append is limited to {_options.MaxBatchSize} events per call; split the batch into appends of at most {_options.MaxBatchSize} events.");
		}

		var streamId = BuildStreamId(aggregateType, aggregateId);
		LogAppendingEvents(streamId, eventsList.Count);

		if (expectedVersion < 0)
		{
			// Appending at the head of a stream asserts the stream does not exist. Unlike the other
			// providers this store proves that inside the transaction by keyed reads rather than through
			// GetCurrentVersionAsync, so the assertion has to be guarded here, or an unmigrated collection
			// would start a second, disjoint history at version 0 without any read having come back empty.
			//
			// Outside the try deliberately: the general catch below flattens an exception into a failed
			// result, and this refusal must reach the caller as a refusal.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventsList.Count, expectedVersion);

		try
		{
			// Use transaction for optimistic concurrency
			var newVersion = expectedVersion;
			var conflictDetected = false;
			var currentActualVersion = expectedVersion;

			await _db!.RunTransactionAsync(async transaction =>
			{
				// Reset per attempt: Firestore re-runs this callback when the transaction conflicts, and a
				// verdict left over from a failed attempt would outlive the attempt that produced it,
				// reporting a conflict the retry disproved -- so a successful write looks like a failure and
				// the caller retries work that already landed.
				conflictDetected = false;

				// Optimistic-concurrency check by KEYED READS, never a transactional query.
				//
				// A transactional query locks the index range it scans, not merely the documents it
				// returns. Because this query filtered on streamId and ordered by version, concurrent
				// appends to DIFFERENT streams took overlapping range locks and aborted each other with
				// "Transaction lock timeout" — 7 of 10 concurrent appends to distinct aggregates failed,
				// even though distinct aggregates share no stream and cannot genuinely contend.
				//
				// Document ids are deterministic ("{streamId}:{version}"), so the same check is expressible
				// as point reads, which lock exactly the documents named and nothing else:
				//   - the slot AFTER the expected version must be empty (nobody appended past us), and
				//   - the expected version itself must exist (the stream really is where we think it is).
				// The actual version for the conflict report is resolved outside the transaction, the same
				// way the AlreadyExists path below already does it.
				var nextRef = _db!.Collection(_options.EventsCollectionName)
					.Document($"{streamId}:{expectedVersion + 1}");
				var nextSnapshot = await transaction.GetSnapshotAsync(nextRef, cancellationToken)
					.ConfigureAwait(false);

				if (nextSnapshot.Exists)
				{
					conflictDetected = true;
					return;
				}

				if (expectedVersion >= 0)
				{
					var expectedRef = _db!.Collection(_options.EventsCollectionName)
						.Document($"{streamId}:{expectedVersion}");
					var expectedSnapshot = await transaction.GetSnapshotAsync(expectedRef, cancellationToken)
						.ConfigureAwait(false);

					if (!expectedSnapshot.Exists)
					{
						conflictDetected = true;
						return;
					}
				}

				// Append events
				var version = expectedVersion;
				foreach (var (evt, eventTypeName) in eventsList.AsNamedEvents())
				{
					version++;
					var docId = $"{streamId}:{version}";
					var docRef = _db!.Collection(_options.EventsCollectionName).Document(docId);

#pragma warning disable IL2026, IL3050
					var data = new Dictionary<string, object>
					{
						["eventId"] = evt.EventId.ToString(),
						["streamId"] = streamId,
						["aggregateId"] = aggregateId,
						["aggregateType"] = aggregateType,
						["eventType"] = eventTypeName,
						["version"] = version,
						["timestamp"] = evt.OccurredAt.ToString("O"),
						["eventData"] = Convert.ToBase64String(SerializeEvent(evt, aggregateId, aggregateType))
					};

					if (evt.Metadata != null)
					{
						data["metadata"] = Convert.ToBase64String(SerializeMetadata(evt.Metadata));
					}
#pragma warning restore IL2026, IL3050

					transaction.Create(docRef, data);
				}

				newVersion = version;
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (conflictDetected)
			{
				LogConcurrencyConflict(streamId, $"Expected version {expectedVersion}");
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				operationResult = WriteStoreTelemetry.Results.Conflict;

				// Read outside the transaction: the keyed checks above prove a conflict exists but do not
				// reveal how far ahead the stream is, and asking inside would reintroduce the range lock.
				currentActualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
					.ConfigureAwait(false);
				return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentActualVersion, 0);
			}

			_ = (activity?.SetTag(EventSourcingTags.Version, newVersion));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			operationResult = WriteStoreTelemetry.Results.Success;
			return CloudAppendResult.CreateSuccess(newVersion, 0);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			LogConcurrencyConflict(streamId, "Document already exists");
			operationResult = WriteStoreTelemetry.Results.Conflict;
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, partitionKey, cancellationToken)
				.ConfigureAwait(false);
			return CloudAppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion, 0);
		}
		// NARROW BY DESIGN. This catches only the faults the provider's own driver raises, which is a
		// closed set the driver defines -- not every exception, filtered down to those that must escape.
		// The difference matters the first time something new appears: an exclusion list is wrong by
		// default and silently converts the newcomer into an ordinary append failure, which is how a
		// cancelled append came to be reported as a store fault and retried inside a cancelled scope.
		// Everything else -- cancellation, an event type the configured resolver does not declare, a
		// programming error -- propagates, because a returned failure means "this could succeed if you
		// try again" and none of those can.
		catch (RpcException ex)
		{
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			operationResult = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
				"append",
				messageId,
				correlationId);
			_logger.LogError(ex, "Error appending events to stream {StreamId}", streamId);
			return CloudAppendResult.CreateFailure(ex.Message, 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.Firestore,
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
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new FirestoreEventStoreListenerSubscription(
			_db!,
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

		var query = _db!.Collection(_options.EventsCollectionName)
			.WhereEqualTo("streamId", streamId)
			.OrderByDescending("version")
			.Limit(1);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (snapshot.Count == 0)
		{
			// Reached from the conflict-reporting paths of AppendAsync, and by callers deciding whether an
			// aggregate exists at all.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return -1;
		}

		return snapshot.Documents[0].GetValue<long>("version");
	}

	#region IEventStore Explicit Implementation

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
			// Firestore has no store-wide global sequence across documents/streams; global ordering is
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

	#endregion IEventStore Explicit Implementation

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
	/// Composes the stream identifier, with the owning tenant as its leading segment. Every Firestore
	/// document id this store writes is this value plus the event's version.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the stream's IDENTITY rather than a filter applied to it. A filter would scope
	/// reads while leaving two tenants sharing one document set and one version sequence, so the second
	/// tenant to use an aggregate identifier would be told it has a concurrency conflict on a stream it
	/// never wrote. Composing the key gives each tenant its own documents and its own version sequence, and
	/// makes a cross-tenant read unaddressable rather than merely filtered out.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default, and a genuinely untenanted row resolves the reserved untenanted sentinel. So
	/// every key carries a tenant segment and none can be produced without one.
	/// </para>
	/// <para>
	/// The constant leading segment also keeps the composed document id clear of Firestore's reserved id
	/// shape (an id may not match <c>__.*__</c>), which the untenanted sentinel would otherwise sit inside.
	/// </para>
	/// </remarks>
	private string BuildStreamId(string aggregateType, string aggregateId)
		=> $"{TenantKeyPrefix}{TenantScope.FromContext(_tenantContext).TenantId}:{aggregateType}:{aggregateId}";

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

	private static CloudStoredEvent ToCloudStoredEvent(DocumentSnapshot doc)
	{
		var streamId = doc.GetValue<string>("streamId");

		return new CloudStoredEvent
		{
			EventId = doc.GetValue<string>("eventId"),
			AggregateId = doc.GetValue<string>("aggregateId"),
			AggregateType = doc.GetValue<string>("aggregateType"),
			EventType = doc.GetValue<string>("eventType"),
			Version = doc.GetValue<long>("version"),
			Timestamp = DateTimeOffset.Parse(doc.GetValue<string>("timestamp"), CultureInfo.InvariantCulture),
			EventData = Convert.FromBase64String(doc.GetValue<string>("eventData")),
			Metadata = doc.ContainsField("metadata") && doc.GetValue<string?>("metadata") != null
				? Convert.FromBase64String(doc.GetValue<string>("metadata"))
				: null,
			PartitionKeyValue = streamId,
			DocumentId = doc.Id
		};
	}

	private async Task<FirestoreDb> CreateDatabaseAsync()
	{
		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			// Point this client at the emulator directly. The process-wide FIRESTORE_EMULATOR_HOST
			// variable is first-write-wins, so routing through it lets a second store silently talk to
			// another store's emulator. Endpoint and EmulatorDetection.EmulatorOnly are mutually
			// exclusive -- setting both throws -- so an explicit endpoint with insecure credentials is
			// the combination that reaches an emulator per instance.
			return await new FirestoreDbBuilder
			{
				ProjectId = _options.ProjectId ?? "test-project",
				Endpoint = _options.EmulatorHost,
				ChannelCredentials = ChannelCredentials.Insecure,
			}.BuildAsync().ConfigureAwait(false);
		}

		FirestoreDbBuilder builder;

		if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
		{
#pragma warning disable CS0618 // Obsolete CredentialsPath/JsonCredentials -- no replacement available yet
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, JsonCredentials = _options.CredentialsJson };
#pragma warning restore CS0618
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
		{
#pragma warning disable CS0618
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, CredentialsPath = _options.CredentialsPath };
#pragma warning restore CS0618
		}
		else
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };
		}

		return await builder.BuildAsync().ConfigureAwait(false);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_db == null)
		{
			throw new InvalidOperationException("Firestore event store has not been initialized.");
		}
	}

	/// <summary>
	/// Verifies, at most once per store instance, that an empty read from the events collection means the
	/// stream is genuinely absent rather than merely unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of documents, and from
	/// nowhere else. A read that returns documents proves the collection is addressable and needs no probe;
	/// only silence is ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path, nor on the entry to every operation. Probing there would
	/// spend two range reads on every process start - on every serverless cold start, forever - to detect a
	/// condition that can only hold across a one-time upgrade, and would make the store unusable without a
	/// live collection even for operations that never read one. Here it costs nothing at startup, nothing on
	/// a read that finds documents, and at most one probe per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-empty-reads may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra pair of range reads and nothing else - cheaper than
	/// serialising every empty read behind a lock. The flag is set only once the probe has come back clean,
	/// so a collection that holds legacy documents refuses every call rather than only the first.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureEmptyReadIsTrustworthyAsync(CancellationToken cancellationToken)
	{
		if (_legacyDocumentsProbed)
		{
			return;
		}

		await RefuseLegacyUntenantedDocumentsAsync(cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	/// <summary>
	/// Refuses when the events collection still holds a document written under the untenanted stream
	/// identifier of an earlier release. Called only through
	/// <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides when it runs.
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
	/// <para>
	/// Two range reads rather than one, because Firestore has no negated prefix operator: the identifiers
	/// that lack the prefix are exactly those sorting below <see cref="TenantKeyPrefix"/> and those sorting
	/// at or above <see cref="TenantKeyPrefixUpperBound"/>. Each is limited to a single document.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one event document whose stream identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(CancellationToken cancellationToken)
	{
		var collection = _db!.Collection(_options.EventsCollectionName);

		Query[] probes =
		[
			collection.WhereLessThan("streamId", TenantKeyPrefix).Limit(1),
			collection.WhereGreaterThanOrEqualTo("streamId", TenantKeyPrefixUpperBound).Limit(1)
		];

		foreach (var probe in probes)
		{
			var snapshot = await probe.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			if (snapshot.Count == 0)
			{
				continue;
			}

			var legacyStreamId = snapshot.Documents[0].TryGetValue<string>("streamId", out var value)
				? value
				: "(unreadable)";

			throw new InvalidOperationException(
				$"Events collection '{_options.EventsCollectionName}' holds at least one event document " +
				$"whose stream identifier ('{legacyStreamId}') carries no tenant segment, so it was " +
				$"written by a release that stored streams without one. Those documents are unaddressable " +
				$"under the current key shape: a load of the aggregate they belong to would return an " +
				$"empty stream, and the caller would then append a second, disjoint history under the " +
				$"same identity. Nothing has been modified. Stop writers, export every event document " +
				$"preserving version order within each stream, re-key each one by prefixing " +
				$"'{TenantKeyPrefix}<tenantId>:' with the tenant that owns the aggregate, re-import, and " +
				$"start the application again.");
		}
	}

	/// <summary>
	/// Serializes a domain event, resolving its type metadata from the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <param name="evt">The domain event to serialize.</param>
	/// <param name="aggregateId">The stream the append targets, reported if the type is undeclared.</param>
	/// <param name="aggregateType">The aggregate type the append targets, reported if undeclared.</param>
	/// <returns>The UTF-8 encoded event payload.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEvent(IDomainEvent evt, string? aggregateId, string? aggregateType) =>
		_hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(evt, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _jsonOptions);

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
