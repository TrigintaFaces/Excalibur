// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IEventStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement
/// <see cref="ConfigureProvider(IServiceCollection, IJsonTypeInfoResolver?)"/> to verify that your event
/// store implementation conforms to the IEventStore contract.
/// </para>
/// <para>
/// The test kit uses the abstract class pattern to provide shared test helpers
/// while allowing each provider to supply its own store factory and cleanup logic.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the event-store contract
/// rather than about the kit.</b> The arms append domain events and read them back through the store, and a conformant store deserializes the event payload into the consumer's own event types. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerEventStoreConformanceTests : EventStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override void ConfigureProvider(IServiceCollection services) =&gt;
///         services.AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.UseSqlServer(_fixture.ConnectionString)));
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Event-store conformance arms append domain events and read them back through the store, which deserializes the payload into the consumer's own event types reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class EventStoreConformanceTestKit : ConformanceTestKit
{
	private const string DefaultAggregateType = "TestAggregate";

	/// <summary>
	/// Registers the provider under test into <paramref name="services"/> using the provider's OWN public
	/// registration extension.
	/// </summary>
	/// <param name="services">The service collection the provider registers itself into.</param>
	/// <param name="eventTypeInfoResolver">
	/// The type-info resolver the registration must hand to the store, or <see langword="null"/> for the
	/// reflection path every other arm uses.
	/// </param>
	/// <remarks>
	/// <para>
	/// <strong>The kit never accepts a constructed store.</strong> It resolves
	/// <see cref="IEventStore"/> from a real container built from these registrations, so what every
	/// arm asserts against is <em>the object the provider's own registration actually produces</em> — the
	/// thing a consumer gets — rather than an instance a conformance-test author assembled by hand.
	/// </para>
	/// <para>
	/// That distinction is the whole point. A seam returning a store lets an author wrap a store which has
	/// no tenancy of its own in a scoping decorator and satisfy every isolation arm; the kit would then
	/// certify the composition while the reader believes it certified the provider. Because the kit
	/// resolves rather than receives, that is not expressible: if the registration wires a decorator, that
	/// is genuinely what a consumer gets and certifying it is correct; if it does not, the bare store is
	/// what gets tested.
	/// </para>
	/// <para>
	/// Implement this by calling the provider's shipped extension and nothing else, e.g.
	/// <c>services.AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.UseSqlServer(connectionString)))</c>.
	/// Registering the store by hand here reintroduces exactly the hole this seam closes.
	/// </para>
	/// <para>
	/// <strong>The resolver is a parameter rather than a second seam.</strong> A provider carries the
	/// host's resolver on its own options type, so only the provider's own registration knows how to hand
	/// it over. Passing it through the one registration path — rather than adding a separate
	/// "configure with a resolver" method — means there is no second path to keep in step with the first,
	/// and a provider that ignores the argument fails the refusal arm rather than silently skipping it.
	/// Pass it to whichever option your provider exposes, e.g.
	/// <c>es =&gt; es.UseSqlServer(cs, sql =&gt; sql.EventTypeInfoResolver(eventTypeInfoResolver))</c>, and
	/// leave the registration otherwise identical.
	/// </para>
	/// <para>
	/// An <see cref="ITenantContext"/> is already registered when this runs, and the provider must consult
	/// it rather than replace it. The kit verifies that it survives registration and fails loudly if it did
	/// not — see the diagnostic below.
	/// </para>
	/// </remarks>
	protected abstract void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver);

	/// <summary>Resolves the reserved untenanted partition — a concrete term, never an absent one.</summary>
	private sealed class UntenantedContext : ITenantContext
	{
		public string? TenantId => TenantScope.UntenantedSentinel;

		public bool HasTenant => true;
	}

	/// <summary>
	/// An ambient tenant context whose resolved tenant the kit controls.
	/// </summary>
	/// <remarks>
	/// One store, one backing set, the tenant switched between operations — exactly as a singleton store
	/// resolving a scoped context does in a real host. Obtaining a second store per tenant instead would
	/// let an implementation satisfy isolation by <em>instance separation</em>: two independent stores
	/// never share an event, so the arms would pass with the tenant predicate deleted.
	/// </remarks>
	private sealed class SwitchableTenantContext : ITenantContext
	{
		public string? TenantId { get; private set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);

		/// <summary>Switches the ambient tenant for subsequent operations.</summary>
		/// <param name="tenantId"> The tenant to resolve from now on. </param>
		public void SwitchTo(string tenantId) => TenantId = tenantId;
	}

	// One container per ambient context, keyed by REFERENCE: every arm therefore addresses ONE store over
	// ONE backing set, and switching the tenant on the context it already holds cannot hand it a second.
	//
	// The containers are deliberately NOT disposed. A provider fixture owns the lifetime of the connection
	// it registers (a container disposes instances registered into it), so disposing here would tear down
	// a shared fixture's connection out from under the suite that owns it.
	private readonly Dictionary<object, IEventStore> _resolvedStores =
		new(ReferenceEqualityComparer.Instance);

	// The refusal arm's store, held apart from the map above because it differs by REGISTRATION rather
	// than by ambient tenant: same provider, same backing set, same untenanted partition, but registered
	// with the host resolver attached. Keeping it out of the map means no other arm can accidentally
	// address it and start refusing the ordinary fixture events.
	private IEventStore? _resolverStore;

	private readonly UntenantedContext _untenantedHost = new();

	/// <summary>
	/// Resolves the provider's store for a host with no tenancy established — the untenanted partition.
	/// </summary>
	/// <remarks>
	/// The default for the non-tenancy cases. The reserved untenanted term is what a store with no ambient
	/// context resolved to before the context became required, so these cases address exactly the partition
	/// they always did.
	/// </remarks>
	private IEventStore CreateStore() => CreateStore(_untenantedHost);

	private IEventStore CreateStore(ITenantContext ambientTenant) =>
		_resolvedStores.TryGetValue(ambientTenant, out var alreadyResolved)
			? alreadyResolved
			: _resolvedStores[ambientTenant] = BuildStore(ambientTenant, eventTypeInfoResolver: null);

	/// <summary>
	/// Resolves the provider's store registered with the conformance type-info resolver attached.
	/// </summary>
	/// <remarks>
	/// Registered against the same untenanted ambient context and the same backing set as
	/// <see cref="CreateStore()"/>, so the two differ in exactly one thing: whether the host declared a
	/// closed set of event types. That is what makes the pair a controlled comparison rather than two
	/// unrelated observations.
	/// </remarks>
	private IEventStore CreateResolverStore() =>
		_resolverStore ??= BuildStore(_untenantedHost, ConformanceResolverContext.Default);

	private IEventStore BuildStore(ITenantContext ambientTenant, IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		var services = new ServiceCollection();

		// Registered FIRST so that a provider registering its own default through TryAdd cannot displace
		// it. A provider that registers one unconditionally still would, which is what the guard below is
		// for.
		_ = services.AddSingleton(ambientTenant);

		// DEPLOYMENT MODE, declared to match the context above. Supplying a tenant-bearing context IS
		// declaring a multi-tenant deployment: the kit resolves a partition of its own choosing and, in the
		// tenancy arms, changes it between operations. Leaving the mode at its single-tenant default while
		// registering such a context is the one pairing the framework refuses at startup -- mode and context
		// would disagree, and a store whose startup schema handshake reads the mode would converge the
		// untenanted partition onto the single-tenant identity, moving these arms' rows out from under them.
		// Declaring the mode here is what makes the untenanted partition a LIVE partition, which is the
		// premise the untenanted round-trip arm asserts.
		_ = services.Configure<TenantContextOptions>(static o => o.RequireTenant = true);

		ConfigureProvider(services, eventTypeInfoResolver);

		var provider = services.BuildServiceProvider();

		// HARNESS LIVENESS GUARD. If the provider's registration replaced the ambient context, every arm
		// below would address a partition the kit does not control -- it would switch tenants on an object
		// the store never reads, both partitions would resolve to the same term, and the isolation arms
		// would pass without exercising isolation at all. That is a passing test certifying nothing, which
		// is the exact defect this seam exists to prevent. Fail loudly instead.
		var resolvedContext = provider.GetRequiredService<ITenantContext>();
		if (!ReferenceEquals(resolvedContext, ambientTenant))
		{
			throw new TestFixtureAssertionException(
				"The provider's registration replaced the ambient ITenantContext supplied by the "
				+ $"conformance kit (resolved {resolvedContext.GetType().Name}). The kit could not then "
				+ "control which partition each operation addresses, and every tenancy arm would pass "
				+ "without exercising tenancy. Register the store against the ambient context rather than "
				+ "supplying one -- use TryAdd for any default so a host-supplied context wins.");
		}

		return provider.GetRequiredService<IEventStore>();
	}

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
	/// rows or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a connection or
	/// client MUST override this with the data-only half — otherwise it tears down the store the arm is
	/// about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>Resolves the untenanted store for one arm, clearing residual data first.</summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm obtains its store here. That is the only thing that causes <see cref="CleanupAsync"/> to
	/// run: a cleanup a deriver overrides but the kit never calls is indistinguishable, from the deriver's
	/// side, from one that works.
	/// </remarks>
	private async Task<IEventStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>Resolves the store for one arm under a supplied ambient tenant, clearing data first.</summary>
	/// <param name="ambientTenant">The ambient tenant context the arm controls.</param>
	/// <returns>A store ready for one conformance arm.</returns>
	private async Task<IEventStore> CreateStoreForArmAsync(ITenantContext ambientTenant)
	{
		var store = CreateStore(ambientTenant);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates test events for the given aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="count">Number of events to create.</param>
	/// <param name="startVersion">
	/// The version the first created event carries. Defaults to <c>0</c>, the version a store assigns to
	/// the first event of a new stream.
	/// </param>
	/// <returns>A list of test domain events.</returns>
	protected virtual IReadOnlyList<IDomainEvent> CreateTestEvents(
		string aggregateId,
		int count,
		long startVersion = 0)
	{
		return Enumerable.Range(0, count)
			.Select(i => TestDomainEvent.Create(aggregateId, startVersion + i))
			.ToList();
	}

	/// <summary>
	/// Generates a unique aggregate ID for test isolation.
	/// </summary>
	/// <returns>A unique aggregate identifier.</returns>
	protected virtual string GenerateAggregateId() => Guid.NewGuid().ToString();

	#region Append Tests

	/// <summary>
	/// Verifies that appending events to a new stream succeeds.
	/// </summary>
	public virtual async Task AppendAsync_ToNewStream_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var events = CreateTestEvents(aggregateId, 3);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected append to succeed but got: {result.ErrorMessage}");
		}

		// Three events on a new stream occupy versions 0, 1 and 2, so the version a subsequent append
		// must expect is 2.
		if (result.NextExpectedVersion != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected NextExpectedVersion to be 2 but was {result.NextExpectedVersion}");
		}
	}

	/// <summary>
	/// Verifies that appending with wrong expected version fails with concurrency conflict.
	/// </summary>
	public virtual async Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);
		var events2 = CreateTestEvents(aggregateId, 1, 2);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events2,
			-1, // Wrong — the stream already exists and sits at version 1
			CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			throw new TestFixtureAssertionException(
				"Expected append to fail due to version mismatch but it succeeded");
		}

		if (!result.IsConcurrencyConflict)
		{
			throw new TestFixtureAssertionException(
				$"Expected IsConcurrencyConflict to be true. Error: {result.ErrorMessage}");
		}
	}

	/// <summary>
	/// Verifies that when many appends race at the same expected version, exactly one succeeds and every
	/// other attempt is rejected as a concurrency conflict — the optimistic-concurrency guarantee under
	/// concurrent contention, not merely the sequential wrong-version case.
	/// </summary>
	public virtual async Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		// Seed the stream so it sits at version 0 (a single event appended to a new stream).
		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 1),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		const int concurrentAttempts = 10;
		var tasks = new List<Task<AppendResult>>(concurrentAttempts);
		for (var i = 0; i < concurrentAttempts; i++)
		{
			// Every racer expects the stream to still be at version 0. Invoke AppendAsync directly and
			// collect the tasks so they run concurrently (Task.Run is banned here, RS0030); each call
			// starts before the WhenAll await, so async stores genuinely overlap.
			tasks.Add(store.AppendAsync(
				aggregateId,
				DefaultAggregateType,
				CreateTestEvents(aggregateId, 1),
				0,
				CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Safety: exactly one racer may win.
		var successCount = results.Count(r => r.Success);
		if (successCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected exactly one concurrent append at the same expected version to succeed but {successCount} did.");
		}

		// Honesty: every loser must be reported as a concurrency conflict — not a silent no-op, a crash,
		// or a lost write.
		var conflicts = results.Count(r => !r.Success && r.IsConcurrencyConflict);
		if (conflicts != concurrentAttempts - 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected the other {concurrentAttempts - 1} racers to be concurrency conflicts but {conflicts} were.");
		}
	}

	/// <summary>
	/// Verifies that concurrent appends to DIFFERENT aggregates all succeed — the store must not serialize
	/// or falsely conflict independent streams (the liveness counterpart to the same-version race).
	/// </summary>
	public virtual async Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		const int concurrentAttempts = 10;
		var tasks = new List<Task<AppendResult>>(concurrentAttempts);
		for (var i = 0; i < concurrentAttempts; i++)
		{
			var aggregateId = GenerateAggregateId();
			tasks.Add(store.AppendAsync(
				aggregateId,
				DefaultAggregateType,
				CreateTestEvents(aggregateId, 1),
				-1,
				CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var failed = results.Count(r => !r.Success);
		if (failed != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected all {concurrentAttempts} concurrent appends to independent aggregates to succeed but {failed} failed.");
		}
	}

	/// <summary>
	/// Verifies that appending with correct expected version succeeds.
	/// </summary>
	public virtual async Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);
		var events2 = CreateTestEvents(aggregateId, 3, 2);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events2,
			1, // Correct — the first two events occupy versions 0 and 1
			CancellationToken.None).ConfigureAwait(false);

		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected append to succeed but got: {result.ErrorMessage}");
		}

		// Three more events extend the stream to versions 2, 3 and 4.
		if (result.NextExpectedVersion != 4)
		{
			throw new TestFixtureAssertionException(
				$"Expected NextExpectedVersion to be 4 but was {result.NextExpectedVersion}");
		}
	}

	/// <summary>
	/// Verifies that appending empty events doesn't change version.
	/// </summary>
	/// <remarks>
	/// The success check alone cannot observe the property this arm is named for: a store that DID advance
	/// the version on an empty append returns success and passes it. Both the version the store reports and
	/// the stream itself are read back and compared against their pre-append values -- the reported version
	/// because that is the number a caller reloads against, and the stream length because a store could
	/// leave the reported version alone while writing a row.
	/// </remarks>
	public virtual async Task AppendAsync_EmptyEvents_ShouldNotChangeVersion()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId, 2);

		var seed = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!seed.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected the seeding append to succeed but got: {seed.ErrorMessage}");
		}

		if (seed.NextExpectedVersion is not { } versionBefore)
		{
			throw new TestFixtureAssertionException(
				"The seeding append reported no version, so there is no pre-append value to compare against "
				+ "and this arm cannot observe the property it is named for");
		}

		var lengthBefore = (await store
			.LoadAsync(aggregateId, DefaultAggregateType, CancellationToken.None)
			.ConfigureAwait(false)).Count;

		// Positive control. If the seeded stream cannot be read back, the two comparisons below hold
		// between values that mean nothing and the arm passes vacuously.
		if (lengthBefore != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected the seeded stream to hold 2 events but it held {lengthBefore}; the arm cannot "
				+ "observe a version change on a stream it cannot read");
		}

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			Array.Empty<IDomainEvent>(),
			versionBefore,
			CancellationToken.None).ConfigureAwait(false);

		if (!result.Success)
		{
			throw new TestFixtureAssertionException(
				$"Expected empty append to succeed but got: {result.ErrorMessage}");
		}

		if (result.NextExpectedVersion != versionBefore)
		{
			throw new TestFixtureAssertionException(
				$"Expected the empty append to leave the version at {versionBefore} but it reported "
				+ $"{result.NextExpectedVersion}. A caller reloads against this number, so advancing it on "
				+ "an append that wrote nothing sends the caller past the last committed event");
		}

		var lengthAfter = (await store
			.LoadAsync(aggregateId, DefaultAggregateType, CancellationToken.None)
			.ConfigureAwait(false)).Count;

		if (lengthAfter != lengthBefore)
		{
			throw new TestFixtureAssertionException(
				$"Expected the empty append to leave the stream at {lengthBefore} events but it held "
				+ $"{lengthAfter}");
		}
	}

	#endregion

	#region Load Tests

	/// <summary>
	/// Verifies that loading from an empty/non-existent stream returns empty list.
	/// </summary>
	public virtual async Task LoadAsync_EmptyStream_ShouldReturnEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		var events = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (events.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected empty stream to return 0 events but got {events.Count}");
		}
	}

	/// <summary>
	/// Verifies that loading returns all events for an aggregate.
	/// </summary>
	public virtual async Task LoadAsync_ExistingStream_ShouldReturnAllEvents()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 5)
		{
			throw new TestFixtureAssertionException(
				$"Expected 5 events but loaded {loaded.Count}");
		}
	}

	/// <summary>
	/// Verifies that events are loaded in version order.
	/// </summary>
	public virtual async Task LoadAsync_ShouldReturnEventsInVersionOrder()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		for (int i = 1; i < loaded.Count; i++)
		{
			if (loaded[i].Version <= loaded[i - 1].Version)
			{
				throw new TestFixtureAssertionException(
					$"Events not in version order: version {loaded[i - 1].Version} followed by {loaded[i].Version}");
			}
		}
	}

	/// <summary>
	/// Verifies that loading from a specific version returns only events after that version.
	/// </summary>
	public virtual async Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 5);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			1, // Exclusive: five events occupy versions 0-4, so versions 2, 3 and 4 follow version 1
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 3) // versions 2, 3, 4
		{
			throw new TestFixtureAssertionException(
				$"Expected 3 events (versions 2-4) but loaded {loaded.Count}");
		}

		if (loaded[0].Version != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected first event to be version 2 but was {loaded[0].Version}");
		}
	}

	/// <summary>
	/// Verifies that loading from a version beyond the stream returns empty.
	/// </summary>
	public virtual async Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var testEvents = CreateTestEvents(aggregateId, 3);

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			testEvents,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			100, // Way beyond stream end
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 events when loading from beyond stream but got {loaded.Count}");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that events are isolated by aggregate type.
	/// </summary>
	public virtual async Task LoadAsync_ShouldIsolateByAggregateType()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var eventsTypeA = CreateTestEvents(aggregateId, 2);
		var eventsTypeB = CreateTestEvents(aggregateId, 3);

		_ = await store.AppendAsync(
			aggregateId,
			"TypeA",
			eventsTypeA,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await store.AppendAsync(
			aggregateId,
			"TypeB",
			eventsTypeB,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loadedA = await store.LoadAsync(
			aggregateId,
			"TypeA",
			CancellationToken.None).ConfigureAwait(false);

		var loadedB = await store.LoadAsync(
			aggregateId,
			"TypeB",
			CancellationToken.None).ConfigureAwait(false);

		if (loadedA.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected 2 events for TypeA but loaded {loadedA.Count}");
		}

		if (loadedB.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected 3 events for TypeB but loaded {loadedB.Count}");
		}
	}

	/// <summary>
	/// Verifies that events are isolated by aggregate ID.
	/// </summary>
	public virtual async Task LoadAsync_ShouldIsolateByAggregateId()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId1 = GenerateAggregateId();
		var aggregateId2 = GenerateAggregateId();
		var events1 = CreateTestEvents(aggregateId1, 2);
		var events2 = CreateTestEvents(aggregateId2, 4);

		_ = await store.AppendAsync(
			aggregateId1,
			DefaultAggregateType,
			events1,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		_ = await store.AppendAsync(
			aggregateId2,
			DefaultAggregateType,
			events2,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.LoadAsync(
			aggregateId1,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.LoadAsync(
			aggregateId2,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded1.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected 2 events for aggregate1 but loaded {loaded1.Count}");
		}

		if (loaded2.Count != 4)
		{
			throw new TestFixtureAssertionException(
				$"Expected 4 events for aggregate2 but loaded {loaded2.Count}");
		}
	}

	#endregion

	#region Data Integrity Tests

	/// <summary>
	/// Verifies that event data is preserved through round-trip.
	/// </summary>
	public virtual async Task AppendAndLoad_ShouldPreserveEventData()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var testEvent = TestDomainEvent.Create(aggregateId, 0);
		var originalEventId = testEvent.EventId;

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			[testEvent],
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 1)
		{
			throw new TestFixtureAssertionException($"Expected 1 event but got {loaded.Count}");
		}

		var loadedEvent = loaded[0];
		if (loadedEvent.EventId != originalEventId)
		{
			throw new TestFixtureAssertionException(
				$"EventId mismatch: expected {originalEventId}, got {loadedEvent.EventId}");
		}

		if (loadedEvent.AggregateId != aggregateId)
		{
			throw new TestFixtureAssertionException(
				$"AggregateId mismatch: expected {aggregateId}, got {loadedEvent.AggregateId}");
		}

		// The first event of a new stream carries version 0.
		if (loadedEvent.Version != 0)
		{
			throw new TestFixtureAssertionException(
				$"Version mismatch: expected 0, got {loadedEvent.Version}");
		}
	}

	/// <summary>
	/// Verifies that metadata is preserved through round-trip.
	/// </summary>
	public virtual async Task AppendAndLoad_ShouldPreserveMetadata()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var metadata = new Dictionary<string, object> { ["UserId"] = "user-123", ["TenantId"] = "tenant-456" };
		var testEvent = new TestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = 0,
			OccurredAt = DateTimeOffset.UtcNow,
			Metadata = metadata,
			Payload = "test-with-metadata"
		};

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			[testEvent],
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 1)
		{
			throw new TestFixtureAssertionException($"Expected 1 event but got {loaded.Count}");
		}

		var loadedMetadata = loaded[0].Metadata;
		if (loadedMetadata is null || loadedMetadata.Length == 0)
		{
			throw new TestFixtureAssertionException("Metadata was not preserved");
		}
	}

	#endregion

	#region Tenant Isolation Tests

	/// <summary>
	/// SAFETY: events written by one tenant must not be observable by another.
	/// </summary>
	/// <remarks>
	/// An event stream carries the aggregate's entire history — every payload and every metadata field — so a
	/// read that crosses tenants discloses one tenant's domain data wholesale. This case is mandatory: a store
	/// that cannot discriminate tenants is not a conformant implementation of this contract.
	/// </remarks>
	public virtual async Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents()
	{
		// ONE store, ONE backing set, ambient tenant switched between operations. Two stores would let an
		// implementation pass this by instance separation with no tenant predicate at all.
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-a");
		var aggregateId = GenerateAggregateId();
		var events = CreateTestEvents(aggregateId, 3);
		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-b");

		// The stream is addressed by the SAME aggregate id tenant A wrote. The request is well-formed and
		// specific, and the correct answer is still nothing: an aggregate identifier is not a secret, so a
		// store keyed on it alone hands B whichever tenant's rows carry that key.
		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Tenant isolation violated: tenant B loaded {loaded.Count} of tenant A's events for aggregate "
				+ $"{aggregateId}, disclosing that aggregate's entire history across tenants.");
		}

		// The from-version overload is a SEPARATE code path — a store may scope one and not the other, and a
		// kit that exercises only the first cannot tell the difference.
		var loadedFromVersion = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (loadedFromVersion.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Tenant isolation violated on the from-version read path: tenant B loaded "
				+ $"{loadedFromVersion.Count} of tenant A's events for aggregate {aggregateId}. The two "
				+ "LoadAsync overloads are distinct paths and both must be scoped.");
		}
	}

	/// <summary>
	/// LIVENESS: a tenant must still see every one of its own events.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that fails when a store is "scoped" by returning nothing to anybody. Without it the
	/// safety case above is satisfied by a completely inert store — isolation is trivially perfect when no
	/// read ever returns a row — so a provider could pass tenancy conformance while being unusable.
	/// </para>
	/// <para>
	/// It asserts the FULL count rather than merely a non-empty result: a store that returns some of a
	/// tenant's history and silently drops the rest rebuilds every aggregate wrong, and a non-empty check
	/// cannot see that.
	/// </para>
	/// </remarks>
	public virtual async Task TenantScopedLoad_MustSeeItsOwnEvents()
	{
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);
		ambient.SwitchTo("conformance-tenant-a");

		var aggregateId = GenerateAggregateId();
		var events = CreateTestEvents(aggregateId, 3);
		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			events,
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Tenant scoping is inert or lossy: tenant A appended 3 events to aggregate {aggregateId} and "
				+ $"read back {loaded.Count}. A store that returns nothing to anybody passes every isolation "
				+ "assertion while being unusable, and one that returns a partial history rebuilds the "
				+ "aggregate wrong.");
		}

		for (var i = 1; i < loaded.Count; i++)
		{
			if (loaded[i].Version <= loaded[i - 1].Version)
			{
				throw new TestFixtureAssertionException(
					$"A tenant-scoped read returned events out of version order: version "
					+ $"{loaded[i - 1].Version} followed by {loaded[i].Version}.");
			}
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: two tenants may each hold the same aggregate identifier at their own independent
	/// versions.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The version counter is part of the partition, not merely the rows. A store that filters reads by
	/// tenant but keeps ONE version counter per aggregate identifier passes both isolation arms above and
	/// still breaks: the second tenant to touch an identifier is told it has a concurrency conflict on an
	/// aggregate it never wrote, and can never create it.
	/// </para>
	/// <para>
	/// Aggregate identifiers are chosen by the consumer's domain, not by the store — natural keys such as an
	/// order number or a customer reference collide across tenants as a matter of course, so this is the
	/// normal case rather than an exotic one.
	/// </para>
	/// </remarks>
	public virtual async Task TenantPartitions_MustVersionTheSameAggregateIndependently()
	{
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);

		// The SAME identifier in both partitions — the whole point of the arm.
		var sharedAggregateId = GenerateAggregateId();

		ambient.SwitchTo("conformance-tenant-a");
		var appendA = await store.AppendAsync(
			sharedAggregateId,
			DefaultAggregateType,
			CreateTestEvents(sharedAggregateId, 3),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!appendA.Success)
		{
			throw new TestFixtureAssertionException(
				$"Tenant A could not create aggregate {sharedAggregateId} on a fresh stream: {appendA.ErrorMessage}");
		}

		ambient.SwitchTo("conformance-tenant-b");

		// B has never written this aggregate, so in B's partition the stream does not exist and -1 is the
		// correct expected version. A shared counter rejects this as a conflict.
		var appendB = await store.AppendAsync(
			sharedAggregateId,
			DefaultAggregateType,
			CreateTestEvents(sharedAggregateId, 2),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!appendB.Success)
		{
			throw new TestFixtureAssertionException(
				$"Version counter is shared across tenants: tenant B was refused a new stream for aggregate "
				+ $"{sharedAggregateId} at expected version -1 because tenant A had already used that "
				+ $"identifier. Error: {appendB.ErrorMessage}"
				+ (appendB.IsConcurrencyConflict
					? " The store reported a concurrency conflict on an aggregate tenant B never wrote, so "
						+ "tenant B can never create it."
					: string.Empty));
		}

		// The version a fresh 2-event stream yields IN THIS STORE, measured rather than assumed. The
		// contract fixes the base -- a new stream's first event is version 0 -- but this arm is about
		// per-partition versioning, not about the base, and measuring keeps its diagnosis specific: a store
		// that got the base wrong is reported by the arms that assert the base, not as a "shared counter"
		// here. Appending the same shape to an identifier no tenant has used yields the reference.
		var referenceAppend = await store.AppendAsync(
			GenerateAggregateId(),
			DefaultAggregateType,
			CreateTestEvents(sharedAggregateId, 2),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (appendB.NextExpectedVersion != referenceAppend.NextExpectedVersion)
		{
			throw new TestFixtureAssertionException(
				$"Tenant B's stream for aggregate {sharedAggregateId} continued tenant A's version sequence: "
				+ $"a fresh 2-event stream in this store yields NextExpectedVersion "
				+ $"{referenceAppend.NextExpectedVersion}, but B's supposedly-fresh stream for an identifier "
				+ $"tenant A had already used yielded {appendB.NextExpectedVersion}. Versions must be "
				+ "per-partition.");
		}

		// LIVENESS on both sides: each partition holds exactly its own history, neither the other's nor the
		// union. Asserted for BOTH tenants — a store leaking in only one direction would otherwise pass.
		var loadedB = await store.LoadAsync(
			sharedAggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loadedB.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Tenant B expected its own 2 events for aggregate {sharedAggregateId} but loaded "
				+ $"{loadedB.Count}. A count of 5 means the partitions were merged.");
		}

		ambient.SwitchTo("conformance-tenant-a");
		var loadedA = await store.LoadAsync(
			sharedAggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loadedA.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Tenant A expected its own 3 events for aggregate {sharedAggregateId} but loaded "
				+ $"{loadedA.Count}. Tenant B's write must not have altered tenant A's stream.");
		}
	}

	/// <summary>
	/// LIVENESS: the untenanted partition is a real partition and must round-trip.
	/// </summary>
	/// <remarks>
	/// The untenanted partition holds the rows that belong to no tenant — system-owned records, and records
	/// written before the deployment adopted multi-tenancy and anchored there during the migration onto it.
	/// It is addressed by a reserved term like any other partition, not by the absence of one. If scoping is
	/// implemented so that this partition matches nothing, every such record becomes unreachable — the
	/// aggregates holding them rehydrate empty — and no isolation assertion would report it, because
	/// isolation is satisfied perfectly by a partition that returns nothing to anybody.
	/// </remarks>
	public virtual async Task UntenantedPartition_MustRoundTripItsOwnEvents()
	{
		var untenanted = CreateStore(new UntenantedContext());

		var aggregateId = GenerateAggregateId();
		var appended = await untenanted.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 3),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!appended.Success)
		{
			throw new TestFixtureAssertionException(
				$"The untenanted partition refused an append for aggregate {aggregateId}: "
				+ $"{appended.ErrorMessage}. The reserved untenanted term addresses a real partition and must "
				+ "accept writes like any other.");
		}

		var loaded = await untenanted.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"The untenanted partition did not round-trip aggregate {aggregateId}: appended 3 events, "
				+ $"loaded {loaded.Count}. Every record anchored to the untenanted term becomes unreachable, "
				+ "and no isolation assertion reports it.");
		}
	}

	#endregion

	#region Suite Wiring

	/// <summary>
	/// SAFETY: an unaddressable aggregate identifier is rejected rather than written somewhere arbitrary.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// A store that accepts an empty or whitespace identifier has written events to a stream no reader can
	/// name, so the aggregate they belong to can never be reconstructed. That is silent data loss at write
	/// time, and it is invisible to every other arm here because every other arm supplies a valid id.
	/// </para>
	/// <para>
	/// All three unaddressable forms are exercised in one arm rather than as separate parameterised cases,
	/// because an arm in this kit takes no parameters - the deriver binds it by declaring one member per
	/// arm, and a parameterised arm could not be bound that way. Each case names itself on failure so a
	/// partial implementation is not reported as a total one.
	/// </para>
	/// </remarks>
	public virtual async Task AppendAsync_UnaddressableAggregateId_ShouldThrow()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var events = CreateTestEvents(GenerateAggregateId(), 1);

		foreach (var (invalidId, description) in new (string? Id, string Description)[]
		{
			(null, "null"),
			(string.Empty, "empty"),
			("   ", "whitespace"),
		})
		{
			var threw = false;

			try
			{
				_ = await store.AppendAsync(
					invalidId!,
					DefaultAggregateType,
					events,
					-1,
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentException)
			{
				threw = true;
			}

			if (!threw)
			{
				throw new TestFixtureAssertionException(
					$"Appending with a {description} aggregate identifier was accepted rather than rejected "
					+ "with an ArgumentException. The events have been written to a stream no reader can "
					+ "name, so the aggregate they belong to cannot be reconstructed.");
			}
		}
	}

	/// <summary>
	/// SAFETY: an append past the stream's tail is refused rather than leaving a gap in the version sequence.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Distinct from appending at a STALE version, and a store can get one right and the other wrong. This
	/// arm requires the store to re-read its current version and compare, which is what makes replay sound:
	/// an implementation guarding only on identifier collision - an id-uniqueness index, or a conditional
	/// write on "this key does not exist" - accepts this happily and writes a non-contiguous version. The
	/// stream then has a hole, and a reader replaying it either stops early or silently skips events.
	/// </remarks>
	public virtual async Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 3),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		// The stream now sits at version 2. Expecting 5 would leave versions 3 and 4 absent.
		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 1, 5),
			5,
			CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			throw new TestFixtureAssertionException(
				"An append at a version beyond the stream tail succeeded, leaving a gap in the version "
				+ "sequence. A reader replaying this stream will stop early or skip events.");
		}

		if (!result.IsConcurrencyConflict)
		{
			throw new TestFixtureAssertionException(
				"An append beyond the stream tail failed, but not as a concurrency conflict, so a caller "
				+ $"cannot tell a version violation from a transport fault and retry correctly. Error: {result.ErrorMessage}");
		}
	}

	/// <summary>
	/// SAFETY: an append to a stream that does not exist is refused unless it claims the empty stream.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The absent-stream twin of the beyond-the-tail case, and separate because the two take different code
	/// paths in most stores: one compares against a version it read, the other against nothing at all. A
	/// store that treats "no stream" as "any expected version is fine" silently creates a stream whose first
	/// event carries a version no writer ever agreed to.
	/// </remarks>
	public virtual async Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		var result = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 1, 5),
			5,
			CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			throw new TestFixtureAssertionException(
				"An append to a stream that does not exist was accepted at a non-initial expected version. "
				+ "The stream now begins at a version no writer agreed to.");
		}

		if (!result.IsConcurrencyConflict)
		{
			throw new TestFixtureAssertionException(
				"An append to an absent stream at the wrong expected version failed, but not as a "
				+ $"concurrency conflict, so a caller cannot retry correctly. Error: {result.ErrorMessage}");
		}
	}

	/// <summary>
	/// The from-version bound is EXCLUSIVE: reading from zero returns everything after version zero.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Zero is the boundary an off-by-one lands on, and it is the value a caller passes most often - it is
	/// what "everything after the first event" means. A store treating the bound as inclusive replays the
	/// first event twice on every snapshot-based rebuild; one treating it as exclusive-from-one drops an
	/// event. Both are silent, and the existing from-version arms use a non-zero bound, so neither shows up.
	/// </remarks>
	public virtual async Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();

		_ = await store.AppendAsync(
			aggregateId,
			DefaultAggregateType,
			CreateTestEvents(aggregateId, 5),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync(
			aggregateId,
			DefaultAggregateType,
			0,
			CancellationToken.None).ConfigureAwait(false);

		if (loaded.Count != 4)
		{
			throw new TestFixtureAssertionException(
				$"Loading from version 0 returned {loaded.Count} of 5 events; the exclusive bound requires "
				+ "exactly the 4 that follow version 0. Returning 5 replays the first event on every "
				+ "rebuild; returning 3 drops one.");
		}

		foreach (var loadedEvent in loaded)
		{
			if (loadedEvent.Version <= 0)
			{
				throw new TestFixtureAssertionException(
					$"Loading from version 0 returned an event at version {loadedEvent.Version}. The bound "
					+ "is exclusive, so version 0 itself must not be included.");
			}
		}
	}

	/// <summary>
	/// LIVENESS: many callers arriving at once on a cold store all succeed rather than racing its startup.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// A store that initialises lazily - opening a connection, creating a container, provisioning a table -
	/// has exactly one window in its life where that work can run twice concurrently, and it is the first
	/// call. Every other arm here reaches the store one caller at a time and cannot enter that window.
	/// </para>
	/// <para>
	/// The read is deliberately of an aggregate that does not exist, so the arm can only fail on the
	/// initialisation race and never on stored data. An empty result from every caller is the pass.
	/// </para>
	/// </remarks>
	public virtual async Task ConcurrentFirstUse_ShouldNotFault()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var absentAggregateId = GenerateAggregateId();

		const int Callers = 16;
		var arrivals = new List<Task<IReadOnlyList<StoredEvent>>>(Callers);

		for (var i = 0; i < Callers; i++)
		{
			// Invoke directly and collect the tasks so they overlap (Task.Run is banned here, RS0030);
			// each call starts before the WhenAll await, so an async store genuinely races its own startup.
			arrivals.Add(store.LoadAsync(
				absentAggregateId,
				DefaultAggregateType,
				CancellationToken.None).AsTask());
		}

		try
		{
			await Task.WhenAll(arrivals).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new TestFixtureAssertionException(
				$"{Callers} callers reaching a cold store at once produced a fault: {ex.GetType().Name}: "
				+ $"{ex.Message}. A store whose first-use initialisation is not safe against concurrent "
				+ "arrival fails on the first burst of traffic after every deployment.");
		}
	}

	/// <summary>
	/// Verifies that an event type the host's configured resolver does not declare is refused by throwing,
	/// and that nothing is written.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A host supplying a source-generated resolver is stating the closed set of event types its process
	/// will ever write. An event outside that set cannot be serialized, and no retry, reload or
	/// reconfiguration at run time reaches it — only a change to the program does. That places it on the
	/// throwing side of this interface's outcome-or-defect line, alongside a blank identifier and an
	/// oversized batch, rather than among the returned failures.
	/// </para>
	/// <para>
	/// The distinction was worth a shared arm because the family did not agree about it. Half the stores
	/// threw and half returned a failure, and each provider had a hand-written test of its own that agreed
	/// with its own provider — so ten passing tests coexisted with a contract that had no single answer. A
	/// returned failure is indistinguishable from a transient store fault, so a caller retries it; every
	/// retry then fails identically and the events are never persisted, quietly, while the same consumer
	/// code against a throwing provider stops at the first append.
	/// </para>
	/// <para>
	/// The final assertion is the one a per-provider test is most likely to omit: refusing loudly is only
	/// half the contract, and a store that threw <em>after</em> writing part of the batch would satisfy
	/// every other assertion here while leaving a torn stream behind.
	/// </para>
	/// </remarks>
	/// <returns>A task representing the conformance arm.</returns>
	public virtual async Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing()
	{
		// FIXTURE DISCRIMINATOR, and it runs first on purpose. The same provider, the same event type, the
		// same call -- with no resolver configured. It must SUCCEED. If this store simply could not write
		// this type for some unrelated reason, this arm would fail here rather than passing below for the
		// wrong reason, which is the failure mode a lone "it threw" assertion cannot detect.
		var reflectionStore = await CreateStoreForArmAsync().ConfigureAwait(false);
		var reflectionId = GenerateAggregateId();

		var reflectionResult = await reflectionStore.AppendAsync(
			reflectionId,
			DefaultAggregateType,
			[new UndeclaredConformanceEvent { AggregateId = reflectionId }],
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		if (!reflectionResult.Success)
		{
			throw new TestFixtureAssertionException(
				"With no type-info resolver configured, this store must serialize any event type through "
				+ "reflection. It refused instead, so the refusal below would prove nothing about the "
				+ $"resolver. Error: {reflectionResult.ErrorMessage}");
		}

		var store = CreateResolverStore();
		var aggregateId = GenerateAggregateId();

		var before = await store.LoadAsync(aggregateId, DefaultAggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		EventTypeNotDeclaredException? thrown = null;

		try
		{
			_ = await store.AppendAsync(
				aggregateId,
				DefaultAggregateType,
				[new UndeclaredConformanceEvent { AggregateId = aggregateId }],
				expectedVersion: -1,
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (EventTypeNotDeclaredException ex)
		{
			thrown = ex;
		}

		if (thrown is null)
		{
			throw new TestFixtureAssertionException(
				"Appending an event type the configured resolver does not declare must throw "
				+ "EventTypeNotDeclaredException. This store did not throw it. Either it reported the "
				+ "refusal as a returned AppendResult failure -- which a caller cannot tell apart from a "
				+ "transient fault, and so retries forever -- or it never consulted the host's resolver at "
				+ "all and serialized through reflection, which is the AOT defect this arm exists to catch.");
		}

		if (thrown.EventType != typeof(UndeclaredConformanceEvent))
		{
			throw new TestFixtureAssertionException(
				"The exception must name the type that was actually refused. Expected "
				+ $"{nameof(UndeclaredConformanceEvent)}, but EventType was "
				+ $"{thrown.EventType?.Name ?? "null"}. A caller reads this to find out what to declare.");
		}

		if (thrown.InnerException is not NotSupportedException)
		{
			throw new TestFixtureAssertionException(
				"The refusal must carry the serializer's own exception as its inner exception, so the "
				+ "reason is traceable to the resolver that was consulted. Inner exception was "
				+ $"{thrown.InnerException?.GetType().Name ?? "null"}.");
		}

		var after = await store.LoadAsync(aggregateId, DefaultAggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		if (after.Count != before.Count)
		{
			throw new TestFixtureAssertionException(
				$"A refused append must write nothing. The stream held {before.Count} events before the "
				+ $"refusal and {after.Count} after, so this store wrote part of the batch and then threw. "
				+ "A partially written batch is a torn event-stream prefix, which event sourcing must "
				+ "never produce -- serialize the whole batch before writing any of it.");
		}
	}

	/// <summary>
	/// The largest number of events this provider can append in ONE all-or-nothing operation, or
	/// <see langword="null"/> when it has no such ceiling.
	/// </summary>
	/// <value>
	/// The provider's atomic-append limit, or <see langword="null"/> when an append of any size is atomic.
	/// </value>
	/// <remarks>
	/// <para>
	/// Declaring a limit is a statement that appends above it are REFUSED, not that they are split. A store
	/// whose underlying service caps an atomic write (Cosmos DB and DynamoDB at 100 operations, Firestore at
	/// 500) states that cap here. A store that can commit an append of any size in one transaction -- the
	/// relational providers -- leaves this <see langword="null"/> and is held to that instead.
	/// </para>
	/// <para>
	/// A provider offering a documented NON-atomic opt-out reports the limit for its default, atomic
	/// configuration: the conformance suite registers the provider as a consumer gets it, and the opt-out is
	/// the consumer's explicit trade rather than this contract's behaviour.
	/// </para>
	/// </remarks>
	protected virtual int? AtomicAppendLimit => null;

	/// <summary>
	/// SAFETY: an append larger than one atomic operation is refused whole, never committed in pieces.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// This is the parity arm: <see cref="IEventStore.AppendAsync"/> promises that every implementation
	/// answers a batch above the provider's atomic limit the same way, so a caller may rely on the contract
	/// rather than on which provider is configured. Each provider's own suite asserts its own behaviour and
	/// so cannot see a disagreement between providers; only an arm every provider runs can.
	/// </para>
	/// <para>
	/// The forbidden outcome is the silent split -- committing the batch as a sequence of smaller atomic
	/// writes. It looks like success and leaves a torn prefix behind whenever one of them fails, and a
	/// consumer CANNOT detect that state: the stream simply has a prefix and no suffix, and every later read
	/// is consistent with a shorter history. So the refusal is checked together with the stream being
	/// untouched; a store that threw after writing part of the batch fails here just as one that never threw.
	/// </para>
	/// <para>
	/// Both answers are honest and the arm accepts either, which is what makes it a parity check rather than
	/// a preference: refuse above a declared <see cref="AtomicAppendLimit"/>, or genuinely append any size
	/// atomically when none is declared. What no provider may do is claim the second and perform the first.
	/// </para>
	/// </remarks>
	public virtual async Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically()
	{
		var limit = AtomicAppendLimit;

		if (limit is not { } atomicLimit)
		{
			// No declared ceiling, so the claim under test is that an append of any size is atomic. Probing
			// above the tightest ceiling any provider in this family declares (100) is what makes the claim
			// falsifiable: a store that quietly splits at some internal batch size fails to read back whole.
			var unboundedStore = await CreateStoreForArmAsync().ConfigureAwait(false);
			var unboundedId = GenerateAggregateId();
			const int ProbeSize = 150;

			var unboundedResult = await unboundedStore.AppendAsync(
				unboundedId,
				DefaultAggregateType,
				CreateTestEvents(unboundedId, ProbeSize),
				-1,
				CancellationToken.None).ConfigureAwait(false);

			if (!unboundedResult.Success)
			{
				throw new TestFixtureAssertionException(
					$"This suite declares no {nameof(AtomicAppendLimit)}, which states that an append of any size "
					+ $"commits atomically. An append of {ProbeSize} events failed instead. If this provider does "
					+ $"have a ceiling, declare it in {nameof(AtomicAppendLimit)} so the refusal is the contract "
					+ $"rather than a surprise. Error: {unboundedResult.ErrorMessage}");
			}

			var unboundedStored = await unboundedStore
				.LoadAsync(unboundedId, DefaultAggregateType, CancellationToken.None).ConfigureAwait(false);

			if (unboundedStored.Count != ProbeSize)
			{
				throw new TestFixtureAssertionException(
					$"The append of {ProbeSize} events reported success, but the stream holds "
					+ $"{unboundedStored.Count}. The batch was split and only part of it survived, so this store "
					+ "does have an atomic ceiling and is not reporting one.");
			}

			return;
		}

		// FIXTURE DISCRIMINATOR, and it runs first on purpose. An append of exactly the limit must SUCCEED.
		// Without it, a store that cannot write a large batch AT ALL would pass the refusal below for the
		// wrong reason, and the arm would certify a ceiling this provider does not actually have.
		var atLimitStore = await CreateStoreForArmAsync().ConfigureAwait(false);
		var atLimitId = GenerateAggregateId();

		var atLimitResult = await atLimitStore.AppendAsync(
			atLimitId,
			DefaultAggregateType,
			CreateTestEvents(atLimitId, atomicLimit),
			-1,
			CancellationToken.None).ConfigureAwait(false);

		if (!atLimitResult.Success)
		{
			throw new TestFixtureAssertionException(
				$"An append of exactly {atomicLimit} events -- the declared {nameof(AtomicAppendLimit)} -- must "
				+ "succeed, or the refusal one event above it proves nothing about the limit. Error: "
				+ $"{atLimitResult.ErrorMessage}");
		}

		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var aggregateId = GenerateAggregateId();
		var oversized = CreateTestEvents(aggregateId, atomicLimit + 1);

		EventBatchTooLargeException? thrown = null;

		try
		{
			_ = await store.AppendAsync(
				aggregateId,
				DefaultAggregateType,
				oversized,
				-1,
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (EventBatchTooLargeException ex)
		{
			thrown = ex;
		}

		if (thrown is null)
		{
			throw new TestFixtureAssertionException(
				$"An append of {atomicLimit + 1} events exceeds this provider's atomic limit of {atomicLimit}, so "
				+ "it must be refused with EventBatchTooLargeException before anything is written. This store "
				+ "accepted it instead -- committing it as a sequence of smaller atomic writes, which is not "
				+ "all-or-nothing. A failure partway through leaves earlier writes committed and later ones not, "
				+ "and the consumer cannot detect the resulting torn stream: it holds a prefix with no suffix, "
				+ "and every later read is consistent with a shorter history.");
		}

		if (thrown.ActualCount != oversized.Count || thrown.MaxBatchSize != atomicLimit)
		{
			throw new TestFixtureAssertionException(
				"The refusal must carry the offending count and the limit, so a caller can split the batch and "
				+ $"retry without guessing. Expected ActualCount {oversized.Count} and MaxBatchSize "
				+ $"{atomicLimit}, but got {thrown.ActualCount} and {thrown.MaxBatchSize}.");
		}

		var after = await store.LoadAsync(aggregateId, DefaultAggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		if (after.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"The refused append left {after.Count} events in the stream. Rejecting the batch AFTER writing "
				+ "part of it is the torn prefix this arm exists to prevent -- the refusal must happen at the "
				+ "boundary, before any write is attempted.");
		}
	}

	#endregion
}
