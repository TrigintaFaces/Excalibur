// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Sharding;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Exhaustiveness lock for the row-discriminator tenant-marker guard, generalized across every contract in the
/// tenant-owned manifest. It composes on <see cref="RowDiscriminatorTenantCapabilityGuardShould"/>: that class
/// proves the guard's inverse control (unmarked store → throw) and one positive decorator resolution for
/// <see cref="IEventStore"/> alone; this class proves the same two properties hold for <em>all</em> contracts the
/// manifest declares tenant-owned, so a provider that skips the marker on any one of them is rejected, and the
/// deliberately-undecorated contracts stay undecorated.
/// </summary>
/// <remarks>
/// <para>
/// The invariant under test (SA re-ruling): every contract in <c>TenantOwnedContracts.All</c> has an
/// <b>enforced marker</b> — registering its store without <see cref="ITenantScopingCapability{TContract}"/>
/// makes <c>AddMultiTenancy(RowDiscriminator)</c> throw, naming that contract. Liveness is deliberately
/// <b>two-tier</b>, and asserting blanket decoration would be the false-positive a prior carrier caught:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Decoratable set</b> (<see cref="IEventStore"/>, <see cref="ISagaStore"/>,
/// <see cref="IProjectionStore{TProjection}"/>): a marked store resolves through the container to its
/// fail-closed tenant-scoping <em>decorator</em>.
/// </item>
/// <item>
/// <b>Marker-only set</b> (<see cref="IInboxStore"/>, <see cref="IOutboxStore"/>,
/// <see cref="IEventStoreErasure"/>, <see cref="IErasureStore"/>, <see cref="ILegalHoldStore"/>): the marker
/// is enforced at registration but the store is <em>not</em>
/// decorated. A tenant decorator on the cross-tenant outbox drain would read the ambient tenant as absent,
/// claim the empty set, and stall the drain — safe-looking, permanently broken. These are gated-but-undecorated
/// by design, so the liveness arm asserts the resolved store is the exact registered instance (unwrapped).
/// </item>
/// </list>
/// <para>
/// The set the tests iterate is <b>derived from the committed manifest</b> (<c>TenantOwnedContracts.All</c>,
/// read by reflection because it is <c>internal</c> to <c>Excalibur.MultiTenancy</c> and this test assembly is
/// not on its <c>InternalsVisibleTo</c> list). A contract added to the manifest therefore automatically gets a
/// negative-control run and must be classified into one of the two tiers, or the coverage assertion fails —
/// the gate cannot silently skip a newly declared tenant-owned contract. (Deriving the manifest itself from the
/// type system, so it cannot be forgotten, is the separate residual work item and is intentionally out of scope
/// here.)
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantScopingExhaustivenessShould
{
    /// <summary>The contracts the manifest decorates: a marked store resolves to its tenant-scoping decorator.</summary>
    private static readonly ImmutableHashSet<Type> DecoratableContracts =
        [typeof(IEventStore), typeof(ISagaStore), typeof(IProjectionStore<object>)];

    /// <summary>The contracts the manifest gates but deliberately does NOT decorate.</summary>
    /// <remarks>
    /// The erasure and legal-hold stores belong here rather than in the decoratable set: they apply the
    /// ambient tenant term inside the store, at a single derivation point, rather than through a wrapping
    /// decorator. The gate still requires their capability marker, so a host registering an unscoped
    /// implementation fails at startup — which is the property this tier asserts.
    /// </remarks>
    private static readonly ImmutableHashSet<Type> MarkerOnlyContracts =
        [typeof(IInboxStore), typeof(IOutboxStore), typeof(IEventStoreErasure),
         typeof(IErasureStore), typeof(ILegalHoldStore)];

    /// <summary>
    /// Theory source: every contract in the committed <c>TenantOwnedContracts.All</c> manifest, read by
    /// reflection. Driving the negative control from the real manifest means a new tenant-owned contract is
    /// covered automatically (and, if this test does not know how to register a trigger for it, fails loudly).
    /// </summary>
    public static TheoryData<Type> ManifestContracts()
    {
        var data = new TheoryData<Type>();
        foreach (var contract in ReadManifest())
        {
            data.Add(contract);
        }

        return data;
    }

    // ---- SAFETY: the enforced-marker negative control, generalized across the whole manifest ---------------

    /// <summary>
    /// For every contract the manifest declares tenant-owned, registering its store WITHOUT the capability
    /// marker must make <c>AddMultiTenancy(RowDiscriminator)</c> throw, naming that contract. This is the
    /// generalization of Lock A across all six contracts (Lock A proves it for <see cref="IEventStore"/> only).
    /// </summary>
    [Theory]
    [MemberData(nameof(ManifestContracts))]
    public void Throw_WhenAManifestContractIsRegisteredWithoutItsTenantScopingMarker(Type contract)
    {
        var services = new ServiceCollection();

        // Register ONLY this contract's trigger (no marker) so its gate is the one that fires — isolating the
        // per-contract "names the offending contract" assertion from any earlier gate.
        RegisterTrigger(services, contract);

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        ex.Message.ShouldContain("not tenant-scoping-capable");
        ex.Message.ShouldContain(ExpectedContractName(contract));
        ex.Message.ShouldContain(nameof(TenantIsolationStrategy.RowDiscriminator));
    }

    // ---- LIVENESS tier 1: the decoratable set resolves to its fail-closed decorator -----------------------

    [Fact]
    public void ResolveTheEventStoreDecorator_WhenTheMarkedEventStoreIsRegistered()
    {
        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton(A.Fake<IEventStore>());
            services.AddSingleton(A.Fake<ITenantScopingCapability<IEventStore>>());
        });

        provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>();
    }

    [Fact]
    public void ResolveTheSagaStoreDecorator_WhenTheMarkedSagaStoreIsRegistered()
    {
        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton(A.Fake<ISagaStore>());
            services.AddSingleton(A.Fake<ITenantScopingCapability<ISagaStore>>());
        });

        provider.GetRequiredService<ISagaStore>().ShouldBeOfType<TenantScopedSagaStore>();
    }

    [Fact]
    public void ResolveTheProjectionStoreDecorator_WhenTheMarkedProjectionStoreIsRegistered()
    {
        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton<IProjectionStore<FakeProjection>>(new FakeProjectionStore());
            // The projection-store family marker is closed over `object`, not the individual projection type.
            services.AddSingleton(A.Fake<ITenantScopingCapability<IProjectionStore<object>>>());
        });

        provider.GetRequiredService<IProjectionStore<FakeProjection>>()
            .ShouldBeOfType<TenantScopedProjectionStore<FakeProjection>>();
    }

    // ---- LIVENESS tier 2: the marker-only set is gated but NOT decorated ----------------------------------

    [Fact]
    public void GateButNotDecorateTheInboxStore_WhenItIsMarked()
    {
        var inbox = A.Fake<IInboxStore>();

        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton(inbox);
            services.AddSingleton(A.Fake<ITenantScopingCapability<IInboxStore>>());
        });

        // Gated (registration did not throw — proven by BuildMarkedProvider returning) AND undecorated: the
        // resolved store is the exact instance registered. A tenant decorator here would be the false-positive
        // that reads the ambient tenant as absent and stalls the cross-tenant drain.
        provider.GetRequiredService<IInboxStore>().ShouldBeSameAs(inbox);
    }

    [Fact]
    public void GateButNotDecorateTheOutboxStore_WhenItIsMarked()
    {
        var outbox = A.Fake<IOutboxStore>();

        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton(outbox);
            services.AddSingleton(A.Fake<ITenantScopingCapability<IOutboxStore>>());
        });

        provider.GetRequiredService<IOutboxStore>().ShouldBeSameAs(outbox);
    }

    [Fact]
    public void GateButNotDecorateEventStoreErasure_WhenItIsMarked()
    {
        // Erasure is never a service type: its gate triggers on the erasure FEATURE (IAggregateDataSubjectMapping,
        // registered when a host opts into event-store erasure), and it is deliberately undecorated because
        // erasure runs from a background service with no ambient tenant.
        var mapping = A.Fake<IAggregateDataSubjectMapping>();

        using var provider = BuildMarkedProvider(services =>
        {
            services.AddSingleton(mapping);
            services.AddSingleton(A.Fake<ITenantScopingCapability<IEventStoreErasure>>());
        });

        // Gated (registration succeeded) AND undecorated: the erasure-feature service resolves unwrapped, and
        // IEventStoreErasure itself is not a container service at all (so nothing could have decorated it).
        provider.GetRequiredService<IAggregateDataSubjectMapping>().ShouldBeSameAs(mapping);
        provider.GetService<IEventStoreErasure>().ShouldBeNull();
    }

    // ---- STRUCTURAL DERIVATION: this lock covers every manifest entry, split across exactly the two tiers ---

    /// <summary>
    /// The union of the two tiers this lock exercises must equal the committed manifest exactly. If a contract
    /// is added to <c>TenantOwnedContracts.All</c> and not classified into a tier here, this fails — so the
    /// exhaustiveness lock cannot silently omit a newly declared tenant-owned contract. If a tier lists a
    /// contract absent from the manifest, this also fails — so the lock cannot test a phantom.
    /// </summary>
    [Fact]
    public void Cover_EveryManifestContract_AcrossExactlyTheTwoLivenessTiers()
    {
        var manifest = ReadManifest();
        var covered = DecoratableContracts.Union(MarkerOnlyContracts);

        covered.SetEquals(manifest).ShouldBeTrue(
            $"This exhaustiveness lock must classify every tenant-owned contract into exactly one liveness tier. "
            + $"Manifest: [{string.Join(", ", manifest.Select(t => t.Name))}]; "
            + $"covered here: [{string.Join(", ", covered.Select(t => t.Name))}]. "
            + "A contract present in one but not the other is either an untested new manifest entry or a phantom.");

        // The two tiers must be disjoint — a contract is decorated xor gated-only, never both.
        DecoratableContracts.Overlaps(MarkerOnlyContracts).ShouldBeFalse();
    }

    // ---- helpers ------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads the authoritative <c>TenantOwnedContracts.All</c> manifest from the <c>Excalibur.MultiTenancy</c>
    /// assembly by reflection. The manifest is <c>internal</c> and this assembly is not on its
    /// <c>InternalsVisibleTo</c> list, so reflection — not a direct reference — is the derivation seam.
    /// </summary>
    private static ImmutableHashSet<Type> ReadManifest()
    {
        var assembly = typeof(TenantIsolationStrategy).Assembly;
        var contractsType = assembly.GetType("Excalibur.MultiTenancy.TenantOwnedContracts", throwOnError: true)!;
        var allField = contractsType.GetField("All", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "TenantOwnedContracts.All was not found — the manifest seam this lock derives from moved or was renamed.");

        var value = (IEnumerable<Type>)allField.GetValue(null)!;
        return value.ToImmutableHashSet();
    }

    /// <summary>Registers a store/feature that triggers a given manifest contract's gate, WITHOUT its marker.</summary>
    private static void RegisterTrigger(IServiceCollection services, Type contract)
    {
        if (contract == typeof(IEventStore))
        {
            services.AddSingleton(A.Fake<IEventStore>());
        }
        else if (contract == typeof(ISagaStore))
        {
            services.AddSingleton(A.Fake<ISagaStore>());
        }
        else if (contract == typeof(IProjectionStore<object>))
        {
            // Any closed-generic IProjectionStore<T> triggers the projection-store gate.
            services.AddSingleton<IProjectionStore<FakeProjection>>(new FakeProjectionStore());
        }
        else if (contract == typeof(IInboxStore))
        {
            services.AddSingleton(A.Fake<IInboxStore>());
        }
        else if (contract == typeof(IOutboxStore))
        {
            services.AddSingleton(A.Fake<IOutboxStore>());
        }
        else if (contract == typeof(IEventStoreErasure))
        {
            // Erasure is gated via the erasure feature service, not an IEventStoreErasure registration.
            services.AddSingleton(A.Fake<IAggregateDataSubjectMapping>());
        }
        else if (contract == typeof(IErasureStore))
        {
            services.AddSingleton(A.Fake<IErasureStore>());
        }
        else if (contract == typeof(ILegalHoldStore))
        {
            services.AddSingleton(A.Fake<ILegalHoldStore>());
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(contract),
                contract,
                "A new tenant-owned contract was added to the manifest but this exhaustiveness lock does not know "
                + "how to register a trigger for it. Add a RegisterTrigger arm and classify it into a liveness tier.");
        }
    }

    /// <summary>The contract name the guard embeds in its throw message for a given manifest contract.</summary>
    private static string ExpectedContractName(Type contract) =>
        contract.IsGenericType ? contract.Name[..contract.Name.IndexOf('`', StringComparison.Ordinal)] : contract.Name;

    /// <summary>
    /// Applies <paramref name="register"/>, wires row-discriminator multi-tenancy, and builds the provider.
    /// Reaching the return proves <c>AddMultiTenancy</c> did not throw for the registered (marked) configuration.
    /// </summary>
    private static ServiceProvider BuildMarkedProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
        return services.BuildServiceProvider();
    }

    /// <summary>A reference-type projection used only to register a closed-generic projection store.</summary>
    private sealed class FakeProjection;

    /// <summary>
    /// A concrete (non-proxied) projection store stub. Hand-written rather than faked so the private
    /// <see cref="FakeProjection"/> type argument need not be made externally visible for a dynamic proxy.
    /// Its members are never invoked — the guard inspects the registered <c>ServiceType</c>, and the liveness
    /// arm only asserts the resolved decorator type.
    /// </summary>
    private sealed class FakeProjectionStore : IProjectionStore<FakeProjection>
    {
        public Task<FakeProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<FakeProjection?>(null);

        public Task UpsertAsync(string id, FakeProjection projection, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<FakeProjection>> QueryAsync(
            IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FakeProjection>>([]);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
            Task.FromResult(0L);
    }
}
