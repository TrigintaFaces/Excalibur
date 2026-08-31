// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing.Sharding;
using Excalibur.MultiTenancy;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Real-engine DI-resolution lock (voeul9) for <see cref="TenantRoutingSagaStore"/>. The sibling tests in
/// this directory prove the routing BEHAVIOUR by hand-constructing the store directly -- they never go
/// through <c>IEventSourcingBuilder.EnableTenantSharding</c> or <c>AddMultiTenancy</c>, the documented
/// entry points a consumer actually calls. That left the registration path itself unverified: before this
/// fix, <c>TenantShardingServiceCollectionExtensions.RegisterTenantRoutingStores</c> wired only
/// <see cref="IEventStore"/> -- <see cref="ISagaStore"/> stayed whatever single-tenant registration the
/// saga provider installed, making <see cref="TenantRoutingSagaStore"/> unreachable through any AddX/EnableX
/// entry point despite being a complete, tested implementation.
/// </summary>
/// <remarks>
/// <para>WIRE proof: <see cref="ISagaStore"/> resolved from the real container built by either
/// <c>EnableTenantSharding</c> or <c>AddMultiTenancy(Sharding)</c> is the routing decorator, not the raw
/// provider store -- and the <see cref="ITenantScopingCapability{TContract}"/> marker is present alongside
/// it (not merely that some marker type resolves).</para>
/// <para>GUARD proof: unlike <see cref="IEventStore"/> (always present), sagas are optional -- a sharding
/// host with no saga provider registered must not throw and must not force saga-routing wiring.</para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantRoutingSagaStoreDiResolutionShould
{
	[Fact]
	public void ResolveISagaStore_AsTheTenantRoutingDecorator_ThroughEnableTenantSharding()
	{
		using var provider = BuildStackViaEnableTenantSharding(withSagaProvider: true);
		using var scope = provider.CreateScope();

		var store = scope.ServiceProvider.GetRequiredService<ISagaStore>();
		store.ShouldBeOfType<TenantRoutingSagaStore>(
			"voeul9: EnableTenantSharding() must resolve ISagaStore, through the real container, to the "
			+ "tenant-routing decorator -- not the raw provider store a missing wiring branch would leave "
			+ "in its place.");

		scope.ServiceProvider.GetService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull(
			"the capability marker must be emitted alongside the routing store, not merely resolvable in "
			+ "isolation -- a silently-absent marker on a store that genuinely honours tenancy is the "
			+ "advertised-but-unwired failure mode this lock exists to catch.");
	}

	[Fact]
	public void ResolveISagaStore_AsTheTenantRoutingDecorator_ThroughAddMultiTenancySharding()
	{
		using var provider = BuildStackViaAddMultiTenancy(withSagaProvider: true);
		using var scope = provider.CreateScope();

		var store = scope.ServiceProvider.GetRequiredService<ISagaStore>();
		store.ShouldBeOfType<TenantRoutingSagaStore>(
			"voeul9: AddMultiTenancy(Sharding) must resolve ISagaStore, through the real container, to the "
			+ "tenant-routing decorator -- the same wiring point EnableTenantSharding uses, so the two "
			+ "seams do not fork.");

		scope.ServiceProvider.GetService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull();
	}

	[Fact]
	public async Task RouteEachTenant_ToItsOwnSagaStore_ThroughTheRealContainer()
	{
		var tenantAStore = A.Fake<ISagaStore>();
		var tenantBStore = A.Fake<ISagaStore>();
		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-saga-a")).Returns(tenantAStore);
		_ = A.CallTo(() => resolver.Resolve("tenant-saga-b")).Returns(tenantBStore);

		using var provider = BuildStackViaEnableTenantSharding(withSagaProvider: true, resolver: resolver);

		// TenantRoutingSagaStore is registered singleton (only the ISagaStore forward is Scoped, resolving
		// the shared concrete instance), so its cross-tenant drift guard tracks SagaId for the whole test
		// process. Two DISTINCT sagas, one per tenant, is also the realistic shape -- a saga's tenant is
		// fixed for its lifetime -- and avoids tripping that guard on a saga id shared across tenants.
		var stateA = new TestSagaState();
		var stateB = new TestSagaState();

		using (TenantContextHolder.BeginScope("tenant-saga-a"))
		using (var scope = provider.CreateScope())
		{
			await scope.ServiceProvider.GetRequiredService<ISagaStore>().SaveAsync(stateA, CancellationToken.None);
		}

		using (TenantContextHolder.BeginScope("tenant-saga-b"))
		using (var scope = provider.CreateScope())
		{
			await scope.ServiceProvider.GetRequiredService<ISagaStore>().SaveAsync(stateB, CancellationToken.None);
		}

		// Each fake is a DISTINCT store instance backing one tenant's shard, so "happened once exactly on
		// its own tenant's fake" already proves isolation -- had tenant A's save reached tenant B's
		// resolved store instead, tenantAStore.SaveAsync would never have been called and this would fail.
		A.CallTo(() => tenantAStore.SaveAsync(stateA, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => tenantBStore.SaveAsync(stateB, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void NotRegisterSagaRouting_WhenNoSagaProviderIsRegistered()
	{
		// GUARD: sagas are optional under sharding, unlike IEventStore. A sharding host with no saga
		// provider must build without throwing and must not carry saga-routing wiring at all.
		using var provider = BuildStackViaEnableTenantSharding(withSagaProvider: false);
		using var scope = provider.CreateScope();

		scope.ServiceProvider.GetService<ISagaStore>().ShouldBeNull(
			"a sharding host with no saga provider registered must not have ISagaStore wired at all.");
		scope.ServiceProvider.GetService<ITenantScopingCapability<ISagaStore>>().ShouldBeNull();
	}

	[Fact]
	public void NotRegisterSagaRouting_WhenNoSagaProviderIsRegistered_ThroughAddMultiTenancySharding()
	{
		using var provider = BuildStackViaAddMultiTenancy(withSagaProvider: false);
		using var scope = provider.CreateScope();

		scope.ServiceProvider.GetService<ISagaStore>().ShouldBeNull();
	}

	private static ServiceProvider BuildStackViaEnableTenantSharding(
		bool withSagaProvider, ITenantStoreResolver<ISagaStore>? resolver = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// The AMBIENT context (reads TenantContextHolder.Current) -- what a real sharding consumer wires.
		_ = services.AddTenantContext();

		if (withSagaProvider)
		{
			// Stands in for a real saga provider's AddXSagaStore() call, which the consumer makes BEFORE
			// enabling sharding -- exactly the ordering RegisterTenantRoutingStores' removal loop expects.
			_ = services.AddSingleton(A.Fake<ISagaStore>());
			_ = services.AddSingleton(resolver ?? A.Fake<ITenantStoreResolver<ISagaStore>>());
		}

		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
			_ = es.EnableTenantSharding(o => o.DefaultShardId = "shard-saga-di-a")));

		return services.BuildServiceProvider();
	}

	private static ServiceProvider BuildStackViaAddMultiTenancy(bool withSagaProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		if (withSagaProvider)
		{
			_ = services.AddSingleton(A.Fake<ISagaStore>());
			_ = services.AddSingleton(A.Fake<ITenantStoreResolver<ISagaStore>>());
		}

		// AddMultiTenancy's own Sharding branch calls RegisterTenantRoutingStores directly -- proving the
		// wiring fires from THIS entry point too, not only from EnableTenantSharding.
		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.Sharding);

		return services.BuildServiceProvider();
	}
}
