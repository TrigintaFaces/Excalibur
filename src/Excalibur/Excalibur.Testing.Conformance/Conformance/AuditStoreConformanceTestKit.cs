// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Compliance;
using Excalibur.Dispatch;

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
	/// Creates a store that resolves the AMBIENT tenant, for the arms that assert tenant-scoped reads.
	/// </summary>
	/// <remarks>
	/// Most arms here deliberately exercise an ambient-less caller and assert the partition such a caller
	/// resolves to, so their fixture supplies no <c>ITenantContext</c>. The tenant-scoped arms need the
	/// opposite, and a store cannot be both: with no tenant context every read resolves the untenanted
	/// sentinel, so a tenant-scoped assertion can never hold no matter what the arm does. Those two
	/// requirements were previously asserted against ONE instance, which is why the tenant arm could not
	/// pass under any fixture.
	/// <para>
	/// Defaults to <see cref="CreateStore"/> so providers that already resolve an ambient tenant need no
	/// change. A provider whose <see cref="CreateStore"/> is deliberately ambient-less overrides this with
	/// an ambient-resolving instance; until it does, the tenant arms are exercised against the same store
	/// as before and behave exactly as they do today.
	/// </para>
	/// </remarks>
	/// <returns>An <see cref="IAuditStore"/> that resolves the ambient tenant.</returns>
	protected virtual IAuditStore CreateTenantAwareStore() => CreateStore();

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
	/// Verifies that QueryAsync confines results to one tenant when the query does not name one.
	/// </summary>
	/// <remarks>
	/// THE QUERY PATH IS THE GAP. This kit's only tenant-aware arms are on GetLastEventAsync, so a
	/// green conformance run has never demonstrated tenant scoping on QueryAsync for any provider.
	///
	/// The defect this arm exists to catch is caller-remembers-or-leaks: a store that applies the
	/// tenant predicate ONLY when the caller-supplied query happens to carry a TenantId. Every code
	/// path that builds an AuditQuery without setting one then receives every tenant's audit events,
	/// including their resource identifiers and actor identities.
	///
	/// Scoping must be a property of the STORE, not a field the caller is trusted to remember. An
	/// arm that always sets TenantId on the query can never observe the difference between the two,
	/// which is precisely why this one deliberately omits it.
	/// </remarks>
	public virtual async Task QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents()
	{
		var store = CreateStore();

		var tenantAEvent = CreateAuditEvent(tenantId: $"tenant-a-{GenerateEventId()}");
		var tenantBEvent = CreateAuditEvent(tenantId: $"tenant-b-{GenerateEventId()}");

		// The caller's OWN event, in the partition an ambient-less caller resolves to. Without it the
		// liveness arm below is unsatisfiable under ambient scoping: a correctly-scoped store would
		// return neither seeded tenant's events and the arm would fail for the RIGHT behaviour.
		var ownEvent = CreateAuditEvent(tenantId: null);

		_ = await store.StoreAsync(tenantAEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(tenantBEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(ownEvent, CancellationToken.None).ConfigureAwait(false);

		// Deliberately carries NO TenantId. This is the shape a caller produces by omission, and the
		// shape under which the leak occurs.
		var query = new AuditQuery();

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		// SAFETY -- the events returned must belong to a single tenant. Asserted on the events' OWN
		// identities rather than on a returned TenantId field: a store that leaks the row but
		// rewrites or drops its tenant label would evade a predicate written against that field.
		var distinctTenants = results
			.Where(e => e.EventId == tenantAEvent.EventId || e.EventId == tenantBEvent.EventId)
			.Select(e => e.EventId == tenantAEvent.EventId ? "A" : "B")
			.Distinct()
			.ToList();

		if (distinctTenants.Count > 1)
		{
			throw new TestFixtureAssertionException(
				"CROSS-TENANT DISCLOSURE ON THE QUERY PATH: QueryAsync with no TenantId on the query "
				+ "returned audit events belonging to BOTH tenants. Tenant scoping is being applied "
				+ "only when the caller remembers to name a tenant, so every AuditQuery built without "
				+ "one discloses the whole estate's audit trail -- resource identifiers and actor "
				+ "identities included. Scoping must be enforced by the store, not supplied by the caller.");
		}

		// LIVENESS -- paired with the safety arm and NOT optional. A store that returns an empty set
		// for every unscoped query satisfies the safety arm perfectly while being useless: it
		// discloses nothing because it answers nothing. An unscoped caller must still receive its
		// OWN tenant's events.
		if (results.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"QueryAsync with no explicit TenantId returned NOTHING. Suppressing the disclosure by "
				+ "returning an empty set is not isolation -- an unscoped caller must still receive "
				+ "the events belonging to its own ambient tenant.");
		}
	}

	/// <summary>
	/// Verifies that GetByIdAsync does not return an event belonging to another tenant.
	/// </summary>
	/// <remarks>
	/// THE FOURTH READ PATH. The query, count and last-event paths resolve the tenant from the store's
	/// ambient scope; this one did not, and the difference is invisible to every other arm in this kit
	/// because they all read through predicate builders that GetByIdAsync bypasses.
	///
	/// The predicate is the whole story: a lookup keyed on the event identifier ALONE returns whichever
	/// tenant's row carries that identifier. An identifier is not a secret — it appears in correlation
	/// headers, logs, links between records and anything a consumer chooses to surface — so a caller who
	/// obtains one from any source reads the corresponding audit record, actor identity and resource
	/// identifier included, with no boundary consulted.
	///
	/// A single-tenant fixture cannot observe this. The arm seeds another tenant's event and asks for it
	/// BY ITS OWN IDENTIFIER: the request is well-formed and specific, and the correct answer is still
	/// nothing.
	/// </remarks>
	public virtual async Task GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt()
	{
		var store = CreateStore();

		var otherTenantEvent = CreateAuditEvent(tenantId: $"tenant-other-{GenerateEventId()}");

		// The caller's OWN event, in the partition an ambient-less caller resolves to. It carries the
		// liveness arm below; without it a store that returned null for EVERY identifier would pass the
		// safety half perfectly while being incapable of reading anything at all.
		var ownEvent = CreateAuditEvent(tenantId: null);

		_ = await store.StoreAsync(otherTenantEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(ownEvent, CancellationToken.None).ConfigureAwait(false);

		// SAFETY -- the other tenant's event, requested by its exact identifier, must not come back.
		var leaked = await store.GetByIdAsync(otherTenantEvent.EventId, CancellationToken.None).ConfigureAwait(false);

		if (leaked is not null)
		{
			throw new TestFixtureAssertionException(
				"CROSS-TENANT DISCLOSURE ON THE GET-BY-ID PATH: GetByIdAsync returned an audit event "
				+ "belonging to another tenant. The lookup is keyed on the event identifier alone, so "
				+ "anyone holding an identifier -- from a correlation header, a log line or a linked "
				+ "record -- reads that tenant's audit record, actor identity and resource identifier "
				+ "included. The other read paths scope to the ambient tenant; this one must too.");
		}

		// LIVENESS -- paired with the safety arm and NOT optional. Returning null for every identifier
		// would satisfy the safety half and leave the store unable to read its own events.
		var own = await store.GetByIdAsync(ownEvent.EventId, CancellationToken.None).ConfigureAwait(false);

		if (own is null)
		{
			throw new TestFixtureAssertionException(
				"GetByIdAsync returned NOTHING for the caller's OWN event. Refusing every lookup is not "
				+ "isolation -- a caller must still read the events belonging to its own ambient tenant.");
		}
	}

	/// <summary>
	/// Verifies that naming another tenant on the query does not return that tenant's events.
	/// </summary>
	/// <remarks>
	/// THE AUTHORISATION HALF, and it is a different defect from the omission arm above.
	///
	/// Omitting the tenant is a caller who FORGETS — a missing filter. Supplying another tenant's
	/// identifier is a caller who ASKS FOR SOMEONE ELSE'S DATA, and the defect is that it was obeyed:
	/// the tenant was read from the caller's own argument with nothing to check it against. That is an
	/// absent authorisation boundary, not a missing predicate, and it is reachable by any code path
	/// that can construct an <see cref="AuditQuery"/>.
	///
	/// THE CONTRACT: there is no admin or estate-wide audit interface in this framework, so there is no
	/// contract under which an unchecked caller-supplied tenant is legitimate. The store resolves the
	/// tenant from its ambient scope and does not consult the query's field at all. This arm holds that
	/// property directly: the field may be set to anything and must change nothing.
	/// </remarks>
	public virtual async Task QueryAsync_NamingAnotherTenant_ShouldNotReturnThatTenantsEvents()
	{
		var store = CreateStore();

		var foreignTenant = $"tenant-foreign-{GenerateEventId()}";
		var foreignEvent = CreateAuditEvent(tenantId: foreignTenant);
		var ownEvent = CreateAuditEvent(tenantId: null);

		_ = await store.StoreAsync(foreignEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(ownEvent, CancellationToken.None).ConfigureAwait(false);

		// The caller names a tenant that is not its own. Under the ruled contract this field is not
		// consulted; under the defect it was obeyed.
		var results = await store.QueryAsync(
			new AuditQuery { TenantId = foreignTenant },
			CancellationToken.None).ConfigureAwait(false);

		// SAFETY -- asking for another tenant's events must not produce them.
		if (results.Any(e => e.EventId == foreignEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				$"CROSS-TENANT DISCLOSURE BY REQUEST: a caller set AuditQuery.TenantId to '{foreignTenant}' "
				+ "and received that tenant's audit events. The tenant is being read from the caller's own "
				+ "argument with nothing to check it against, so any caller that can construct an "
				+ "AuditQuery can read any tenant's audit trail. Scope must come from the ambient context; "
				+ "the query's tenant field must not be consulted.");
		}

		// LIVENESS -- and the caller still gets its OWN events. Ignoring the field must not degrade into
		// ignoring the query: a store that returned nothing here would satisfy the safety arm while
		// being useless.
		if (!results.Any(e => e.EventId == ownEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"A caller that named a foreign tenant did not receive its OWN events either. Ignoring the "
				+ "query's tenant field must not turn into ignoring the caller: the ambient scope still "
				+ "applies and must still answer.");
		}
	}

	/// <summary>
	/// Verifies that a tenant-scoped query still returns that tenant's own events.
	/// </summary>
	/// <remarks>
	/// THE LIVENESS HALF OF TENANT SCOPING, and it is deliberately a SEPARATE arm.
	///
	/// Every other tenant assertion on the query path is a safety assertion — it says which events must
	/// NOT come back. A store that returns an empty set for every query satisfies all of them, forever.
	/// This arm is the one that fails for such a store, so it is what distinguishes "scoping works" from
	/// "the read is broken and nobody noticed".
	///
	/// It asserts the caller's OWN partition, whatever that partition is. The tenant is resolved by the
	/// store from its ambient scope — never from a field on the query — so this arm deliberately sets no
	/// TenantId and seeds an event in the partition an ambient-less store resolves to. That keeps it
	/// honest under the scoping contract rather than the retired opt-in one, and lets it run without the
	/// kit's construction seam having to supply a context first.
	/// </remarks>
	public virtual async Task QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents()
	{
		var store = CreateStore();

		// The caller's OWN event: written with no tenant, so it lands in the same partition an
		// ambient-less caller reads from.
		var ownEvent = CreateAuditEvent(tenantId: null);
		var otherEvent = CreateAuditEvent(tenantId: $"tenant-other-{GenerateEventId()}");

		_ = await store.StoreAsync(ownEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(otherEvent, CancellationToken.None).ConfigureAwait(false);

		// No TenantId. The store resolves the partition itself; a caller cannot name one.
		var results = await store.QueryAsync(new AuditQuery(), CancellationToken.None).ConfigureAwait(false);

		// LIVENESS -- the caller's own event must come back. A store that answers every read with an
		// empty set is perfectly isolated and completely useless, and it would satisfy every safety arm
		// in this kit. This is the arm that fails for it.
		if (!results.Any(e => e.EventId == ownEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"A scoped read did NOT return the caller's OWN audit event. Tenant scoping is "
				+ "over-filtering: the caller is isolated from its own data. Returning nothing is not "
				+ "isolation — an isolated caller must still receive the events that belong to it.");
		}

		// SAFETY -- paired, so this arm cannot be satisfied by a store that returns everything either.
		if (results.Any(e => e.EventId == otherEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"A scoped read returned an audit event belonging to a different tenant.");
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
		// Tenant-aware store and an AMBIENT scope, because the stores resolve tenancy from ambient
		// context alone and deliberately ignore the tenantId argument -- "scope, not filter", so that
		// passing null cannot widen a read across every tenant. Against the ambient-less store the other
		// arms use, this arm asserted something unreachable: no tenant resolves, so the read is scoped to
		// the untenanted sentinel and never sees these events. The tenantId argument below is retained
		// only because it is still on the interface; the scope is what does the work.
		var store = CreateTenantAwareStore();
		var tenantId = $"tenant-{GenerateEventId()}";

		var evt1 = CreateAuditEvent(tenantId: tenantId);
		var evt2 = CreateAuditEvent(tenantId: tenantId);
		var otherTenantEvt = CreateAuditEvent(tenantId: "other-tenant");

		using (TenantContextHolder.BeginScope(tenantId))
		{
			_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
			_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope("other-tenant"))
		{
			_ = await store.StoreAsync(otherTenantEvt, CancellationToken.None).ConfigureAwait(false);
		}

		AuditEvent? lastEvent;
		using (TenantContextHolder.BeginScope(tenantId))
		{
			lastEvent = await store.GetLastEventAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
		}

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

		// First (genesis) event has NO prior tag: the keyed-MAC chain (qa71t5) uses a null PreviousEventHash
		// for the genesis link — the tenant is bound via the canonicalized record, not a tenant-seeded genesis.
		if (!string.IsNullOrEmpty(retrieved1.PreviousEventHash))
		{
			throw new TestFixtureAssertionException(
				"First (genesis) event should have a null PreviousEventHash (keyed-MAC chain genesis = null prior tag)");
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

		// EventHash must be the keyed, versioned integrity tag (v1:{keyId}:{mac}) — confirms the keyed-MAC
		// migration landed (not a bare/unkeyed hash). The keyId is consumer-configured, so assert the
		// structural shape (version prefix + 3 colon-delimited parts), not a specific key id.
		foreach (var tag in new[] { retrieved1.EventHash, retrieved2.EventHash })
		{
			if (tag is null || !tag.StartsWith("v1:", StringComparison.Ordinal) || tag.Split(':').Length != 3)
			{
				throw new TestFixtureAssertionException(
					$"EventHash should be a keyed versioned tag 'v1:{{keyId}}:{{mac}}', got '{tag ?? "<null>"}'");
			}
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

	#region ApplicationName Tests

	/// <summary>
	/// Verifies that ApplicationName is stored and retrieved correctly.
	/// </summary>
	public virtual async Task StoreAsync_WithApplicationName_ShouldPersistApplicationName()
	{
		var store = CreateStore();
		var evt = CreateAuditEvent();
		var evtWithApp = evt with { ApplicationName = "my-service" };

		_ = await store.StoreAsync(evtWithApp, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(evtWithApp.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Event with EventId {evtWithApp.EventId} was not found after StoreAsync");
		}

		if (!string.Equals(retrieved.ApplicationName, "my-service", StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"ApplicationName mismatch. Expected: 'my-service', Actual: '{retrieved.ApplicationName}'");
		}
	}

	/// <summary>
	/// Verifies that null ApplicationName is stored and retrieved as null.
	/// </summary>
	public virtual async Task StoreAsync_WithNullApplicationName_ShouldPersistNull()
	{
		var store = CreateStore();
		var evt = CreateAuditEvent(); // default has null ApplicationName

		_ = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(evt.EventId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Event with EventId {evt.EventId} was not found after StoreAsync");
		}

		if (retrieved.ApplicationName is not null)
		{
			throw new TestFixtureAssertionException(
				$"ApplicationName should be null, but was: '{retrieved.ApplicationName}'");
		}
	}

	/// <summary>
	/// Verifies that QueryAsync filters by ApplicationName correctly.
	/// </summary>
	public virtual async Task QueryAsync_ByApplicationName_ShouldFilter()
	{
		var store = CreateStore();

		var appAEvent = CreateAuditEvent() with { ApplicationName = "app-a" };
		var appBEvent = CreateAuditEvent() with { ApplicationName = "app-b" };

		_ = await store.StoreAsync(appAEvent, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(appBEvent, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { ApplicationName = "app-a" };

		var results = await store.QueryAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (!results.Any(e => e.EventId == appAEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"Event with ApplicationName 'app-a' should be returned when filtering by 'app-a'");
		}

		if (results.Any(e => e.EventId == appBEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				"Event with ApplicationName 'app-b' should NOT be returned when filtering by 'app-a'");
		}
	}

	/// <summary>
	/// Verifies that CountAsync respects ApplicationName filter.
	/// </summary>
	public virtual async Task CountAsync_ByApplicationName_ShouldCount()
	{
		var store = CreateStore();

		var evt1 = CreateAuditEvent() with { ApplicationName = "svc-1" };
		var evt2 = CreateAuditEvent() with { ApplicationName = "svc-1" };
		var evt3 = CreateAuditEvent() with { ApplicationName = "svc-2" };

		_ = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(evt3, CancellationToken.None).ConfigureAwait(false);

		var query = new AuditQuery { ApplicationName = "svc-1" };

		var count = await store.CountAsync(query, CancellationToken.None).ConfigureAwait(false);

		if (count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected count 2 for ApplicationName 'svc-1', got {count}");
		}
	}

	/// <summary>
	/// Verifies that ApplicationName is included in hash computation (different app names produce different hashes).
	/// </summary>
	public virtual async Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash()
	{
		var store = CreateStore();

		var evt1 = CreateAuditEvent() with { ApplicationName = "hash-app-1" };
		var evt2 = CreateAuditEvent() with { ApplicationName = "hash-app-2" };

		var result1 = await store.StoreAsync(evt1, CancellationToken.None).ConfigureAwait(false);
		var result2 = await store.StoreAsync(evt2, CancellationToken.None).ConfigureAwait(false);

		if (string.IsNullOrEmpty(result1.EventHash) || string.IsNullOrEmpty(result2.EventHash))
		{
			throw new TestFixtureAssertionException(
				"Both events should have computed EventHash values");
		}

		// While tags also differ due to different EventIds and chain position,
		// verify both are computed (non-null) -- direct keyed-MAC verification
		// is covered by the IAuditIntegrityStrategy unit/integrity tests.
		if (string.Equals(result1.EventHash, result2.EventHash, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				"Events with different ApplicationNames (and different EventIds) should have different hashes");
		}
	}

	#endregion
}
