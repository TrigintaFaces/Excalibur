// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Data.Observability;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.EventSourcing.MongoDB;

/// <summary>
/// MongoDB implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Provides atomic event appends with optimistic concurrency control using MongoDB's UNIQUE index.
/// A UNIQUE compound index on (streamId, aggregateType, version) enforces version uniqueness.
/// </para>
/// <para>
/// When a version conflict occurs, MongoDB returns error code 11000 (duplicate key),
/// which is caught and translated to a concurrency conflict result.
/// </para>
/// <para>
/// Global ordering is provided via an atomic counter document using FindOneAndUpdate.
/// </para>
/// <para>
/// Supports pluggable serialization via <see cref="IPayloadSerializer"/> for event payloads,
/// with backward compatibility for existing JSON-serialized events.
/// </para>
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Event store implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class MongoDbEventStore : IEventStore, IEventStoreErasure, IAsyncDisposable
{
	// MongoDB error code for duplicate key (unique constraint violation)
	private const int DuplicateKeyErrorCode = 11000;

	// Counter document ID for global sequence
	private const string GlobalSequenceCounterId = "global_sequence";

	// Format markers for envelope detection
	private const byte EnvelopeFormatMarker = 0x01;

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
	/// range lacks the prefix. Expressing the probe as a range lets the existing
	/// <c>(streamId, aggregateType, version)</c> index serve it, instead of a collection scan.
	/// </remarks>
	private const string TenantKeyPrefixUpperBound = "t;";

	// Set only once the legacy-document probe has come back clean. Separate from _initialized because the
	// probe is deliberately NOT on the initialisation path: it runs on the first read that comes back empty,
	// which is the first moment an unaddressable document could be mistaken for an absent one.
	private volatile bool _legacyDocumentsProbed;

	private readonly MongoDbEventStoreOptions _options;
	private readonly ILogger<MongoDbEventStore> _logger;
	private readonly ITenantContext _tenantContext;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;
	private readonly ISerializer? _internalSerializer;
	private readonly IPayloadSerializer? _payloadSerializer;
	private readonly bool _ownsClient;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbEventDocument>? _eventsCollection;
	private IMongoCollection<MongoDbCounterDocument>? _countersCollection;
	// Serialises first-time initialisation. Without it two concurrent first callers race:
	// one assigns the client and is still assigning the collection when the other observes a
	// non-null client, skips the whole block, and dereferences a collection that is still null.
	// That is a NullReferenceException a few instructions wide, so it is intermittent and
	// load-dependent -- it was observed in CI on two different stores in a single run.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: the fast path reads this outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class.
	/// </summary>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public MongoDbEventStore(
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger,
		ITenantContext tenantContext)
		: this(options, logger, tenantContext, null, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class with optional serializers.
	/// </summary>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	public MongoDbEventStore(
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger,
		ITenantContext tenantContext,
		ISerializer? internalSerializer,
		IPayloadSerializer? payloadSerializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions events by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="internalSerializer">Optional internal serializer for envelope support.</param>
	/// <param name="payloadSerializer">Optional pluggable serializer for event payloads.</param>
	// Three public constructors accept an ITenantContext, so DI construction would otherwise depend on which
	// of the OTHER parameters happen to be registered. This one is the injection constructor: the shipped
	// registration always registers an IMongoClient.
	[Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
	public MongoDbEventStore(
		IMongoClient client,
		IOptions<MongoDbEventStoreOptions> options,
		ILogger<MongoDbEventStore> logger,
		ITenantContext tenantContext,
		ISerializer? internalSerializer = null,
		IPayloadSerializer? payloadSerializer = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;
		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
		_internalSerializer = internalSerializer;
		_payloadSerializer = payloadSerializer;
		_database = client.GetDatabase(_options.DatabaseName);
		_eventsCollection = _database.GetCollection<MongoDbEventDocument>(_options.CollectionName);
		_countersCollection = _database.GetCollection<MongoDbCounterDocument>(_options.CounterCollectionName);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		return await LoadAsync(aggregateId, aggregateType, -1, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			var filterBuilder = Builders<MongoDbEventDocument>.Filter;
			var filter = filterBuilder.And(
				filterBuilder.Eq(d => d.StreamId, BuildStreamId(aggregateId)),
				filterBuilder.Eq(d => d.AggregateType, aggregateType));

			if (fromVersion >= 0)
			{
				filter = filterBuilder.And(filter, filterBuilder.Gt(d => d.Version, fromVersion));
			}

			var sort = Builders<MongoDbEventDocument>.Sort.Ascending(d => d.Version);

			var documents = await _eventsCollection!
				.Find(filter)
				.Sort(sort)
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false);

			var loadedEvents = documents.Select(d => d.ToStoredEvent(aggregateId)).ToList();

			if (loadedEvents.Count == 0 && fromVersion < 0)
			{
				// An empty WHOLE-stream read is the ambiguous one: either this aggregate was never written,
				// or it was written under the untenanted key shape and is unaddressable now. Refuse before
				// reporting emptiness to a caller who would read it as a new aggregate.
				//
				// A read from a version is not guarded: the caller already holds state for this stream, so
				// it is not deciding the aggregate is new, and its subsequent append is guarded by the
				// version precheck below.
				await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			}

			_ = (activity?.SetTag(EventSourcingTags.EventCount, loadedEvents.Count));
			activity.SetOperationResult(EventSourcingTagValues.Success);

			return loadedEvents;
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			activity.RecordException(ex);
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(events);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var eventList = events.ToList();
		var correlationId = ExtractCorrelationId(eventList);
		var messageId = ExtractEventId(eventList);
		if (eventList.Count == 0)
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				result,
				stopwatch.Elapsed);
			return AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null);
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		try
		{
			await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

			// Check current version first for optimistic concurrency
			var currentVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			if (currentVersion != expectedVersion)
			{
				activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
				result = WriteStoreTelemetry.Results.Conflict;
				return AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion);
			}

			// Reserve a contiguous block of global-sequence numbers with a SINGLE atomic Inc, rather than
			// one FindOneAndUpdate round-trip per event. The block is [firstGlobalSequence ..
			// firstGlobalSequence + count - 1], assigned to events in order.
			var documents = new List<MongoDbEventDocument>(eventList.Count);
			var version = currentVersion;
			var firstGlobalSequence = await ReserveGlobalSequenceBlockAsync(eventList.Count, cancellationToken)
				.ConfigureAwait(false);
			var firstPosition = firstGlobalSequence;
			var globalSequence = firstGlobalSequence;

			foreach (var @event in eventList)
			{
				version++;
				var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

				var eventData = SerializeEventWithEnvelopeSupport(@event, aggregateId, aggregateType, version);
				var metadata = @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null;

				documents.Add(new MongoDbEventDocument
				{
					EventId = @event.EventId,
					StreamId = BuildStreamId(aggregateId),
					AggregateType = aggregateType,
					EventType = eventTypeName,
					Payload = eventData,
					Metadata = metadata,
					Version = version,
					OccurredAt = @event.OccurredAt,
					GlobalSequence = globalSequence
				});

				globalSequence++;
			}

			// A multi-event append must be atomic: an ordered InsertMany that fails mid-batch on a
			// non-concurrency error (oversized document, transient network fault after document k)
			// would otherwise leave a torn prefix of committed events. Wrap the batch in a client
			// session + transaction so it commits all-or-nothing. A single-event append is atomic on
			// its own (single-document write), so it uses a plain insert and does not require a
			// transaction-capable (replica-set) deployment.
			if (documents.Count == 1)
			{
				await _eventsCollection!.InsertOneAsync(
					documents[0],
					options: null,
					cancellationToken).ConfigureAwait(false);
			}
			else
			{
				using var session = await _client!.StartSessionAsync(cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				_ = await session.WithTransactionAsync(
					async (s, ct) =>
					{
						// Ordered insert - stops at first failure; the transaction rolls back any
						// prior inserts in the batch so no partial prefix survives.
						await _eventsCollection!.InsertManyAsync(
							s,
							documents,
							new InsertManyOptions { IsOrdered = true },
							ct).ConfigureAwait(false);
						return true;
					},
					cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			LogEventsAppended(eventList.Count, aggregateType, aggregateId, version);

			_ = (activity?.SetTag(EventSourcingTags.Version, version));
			activity.SetOperationResult(EventSourcingTagValues.Success);
			return AppendResult.CreateSuccess(version, firstPosition);
		}
		catch (MongoBulkWriteException<MongoDbEventDocument> ex)
			when (ex.WriteErrors.Any(e => e.Code == DuplicateKeyErrorCode))
		{
			// Duplicate key error - version conflict detected
			LogConcurrencyConflict(aggregateType, aggregateId, expectedVersion);

			// Re-read current version to report accurate conflict
			var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			result = WriteStoreTelemetry.Results.Conflict;
			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyErrorCode)
		{
			// Duplicate key error - version conflict detected (single document case)
			LogConcurrencyConflict(aggregateType, aggregateId, expectedVersion);

			var actualVersion = await GetCurrentVersionAsync(aggregateId, aggregateType, cancellationToken).ConfigureAwait(false);
			activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
			result = WriteStoreTelemetry.Results.Conflict;
			return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
		}
		// NARROW BY DESIGN. This catches only the faults the provider's own driver raises, which is a
		// closed set the driver defines -- not every exception, filtered down to those that must escape.
		// The difference matters the first time something new appears: an exclusion list is wrong by
		// default and silently converts the newcomer into an ordinary append failure, which is how a
		// cancelled append came to be reported as a store fault and retried inside a cancelled scope.
		// Everything else -- cancellation, an event type the configured resolver does not declare, a
		// programming error -- propagates, because a returned failure means "this could succeed if you
		// try again" and none of those can.
		catch (MongoException ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				messageId,
				correlationId);
			LogAppendError(aggregateType, aggregateId, ex);
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			return AppendResult.CreateFailure(GetFullExceptionMessage(ex));
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.EventStore,
				WriteStoreTelemetry.Providers.MongoDb,
				"append",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
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

		if (_ownsClient && _client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		return ValueTask.CompletedTask;
	}

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

	private static string GetFullExceptionMessage(Exception ex)
	{
		var messages = new List<string>();
		var current = ex;
		while (current != null)
		{
			messages.Add(current.Message);
			current = current.InnerException;
		}

		return string.Join(" -> ", messages);
	}

	/// <summary>
	/// Composes the stored stream identifier, with the owning tenant as its leading segment.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant is part of the stream's IDENTITY rather than a filter applied to it. The unique compound
	/// index is <c>(streamId, aggregateType, version)</c>, so putting the tenant in <c>streamId</c> makes
	/// the version sequence per-tenant as well as the reads. A filter alone would scope reads while leaving
	/// two tenants sharing one version sequence, so the second tenant to use an aggregate identifier would
	/// be told it has a duplicate-key conflict on a stream it never wrote and could never create it.
	/// </para>
	/// <para>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default, and a genuinely untenanted row resolves the reserved untenanted sentinel. So
	/// every stored stream identifier carries a tenant segment and none can be produced without one.
	/// </para>
	/// </remarks>
	/// <param name="aggregateId">The caller-supplied aggregate identifier.</param>
	/// <returns>The stream identifier as stored on the document.</returns>
	private string BuildStreamId(string aggregateId) =>
		$"{TenantKeyPrefix}{TenantScope.FromContext(_tenantContext).TenantId}:{aggregateId}";

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
	/// The probe runs only after initialisation has created the unique index, so it reads through that index
	/// and returns on the first document outside the tenant-prefixed range rather than scanning the
	/// collection. It projects the identifier alone, so no event payload is deserialized to answer it.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The collection holds at least one event document whose stream identifier carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedDocumentsAsync(CancellationToken cancellationToken)
	{
		var filterBuilder = Builders<MongoDbEventDocument>.Filter;
		var legacyShape = filterBuilder.Or(
			filterBuilder.Lt(d => d.StreamId, TenantKeyPrefix),
			filterBuilder.Gte(d => d.StreamId, TenantKeyPrefixUpperBound));

		var legacyStreamId = await _eventsCollection!
			.Find(legacyShape)
			.Project(d => d.StreamId)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (legacyStreamId is null)
		{
			return;
		}

		throw new InvalidOperationException(
			$"Events collection '{_options.CollectionName}' holds at least one event document whose " +
			$"stream identifier ('{legacyStreamId}') carries no tenant segment, so it was written by a " +
			$"release that stored streams without one. Those documents are unaddressable under the " +
			$"current key shape: a load of the aggregate they belong to would return an empty stream, and " +
			$"the caller would then append a second, disjoint history under the same identity. Nothing " +
			$"has been modified. Stop writers, export every event document preserving version order " +
			$"within each stream, re-key each one by prefixing '{TenantKeyPrefix}<tenantId>:' with the " +
			$"tenant that owns the aggregate, re-import, and start the application again.");
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
	/// Deliberately not on the initialisation path. Probing there would spend a query on every process start
	/// - on every serverless cold start, forever - to detect a condition that can only hold across a
	/// one-time upgrade, and would make the store unconstructible without a live collection. Here it costs
	/// nothing at startup, nothing on a read that finds documents, and at most one query per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-empty-reads may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra query and nothing else - cheaper than serialising every empty
	/// read behind a lock. The flag is set only once the probe has come back clean, so a collection that
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

		await RefuseLegacyUntenantedDocumentsAsync(cancellationToken).ConfigureAwait(false);
		_legacyDocumentsProbed = true;
	}

	private async Task<long> GetCurrentVersionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var filterBuilder = Builders<MongoDbEventDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.StreamId, BuildStreamId(aggregateId)),
			filterBuilder.Eq(d => d.AggregateType, aggregateType));

		var sort = Builders<MongoDbEventDocument>.Sort.Descending(d => d.Version);

		var latestEvent = await _eventsCollection!
			.Find(filter)
			.Sort(sort)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (latestEvent is null)
		{
			// AppendAsync prechecks through here, so guarding this answer also guards the write that would
			// otherwise start a second, disjoint history at version 0.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
			return -1;
		}

		return latestEvent.Version;
	}

	/// <summary>
	/// Atomically reserves a contiguous block of <paramref name="count"/> global-sequence numbers with a
	/// single <c>Inc</c>, returning the first number in the reserved block. Replaces per-event counter
	/// round-trips: one append reserves its whole range in one operation.
	/// </summary>
	private async Task<long> ReserveGlobalSequenceBlockAsync(int count, CancellationToken cancellationToken)
	{
		var filter = Builders<MongoDbCounterDocument>.Filter.Eq(d => d.Id, GlobalSequenceCounterId);
		var update = Builders<MongoDbCounterDocument>.Update.Inc(d => d.Sequence, count);
		var options = new FindOneAndUpdateOptions<MongoDbCounterDocument> { ReturnDocument = ReturnDocument.After, IsUpsert = true };

		var result = await _countersCollection!.FindOneAndUpdateAsync(
			filter,
			update,
			options,
			cancellationToken).ConfigureAwait(false);

		// ReturnDocument.After yields the post-increment value = the LAST number in the reserved block;
		// the first is (last - count + 1).
		return result.Sequence - count + 1;
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
			// Re-check under the lock: the winner of the race above completed initialisation
			// while this caller was waiting, and repeating the work would be wrong as well as wasteful.
			if (_initialized)
			{
				return;
			}
			if (_client == null)
			{
				var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
				settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
				settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
				settings.MaxConnectionPoolSize = _options.MaxPoolSize;

				if (_options.UseSsl)
				{
					settings.UseTls = true;
				}

				_client = new MongoClient(settings);
				_database = _client.GetDatabase(_options.DatabaseName);
				_eventsCollection = _database.GetCollection<MongoDbEventDocument>(_options.CollectionName);
				_countersCollection = _database.GetCollection<MongoDbCounterDocument>(_options.CounterCollectionName);
			}

			// Create indexes
			var indexBuilder = Builders<MongoDbEventDocument>.IndexKeys;

			// UNIQUE compound index for optimistic concurrency
			// This enforces that (streamId, aggregateType, version) is unique
			var uniqueVersionIndex = new CreateIndexModel<MongoDbEventDocument>(
				indexBuilder.Combine(
					indexBuilder.Ascending(d => d.StreamId),
					indexBuilder.Ascending(d => d.AggregateType),
					indexBuilder.Ascending(d => d.Version)),
				new CreateIndexOptions { Unique = true, Name = "ix_stream_version_unique" });

			// Index on globalSequence for ordering
			var globalSequenceIndex = new CreateIndexModel<MongoDbEventDocument>(
				indexBuilder.Ascending(d => d.GlobalSequence),
				new CreateIndexOptions { Name = "ix_global_sequence" });

			// Index on eventId for lookups
			var eventIdIndex = new CreateIndexModel<MongoDbEventDocument>(
				indexBuilder.Ascending(d => d.EventId),
				new CreateIndexOptions { Name = "ix_event_id" });

			_ = await _eventsCollection!.Indexes.CreateManyAsync(
				[uniqueVersionIndex, globalSequenceIndex, eventIdIndex],
				cancellationToken).ConfigureAwait(false);

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	private byte[] SerializeEvent(IDomainEvent @event, string? aggregateId, string? aggregateType)
	{
		if (_payloadSerializer != null)
		{
			return _payloadSerializer.Serialize(@event);
		}

#pragma warning disable IL2026, IL3050 // AOT: MongoDB provider uses reflection-based JSON serialization
		// Fallback to System.Text.Json for backward compatibility
		return _hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(@event, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);
#pragma warning restore IL2026, IL3050
	}

#pragma warning disable IL2026, IL3050 // AOT: MongoDB provider uses reflection-based JSON serialization
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		_hasEventTypeInfoResolver
			? EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
			: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
#pragma warning restore IL2026, IL3050

	private byte[] SerializeEventWithEnvelopeSupport(
		IDomainEvent @event,
		string aggregateId,
		string aggregateType,
		long version)
	{
		var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

		if (_internalSerializer is null)
		{
			return SerializeEvent(@event, aggregateId, aggregateType);
		}

		// Create envelope with event data
		var eventBytes = SerializeEvent(@event, aggregateId, aggregateType);

		var envelope = new EventEnvelope
		{
			EventId = Guid.TryParse(@event.EventId, out var guid) ? guid : Guid.NewGuid(),
			AggregateId = Guid.TryParse(aggregateId, out var aggGuid) ? aggGuid : Guid.NewGuid(),
			AggregateType = aggregateType,
			EventType = eventTypeName,
			Version = version,
			Payload = eventBytes,
			OccurredAt = @event.OccurredAt,
			Metadata = @event.Metadata?.ToDictionary(
				kvp => kvp.Key,
				kvp => kvp.Value?.ToString() ?? string.Empty,
				StringComparer.OrdinalIgnoreCase),
			SchemaVersion = 1,
		};

		var envelopeData = _internalSerializer.SerializeToBytes(envelope);

		// Prepend format marker
		var result = new byte[envelopeData.Length + 1];
		result[0] = EnvelopeFormatMarker;
		envelopeData.CopyTo(result, 1);
		return result;
	}

	[LoggerMessage(DataMongoDbEventId.EventsAppended, LogLevel.Debug,
		"Appended {Count} events to {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogEventsAppended(int count, string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataMongoDbEventId.ConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict detected for {AggregateType}/{AggregateId} at expected version {ExpectedVersion}")]
	private partial void LogConcurrencyConflict(string aggregateType, string aggregateId, long expectedVersion);

	[LoggerMessage(DataMongoDbEventId.AppendError, LogLevel.Error, "Failed to append events to {AggregateType}/{AggregateId}")]
	private partial void LogAppendError(string aggregateType, string aggregateId, Exception ex);

	/// <inheritdoc/>
	public async Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbEventDocument>.Filter.And(
			Builders<MongoDbEventDocument>.Filter.Eq(e => e.StreamId, BuildStreamId(aggregateId)),
			Builders<MongoDbEventDocument>.Filter.Eq(e => e.AggregateType, aggregateType),
			Builders<MongoDbEventDocument>.Filter.Ne(e => e.EventType, ErasedEventMarker.EventType));

		var update = Builders<MongoDbEventDocument>.Update
			.Set(e => e.Payload, null!)
			.Set(e => e.EventType, ErasedEventMarker.EventType)
			.Set(e => e.Metadata, null);

		var result = await _eventsCollection!.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return (int)result.ModifiedCount;
	}

	/// <inheritdoc/>
	public async Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbEventDocument>.Filter.And(
			Builders<MongoDbEventDocument>.Filter.Eq(e => e.StreamId, BuildStreamId(aggregateId)),
			Builders<MongoDbEventDocument>.Filter.Eq(e => e.AggregateType, aggregateType),
			Builders<MongoDbEventDocument>.Filter.Eq(e => e.EventType, ErasedEventMarker.EventType));

		var count = await _eventsCollection!.CountDocumentsAsync(filter, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return count > 0;
	}
}
