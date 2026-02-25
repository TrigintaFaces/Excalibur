// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IAuditStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your audit store implementation conforms to the IAuditStore contract.
/// </para>
/// <para>
/// The test kit verifies core audit store operations including store, retrieval,
/// query, count, chain integrity verification, and multi-tenant isolation.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IAuditStore implements hash chain integrity
/// for tamper-evident audit logging required by SOC2 and regulatory compliance:
/// <list type="bullet">
/// <item><description><c>StoreAsync</c> automatically links events via PreviousEventHash and computes EventHash</description></item>
/// <item><description><c>StoreAsync</c> THROWS InvalidOperationException on duplicate EventId (not upsert)</description></item>
/// <item><description><c>VerifyChainIntegrityAsync</c> detects any tampering with audit records</description></item>
/// <item><description>Multi-tenant isolation via TenantId with "_default_" for null tenant</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerAuditStoreConformanceTests : AuditStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IAuditStore CreateStore() =&gt;
///         new SqlServerAuditStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class AuditStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh audit store instance for testing.
	/// </summary>
	/// <returns>An IAuditStore implementation to test.</returns>
	protected abstract IAuditStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a test audit event with the given parameters.
	/// </summary>
	/// <param name="eventId">Optional event identifier. If not provided, a new GUID is generated.</param>
	/// <param name="eventType">Optional event type. Default is DataAccess.</param>
	/// <param name="actorId">Optional actor identifier.</param>
	/// <param name="tenantId">Optional tenant identifier for multi-tenant isolation.</param>
	/// <param name="timestamp">Optional timestamp. Default is UtcNow.</param>
	/// <returns>A test audit event.</returns>
	protected virtual AuditEvent CreateAuditEvent(
		string? eventId = null,
		AuditEventType? eventType = null,
		string? actorId = null,
		string? tenantId = null,
		DateTimeOffset? timestamp = null) =>
		new()
		{
			EventId = eventId ?? GenerateEventId(),
			EventType = eventType ?? AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
			ActorId = actorId ?? "test-actor",
			TenantId = tenantId,
		};

	/// <summary>
	/// Generates a unique event ID for test isolation.
	/// </summary>
	/// <returns>A unique event identifier.</returns>
	protected virtual string GenerateEventId() => Guid.NewGuid().ToString("N");

	#region Store Tests

	/// <summary>
	/// Verifies that storing a new event persists it successfully.
	/// </summary>
	public virtual async Task StoreAsync_ShouldPersistEvent()
	{
		var store = CreateStore();
		var evt = CreateAuditEvent();

		var result = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		if (result.EventId != evt.EventId)
		{
			throw new TestFixtureAssertionException(
				$"EventId mismatch in result. Expected: {evt.EventId}, Actual: {result.EventId}");
		}

		if (string.IsNullOrEmpty(result.EventHash))
		{
			throw new TestFixtureAssertionException(
				"EventHash should be computed and returned in AuditEventId");
		}

		var retrieved = await store.GetByIdAsync(evt.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Event with EventId {evt.EventId} was not found after StoreAsync");
		}

		if (retrieved.Action != evt.Action)
		{
			throw new TestFixtureAssertionException(
				$"Action mismatch. Expected: {evt.Action}, Actual: {retrieved.Action}");
		}
	}

	/// <summary>
	/// Verifies that storing a null event throws ArgumentNullException.
	/// </summary>
	public virtual async Task StoreAsync_WithNullEvent_ShouldThrow()
	{
		var store = CreateStore();

		try
		{
			_ = await store.StoreAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentNullException but no exception was thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that storing an event with duplicate ID throws InvalidOperationException.
	/// </summary>
	public virtual async Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException()
	{
		var store = CreateStore();
		var eventId = GenerateEventId();
		var evt1 = CreateAuditEvent(eventId: eventId);
		var evt2 = CreateAuditEvent(eventId: eventId);

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);

		try
		{
			_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected InvalidOperationException for duplicate EventId but no exception was thrown");
		}
		catch (InvalidOperationException)
		{
			// Expected - StoreAsync throws on duplicate, NOT upsert
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetByIdAsync returns the event when it exists.
	/// </summary>
	public virtual async Task GetByIdAsync_ExistingEvent_ShouldReturnEvent()
	{
		var store = CreateStore();
		var evt = CreateAuditEvent();

		_ = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(evt.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"GetByIdAsync should return event for EventId {evt.EventId}");
		}

		if (retrieved.EventId != evt.EventId)
		{
			throw new TestFixtureAssertionException(
				$"EventId mismatch. Expected: {evt.EventId}, Actual: {retrieved.EventId}");
		}
	}

	/// <summary>
	/// Verifies that GetByIdAsync returns null for non-existent event.
	/// </summary>
	public virtual async Task GetByIdAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var nonExistentId = GenerateEventId();

		var retrieved = await store.GetByIdAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is not null)
		{
			throw new TestFixtureAssertionException(
				"GetByIdAsync should return null for non-existent EventId");
		}
	}

	/// <summary>
	/// Verifies that GetByIdAsync throws for null or empty eventId.
	/// </summary>
	public virtual async Task GetByIdAsync_NullOrEmpty_ShouldThrow()
	{
		var store = CreateStore();

		try
		{
			_ = await store.GetByIdAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentException for null eventId but no exception was thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}

		try
		{
			_ = await store.GetByIdAsync("", CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentException for empty eventId but no exception was thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	#endregion

	#region Query Tests

	/// <summary>
	/// Verifies that QueryAsync filters by date range correctly.
	/// </summary>
	public virtual async Task QueryAsync_ByDateRange_ShouldReturnMatching()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		var oldEvent = CreateAuditEvent(timestamp: now.AddDays(-10));
		var recentEvent = CreateAuditEvent(timestamp: now.AddMinutes(-5));

		_ = await store.StoreAsync(oldEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(recentEvent, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { StartDate = now.AddDays(-1), EndDate = now.AddDays(1) };

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(e => e.EventId == recentEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"Recent event should be returned within date range");
		}

		if (results.Any(e => e.EventId == oldEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"Old event should NOT be returned outside date range");
		}
	}

	/// <summary>
	/// Verifies that QueryAsync filters by event type correctly.
	/// </summary>
	public virtual async Task QueryAsync_ByEventType_ShouldFilter()
	{
		var store = CreateStore();

		var authEvent = CreateAuditEvent(eventType: AuditEventType.Authentication);
		var dataEvent = CreateAuditEvent(eventType: AuditEventType.DataAccess);

		_ = await store.StoreAsync(authEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(dataEvent, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { EventTypes = [AuditEventType.Authentication] };

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(e => e.EventId == authEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"Authentication event should be returned when filtering by Authentication type");
		}

		if (results.Any(e => e.EventId == dataEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"DataAccess event should NOT be returned when filtering by Authentication type");
		}
	}

	/// <summary>
	/// Verifies that QueryAsync filters by actorId correctly.
	/// </summary>
	public virtual async Task QueryAsync_ByActorId_ShouldFilter()
	{
		var store = CreateStore();

		var actor1Event = CreateAuditEvent(actorId: "actor-1");
		var actor2Event = CreateAuditEvent(actorId: "actor-2");

		_ = await store.StoreAsync(actor1Event, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(actor2Event, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { ActorId = "actor-1" };

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(e => e.EventId == actor1Event.EventId))
		{
			throw new TestFixtureAssertionException(
				"Event from actor-1 should be returned when filtering by actor-1");
		}

		if (results.Any(e => e.EventId == actor2Event.EventId))
		{
			throw new TestFixtureAssertionException(
				"Event from actor-2 should NOT be returned when filtering by actor-1");
		}
	}

	/// <summary>
	/// Verifies that QueryAsync respects pagination parameters.
	/// </summary>
	public virtual async Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults()
	{
		var store = CreateStore();

		// Store 5 events
		for (var i = 0; i < 5; i++)
		{
			var evt = CreateAuditEvent();
			_ = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);
		}

		var query = new AuditQuery { MaxResults = 2, Skip = 1 };

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (results.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected 2 results with MaxResults=2, got {results.Count}");
		}
	}

	#endregion

	#region Count Tests

	/// <summary>
	/// Verifies that CountAsync returns correct count with filters.
	/// </summary>
	public virtual async Task CountAsync_WithFilters_ShouldReturnCount()
	{
		var store = CreateStore();

		var authEvent1 = CreateAuditEvent(eventType: AuditEventType.Authentication);
		var authEvent2 = CreateAuditEvent(eventType: AuditEventType.Authentication);
		var dataEvent = CreateAuditEvent(eventType: AuditEventType.DataAccess);

		_ = await store.StoreAsync(authEvent1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(authEvent2, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(dataEvent, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { EventTypes = [AuditEventType.Authentication] };

		var count = await store.CountAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected count 2 for Authentication events, got {count}");
		}
	}

	/// <summary>
	/// Verifies that CountAsync returns zero for empty result.
	/// </summary>
	public virtual async Task CountAsync_EmptyResult_ShouldReturnZero()
	{
		var store = CreateStore();

		var query = new AuditQuery
		{
			EventTypes = [AuditEventType.Security] // No security events stored
		};

		var count = await store.CountAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected count 0 for non-matching filter, got {count}");
		}
	}

	#endregion

	#region Integrity Tests (Compliance-Critical)

	/// <summary>
	/// Verifies that VerifyChainIntegrityAsync returns valid for a valid chain.
	/// </summary>
	public virtual async Task VerifyChainIntegrityAsync_ValidChain_ShouldReturnValid()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		// Store multiple events to create a chain
		var evt1 = CreateAuditEvent(timestamp: now.AddMinutes(-3));
		var evt2 = CreateAuditEvent(timestamp: now.AddMinutes(-2));
		var evt3 = CreateAuditEvent(timestamp: now.AddMinutes(-1));

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt3, CancellationToken.None).ConfigureAwait(false);

		var result = await store.VerifyChainIntegrityAsync(
			now.AddHours(-1),
			now.AddHours(1),
			CancellationToken.None).ConfigureAwait(false);

		if (!result.IsValid)
		{
			throw new TestFixtureAssertionException(
				$"Chain integrity should be valid. Violation: {result.ViolationDescription}");
		}

		if (result.EventsVerified < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 events verified, got {result.EventsVerified}");
		}
	}

	/// <summary>
	/// Verifies that VerifyChainIntegrityAsync returns valid with zero events for empty range.
	/// </summary>
	public virtual async Task VerifyChainIntegrityAsync_EmptyRange_ShouldReturnValidWithZeroEvents()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		// Store event outside verification range
		var evt = CreateAuditEvent(timestamp: now.AddDays(-10));
		_ = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		var result = await store.VerifyChainIntegrityAsync(
			now.AddDays(-1),
			now.AddDays(1),
			CancellationToken.None).ConfigureAwait(false);

		if (!result.IsValid)
		{
			throw new TestFixtureAssertionException(
				"Chain integrity should be valid for empty range");
		}

		if (result.EventsVerified != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 events verified for empty range, got {result.EventsVerified}");
		}
	}

	#endregion

	#region LastEvent Tests

	/// <summary>
	/// Verifies that GetLastEventAsync returns last event for specific tenant.
	/// </summary>
	public virtual async Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant()
	{
		var store = CreateStore();
		var tenantId = $"tenant-{GenerateEventId()}";

		var evt1 = CreateAuditEvent(tenantId: tenantId);
		var evt2 = CreateAuditEvent(tenantId: tenantId);
		var otherTenantEvt = CreateAuditEvent(tenantId: "other-tenant");

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(otherTenantEvt, CancellationToken.None).ConfigureAwait(false);

		var lastEvent = await store.GetLastEventAsync(tenantId, CancellationToken.None).ConfigureAwait(false);

		if (lastEvent is null)
		{
			throw new TestFixtureAssertionException(
				"GetLastEventAsync should return last event for tenant");
		}

		if (lastEvent.EventId != evt2.EventId)
		{
			throw new TestFixtureAssertionException(
				$"Expected last event to be {evt2.EventId}, got {lastEvent.EventId}");
		}

		if (lastEvent.TenantId != tenantId)
		{
			throw new TestFixtureAssertionException(
				$"Last event should belong to tenant {tenantId}");
		}
	}

	/// <summary>
	/// Verifies that GetLastEventAsync with null tenant returns last event for default tenant.
	/// </summary>
	public virtual async Task GetLastEventAsync_DefaultTenant_ShouldReturnLast()
	{
		var store = CreateStore();

		// Events with null TenantId go to "_default_" tenant
		var evt1 = CreateAuditEvent(tenantId: null);
		var evt2 = CreateAuditEvent(tenantId: null);

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);

		var lastEvent = await store.GetLastEventAsync(null, CancellationToken.None).ConfigureAwait(false);

		if (lastEvent is null)
		{
			throw new TestFixtureAssertionException(
				"GetLastEventAsync should return last event for default tenant");
		}

		if (lastEvent.EventId != evt2.EventId)
		{
			throw new TestFixtureAssertionException(
				$"Expected last event to be {evt2.EventId}, got {lastEvent.EventId}");
		}
	}

	#endregion

	#region Hash Chain Tests

	/// <summary>
	/// Verifies that StoreAsync sets PreviousEventHash for chain linking.
	/// </summary>
	public virtual async Task StoreAsync_ShouldSetPreviousEventHash()
	{
		var store = CreateStore();

		var evt1 = CreateAuditEvent();
		var evt2 = CreateAuditEvent();

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);

		var retrieved1 = await store.GetByIdAsync(evt1.EventId, CancellationToken.None).ConfigureAwait(false);
		var retrieved2 = await store.GetByIdAsync(evt2.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved1 is null || retrieved2 is null)
		{
			throw new TestFixtureAssertionException(
				"Both events should be retrievable");
		}

		// First event should have genesis hash as previous
		if (string.IsNullOrEmpty(retrieved1.PreviousEventHash))
		{
			throw new TestFixtureAssertionException(
				"First event should have PreviousEventHash set (genesis hash)");
		}

		// Second event should link to first event's hash
		if (string.IsNullOrEmpty(retrieved2.PreviousEventHash))
		{
			throw new TestFixtureAssertionException(
				"Second event should have PreviousEventHash set");
		}

		if (retrieved2.PreviousEventHash != retrieved1.EventHash)
		{
			throw new TestFixtureAssertionException(
				"Second event's PreviousEventHash should equal first event's EventHash");
		}
	}

	/// <summary>
	/// Verifies that StoreAsync computes and stores EventHash.
	/// </summary>
	public virtual async Task StoreAsync_ShouldComputeEventHash()
	{
		var store = CreateStore();
		var evt = CreateAuditEvent();

		var result = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		if (string.IsNullOrEmpty(result.EventHash))
		{
			throw new TestFixtureAssertionException(
				"StoreAsync result should include computed EventHash");
		}

		var retrieved = await store.GetByIdAsync(evt.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Event should be retrievable after store");
		}

		if (retrieved.EventHash != result.EventHash)
		{
			throw new TestFixtureAssertionException(
				"Stored event's EventHash should match returned EventHash");
		}
	}

	#endregion
}
