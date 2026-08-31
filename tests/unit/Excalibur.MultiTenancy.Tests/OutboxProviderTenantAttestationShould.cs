// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.MongoDB;

using MongoDB.Driver;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Author-independent lock on the <em>liveness</em> half of the outbox tenant gate: that a correctly wired
/// non-relational outbox provider is <b>admitted</b> by row-discriminator multi-tenancy, resolves, and
/// resolves undecorated.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> The gate demands <see cref="ITenantPartitionedCapability{TContract}"/> of
/// <see cref="IOutboxStore"/>, and only the three relational providers emitted it. Every other provider
/// that registers <see cref="IOutboxStore"/> — MongoDB among them — presented no marker at all, so
/// <c>AddMultiTenancy(RowDiscriminator)</c> threw at startup for a host that was <em>correctly wired</em>.
/// That is a gate rejecting correct hosts, not a leak: the safety property held perfectly and the liveness
/// property was broken, which is the failure a suite of safety-only arms is structurally incapable of
/// seeing. Every pre-existing arm on this contract asserts a refusal; the arms below assert an admission,
/// and they are the point of this file.
/// </para>
/// <para>
/// <b>Why MongoDB belongs on the partitioned seam and not the scoped one.</b> The store reads no ambient
/// tenant on any path. It persists the tenant on the document it writes and hands that value back on the
/// drain, so the owning tenant is re-established from the row. A store filtering on the ambient tenant here
/// would read it as absent at drain time, claim the empty set, and stall delivery for every tenant — while
/// passing any arm that only checks one tenant cannot see another tenant rows.
/// </para>
/// <para>
/// <b>What breaks these arms.</b> The marker is emitted only by <c>AddTenantAwareStore</c>, whose
/// emitter is private, so no provider can register a bare marker and no marker can outlive the registration
/// it attests — a decoupled marker is inexpressible rather than discouraged. The reachable one-token
/// mutation is therefore at the call site: change either <c>AddTenantAwareStore</c> in
/// <c>OutboxBuilderMongoDbExtensions</c> back to <c>TryAddSingleton</c> and the admission arm for that
/// connection shape goes red, because the marker disappears with the seam. Both shapes are covered because
/// each takes its own branch and has its own registration call — a fix applied to one leaves the other
/// rejected while looking done.
/// </para>
/// <para>
/// <b>Real container, production path.</b> Every arm builds a real <see cref="ServiceProvider"/> through
/// <c>AddExcalibur</c> to <c>AddOutbox</c> to <c>UseMongoDB</c> and resolves with
/// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>. A lock that hand-registered the
/// marker would prove only that the gate reads a marker it was handed, which is precisely how the original
/// lying-marker defect passed a full CI run. No MongoDB server is required: the store captures its client
/// and options without connecting.
/// </para>
/// <para>
/// <b>What these arms do not prove.</b> That the writes actually populate the discriminator. That is
/// observable only against real infrastructure and is held by the conformance round-trip, not by a
/// registration-time marker — as the seam contract itself states.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class OutboxProviderTenantAttestationShould
{
    /// <summary>Never opened: the store captures its connection inputs lazily.</summary>
    private const string UnusedConnectionString = "mongodb://localhost:27017";

    // ---- LIVENESS: the real gate ADMITS the real provider. This is the bead. ---------------------------

    [Theory]
    [InlineData(MongoConnectionShape.ConnectionString)]
    [InlineData(MongoConnectionShape.ClientFactory)]
    public void AdmitTheRealMongoDbOutbox_UnderRowDiscriminator_ForEitherConnectionShape(
        MongoConnectionShape shape)
    {
        var services = BuildMongoDbOutboxHost(shape);

        // Reaching past this line is the assertion. Before the fix this threw, so a consumer on MongoDB
        // could not turn on row-discriminator multi-tenancy at all.
        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT a correctly wired MongoDB outbox. This provider carries the tenant "
            + "on the document and re-establishes it on drain, which is exactly the mechanism "
            + "ITenantPartitionedCapability attests. Rejecting it is the gate refusing a correct host, and "
            + "that is invisible to every safety-only arm on this contract because they all assert a refusal.");
    }

    [Theory]
    [InlineData(MongoConnectionShape.ConnectionString)]
    [InlineData(MongoConnectionShape.ClientFactory)]
    public async Task ResolveTheMongoDbOutboxUndecorated_AfterTheGateAdmitsIt(MongoConnectionShape shape)
    {
        var services = BuildMongoDbOutboxHost(shape);
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        // Disposed asynchronously: this arm actually constructs the store, and the store is
        // IAsyncDisposable-only, so a synchronous container dispose throws.
        await using var provider = services.BuildServiceProvider();

        // Resolved through the real container, not asserted from a descriptor: a registration that cannot be
        // constructed satisfies a descriptor scan and still fails the consumer at runtime.
        var store = provider.GetRequiredKeyedService<IOutboxStore>("mongodb");

        _ = store.ShouldBeOfType<MongoDbOutboxStore>(
            "The outbox must resolve as the provider own store. A tenant-scoping wrapper on this contract "
            + "would read the ambient tenant as absent at drain time, claim the empty set, and stall "
            + "delivery for every tenant while looking safe.");

        // The keyed default alias is what a host without an explicit key resolves, so it must reach the same
        // admitted store rather than a second, unattested registration.
        provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldBeSameAs(
            store,
            "The default outbox alias must resolve to the same admitted store instance.");
    }

    [Theory]
    [InlineData(MongoConnectionShape.ConnectionString)]
    [InlineData(MongoConnectionShape.ClientFactory)]
    public void AttestRowPartitionedTenancy_ForEitherMongoConnectionShape(MongoConnectionShape shape)
    {
        using var provider = BuildMongoDbOutboxHost(shape).BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
            "The MongoDB outbox must present ITenantPartitionedCapability<IOutboxStore>, emitted by "
            + "AddTenantAwareStore inseparably from the store registration. Without it every host "
            + "using this provider is refused by RowDiscriminator.");
    }

    // ---- SAFETY: it attests the mechanism it has, and not the one it does not ---------------------------

    [Theory]
    [InlineData(MongoConnectionShape.ConnectionString)]
    [InlineData(MongoConnectionShape.ClientFactory)]
    public void NotAttestAmbientTenantScoping_ForEitherMongoConnectionShape(MongoConnectionShape shape)
    {
        using var provider = BuildMongoDbOutboxHost(shape).BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<IOutboxStore>>().ShouldBeNull(
            "The MongoDB outbox must not present ITenantScopingCapability<IOutboxStore>. That marker attests "
            + "the store applies the ambient tenant discriminator to every operation, and this store reads no "
            + "ambient tenant on any path. Presenting it is the lying-marker defect: the gate passes and the "
            + "documentation then describes a verification that did not happen.");
    }

    // ---- SAFETY: the gate still has teeth, and it has them against a KEYED registration -----------------

    [Fact]
    public void RejectAKeyedOutbox_ThatAttestsNothing()
    {
        var services = new ServiceCollection();

        // Every non-relational provider registers IOutboxStore KEYED, never as a plain service type. If the
        // gate descriptor scan did not see keyed registrations it would silently never fire for any of
        // them — an ungated outbox rather than a refused one — and the admission arms above would pass for
        // the wrong reason. This arm is what establishes that the gate reaches the registration shape those
        // providers actually use.
        _ = services.AddKeyedSingleton("mongodb", (IServiceProvider _, object? _) => A.Fake<IOutboxStore>());

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must reject a KEYED outbox registration that proves no tenant capability. If "
            + "this does not throw, the gate does not see keyed descriptors, and the admission arms above "
            + "are vacuous: they would pass against a provider that attests nothing at all.");

        thrown.Message.ShouldContain(
            nameof(IOutboxStore),
            Case.Sensitive,
            "The rejection must name the contract that failed, or a consumer cannot act on it.");
    }

    /// <summary>The two connection shapes <c>UseMongoDB</c> supports; each takes its own registration branch.</summary>
    public enum MongoConnectionShape
    {
        /// <summary>Configured with a connection string; the store is DI-constructed.</summary>
        ConnectionString,

        /// <summary>Configured with an <see cref="IMongoClient"/> factory; the store is factory-constructed.</summary>
        ClientFactory,
    }

    /// <summary>Wires a host through the production MongoDB outbox registration path for the given shape.</summary>
    private static ServiceCollection BuildMongoDbOutboxHost(MongoConnectionShape shape)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(A.Fake<IMongoClient>());
        _ = services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseMongoDB(mongo =>
        {
            _ = mongo.DatabaseName("outbox_unused").CollectionName("outbox");
            _ = shape == MongoConnectionShape.ConnectionString
                ? mongo.ConnectionString(UnusedConnectionString)
                : mongo.ClientFactory(static sp => sp.GetRequiredService<IMongoClient>());
        })));

        return services;
    }
}
