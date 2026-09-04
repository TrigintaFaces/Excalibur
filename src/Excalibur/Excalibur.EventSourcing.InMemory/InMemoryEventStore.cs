// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Observability;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IEventStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe implementation using concurrent collections and locks for atomic operations.
/// </para>
/// <para>
/// Streams are keyed by a composite of (Tenant, AggregateId, AggregateType), so two tenants can address
/// distinct aggregates under one aggregate id without colliding. In a single-tenant deployment the tenant
/// term is the reserved untenanted marker, so the key shape does not vary by deployment.
/// </para>
/// <para>
/// <b>Warning:</b> Not recommended for production use - data is lost on process restart.
/// This implementation is intended for:
/// <list type="bullet">
/// <item>Unit testing</item>
/// <item>Integration testing</item>
/// <item>Local development</item>
/// <item>Proof-of-concept implementations</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class InMemoryEventStore: IEventStore, IEventStoreErasure
{
	private readonly ConcurrentDictionary<(string TenantId, string AggregateId, string AggregateType), List<StoredEvent>> _events = new();
	private readonly ConcurrentDictionary<(string TenantId, string EventId), StoredEvent> _eventsById = new();
	private readonly Lock _lock = new();
	private readonly ITenantContext _tenantContext;
	private long _position;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryEventStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// Resolves the tenant each operation addresses. Consulted per call rather than captured, because one
	/// registered store serves every caller and the tenant belongs to the operation. Its resolved value
	/// becomes part of the stream key, so two tenants writing distinct aggregates under one aggregate id
	/// do not collide.
	/// <para>
	/// Required, not optional. A caller that deliberately runs untenanted passes
	/// <see cref="UntenantedContext.Instance"/>, which names the reserved untenanted partition explicitly.
	/// Were the dependency omissible, "this host runs untenanted" and "the context was forgotten" would
	/// reach the store as the same state, and the two name different partitions -- so events written under
	/// one would stop being visible under the other with nothing raised.
	/// </para>
	/// </param>
	/// <param name="options">
	/// The host's store configuration, supplying the optional source-generated event type-info resolver
	/// (<see cref="InMemoryEventStoreOptions.EventTypeInfoResolver"/>). Optional: omitted, or carrying no
	/// resolver, the store serializes through the reflection-based serializer exactly as before, so a
	/// caller that constructs the store directly is unaffected.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="tenantContext"/> is <see langword="null"/>.</exception>
	public InMemoryEventStore(ITenantContext tenantContext, IOptions<InMemoryEventStoreOptions>? options = null)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;
 _jsonOptions = Excalibur.Dispatch.EventSerializationDefaults.CreateCanonicalOptions();
 _jsonOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;

 // The resolver supplies type METADATA only. It is attached to the canonical options rather than
 // replacing them, so the naming policy, string-enum representation and null handling that fix the
 // stored wire format stay the store's own and apply to whichever resolver is in use -- events written
 // with a resolver are byte-identical to events written without one.
 _hasEventTypeInfoResolver = Excalibur.Dispatch.EventSerializationDefaults.TryApplyTypeInfoResolver(
 _jsonOptions,
 options?.Value?.EventTypeInfoResolver);
	}

	/// <summary>
	/// The tenant partition the current call addresses, re-resolved per call.
	/// </summary>
	/// <remarks>
	/// Re-read rather than captured because this store is registered once and serves every caller: the
	/// tenant is a property of the operation, not of the instance. <see cref="TenantScope.FromContext"/>
	/// fails closed when multi-tenancy is active but no tenant is resolved, so the store cannot reach a
	/// key with no tenant term in it, and yields the reserved untenanted marker in a single-tenant
	/// deployment.
	/// </remarks>
	private TenantScope CurrentTenantScope => TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// Composes the stream key for the aggregate as addressed by the calling tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant term is part of the key because an aggregate id is chosen by the writing application and
	/// is unique only within the tenant that issued it, so two tenants routinely address distinct
	/// aggregates under one id. Keyed on the pair alone, both resolve to a single stream: the second
	/// tenant's append lands in the first tenant's history and its load returns the first tenant's events.
	/// Nothing throws -- it is a cross-tenant read and write on the success path.
	/// </para>
	/// <para>
	/// The key is a tuple, not a delimited string, so it is injective by construction: an aggregate id
	/// containing the character a string form would join on cannot shift a term across the tuple boundary
	/// and collide with another tenant's stream.
	/// </para>
	/// </remarks>
	private (string TenantId, string AggregateId, string AggregateType) GetKey(string aggregateId, string aggregateType)
		=> (CurrentTenantScope.TenantId, aggregateId, aggregateType);

	/// <inheritdoc/>
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
 string aggregateId,
 string aggregateType,
 CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
 return LoadAsync(aggregateId, aggregateType, -1, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
 string aggregateId,
 string aggregateType,
 long fromVersion,
 CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
 cancellationToken.ThrowIfCancellationRequested();

 using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

 try
 {
 var key = GetKey(aggregateId, aggregateType);
 if (!_events.TryGetValue(key, out var events))
 {
 _ = (activity?.SetTag(EventSourcingTags.EventCount, 0));
 activity.SetOperationResult(EventSourcingTagValues.Success);
 // Performance optimization: - ValueTask avoids heap allocation for sync completions
 return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
 }

 lock (events)
 {
 // Events are stored in version order (appended sequentially), so we can use
 // binary search to find the starting index and avoid OrderBy allocation.
 // Performance optimization:, - avoid LINQ materializations
 if (fromVersion < 0)
 {
 // Return all events - they're already sorted by version
 _ = (activity?.SetTag(EventSourcingTags.EventCount, events.Count));
 activity.SetOperationResult(EventSourcingTagValues.Success);
 return new ValueTask<IReadOnlyList<StoredEvent>>(events.ToArray());
 }

 // Find first event with version > fromVersion using linear scan
 // (binary search would require StoredEvent to implement IComparable or a comparer)
 var startIndex = 0;
 for (var i = 0; i < events.Count; i++)
 {
 if (events[i].Version > fromVersion)
 {
 startIndex = i;
 break;
 }

 if (i == events.Count - 1)
 {
 // All events have version <= fromVersion
 _ = (activity?.SetTag(EventSourcingTags.EventCount, 0));
 activity.SetOperationResult(EventSourcingTagValues.Success);
 return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
 }
 }

 // Copy range from startIndex to end - avoids LINQ Where/OrderBy/ToList allocations
 var count = events.Count - startIndex;
 var result = new StoredEvent[count];
 events.CopyTo(startIndex, result, 0, count);

 _ = (activity?.SetTag(EventSourcingTags.EventCount, count));
 activity.SetOperationResult(EventSourcingTagValues.Success);

 return new ValueTask<IReadOnlyList<StoredEvent>>(result);
 }
 }
 catch (Exception ex)
 {
 activity.RecordException(ex);
 activity.SetOperationResult(EventSourcingTagValues.Failure);
 throw;
 }
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
 Justification = "InMemoryEventStore is a test/dev store. SerializeEvent and SerializeMetadata use reflection-based JSON serialization by design.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
 Justification = "InMemoryEventStore is a test/dev store. SerializeEvent and SerializeMetadata use reflection-based JSON serialization by design.")]
	public ValueTask<AppendResult> AppendAsync(
 string aggregateId,
 string aggregateType,
 IEnumerable<IDomainEvent> events,
 long expectedVersion,
 CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentNullException.ThrowIfNull(events);
 cancellationToken.ThrowIfCancellationRequested();

 // Performance optimization: - avoid ToList() when possible
 // If already a collection with Count, use directly; otherwise materialize once
 var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

 if (eventList.Count == 0)
 {
 // Performance optimization: - ValueTask avoids heap allocation for sync completions
 return new ValueTask<AppendResult>(AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null));
 }

 using var activity = EventSourcingActivitySource.StartAppendActivity(
 aggregateId, aggregateType, eventList.Count, expectedVersion);

 try
 {
 var key = GetKey(aggregateId, aggregateType);

 lock (_lock)
 {
 // Get or create event list for this aggregate
 var aggregateEvents = _events.GetOrAdd(key, _ => new List<StoredEvent>());

 lock (aggregateEvents)
 {
 // Check current version for optimistic concurrency
 // Events are stored in order, so last event has max version - avoid LINQ Max
 var currentVersion = aggregateEvents.Count > 0
 ? aggregateEvents[^1].Version
: -1;

 if (currentVersion != expectedVersion)
 {
 // Concurrency conflict detected via return value (no exception)
 activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);
 return new ValueTask<AppendResult>(AppendResult.CreateConcurrencyConflict(expectedVersion, currentVersion));
 }

 // Append events
 long firstPosition = 0;
 var version = currentVersion;

 foreach (var (@event, eventTypeName) in eventList.AsNamedEvents())
 {
 version++;
 var position = Interlocked.Increment(ref _position);

 if (firstPosition == 0)
 {
 firstPosition = position;
 }

 var storedEvent = new StoredEvent(
 EventId: @event.EventId,
 AggregateId: aggregateId,
 AggregateType: aggregateType,
 EventType: eventTypeName,
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
 EventData: SerializeEvent(@event, aggregateId, aggregateType),
 Metadata: @event.Metadata != null ? SerializeMetadata(@event.Metadata): null,
#pragma warning restore IL2026, IL3050
 Version: version,
 Timestamp: @event.OccurredAt);

 aggregateEvents.Add(storedEvent);
 _eventsById[(key.TenantId, storedEvent.EventId)] = storedEvent;
 }

 _ = (activity?.SetTag(EventSourcingTags.Version, version));
 activity.SetOperationResult(EventSourcingTagValues.Success);
 return new ValueTask<AppendResult>(AppendResult.CreateSuccess(version, firstPosition));
 }
 }
 }
 catch (Exception ex) when (ex is not OperationCanceledException)
 {
 activity.RecordException(ex);
 activity.SetOperationResult(EventSourcingTagValues.Failure);
 // A corrupted in-memory structure is a programming error, not a store outcome. It propagates: a caller
 // cannot act on it, a resilience pipeline must not retry it, and swallowing it into a failure result
 // would hide the defect behind a value that looks like an ordinary infrastructure fault.
 throw;
 }
	}

	/// <summary>
	/// The event type marker used for tombstoned (erased) events.
	/// </summary>
	/// <remarks>
	/// sourced from <see cref="ErasedEventMarker.EventType"/> so the sentinel has a single
	/// source of truth across all event-store providers (avoids a latent GDPR-erasure desync).
	/// </remarks>
	internal const string TombstoneEventType = ErasedEventMarker.EventType;

	private static readonly byte[] TombstonePayload = "ERASED"u8.ToArray();

	// Tenant-keyed for the same reason the stream itself is: one tenant erasing its aggregate must not
	// make another tenant's same-id aggregate report as erased, which would suppress that tenant's replay.
	private readonly HashSet<(string TenantId, string AggregateId, string AggregateType)> _erasedAggregates = [];

	/// <inheritdoc/>
	public Task<int> EraseEventsAsync(
 string aggregateId,
 string aggregateType,
 Guid erasureRequestId,
 CancellationToken cancellationToken)
	{
 cancellationToken.ThrowIfCancellationRequested();

 var key = GetKey(aggregateId, aggregateType);

 lock (_lock)
 {
 if (!_events.TryGetValue(key, out var aggregateEvents))
 {
 return Task.FromResult(0);
 }

 lock (aggregateEvents)
 {
 var count = aggregateEvents.Count;
 for (var i = 0; i < aggregateEvents.Count; i++)
 {
 var original = aggregateEvents[i];
 var tombstoned = original with
 {
 EventType = TombstoneEventType,
 EventData = TombstonePayload,
 Metadata = null
 };
 aggregateEvents[i] = tombstoned;
 _eventsById[(key.TenantId, original.EventId)] = tombstoned;
 }

 _erasedAggregates.Add(key);
 return Task.FromResult(count);
 }
 }
	}

	/// <inheritdoc/>
	public Task<bool> IsErasedAsync(
 string aggregateId,
 string aggregateType,
 CancellationToken cancellationToken)
	{
 cancellationToken.ThrowIfCancellationRequested();

 lock (_lock)
 {
 return Task.FromResult(_erasedAggregates.Contains(GetKey(aggregateId, aggregateType)));
 }
	}

	/// <summary>
	/// Clears all stored events. For testing purposes only.
	/// </summary>
	public void Clear()
	{
 lock (_lock)
 {
 _events.Clear();
 _eventsById.Clear();
 _erasedAggregates.Clear();
 _position = 0;
 }
	}

	/// <summary>
	/// Gets the total count of stored events across all aggregates.
	/// </summary>
	/// <returns>The total number of events stored.</returns>
	public int GetEventCount()
	{
 lock (_lock)
 {
 return _eventsById.Count;
 }
	}

	/// <summary>
	/// Serializes a domain event, resolving its type metadata from the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	/// <remarks>
	/// A domain event is a consumer type the framework cannot source-generate, so with no resolver the only
	/// available path is the reflection-based serializer -- which works under the JIT and has nothing to fall
	/// back on in a native-AOT application published with reflection-based serialization disabled. The
	/// annotations therefore stay on this method: the reflection branch remains reachable, and saying
	/// otherwise would be the claim, not the fix. A host that supplies a resolver takes the
	/// <see cref="JsonTypeInfo"/> branch, which resolves nothing at run time.
	/// </remarks>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEvent(IDomainEvent @event, string? aggregateId, string? aggregateType) =>
		_hasEventTypeInfoResolver
			? ResolvedEventPayload.Serialize(@event, _jsonOptions, aggregateId, aggregateType)
			: JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);

	/// <summary>
	/// Serializes event metadata, dispatching each value through the host's source-generated resolver when
	/// one was supplied and falling back to reflection when none was.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
 _hasEventTypeInfoResolver
 ? Excalibur.Dispatch.EventSerializationDefaults.SerializeMetadataWithResolver(metadata, _jsonOptions)
: JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
}
