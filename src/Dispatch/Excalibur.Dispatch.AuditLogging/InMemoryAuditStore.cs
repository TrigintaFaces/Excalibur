// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging;

/// <summary>
/// In-memory implementation of <see cref="IAuditStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is NOT suitable for production use:
/// - Events are not persisted across application restarts
/// - Memory grows unbounded
/// - No multi-instance support
/// </para>
/// <para> For production, use a persistent store implementation (SQL Server, Postgres, etc.). </para>
/// </remarks>
public sealed class InMemoryAuditStore : IAuditStore
{
	private readonly ConcurrentDictionary<string, AuditEvent> _eventsById = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, List<AuditEvent>> _eventsByTenant = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER

	private readonly Lock _sequenceLock = new();

#else
	private readonly object _sequenceLock = new();

#endif
	private long _sequenceNumber;
	private readonly DateTimeOffset _chainInitTime = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var tenantKey = auditEvent.TenantId ?? "_default_";

		lock (_sequenceLock)
		{
			_sequenceNumber++;
			var sequenceNumber = _sequenceNumber;

			// Get previous hash for chain linking
			var previousHash = GetPreviousHash(tenantKey);

			// Compute hash for this event
			var eventHash = AuditHasher.ComputeHash(auditEvent, previousHash);

			// Create stored event with hash and chain link
			var storedEvent = auditEvent with { PreviousEventHash = previousHash, EventHash = eventHash };

			// Store the event
			if (!_eventsById.TryAdd(storedEvent.EventId, storedEvent))
			{
				throw new InvalidOperationException($"Audit event with ID '{storedEvent.EventId}' already exists.");
			}

			var tenantEvents = _eventsByTenant.GetOrAdd(tenantKey, _ => []);
			lock (tenantEvents)
			{
				tenantEvents.Add(storedEvent);
			}

			return Task.FromResult(new AuditEventId
			{
				EventId = storedEvent.EventId,
				EventHash = eventHash,
				SequenceNumber = sequenceNumber,
				RecordedAt = DateTimeOffset.UtcNow
			});
		}
	}

	/// <inheritdoc />
	public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		cancellationToken.ThrowIfCancellationRequested();

		_ = _eventsById.TryGetValue(eventId, out var auditEvent);
		return Task.FromResult(auditEvent);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<AuditEvent> events;

		if (!string.IsNullOrEmpty(query.TenantId))
		{
			if (!_eventsByTenant.TryGetValue(query.TenantId, out var tenantEvents))
			{
				return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
			}

			lock (tenantEvents)
			{
				events = tenantEvents.ToList();
			}
		}
		else
		{
			events = _eventsById.Values;
		}

		events = ApplyFilters(events, query);

		// Apply ordering
		events = query.OrderByDescending
			? events.OrderByDescending(e => e.Timestamp)
			: events.OrderBy(e => e.Timestamp);

		// Apply pagination
		var result = events
			.Skip(query.Skip)
			.Take(query.MaxResults)
			.ToList();

		return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
	}

	/// <inheritdoc />
	public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<AuditEvent> events;

		if (!string.IsNullOrEmpty(query.TenantId))
		{
			if (!_eventsByTenant.TryGetValue(query.TenantId, out var tenantEvents))
			{
				return Task.FromResult(0L);
			}

			lock (tenantEvents)
			{
				events = tenantEvents.ToList();
			}
		}
		else
		{
			events = _eventsById.Values;
		}

		events = ApplyFilters(events, query);
		return Task.FromResult((long)events.Count());
	}

	/// <inheritdoc />
	public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Get all events in the date range, ordered by timestamp
		var events = _eventsById.Values
			.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
			.OrderBy(e => e.Timestamp)
			.ThenBy(e => e.EventId, StringComparer.Ordinal)
			.ToList();

		if (events.Count == 0)
		{
			return Task.FromResult(AuditIntegrityResult.Valid(0, startDate, endDate));
		}

		var violationCount = 0;
		string? firstViolationEventId = null;
		string? violationDescription = null;

		// Verify each event's hash
		for (var i = 0; i < events.Count; i++)
		{
			var currentEvent = events[i];
			var previousHash = currentEvent.PreviousEventHash;

			// Verify the current event's hash
			if (!AuditHasher.VerifyHash(currentEvent, previousHash))
			{
				violationCount++;
				firstViolationEventId ??= currentEvent.EventId;
				violationDescription ??= $"Hash mismatch for event '{currentEvent.EventId}' at {currentEvent.Timestamp:O}";
			}
		}

		if (violationCount > 0)
		{
			return Task.FromResult(AuditIntegrityResult.Invalid(
				events.Count,
				startDate,
				endDate,
				firstViolationEventId,
				violationDescription,
				violationCount));
		}

		return Task.FromResult(AuditIntegrityResult.Valid(events.Count, startDate, endDate));
	}

	/// <inheritdoc />
	public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var tenantKey = tenantId ?? "_default_";

		if (!_eventsByTenant.TryGetValue(tenantKey, out var tenantEvents))
		{
			return Task.FromResult<AuditEvent?>(null);
		}

		lock (tenantEvents)
		{
			var lastEvent = tenantEvents.LastOrDefault();
			return Task.FromResult(lastEvent);
		}
	}

	/// <summary>
	/// Clears all events from the store. For testing purposes only.
	/// </summary>
	public void Clear()
	{
		lock (_sequenceLock)
		{
			_eventsById.Clear();
			_eventsByTenant.Clear();
			_sequenceNumber = 0;
		}
	}

	/// <summary>
	/// Gets the total count of events in the store.
	/// </summary>
	public int Count => _eventsById.Count;

	private string? GetPreviousHash(string tenantKey)
	{
		if (!_eventsByTenant.TryGetValue(tenantKey, out var tenantEvents))
		{
			// First event in chain - return genesis hash
			return AuditHasher.ComputeGenesisHash(tenantKey == "_default_" ? null : tenantKey, _chainInitTime);
		}

		lock (tenantEvents)
		{
			var lastEvent = tenantEvents.LastOrDefault();
			if (lastEvent is null)
			{
				return AuditHasher.ComputeGenesisHash(tenantKey == "_default_" ? null : tenantKey, _chainInitTime);
			}

			return lastEvent.EventHash;
		}
	}

	private static IEnumerable<AuditEvent> ApplyFilters(IEnumerable<AuditEvent> events, AuditQuery query)
	{
		if (query.StartDate.HasValue)
		{
			events = events.Where(e => e.Timestamp >= query.StartDate.Value);
		}

		if (query.EndDate.HasValue)
		{
			events = events.Where(e => e.Timestamp <= query.EndDate.Value);
		}

		if (query.EventTypes is { Count: > 0 })
		{
			events = events.Where(e => query.EventTypes.Contains(e.EventType));
		}

		if (query.Outcomes is { Count: > 0 })
		{
			events = events.Where(e => query.Outcomes.Contains(e.Outcome));
		}

		if (!string.IsNullOrEmpty(query.ActorId))
		{
			events = events.Where(e => string.Equals(e.ActorId, query.ActorId, StringComparison.Ordinal));
		}

		if (!string.IsNullOrEmpty(query.ResourceId))
		{
			events = events.Where(e => string.Equals(e.ResourceId, query.ResourceId, StringComparison.Ordinal));
		}

		if (!string.IsNullOrEmpty(query.ResourceType))
		{
			events = events.Where(e => string.Equals(e.ResourceType, query.ResourceType, StringComparison.Ordinal));
		}

		if (query.MinimumClassification.HasValue)
		{
			events = events.Where(e => e.ResourceClassification >= query.MinimumClassification.Value);
		}

		if (!string.IsNullOrEmpty(query.CorrelationId))
		{
			events = events.Where(e => string.Equals(e.CorrelationId, query.CorrelationId, StringComparison.Ordinal));
		}

		if (!string.IsNullOrEmpty(query.Action))
		{
			events = events.Where(e => string.Equals(e.Action, query.Action, StringComparison.Ordinal));
		}

		if (!string.IsNullOrEmpty(query.IpAddress))
		{
			events = events.Where(e => string.Equals(e.IpAddress, query.IpAddress, StringComparison.Ordinal));
		}

		return events;
	}
}
