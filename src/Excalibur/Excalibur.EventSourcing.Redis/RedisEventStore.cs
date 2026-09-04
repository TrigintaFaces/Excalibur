// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Redis.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Redis Streams-based implementation of <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses Redis Streams for the event log: one stream per aggregate (<c>es:{aggregateType}:{aggregateId}</c>).
/// </para>
/// <para>
/// Optimistic concurrency is enforced against an authoritative per-stream version counter (a companion
/// key holding the highest version appended), never against the raw stream length. Stream length
/// (<c>XLEN</c>) drifts below the true version when entries are trimmed or deleted (<c>XTRIM</c>/
/// <c>XDEL</c>), which would corrupt a length-based concurrency check; the stored counter is immune to
/// trimming. The counter check, increment, and event append all execute inside a single Lua script so
/// they are atomic.
/// </para>
/// </remarks>
public sealed partial class RedisEventStore : IEventStore
{
	private readonly ConnectionMultiplexer _connection;
	private readonly RedisEventStoreOptions _options;
	private readonly ILogger<RedisEventStore> _logger;
	private readonly ITenantContext _tenantContext;

	// The single canonical event contract (camelCase + string-enum + null-ignore) shared by every event
	// store. Using the default serializer here would write PascalCase / enum-as-number bodies that mis-read
	// when loaded through the canonical read path (the cross-path fault).
	private readonly JsonSerializerOptions _jsonOptions = EventSerializationDefaults.CreateCanonicalOptions();

