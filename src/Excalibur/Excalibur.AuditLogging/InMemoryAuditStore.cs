// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Compliance;
using Excalibur.Dispatch;

namespace Excalibur.AuditLogging;

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
internal sealed class InMemoryAuditStore : IAuditStore, IDisposable
{
	private readonly ConcurrentDictionary<string, AuditEvent> _eventsById = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, List<AuditEvent>> _eventsByTenant = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _sequenceSemaphore = new(1, 1);
	private readonly IAuditIntegrityStrategy _integrity;
	private readonly ITenantContext? _tenantContext;
	private long _sequenceNumber;
	private volatile bool _disposed;

	/// <summary>
	/// The partition key for events carrying no tenant. This is the store's own internal partition label,
	/// deliberately distinct from a real tenant identifier so an untenanted event can never share a
	/// partition with one.
	/// </summary>
	private const string UntenantedPartitionKey = "_default_";

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryAuditStore"/> class.
	/// </summary>
	/// <param name="integrity">The shared audit-integrity strategy (keyed-MAC + hash-chain).</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> when multi-tenancy is not registered. Reads are
	/// scoped to the tenant this resolves — never to a tenant named by the caller's query.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="integrity"/> is null.</exception>
	public InMemoryAuditStore(IAuditIntegrityStrategy integrity, ITenantContext? tenantContext = null)
	{
		_integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Resolves the partition every read is confined to. The tenant is a <em>scope</em> taken from ambient
	/// context, not a filter supplied by the caller: a caller cannot widen it by omitting
	/// <see cref="AuditQuery.TenantId"/>, nor redirect it by naming another tenant.
	/// </summary>
	/// <returns>The tenant partition key for the ambient tenant, or the untenanted partition.</returns>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is registered but resolves no tenant — the read fails closed rather than widening to
	/// every partition.
	/// </exception>
	private string ResolveTenantKey()
	{
		var scope = TenantScope.FromContext(_tenantContext);

		return scope.IsScoped ? scope.TenantId! : UntenantedPartitionKey;
	}

	/// <inheritdoc />
	public async Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);
		cancellationToken.ThrowIfCancellationRequested();

		var tenantKey = auditEvent.TenantId ?? UntenantedPartitionKey;

		// Async-safe critical section: the keyed-MAC tag computation is async, so the chain-ordering
		// invariant (read prior tag -> compute this tag -> append) is held under a SemaphoreSlim, not a
		// lock (you cannot await inside a lock). Serializing here keeps concurrent appends from forking the chain.
		await _sequenceSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_sequenceNumber++;
			var sequenceNumber = _sequenceNumber;

			// Prior tag for chain linking (null = genesis; the keyed MAC over null-prior + tenant-bearing
			// canonical content makes a cross-tenant splice fail verification — qa71t5).
			var previousHash = GetPreviousHash(tenantKey);

			// Keyed integrity tag for this event.
			var eventHash = await _integrity.ComputeTagAsync(
				AuditEventCanonicalizer.Canonicalize(auditEvent), previousHash, cancellationToken).ConfigureAwait(false);

			// Create stored event with tag and chain link
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

