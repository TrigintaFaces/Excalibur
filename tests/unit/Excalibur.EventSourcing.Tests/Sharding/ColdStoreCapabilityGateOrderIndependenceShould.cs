// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sharding;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Order-independence lock for the cold-tier tenant-scoping gate: under row-discriminator isolation, a cold
/// event tier whose provider does not attest tenant scoping must be refused <b>whichever order</b> the two
/// registration calls are made in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the sibling lock is not sufficient.</b> The registration-time predicate
/// (<c>services.Any(d =&gt; d.ServiceType == typeof(IColdEventStore))</c>) is a statement about one instant in
/// a mutable list. It fires only when the cold store was registered <em>before</em> multi-tenancy. A host that
/// registers the cold tier <em>after</em> <c>AddMultiTenancy</c> is never seen by that predicate, and startup
/// succeeds straight into the unsafe configuration — reversing two ordinary calls silently decides whether a
/// safety gate runs at all. The sibling
/// <see cref="RowDiscriminatorColdStoreCapabilityGuardShould"/> covers the cold-first ordering; this file
/// covers the one that used to pass.
/// </para>
/// <para>
/// <b>Non-vacuity (RED condition).</b>
/// <see cref="RefuseToStart_WhenTheColdStoreIsRegisteredAfterMultiTenancy"/> is the discriminating arm: at the
/// pre-fix seam it <b>succeeds</b> — the host starts and the leak ships. It is RED exactly when the
/// container-evaluated guard is absent, weakened to a registration-time-only check, or moved inside the
/// <c>if</c> whose ordering dependence it exists to remove. If this arm ever passes without a guard evaluating
/// the finished container, it has stopped discriminating and must be re-derived before being trusted.
/// </para>
/// <para>
/// <b>Real container, not a hand-built guard.</b> Every arm resolves through
/// <c>BuildServiceProvider()</c> and starts the registered <see cref="IHostedService"/> set, so the arms bind
/// the production registration path — a guard that is written correctly but never registered fails here.
/// Constructing the validator directly would prove only that a type nobody resolves behaves well. The guard is
/// <c>internal</c> and is deliberately never named: these arms assert the <em>property</em> (does this
/// configuration reach a started host?), not the mechanism, so a future guard that holds the invariant by some
/// other means still satisfies them.
/// </para>
/// <para>
/// <b>Liveness.</b> A gate asserted only on its refusal half is satisfied by one that refuses every startup.
/// Two arms pin the permitted cases: a capability-proving cold tier registered in the same late order
/// <em>starts</em>, and a host with no cold tier at all starts untouched.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ColdStoreCapabilityGateOrderIndependenceShould
{
	[Fact]
	public async Task RefuseToStart_WhenTheColdStoreIsRegisteredAfterMultiTenancy()
	{
		// NON-VACUITY ARM. This ordering is legal, ordinary, and pre-fix it SUCCEEDED: AddMultiTenancy runs
		// first, its registration-time predicate sees no IColdEventStore, and the cold tier arrives a moment
		// later. Nothing rejects it, and a hot-miss read-through then serves another tenant's archived events.
		var services = new ServiceCollection();
		AddCapabilityProvingHotStore(services);

		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// Registered AFTER multi-tenancy, and tenant-UNAWARE: no ITenantScopingCapability<IColdEventStore>.
		_ = services.AddSingleton<IColdEventStore>(new TenantUnawareColdEventStore(A.Fake<ITenantContext>()));

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

		// The refusal must name the contract that was rejected, so an operator can act on it.
		ex.Message.ShouldContain(nameof(IColdEventStore));

		// And must state what the registration LACKS. This is the assertion that goes RED when the guard stops
		// evaluating the finished container.
		ex.Message.ShouldContain("not tenant-scoping-capable");
	}

	[Fact]
	public async Task RefuseToStart_WithAMessageThatDoesNotClaimTheCapabilityIsUnsupported()
	{
		// The message is part of the contract. Tenant-aware cold providers exist and ship, so wording that says
		// the capability is "not yet supported" would tell a consumer whose provider IS capable that the
		// framework cannot do what it can — and send them to build a workaround for a limitation that does not
		// exist. Describe the contract, never a roadmap.
		var services = new ServiceCollection();
		AddCapabilityProvingHotStore(services);
		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		_ = services.AddSingleton<IColdEventStore>(new TenantUnawareColdEventStore(A.Fake<ITenantContext>()));

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() => StartHostedServicesAsync(provider));

		ex.Message.ShouldNotContain(
			"not yet supported",
			Case.Insensitive,
			"a capable provider's owner would read this as a framework limitation that does not exist and route around it.");
	}

	[Fact]
	public async Task Start_WhenACapabilityProvingColdStoreIsRegisteredAfterMultiTenancy()
	{
		// LIVENESS, same late ordering as the non-vacuity arm — only the capability differs. Without this arm a
		// guard that refuses EVERY startup satisfies the refusal arms perfectly while breaking every cold-tier
		// consumer, capable or not.
		var services = new ServiceCollection();
		AddCapabilityProvingHotStore(services);

		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// The dep-gated seam emits ITenantScopingCapability<IColdEventStore> inseparably from the registration,
		// so the attestation cannot be present without the wiring that earns it. Register the CONCRETE type
		// through the seam (its constructor now declares ITenantContext, so the seam derives the scoped
		// marker), then forward-register IColdEventStore separately — the same two-registration pattern
		// every real provider uses (concrete + interface forwarder).
		_ = services.AddTenantAwareStore<IColdEventStore, TenantUnawareColdEventStore>(
			sp => new TenantUnawareColdEventStore(sp.GetRequiredService<ITenantContext>()));
		services.TryAddSingleton<IColdEventStore>(sp => sp.GetRequiredService<TenantUnawareColdEventStore>());

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));

		provider.GetRequiredService<IColdEventStore>().ShouldNotBeNull(
			"a capability-proving cold tier must reach a started host and resolve.");
	}

	[Fact]
	public async Task Start_WhenRowDiscriminatorHostHasNoColdTierAtAll()
	{
		// LIVENESS: the guard is registered unconditionally for every row-discriminator host, so it must no-op
		// when nothing cold is resolvable. The cost to a host that never uses tiered storage is one type check.
		var services = new ServiceCollection();
		AddCapabilityProvingHotStore(services);

		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	/// <summary>
	/// Starts every registered hosted service the way the host does. Resolving the set — rather than naming the
	/// guard — is what makes these arms bind the production registration path: an unregistered guard cannot
	/// throw from here.
	/// </summary>
	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(CancellationToken.None);
		}
	}

	/// <summary>
	/// Registers the tenant-owned primary store that row-discriminator isolation requires, so every arm fails or
	/// passes for the cold-tier reason under test and never because no primary store was present.
	/// </summary>
	private static void AddCapabilityProvingHotStore(IServiceCollection services)
	{
		_ = services.AddSingleton<IEventStore>(new MinimalEventStore(A.Fake<ITenantContext>()));
		_ = services.AddTenantAwareStore<IEventStore, MinimalEventStore>(
			sp => new MinimalEventStore(sp.GetRequiredService<ITenantContext>()));
	}

	/// <summary>
	/// A cold tier that accepts the tenant term and deliberately ignores it — the leak vector the gate exists to
	/// refuse. Implements <see cref="IColdEventStore"/> directly; no first-party base supplies any member.
	/// </summary>
	private sealed class TenantUnawareColdEventStore(ITenantContext tenantContext) : IColdEventStore
	{
		public Task<long> WriteAsync(
			KeyedTenantPartition tenant, string aggregateId, IReadOnlyList<StoredEvent> events,
			CancellationToken cancellationToken) =>
			Task.FromResult(events.Count > 0 ? events[^1].Version : -1);

		public Task<IReadOnlyList<StoredEvent>> ReadAsync(
			KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<StoredEvent>>([]);

		public Task<IReadOnlyList<StoredEvent>> ReadAsync(
			KeyedTenantPartition tenant, string aggregateId, long fromVersion,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<StoredEvent>>([]);

		public Task<bool> HasArchivedEventsAsync(
			KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
			Task.FromResult(false);
	}

	/// <summary>Satisfies the primary tenant-owned store requirement; implements the interface directly.</summary>
	private sealed class MinimalEventStore(ITenantContext tenantContext) : IEventStore
	{
		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
			long expectedVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult(AppendResult.CreateSuccess(0, null));
	}
}
