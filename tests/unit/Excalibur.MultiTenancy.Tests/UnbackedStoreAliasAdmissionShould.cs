// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.InMemory;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Runtime lock on the inverse failure: the tenant gate must not demand a capability of a store that
/// <b>nobody registered</b>. Core event sourcing registers non-keyed <see cref="IEventStore"/> and
/// <see cref="ISnapshotStore"/> convenience aliases unconditionally, so the contract is present in the
/// collection with nothing behind it — most visibly the snapshot store, which is optional.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the defect looked like.</b> The gate's totality sweep requires every registered tenant-owned
/// contract to attest a capability, and it fails closed. Reading a forwarding alias as a registration made
/// it demand tenant-scoping of a store no provider supplied, so an event-sourced host with an event store
/// and no snapshot store was refused at startup — a gate rejecting a deployment whose store does not exist.
/// </para>
/// <para>
/// <b>What must NOT be done about it.</b> The temptation is to make the refusal go away by attesting the
/// alias. That is the lying-marker defect: it converts a loud startup refusal into a silent admission of
/// whatever store a consumer later hangs on that contract. The correct resolution distinguishes "a store is
/// registered" from "something promises this contract and nothing provides it", which is what the alias seam
/// marks and the sweep reads — and it must leave the refusal of a genuinely tenant-unaware store intact.
/// That is why the last arm here matters as much as the first.
/// </para>
/// <para>
/// <b>Lifetime, deliberately.</b> The alias forwards at <see cref="ServiceLifetime.Singleton"/>, matching
/// what the providers register. A scoped alias over a factory-returned root-owned store would let the
/// container capture an <see cref="IAsyncDisposable"/> store in the RESOLVING scope, so disposing one
/// request scope would dispose the store every other request shares.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class UnbackedStoreAliasAdmissionShould
{
    // ---- LIVENESS: a host is not refused over a store nobody registered ------------------------------

    [Fact]
    public void RefuseEventSourcingWithNoStoreProviderAtAll_ForHavingNothingToScope_NotForAnUnbackedAlias()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static x => x.AddEventSourcing());

        // A provider-less host is still refused — but the REASON is what this arm pins. Both store contracts
        // are present here only as forwarding aliases, and the sweep now sees them for what they are: it
        // finds no tenant-owned store at all, and says so. The defect was the other message, which named a
        // contract and blamed its provider for not being tenant-scoping-capable when no provider existed.
        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        thrown.Message.ShouldContain(
            "no tenant-owned store is registered",
            Case.Sensitive,
            "A host with no store provider must be refused for having nothing to scope — an honest, "
            + "actionable message about a configuration that genuinely makes no sense.");

        thrown.Message.ShouldNotContain(
            "not tenant-scoping-capable",
            Case.Sensitive,
            "This is the defect. Reading a forwarding alias as a registration made the gate name a store "
            + "contract and blame its provider for lacking a tenancy capability, when no provider had "
            + "registered a store at all. A consumer reading that goes looking for a provider defect that "
            + "does not exist, and the obvious way to silence it — attesting the alias — is the "
            + "lying-marker defect, which admits whatever store is later hung on that contract.");
    }

    [Fact]
    public void AdmitAnEventStoreWithNoSnapshotStore_UnderRowDiscriminator()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static x => x.AddEventSourcing());

        // An event store and deliberately NO snapshot store: snapshots are optional, and this is the
        // asymmetry that made the defect visible on providers shipping a granular event-store-only verb
        // while others register both stores together inseparably.
        _ = services.AddInMemoryEventStore();

        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "An event-sourced host with an event store and no snapshot store must be admitted. The "
            + "ISnapshotStore alias is unbacked; refusing on it rejects the most ordinary event-sourcing "
            + "configuration there is.");
    }

    [Fact]
    public void ResolveTheEventStoreThroughTheUnkeyedAlias_AfterTheGateAdmitsIt()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static x => x.AddEventSourcing());
        _ = services.AddInMemoryEventStore();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // The liveness half: skipping the alias in the sweep must not have made it unresolvable. A host
        // admitted at startup and unable to resolve its store afterwards has simply moved the failure.
        _ = provider.GetRequiredService<IEventStore>().ShouldNotBeNull(
            "The non-keyed IEventStore alias must still resolve after the gate admits the host. The sweep "
            + "classifies the descriptor; it must not change what the descriptor does.");
    }

    [Fact]
    public void LeaveTheUnbackedSnapshotAliasUnresolvable_RatherThanSilentlySubstituting()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static x => x.AddEventSourcing());
        _ = services.AddInMemoryEventStore();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // The honest consequence of an unbacked alias: it fails when RESOLVED, where the consumer asked for
        // a store they never registered, not at startup where the whole host is refused for it. This arm
        // pins that the admission above did not buy itself a silent substitute.
        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<ISnapshotStore>(),
            "An unbacked snapshot alias must fail where it is resolved, not be quietly satisfied. If this "
            + "ever returns an instance, some registration is standing in for a store the consumer never "
            + "chose, and the admission arms above would then be hiding a substitution rather than "
            + "recording an absence.");
    }

    // ---- SAFETY: the gate still refuses a real store that implements no tenancy mechanism -------------

    [Fact]
    public void StillRefuseARegisteredEventStoreThatPresentsNoCapability()
    {
        var services = new ServiceCollection();
        _ = services.AddExcalibur(static x => x.AddEventSourcing());

        // Not an alias: a real, non-forwarding registration of the contract, with no attestation. This is
        // what a genuinely tenant-unaware provider looks like to the sweep.
        _ = services.AddSingleton<IEventStore>(new TenancyBlindEventStore());

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "The gate must STILL refuse a registered event store that attests nothing. Without this arm the "
            + "admission arms above are satisfied by a sweep that skips everything — the cheapest way to "
            + "stop rejecting correct hosts is to stop checking, and that admits a tenancy-blind store to "
            + "read every tenant's events.");
    }

    /// <summary>An event store with no tenancy mechanism — the shape the gate must keep refusing.</summary>
    private sealed class TenancyBlindEventStore : IEventStore
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
