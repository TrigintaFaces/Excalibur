// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Author≠impl, security-critical regression lock for <c>93ilgc</c> — the tenant-drift guard in
/// <see cref="TenantRoutingSagaStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The decorator binds each saga to the tenant it was resolved under (<c>_loadedTenants</c>): a saga read from
/// tenant A's shard MUST be written back to tenant A's shard. If the ambient <see cref="ITenantId"/> drifts
/// between load and save (a cross-tenant step, a background timeout, a retry on a drifted scope), <c>SaveAsync</c>
/// MUST fail loud rather than silently persist to the wrong shard — that silent path is cross-tenant data
/// leakage. (Implementer = PlatformDeveloper; this is the independent lock authored by TestsDeveloper.)
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the drift case asserts the underlying store is NEVER written — so removing the guard
/// (letting the save route to the drifted tenant's shard) flips the test RED. The no-drift and load-null cases
/// prove the guard is <em>conditional</em> (it does not vacuously throw on every save).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantRoutingSagaStoreDriftGuardShould
{
	[Fact]
	public async Task ThrowAndNotWrite_WhenAmbientTenantDriftsBetweenLoadAndSave()
	{
		// Arrange — two tenant shards; saga loaded under tenant-a.
		var storeA = A.Fake<ISagaStore>();
		var storeB = A.Fake<ISagaStore>();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		A.CallTo(() => storeA.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._)).Returns(state);

		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		A.CallTo(() => resolver.Resolve("tenant-a")).Returns(storeA);
		A.CallTo(() => resolver.Resolve("tenant-b")).Returns(storeB);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		// Load under tenant-a (binds the saga to tenant-a).
		_ = await routingStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Ambient tenant drifts to tenant-b before the save.
		tenantId.Value = "tenant-b";

		// Act + Assert — save MUST fail loud, naming both tenants.
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.SaveAsync(state, CancellationToken.None));
		ex.Message.ShouldContain("tenant-a");
		ex.Message.ShouldContain("tenant-b");

		// Security invariant: NEITHER shard was written — the drifted save never reached any store.
		A.CallTo(() => storeB.SaveAsync(A<TestSagaState>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => storeA.SaveAsync(A<TestSagaState>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task SaveToTheLoadedTenantShard_WhenAmbientTenantIsUnchanged()
	{
		// Anchor (non-vacuity): with no drift, the save routes to the SAME tenant's shard — the guard does
		// not throw on the normal path.
		var storeA = A.Fake<ISagaStore>();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		A.CallTo(() => storeA.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._)).Returns(state);

		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		A.CallTo(() => resolver.Resolve("tenant-a")).Returns(storeA);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		_ = await routingStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		await routingStore.SaveAsync(state, CancellationToken.None);

		A.CallTo(() => storeA.SaveAsync(state, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowAndNotWrite_WhenNewSagaIsCreatedUnderOneTenantThenSavedUnderAnother()
	{
		// The guard also binds a newly-created saga (no prior load) to the tenant of its first save, so a
		// later save under a drifted tenant is rejected.
		var storeA = A.Fake<ISagaStore>();
		var storeB = A.Fake<ISagaStore>();
		var state = new TestSagaState { SagaId = Guid.NewGuid() };

		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		A.CallTo(() => resolver.Resolve("tenant-a")).Returns(storeA);
		A.CallTo(() => resolver.Resolve("tenant-b")).Returns(storeB);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		// First save creates + binds the saga to tenant-a.
		await routingStore.SaveAsync(state, CancellationToken.None);
		A.CallTo(() => storeA.SaveAsync(state, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

		// Drift, then save again — rejected, and tenant-b is never written.
		tenantId.Value = "tenant-b";
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.SaveAsync(state, CancellationToken.None));
		A.CallTo(() => storeB.SaveAsync(A<TestSagaState>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotThrow_WhenLoadReturnsNull_ThenSaveUnderADifferentTenant()
	{
		// Proves the guard keys on an actual loaded saga, not vacuously on any tenant change: a load that
		// returns null (saga not found on that shard) records no binding, so a subsequent save under a
		// different ambient tenant is the legitimate "create on the current tenant" path — not drift.
		var storeA = A.Fake<ISagaStore>();
		var storeB = A.Fake<ISagaStore>();
		var sagaId = Guid.NewGuid();
		A.CallTo(() => storeA.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._)).Returns((TestSagaState?)null);

		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		A.CallTo(() => resolver.Resolve("tenant-a")).Returns(storeA);
		A.CallTo(() => resolver.Resolve("tenant-b")).Returns(storeB);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		_ = await routingStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None); // returns null, records nothing

		tenantId.Value = "tenant-b";
		var state = new TestSagaState { SagaId = sagaId };
		await routingStore.SaveAsync(state, CancellationToken.None); // no binding existed → no drift

		A.CallTo(() => storeB.SaveAsync(state, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}
}