	/// <summary>
	/// Whether the host supplied an event type-info resolver, selecting the reflection-free serialization
	/// path. Decided once at construction because the resolver cannot change for a constructed store.
	/// </summary>
	private readonly bool _hasEventTypeInfoResolver;

	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every stream key it builds
	/// binds the same value. The context is a required dependency, so the term is decided identically on
	/// every path: the store cannot resolve one partition on write and a different one on read. Matches
	/// <see cref="RedisSnapshotStore"/>'s own shape — its sibling store in this package, which already
	/// does this.
	/// </summary>
	private TenantScope CurrentTenantScope => TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// Lua script for atomic append with optimistic concurrency control.
	/// The current aggregate version is read from an authoritative version counter (<c>KEYS[2]</c>),
	/// NOT from the stream length, so trimmed or deleted stream entries can never produce a false
	/// concurrency match. For a new-aggregate create (<c>expectedVersion == -1</c>) the counter must be
	/// absent; otherwise the stored version must equal the expected version before appending. On success
	/// the counter is advanced to the new highest version atomically with the appends. Returns the new
	/// version on success, or <c>-1</c> plus the actual stored version on concurrency conflict.
	/// </summary>
	/// <remarks>
	/// KEYS[1] = stream key; KEYS[2] = version counter key.
	/// </remarks>
	private static readonly string AppendScript = """
		local stream_key = KEYS[1]
		local version_key = KEYS[2]
		local expected_version = tonumber(ARGV[1])
		local event_count = tonumber(ARGV[2])

		-- Authoritative current version comes from the stored counter, NOT XLEN.
		-- XLEN drifts below the true version under XTRIM/XDEL, which would corrupt this check.
		local stored = redis.call('GET', version_key)
		local current_version
		if stored == false then
			current_version = -1
		else
			current_version = tonumber(stored)
		end

		-- Optimistic concurrency: the stored version must match the caller's expectation.
		-- Create (expected_version == -1) requires an absent counter (current_version == -1);
		-- without this guard two concurrent creates would both append (lost-write / double-create).
		if current_version ~= expected_version then
			return {-1, current_version}
		end

		-- Append each event to the stream
		local first_id = nil
		for i = 1, event_count do
			local base = 2 + (i - 1) * 2
			local field = ARGV[base + 1]
			local value = ARGV[base + 2]
			local id = redis.call('XADD', stream_key, '*', field, value)
			if not first_id then
				first_id = id
			end
		end

		-- Advance the authoritative version counter atomically with the appends.
		local new_version = expected_version + event_count
		redis.call('SET', version_key, new_version)
		return {new_version, first_id or '0-0'}
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisEventStore"/> class.
	/// </summary>
	/// <param name="connection">The Redis connection multiplexer.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions streams by tenant, and it resolves
	/// that partition from here, so there is no state in which the partition is undecided. A
	/// single-tenant host receives the framework default context and operates as the one canonical
	/// tenant.
	/// </param>
	public RedisEventStore(
		ConnectionMultiplexer connection,
		IOptions<RedisEventStoreOptions> options,
		ILogger<RedisEventStore> logger,
		ITenantContext tenantContext)
	{
		_connection = connection ?? throw new ArgumentNullException(nameof(connection));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		_hasEventTypeInfoResolver = EventSerializationDefaults.TryApplyTypeInfoResolver(_jsonOptions, _options.EventTypeInfoResolver);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var db = GetDatabase();
		var streamKey = GetStreamKey(aggregateType, aggregateId);

		var entries = await db.StreamRangeAsync(streamKey, "-", "+").ConfigureAwait(false);

		var events = ParseStreamEntries(entries, _jsonOptions);
		LogEventsLoaded(aggregateId, aggregateType, events.Count);

		return events;
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

		var db = GetDatabase();
		var streamKey = GetStreamKey(aggregateType, aggregateId);

		// Load all events then filter by version (Redis Streams don't have native version filtering)
		var entries = await db.StreamRangeAsync(streamKey, "-", "+").ConfigureAwait(false);

		var allEvents = ParseStreamEntries(entries, _jsonOptions);
		var filtered = allEvents.Where(e => e.Version > fromVersion).ToList();

		LogEventsLoaded(aggregateId, aggregateType, filtered.Count);

		return filtered;
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

		var eventList = events.ToList();
		if (eventList.Count == 0)
		{
			// Redis tracks only a per-stream version counter, not a store-wide global sequence, so there
			// is no meaningful global first-event position to report; global ordering is unsupported here.
			return AppendResult.CreateSuccess(expectedVersion, firstEventPosition: null);
		}

		var db = GetDatabase();
		var streamKey = GetStreamKey(aggregateType, aggregateId);

		// Build Lua script arguments: expectedVersion, eventCount, then pairs of (eventId, serializedEvent)
		var args = new List<RedisValue>
		{
			expectedVersion,
			eventList.Count,
		};

		var nextVersion = expectedVersion;
		foreach (var (evt, eventTypeName) in eventList.AsNamedEvents())
		{
			nextVersion++;
			// Named arguments, deliberately: this envelope is built positionally by every other store, but
			// here the metadata term is the one whose type (byte[]?) accepts a bare `null` silently. A
			// literal null in that position is indistinguishable from a supplied value at a glance, and is
			// exactly how this store came to drop metadata on the contract surface while every sibling
			// carried it. Naming the arguments makes the omission unspellable rather than merely unlikely.
			var storedEvent = new StoredEvent(
				EventId: evt.EventId,
				AggregateId: aggregateId,
				AggregateType: aggregateType,
				EventType: eventTypeName,
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				EventData: SerializeEvent(evt, aggregateId, aggregateType),
				// The event's own metadata, carried on the envelope as every other event store carries it.
				// It is also inside EventData (the whole event is serialized there), so this was never a
				// loss at rest — but a consumer reads StoredEvent.Metadata, and this store used to hand
				// back null, so switching a provider to Redis silently emptied that property.
				Metadata: evt.Metadata != null
					? SerializeMetadata(evt.Metadata)
					: null,
#pragma warning restore IL2026, IL3050
				Version: nextVersion,
				Timestamp: evt.OccurredAt);

			var serialized = JsonSerializer.Serialize(storedEvent, RedisEventStoreJsonContext.Default.StoredEvent);
			args.Add(evt.EventId);
			args.Add(serialized);
		}

		var versionKey = GetVersionKey(streamKey);

		try
		{
			var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
				AppendScript,
				[streamKey, versionKey],
				args.ToArray()).ConfigureAwait(false);

			if (result == null || result.Length < 2)
			{
				return AppendResult.CreateFailure("Unexpected Lua script result.");
			}

			var statusValue = (long)result[0];

			if (statusValue == -1)
			{
				var actualVersion = (long)result[1];
				LogConcurrencyConflict(aggregateId, aggregateType, expectedVersion, actualVersion);
				return AppendResult.CreateConcurrencyConflict(expectedVersion, actualVersion);
			}

			LogEventsAppended(aggregateId, aggregateType, eventList.Count, nextVersion);
			// Per-stream version counter only — no store-wide global sequence, so no global first-event position.
			return AppendResult.CreateSuccess(nextVersion, firstEventPosition: null);
		}
		// Only a provider fault normalizes to a failure result. Cancellation, and any programming error
		// (a null reference, a bad argument), propagates untouched: the caller asked to stop, or the code is
		// wrong. Neither is a store outcome, and neither should be retried by a resilience pipeline.
		catch (RedisException ex)
		{
			// Liskov (MS-01): a transient Redis fault (connection loss, timeout) is REPORTED as a failed
			// result — never propagated as a raw RedisException. Version conflicts are returned above; a
			// leaked provider exception is the substitutability violation this normalizes away.
			return AppendResult.CreateFailure(ex.Message);
		}
	}

	private IDatabase GetDatabase() =>
		_options.DatabaseIndex >= 0
			? _connection.GetDatabase(_options.DatabaseIndex)
			: _connection.GetDatabase();

	/// <summary>
	/// Builds the key identifying a single aggregate's stream, including the tenant term. Every read and
	/// write path routes through this one method, so the tenant cannot be applied on some operations and
	/// forgotten on others — the exact gap this store used to have: two tenants
	/// appending events for the same (aggregateType, aggregateId) shared one stream and one version
	/// counter, a cross-tenant collision rather than merely a read leak.
	/// </summary>
	private string GetStreamKey(string aggregateType, string aggregateId)
	{
		var scope = CurrentTenantScope;
		return $"{_options.StreamKeyPrefix}:t:{scope.TenantId}:{aggregateType}:{aggregateId}";
	}

	// The authoritative per-stream version counter lives in a companion key. The stream key is wrapped
	// in a Redis Cluster hash tag so the counter always hashes to the same slot as its stream, keeping
	// the multi-key append script single-slot (and therefore cluster-safe). The stream key already binds
	// the tenant (GetStreamKey), so the version key inherits it automatically.
	private static string GetVersionKey(string streamKey) => $"{{{streamKey}}}:ver";

	private static List<StoredEvent> ParseStreamEntries(StreamEntry[] entries, JsonSerializerOptions options)
	{
		var events = new List<StoredEvent>(entries.Length);

		foreach (var entry in entries)
		{

			// Each stream entry has a single field-value pair where the value is serialized JSON
			foreach (var nv in entry.Values)
			{
				var json = nv.Value.ToString();
				var storedEvent = JsonSerializer.Deserialize(json, RedisEventStoreJsonContext.Default.StoredEvent);
				if (storedEvent != null)
				{
					events.Add(storedEvent);
				}

				break; // Only one field-value pair per entry
			}
		}

		return events;
	}

	[LoggerMessage(RedisEventSourcingEventId.EventsLoaded, LogLevel.Debug,
		"Loaded {EventCount} events for aggregate {AggregateId} of type {AggregateType}")]
	private partial void LogEventsLoaded(string aggregateId, string aggregateType, int eventCount);

	[LoggerMessage(RedisEventSourcingEventId.EventsAppended, LogLevel.Debug,
		"Appended {EventCount} events for aggregate {AggregateId} of type {AggregateType}, new version {NewVersion}")]
	private partial void LogEventsAppended(string aggregateId, string aggregateType, int eventCount, long newVersion);

	[LoggerMessage(RedisEventSourcingEventId.ConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict for aggregate {AggregateId} of type {AggregateType}: expected version {ExpectedVersion}, actual {ActualVersion}")]
	private partial void LogConcurrencyConflict(string aggregateId, string aggregateType, long expectedVersion, long actualVersion);

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
