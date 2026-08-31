// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISagaStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="ConfigureProvider(IServiceCollection)"/> to verify that
/// your saga store implementation conforms to the ISagaStore contract.
/// </para>
/// <para>
/// The test kit verifies core saga operations including save, load, update,
/// and isolation behavior.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the saga-store contract
/// rather than about the kit.</b> The arms round-trip saga state through the store's open generic load and save members, and a conformant store deserializes the consumer's own state type. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSagaStoreConformanceTests : SagaStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override void ConfigureProvider(IServiceCollection services) =&gt;
///         services.AddSagas(o =&gt; o.UseSqlServer(_fixture.ConnectionString));
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Saga conformance arms round-trip state through the store's open generic load and save members, which deserialize the consumer's own state type reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class SagaStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Registers the provider under test into <paramref name="services"/> using the provider's OWN public
	/// registration extension.
	/// </summary>
	/// <param name="services">The service collection the provider registers itself into.</param>
	/// <remarks>
	/// <para>
	/// <strong>The kit never accepts a constructed store.</strong> It resolves
	/// <see cref="ISagaStore"/> from a real container built from these registrations, so what every
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
	/// An <see cref="ITenantContext"/> is already registered when this runs, and the provider must consult
	/// it rather than replace it. The kit verifies that it survives registration and fails loudly if it did
	/// not — see the diagnostic below.
	/// </para>
	/// </remarks>
	protected abstract void ConfigureProvider(IServiceCollection services);

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
	/// never share saga state, so the arms would pass with the tenant predicate deleted.
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
	private readonly Dictionary<object, ISagaStore> _resolvedStores =
		new(ReferenceEqualityComparer.Instance);

	private readonly UntenantedContext _untenantedHost = new();

	/// <summary>
	/// Resolves the provider's store for a host with no tenancy established — the untenanted partition.
	/// </summary>
	/// <remarks>
	/// The default for the non-tenancy cases. The reserved untenanted term is what a store with no ambient
	/// context resolved to before the context became required, so these cases address exactly the partition
	/// they always did.
	/// </remarks>
	private ISagaStore CreateStore() => CreateStore(_untenantedHost);

	private ISagaStore CreateStore(ITenantContext ambientTenant)
	{
		if (_resolvedStores.TryGetValue(ambientTenant, out var alreadyResolved))
		{
			return alreadyResolved;
		}

		var services = new ServiceCollection();

		// A real host registers logging; a bare ServiceCollection does not. Every shipped saga store
		// factory resolves ILogger<TStore> with GetRequiredService, so without this the kit fails on a
		// missing logger rather than on the conformance property it exists to measure -- and it would
		// fail that way for every consumer deriving it, not just for us. The kit builds the container,
		// so the kit supplies what a host would.
		_ = services.AddLogging();

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

		ConfigureProvider(services);

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

		var store = provider.GetRequiredService<ISagaStore>();
		_resolvedStores[ambientTenant] = store;

		return store;
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
	private async Task<ISagaStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>Resolves the store for one arm under a supplied ambient tenant, clearing data first.</summary>
	/// <param name="ambientTenant">The ambient tenant context the arm controls.</param>
	/// <returns>A store ready for one conformance arm.</returns>
	private async Task<ISagaStore> CreateStoreForArmAsync(ITenantContext ambientTenant)
	{
		var store = CreateStore(ambientTenant);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates a test saga state with the given ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>A test saga state.</returns>
	protected virtual TestSagaState CreateTestSagaState(Guid sagaId) =>
		TestSagaState.Create(sagaId);

	/// <summary>
	/// Generates a unique saga ID for test isolation.
	/// </summary>
	/// <returns>A unique saga identifier.</returns>
	protected virtual Guid GenerateSagaId() => Guid.NewGuid();

	/// <summary>
	/// Gets a value indicating whether the store under test enforces optimistic concurrency
	/// (version-gated, store-owns-increment — a stale-version save throws
	/// <see cref="ConcurrencyException"/> rather than silently overwriting, and a non-zero-version save
	/// against a missing saga does not resurrect it).
	/// </summary>
	/// <remarks>
	/// Default <see langword="false"/> so consumer stores that have not yet implemented the optimistic-
	/// concurrency contract are not held to it (the keystone facts early-return). A store that DOES
	/// implement it overrides this to <see langword="true"/> and is then held to
	/// <see cref="StaleSave_ThrowsConcurrencyException_NoLostUpdate"/>,
	/// <see cref="StaleSave_OnMissingSaga_DoesNotResurrect"/>, and
	/// <see cref="LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds"/>. This is the
	/// consumer-protection contract: the shipped kit must be able to FAIL a saga store that
	/// silently allows lost-updates or zombie-resurrection.
	/// </remarks>
	protected virtual bool SupportsOptimisticConcurrency => false;

	#region Save Tests

	/// <summary>
	/// Verifies that saving a new saga succeeds.
	/// </summary>
	public virtual async Task SaveAsync_NewSaga_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}
	}

	/// <summary>
	/// Verifies that saving an existing saga updates it.
	/// </summary>
	public virtual async Task SaveAsync_ExistingSaga_ShouldUpdate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Initial";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Status = "Updated";
		state.Counter = 42;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Status != "Updated")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Updated', got '{loaded.Status}'");
		}

		if (loaded.Counter != 42)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 42, got {loaded.Counter}");
		}
	}

	/// <summary>
	/// Verifies that the Completed flag is persisted.
	/// </summary>
	public virtual async Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Completed = true;

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (!loaded.Completed)
		{
			throw new TestFixtureAssertionException("Expected Completed to be true but got false");
		}
	}

	#endregion

	#region Load Tests

	/// <summary>
	/// Verifies that loading a non-existent saga returns null.
	/// </summary>
	public virtual async Task LoadAsync_NonExistent_ShouldReturnNull()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent saga but got state with status '{loaded.Status}'");
		}
	}

	/// <summary>
	/// Verifies that loading an existing saga returns its state.
	/// </summary>
	public virtual async Task LoadAsync_ExistingSaga_ShouldReturnState()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Persisted";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Status != "Persisted")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Persisted', got '{loaded.Status}'");
		}
	}

	/// <summary>
	/// Verifies that loading after multiple updates returns the latest state.
	/// </summary>
	public virtual async Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);

		state.Counter = 1;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Counter = 2;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Counter = 3;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Counter != 3)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 3 (latest), got {loaded.Counter}");
		}
	}

	#endregion

	#region Round-Trip Tests

	/// <summary>
	/// Verifies that all properties are preserved through save/load cycle.
	/// </summary>
	public virtual async Task SaveAndLoad_ShouldPreserveAllProperties()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Complete";
		state.Counter = 100;
		state.CreatedUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
		state.Completed = true;
		state.CompletedUtc = new DateTime(2025, 1, 16, 14, 45, 0, DateTimeKind.Utc);
		state.Data["key1"] = "value1";
		state.Data["key2"] = "value2";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}

		if (loaded.Status != "Complete")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Complete', got '{loaded.Status}'");
		}

		if (loaded.Counter != 100)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 100, got {loaded.Counter}");
		}

		if (!loaded.Completed)
		{
			throw new TestFixtureAssertionException("Expected Completed to be true");
		}

		if (loaded.Data is null || loaded.Data.Count != 2)
		{
			throw new TestFixtureAssertionException("Expected Data dictionary with 2 entries");
		}

		if (!loaded.Data.TryGetValue("key1", out var value1) || value1 != "value1")
		{
			throw new TestFixtureAssertionException("Expected Data['key1'] = 'value1'");
		}
	}

	/// <summary>
	/// Verifies that DateTime values are preserved correctly.
	/// </summary>
	public virtual async Task SaveAndLoad_ShouldPreserveDateTimeValues()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		var createdUtc = new DateTime(2025, 6, 15, 12, 30, 45, DateTimeKind.Utc);
		state.CreatedUtc = createdUtc;

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		// Allow for minor precision differences in some stores
		var timeDiff = Math.Abs((loaded.CreatedUtc - createdUtc).TotalSeconds);
		if (timeDiff > 1)
		{
			throw new TestFixtureAssertionException(
				$"CreatedUtc mismatch: expected {createdUtc}, got {loaded.CreatedUtc}");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that sagas are isolated by saga ID.
	/// </summary>
	public virtual async Task Sagas_ShouldIsolateBySagaId()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestSagaState(sagaId1);
		state1.Counter = 111;
		var state2 = CreateTestSagaState(sagaId2);
		state2.Counter = 222;

		await store.SaveAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveAsync(state2, CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded1 is null || loaded1.Counter != 111)
		{
			throw new TestFixtureAssertionException(
				$"Expected saga1 counter 111 but got {loaded1?.Counter}");
		}

		if (loaded2 is null || loaded2.Counter != 222)
		{
			throw new TestFixtureAssertionException(
				$"Expected saga2 counter 222 but got {loaded2?.Counter}");
		}
	}

	/// <summary>
	/// Verifies that updating one saga doesn't affect others.
	/// </summary>
	public virtual async Task UpdateOneSaga_ShouldNotAffectOthers()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestSagaState(sagaId1);
		state1.Status = "First";
		var state2 = CreateTestSagaState(sagaId2);
		state2.Status = "Second";

		await store.SaveAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveAsync(state2, CancellationToken.None).ConfigureAwait(false);

		// Update only state1 — reload to carry the STORE-OWNED version, mutate, save. The
		// saga-store contract is optimistic concurrency / store-owns-increment, so the caller performs NO
		// version arithmetic — a manual `state1.Version++` would submit a stale expected-version and throw
		// ConcurrencyException. Reload-before-save is the provider-agnostic update pattern.
		var state1ToUpdate = await store.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		if (state1ToUpdate is null)
		{
			throw new TestFixtureAssertionException("Expected saga1 state to update but got null");
		}

		state1ToUpdate.Status = "Updated";
		await store.SaveAsync(state1ToUpdate, CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded2 is null)
		{
			throw new TestFixtureAssertionException("Expected saga2 state but got null");
		}

		if (loaded2.Status != "Second")
		{
			throw new TestFixtureAssertionException(
				$"Expected saga2 status 'Second' (unchanged) but got '{loaded2.Status}'");
		}
	}

	#endregion

	#region Edge Cases

	/// <summary>
	/// Verifies that saving a saga with default values works correctly.
	/// </summary>
	public virtual async Task SaveAsync_WithDefaultValues_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = new TestSagaState { SagaId = sagaId };

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}

		if (loaded.Status != "Pending")
		{
			throw new TestFixtureAssertionException(
				$"Expected default status 'Pending' but got '{loaded.Status}'");
		}
	}

	#endregion

	#region Optimistic Concurrency Tests

	// These keystone facts gate on SupportsOptimisticConcurrency. They mirror the framework's internal
	// reference contract so the SHIPPED public kit
	// can validate that a consumer's saga store enforces optimistic concurrency — and, critically, can FAIL
	// a store that silently allows lost-updates or zombie-resurrection. A store that has not implemented the
	// contract leaves SupportsOptimisticConcurrency == false and these facts no-op.

	/// <summary>
	/// Verifies the store rejects a stale concurrent save with <see cref="ConcurrencyException"/> and the
	/// committed winner survives (no lost update). The canonical optimistic-concurrency contract:
	/// store-owns-increment, the caller performs no version arithmetic.
	/// </summary>
	public virtual async Task StaleSave_ThrowsConcurrencyException_NoLostUpdate()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			SkipArm(
				nameof(StaleSave_ThrowsConcurrencyException_NoLostUpdate),
				capability: null,
				reason: "The store does not declare optimistic concurrency, so the version-gated contract "
					+ "is not part of what it claims to implement. Recorded rather than returned silently: "
					+ "an arm that returns early is indistinguishable in every test runner from one that "
					+ "ran and passed, and this arm is the one that would catch a lost update.");
			return;
		}

		RecordArmExecuted(nameof(StaleSave_ThrowsConcurrencyException_NoLostUpdate));

		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var initial = CreateTestSagaState(sagaId);
		initial.Status = "v1";
		await store.SaveAsync(initial, CancellationToken.None).ConfigureAwait(false);

		// Two parties load the same saga at the same version.
		var copy1 = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		var copy2 = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (copy1 is null || copy2 is null)
		{
			throw new TestFixtureAssertionException("Expected both loaded copies but got null");
		}

		// copy1 saves first — the store CASes on the loaded version and succeeds.
		copy1.Status = "winner";
		await store.SaveAsync(copy1, CancellationToken.None).ConfigureAwait(false);

		// copy2 still carries the now-stale version → its save MUST be rejected (no lost update).
		copy2.Status = "loser";
		var threw = false;
		try
		{
			await store.SaveAsync(copy2, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ConcurrencyException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"Expected ConcurrencyException on the stale concurrent save (lost-update protection)");
		}

		// The winner's write survived — the stale "loser" did NOT overwrite it.
		var persisted = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (persisted is null || persisted.Status != "winner")
		{
			throw new TestFixtureAssertionException(
				$"Expected the committed winner to survive but got '{persisted?.Status}'");
		}
	}

	/// <summary>
	/// Verifies that a non-zero-version save against a MISSING saga is rejected with
	/// <see cref="ConcurrencyException"/> and does NOT re-create (resurrect) the saga. A store that blocks
	/// stale-overwrite but allows stale-resurrect is only a partial mirror (zombie saga).
	/// </summary>
	public virtual async Task StaleSave_OnMissingSaga_DoesNotResurrect()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			SkipArm(
				nameof(StaleSave_OnMissingSaga_DoesNotResurrect),
				capability: null,
				reason: "The store does not declare optimistic concurrency, so the version-gated contract "
					+ "is not part of what it claims to implement. Recorded rather than returned silently: "
					+ "an arm that returns early is indistinguishable in every test runner from one that "
					+ "ran and passed, and this arm is the one that would catch a lost update.");
			return;
		}

		RecordArmExecuted(nameof(StaleSave_OnMissingSaga_DoesNotResurrect));

		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();

		// A state carrying a non-zero expected version for a saga that does not exist in the store
		// (models a since-deleted/completed saga still held by a caller at its loaded version).
		var staleState = CreateTestSagaState(sagaId);
		staleState.Version = 5;

		var threw = false;
		try
		{
			await store.SaveAsync(staleState, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ConcurrencyException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"Expected ConcurrencyException — a non-zero-version save against a missing saga must not resurrect it");
		}

		// No zombie row exists — the saga was NOT resurrected.
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				"Expected no zombie saga — the stale-version save must not resurrect a missing saga");
		}
	}

	/// <summary>
	/// Verifies that <c>LoadAsync</c> returns the authoritative, store-incremented version (not the caller's
	/// pre-save value), and that a reload-mutate-save round-trip succeeds without manual version arithmetic
	/// (store-owns-increment).
	/// </summary>
	public virtual async Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			SkipArm(
				nameof(LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds),
				capability: null,
				reason: "The store does not declare optimistic concurrency, so the version-gated contract "
					+ "is not part of what it claims to implement. Recorded rather than returned silently: "
					+ "an arm that returns early is indistinguishable in every test runner from one that "
					+ "ran and passed, and this arm is the one that would catch a lost update.");
			return;
		}

		RecordArmExecuted(nameof(LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds));

		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// LoadAsync returns the store-incremented version (the contract: the version is authoritative
		// store-owned state, not the caller's pre-save 0).
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Version <= 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected LoadAsync to return the store-incremented version (> 0) but got {loaded.Version}");
		}

		// Reload-mutate-save round-trips WITHOUT manual version arithmetic — the store owns the increment.
		loaded.Status = "updated";
		await store.SaveAsync(loaded, CancellationToken.None).ConfigureAwait(false);

		var reloaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (reloaded is null || reloaded.Status != "updated")
		{
			throw new TestFixtureAssertionException(
				$"Expected reload-mutate-save to succeed and persist 'updated' but got '{reloaded?.Status}'");
		}
	}

	#endregion

	#region Tenant Confinement Tests

	/// <summary>
	/// SAFETY: a saga loaded under one tenant must not resolve another tenant's state.
	/// </summary>
	/// <remarks>
	/// This is the contract's own written guarantee — a confined load "returns the caller's own saga state
	/// and never another tenant's" — and until now nothing in this kit asserted it. A saga identifier
	/// travels in correlation headers and logs, so it is not a secret: a store keyed on it alone hands any
	/// caller who has seen one the whole business process state behind it.
	/// </remarks>
	public virtual async Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga()
	{
		// ONE store, ONE backing set, ambient tenant switched between operations.
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-a");
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId);
		state.Status = "tenant-a-status";
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-b");

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant confinement violated: tenant B resolved tenant A's saga {sagaId}, disclosing the "
				+ "business process state behind it. The contract requires this lookup return null — the "
				+ "same outcome as a genuinely missing saga.");
		}
	}

	/// <summary>
	/// LIVENESS: a tenant must still load its own saga.
	/// </summary>
	/// <remarks>
	/// The arm that fails when a store is "confined" by resolving nothing for anybody. Without it the
	/// safety case above is satisfied by a store that has stopped working, and a provider could pass
	/// confinement conformance while every saga in the system silently restarts from nothing.
	/// </remarks>
	public virtual async Task TenantScopedLoad_MustSeeItsOwnSaga()
	{
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);
		ambient.SwitchTo("conformance-tenant-a");

		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId);
		state.Status = "tenant-a-status";
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant confinement is inert: tenant A saved saga {sagaId} and could not load it back. A "
				+ "store that resolves nothing for anybody passes every confinement assertion while being "
				+ "unusable.");
		}

		if (!string.Equals(loaded.Status, "tenant-a-status", StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"A tenant-scoped load returned the wrong state for saga {sagaId}: expected status "
				+ $"'tenant-a-status' but got '{loaded.Status}'.");
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: two tenants may hold the same saga identifier, and neither save may overwrite
	/// the other.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The contract's second written guarantee: a confined save "can neither overwrite nor be overwritten
	/// by another tenant's state for the same sagaId — the two occupy separate partitions even when every
	/// other field is identical."
	/// </para>
	/// <para>
	/// This is invisible to the load arms above. A store may filter reads by tenant correctly and still
	/// key its WRITES on the saga identifier alone, at which point the second tenant's save destroys the
	/// first tenant's in-flight business process — silently, with no error on either side.
	/// </para>
	/// </remarks>
	public virtual async Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId()
	{
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);

		// The SAME identifier in both partitions — the whole point of the arm.
		var sharedSagaId = Guid.NewGuid();

		ambient.SwitchTo("conformance-tenant-a");
		var stateA = CreateTestSagaState(sharedSagaId);
		stateA.Status = "tenant-a-status";
		stateA.Counter = 11;
		await store.SaveAsync(stateA, CancellationToken.None).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-b");
		var stateB = CreateTestSagaState(sharedSagaId);
		stateB.Status = "tenant-b-status";
		stateB.Counter = 22;
		await store.SaveAsync(stateB, CancellationToken.None).ConfigureAwait(false);

		// B reads its own, not A's.
		var loadedB = await store.LoadAsync<TestSagaState>(sharedSagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loadedB is null || !string.Equals(loadedB.Status, "tenant-b-status", StringComparison.Ordinal)
			|| loadedB.Counter != 22)
		{
			throw new TestFixtureAssertionException(
				$"Tenant B did not read back its own saga {sharedSagaId}: expected status "
				+ $"'tenant-b-status' with counter 22 but got "
				+ $"'{loadedB?.Status ?? "<null>"}' with counter {loadedB?.Counter ?? -1}.");
		}

		// THE ARM. A's state must be exactly as A left it — B's save addressed a different partition.
		ambient.SwitchTo("conformance-tenant-a");
		var loadedA = await store.LoadAsync<TestSagaState>(sharedSagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loadedA is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant B's save DESTROYED tenant A's saga {sharedSagaId}: A can no longer load the saga "
				+ "it saved. The write path is keyed on the saga identifier alone, so one tenant's business "
				+ "process silently erases another's.");
		}

		if (!string.Equals(loadedA.Status, "tenant-a-status", StringComparison.Ordinal)
			|| loadedA.Counter != 11)
		{
			throw new TestFixtureAssertionException(
				$"Tenant B's save OVERWROTE tenant A's saga {sharedSagaId}: A expected status "
				+ $"'tenant-a-status' with counter 11 but read '{loadedA.Status}' with counter "
				+ $"{loadedA.Counter}. The two partitions must hold independent state for the same "
				+ "identifier.");
		}
	}

	/// <summary>
	/// LIVENESS: the untenanted partition is a real partition and must round-trip.
	/// </summary>
	/// <remarks>
	/// The untenanted partition holds the sagas that belong to no tenant — system-owned processes, and
	/// processes started before the deployment adopted multi-tenancy and anchored there during the migration
	/// onto it. It is addressed by a reserved term like any other partition, not by the absence of one. If
	/// confinement is implemented so that this partition matches nothing, every such saga becomes
	/// unreachable — each one restarting from nothing on every load — and no confinement assertion would
	/// report it, because confinement is satisfied perfectly by a partition that returns nothing to anybody.
	/// </remarks>
	public virtual async Task UntenantedPartition_MustRoundTripItsOwnSaga()
	{
		var untenanted = CreateStore(new UntenantedContext());

		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId);
		state.Status = "untenanted-status";
		await untenanted.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await untenanted.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException(
				$"The untenanted partition did not round-trip saga {sagaId}. Every saga anchored to the "
				+ "reserved untenanted term becomes unreachable, and no confinement assertion reports it.");
		}
	}

	#endregion

	#region Suite Wiring

	#endregion
}
