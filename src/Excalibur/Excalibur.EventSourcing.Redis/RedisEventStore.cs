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

	// The single canonical event contract (camelCase + string-enum + null-ignore) shared by every event
	// store. Using the default serializer here would write PascalCase / enum-as-number bodies that mis-read
	// when loaded through the canonical read path (the i2eabb cross-path fault).
	private readonly JsonSerializerOptions _jsonOptions = EventSerializationDefaults.CreateCanonicalOptions();

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
	public RedisEventStore(
		ConnectionMultiplexer connection,
		IOptions<RedisEventStoreOptions> options,
		ILogger<RedisEventStore> logger)
	{
		_connection = connection ?? throw new ArgumentNullException(nameof(connection));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
		foreach (var evt in eventList)
		{
			nextVersion++;
			var storedEvent = new StoredEvent(
				evt.EventId,
				aggregateId,
				aggregateType,
				evt.EventType,
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType(), _jsonOptions),
#pragma warning restore IL2026, IL3050
				null,
				nextVersion,
				evt.OccurredAt);

#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
			var serialized = JsonSerializer.Serialize(storedEvent, _jsonOptions);
#pragma warning restore IL2026, IL3050
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

	private string GetStreamKey(string aggregateType, string aggregateId) =>
		$"{_options.StreamKeyPrefix}:{aggregateType}:{aggregateId}";

	// The authoritative per-stream version counter lives in a companion key. The stream key is wrapped
	// in a Redis Cluster hash tag so the counter always hashes to the same slot as its stream, keeping
	// the multi-key append script single-slot (and therefore cluster-safe).
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
#pragma warning disable IL2026, IL3050 // Serialization inherently uses reflection
				var storedEvent = JsonSerializer.Deserialize<StoredEvent>(json, options);
#pragma warning restore IL2026, IL3050
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
}
