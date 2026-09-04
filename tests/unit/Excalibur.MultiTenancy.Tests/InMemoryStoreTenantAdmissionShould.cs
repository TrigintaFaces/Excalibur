// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.InMemory;

using Excalibur.Dispatch;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Runtime lock on the tenant gate for two contracts that were registered without an attestation: a host
/// wired through the production path is <b>admitted</b> by row-discriminator multi-tenancy, presents the
/// scoped capability for both contracts, and then actually <b>serves reads and writes</b> afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> These stores read the ambient tenant on every path - the tenant term is part of the
/// event-stream key and of the snapshot key - and both registered through a bare <c>AddSingleton</c> that
/// emitted no capability marker. <c>AddMultiTenancy</c> therefore threw at startup for stores that do exactly
/// what the gate requires. That is a gate rejecting a correct host, not a leak: the safety property held
/// perfectly and the liveness property was broken, which is the failure a suite of safety-only arms is
/// structurally incapable of seeing.
/// </para>
/// <para>
/// <b>Why all three arms.</b> The admission arm alone is satisfied by a gate that checks nothing. The
/// round-trip arm alone is satisfied by a store nobody gated. The isolation arm is the third leg: an
/// attestation the store does not honor would be the lying-marker defect, strictly worse than the gap it
/// replaced, because it converts a loud startup refusal into a silent cross-tenant read.
/// </para>
/// <para>
/// <b>Real container, production path, no infrastructure.</b> Every arm wires the host through the provider's
/// own public registration method and resolves from a real <see cref="ServiceProvider"/>. A lock that
/// registered the marker itself would prove only that the gate reads a marker it was handed. These providers
/// keep their state in memory, so the round-trip is real rather than mocked and needs no emulator.
/// </para>
/// <para>
/// <b>What turns this red.</b> Change either provider's <c>AddTenantAwareStore</c> call back to a bare
/// <c>AddSingleton</c> and the admission arm throws, because the marker disappears with the seam that emits
/// it. Drop the tenant term from either store's key and the isolation arm fails.
/// </para>
/// </remarks>
public sealed class InMemoryStoreTenantAdmissionShould
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    [Fact]
    public void AdmitAnEventStoreAndSnapshotStoreWiredThroughTheProductionPath()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddInMemoryEventStore("inmemory");
        _ = services.AddInMemorySnapshotStore();

        // Reaching past this line is the assertion. Before these two providers were moved onto the
        // tenant-aware seam this threw, so a consumer could not turn on row-discriminator multi-tenancy at
        // all while using them - including the test hosts the in-memory providers exist to serve.
        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT these correctly wired stores. Both read the ambient tenant on every "
            + "path and make it part of their key, which is exactly the mechanism ITenantScopingCapability "
            + "attests. Rejecting them is the gate refusing a correct host, and that is invisible to every "
            + "safety-only arm on these contracts because they all assert a refusal.");

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantScopingCapability<IEventStore>>().ShouldNotBeNull(
            "The in-memory event store must present ITenantScopingCapability<IEventStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration. Without it every host using this "
            + "provider is refused by RowDiscriminator.");

        _ = provider.GetRequiredService<ITenantScopingCapability<ISnapshotStore>>().ShouldNotBeNull(
            "The in-memory snapshot store must present ITenantScopingCapability<ISnapshotStore>. Fixing one "
            + "of these two and leaving the other is the failure mode that looks done: the host is still "
            + "refused, by the other contract.");
    }

    [Fact]
    public async Task StillServeReadsAndWritesAfterBeingAdmitted()
    {
        using var provider = BuildAdmittedHost();
        var store = provider.GetRequiredKeyedService<IEventStore>("inmemory");
        var aggregateId = Guid.NewGuid().ToString();

        using (TenantContextHolder.BeginScope(TenantA))
        {
            // -1, not 0: an empty stream's current version is -1, and this store reports a version
            // mismatch by RETURNING a concurrency conflict rather than throwing. Asserting Success is
            // therefore load-bearing -- an unchecked append silently writes nothing, and the read below
            // would then be empty for a reason that has nothing to do with tenancy.
            var appended = await store.AppendAsync(
                    aggregateId,
                    nameof(TestAggregate),
                    [new TestEvent()],
                    -1,
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            appended.Success.ShouldBeTrue(
                "The append must actually land before anything is concluded from a read. This store signals "
                + "a version mismatch by return value, so an unasserted append turns every downstream "
                + "assertion into a statement about an empty store.");

            var loaded = await store
                .LoadAsync(aggregateId, nameof(TestAggregate), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            loaded.ShouldNotBeEmpty(
                "The admitted event store must still serve a read. An admission arm alone is satisfied by a "
                + "store that does nothing at all - inaction is the cheapest way to look safe and the most "
                + "expensive way to be wrong. This is the arm that fails if the seam admitted a store the "
                + "container cannot actually build or the decorator cannot drive.");
        }
    }

    [Fact]
    public async Task NotServeOneTenantsEventsToAnother()
    {
        using var provider = BuildAdmittedHost();
        var store = provider.GetRequiredKeyedService<IEventStore>("inmemory");
        var aggregateId = Guid.NewGuid().ToString();

        using (TenantContextHolder.BeginScope(TenantA))
        {
            // -1, not 0: an empty stream's current version is -1, and this store reports a version
            // mismatch by RETURNING a concurrency conflict rather than throwing. Asserting Success is
            // therefore load-bearing -- an unchecked append silently writes nothing, and the read below
            // would then be empty for a reason that has nothing to do with tenancy.
            var appended = await store.AppendAsync(
                    aggregateId,
                    nameof(TestAggregate),
                    [new TestEvent()],
                    -1,
                    TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            appended.Success.ShouldBeTrue(
                "The append must actually land before anything is concluded from a read. This store signals "
                + "a version mismatch by return value, so an unasserted append turns every downstream "
                + "assertion into a statement about an empty store.");
        }

        using (TenantContextHolder.BeginScope(TenantB))
        {
            var seenByB = await store
                .LoadAsync(aggregateId, nameof(TestAggregate), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            seenByB.ShouldBeEmpty(
                "Tenant B must not see tenant A's events through the SAME aggregate id. This is the property "
                + "the capability marker attests. If it fails the marker is a lying attestation - and a "
                + "marker on a store that does not scope is worse than no marker, because it converts a loud "
                + "startup refusal into a silent cross-tenant read.");
        }

        using (TenantContextHolder.BeginScope(TenantA))
        {
            var seenByA = await store
                .LoadAsync(aggregateId, nameof(TestAggregate), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);

            seenByA.ShouldNotBeEmpty(
                "Tenant A must still see its OWN events. Without this arm the isolation assertion above is "
                + "satisfied by a store that returns nothing to anybody, forever.");
        }
    }

    /// <summary>
    /// Builds a host through the production registration path, admitted under row-discrimination. The ambient
    /// tenant is established with TenantContextHolder.BeginScope - the same mechanism a real host uses -
    /// rather than a hand-injected context, so the arms drive the wiring the gate actually installs.
    /// </summary>
    private static ServiceProvider BuildAdmittedHost()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddInMemoryEventStore("inmemory");
        _ = services.AddInMemorySnapshotStore();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        return services.BuildServiceProvider();
    }

    private sealed class TestAggregate;

    /// <summary>
    /// A minimal domain event implementing the interface directly rather than through a first-party base, so
    /// the round trip binds the contract the store actually persists.
    /// </summary>
    [MessageName("Test.InMemoryStoreTenantAdmission.TestEvent")]
    private sealed class TestEvent : IDomainEvent
    {
        public string EventId { get; } = Guid.NewGuid().ToString();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;


        public IDictionary<string, object>? Metadata => null;
    }
}