			return new AuditEventId
			{
				EventId = storedEvent.EventId,
				EventHash = eventHash,
				SequenceNumber = sequenceNumber,
				RecordedAt = DateTimeOffset.UtcNow
			};
		}
		finally
		{
			_ = _sequenceSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		cancellationToken.ThrowIfCancellationRequested();

		// A lookup by primary key is still a tenant-scoped read. The identifier alone does not authorise the
		// row: without this check a caller holding an event id obtained from anywhere — a log line, an export,
		// a correlation trail — reads another tenant's audit record verbatim. The by-id index is deliberately
		// flat (ids are unique across tenants and StoreAsync relies on that to reject duplicates), so the
		// scope is applied on the way out, against the same partition key the query paths resolve.
		if (!_eventsById.TryGetValue(eventId, out var auditEvent))
		{
			return Task.FromResult<AuditEvent?>(null);
		}

		var partitionKey = auditEvent.TenantId ?? UntenantedPartitionKey;

		return Task.FromResult(
			string.Equals(partitionKey, ResolveTenantKey(), StringComparison.Ordinal) ? auditEvent : null);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(query);
		cancellationToken.ThrowIfCancellationRequested();

		// SECURITY: the partition is resolved from AMBIENT context, never from query.TenantId. Reading the
		// caller's field made tenancy opt-in — omitting it returned every tenant's events, and naming
		// another tenant returned theirs. Both are closed by never consulting it here.
		IEnumerable<AuditEvent> events;

		if (!_eventsByTenant.TryGetValue(ResolveTenantKey(), out var tenantEvents))
		{
			return Task.FromResult<IReadOnlyList<AuditEvent>>([]);
		}

		lock (tenantEvents)
		{
			events = tenantEvents.ToList();
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

		// SECURITY: a count is a disclosure — an estate-wide count tells one tenant how much audit activity
		// every other tenant has. Scoped from ambient context exactly as the read is, and for the same reason.
		IEnumerable<AuditEvent> events;

		if (!_eventsByTenant.TryGetValue(ResolveTenantKey(), out var tenantEvents))
		{
			return Task.FromResult(0L);
		}

		lock (tenantEvents)
		{
			events = tenantEvents.ToList();
		}

		events = ApplyFilters(events, query);
		return Task.FromResult((long)events.Count());
	}

	/// <inheritdoc />
	public async Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
		DateTimeOffset startDate,
		DateTimeOffset endDate,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Chain verification is a tenant-scoped read like every other read path. The flat by-id index spans
		// all tenants, so reading it here would report another tenant's event count and leak the identifier
		// of their first violating event through firstViolationEventId. Scope against the same partition,
		// resolved by the same ResolveTenantKey() the query and count paths already use.
		List<AuditEvent> tenantSnapshot;

		if (!_eventsByTenant.TryGetValue(ResolveTenantKey(), out var tenantEvents))
		{
			return AuditIntegrityResult.Valid(0, startDate, endDate);
		}

		lock (tenantEvents)
		{
			tenantSnapshot = tenantEvents.ToList();
		}

		// Get all events in the date range, ordered by timestamp
		var events = tenantSnapshot
			.Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
			.OrderBy(e => e.Timestamp)
			.ThenBy(e => e.EventId, StringComparer.Ordinal)
			.ToList();

		if (events.Count == 0)
		{
			return AuditIntegrityResult.Valid(0, startDate, endDate);
		}

		var violationCount = 0;
		string? firstViolationEventId = null;
		string? violationDescription = null;

		// Verify each event's keyed tag against its stored prior tag (a missing tag is itself a violation).
		foreach (var currentEvent in events)
		{
			var verified = currentEvent.EventHash is not null
				&& await _integrity.VerifyAsync(
					AuditEventCanonicalizer.Canonicalize(currentEvent),
					currentEvent.PreviousEventHash,
					currentEvent.EventHash,
					cancellationToken).ConfigureAwait(false);

			if (!verified)
			{
				violationCount++;
				firstViolationEventId ??= currentEvent.EventId;
				violationDescription ??= $"Integrity tag mismatch for event '{currentEvent.EventId}' at {currentEvent.Timestamp:O}";
			}
		}

		if (violationCount > 0)
		{
			return AuditIntegrityResult.Invalid(
				events.Count,
				startDate,
				endDate,
				firstViolationEventId!,
				violationDescription!,
				violationCount);
		}

		return AuditIntegrityResult.Valid(events.Count, startDate, endDate);
	}

	/// <inheritdoc />
	public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var tenantKey = tenantId ?? UntenantedPartitionKey;

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
		_sequenceSemaphore.Wait();
		try
		{
			_eventsById.Clear();
			_eventsByTenant.Clear();
			_sequenceNumber = 0;
		}
		finally
		{
			_ = _sequenceSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_sequenceSemaphore.Dispose();
	}

	/// <summary>
	/// Gets the total count of events in the store.
	/// </summary>
	public int Count => _eventsById.Count;

	private string? GetPreviousHash(string tenantKey)
	{
		// Genesis is a null prior tag (qa71t5): no separate genesis seed is needed because each link's keyed
		// MAC already covers the tenant via the canonical content, so a cross-tenant splice fails verification.
		if (!_eventsByTenant.TryGetValue(tenantKey, out var tenantEvents))
		{
			return null;
		}

		lock (tenantEvents)
		{
			return tenantEvents.LastOrDefault()?.EventHash;
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

		if (!string.IsNullOrEmpty(query.ApplicationName))
		{
			events = events.Where(e => string.Equals(e.ApplicationName, query.ApplicationName, StringComparison.Ordinal));
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
