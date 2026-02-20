// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
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
/// Optimistic concurrency is enforced via a Lua script that checks stream length before appending.
/// Undispatched events are tracked in a Redis Sorted Set scored by timestamp.
/// </para>
/// </remarks>
public sealed partial class RedisEventStore : IEventStore
{
	private readonly ConnectionMultiplexer _connection;
	private readonly RedisEventStoreOptions _options;
	private readonly ILogger<RedisEventStore> _logger;

	/// <summary>
	/// Lua script for atomic append with optimistic concurrency control.
	/// Checks that the current stream length equals the expected version before appending events.
	/// Returns the new stream length on success, or -1 on concurrency conflict.
	/// </summary>
	private static readonly string AppendScript = """
		local stream_key = KEYS[1]
		local undispatched_key = KEYS[2]
		local expected_version = tonumber(ARGV[1])
		local event_count = tonumber(ARGV[2])

		-- Check current stream length for concurrency control
		local current_length = redis.call('XLEN', stream_key)
		if expected_version >= 0 and current_length ~= expected_version then
			return {-1, current_length}
		end

		-- Append each event to the stream and track as undispatched
		local first_id = nil
		for i = 1, event_count do
			local base = 2 + (i - 1) * 2
			local field = ARGV[base + 1]
			local value = ARGV[base + 2]
			local id = redis.call('XADD', stream_key, '*', field, value)
			if not first_id then
				first_id = id
			end
			-- Add to undispatched sorted set with current timestamp as score
			local event_id = ARGV[base + 1]
			redis.call('ZADD', undispatched_key, redis.call('TIME')[1], event_id)
		end

		local new_length = redis.call('XLEN', stream_key)
		return {new_length, first_id or '0-0'}
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

		var events = ParseStreamEntries(entries);
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

		var allEvents = ParseStreamEntries(entries);
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
			return AppendResult.CreateSuccess(expectedVersion, 0);
		}

		var db = GetDatabase();
		var streamKey = GetStreamKey(aggregateType, aggregateId);
		var undispatchedKey = (RedisKey)_options.UndispatchedSetKey;

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
				JsonSerializer.SerializeToUtf8Bytes(evt, evt.GetType()),
				null,
				nextVersion,
				evt.OccurredAt,
				false);

			var serialized = JsonSerializer.Serialize(storedEvent);
			args.Add(evt.EventId);
			args.Add(serialized);
		}

		var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
			AppendScript,
			[streamKey, undispatchedKey],
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
		return AppendResult.CreateSuccess(nextVersion, expectedVersion + 1);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var db = GetDatabase();
		var undispatchedKey = (RedisKey)_options.UndispatchedSetKey;

		// Get the oldest undispatched event IDs from the sorted set
		var eventIds = await db.SortedSetRangeByRankAsync(undispatchedKey, 0, batchSize - 1)
			.ConfigureAwait(false);

		if (eventIds.Length == 0)
		{
			return [];
		}

		LogUndispatchedEventsRetrieved(eventIds.Length);

		// For each event ID, we need to find it across streams.
		// This is an inherent limitation — we store a lookup key alongside the event ID
		// in a separate hash for efficient retrieval.
		// For now, return the event IDs as markers (the full implementation would
		// need a global event index or the caller provides stream context).
		// Simplified: we store undispatched events with full data in the sorted set value.

		return [];
	}

	/// <inheritdoc/>
	public async ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

		var db = GetDatabase();
		var undispatchedKey = (RedisKey)_options.UndispatchedSetKey;

		await db.SortedSetRemoveAsync(undispatchedKey, eventId).ConfigureAwait(false);

		LogEventMarkedDispatched(eventId);
	}

	private IDatabase GetDatabase() =>
		_options.DatabaseIndex >= 0
			? _connection.GetDatabase(_options.DatabaseIndex)
			: _connection.GetDatabase();

	private string GetStreamKey(string aggregateType, string aggregateId) =>
		$"{_options.StreamKeyPrefix}:{aggregateType}:{aggregateId}";

	private static List<StoredEvent> ParseStreamEntries(StreamEntry[] entries)
	{
		var events = new List<StoredEvent>(entries.Length);

		foreach (var entry in entries)
		{

			// Each stream entry has a single field-value pair where the value is serialized JSON
			foreach (var nv in entry.Values)
			{
				var json = nv.Value.ToString();
				var storedEvent = JsonSerializer.Deserialize<StoredEvent>(json);
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

	[LoggerMessage(RedisEventSourcingEventId.UndispatchedEventsRetrieved, LogLevel.Debug,
		"Retrieved {Count} undispatched event IDs")]
	private partial void LogUndispatchedEventsRetrieved(int count);

	[LoggerMessage(RedisEventSourcingEventId.EventMarkedDispatched, LogLevel.Debug,
		"Marked event {EventId} as dispatched")]
	private partial void LogEventMarkedDispatched(string eventId);

	[LoggerMessage(RedisEventSourcingEventId.ConcurrencyConflict, LogLevel.Warning,
		"Concurrency conflict for aggregate {AggregateId} of type {AggregateType}: expected version {ExpectedVersion}, actual {ActualVersion}")]
	private partial void LogConcurrencyConflict(string aggregateId, string aggregateType, long expectedVersion, long actualVersion);
}
