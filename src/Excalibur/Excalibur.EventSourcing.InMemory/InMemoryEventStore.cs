// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Observability;

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IEventStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe implementation using concurrent collections and locks for atomic operations.
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
public sealed class InMemoryEventStore : IEventStore, IEventStoreErasure
{
	private readonly ConcurrentDictionary<(string AggregateId, string AggregateType), List<StoredEvent>> _events = new();
	private readonly ConcurrentDictionary<string, StoredEvent> _eventsById = new();
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif
	private long _position;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryEventStore"/> class.
	/// </summary>
	public InMemoryEventStore()
	{
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
		};
	}

	/// <inheritdoc/>
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		return LoadAsync(aggregateId, aggregateType, -1, cancellationToken);
	}

	/// <inheritdoc/>
	public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = EventSourcingActivitySource.StartLoadActivity(aggregateId, aggregateType, fromVersion);

		try
		{
			var key = (aggregateId, aggregateType);
			if (!_events.TryGetValue(key, out var events))
			{
				_ = (activity?.SetTag(EventSourcingTags.EventCount, 0));
				activity.SetOperationResult(EventSourcingTagValues.Success);
				// Performance optimization: AD-250-5 - ValueTask avoids heap allocation for sync completions
				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			}

			lock (events)
			{
				// Events are stored in version order (appended sequentially), so we can use
				// binary search to find the starting index and avoid OrderBy allocation.
				// Performance optimization: AD-250-1, AD-250-3 - avoid LINQ materializations
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
	public ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Performance optimization: AD-250-1 - avoid ToList() when possible
		// If already a collection with Count, use directly; otherwise materialize once
		var eventList = events as IReadOnlyCollection<IDomainEvent> ?? events.ToList();

		if (eventList.Count == 0)
		{
			// Performance optimization: AD-250-5 - ValueTask avoids heap allocation for sync completions
			return new ValueTask<AppendResult>(AppendResult.CreateSuccess(expectedVersion, 0));
		}

		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId, aggregateType, eventList.Count, expectedVersion);

		try
		{
			var key = (aggregateId, aggregateType);

			lock (_lock)
			{
				// Get or create event list for this aggregate
				var aggregateEvents = _events.GetOrAdd(key, _ => new List<StoredEvent>());

				lock (aggregateEvents)
				{
					// Check current version for optimistic concurrency
					// AD-251-3: Events are stored in order, so last event has max version - avoid LINQ Max
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

					foreach (var @event in eventList)
					{
						version++;
						var position = Interlocked.Increment(ref _position);
						var eventTypeName = EventTypeNameHelper.GetEventTypeName(@event.GetType());

						if (firstPosition == 0)
						{
							firstPosition = position;
						}

						var storedEvent = new StoredEvent(
							EventId: @event.EventId,
							AggregateId: aggregateId,
							AggregateType: aggregateType,
							EventType: eventTypeName,
							EventData: SerializeEvent(@event),
							Metadata: @event.Metadata != null ? SerializeMetadata(@event.Metadata) : null,
							Version: version,
							Timestamp: @event.OccurredAt,
							IsDispatched: false);

						aggregateEvents.Add(storedEvent);
						_eventsById[storedEvent.EventId] = storedEvent;
					}

					_ = (activity?.SetTag(EventSourcingTags.Version, version));
					activity.SetOperationResult(EventSourcingTagValues.Success);
					return new ValueTask<AppendResult>(AppendResult.CreateSuccess(version, firstPosition));
				}
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
	public ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		try
		{
			lock (_lock)
			{
				// Performance optimization: AD-250-1, AD-250-3 - avoid LINQ materializations
				// Pre-allocate array with max expected size to avoid List resizing
				var undispatched = new StoredEvent[Math.Min(batchSize, _eventsById.Count)];
				var count = 0;

				// Single pass through events, collecting undispatched ones
				// Note: We need ordering by timestamp, so we collect first then sort
				foreach (var storedEvent in _eventsById.Values)
				{
					if (!storedEvent.IsDispatched)
					{
						if (count < undispatched.Length)
						{
							undispatched[count++] = storedEvent;
						}
						else
						{
							// Need more space - resize (rare case)
							Array.Resize(ref undispatched, undispatched.Length * 2);
							undispatched[count++] = storedEvent;
						}
					}
				}

				if (count == 0)
				{
					_ = (activity?.SetTag(EventSourcingTags.EventCount, 0));
					activity.SetOperationResult(EventSourcingTagValues.Success);
					// Performance optimization: AD-250-5 - ValueTask avoids heap allocation for sync completions
					return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
				}

				// Sort by timestamp and take batchSize
				var resultSpan = undispatched.AsSpan(0, count);
				resultSpan.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

				var resultCount = Math.Min(count, batchSize);
				var result = new StoredEvent[resultCount];
				resultSpan[..resultCount].CopyTo(result);

				_ = (activity?.SetTag(EventSourcingTags.EventCount, resultCount));
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
	public ValueTask MarkEventAsDispatchedAsync(
		string eventId,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		try
		{
			if (_eventsById.TryGetValue(eventId, out var storedEvent))
			{
				lock (_lock)
				{
					// Create updated event with IsDispatched = true
					var updatedEvent = storedEvent with { IsDispatched = true };

					// Update in both dictionaries
					_eventsById[eventId] = updatedEvent;

					var key = (storedEvent.AggregateId, storedEvent.AggregateType);
					if (_events.TryGetValue(key, out var aggregateEvents))
					{
						lock (aggregateEvents)
						{
							var index = aggregateEvents.FindIndex(e => e.EventId == eventId);
							if (index >= 0)
							{
								aggregateEvents[index] = updatedEvent;
							}
						}
					}
				}
			}

			activity.SetOperationResult(EventSourcingTagValues.Success);
			// Performance optimization: AD-250-5 - ValueTask avoids heap allocation for sync completions
			return ValueTask.CompletedTask;
		}
		catch (Exception ex)
		{
			activity.RecordException(ex);
			activity.SetOperationResult(EventSourcingTagValues.Failure);
			throw;
		}
	}

	/// <summary>
	/// The event type marker used for tombstoned (erased) events.
	/// </summary>
	internal const string TombstoneEventType = "$erased";

	private static readonly byte[] TombstonePayload = "ERASED"u8.ToArray();

	private readonly HashSet<(string AggregateId, string AggregateType)> _erasedAggregates = [];

	/// <inheritdoc/>
	public Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = (aggregateId, aggregateType);

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
					_eventsById[original.EventId] = tombstoned;
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
			return Task.FromResult(_erasedAggregates.Contains((aggregateId, aggregateType)));
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
	/// Gets the count of undispatched events.
	/// </summary>
	/// <returns>The number of events that have not been dispatched.</returns>
	public int GetUndispatchedEventCount()
	{
		lock (_lock)
		{
			// AD-251-3: Avoid LINQ Count - use manual iteration
			var count = 0;
			foreach (var e in _eventsById.Values)
			{
				if (!e.IsDispatched)
				{
					count++;
				}
			}

			return count;
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Object, Type, JsonSerializerOptions)")]
	private byte[] SerializeEvent(IDomainEvent @event) =>
		JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _jsonOptions);

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private byte[] SerializeMetadata(IDictionary<string, object> metadata) =>
		JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
}
