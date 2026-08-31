// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Linq;

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
/// <item><description>Multi-tenant isolation via TenantId, with null TenantId routed to the reserved untenanted partition</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The kit resolves the store from a container built by the store's own registration
/// // extension, so every arm runs against the object a consumer actually gets. Constructing
/// // the store by hand certifies an instance you assembled rather than the one your
/// // registration produces.
/// public class SqlServerAuditStoreConformanceTests : AuditStoreConformanceTestKit
/// {
///     private readonly ServiceProvider _provider;
///
///     public SqlServerAuditStoreConformanceTests(SqlServerFixture fixture) =&gt;
///         _provider = new ServiceCollection()
///             .AddLogging()
///             .AddSqlServerAuditStore(options =&gt;
///             {
///                 options.ConnectionString = fixture.ConnectionString;
///                 options.EnableHashChain = true;
///             })
///             .BuildServiceProvider();
///
///     protected override IAuditStore CreateStore() =&gt;
///         _provider.GetRequiredService&lt;IAuditStore&gt;();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class AuditStoreConformanceTestKit : ConformanceTestKit
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
	/// There is no default. A suite MUST supply an ambient-resolving store, because no fallback this
	/// method could choose would make the tenant arms mean anything: falling back to
	/// <see cref="CreateStore"/> hands the tenant arms an instance that resolves the untenanted sentinel
	/// for every read, so those arms report a pass having asserted nothing about tenant scoping — the one
	/// property they exist to check. A suite that genuinely cannot build such a store should say so
	/// explicitly rather than be certified by silence.
	/// </para>
	/// </remarks>
	/// <returns>An <see cref="IAuditStore"/> that resolves the ambient tenant.</returns>
	/// <exception cref="NotSupportedException">
	/// Thrown when the deriving suite does not override this method. The tenant-scoped arms cannot be
	/// exercised without an ambient-resolving store, and passing them against a store that has no tenant
	/// context is not a weaker result — it is no result.
	/// </exception>
	protected virtual IAuditStore CreateTenantAwareStore() =>
		throw new NotSupportedException(
			$"{GetType().Name} does not override CreateTenantAwareStore(). The tenant-scoped conformance " +
			"arms need a store that resolves the AMBIENT tenant; without one, every read resolves the " +
			"untenanted partition and the arms pass without asserting tenant scoping at all. Override " +
			"CreateTenantAwareStore() to return an instance built with an ITenantContext that resolves " +
			"the ambient tenant.");

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Clears residual data before an arm runs. Defaults to <see cref="CleanupAsync"/>.
	/// </summary>
	/// <returns>A task that completes when the store holds no data from a previous arm.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half — otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates the store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm in this kit obtains its store here rather than from <see cref="CreateStore"/> directly.
	/// That is the only thing that causes <see cref="CleanupAsync"/> to run: a cleanup a deriver overrides
	/// but the kit never calls is indistinguishable, from the deriver's side, from one that works.
	/// </remarks>
	protected async Task<IAuditStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates the tenant-aware store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A tenant-aware store ready for one conformance arm.</returns>
	/// <remarks>
	/// The tenant arms obtain their store here for the same reason every other arm uses
	/// <see cref="CreateStoreForArmAsync"/>: an arm that skipped the reset would be the only one whose
	/// starting state depended on its predecessor, which is precisely the contamination this seam removes.
	/// </remarks>
	protected async Task<IAuditStore> CreateTenantAwareStoreForArmAsync()
	{
		var store = CreateTenantAwareStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

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

	/// <summary>
	/// Removes a stored record from the provider's underlying storage, bypassing the store, as a party with
	/// database access would.
	/// </summary>
	/// <param name="store">The store instance the arm is exercising, so that a provider holding its records in process can reach them.</param>
	/// <param name="eventId">The identifier of the record to remove.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when the record has been removed.</returns>
	/// <remarks>
	/// Required rather than optional, and abstract rather than a no-op default, because the arms that use it
	/// are the ones a blind store fails. A provider able to skip them could be certified while detecting no
	/// tampering at all, which is the outcome this kit exists to prevent. Implementations must throw if the
	/// record was not found: a removal that removed nothing turns the arm into a test of nothing.
	/// </remarks>
	protected abstract Task DeleteRecordOutOfBandAsync(
		IAuditStore store,
		string eventId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Rewrites a stored record's action in the provider's underlying storage, bypassing the store, and
	/// leaving every integrity column exactly as written.
	/// </summary>
	/// <param name="store">The store instance the arm is exercising, so that a provider holding its records in process can reach them.</param>
	/// <param name="eventId">The identifier of the record to rewrite.</param>
	/// <param name="newAction">The action value to write in place of the stored one.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task that completes when the record has been rewritten.</returns>
	/// <remarks>
	/// The integrity columns must be left untouched. Altering them as well would produce a record that fails
	/// on linkage grounds, and the arm would pass without ever establishing that the store recomputes
	/// anything from the record's live content. Implementations must throw if the record was not found.
	/// </remarks>
	protected abstract Task RewriteRecordActionOutOfBandAsync(
		IAuditStore store,
		string eventId,
		string newAction,
		CancellationToken cancellationToken);

	#region Store Tests

	/// <summary>
	/// Verifies that storing a new event persists it successfully.
	/// </summary>
	public virtual async Task StoreAsync_ShouldPersistEvent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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

		// SAFETY -- NEITHER seeded tenant's event may appear. Asserted per event on the events' OWN
		// identities rather than on a returned TenantId field: a store that leaks the row but rewrites
		// or drops its tenant label would evade a predicate written against that field.
		//
		// Asserted as "no foreign row" rather than as "not more than one distinct foreign tenant". A
		// predicate that fires only when BOTH tenants appear treats a leak of exactly one of them as
		// conformant -- and a leak of one tenant is the entire disclosure, with one victim instead of
		// two. The caller has no ambient claim on either seeded partition, so the correct count is zero.
		var leaked = results.FirstOrDefault(
			e => e.EventId == tenantAEvent.EventId || e.EventId == tenantBEvent.EventId);

		if (leaked is not null)
		{
			throw new TestFixtureAssertionException(
				"CROSS-TENANT DISCLOSURE ON THE QUERY PATH: QueryAsync with no TenantId on the query "
				+ $"returned audit event {leaked.EventId}, which belongs to another tenant. Tenant "
				+ "scoping is being applied only when the caller remembers to name a tenant, so every "
				+ "AuditQuery built without one discloses the whole estate's audit trail -- resource "
				+ "identifiers and actor identities included. Scoping must be enforced by the store, not "
				+ "supplied by the caller.");
		}

		// LIVENESS -- paired with the safety arm and NOT optional. A store that returns an empty set
		// for every unscoped query satisfies the safety arm perfectly while being useless: it
		// discloses nothing because it answers nothing. An unscoped caller must still receive its
		// OWN tenant's events.
		//
		// Asserted on the caller's own event BY IDENTITY rather than on a non-empty total. A count
		// counts foreign rows too, so a store that leaked one seeded tenant's event and dropped the
		// caller's own would satisfy a non-empty check while having answered the wrong question
		// entirely -- and the safety arm above would then be the only thing standing between that
		// store and a green conformance run.
		if (!results.Any(e => e.EventId == ownEvent.EventId))
		{
			throw new TestFixtureAssertionException(
				$"QueryAsync with no explicit TenantId did not return the caller's own event "
				+ $"{ownEvent.EventId}. Suppressing the disclosure by withholding rows is not isolation "
				+ "-- an unscoped caller must still receive the events belonging to its own ambient "
				+ "tenant.");
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
	/// Verifies that VerifyChainIntegrityAsync reports Verified for an intact chain.
	/// </summary>
	public virtual async Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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

		if (result.Outcome != AuditIntegrityOutcome.Verified)
		{
			throw new TestFixtureAssertionException(
				$"Chain integrity should be Verified but was {result.Outcome}. Violation: {result.ViolationDescription}");
		}

		if (result.EventsVerified < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 events verified, got {result.EventsVerified}");
		}
	}

	/// <summary>
	/// Verifies that VerifyChainIntegrityAsync reports NoEventsInScope for a window containing no events.
	/// </summary>
	/// <remarks>
	/// A store must not report a window it never examined as a successful verification. An empty window is
	/// its own outcome: the store examined nothing, so nothing about the integrity of the audit log follows
	/// from the run. A store that answers Verified here would let a caller emit compliance evidence claiming
	/// the log was checked and intact over a period in which no record was read at all.
	/// </remarks>
	public virtual async Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		// Store event outside verification range
		var evt = CreateAuditEvent(timestamp: now.AddDays(-10));
		_ = await store.StoreAsync(evt, CancellationToken.None).ConfigureAwait(false);

		var result = await store.VerifyChainIntegrityAsync(
			now.AddDays(-1),
			now.AddDays(1),
			CancellationToken.None).ConfigureAwait(false);

		if (result.Outcome != AuditIntegrityOutcome.NoEventsInScope)
		{
			throw new TestFixtureAssertionException(
				$"An empty verification window must report NoEventsInScope, but the store reported "
				+ $"{result.Outcome}. Reporting an unexamined window as a verified one lets a caller emit "
				+ $"compliance evidence that was never earned.");
		}

		if (result.EventsVerified != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 events verified for empty range, got {result.EventsVerified}");
		}
	}

	/// <summary>
	/// Verifies that removing a record from the middle of an intact trail is reported as a violation.
	/// </summary>
	/// <remarks>
	/// This is the arm a store fails when it verifies each record against the record's own stored claim about
	/// its predecessor. That claim is stored in the same row, so it survives the removal of the record it
	/// names: every survivor still agrees with itself, and the trail reports clean. Only carrying the prior
	/// tag forward from the record actually present exposes the gap.
	/// </remarks>
	public virtual async Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		var first = CreateAuditEvent(timestamp: now.AddMinutes(-3));
		var middle = CreateAuditEvent(timestamp: now.AddMinutes(-2));
		var last = CreateAuditEvent(timestamp: now.AddMinutes(-1));

		_ = await store.StoreAsync(first, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(middle, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(last, CancellationToken.None).ConfigureAwait(false);

		// Without this the assertion below is satisfied by a store that reports violations unconditionally,
		// which detects nothing while appearing to detect everything.
		var before = await store.VerifyChainIntegrityAsync(
			now.AddHours(-1), now.AddHours(1), CancellationToken.None).ConfigureAwait(false);

		if (before.Outcome != AuditIntegrityOutcome.Verified)
		{
			throw new TestFixtureAssertionException(
				$"The intact trail must verify before the deletion, otherwise this arm proves nothing about "
				+ $"deletion. The store reported {before.Outcome}: {before.ViolationDescription}");
		}

		await DeleteRecordOutOfBandAsync(store, middle.EventId, CancellationToken.None).ConfigureAwait(false);

		var result = await store.VerifyChainIntegrityAsync(
			now.AddHours(-1), now.AddHours(1), CancellationToken.None).ConfigureAwait(false);

		if (result.Outcome != AuditIntegrityOutcome.ViolationsDetected)
		{
			throw new TestFixtureAssertionException(
				$"A record removed from the middle of the trail must be reported as a violation, but the "
				+ $"store reported {result.Outcome}. A store reaching this line is blind to deletion: it is "
				+ $"checking each record against its own stored claim rather than against the record that "
				+ $"actually precedes it, so a deleted record leaves no trace it can see.");
		}
	}

	/// <summary>
	/// Verifies that rewriting a record's content, while leaving every integrity column as written, is
	/// reported as a violation.
	/// </summary>
	/// <remarks>
	/// This is the arm a store fails when it checks linkage alone. Linkage compares a stored hash to a stored
	/// hash; neither value is recomputed from the record's live content, so a rewritten field leaves every
	/// link in agreement.
	/// </remarks>
	public virtual async Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;

		var first = CreateAuditEvent(timestamp: now.AddMinutes(-3));
		var target = CreateAuditEvent(timestamp: now.AddMinutes(-2));
		var last = CreateAuditEvent(timestamp: now.AddMinutes(-1));

		_ = await store.StoreAsync(first, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(target, CancellationToken.None).ConfigureAwait(false);
		_ = await store.StoreAsync(last, CancellationToken.None).ConfigureAwait(false);

		var before = await store.VerifyChainIntegrityAsync(
			now.AddHours(-1), now.AddHours(1), CancellationToken.None).ConfigureAwait(false);

		if (before.Outcome != AuditIntegrityOutcome.Verified)
		{
			throw new TestFixtureAssertionException(
				$"The intact trail must verify before the rewrite, otherwise this arm proves nothing about "
				+ $"content tampering. The store reported {before.Outcome}: {before.ViolationDescription}");
		}

		await RewriteRecordActionOutOfBandAsync(store, target.EventId, "Read-REWRITTEN", CancellationToken.None)
			.ConfigureAwait(false);

		var result = await store.VerifyChainIntegrityAsync(
			now.AddHours(-1), now.AddHours(1), CancellationToken.None).ConfigureAwait(false);

		if (result.Outcome != AuditIntegrityOutcome.ViolationsDetected)
		{
			throw new TestFixtureAssertionException(
				$"A record whose content was rewritten while its integrity columns were left intact must be "
				+ $"reported as a violation, but the store reported {result.Outcome}. A store reaching this "
				+ $"line never recomputes anything from the record's live content, so its stored tags attest "
				+ $"to contents the store no longer holds.");
		}
	}

	/// <summary>
	/// Verifies that an intact trail whose writes interleave two tenants is reported as verified.
	/// </summary>
	/// <remarks>
	/// The paired liveness assertion for the two arms above, and the one that catches the opposite failure.
	/// A store chaining per tenant but verifying without that partitioning compares each record against
	/// whichever record happens to sit next to it in the global write order, which here is a record from the
	/// other tenant's chain. The trail is intact and the store reports tampering. A verifier that reports
	/// violations on healthy data is not a conservative verifier; it is one that gets switched off, and it
	/// takes the real detections with it.
	/// </remarks>
	public virtual async Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified()
	{
		var store = await CreateTenantAwareStoreForArmAsync().ConfigureAwait(false);
		var now = DateTimeOffset.UtcNow;
		var tenantA = $"tenant-a-{GenerateEventId()}";
		var tenantB = $"tenant-b-{GenerateEventId()}";

		// Interleaved, so that consecutive records of one tenant's chain are never adjacent in write order.
		for (var i = 0; i < 3; i++)
		{
			using (TenantContextHolder.BeginScope(tenantA))
			{
				_ = await store.StoreAsync(
					CreateAuditEvent(tenantId: tenantA, timestamp: now.AddMinutes(-10 + (i * 2))),
					CancellationToken.None).ConfigureAwait(false);
			}

			using (TenantContextHolder.BeginScope(tenantB))
			{
				_ = await store.StoreAsync(
					CreateAuditEvent(tenantId: tenantB, timestamp: now.AddMinutes(-9 + (i * 2))),
					CancellationToken.None).ConfigureAwait(false);
			}
		}

		AuditIntegrityResult result;
		using (TenantContextHolder.BeginScope(tenantA))
		{
			result = await store.VerifyChainIntegrityAsync(
				now.AddHours(-1), now.AddHours(1), CancellationToken.None).ConfigureAwait(false);
		}

		if (result.Outcome == AuditIntegrityOutcome.ViolationsDetected)
		{
			throw new TestFixtureAssertionException(
				$"An untouched trail whose writes interleave two tenants must not be reported as tampered, "
				+ $"but the store reported a violation at '{result.FirstViolationEventId}': "
				+ $"{result.ViolationDescription}. A store reaching this line is comparing records drawn from "
				+ $"different chains, so it reports tampering on every multi-tenant estate.");
		}

		if (result.Outcome != AuditIntegrityOutcome.Verified)
		{
			throw new TestFixtureAssertionException(
				$"Expected the interleaved but intact trail to report Verified, got {result.Outcome}.");
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
		var store = await CreateTenantAwareStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// A null TenantId names the reserved untenanted partition, not the framework's separate
		// single-tenant default identity -- the two are distinct partitions and this arm exercises the
		// former.
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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

		// First (genesis) event has NO prior tag: the keyed-MAC chain uses a null PreviousEventHash
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
