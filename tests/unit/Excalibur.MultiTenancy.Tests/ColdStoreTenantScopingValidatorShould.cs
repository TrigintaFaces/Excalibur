// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Order-independence lock for the cold-tier tenant-scoping guard.
/// </summary>
/// <remarks>
/// <para>
/// <b>The property, stated so it is falsifiable.</b> Under row-discriminator multi-tenancy, a host that can
/// resolve an <see cref="IColdEventStore"/> which does <i>not</i> advertise
/// <c>ITenantScopingCapability&lt;IColdEventStore&gt;</c> MUST fail to start — <b>whichever order</b> the two
/// registration calls were made in. The unsafe configuration it prevents is concrete: the cold leg is keyed by
/// aggregate id with no tenant term, so a caller with a different ambient tenant reads another tenant's
/// archived events on a hot miss, and — the worse direction — an archive submitting a shorter version range
/// can drive a hot-tier delete of events that were never written under that tenant.
/// </para>
/// <para>
/// <b>Why the ordering is the whole point.</b> The early half of the gate is
/// <c>services.Any(d =&gt; d.ServiceType == typeof(IColdEventStore))</c> — a predicate over a <i>mutable list at
/// one instant</i>. It fires only when the cold store was registered BEFORE <c>AddMultiTenancy</c>. Registering
/// it AFTER means the predicate already ran and evaluated false, and startup proceeds straight into the unsafe
/// configuration. That is the defect: reversing two lines changed whether a safety gate ran at all, and it
/// failed <i>silently</i>, unlike the primary stores which fail loud. The invariant is therefore carried by
/// <c>ColdStoreTenantScopingValidator</c>, which asks the FINISHED container via
/// <see cref="IServiceProviderIsService"/> and so cannot be order-dependent by construction.
/// </para>
/// <para>
/// <b>Arms (testing-patterns §3 — no safety assertion without its liveness pair).</b> Arms 1 and 2 are the
/// same safety property approached from both orderings; arms 3 and 4 exist because <i>a validator that threw
/// unconditionally would satisfy arms 1 and 2 perfectly</i> while refusing to start every legitimate host.
/// Arm 4 is the one that would be forgotten: it is the only arm that fails if the guard stops distinguishing a
/// capable cold store from an incapable one.
/// <list type="bullet">
/// <item>SAFETY — cold store registered AFTER <c>AddMultiTenancy</c>, no capability ⇒ host refuses to start
/// (arm 1, the ordering that used to fail silent).</item>
/// <item>SAFETY — cold store registered BEFORE, no capability ⇒ also refuses (arm 2, order-independence).</item>
/// <item>LIVENESS — no cold tier at all ⇒ starts (arm 3; the guard no-ops rather than punishing hosts that
/// never opted into tiered storage).</item>
/// <item>LIVENESS — cold store that DOES advertise the capability ⇒ starts (arm 4).</item>
/// </list>
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Deleting the unconditional <c>TryAddEnumerable</c> registration of the validator, or
/// moving it back inside the <c>if (services.Any(...))</c> block, turns arm 1 RED while arms 2–4 stay green —
/// which is precisely the defect being locked. Replacing the guard's condition with an unconditional throw
/// turns arms 3 and 4 RED.
/// </para>
/// </remarks>
public sealed class ColdStoreTenantScopingValidatorShould
{
	/// <summary>
	/// Arm 1 — SAFETY, the ordering that used to fail silent: cold store registered AFTER
	/// <c>AddMultiTenancy</c>. The registration-time predicate cannot see it, so only the container-evaluated
	/// guard can catch this.
	/// </summary>
	[Fact]
	public async Task RefuseToStart_WhenAnIncapableColdStoreIsRegisteredAFTERMultiTenancy()
	{
		var services = new ServiceCollection();
		AddRequiredPrimaryStore(services);

		services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// Registered a moment too late for the descriptor predicate — the exact reversal that used to pass.
		services.AddSingleton(A.Fake<IColdEventStore>());

		using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider));

		ex.Message.ShouldContain(
			nameof(IColdEventStore),
			Case.Sensitive,
			"the failure must name the offending contract, or an operator cannot act on it");
	}

	/// <summary>
	/// Arm 2 — SAFETY from the other ordering. Registering the cold store first is caught earlier (at the
	/// <c>AddMultiTenancy</c> call site, with the registration in view), but it must still be caught: the
	/// property is that BOTH orderings fail, not that one of them does.
	/// </summary>
	[Fact]
	public async Task RefuseToStart_WhenAnIncapableColdStoreIsRegisteredBEFOREMultiTenancy()
	{
		var services = new ServiceCollection();
		AddRequiredPrimaryStore(services);

		services.AddSingleton(A.Fake<IColdEventStore>());

		// The early predicate may throw here; if it does not, the container guard must. Either is a pass —
		// what must NOT happen is a host that starts.
		var threwAtRegistration = false;
		try
		{
			services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		}
		catch (InvalidOperationException)
		{
			threwAtRegistration = true;
		}

		if (threwAtRegistration)
		{
			return;
		}

		using var provider = services.BuildServiceProvider();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider));
	}

	/// <summary>
	/// Arm 3 — LIVENESS. A host with no cold tier must start. Without this arm, a guard that threw
	/// unconditionally would satisfy both safety arms while breaking every single-tier deployment.
	/// </summary>
	[Fact]
	public async Task Start_WhenNoColdTierIsRegisteredAtAll()
	{
		var services = new ServiceCollection();
		AddRequiredPrimaryStore(services);

		services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	/// <summary>
	/// Arm 4 — LIVENESS, and the arm that would be forgotten. A cold store that genuinely advertises the
	/// tenant-scoping capability must be allowed to start. This is the only arm that fails if the guard stops
	/// telling a capable cold store apart from an incapable one — i.e. the arm that proves it is discriminating
	/// rather than merely refusing.
	/// </summary>
	[Fact]
	public async Task Start_WhenTheColdStoreAdvertisesTheTenantScopingCapability()
	{
		var services = new ServiceCollection();
		AddRequiredPrimaryStore(services);

		// The marker is structurally unimplementable from outside the abstractions assembly, so it is emitted
		// the only legitimate way: through the seam that also supplies the tenant context. A bare fake marker
		// would be the "lying marker" shape this capability was designed to make inexpressible.
		_ = services.AddTenantScopedStore<IColdEventStore, TenantAwareColdEventStore>(
			(_, _) => new TenantAwareColdEventStore());

		services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}


	/// <summary>
	/// Registers the capable PRIMARY store that row-discriminator mode requires before it will configure at
	/// all ("no tenant-owned store is registered" otherwise). It is scaffolding for every arm, deliberately
	/// registered through the real seam so it carries a genuine capability marker — the cold tier is the only
	/// variable under test here.
	/// </summary>
	private static void AddRequiredPrimaryStore(IServiceCollection services)
	{
		// Two registrations, and both are needed for different reasons. AddTenantScopedStore registers the
		// CONCRETE store plus the capability marker; it does not register the IEventStore interface, which is
		// what the row-discriminator precondition scans the descriptor list for. The AddSingleton supplies that
		// interface. Registering only the seam leaves the precondition unsatisfied ("no tenant-owned store is
		// registered") and every arm fails for a reason that has nothing to do with the cold tier.
		services.AddSingleton(A.Fake<IEventStore>());

		_ = services.AddTenantScopedStore<IEventStore, TestDoubles.NoopEventStore>(
			(_, _) => new TestDoubles.NoopEventStore());
	}

	/// <summary>
	/// Starts every registered <see cref="IHostedService"/>, which is where the guard lives. Startup validation
	/// is deliberately an <see cref="IHostedService"/> rather than a <c>BackgroundService</c>: a throw from
	/// <c>StartAsync</c> refuses the host deterministically before traffic, whereas a throw from
	/// <c>ExecuteAsync</c> is governed by host options and can be swallowed.
	/// </summary>
	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}
	}

	/// <summary>
	/// A cold store standing in for a genuinely tenant-partitioned provider. Its bodies are unreachable in this
	/// lock — the guard inspects registrations, never calls the store — so they throw rather than return
	/// plausible values, so that a future test which accidentally depends on cold-store behaviour fails loudly
	/// instead of silently trusting a fake.
	/// </summary>
	private sealed class TenantAwareColdEventStore : IColdEventStore
	{
		public Task<long> WriteAsync(
			KeyedTenantPartition tenant,
			string aggregateId,
			IReadOnlyList<StoredEvent> events,
			CancellationToken cancellationToken) => throw new NotSupportedException(NotExercised);

		public Task<IReadOnlyList<StoredEvent>> ReadAsync(
			KeyedTenantPartition tenant,
			string aggregateId,
			CancellationToken cancellationToken) => throw new NotSupportedException(NotExercised);

		public Task<IReadOnlyList<StoredEvent>> ReadAsync(
			KeyedTenantPartition tenant,
			string aggregateId,
			long fromVersion,
			CancellationToken cancellationToken) => throw new NotSupportedException(NotExercised);

		public Task<bool> HasArchivedEventsAsync(
			KeyedTenantPartition tenant,
			string aggregateId,
			CancellationToken cancellationToken) => throw new NotSupportedException(NotExercised);

		private const string NotExercised =
			"This lock asserts startup wiring only; the cold store is never invoked. Reaching this means the "
			+ "test is measuring something other than what it claims.";
	}
}
