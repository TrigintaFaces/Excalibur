// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Holds the tenant-capability gate independent of the order in which a consumer composes their container.
/// </summary>
/// <remarks>
/// <para>
/// The composition-time sweep enumerates <see cref="IServiceCollection"/> at the instant
/// <c>AddMultiTenancy</c> runs. A service collection is a mutable list, so a tenant-owned store registered
/// <em>after</em> that call is never in the enumeration, the assertion never evaluates for it, and the host
/// starts into the unconfined configuration with no error. Registering multi-tenancy in a composition root
/// and adding persistence in later feature modules is an ordinary DI arrangement, not misuse -- so the
/// unsafe order is reachable by a consumer following normal practice, and a documented precondition is not
/// an enforcement mechanism.
/// </para>
/// <para>
/// Reversing two registration calls must not change whether a safety gate runs. The property these arms
/// hold is that one, stated over the outcome a consumer can observe -- does the host start? -- rather than
/// over the mechanism that decides it, because a lock written from an assumed mechanism goes blind the
/// moment the mechanism moves.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantOwnedGateIsOrderIndependentShould
{
	/// <summary>
	/// SAFETY: a tenant-owned store registered AFTER <c>AddMultiTenancy</c> is still refused.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm the composition-time sweep cannot satisfy. It is placed against the outcome, not the
	/// instant: composition is allowed to succeed here, because at that moment there is genuinely nothing to
	/// refuse. What must not succeed is the host START, once the collection is complete and the unattested
	/// store is in it.
	/// </para>
	/// <para>
	/// NON-VACUITY: delete the <c>TenantOwnedCapabilityStartupValidator</c> registration from
	/// <c>AddMultiTenancy</c> -- the single line that re-asserts the sweep against the finished collection --
	/// and this arm is RED, because nothing else in the framework ever looks at a late registration. The
	/// message assertions pin the refusal to the capability gate for this contract specifically, so an
	/// unrelated startup failure cannot be mistaken for the gate doing its job.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RefuseAnUnattestedTenantOwnedStoreRegisteredAfterAddMultiTenancy()
	{
		var services = new ServiceCollection();

		// A capable store present at composition time, so the sweep has something to accept and the failure
		// below cannot be the "no store registered" guard firing instead of the capability gate.
		_ = services.AddTenantAwareStore<IEventStore, NoopEventStore>(
			sp => new NoopEventStore(sp.GetRequiredService<ITenantContext>()));

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// The late registration. Models every provider that registers a plain store without going through
		// the seam that emits the capability -- and it lands where the sweep has already run.
		_ = services.AddSingleton<ISagaStore>(A.Fake<ISagaStore>());

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider));

		// The reasons sit in comments rather than as customMessage arguments: ex.Message is a string, which is
		// also IEnumerable<char>, so the two-argument form binds the element-predicate overload and will not compile.
		//
		// The refusal must NAME the unattested contract, or an operator cannot act on it.
		ex.Message.ShouldContain(nameof(ISagaStore));

		// And it must be the tenant-capability gate rather than an unrelated startup failure standing in for it:
		// an arm that accepts any throw passes against a broken container.
		ex.Message.ShouldContain("capability");
	}

	/// <summary>
	/// LIVENESS: a correctly ordered, fully attested configuration still starts.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above on purpose. "Refuses the unattested store" is satisfied completely by a
	/// guard that refuses every host, and refusing everything is the cheapest way to look safe. This arm is
	/// the one that fails if the re-assertion over-corrects -- if, for instance, it read the completed
	/// collection through a probe that cannot see keyed registrations and therefore reported every provider
	/// as unattested.
	/// </remarks>
	[Fact]
	public async Task StartAHostWhoseTenantOwnedStoresAllPresentACapability()
	{
		var services = new ServiceCollection();

		_ = services.AddTenantAwareStore<IEventStore, NoopEventStore>(
			sp => new NoopEventStore(sp.GetRequiredService<ITenantContext>()));

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		// Also registered late -- but through the seam that emits the marker inseparably from the store. The
		// gate is about attestation, never about ordering for its own sake.
		_ = services.AddTenantAwareStore<ISagaStore, NoopSagaStore>(
			sp => new NoopSagaStore(sp.GetRequiredService<ITenantContext>()));

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));

		// ...and BOTH attestations are present, including the one registered after AddMultiTenancy. This is the
		// assertion that matters for this arm: the marker is what the gate reads, so a re-assertion that over-
		// corrected -- reporting a late registration as unattested -- would fail here rather than merely letting
		// the host boot. Asserting the marker is stronger than resolving the store, because a store can resolve
		// through a registration the gate never saw.
		_ = provider.GetRequiredService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull();
		_ = provider.GetRequiredService<ITenantScopingCapability<IEventStore>>().ShouldNotBeNull();
	}

	/// <summary>
	/// SAFETY: the same refusal reaches a contract whose coverage came only from the composition-time block.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The order-independent re-assertion derives its subject from each contract's own
	/// <see cref="TenantOwnedAttribute"/> declaration, so a tenant-owned contract that never declared one is
	/// covered by nothing but the composition-time sweep -- exactly the enumeration a late registration is
	/// invisible to. Three contracts were listed as tenant-owned and gated by name while declaring nothing:
	/// the inbox, the outbox, and the cold event store. Measured, they were in three different states. The
	/// cold store already had a validator of its own that runs against the built provider, so its arm held
	/// before any declaration was added and is kept here as the regression lock for that second mechanism.
	/// The inbox and the outbox had nothing. The outbox could not be brought under the attribute sweep at
	/// all -- that sweep is a floor accepting either capability, and the outbox is the one contract for which
	/// one of the two is a false claim, so admitting it there would admit the ambient-scoping attestation its
	/// own gate exists to reject. What closes both is the startup validator replaying the specific
	/// per-contract requirements against the finished collection, which is the same list composition time
	/// evaluates rather than a second copy of it.
	/// </para>
	/// <para>
	/// NON-VACUITY: delete the <c>AssertPerContractCapabilities</c> call from
	/// <c>TenantOwnedCapabilityStartupValidator.Validate</c> and the inbox and outbox arms are RED, because
	/// nothing else demands the correct marker of a late registration. The cold-store arm is held instead by
	/// <c>ColdStoreTenantScopingValidator</c>; deleting that validator's registration is what turns it red.
	/// </para>
	/// </remarks>
	[Fact]
	public Task RefuseAnUnattestedInboxStoreRegisteredAfterAddMultiTenancy() =>
		RefuseLateUnattestedRegistrationOf(A.Fake<IInboxStore>());

	/// <inheritdoc cref="RefuseAnUnattestedInboxStoreRegisteredAfterAddMultiTenancy"/>
	[Fact]
	public Task RefuseAnUnattestedOutboxStoreRegisteredAfterAddMultiTenancy() =>
		RefuseLateUnattestedRegistrationOf(A.Fake<IOutboxStore>());

	/// <inheritdoc cref="RefuseAnUnattestedInboxStoreRegisteredAfterAddMultiTenancy"/>
	[Fact]
	public Task RefuseAnUnattestedWorkflowSignalInboxRegisteredAfterAddMultiTenancy() =>
		RefuseLateUnattestedRegistrationOf(A.Fake<Excalibur.Workflows.IWorkflowSignalInbox>());

	/// <inheritdoc cref="RefuseAnUnattestedInboxStoreRegisteredAfterAddMultiTenancy"/>
	[Fact]
	public Task RefuseAnUnattestedColdEventStoreRegisteredAfterAddMultiTenancy() =>
		RefuseLateUnattestedRegistrationOf(A.Fake<IColdEventStore>());

	/// <summary>
	/// Composes a host whose only unattested store is registered after <c>AddMultiTenancy</c>, and asserts the
	/// start is refused naming that contract.
	/// </summary>
	/// <typeparam name="TContract">The tenant-owned store contract under test.</typeparam>
	/// <param name="unattestedStore">A store registered without going through the seam that emits a capability.</param>
	private static async Task RefuseLateUnattestedRegistrationOf<TContract>(TContract unattestedStore)
		where TContract : class
	{
		var services = new ServiceCollection();

		// A capable store present at composition time, so the failure below cannot be the "no tenant-owned
		// store is registered" guard firing instead of the capability gate.
		_ = services.AddTenantAwareStore<IEventStore, NoopEventStore>(
			sp => new NoopEventStore(sp.GetRequiredService<ITenantContext>()));

		_ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

		_ = services.AddSingleton(unattestedStore);

		await using var provider = services.BuildServiceProvider();

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider));

		ex.Message.ShouldContain(typeof(TContract).Name);
		ex.Message.ShouldContain("capability");
	}

	/// <summary>
	/// Starts every registered <see cref="IHostedService"/>, which is where the order-independent half of the
	/// gate lives.
	/// </summary>
	/// <remarks>
	/// The guard is deliberately an <see cref="IHostedService"/> rather than a <c>BackgroundService</c>: a
	/// throw from <c>StartAsync</c> refuses the host deterministically and before traffic, whereas a throw
	/// from <c>ExecuteAsync</c> is governed by <c>HostOptions.BackgroundServiceExceptionBehavior</c> and runs
	/// asynchronously to startup, so the host may already be serving requests before the gate fires.
	/// </remarks>
	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}
	}
}
